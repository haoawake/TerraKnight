using System.Collections.Generic;

namespace TerraKnight.Items
{
    /// <summary>
    /// The buff stations Terraria has the player touch (the Crystal Ball, the Ammo Box, the Bewitching Table, the Sharpening
    /// Station, the War Table, the Slice of Cake: Player.TileInteractionsUse's AddBuff) give their buff while one is carried
    /// in the bag or lies in the Piggy Bank, held as long as it stays there and shown without a timer. Set down and
    /// right-clicked they give it as they do in Terraria.
    /// </summary>
    internal static class BuffStationItems
    {
        static List<(ItemType Item, int Buff)> stations;
        static readonly HashSet<int> held = new HashSet<int>();

        public static int DevHeld => held.Count;

        /// <summary>The buff is held by a station carried or banked (no timer shown).</summary>
        public static bool Holds(int buff) => held.Contains(buff);

        /// <summary>One of the item in the bag (the hotbar and the coin and ammo slots too) or in the Piggy Bank.</summary>
        public static bool Carried(Inventory inv, ItemType type)
        {
            foreach (var s in inv.Slots) if (!s.IsEmpty && s.Type == type) return true;
            foreach (var s in inv.BankOf(0)) if (!s.IsEmpty && s.Type == type) return true;
            return !inv.Mouse.IsEmpty && inv.Mouse.Type == type;
        }

        /// <summary>Each frame in play: each carried or banked station's buff refreshed (Player.AddBuff only lengthens a running one).</summary>
        public static void Tick(Inventory inv, bool dead)
        {
            held.Clear();
            if (inv == null || dead) return;
            if (stations == null) stations = new List<(ItemType, int)>(World.Stations.BuffItems());
            foreach (var (item, buff) in stations)
            {
                if (!Carried(inv, item)) continue;
                Player.PlayerBuffs.Add(buff, 10);
                held.Add(buff);
            }
        }
    }
}
