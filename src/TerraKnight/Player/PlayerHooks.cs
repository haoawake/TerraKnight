using HarmonyLib;
using UnityEngine;

namespace TerraKnight.Player
{
    /// <summary>
    /// Turns the Knight into a Terraria player: Hollow Knight's own abilities are switched off at
    /// their gates (Terraria accessories switch movement back on through <see cref="Abilities"/>),
    /// and mask damage is redirected into Terraria life.
    /// </summary>
    [HarmonyPatch]
    internal static class PlayerHooks
    {
        /// <summary>Movement abilities granted by equipped Terraria accessories.</summary>
        public static class Abilities
        {
            public static bool Dash;
            public static bool DoubleJump;
            public static bool WallCling;
        }

        static bool Active => TerraMod.Instance != null;

        /// <summary>Knight abilities and the accessory that stands in for each.</summary>
        public static readonly (string flag, Items.ItemType item)[] AbilityItems =
        {
            ("hasDash", Items.ItemType.ShieldOfCthulhu),     // Mothwing Cloak
            ("hasWalljump", Items.ItemType.ClimbingClaws),   // Mantis Claw
            ("hasDoubleJump", Items.ItemType.CloudinaBottle), // Monarch Wings
            ("gotCharm_37", Items.ItemType.HermesBoots),     // Sprintmaster
            ("hasAcidArmour", Items.ItemType.Flipper),       // Isma's Tear
            ("quakeLevel", Items.ItemType.DeadCellsRamRune), // Desolate Dive: the slam breaks the dive floors
            ("hasShadowDash", Items.ItemType.ShadeCloak),    // Shade Cloak: the dashes go through enemies and shade gates
            ("hasSuperDash", Items.ItemType.CrystalHeart),   // Crystal Heart: the Super Dash on a long press of Dash
        };

        /// <summary>
        /// The game asks "has the lumafly lantern?" before lighting a dark room, and "has Isma's
        /// tear?" before letting acid be swum in. A worn mining helmet lights dark rooms too; worn
        /// flippers let acid be swum in too (Isma's tear also gives a pair).
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(PlayerData), nameof(PlayerData.GetBool))]
        static void EquipmentFlags(string boolName, ref bool __result)
        {
            if (!Active) return;
            // Hallownest's key items work from the hotbar (Items.HkItems).
            Items.HkItems.GateBool(boolName, ref __result);
            // The mining helmet, and the Night Owl potion (nightVision: the dark is seen through), light the dark rooms as the lantern does.
            if (boolName == "hasLantern") __result |= Items.Equipment.Current.Light || Items.Equipment.Current.NightVision;
            else if (boolName == "hasAcidArmour") __result |= Items.Equipment.Current.Swim;
        }

        // --- Hollow Knight abilities: nail, spells, focus, dream nail, crystal heart are gone (the focus comes back to drain a Dreamer). ---

        [HarmonyPrefix]
        [HarmonyPatch(typeof(HeroController), "CanAttack")]
        [HarmonyPatch(typeof(HeroController), "CanNailCharge")]
        [HarmonyPatch(typeof(HeroController), nameof(HeroController.CanNailArt))]
        [HarmonyPatch(typeof(HeroController), nameof(HeroController.CanCast))]
        [HarmonyPatch(typeof(HeroController), nameof(HeroController.CanFocus))]
        [HarmonyPatch(typeof(HeroController), nameof(HeroController.CanBackDash))]
        static bool Disabled(ref bool __result, System.Reflection.MethodBase __originalMethod)
        {
            if (!Active) return true;
            // The nail item in hand: the game's own nail checks (Items.NailItem).
            if (__originalMethod.Name == "CanAttack" && Items.NailItem.LetsAttack) return true;
            // A Dreamer struck down in its dream, the Dream Nail in hand: the focus that drains it (Items.DreamNailItem).
            if (__originalMethod.Name == "CanFocus" && Items.DreamNailItem.DreamFocus()) return true;
            __result = false;
            return false;
        }

        /// <summary>The Dream Nail and its gate: only with the Dream Nail item in hand (Items.DreamNailItem), then as the game decides.</summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(HeroController), nameof(HeroController.CanDreamNail))]
        [HarmonyPatch(typeof(HeroController), nameof(HeroController.CanDreamGate))]
        static bool DreamNailInHand(ref bool __result)
        {
            if (!Active || Items.DreamNailItem.Holding()) return true;
            __result = false;
            return false;
        }

        // --- Movement abilities come back only through accessories. ---
        //
        // The hero's movement code reads playerData.canDash / hasDoubleJump / hasWalljump in several
        // places. While its Update and FixedUpdate run, those flags are swapped for "is the matching
        // accessory equipped", and swapped back afterwards, so the save data is never changed.

        struct SavedFlags
        {
            public bool Active, CanDash, DoubleJump, WallJump;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(HeroController), "Update")]
        [HarmonyPatch(typeof(HeroController), "FixedUpdate")]
        static void SwapInAbilities(HeroController __instance, out SavedFlags __state)
        {
            __state = default;
            var pd = __instance.playerData;
            if (!Active || pd == null) return;
            __state = new SavedFlags { Active = true, CanDash = pd.canDash, DoubleJump = pd.hasDoubleJump, WallJump = pd.hasWalljump };
            pd.canDash = Abilities.Dash;
            pd.hasDoubleJump = Abilities.DoubleJump;
            pd.hasWalljump = Abilities.WallCling;
        }

        [HarmonyFinalizer]
        [HarmonyPatch(typeof(HeroController), "Update")]
        [HarmonyPatch(typeof(HeroController), "FixedUpdate")]
        static void SwapOutAbilities(HeroController __instance, SavedFlags __state)
        {
            if (!__state.Active) return;
            var pd = __instance.playerData;
            pd.canDash = __state.CanDash;
            pd.hasDoubleJump = __state.DoubleJump;
            pd.hasWalljump = __state.WallJump;
        }

        /// <summary>Hollow Knight's own inventory key is taken over by the Terraria inventory.</summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(HeroController), nameof(HeroController.CanOpenInventory))]
        static bool NoKnightInventory(ref bool __result)
        {
            if (!Active) return true;
            __result = false;
            return false;
        }

        // --- Health. ---

        static bool inTakeHealth;
        /// <summary>The hazardType of the hit being taken (1 enemies; 2 spikes, 3 acid, 4 lava, 5 pits).</summary>
        static int hazardNow;

        /// <summary>Dev: the spikes' hits the Dead Man's Sweater halved.</summary>
        public static int DevSweater;

        /// <summary>What dealt the hit being taken (an enemy, its attack, its shot).</summary>
        static GameObject sourceNow;

        [HarmonyPrefix]
        [HarmonyPatch(typeof(HeroController), nameof(HeroController.TakeDamage))]
        static void RecordHazard(GameObject go, int hazardType)
        {
            hazardNow = hazardType;
            sourceNow = go;
        }

        [HarmonyFinalizer]
        [HarmonyPatch(typeof(HeroController), nameof(HeroController.TakeDamage))]
        static void ForgetHazard()
        {
            hazardNow = 0;
            sourceNow = null;
        }

        /// <summary>
        /// How much harder than the area's common enemies the hit's source hits: a boss's or an
        /// elite's body or attack. A shot flying on its own (Hornet's needle, a spat glob) has no
        /// enemy above it: then the nearest boss or elite within reach of where it hit counts.
        /// </summary>
        /// <summary>The Terraria damage a touch of this many masks from this source would be (before defense).</summary>
        public static int RawContact(int amount, GameObject source) =>
            TerraStats.NativeHitDamage(amount, Mathf.RoundToInt(StageDamage(World.Areas.DamagePerMask) * SourceFactor(source)));

        /// <summary>A mask's damage in the room now, at the level its enemies are met at (Areas.StatLevel), a little less when brought up to it.</summary>
        static float StageDamage(System.Func<int, int> perMask)
        {
            int level = World.Areas.LevelOf(GameManager.UnsafeInstance?.sceneName);
            return perMask(World.Areas.StatLevel(level)) * World.Areas.CatchUp(level);
        }

        static float SourceFactor(GameObject go)
        {
            if (go == null || hazardNow > 1) return 1f;
            var hm = go.GetComponentInParent<HealthManager>();
            if (hm != null) return Items.EnemyStats.DamageFactor(Items.EnemyStats.Of(hm));
            Vector2 at = go.transform.position;
            float best = 30f, factor = 1f;
            foreach (var info in Items.EnemyStats.All)
            {
                if (info.Hm == null || info.Hm.isDead || !info.Hm.gameObject.activeInHierarchy || !Items.EnemyStats.Unstoppable(info)) continue;
                float d = Vector2.Distance(info.Hm.transform.position, at);
                if (d < best)
                {
                    best = d;
                    factor = Items.EnemyStats.DamageFactor(info);
                }
            }
            return factor;
        }
        /// <summary>Terraria damage the next hit deals instead of its masks' worth (an explosion).</summary>
        static int overrideDamage;

        /// <summary>
        /// Hurts the player for a Terraria amount (a bomb going off beside him) through the game's
        /// own damage path, so immunity, knockback and death all work as for any hit.
        /// </summary>
        public static void HurtPlayer(HeroController hero, int damage, int dir, GameObject source)
        {
            overrideDamage = damage;
            try
            {
                hero.TakeDamage(source, dir > 0 ? GlobalEnums.CollisionSide.left : GlobalEnums.CollisionSide.right, 1, 1);
            }
            finally
            {
                overrideDamage = 0;
            }
        }

        /// <summary>
        /// Every source of damage ends here with an amount in masks. Convert it to Terraria life
        /// (more per mask the deeper the scene); the masks themselves are only spent when that
        /// kills the player.
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(PlayerData), nameof(PlayerData.TakeHealth))]
        static void TakeHealth(PlayerData __instance, ref int amount)
        {
            var mod = TerraMod.Instance;
            if (mod == null || amount <= 0 || inTakeHealth) return;
            inTakeHealth = true;
            try
            {
                // Scale the enemy's raw damage first; TerraStats then applies this difficulty's defense rule.
                int perMask = Mathf.RoundToInt((hazardNow > 1
                    ? StageDamage(World.Areas.MasterHazardDamagePerMask)
                    : StageDamage(World.Areas.MasterDamagePerMask)) * SourceFactor(sourceNow));
                // The Dead Man's Sweater (deadMansSweater, P:14920-14924, 15120-15124): a trap's hit halved, at most 300; Hallownest's
                // spikes are its traps.
                if (hazardNow == 2 && Items.Equipment.Current.DeadMansSweater)
                {
                    perMask = Mathf.Max(1, Mathf.Min(perMask / 2, 300 / (World.GameDifficulty.UsesMaskDamage ? Mathf.Max(1, amount) : 1)));
                    DevSweater++;
                }
                // The enemy's banner carried (its body, its attack, a shot it spawned): its DamageReceived before the defense (Pl:31624-31627, P:14906-14909).
                if (hazardNow <= 1 && overrideDamage <= 0) perMask = Items.Banners.Taken(sourceNow, perMask);
                float share = World.GameDifficulty.EnemyFactor;
                int raw = overrideDamage > 0 ? overrideDamage : TerraStats.NativeHitDamage(amount, perMask, share);
                TerraHurt.BeforeHurt();
                // An enemy's or a shot's hit (not a hazard): what the Solar flare answers.
                Items.ArmorEffects.CounterHit = hazardNow <= 1;
                int damage = overrideDamage > 0 ? mod.Stats.TakeDirect(overrideDamage) : mod.Stats.TakeMasks(amount, perMask, share);
                overrideDamage = 0;
                // Hazards are Terraria's touch and lava damage: no stars or bees for them.
                TerraHurt.AfterHurt(HeroController.SilentInstance, raw, damage, hazardNow <= 1);
                TerraHurt.LastDamage = damage;
                var hero = HeroController.SilentInstance;
                if (hero != null) UI.CombatText.Show(hero.transform.position + Vector3.up * 0.9f, damage.ToString(), UI.CombatText.PlayerHurt);
                // Player.PlayHurtSound, the default (male) voice: one of three.
                // The werewolf yelps as a wolf does (NPC_Hit_6, Pl:38977-38981).
                if (!mod.Stats.IsDead) mod.PlaySound(Items.Equipment.WerewolfNow ? "NPC_Hit_6" : "Player_Hit_" + Random.Range(0, 3));
                amount = mod.Stats.IsDead ? __instance.health + __instance.healthBlue + 10 : 0;
            }
            finally
            {
                inTakeHealth = false;
            }
        }

        /// <summary>Benches, respawns and loading a save refill the masks; refill Terraria life with them.</summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(PlayerData), nameof(PlayerData.MaxHealth))]
        static void MaxHealth()
        {
            TerraMod.Instance?.Stats.FullHeal();
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(HeroController), nameof(HeroController.Respawn))]
        static void Respawn()
        {
            TerraMod.Instance?.Stats.FullHeal();
        }
    }
}
