using HarmonyLib;
using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight.Achievements
{
    /// <summary>
    /// The achievements got by doing things, each called from where the thing is done: forging and reforging, bags and
    /// drops, pylons, digging, the Shimmer, chests and blocks, houses and townsfolk, the Target Dummy, the shields, the
    /// bag and the shop; and the hits on enemies (their damage, the weapons' classes each boss fight saw, the kills).
    /// </summary>
    internal static partial class AchievementTracker
    {
        // ------------------------------------------------------------------ the Goblin Tinkerer, bags, drops

        /// <summary>A forge level up: the piece and its level now, worn armor's levels for the whole set.</summary>
        public static void Forged(ItemStack item, bool top)
        {
            if (!Achievements.Counting || item.IsEmpty) return;
            var def = ItemDb.Get(item.Type);
            if (def != null && !Forging.IsArmorPiece(def) && item.Forge >= 6) Achievements.Unlock("I01");
            var mod = TerraMod.Instance;
            if (mod == null) return;
            bool set = true;
            for (int i = 0; i < 3 && i < mod.Inventory.Armor.Length; i++)
            {
                var a = mod.Inventory.Armor[i];
                if (a.IsEmpty || !Forging.AtMax(a)) { set = false; break; }
            }
            if (set) Achievements.Unlock("I04");
        }

        /// <summary>A reforge: its prefix (81 Legendary, 84 the yoyos' Legendary).</summary>
        public static void Reforged(int prefix)
        {
            if (!Achievements.Counting) return;
            Achievements.AddTo("I03", "reforges", 200);
            if (prefix == 81 || prefix == 84) Achievements.Unlock("I02");
        }

        public static void BagOpened() => Achievements.AddTo("I20", "bags", 25);

        /// <summary>A drop given (a kill's or a bag's): its chance, whether it is a developer item out of a bag.</summary>
        public static void Dropped(float chance, bool developerFromBag)
        {
            if (!Achievements.Counting) return;
            if (chance > 0f && chance <= 0.01f) Achievements.Unlock("I14");
            if (developerFromBag) Achievements.Unlock("I07");
        }

        // ------------------------------------------------------------------ the world

        public static void PylonArrived()
        {
            if (!Achievements.Counting) return;
            Achievements.AddTo("E19", "pylon trips", 100);
            int lit = 0;
            foreach (var p in World.Pylons.Unlocked) if (!p.Placed && !p.Provisional) lit++;
            if (lit >= World.Pylons.BenchCount) Achievements.Unlock("E03");
        }

        /// <summary>A cell dug out by the player (what it was).</summary>
        public static void Mined(World.CellKind kind)
        {
            if (!Achievements.Counting || kind == World.CellKind.Empty) return;
            long dug = Achievements.Add("tiles dug");
            if (dug >= 10000) Achievements.Unlock("E13");
            if (dug >= 100000) Achievements.Unlock("E14");
            if (kind == World.CellKind.Meteorite) Achievements.Unlock("E10");
        }

        /// <summary>Things changed in the Shimmer (how many).</summary>
        public static void Shimmered(int count) => Achievements.AddTo("H07", "shimmered", 50, Mathf.Max(1, count));

        public static void ChestRenamed() => Achievements.AddTo("H10", "chest renames", 20);

        public static void BlockSwapped() => Achievements.AddTo("H11", "block swaps", 1000);

        /// <summary>A kit set up: a prefab house, or the casino's (its three machines with it).</summary>
        public static void KitPlaced(ItemType kit)
        {
            if (!Achievements.Counting) return;
            if (kit == ItemType.CasinoKit) Achievements.Unlock("G01");
            else Achievements.AddTo("H03", "prefab houses", 10);
        }

        /// <summary>A tombstone stood up: ten of them about in the rooms loaded.</summary>
        public static void TombstoneStood()
        {
            if (!Achievements.Counting) return;
            if (World.Stations.Standing(World.Stations.IsGrave).Count >= 10) Achievements.Unlock("H12");
        }

        public static void BottomlessPour() => Achievements.AddTo("H14", "bucket cells", 1000);

        /// <summary>A purchase from a townsperson: what it cost (copper).</summary>
        public static void Bought(long copper)
        {
            if (!Achievements.Counting) return;
            if (Achievements.Add("npc spent copper", copper) >= 1000000L * 100) Achievements.Unlock("H15");
        }

        /// <summary>A townsperson moved into a house the player built: every one housed, all of them.</summary>
        public static void Housed()
        {
            if (!Achievements.Counting) return;
            Achievements.Unlock("H01");
            foreach (var def in Town.TownNpcs.AllDefs)
                if (Town.TownNpcs.Housable(def) && !Town.TownNpcs.Houses.ContainsKey(def.Internal)) return;
            Achievements.Unlock("H02");
        }

        public static void DummyHit() => Achievements.AddTo("X09", "dummy hits", 10000);

        /// <summary>The Target Dummy's damage a second, as it shows.</summary>
        public static void DummyDps(int dps)
        {
            if (!Achievements.Counting) return;
            if (dps >= 1000) Achievements.Unlock("H04");
            if (dps >= 10000) Achievements.Unlock("H05");
            if (dps >= 50000) Achievements.Unlock("H06");
        }

        /// <summary>A blow taken on a raised shield (the Sergeant United Shield's parry, the Baldur Shell's guard).</summary>
        public static void Blocked() => Achievements.AddTo("W09", "blocks", 100);

        static float fullAt = -10f;
        static object fullItem;

        /// <summary>A pickup with no room in the bag (as long as the player touches it: counted once an item).</summary>
        public static void BagFull(object item)
        {
            if (!Achievements.Counting || item == fullItem && Time.time - fullAt < 5f) return;
            fullItem = item;
            fullAt = Time.time;
            Achievements.AddTo("X07", "bag full", 50);
        }

        /// <summary>A quick sale: how many went in it.</summary>
        public static void Sold(int count)
        {
            if (Achievements.Counting && count >= 50) Achievements.Unlock("X08");
        }

        public static void WorldEdge()
        {
            if (Achievements.Counting) Achievements.Unlock("E15");
        }

        /// <summary>Fallen Stars picked up: ten in one night.</summary>
        public static void StarsPicked(int count)
        {
            if (!Achievements.Counting || World.TerraTime.IsDay) return;
            string key = "stars night " + World.TerraTime.Day;
            if (Achievements.Add(key, count) >= 10) Achievements.Unlock("E11");
        }

        // ------------------------------------------------------------------ hits and kills

        const int Melee = 1, Ranged = 2, Magic = 4, Summon = 8, Nail = 16, Spell = 32;

        /// <summary>The nail's swing being dealt now (its art, "" for a plain swing), else null.</summary>
        public static string NailSwing;

        static int killFrame, killsThisFrame;

        /// <summary>A hit of the player's on an enemy (MeleeHits.Deal): its damage, its class, whether it killed.</summary>
        public static void Hit(HealthManager hm, int damage, DamageClass cls, bool autonomous)
        {
            try { HitNow(hm, damage, cls, autonomous); }
            catch (System.Exception e) { Quiet(e); }
        }

        /// <summary>A tracker's fault never reaches the fight it watches: logged once, then quiet.</summary>
        static void Quiet(System.Exception e)
        {
            if (quietLogged) return;
            quietLogged = true;
            Plugin.Log.LogWarning($"[成就] 统计出错（已忽略）：{e}");
        }

        static bool quietLogged;

        static void HitNow(HealthManager hm, int damage, DamageClass cls, bool autonomous)
        {
            if (!Achievements.Counting || hm == null) return;
            if (damage > 10000) Achievements.Unlock("W08");
            int flag = NailSwing != null ? Nail : cls == DamageClass.Melee ? Melee : cls == DamageClass.Ranged ? Ranged
                : cls == DamageClass.Magic ? Magic : cls == DamageClass.Summon || autonomous ? Summon : 0;
            if (fights.TryGetValue(hm.GetInstanceID(), out var f)) f.Classes |= flag;
            if (!hm.isDead) return;
            if (killFrame != Time.frameCount)
            {
                killFrame = Time.frameCount;
                killsThisFrame = 0;
            }
            if (++killsThisFrame >= 10) Achievements.Unlock("W11");
            if (NailSwing != null && NailSwing.Length > 0) Achievements.AddTo("W14", "art kills", 100);
        }

        /// <summary>Hollow Knight's own spells on an enemy (not the mod's magic dealt as a spell to a shell that takes only spells).</summary>
        [HarmonyPrefix, HarmonyPatch(typeof(HealthManager), "TakeDamage")]
        static void SpellHit(HealthManager __instance, HitInstance hitInstance)
        {
            try
            {
                if (hitInstance.AttackType != AttackTypes.Spell || hitInstance.Source == Items.TerraHitFx.Source) return;
                if (__instance != null && fights.TryGetValue(__instance.GetInstanceID(), out var f)) f.Classes |= Spell;
            }
            catch (System.Exception e) { Quiet(e); }
        }

        [HarmonyPrefix, HarmonyPatch(typeof(HealthManager), nameof(HealthManager.Die))]
        static void SpellKill(HealthManager __instance, AttackTypes attackType)
        {
            try
            {
                if (attackType != AttackTypes.Spell || __instance == null || __instance.isDead || !Achievements.Counting) return;
                Achievements.AddTo("W15", "spell kills", 500);
            }
            catch (System.Exception e) { Quiet(e); }
        }

        /// <summary>A boss down: what the player fought it with (OnBossDown).</summary>
        static void ByClass(string baseRecord, int classes)
        {
            if (classes == 0) return;
            void Only(string id, string cls, int allowed)
            {
                if ((classes & ~allowed) != 0) return;
                if (Achievements.Count("class " + cls + ":" + baseRecord) == 0)
                {
                    Achievements.Set("class " + cls + ":" + baseRecord, 1);
                    Achievements.Add("class bosses " + cls);
                }
                if (Achievements.Count("class bosses " + cls) >= 10) Achievements.Unlock(id);
            }
            Only("W03", "melee", Melee | Nail);
            Only("W04", "ranged", Ranged);
            Only("W05", "magic", Magic);
            Only("W06", "summon", Summon);
            // The nail only (its arts too): five bosses.
            if ((classes & ~Nail) == 0)
            {
                if (Achievements.Count("nail only:" + baseRecord) == 0)
                {
                    Achievements.Set("nail only:" + baseRecord, 1);
                    Achievements.AddTo("C25", "nail only bosses", 5);
                }
            }
            if (baseRecord == "HollowKnight")
            {
                if ((classes & Nail) == 0) Achievements.Unlock("C26");
                foreach (var (cls, bit) in new[] { ("melee", Melee), ("ranged", Ranged), ("magic", Magic), ("summon", Summon) })
                    if (classes == bit) Achievements.Set("hk class " + cls, 1);
                if (Achievements.Count("hk class melee") > 0 && Achievements.Count("hk class ranged") > 0
                    && Achievements.Count("hk class magic") > 0 && Achievements.Count("hk class summon") > 0) Achievements.Unlock("C28");
            }
        }

        // ------------------------------------------------------------------ multiplayer

        /// <summary>The others in this room now (alive and dead), from the session's latest of each.</summary>
        static void Others(out int here, out int deadHere)
        {
            here = deadHere = 0;
            if (!Net.NetSession.Active) return;
            string scene = GameManager.UnsafeInstance != null ? GameManager.UnsafeInstance.sceneName : null;
            foreach (var p in Net.NetSession.Players.Values)
            {
                if (p == null || !p.HasState || p.Latest.Scene != scene) continue;
                here++;
                if (p.Latest.Dead) deadHere++;
            }
        }
    }
}
