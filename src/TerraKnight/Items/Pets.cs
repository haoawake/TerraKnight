using System.Collections.Generic;
using TerraKnight.Player;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>How a pet gets about: walking after the player (AI_026's walkers, AI_067), an orb or a fairy (aiStyle 11), a flyer (AI_026's flying pets), held by the player's side (aiStyles 124, 144, 90), a worm (AI_174).</summary>
    internal enum PetMove : byte { Ground, Orb, Flyer, Anchor, Worm }

    /// <summary>How a flying pet turns: not at all, with its speed across (rotation = vx × k), along its flight.</summary>
    internal enum PetTurn : byte { None, VelX, Along }

    /// <summary>
    /// A pet's projectile: its sheet (frames, hitbox, scale, Main.DrawProj's offsets), how it moves, its frames walking
    /// (idle, the loop, up, down) and flying (a frame or a loop), and a light pet's light. A Terraria pet moves and picks
    /// its frames by its own AI (Minions.PetAi: its id's aiStyle, with Terraria's numbers); the walking and flying fields
    /// here drive Hallownest's own pets.
    /// </summary>
    internal sealed class PetProj
    {
        public int Id, Frames = 1, W = 16, H = 16;
        /// <summary>A picture of the mod's instead of Projectile_&lt;Id&gt; (the Lumafly Lantern's), and the height it is drawn at (px; 0: its own).</summary>
        public string Texture;
        public float DrawHeight;
        public float Scale = 1f;
        public PetMove Move;
        public Color Light;
        public int Alpha;
        /// <summary>Projectile.drawOffsetX, drawOriginOffsetX, drawOriginOffsetY.</summary>
        public int OffX, OriginX, OriginY;
        // Walking (AI_026, P:62565-66045): the idle frame; the loop from WalkStart, back to WalkTo at WalkWrap, a frame each
        // time its counter passes WalkCounter (sped up by the walk when WalkBySpeed); rising and falling frames.
        public int Idle, WalkStart, WalkWrap = 1, WalkTo, WalkCounter = 6, Up = -1, Down = -1;
        public bool WalkBySpeed = true;
        // Flying: a frame, or a loop (FlyStart, back to FlyTo at FlyWrap, FlyTicks a frame).
        public int FlyFixed = -1, FlyStart, FlyWrap = 1, FlyTo, FlyTicks = 4;
        public PetTurn FlyTurn;
        public float FlyTurnK = 0.1f;
        // AI_026's walker: the gap it lets the player keep (num), its walk, when it takes to the air and lands, its flight.
        public int StopDist = 85, FlyAt = 500, LandAt = 200;
        public float WalkSpeed = 6.5f, WalkAccel = 0.08f, FlySpeed = 10f, FlyAccel = 0.2f;
        /// <summary>A flyer's place by the player (x by the player's facing), how far it may keep from it, its top speed and pull.</summary>
        public Vector2 Anchor = new Vector2(-30f, -40f);
        public float Slack = 10f, Speed = 8f, Pull = 0.1f;
        /// <summary>Its sprite turned by the player's facing rather than its own flight.</summary>
        public bool FacesPlayer;
        /// <summary>The sprite faces right as drawn (spriteDirection 1 unflipped moving right); most face left.</summary>
        public bool FacesRight;
        /// <summary>Main.DrawProj's centred branch (the frame's middle on the hitbox's; drawOffsetX unused); the whole sheet as its one frame.</summary>
        public bool Centered, Whole;
        /// <summary>A worm's segments, the head among them, and how far apart (Main.DrawMultisegmentPet).</summary>
        public int Segments = 1;
        public float Spacing = 16f;
    }

    /// <summary>A pet item's buff (Main.vanityPet / Main.lightPet), its name and tooltip, and its pet(s): the Fairy Bell has three, one of them each time.</summary>
    internal sealed class PetDef
    {
        public readonly int Buff;
        public readonly bool Light;
        public readonly string BuffName, BuffDesc;
        public readonly PetProj[] Projs;

        public PetDef(int buff, bool light, string buffName, string buffDesc, PetProj[] projs)
        {
            Buff = buff; Light = light; BuffName = buffName; BuffDesc = buffDesc; Projs = projs;
        }
    }

    internal static partial class ItemDb
    {
        /// <summary>Item.DefaultToVanitypet: swung (useStyle 1, 20 ticks), Item_2, worth 2 gold; using it calls the pet or sends it away.</summary>
        static ItemDef PetItem(ItemType t, string name, int id, int rare, string tip, bool light, int buff) => new ItemDef
        {
            Type = t, Name = name, Texture = "Item_" + id, PetBuff = buff, LightPet = light, Rare = rare, Tooltip = tip,
            UseAnimation = 20 * Tick, UseTime = 20 * Tick, AutoReuse = false, NoMelee = true, UseSound = "Item_2",
        };
    }

    /// <summary>
    /// Terraria's pets and light pets (Player.UpdatePet / UpdatePetLight, Main.vanityPet / lightPet): using the item puts its
    /// buff on (one vanity pet and one light pet at a time, a new one sends the old away) or takes it off; worn in the misc
    /// equipment's pet or light pet slot the pet stays, back after death, until its buff is right-clicked off. A pet lives
    /// while its buff does (Minions draws and moves it). Every enemy may drop one: one kill in 10000 for each pet, the same for all.
    /// </summary>
    internal static partial class Pets
    {
        public const int PetSlot = 0, LightSlot = 1;
        /// <summary>The Lumafly Lantern's light pet buff (the mod's own, past Terraria's).</summary>
        public const int LanternBuff = 9001;

        /// <summary>The Lumafly Lantern is out as a light pet.</summary>
        public static bool LanternOut => current[LightSlot] == ItemType.HkLumaflyLantern;

        /// <summary>The Grimmchild's summon buff (the mod's own), and whether it is out.</summary>
        public const int GrimmBuff = 9002;
        public static bool GrimmOut => current[PetSlot] == ItemType.HkGrimmchild;
        /// <summary>The chance of each pet from a kill (all of them alike).</summary>
        public const float KillChance = 1f / 10000f;

        /// <summary>The item whose pet is out, a vanity pet's and a light pet's (None: none).</summary>
        static readonly ItemType[] current = { ItemType.None, ItemType.None };
        /// <summary>The worn item last tick, and whether the player sent its pet away (Main.TryRemovingBuff: hideMisc).</summary>
        static readonly ItemType[] worn = { ItemType.None, ItemType.None };
        static readonly bool[] hidden = new bool[2];
        static bool buffsDefined;

        public static bool Is(ItemType t) => defs.ContainsKey(t);
        public static PetDef Of(ItemType t) => defs.TryGetValue(t, out var d) ? d : null;
        public static IEnumerable<ItemType> All => defs.Keys;

        /// <summary>The pet out now of a kind (vanity, light): its item, None for none.</summary>
        public static ItemType Current(bool light) => current[light ? 1 : 0];

        /// <summary>The pet buffs' names and tooltips (Terraria's BuffName / BuffDescription), shown without a time.</summary>
        public static void DefineBuffs()
        {
            if (buffsDefined) return;
            buffsDefined = true;
            foreach (var d in defs.Values) PlayerBuffs.Define(d.Buff, d.BuffName, d.BuffDesc, noTime: true);
        }

        /// <summary>Using a pet item: its pet comes (the other of its kind goes), or goes if it is out.</summary>
        public static void Use(ItemDef def)
        {
            var d = Of(def.Type);
            if (d == null) return;
            DefineBuffs();
            if (PlayerBuffs.Has(d.Buff))
            {
                PlayerBuffs.Remove(d.Buff);
                current[d.Light ? 1 : 0] = ItemType.None;
                return;
            }
            Call(def.Type);
        }

        /// <summary>Player.AddBuff for a pet: the other buffs of its kind are cleared first.</summary>
        static void Call(ItemType t)
        {
            var d = defs[t];
            int k = d.Light ? 1 : 0;
            foreach (var o in defs.Values)
                if (o.Light == d.Light && o.Buff != d.Buff) PlayerBuffs.Remove(o.Buff);
            PlayerBuffs.Add(d.Buff, 18000);
            current[k] = t;
        }

        /// <summary>
        /// Each tick (before the pets move): the worn pets called (UpdatePet), unless sent away by their buff; the buff
        /// out kept full (Main.vanityPet buffs never run out); a pet whose buff is gone goes too.
        /// </summary>
        public static void Tick(Inventory inv, bool dead)
        {
            if (inv == null) return;
            DefineBuffs();
            for (int k = 0; k < 2; k++)
            {
                var slot = inv.Misc[k];
                var want = !slot.IsEmpty && Inventory.FitsMisc(slot.Def, k) ? slot.Type : ItemType.None;
                if (want != worn[k])
                {
                    // The Lumafly Lantern and the Grimmchild taken out of their slot: what they called goes with them (their
                    // effects are the game's, for the one who has them out). A Terraria pet stays, as there.
                    var was = worn[k];
                    worn[k] = want;
                    hidden[k] = false;
                    if ((was == ItemType.HkLumaflyLantern || was == ItemType.HkGrimmchild) && current[k] == was)
                    {
                        PlayerBuffs.Remove(defs[was].Buff);
                        current[k] = ItemType.None;
                    }
                    if (want != ItemType.None && !dead) Call(want);
                }
                var cur = current[k];
                if (cur != ItemType.None && !PlayerBuffs.Has(defs[cur].Buff))
                {
                    // Right-clicked off while worn: it stays away until the slot changes. Death clears it for a while only.
                    if (cur == want && !dead) hidden[k] = true;
                    current[k] = ItemType.None;
                }
                if (dead) continue;
                if (current[k] == ItemType.None && want != ItemType.None && !hidden[k]) Call(want);
                if (current[k] != ItemType.None) PlayerBuffs.Add(defs[current[k]].Buff, 18000);
            }
        }

        /// <summary>A new save or a slot loaded: no pet out, the worn ones called again.</summary>
        public static void Clear()
        {
            for (int k = 0; k < 2; k++)
            {
                current[k] = ItemType.None;
                worn[k] = ItemType.None;
                hidden[k] = false;
            }
        }

        static List<ItemType> eggs;

        /// <summary>Every pet item, the easter eggs a kill or a crate may give.</summary>
        public static List<ItemType> Eggs
        {
            get
            {
                if (eggs == null)
                {
                    eggs = new List<ItemType>(defs.Keys);
                    // The game gives the Lumafly Lantern (Sly sells it); no kill or crate does.
                    eggs.Remove(ItemType.HkLumaflyLantern);
                    eggs.Remove(ItemType.HkGrimmchild);
                }
                return eggs;
            }
        }

        /// <summary>A kill's easter egg: each pet at <see cref="KillChance"/>, any enemy (a boss too).</summary>
        public static void KillDrop(string record, Vector2 at, bool direct)
        {
            if (record == null || !EnemyDrops.Known(record)) return;
            var all = Eggs;
            if (Random.value >= KillChance * all.Count) return;
            EnemyDrops.Spill(ItemStack.Of(all[Random.Range(0, all.Count)], 1), at, direct);
        }
    }
}
