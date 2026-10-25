using System.Collections;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// The Colosseum of Fools' summon. The colosseum's bosses are fought in its own trials (the old seals of its two bosses
    /// took the player to Godhome's copies, whose ending left a white screen): used anywhere in the world, this takes the
    /// player to the colosseum's hall by its door from Kingdom's Edge (Room_Colosseum_01, left1), where the trials are
    /// taken up; one is spent on arriving, none when it can't be used. It is the old Twin Acid Sacs' item
    /// (BossSigilOblobbles); a save's God Tamer's Beast Whistles come back as it (Save.ModSave.Unpack).
    /// </summary>
    internal static class ColosseumTicket
    {
        public const ItemType Type = ItemType.BossSigilOblobbles;
        public const string Scene = "Room_Colosseum_01", Gate = "left1";
        const int Stage = 4;

        /// <summary>Its picture: drawn as a seal would be, of the God Tamer's portrait, at its stage (BossSummons.Icon).</summary>
        internal static readonly BossSummons.Seal IconSeal = new BossSummons.Seal { Key = "LobsterLancer", Stage = Stage };

        static bool pending;
        public static int DevUsed, DevSpent;
        public static string DevLast = "";

        public static ItemDef Def() => new ItemDef
        {
            Type = Type, Name = "竞技场召唤物", Texture = BossSummons.IconPrefix + IconSeal.Key, Consumable = true, MaxStack = 9999,
            UseTime = 0.75f, UseAnimation = 0.75f, AutoReuse = false, NoMelee = true, NoUseGraphic = true, Rare = 5, Stage = Stage,
            Source = ItemSource.Craft, Value = 0, Price = 0,
            Tooltip = "使用后传送到愚人斗兽场门口\n斗兽场的试炼在那里报名\n到达时消耗1个；条件不符不消耗",
        };

        /// <summary>At an anvil, of the old Twin Acid Sacs' materials.</summary>
        public static Recipe Recipe() => new Recipe(Type, 1, "砧", Stage, (ItemType.CobaltOre, 12), (ItemType.Fireblossom, 4));

        static string Refusal()
        {
            var gm = GameManager.UnsafeInstance;
            var hero = HeroController.SilentInstance;
            if (pending || BossArena.Visiting) return Lang.T("正在传送");
            if (gm == null || hero == null || gm.IsInSceneTransition || !GameHooks.PlayerHasControl()) return Lang.T("现在无法使用");
            if (gm.sceneName.StartsWith("GG_") || gm.sceneName.StartsWith("Dream_")) return Lang.T("梦境和神居里不能使用");
            if (gm.sceneName.StartsWith("Room_Colosseum")) return Lang.T("已经在愚人斗兽场里");
            if (Shop.UnlockedStage() < Stage) return Lang.T("当前探索进度不足");
            return null;
        }

        /// <summary>Used from the hand: off to the colosseum's door, one spent once there.</summary>
        public static bool TryUse(Inventory inv)
        {
            string why = Refusal();
            if (inv == null || inv.Current.IsEmpty || inv.Current.Type != Type) why = Lang.T("请手持该召唤物");
            if (why != null)
            {
                DevLast = why;
                UI.CenterNotice.Show(why);
                return false;
            }
            var gm = GameManager.UnsafeInstance;
            var hero = HeroController.SilentInstance;
            pending = true;
            int slot = inv.Held;
            InventoryGui.Open = false;
            UI.HudDialogue.Close();
            World.PylonObject.TeleportEffect(hero);
            Player.DelicateFlower.OnTeleport();
            gm.BeginSceneTransition(new GameManager.SceneLoadInfo
            {
                SceneName = Scene, EntryGateName = Gate, HeroLeaveDirection = GlobalEnums.GatePosition.unknown, EntryDelay = 0f,
                WaitForSceneTransitionCameraFade = true, Visualization = GameManager.SceneLoadVisualizations.Default,
            });
            DevUsed++;
            DevLast = "去斗兽场";
            Plugin.Log.LogInfo($"[竞技场召唤物] 传送到 {Scene}（{Gate}）");
            TerraMod.Instance.StartCoroutine(SpendOnArrival(inv, slot));
            return true;
        }

        static IEnumerator SpendOnArrival(Inventory inv, int slot)
        {
            try
            {
                float until = Time.realtimeSinceStartup + 30f;
                while (Time.realtimeSinceStartup < until)
                {
                    var gm = GameManager.UnsafeInstance;
                    if (gm != null && !gm.IsInSceneTransition && gm.sceneName == Scene && HeroController.SilentInstance != null) break;
                    yield return null;
                }
                var at = GameManager.UnsafeInstance;
                if (at == null || at.sceneName != Scene) { DevLast = "没到斗兽场，没有消耗"; yield break; }
                // The stack it was used from, else another of it.
                int i = slot >= 0 && slot < inv.Slots.Length && !inv.Slots[slot].IsEmpty && inv.Slots[slot].Type == Type ? slot
                    : System.Array.FindIndex(inv.Slots, s => !s.IsEmpty && s.Type == Type);
                if (i < 0 || inv.Infinite) yield break;
                inv.Slots[i].Count--;
                if (inv.Slots[i].Count <= 0) inv.Slots[i] = default;
                DevSpent++;
                DevLast = "到了斗兽场";
            }
            finally { pending = false; }
        }
    }
}
