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
            bool raw = DevHold ?? (!TerraMod.DevNoRealMouse && mouse != null && mouse.leftButton.isPressed);
            if (!raw) suppressed = false;
            // A press that lands on the inventory or the hotbar is the interface's.
            if (raw && !suppressed && mouse != null && mouse.leftButton.wasPressedThisFrame && (InventoryGui.MouseOverUi || UI.IngameOptions.MouseOver)) suppressed = true;
            if (raw && !suppressed && Holding() && GameHooks.PlayerHasControl() && !InventoryGui.Open && (bool)canAttack.Invoke(hero, null))
            {
                doAttack.Invoke(hero, null);
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
        /// number. The slash, its pogo, its parry and the Soul it gives stay the game's.
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(HealthManager), nameof(HealthManager.Hit))]
        static bool NailHit(HealthManager __instance, HitInstance hitInstance)
        {
            var mod = TerraMod.Instance;
            var hero = HeroController.SilentInstance;
            if (mod == null || hero == null || hitInstance.AttackType != AttackTypes.Nail || hitInstance.Source == null) return true;
            if (!hitInstance.Source.transform.IsChildOf(hero.transform) || !Holding()) return true;
            if (__instance == null || __instance.isDead || hitInstance.DamageDealt <= 0) return true;
            var col = __instance.GetComponent<Collider2D>() ?? __instance.GetComponentInChildren<Collider2D>();
            if (col == null) return true;
            var def = ItemDb.Get(ItemType.KnightNail);
            def.Damage = DamageNow();
            Vector2 from = hero.GetComponent<BoxCollider2D>().bounds.center;
            float facing = col.bounds.center.x >= from.x ? 1f : -1f;
            // The slash hears that it struck (its "DEALT DAMAGE"), as from the game's own hit.
            FSMUtility.SendEventToGameObject(hitInstance.Source, "DEALT DAMAGE");
            DevIntercepted++;
            if (mod.Melee.HitWith(col, Equipment.Damage(def), def.Knockback, Equipment.Crit(def), facing, from))
            {
                hero.SoulGain();
                DevHits++;
            }
            else DevLastRefused = $"{__instance.name}：场地外 {World.ArenaGuard.Outside(__instance)}，唤醒中 {BossWake.Asleep(__instance)}";
            return false;
        }

        /// <summary>
        /// The nail's recoil off what it strikes ends when the game ends it (CancelRecoilHorizontal). With
        /// Terraria's movement the recoil's speed would carry on and slide the player far back, the run
        /// taking it off only a little each tick: the horizontal speed stops there, and the keys take over.
        /// </summary>
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
