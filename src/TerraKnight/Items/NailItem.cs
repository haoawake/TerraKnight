using System.Collections.Generic;
using HarmonyLib;
using TerraKnight.Terraria;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TerraKnight.Items
{
    /// <summary>
    /// The Knight's own nail as an item of the bag, his from the start and kept for good (it can't
    /// be sold, thrown, dropped or trashed; one gone missing comes back). In hand, the left button is
    /// the game's own nail: held, it swings as fast as the nail's cooldown lets it, up or down with
    /// W or S, by the game's own slashes, so it parries what the nail parries (Revek in the Spirits'
    /// Glade), bounces off spikes and enemies on a down slash, hits levers and walls. Its hits deal
    /// Terraria damage like every weapon's (<see cref="NailHit"/>): its stage's damage, raised by each of
    /// the Nailsmith's upgrades (<see cref="DamageNow"/>).
    /// </summary>
    [HarmonyPatch]
    internal static class NailItem
    {
        public const string Icon = "HK_Nail";

        /// <summary>The Old Nail's damage at each stage: a melee weapon of that stage, a quick one.</summary>
        static readonly int[] DamageByStage = { 14, 26, 42, 60, 85, 125 };

        /// <summary>The Nailsmith's upgrades (Sharpened, Channelled, Coiled, Pure Nail): a quarter more each.</summary>
        public const float PerUpgrade = 0.25f;

        /// <summary>Its damage now: the stage's, times the upgrades'.</summary>
        public static int DamageNow()
        {
            var pd = PlayerData.instance;
            int upgrades = pd != null ? Mathf.Clamp(pd.nailSmithUpgrades, 0, 4) : 0;
            int stage = Mathf.Clamp(Shop.UnlockedStage(), 0, DamageByStage.Length - 1);
            return Mathf.RoundToInt(DamageByStage[stage] * (1f + PerUpgrade * upgrades));
        }

        public static int DevHits, DevRecoilStops, DevIntercepted;
        public static string DevLastRefused = "";

        public static ItemDef Def() => new ItemDef
        {
            Type = ItemType.KnightNail, Name = "骨钉", Texture = Icon, Class = DamageClass.Melee, Damage = 5, Knockback = 2f,
            UseAnimation = 1f, UseTime = 1f, MaxStack = 1, Rare = -11, Value = 0, Stage = 0, Source = ItemSource.Basic,
            NoMelee = true, NoUseGraphic = true, UseSound = null, Permanent = true, GameTool = true,
            Tooltip = "拿在手上时，左键就是空洞骑士的骨钉：按住连续挥砍，按住 W / S 上劈 / 下劈\n能格挡（比如安息之地的瑞维克），下劈能在尖刺和敌人身上弹起\n伤害随阶段提升，骨钉匠的每次升级再提高 25%\n不能出售、丢弃，也不能扔进垃圾桶",
        };

        public static bool Holding()
        {
            var mod = TerraMod.Instance;
            if (mod == null) return false;
            var def = mod.Inventory.Current.Def;
            return def != null && def.Type == ItemType.KnightNail;
        }

        /// <summary>Dev: the button held for a test (null: the real mouse).</summary>
        public static bool? DevHold;
        public static int DevSwings;

        static readonly System.Reflection.MethodInfo doAttack = AccessTools.Method(typeof(HeroController), "DoAttack");
        static readonly System.Reflection.MethodInfo canAttack = AccessTools.Method(typeof(HeroController), "CanAttack");
        static readonly System.Reflection.FieldInfo attackCooldown = AccessTools.Field(typeof(HeroController), "attack_cooldown");
        static float nextCheck;
        static bool suppressed;

        /// <summary>Each frame in play: the swing while the button is held, and the nail given back if it went missing, kept to one (<see cref="UniqueItems"/>).</summary>
        public static void Tick(TerraMod mod, HeroController hero)
        {
            EnsureIcon();
            if (hero == null) return;
            var def = ItemDb.Get(ItemType.KnightNail);
            var pd = PlayerData.instance;
            def.Damage = DamageNow();
            var mouse = Mouse.current;
            bool raw = DevHold ?? (!TerraMod.DevNoRealMouse && Player.TerraKeys.Held(Player.TerraKeys.Action.UseItem));
            if (!raw) suppressed = false;
            // A press that lands on the inventory or the hotbar is the interface's; one with a townsperson's head or the
            // housing query on the cursor puts it down (TownNpcs.PlaceClick), it doesn't swing.
            if (raw && !suppressed && Player.TerraKeys.Pressed(Player.TerraKeys.Action.UseItem) && Player.TerraKeys.IsMouse(Player.TerraKeys.Get(Player.TerraKeys.Action.UseItem))
                && (InventoryGui.MouseOverUi || UI.IngameOptions.MouseOver || Town.TownNpcs.Querying || Town.TownNpcs.Placing)) suppressed = true;
            // The inventory open, it swings as Terraria's held item does; an item on the cursor is the one used instead.
            if (raw && !suppressed && Holding() && GameHooks.PlayerHasControl() && !(InventoryGui.Open && !mod.Inventory.Mouse.IsEmpty) && (bool)canAttack.Invoke(hero, null))
            {
                // The Terraria settings' aim at the mouse: facing it, the game's way nearest it (NailAim).
                NailAim.BeforeAttack(hero);
                doAttack.Invoke(hero, null);
                // A reforged nail's speed (its use time against the plain one's) scales the wait before the next slash.
                var held = mod.Inventory.Current.Def;
                if (held?.Unprefixed != null && held.Unprefixed.UseAnimation > 0f && attackCooldown != null)
                    attackCooldown.SetValue(hero, (float)attackCooldown.GetValue(hero) * held.UseAnimation / held.Unprefixed.UseAnimation);
                DevSwings++;
            }
            if (Time.unscaledTime < nextCheck) return;
            nextCheck = Time.unscaledTime + 1f;
            if (UniqueItems.Keep(mod.Inventory, ItemType.KnightNail)) return;
            int left = mod.Inventory.Pickup(ItemType.KnightNail, 1);
            var at = (Vector2)hero.GetComponent<BoxCollider2D>().bounds.center;
            if (left > 0) WorldItems.Drop(ItemStack.Of(ItemType.KnightNail), at);
            Plugin.Log.LogInfo($"[骨钉] 给了骨钉{(left > 0 ? "（背包满了，放在脚下）" : "")}");
        }

        /// <summary>
        /// The Knight's own nail landing on an enemy (the Nail item in hand): the game's nail damage would
        /// come off the game's own health, a fixed share of the enemy's Terraria life whatever the stage.
        /// Instead the hit is the mod's, as every weapon's: the Nail item's damage, crits, knockback, the
        /// number. The slash, its pogo, its parry and the Soul it gives stay the game's. Before every other prefix of the
        /// hit (a LAN game's puppet would send the game's nail damage on to its owner, a share of the enemy's life).
        /// </summary>
        [HarmonyPrefix, HarmonyPriority(Priority.High)]
        [HarmonyPatch(typeof(HealthManager), nameof(HealthManager.Hit))]
        static bool NailHit(HealthManager __instance, HitInstance hitInstance)
        {
            if (!Converts(__instance, hitInstance)) return true;
            var mod = TerraMod.Instance;
            var hero = HeroController.SilentInstance;
            var col = __instance.GetComponent<Collider2D>() ?? __instance.GetComponentInChildren<Collider2D>();
            if (col == null) return true;
            // The slash strikes again every physics step it stays on the enemy (DamageEnemies.FixedUpdate), the game's own
            // evasion after a hit keeping that to one; the hit being the mod's, one a slash is kept here.
            DevLog?.Invoke($"{hitInstance.Source.name} → {__instance.name} 第 {Time.frameCount} 帧 物理 {Time.fixedTime:F2}");
            if (!FirstStrike(hitInstance.Source, __instance)) return false;
            ItemDb.Get(ItemType.KnightNail).Damage = DamageNow();
            // The nail in hand: a reforged one hits with its prefix.
            var def = mod.Inventory.Current.Def;
            Vector2 from = hero.GetComponent<BoxCollider2D>().bounds.center;
            float facing = col.bounds.center.x >= from.x ? 1f : -1f;
            // The slash hears that it struck (its "DEALT DAMAGE"), as from the game's own hit.
            FSMUtility.SendEventToGameObject(hitInstance.Source, "DEALT DAMAGE");
            DevIntercepted++;
            if (mod.Melee.HitWith(col, Equipment.Damage(def), def.Knockback, Equipment.Crit(def), facing, from))
            {
                // The upward slash knocks the enemy up (Terraria's push is sideways only).
                if (Mathf.Abs(Mathf.DeltaAngle(hitInstance.Direction, 90f)) < 30f) TerraKnockback.PushUp(__instance, Equipment.Knockback(def));
                hero.SoulGain();
                DevHits++;
            }
            else DevLastRefused = $"{__instance.name}：场地外 {World.ArenaGuard.Outside(__instance)}，唤醒中 {BossWake.Asleep(__instance)}";
            return false;
        }

        /// <summary>A hit this turns into the Nail item's own: the Knight's nail (a slash of his) with the Nail in hand, on a live enemy.</summary>
        public static bool Converts(HealthManager hm, HitInstance hit)
        {
            var hero = HeroController.SilentInstance;
            if (TerraMod.Instance == null || hero == null || hit.AttackType != AttackTypes.Nail || hit.Source == null) return false;
            if (!hit.Source.transform.IsChildOf(hero.transform) || !Holding()) return false;
            return hm != null && !hm.isDead && hit.DamageDealt > 0;
        }

        /// <summary>What each of the Knight's slashes has struck since it began (NailSlash.StartSlash).</summary>
        static readonly Dictionary<GameObject, HashSet<HealthManager>> struck = new Dictionary<GameObject, HashSet<HealthManager>>();

        /// <summary>When something of the nail's that isn't a begun slash last struck each enemy.</summary>
        static readonly Dictionary<HealthManager, float> struckElse = new Dictionary<HealthManager, float>();

        /// <summary>Dev: hits refused as the same slash's again.</summary>
        public static int DevRepeatsRefused;

        /// <summary>Dev: when set, hears every hit of the nail's that reaches an enemy (before the one-a-slash rule).</summary>
        public static System.Action<string> DevLog;

        [HarmonyPostfix]
        [HarmonyPatch(typeof(NailSlash), nameof(NailSlash.StartSlash))]
        static void NewSlash(NailSlash __instance)
        {
            if (struck.Count > 16) struck.Clear();
            if (!struck.TryGetValue(__instance.gameObject, out var set)) struck[__instance.gameObject] = set = new HashSet<HealthManager>();
            set.Clear();
        }

        /// <summary>The slash's first hit on the enemy; anything else of the nail's, once in the game's evasion time (0.2 s).</summary>
        static bool FirstStrike(GameObject source, HealthManager hm)
        {
            bool first;
            if (struck.TryGetValue(source, out var set)) first = set.Add(hm);
            else
            {
                first = !(struckElse.TryGetValue(hm, out float at) && Time.time - at < 0.2f);
                if (first)
                {
                    if (struckElse.Count > 64) struckElse.Clear();
                    struckElse[hm] = Time.time;
                }
            }
            if (!first) DevRepeatsRefused++;
            return first;
        }

        /// <summary>
        /// The nail's recoil off what it strikes ends when the game ends it (CancelRecoilHorizontal). With
        /// Terraria's movement the recoil's speed would carry on and slide the player far back, the run
        /// taking it off only a little each tick: the horizontal speed stops there, and the keys take over.
        /// </summary>
        /// <summary>A reforged nail's size (Large, Massive…) on the Knight's slash, over the charms' own scale.</summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(NailSlash), nameof(NailSlash.StartSlash))]
        static void SlashSize(NailSlash __instance)
        {
            var hero = HeroController.SilentInstance;
            var mod = TerraMod.Instance;
            if (hero == null || mod == null || !Holding() || !__instance.transform.IsChildOf(hero.transform)) return;
            var held = mod.Inventory.Current.Def;
            if (held?.Unprefixed == null || held.Unprefixed.Scale <= 0f) return;
            float k = held.Scale / held.Unprefixed.Scale;
            var s = __instance.transform.localScale;
            __instance.transform.localScale = new Vector3(s.x * k, s.y * k, s.z);
            DevSlashScale = k;
        }

        /// <summary>Dev: the last slash's prefix size.</summary>
        public static float DevSlashScale = 1f;

        [HarmonyPostfix]
        [HarmonyPatch(typeof(HeroController), "CancelRecoilHorizontal")]
        static void StopAfterRecoil(Rigidbody2D ___rb2d)
        {
            if (!Player.TerraMovement.Enabled || TerraMod.Instance == null || ___rb2d == null) return;
            ___rb2d.linearVelocity = new Vector2(0f, ___rb2d.linearVelocity.y);
            DevRecoilStops++;
        }

        /// <summary>The game's own nail checks (CanAttack) run while it is in hand; the Terraria hands keep them off otherwise.</summary>
        public static bool LetsAttack => TerraMod.Instance != null && Holding();

        // ------------------------------------------------------------------ its picture

        static bool iconFromGame;
        static float nextIconLook;

        /// <summary>The game's own nail picture (the inventory's), until found a grey stand-in.</summary>
        static void EnsureIcon()
        {
            if (iconFromGame) return;
            if (!TerrariaAssets.Has(Icon)) TerrariaAssets.Put(Icon, StandIn());
            if (Time.unscaledTime < nextIconLook) return;
            nextIconLook = Time.unscaledTime + 5f;
            Sprite best = null;
            int bestScore = 0;
            foreach (var s in Resources.FindObjectsOfTypeAll<Sprite>())
            {
                if (s == null) continue;
                string n = s.name.ToLowerInvariant();
                if (!n.Contains("nail") || n.Contains("dream") || n.Contains("art") || n.Contains("smith") || n.Contains("glow") || n.Contains("prompt") || n.Contains("master")) continue;
                int score = 1 + (n.Contains("inv") ? 4 : 0) + (n.Contains("icon") ? 2 : 0) + (n.EndsWith("_1") || n.EndsWith("1") ? 1 : 0);
                if (score > bestScore) { best = s; bestScore = score; }
            }
            if (best == null) return;
            var tex = DreamNailItem.Capture(best, 40, Icon);
            if (tex == null) return;
            TerrariaAssets.Put(Icon, tex);
            iconFromGame = true;
            Plugin.Log.LogInfo($"[骨钉] 图标用原版的 {best.name}");
        }

        static Texture2D StandIn()
        {
            var px = TerrariaAssets.Pixels("Item_4", out int w, out int h);
            for (int i = 0; i < px.Length; i++)
            {
                if (px[i].a == 0) continue;
                byte l = (byte)(150 + 80 * (px[i].r + px[i].g + px[i].b) / (3f * 255f));
                px[i] = new Color32(l, l, l, px[i].a);
            }
            return TerrariaAssets.Make(Icon + "#standin", px, w, h);
        }
    }
}
