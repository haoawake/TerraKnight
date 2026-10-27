using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// The Nailmasters' arts with the Nail item (the user's): once one is learned (hasNailArt), the Nail in hand swings once
    /// a press (no repeat while held) and, held on, charges as the game's own nail does (1 s here, the game's 1.35; the
    /// Nailmaster's Glory 0.75 s); let go, the Knight's Nail Arts FSM does the art: the Great Slash standing or walking, the Cyclone Slash with
    /// up or down held, the Dash Slash during a dash (the Terraria dash counts, and the dash's end the FSM waits for is
    /// told at once). The game's own attack action is on for it, on the Use Item key, but only the Nail item's own swing
    /// may attack (aimed at the mouse, NailAim). Their hits are the Nail item's, as its slashes' are (NailItem.NailHit):
    /// the Great Slash and the Dash Slash 2.5 times a slash (once an art on each enemy), the Cyclone Slash a slash's every
    /// 0.2 s it stays on an enemy. Learned from the Nailmasters as in the game (hasCyclone, hasDashSlash: the Great Slash,
    /// hasUpwardSlash: the Dash Slash).
    /// </summary>
    [HarmonyPatch]
    internal static class NailArts
    {
        public const float GreatSlash = 2.5f, DashSlash = 2.5f, Cyclone = 1f;
        /// <summary>The charge without the Nailmaster's Glory (HeroController.NAIL_CHARGE_TIME_DEFAULT).</summary>
        public const float ChargeTime = 1f;
        static readonly System.Reflection.FieldInfo chargeTime = AccessTools.Field(typeof(HeroController), "nailChargeTime");
        public static int DevArts, DevDashSlashes;

        /// <summary>An art learned and the Nail in hand: the game's charge and arts run.</summary>
        public static bool Ready
        {
            get
            {
                var pd = PlayerData.instance;
                return NailItem.LetsAttack && pd != null && pd.hasNailArt;
            }
        }

        static PlayMakerFSM artsFsm;

        /// <summary>A nail art being done (the Knight's Nail Arts FSM past Inactive): it holds his control while it plays.</summary>
        public static bool Striking
        {
            get
            {
                if (!Ready) return false;
                var hero = HeroController.SilentInstance;
                if (hero == null) return false;
                if (artsFsm == null || artsFsm.gameObject != hero.gameObject)
                {
                    artsFsm = null;
                    foreach (var f in hero.GetComponents<PlayMakerFSM>()) if (f.FsmName == "Nail Arts") artsFsm = f;
                }
                return artsFsm != null && artsFsm.ActiveStateName != "Inactive";
            }
        }

        static float nextSpin;
        static readonly List<Collider2D> spinHits = new List<Collider2D>();
        static readonly HashSet<HealthManager> spun = new HashSet<HealthManager>();
        static readonly ContactFilter2D enemies = new ContactFilter2D { useTriggers = true, useLayerMask = true, layerMask = 1 << 11 };
        public static int DevSpinHits;

        /// <summary>
        /// Each frame: the charge time; and while the Cyclone Slash spins, every 0.2 s each enemy within its sweep is struck
        /// again, as the nail's own hit is (HitTaker.Hit → NailItem.NailHit). The game's own spin reports a hit only as a
        /// body comes into its blades, so one standing still (the Target Dummy) took a single hit.
        /// </summary>
        public static void Tick(HeroController hero)
        {
            var input = GameHooks.Input;
            if (DevUpDown || (input != null && (input.inputActions.up.IsPressed || input.inputActions.down.IsPressed))) upDownAt = Time.time;
            // The 360° setting: the Great Slash and the Dash Slash at the cursor (NailAim).
            NailAim.TickArts(hero);
            // The charge: 1 s (the user's; the game's 1.35), the Nailmaster's Glory's 0.75 as ever.
            if (hero != null && hero.NAIL_CHARGE_TIME_DEFAULT != ChargeTime)
            {
                hero.NAIL_CHARGE_TIME_DEFAULT = ChargeTime;
                var pd = PlayerData.instance;
                if (chargeTime != null && pd != null && !pd.GetBool("equippedCharm_26")) chargeTime.SetValue(hero, ChargeTime);
            }
            // The charm bag on: Nailmaster's Glory's charge (CharmEffects.ChargeTime), not the game's.
            if (hero != null && Charms.Enabled && chargeTime != null)
            {
                float want = CharmEffects.ChargeTime(ChargeTime);
                if ((float)chargeTime.GetValue(hero) != want) chargeTime.SetValue(hero, want);
            }
            if (hero != null && frontFor != hero.GetInstanceID()) Front(hero);
            if (hero == null || !Striking || artsFsm == null || artsFsm.ActiveStateName != "Cyclone Spin") return;
            if (Time.time < nextSpin) return;
            nextSpin = Time.time + 0.2f;
            if (cyclone == null || !cyclone.IsChildOf(hero.transform))
            {
                cyclone = null;
                foreach (var t in hero.GetComponentsInChildren<Transform>(true)) if (t.name == "Cyclone Slash") { cyclone = t; break; }
            }
            if (cyclone == null) return;
            // Quick Slash's echo and Grubberfly's beam for the sweep.
            CharmEffects.OnCycloneSweep(hero, cyclone);
            spun.Clear();
            spinHits.Clear();
            var box = hero.GetComponent<BoxCollider2D>();
            Vector2 c = box != null ? (Vector2)box.bounds.center : (Vector2)hero.transform.position;
            Physics2D.OverlapBox(c, SpinArea * CharmEffects.ArtReach, 0f, enemies, spinHits);
            foreach (var other in spinHits)
            {
                var hm = other != null ? other.GetComponentInParent<HealthManager>() : null;
                if (hm == null || hm.isDead || !spun.Add(hm)) continue;
                DevSpinHits++;
                HitTaker.Hit(hm.gameObject, new HitInstance
                {
                    Source = cyclone.gameObject,
                    AttackType = AttackTypes.Nail,
                    DamageDealt = 1,
                    Direction = hm.transform.position.x >= c.x ? 0f : 180f,
                    Multiplier = 1f,
                    MagnitudeMultiplier = 1f,
                });
            }
            DevSpinState = $"{artsFsm.ActiveStateName} 碰到 {spinHits.Count}";
        }

        /// <summary>When up or down was last held (the Cyclone Slash's keys), for a charge let go just before or after one.</summary>
        static float upDownAt = -10f;
        const float UpDownGrace = 0.25f;
        public static int DevCycloneGrace;
        public static string DevGraceWhy = "没进选招";
        internal static bool DevUpDown;

        /// <summary>
        /// The Nail Arts FSM choosing its art (Move Choice: its ListenForUp / ListenForDown): up or down held now or in the last
        /// UpDownGrace seconds is the Cyclone Slash whatever else is held (the user's 10-26: "按住三个键也能释放"); the
        /// listeners see it as held.
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(HutongGames.PlayMaker.Actions.ListenForUp), "CheckForInput")]
        static bool UpChoice(HutongGames.PlayMaker.Actions.ListenForUp __instance) => !CycloneGrace(__instance, __instance.isPressed);

        [HarmonyPrefix]
        [HarmonyPatch(typeof(HutongGames.PlayMaker.Actions.ListenForDown), "CheckForInput")]
        static bool DownChoice(HutongGames.PlayMaker.Actions.ListenForDown __instance) => !CycloneGrace(__instance, __instance.isPressed);

        static bool CycloneGrace(HutongGames.PlayMaker.FsmStateAction action, HutongGames.PlayMaker.FsmEvent pressed)
        {
            var st = action.State;
            if (st == null || st.Name != "Move Choice" || action.Fsm == null || action.Fsm.Name != "Nail Arts" || pressed == null) return false;
            var pd = PlayerData.instance;
            DevGraceWhy = $"选招：剑技就绪 {Ready}，学了旋风 {pd != null && pd.hasCyclone}，距上次按上下 {Time.time - upDownAt:F2} 秒";
            if (!Ready || pd == null || !pd.hasCyclone || Time.time - upDownAt > UpDownGrace) return false;
            DevCycloneGrace++;
            action.Fsm.Event(pressed);
            return true;
        }

        static int frontFor;
        public static int DevFronted;

        /// <summary>
        /// The Knight's slashes and arts drawn in front of everything, as the Terraria weapons' effects are (FxBatch.OnTopOrder,
        /// over the light map and the scenery in front): every renderer under his Attacks, and his effects named for a slash
        /// or the nail art's charge. Once a Knight.
        /// </summary>
        static void Front(HeroController hero)
        {
            frontFor = hero.GetInstanceID();
            DevFronted = 0;
            foreach (var r in hero.GetComponentsInChildren<Renderer>(true))
            {
                if (Under(r, hero.transform, "Attacks") || Under(r, hero.transform, "Effects") || Under(r, hero.transform, "Dream Effects")) Effects.NativeWeaponEffects.Track(r);
                if (!IsAttackPicture(r, hero.transform)) continue;
                ToFront(r);
            }
            // The nail's spark where it strikes terrain (the Knight's pooled nailTerrainImpactEffectPrefab, spawned at the wall):
            // the prefab, and the copies the pool has made of it already.
            var spark = hero.nailTerrainImpactEffectPrefab;
            if (spark != null)
            {
                foreach (var r in spark.GetComponentsInChildren<Renderer>(true)) ToFront(r);
                string copy = spark.name + "(Clone)";
                foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
                    if (t != null && t.name == copy && t.gameObject.scene.IsValid())
                        foreach (var r in t.GetComponentsInChildren<Renderer>(true)) ToFront(r);
            }
            Plugin.Log.LogInfo($"[骨钉] 挥砍、骨钉技艺和梦之钉的贴图放到最前面（{DevFronted} 个）");
        }

        /// <summary>The renderers put in front, by instance id: a tk2d sprite among them gets its order back as it first wakes.</summary>
        static readonly HashSet<int> fronted = new HashSet<int>();

        /// <summary>
        /// One of the Knight's effects in front of everything: the Terraria effects' sorting layer (Default) when its own is
        /// below it, then their order, on the renderer only. A tk2d sprite's own renderLayer stays as it is: tk2d moves the
        /// mesh's bounds by it (z −0.01 a step, AdjustedMeshBounds), so a large one puts the slash behind the camera, culled.
        /// </summary>
        static void ToFront(Renderer r)
        {
            if (r == null) return;
            int def = SortingLayer.NameToID("Default");
            if (SortingLayer.GetLayerValueFromID(r.sortingLayerID) < SortingLayer.GetLayerValueFromID(def)) r.sortingLayerID = def;
            r.sortingOrder = Effects.FxBatch.OnTopOrder + 2;
            fronted.Add(r.GetInstanceID());
            DevFronted++;
        }

        /// <summary>
        /// tk2dBaseSprite.Awake puts its renderLayer back on the renderer as the sprite first wakes: one put in front while it
        /// was still asleep (the Dream Nail's slash, until its first swing) gets its order again.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(tk2dBaseSprite), "Awake")]
        static void FrontAgain(tk2dBaseSprite __instance)
        {
            if (fronted.Count == 0 || __instance == null) return;
            var r = __instance.GetComponent<Renderer>();
            if (r != null && fronted.Contains(r.GetInstanceID())) { r.sortingOrder = Effects.FxBatch.OnTopOrder + 2; DevFrontedAgain++; }
        }

        public static int DevFrontedAgain;

        static bool Under(Renderer r, Transform hero, string name)
        {
            for (var t = r.transform; t != null && t != hero; t = t.parent) if (t.name == name) return true;
            return false;
        }

        /// <summary>
        /// One of the Knight's attack pictures: every renderer under his Attacks (the slashes, the arts), his effects named for a
        /// slash or the nail art's charge, and the Dream Nail's (under Dream Effects). Drawn in front (<see cref="Front"/>), and
        /// blue in Deep Focus's mode.
        /// </summary>
        public static bool IsAttackPicture(Renderer r, Transform hero)
        {
            if (r == null || hero == null) return false;
            if (Under(r, hero, "Attacks") || Under(r, hero, "Dream Effects")) return true;
            string n = r.name;
            return Under(r, hero, "Effects") && (n.Contains("Slash") || n.Contains("Charge") || n.StartsWith("NA "));
        }

        static Transform cyclone;
        /// <summary>The spin's reach round the Knight: its blades' sweep, two and a third cells each way, a cell up and down.</summary>
        static readonly Vector2 SpinArea = new Vector2(4.6f, 2f);

        public static string DevSpinState = "";

        /// <summary>The Nail item's tooltip: the arts learned and how each is done, or where they are learned.</summary>
        public static List<string> TooltipLines()
        {
            var lines = new List<string>();
            var pd = PlayerData.instance;
            if (pd == null) return lines;
            if (!pd.hasNailArt)
            {
                lines.Add(Lang.T("向三位骨钉大师学习骨钉技艺"));
                return lines;
            }
            string key = Player.TerraKeys.KeyName(Player.TerraKeys.Get(Player.TerraKeys.Action.UseItem));
            lines.Add(Lang.F("骨钉技艺：按住 {0} 蓄力，闪光后松开", key));
            if (pd.hasDashSlash) lines.Add(Lang.T("  强力斩：不按上下时松开，2.5 倍伤害"));
            if (pd.hasCyclone) lines.Add(Lang.T("  旋风斩：按住上或下时松开，转圈连续命中"));
            if (pd.hasUpwardSlash) lines.Add(Lang.T("  冲刺斩：冲刺中松开，2.5 倍伤害"));
            return lines;
        }

        /// <summary>The Nail item's own swing being made (NailItem.Tick): the only one CanAttack lets through while Ready.</summary>
        public static bool Manual;

        /// <summary>Which art a hit of the nail's comes from (its source or a parent named for it), null: a plain slash.</summary>
        public static string ArtOf(GameObject source)
        {
            var t = source != null ? source.transform : null;
            for (int k = 0; k < 4 && t != null; k++, t = t.parent)
            {
                string n = t.name;
                if (n.StartsWith("Great Slash")) return "Great Slash";
                if (n.StartsWith("Dash Slash")) return "Dash Slash";
                if (n.StartsWith("Cyclone Slash")) return "Cyclone Slash";
            }
            return null;
        }

        /// <summary>A hit's damage multiple: an art's, 1 for a slash.</summary>
        public static float Multiplier(string art) => art == "Great Slash" ? GreatSlash : art == "Dash Slash" ? DashSlash : art == "Cyclone Slash" ? Cyclone : 1f;

        static readonly Dictionary<HealthManager, float> artHit = new Dictionary<HealthManager, float>();

        /// <summary>The Great Slash and the Dash Slash strike each enemy once an art (their hitboxes stay out a while).</summary>
        public static bool FirstOfArt(string art, HealthManager hm)
        {
            if (art != "Great Slash" && art != "Dash Slash") return true;
            if (artHit.TryGetValue(hm, out float at) && Time.time - at < 0.6f) return false;
            if (artHit.Count > 64) artHit.Clear();
            artHit[hm] = Time.time;
            DevArts++;
            return true;
        }

        /// <summary>
        /// The Nail Arts FSM asks whether the Knight is dashing when the button is let go: the Terraria dash (the Shield of
        /// Cthulhu's) counts while the Nail is in hand, and the FSM's wait for the game's DASH END is ended at once.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(HeroController), nameof(HeroController.GetState))]
        static void TerrariaDash(HeroController __instance, string stateName, ref bool __result)
        {
            // Both Terraria shield dashes and the adapted Shade Cloak dash are driven by
            // TerraMovement, so neither sets the native hero's dashing flag by itself. Only with the Dash Slash learned: the
            // FSM's dash branch cancels the art without it, and a let-go charge while running on a dash did nothing (the
            // user's 10-26 "旋风斩…必须松开方向键").
            var pd = PlayerData.instance;
            if (__result || stateName != "dashing" || !Ready || !Player.TerraMovement.IsDashing || pd == null || !pd.hasUpwardSlash) return;
            __result = true;
            DevDashSlashes++;
            TerraMod.Instance?.StartCoroutine(EndDash(__instance));
        }

        static IEnumerator EndDash(HeroController hero)
        {
            for (int i = 0; i < 3; i++)
            {
                yield return null;
                if (hero == null) yield break;
                foreach (var fsm in hero.GetComponents<PlayMakerFSM>())
                    if (fsm.FsmName == "Nail Arts" && fsm.ActiveStateName == "Dash Slash Ready")
                    {
                        fsm.SendEvent("DASH END");
                        yield break;
                    }
            }
        }
    }
}
