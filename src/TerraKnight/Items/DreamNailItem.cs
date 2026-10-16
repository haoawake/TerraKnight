using HarmonyLib;
using HutongGames.PlayMaker.Actions;
using TerraKnight.Terraria;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TerraKnight.Items
{
    /// <summary>
    /// The Dream Nail as an item of the bag: given once the Knight has it, and kept for good (it
    /// can't be sold, thrown, dropped or trashed; one gone missing comes back). In hand, the left
    /// or the right button is the game's own Dream Nail button: held it charges, let go it slashes,
    /// with S or W held it sets or takes the Dreamgate, all by the game's Dream Nail FSM and its
    /// animations. Its slash also deals damage, a melee hit as strong as the progress reached
    /// (<see cref="Shop.UnlockedStage"/>), to whatever it reads. In a Dreamer's dream, once the
    /// Dreamer is struck down, its button held is the Knight's focus that drains the Dreamer.
    /// </summary>
    [HarmonyPatch]
    internal static class DreamNailItem
    {
        public const string Icon = "HK_DreamNail";
        /// <summary>Its damage at each stage: about three swings of a melee weapon of that stage (it charges a second and more).</summary>
        static readonly int[] DamageByStage = { 40, 75, 120, 150, 200, 300 };

        /// <summary>The Awoken Dream Nail (the Seer's reward for 1800 Essence): half as much again.</summary>
        public const float Awoken = 1.5f;

        /// <summary>Its damage now: the stage's, half as much again once awoken.</summary>
        public static int DamageNow()
        {
            int stage = Mathf.Clamp(Shop.UnlockedStage(), 0, DamageByStage.Length - 1);
            var pd = PlayerData.instance;
            return Mathf.RoundToInt(DamageByStage[stage] * (pd != null && pd.dreamNailUpgraded ? Awoken : 1f));
        }

        public static ItemDef Def() => new ItemDef
        {
            Type = ItemType.DreamNail, Name = "梦之钉", Texture = Icon, Class = DamageClass.Melee, Damage = DamageByStage[0], Knockback = 2f,
            UseAnimation = 1f, UseTime = 1f, MaxStack = 1, Rare = -11, Value = 0, Stage = 0, Source = ItemSource.Basic,
            NoMelee = true, NoUseGraphic = true, UseSound = null, Permanent = true, GameTool = true,
            Tooltip = "拿在手上时，左键或右键就是空洞骑士的梦之钉：按住蓄力，松开挥出\n能读出生物的想法，进入梦境，伤害随进度提升，觉醒后再提高 50%\n按住 S 挥出放下梦之门，按住 W 挥出传送到梦之门\n在守梦人的梦里打伤守梦人后，按住左键或右键就是聚集，吸收守梦人\n不能出售、丢弃，也不能扔进垃圾桶\n背包满时安全保管在圣巢背包中",
        };

        public static bool Holding()
        {
            var mod = TerraMod.Instance;
            if (mod == null) return false;
            var def = mod.Inventory.Current.Def;
            return def != null && def.Type == ItemType.DreamNail;
        }

        // ------------------------------------------------------------------ the button

        static int sampled = -1;
        static bool down, pressed, released, suppressed;
        /// <summary>Dev: the button held for a test (null: the real mouse).</summary>
        public static bool? DevHold;

        /// <summary>Once a frame: the Dream Nail button is either mouse button, pressed with it in hand and not on the interface.</summary>
        public static void Sample()
        {
            if (sampled == Time.frameCount) return;
            sampled = Time.frameCount;
            var mouse = Mouse.current;
            bool raw = DevHold ?? (!TerraMod.DevNoRealMouse && (Player.TerraKeys.Held(Player.TerraKeys.Action.UseItem) || Player.TerraKeys.Held(Player.TerraKeys.Action.Interact)));
            if (!raw) suppressed = false;
            bool btn = raw && !suppressed && Holding();
            // A press that lands on the inventory or the hotbar is the interface's.
            if (btn && !down && (InventoryGui.MouseOverUi || UI.IngameOptions.MouseOver)) { btn = false; suppressed = true; }
            pressed = btn && !down;
            released = !btn && down;
            down = btn;
        }

        /// <summary>The click went to something else (a townsperson talked to, a chest opened): no Dream Nail until the buttons are let go.</summary>
        public static void Cancel()
        {
            if (!down && !pressed) { suppressed = true; return; }
            suppressed = true;
            down = false;
            pressed = false;
            released = true;
        }

        /// <summary>The game's Dream Nail button listener, fed from the item's button instead of the (switched off) key.</summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(ListenForDreamNail), nameof(ListenForDreamNail.OnUpdate))]
        static bool Listen(ListenForDreamNail __instance)
        {
            if (TerraMod.Instance == null) return true;
            var gm = GameManager.UnsafeInstance;
            if (gm == null || gm.isPaused || !(__instance.activeBool.Value || __instance.activeBool.IsNone)) return false;
            Sample();
            var fsm = __instance.Fsm;
            // Draining a Dreamer: the button is the focus (ListenFocus), not a slash.
            if (DreamFocus())
            {
                fsm.Event(__instance.isNotPressed);
                return false;
            }
            if (pressed) fsm.Event(__instance.wasPressed);
            if (released) fsm.Event(__instance.wasReleased);
            fsm.Event(down ? __instance.isPressed : __instance.isNotPressed);
            return false;
        }

        // ------------------------------------------------------------------ the Dreamers' focus

        static PlayMakerFSM spellFsm;
        static HeroController spellOf;

        static PlayMakerFSM SpellFsm(HeroController hero)
        {
            if (hero == null) return null;
            if (spellOf == hero && spellFsm != null) return spellFsm;
            spellOf = hero;
            spellFsm = null;
            foreach (var f in hero.GetComponents<PlayMakerFSM>())
                if (f.FsmName == "Spell Control") { spellFsm = f; break; }
            return spellFsm;
        }

        /// <summary>
        /// A Dreamer struck down in its dream: the game sets the Knight's Spell Control "Dream Focus"
        /// and waits for the focus that drains the Dreamer (Dreamer NPC: DREAM FOCUS START).
        /// </summary>
        public static bool DreamerWounded()
        {
            var fsm = SpellFsm(HeroController.SilentInstance);
            var b = fsm != null ? fsm.FsmVariables.FindFsmBool("Dream Focus") : null;
            return b != null && b.Value;
        }

        /// <summary>
        /// The Knight's focus is gone with the rest of his spells (Terraria heals with potions); with a
        /// Dreamer struck down and the Dream Nail in hand, the Dream Nail's button is that focus.
        /// </summary>
        public static bool DreamFocus() => TerraMod.Instance != null && Holding() && DreamerWounded();

        /// <summary>The game's focus button listener (its key is switched off), fed from the Dream Nail's button while draining a Dreamer.</summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(ListenForCast), nameof(ListenForCast.CheckForInput))]
        static bool ListenFocus(ListenForCast __instance)
        {
            if (!DreamFocus()) return true;
            var gm = GameManager.UnsafeInstance;
            if (gm == null || gm.isPaused || !(__instance.activeBool.IsNone || __instance.activeBool.Value)) return false;
            Sample();
            var fsm = __instance.Fsm;
            if (pressed) fsm.Event(__instance.wasPressed);
            if (released) fsm.Event(__instance.wasReleased);
            fsm.Event(down ? __instance.isPressed : __instance.isNotPressed);
            return false;
        }

        static float nextHint;

        /// <summary>While a struck-down Dreamer waits to be drained: what to press, every few seconds until the button is held.</summary>
        static void DreamFocusHint()
        {
            if (!DreamerWounded()) { nextHint = 0f; return; }
            if (down && Holding()) { nextHint = Time.unscaledTime + 4f; return; }
            if (Time.unscaledTime < nextHint) return;
            nextHint = Time.unscaledTime + 6f;
            UI.CenterNotice.Show(Lang.T(Holding() ? "按住左键或右键聚集，吸收守梦人" : "拿起梦之钉，按住左键或右键聚集"));
        }

        // ------------------------------------------------------------------ standing still

        static PlayMakerFSM dreamFsm;
        static HeroController fsmOf;
        static float heldGravity = float.NaN;

        static PlayMakerFSM DreamFsm(HeroController hero)
        {
            if (fsmOf == hero && dreamFsm != null) return dreamFsm;
            fsmOf = hero;
            dreamFsm = null;
            foreach (var f in hero.GetComponents<PlayMakerFSM>())
                if (f.FsmName == "Dream Nail") { dreamFsm = f; break; }
            return dreamFsm;
        }

        /// <summary>
        /// The FSM's states in which the Knight stands still: its charge and slash (Start sets the
        /// velocity to nothing) and the Dreamgate's, whose charge is cancelled by any velocity at all
        /// (FloatCompare against 0 with no tolerance).
        /// </summary>
        static bool Still(string state) => state != null && (state == "Start" || state == "Take Control" || state.Contains("Charge") || state.StartsWith("Set")
            || state == "Dream Gate?" || state == "Can Set?" || state == "Can Warp?" || state == "Spawn Gate" || state == "Slash Antic" || state == "Slash");

        /// <summary>
        /// After every other step of the hero's (Terraria's gravity among them): held still on the
        /// ground while the Dream Nail charges, gravity off so the physics adds nothing either.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last)]
        [HarmonyPatch(typeof(HeroController), "FixedUpdate")]
        static void HoldStill(HeroController __instance, Rigidbody2D ___rb2d)
        {
            if (TerraMod.Instance == null || ___rb2d == null) return;
            var fsm = DreamFsm(__instance);
            bool still = fsm != null && __instance.cState.onGround && !__instance.cState.dead && Still(fsm.ActiveStateName);
            if (still)
            {
                if (float.IsNaN(heldGravity)) heldGravity = ___rb2d.gravityScale;
                ___rb2d.gravityScale = 0f;
                ___rb2d.linearVelocity = Vector2.zero;
            }
            else if (!float.IsNaN(heldGravity))
            {
                ___rb2d.gravityScale = heldGravity;
                heldGravity = float.NaN;
            }
        }

        // ------------------------------------------------------------------ the damage

        static readonly AccessTools.FieldRef<EnemyDreamnailReaction, int> reactionState = AccessTools.FieldRefAccess<EnemyDreamnailReaction, int>("state");
        /// <summary>EnemyDreamnailReaction.States.Ready.</summary>
        const int Ready = 1;

        public static int DevHits;
        public static string DevLastHit = "";

        /// <summary>
        /// The Dream Nail's slash reading an enemy (SendDreamImpact, on the slash's own FSM): when the
        /// game takes the impact (its reaction ready, not cooling down), the slash's damage goes in too.
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(SendDreamImpact), nameof(SendDreamImpact.OnEnter))]
        static void Impact(SendDreamImpact __instance)
        {
            var mod = TerraMod.Instance;
            var hero = HeroController.SilentInstance;
            if (mod == null || hero == null) return;
            var owner = __instance.Fsm?.GameObject;
            if (owner == null || !owner.transform.IsChildOf(hero.transform)) return;
            var target = __instance.target.GetSafe(__instance);
            if (target == null) return;
            var reaction = target.GetComponent<EnemyDreamnailReaction>();
            if (reaction == null)
            {
                reaction = target.GetComponentInParent<EnemyDreamnailReaction>();
                if (reaction != null && !reaction.allowUseChildColliders) reaction = null;
            }
            if (reaction == null || reactionState(reaction) != Ready) return;
            var hm = reaction.GetComponent<HealthManager>() ?? reaction.GetComponentInParent<HealthManager>();
            if (hm == null || hm.isDead) return;
            var col = hm.GetComponent<Collider2D>() ?? target.GetComponent<Collider2D>();
            if (col == null) return;
            // The Dream Nail in hand: a reforged one hits with its prefix.
            var def = mod.Inventory.Current.Type == ItemType.DreamNail ? mod.Inventory.Current.Def : ItemDb.Get(ItemType.DreamNail);
            Vector2 from = hero.GetComponent<BoxCollider2D>().bounds.center;
            float facing = col.bounds.center.x >= from.x ? 1f : -1f;
            mod.Melee.HitWith(col, Equipment.Damage(def), def.Knockback, Equipment.Crit(def), facing, from);
            DevHits++;
            DevLastHit = $"{hm.gameObject.scene.name}/{hm.name}";
        }

        // ------------------------------------------------------------------ kept for good

        static float nextCheck;

        /// <summary>
        /// Each second in play: its damage set by the progress reached, and, with the Knight's Dream
        /// Nail got, the item given if the player has it nowhere, and kept to one (<see cref="UniqueItems"/>).
        /// </summary>
        public static void Tick(TerraMod mod, HeroController hero)
        {
            EnsureIcon();
            DreamFocusHint();
            if (Time.unscaledTime < nextCheck) return;
            nextCheck = Time.unscaledTime + 1f;
            var def = ItemDb.Get(ItemType.DreamNail);
            def.Damage = DamageNow();
            var pd = PlayerData.instance;
            if (pd == null || !pd.hasDreamNail || hero == null || UniqueItems.Keep(mod.Inventory, ItemType.DreamNail, includeGround: false)) return;
            // The pickup's cutscene can leave this scene immediately. A drop in that old scene
            // is not an owned Dream Nail: recover the actual stack (including its prefix/forge).
            var stack = WorldItems.PersonalStack(ItemType.DreamNail);
            if (stack.IsEmpty) stack = ItemStack.Of(ItemType.DreamNail);
            stack.Count = 1;
            int left = mod.Inventory.Pickup(stack, stack.Favorite);
            bool stored = left > 0 && TryStoreInBag(mod.Inventory, stack);
            if (left > 0 && !stored) return; // Retain the entitlement and retry; never spill a permanent quest tool.
            UniqueItems.Keep(mod.Inventory, ItemType.DreamNail);
            var at = (Vector2)hero.GetComponent<BoxCollider2D>().bounds.center;
            UI.PopupText.Show(def, 1, at);
            if (stored) UI.CenterNotice.Show(Lang.T("背包已满，梦之钉已放入圣巢背包"));
            mod.PlaySound("Grab");
            Plugin.Log.LogInfo($"[梦之钉] 给了梦之钉{(stored ? "（背包满了，已放入圣巢背包）" : "")}");
        }

        /// <summary>A saved reserved slot, also used when closing a full inventory with the tool on the cursor.</summary>
        internal static bool TryStoreInBag(Inventory inv, ItemStack stack)
        {
            if (stack.IsEmpty || stack.Type != ItemType.DreamNail) return false;
            if (!inv.HkBag[HkItems.DreamNailSlot].IsEmpty) return false;
            inv.HkBag[HkItems.DreamNailSlot] = stack;
            return true;
        }

        // ------------------------------------------------------------------ its picture

        static bool iconFromGame;
        static float nextIconLook;

        /// <summary>
        /// The game's own Dream Nail picture (the inventory's), made into a small texture under
        /// <see cref="Icon"/>; until it is found, a pale stand-in.
        /// </summary>
        public static void EnsureIcon()
        {
            if (iconFromGame) return;
            if (!TerrariaAssets.Has(Icon)) TerrariaAssets.Put(Icon, StandIn());
            if (Time.unscaledTime < nextIconLook) return;
            nextIconLook = Time.unscaledTime + 5f;
            var sprite = FindSprite(out string candidates);
            if (candidates.Length > 0) Plugin.Log.LogInfo($"[梦之钉] 名字里带 dream 的图：{candidates}");
            if (sprite == null) return;
            var tex = Capture(sprite, 40);
            if (tex == null) return;
            TerrariaAssets.Put(Icon, tex);
            iconFromGame = true;
            Plugin.Log.LogInfo($"[梦之钉] 图标用原版的 {sprite.name}（{sprite.rect.width}x{sprite.rect.height} → {tex.width}x{tex.height}）");
        }

        static Sprite FindSprite(out string candidates)
        {
            Sprite best = null;
            int bestScore = 0;
            var names = new System.Text.StringBuilder();
            foreach (var s in Resources.FindObjectsOfTypeAll<Sprite>())
            {
                if (s == null) continue;
                string n = s.name.ToLowerInvariant();
                if (!n.Contains("dream")) continue;
                if (names.Length < 800) names.Append(s.name).Append(' ');
                if (!n.Contains("nail") || n.Contains("upgrade") || n.Contains("prompt") || n.Contains("glow") || n.Contains("dialogue") || n.Contains("backboard") || n.Contains("msg")) continue;
                int score = 1 + (n.Contains("inv") ? 4 : 0) + (n.Contains("icon") ? 2 : 0);
                if (score > bestScore) { best = s; bestScore = score; }
            }
            candidates = names.ToString();
            return best;
        }

        /// <summary>A sprite's picture (it may sit in an atlas that can't be read) drawn into a texture of the given height.</summary>
        internal static Texture2D Capture(Sprite s, int height, string name = Icon)
        {
            var src = s.texture;
            if (src == null) return null;
            var r = s.textureRect;
            int h = Mathf.Max(1, height), w = Mathf.Max(1, Mathf.RoundToInt(height * r.width / r.height));
            var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32);
            var prev = RenderTexture.active;
            try
            {
                Graphics.Blit(src, rt, new Vector2(r.width / src.width, r.height / src.height), new Vector2(r.x / src.width, r.y / src.height));
                RenderTexture.active = rt;
                var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = name, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                tex.Apply(false, false);
                return tex;
            }
            finally
            {
                RenderTexture.active = prev;
                RenderTexture.ReleaseTemporary(rt);
            }
        }

        /// <summary>Until the game's picture is found: the Iron Broadsword's shape in the Dream Nail's pale violet.</summary>
        static Texture2D StandIn()
        {
            var px = TerrariaAssets.Pixels("Item_4", out int w, out int h);
            for (int i = 0; i < px.Length; i++)
            {
                if (px[i].a == 0) continue;
                float l = (px[i].r + px[i].g + px[i].b) / (3f * 255f);
                px[i] = new Color32((byte)(200 + 55 * l), (byte)(180 + 60 * l), 255, px[i].a);
            }
            return TerrariaAssets.Make(Icon + "#standin", px, w, h);
        }
    }
}
