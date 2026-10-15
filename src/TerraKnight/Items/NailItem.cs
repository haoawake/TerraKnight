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
    /// Glade), bounces off spikes and enemies on a down slash, hits levers and walls, and deals the
    /// nail's damage (the Nailsmith's upgrades count).
    /// </summary>
    internal static class NailItem
    {
        public const string Icon = "HK_Nail";

        public static ItemDef Def() => new ItemDef
        {
            Type = ItemType.KnightNail, Name = "骨钉", Texture = Icon, Class = DamageClass.Melee, Damage = 5, Knockback = 2f,
            UseAnimation = 1f, UseTime = 1f, MaxStack = 1, Rare = -11, Value = 0, Stage = 0, Source = ItemSource.Basic,
            NoMelee = true, NoUseGraphic = true, UseSound = null, Permanent = true, GameTool = true,
            Tooltip = "拿在手上时，左键就是空洞骑士的骨钉：按住连续挥砍，按住 W / S 上劈 / 下劈\n能格挡（比如安息之地的瑞维克），下劈能在尖刺和敌人身上弹起\n伤害是原版骨钉的，骨钉匠的升级照样有用\n不能出售、丢弃，也不能扔进垃圾桶",
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

        /// <summary>Each frame in play: the swing while the button is held, and the nail given back if it went missing.</summary>
        public static void Tick(TerraMod mod, HeroController hero)
        {
            EnsureIcon();
            if (hero == null) return;
            var def = ItemDb.Get(ItemType.KnightNail);
            var pd = PlayerData.instance;
            if (pd != null) def.Damage = Mathf.Max(1, pd.nailDamage);
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
            if (Owned(mod)) return;
            int left = mod.Inventory.Pickup(ItemType.KnightNail, 1);
            var at = (Vector2)hero.GetComponent<BoxCollider2D>().bounds.center;
            if (left > 0) WorldItems.Drop(ItemStack.Of(ItemType.KnightNail), at);
            Plugin.Log.LogInfo($"[骨钉] 给了骨钉{(left > 0 ? "（背包满了，放在脚下）" : "")}");
        }

        /// <summary>The game's own nail checks (CanAttack) run while it is in hand; the Terraria hands keep them off otherwise.</summary>
        public static bool LetsAttack => TerraMod.Instance != null && Holding();

        static bool Owned(TerraMod mod)
        {
            var inv = mod.Inventory;
            if (inv.Mouse.Type == ItemType.KnightNail && !inv.Mouse.IsEmpty) return true;
            foreach (var s in inv.Slots) if (s.Type == ItemType.KnightNail && !s.IsEmpty) return true;
            foreach (var l in inv.Loadouts)
                if (l.Bag != null) foreach (var s in l.Bag) if (s.Type == ItemType.KnightNail && !s.IsEmpty) return true;
            if (World.Chests.Holds(ItemType.KnightNail)) return true;
            return WorldItems.Lying(ItemType.KnightNail);
        }

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
