using System;
namespace TerraKnight.Items
{
    /// <summary>Chest moves transfer native inventory entitlement; stored items must not be auto-issued again.</summary>
    internal static class HallownestStorage
    {
        static readonly ItemType[] Types = Build();
        static int depth;
        static ItemType[] Build()
        {
            var list = new System.Collections.Generic.List<ItemType>();
            foreach (var d in ItemDb.All) if (HkItems.Is(d.Type)) list.Add(d.Type);
            list.Add(ItemType.KnightNail); list.Add(ItemType.DreamNail);
            return list.ToArray();
        }
        static int Carried(Inventory inv, ItemType t)
        {
            int n = 0;
            foreach (var s in inv.Slots) if (!s.IsEmpty && s.Type == t) n += s.Count;
            foreach (var s in inv.HkBag) if (!s.IsEmpty && s.Type == t) n += s.Count;
            foreach (var s in inv.Misc) if (!s.IsEmpty && s.Type == t) n += s.Count;
            if (!inv.Mouse.IsEmpty && inv.Mouse.Type == t) n += inv.Mouse.Count;
            return n;
        }
        public static int[] Snapshot(Inventory inv)
        {
            if (inv == null || depth > 0) return null;
            HallownestRecovery.Observe(inv);
            int[] counts = new int[Types.Length];
            for (int i = 0; i < Types.Length; i++)
            {
                counts[i] = Carried(inv, Types[i]);
                if (counts[i] > 0) HallownestRecovery.Record(inv, Types[i]);
            }
            return counts;
        }
        public static void Complete(Inventory inv, int[] before)
        {
            if (inv == null || before == null) return;
            for (int i = 0; i < Types.Length; i++)
            {
                int delta = Carried(inv, Types[i]) - before[i];
                if (delta == 0) continue;
                HkItems.Trade(inv, Types[i], delta, alreadyMoved: true);
                if (delta > 0) HallownestRecovery.Record(inv, Types[i]);
            }
            HallownestRecovery.Observe(inv);
        }
        public static void Move(Inventory inv, Action move)
        {
            if (depth > 0) { move(); return; }
            var before = Snapshot(inv);
            depth++;
            try { move(); } finally { depth--; Complete(inv, before); }
        }
        public static T Move<T>(Inventory inv, Func<T> move)
        {
            if (depth > 0) return move();
            var before = Snapshot(inv);
            depth++;
            try { return move(); } finally { depth--; Complete(inv, before); }
        }
    }
}

