using System.Collections.Generic;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// Each of the 48 boss fights' own pet (空洞骑士首领宠物素材包, drawn as Terraria's Master Mode boss pets), an easter egg
    /// its boss drops at 1 in 100 (the user's 10-26 "极低概率由boss本身掉落"): the pet item (Master rarity), its buff, its
    /// pet on one of three sheets: on the ground (20 frames: idle 0-3, a walk 4-11, up 12, down 13, flying after the player
    /// 14-19; Terraria's AI_026 moves it), a flyer (8 frames, tilted by its flight) or by the shoulder (12: a float 0-7 and
    /// every few seconds a gesture 8-11). Only their bosses give them (BossDropPlan.BossOnly).
    /// </summary>
    internal static partial class BossPets
    {
        internal enum Kind : byte { Ground, Flyer, Anchor }

        internal readonly struct Row
        {
            public readonly ItemType Type;
            public readonly string Name, Record;
            /// <summary>The scenes where the record's fight is this pet's boss (the Sisters of Battle's, the Winged Nosk's...); null: its own.</summary>
            public readonly string[] Scenes;
            public readonly Kind Kind;
            public readonly int CellW, CellH, Frames, Stage;
            public readonly bool Light;
            public readonly string PetZh, ItemZh;

            public Row(ItemType type, string name, string record, string[] scenes, Kind kind, int cellW, int cellH, int frames, bool light, int stage, string petZh, string itemZh)
            {
                Type = type; Name = name; Record = record; Scenes = scenes; Kind = kind; CellW = cellW; CellH = cellH; Frames = frames;
                Light = light; Stage = stage; PetZh = petZh; ItemZh = itemZh;
            }
        }

        /// <summary>A kill of its boss drops it one time in this many.</summary>
        public const int OneIn = 100;

        /// <summary>The pets' buffs: this and on (the mod's own, past Terraria's, the lantern's and the charm skills' 9100-9240).</summary>
        const int BuffBase = 9400;

        static Dictionary<ItemType, (Row Row, PetDef Def)> byType;

        static void Build()
        {
            if (byType != null) return;
            byType = new Dictionary<ItemType, (Row, PetDef)>();
            var rows = Table.Rows;
            for (int i = 0; i < rows.Length; i++)
            {
                var r = rows[i];
                byType[r.Type] = (r, new PetDef(BuffBase + i + 1, r.Light, r.PetZh, r.PetZh + "正跟着你", new[] { Proj(r, i) }));
            }
        }

        static PetProj Proj(Row r, int i)
        {
            const int W = 20, H = 20;
            var p = new PetProj { Texture = "BossPet_" + r.Name, Frames = r.Frames, W = W, H = H, Scale = 1f, BossPet = true, Boss = r.Kind };
            if (r.Light) p.Light = new Color(1.6f, 1.45f, 1.05f);
            switch (r.Kind)
            {
                case Kind.Ground:
                    // Terraria's AI_026 walker under an id of its own (none of its pets'): its feet on the hitbox's bottom.
                    p.Id = 20000 + i;
                    p.Move = PetMove.Ground;
                    p.OffX = -(r.CellW - W) / 2;
                    p.OriginY = r.CellH - 2 - H;
                    break;
                case Kind.Flyer:
                    p.Id = 0;
                    p.Move = PetMove.Flyer;
                    p.Centered = true;
                    p.FlyStart = 0; p.FlyWrap = 8; p.FlyTo = 0; p.FlyTicks = 5;
                    p.FlyTurn = PetTurn.VelX; p.FlyTurnK = 0.04f;
                    p.Anchor = new Vector2(-38f, -34f);
                    p.Pull = 0.07f;
                    break;
                default:
                    p.Id = 0;
                    p.Move = PetMove.Anchor;
                    p.Centered = true;
                    p.FacesPlayer = true;
                    p.Anchor = new Vector2(-30f, -40f);
                    p.Pull = 0.1f;
                    break;
            }
            return p;
        }

        public static bool Is(ItemType t)
        {
            Build();
            return byType.ContainsKey(t);
        }

        public static PetDef Def(ItemType t)
        {
            Build();
            return byType.TryGetValue(t, out var e) ? e.Def : null;
        }

        public static IEnumerable<ItemType> Items
        {
            get
            {
                Build();
                return byType.Keys;
            }
        }

        public static IEnumerable<PetDef> Defs
        {
            get
            {
                Build();
                foreach (var e in byType.Values) yield return e.Def;
            }
        }

        /// <summary>A pet buff's icon (Buff_9201...): its item's.</summary>
        public static Texture2D BuffIcon(int buff)
        {
            int i = buff - BuffBase - 1;
            var rows = Table.Rows;
            return i >= 0 && i < rows.Length ? Terraria.TerrariaAssets.Texture("BossPetItem_" + rows[i].Name) : null;
        }

        public static bool IsBuff(int buff) => buff > BuffBase && buff <= BuffBase + Table.Rows.Length;

        /// <summary>A kill's rows (ProgressionLoot.Of): its boss's pet, at <see cref="OneIn"/>.</summary>
        internal static void AddDrops(List<EnemyDrops.Drop> rows, string key, string scene)
        {
            if (key == null) return;
            // A variant's fight where its scene is (the Sisters of Battle in Godhome...), else the record's own pet.
            ItemType pet = ItemType.None;
            foreach (var r in Table.Rows)
                if (r.Record == key && r.Scenes != null && scene != null && System.Array.IndexOf(r.Scenes, scene) >= 0) pet = r.Type;
            if (pet == ItemType.None)
                foreach (var r in Table.Rows)
                    if (r.Record == key && r.Scenes == null) pet = r.Type;
            if (pet != ItemType.None && ItemDb.Has(pet))
                rows.Add(new EnemyDrops.Drop { Type = pet, Num = 1, Den = OneIn, Min = 1, Max = 1, LockStage = 0 });
        }
    }

    internal static partial class ItemDb
    {
        /// <summary>The pets' items, as Terraria's Master Mode boss pets: Item.DefaultToVanitypet, Master rarity, worth 5 gold.</summary>
        static void AddBossPets(List<ItemDef> list)
        {
            foreach (var r in BossPets.Table.Rows)
            {
                var def = BossPets.Def(r.Type);
                list.Add(new ItemDef
                {
                    Type = r.Type, Name = r.ItemZh, Texture = "BossPetItem_" + r.Name, PetBuff = def.Buff, LightPet = r.Light, Rare = -13,
                    Tooltip = "召唤" + r.PetZh, UseAnimation = 20 * Tick, UseTime = 20 * Tick, AutoReuse = false, NoMelee = true, UseSound = "Item_2",
                });
            }
        }

        static IEnumerable<(ItemType, int, ItemSource, int)> BossPetSources()
        {
            foreach (var r in BossPets.Table.Rows) yield return (r.Type, r.Stage, ItemSource.Drop, 50000);
        }
    }
}
