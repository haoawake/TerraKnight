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
            // The nail item in hand: the game's own nail checks (Items.NailItem); with the arts on (Items.NailArts), only its own
            // swing attacks (the game's attack action is on for the charge), and the charge and the arts are the game's.
            if (__originalMethod.Name == "CanAttack" && Items.NailItem.LetsAttack && (!Items.NailArts.Ready || Items.NailArts.Manual)) return true;
            if ((__originalMethod.Name == "CanNailCharge" || __originalMethod.Name == nameof(HeroController.CanNailArt)) && Items.NailArts.Ready) return true;
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
        static int movementSwapDepth;
        static PlayerData movementPlayer;
        static SavedFlags learnedMovement;
        internal static void DevDuringAbilitySwap(HeroController hero,System.Action action)
        {
            SwapInAbilities(hero,out var state);
            try {action();}finally{SwapOutAbilities(hero,state);}
        }
        internal static bool? LearnedSuperDash;
        internal static bool LearnedAbility(string flag)
        {
            var pd=PlayerData.instance;
            if(pd==null)return false;
            if(movementSwapDepth>0&&ReferenceEquals(pd,movementPlayer)) {
                if(flag=="hasWalljump")return learnedMovement.WallJump;
                if(flag=="hasDoubleJump")return learnedMovement.DoubleJump;
            }
            if(flag=="hasSuperDash"&&LearnedSuperDash.HasValue)return LearnedSuperDash.Value;
            return pd.GetBool(flag);
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
            if(movementSwapDepth++==0){movementPlayer=pd;learnedMovement=__state;}
            // No dash of the Knight's on a mount (Terraria's mounts take none of the worn dashes either).
            pd.canDash = Abilities.Dash && !Mounts.OwnMovement;
            pd.hasDoubleJump = Abilities.DoubleJump;
            pd.hasWalljump = Abilities.WallCling;
        }

        [HarmonyFinalizer]
        [HarmonyPatch(typeof(HeroController), "Update")]
        [HarmonyPatch(typeof(HeroController), "FixedUpdate")]
        static void SwapOutAbilities(HeroController __instance, SavedFlags __state)
        {
            if (!__state.Active) return;
            if(--movementSwapDepth==0)movementPlayer=null;
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
            int level = World.Areas.FightLevel(GameManager.UnsafeInstance?.sceneName);
            return perMask(World.Areas.StatLevel(level)) * World.Areas.CatchUp(level);
        }

        static float SourceFactor(GameObject go)
        {
            if (go == null || hazardNow > 1) return 1f;
            // The new AI's shots and areas hit as hard as their enemy's touch.
            var hm = go.GetComponentInParent<HealthManager>() ?? EnemyAi.Hostiles.OwnerOf(go);
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
        static bool environmentOverride;
        public static void HurtEnvironment(HeroController hero, int damage, int dir, GameObject source)
        {
            bool previous = environmentOverride;
            environmentOverride = true;
            try { HurtPlayer(hero, damage, dir, source); }
            finally { environmentOverride = previous; }
        }

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
                // Defender's Crest: the ground's harm (spikes, thorns, acid, pits) takes nothing, no poison; the game still
                // puts the player back where it would (its hazard respawn).
                if (hazardNow > 1 && overrideDamage <= 0 && Items.CharmEffects.Sheltered(HeroController.SilentInstance))
                {
                    amount = 0;
                    return;
                }
                if (Items.PlayerChallenges.Hollow && (hazardNow > 1 || environmentOverride))
                {
                    TerraHurt.BeforeHurt();
                    Items.ArmorEffects.CounterHit = false;
                    int environmentDamage = mod.Stats.TakeEnvironment(1);
                    overrideDamage = 0;
                    TerraHurt.AfterHurt(HeroController.SilentInstance, environmentDamage, environmentDamage, false);
                    TerraHurt.LastDamage = environmentDamage;
                    if (hazardNow == SpikeHazard && Poison.IsThorn(sourceNow) && !mod.Stats.IsDead && !Items.CharmEffects.SoftHitNow) Poison.Prick();
                    Hurt(__instance, ref amount, mod, environmentDamage);
                    return;
                }
                if (hazardNow == SpikeHazard && overrideDamage <= 0)
                {
                    SpikeHit(__instance, ref amount, mod);
                    return;
                }
                // Native Radiant replaces a landed hit with 9999 masks. Ordinary Terraria
                // hit conversion intentionally ignores mask count, so preserve this death rule explicitly.
                if (amount >= 9999 && BossSceneController.IsBossScene && BossSceneController.Instance.BossLevel == 2)
                {
                    int lethal=mod.Stats.Life+mod.Stats.ShieldShown;
                    mod.Stats.TakeShare(lethal);
                    overrideDamage=0;
                    TerraHurt.LastDamage=lethal;
                    Hurt(__instance,ref amount,mod,lethal);
                    return;
                }
                // Scale the enemy's raw damage first; TerraStats then applies this difficulty's defense rule.
                int perMask = Mathf.RoundToInt((hazardNow > 1
                    ? StageDamage(World.Areas.MasterHazardDamagePerMask)
                    : StageDamage(World.Areas.MasterDamagePerMask)) * SourceFactor(sourceNow));
                // The enemy's banner carried (its body, its attack, a shot it spawned): its DamageReceived before the defense (Pl:31624-31627, P:14906-14909).
                if (hazardNow <= 1 && overrideDamage <= 0) perMask = Items.Banners.Taken(sourceNow, perMask);
                // An enemy's hit varies as Terraria's (Main.DamageVar, before the defense): within 15% either way, luck leaning it down.
                if (hazardNow <= 1 && overrideDamage <= 0) perMask = Mathf.Max(1, Mathf.RoundToInt(perMask * TerraStats.HurtVariation()));
                float share = World.GameDifficulty.EnemyFactor;
                int raw = overrideDamage > 0 ? overrideDamage : TerraStats.NativeHitDamage(amount, perMask, share);
                TerraHurt.BeforeHurt();
                // An enemy's or a shot's hit (not a hazard): what the Solar flare answers.
                Items.ArmorEffects.CounterHit = hazardNow <= 1;
                int damage = overrideDamage > 0 ? mod.Stats.TakeDirect(overrideDamage) : mod.Stats.TakeMasks(amount, perMask, share);
                bool overridden = overrideDamage > 0;
                overrideDamage = 0;
                DevLastHit = $"{(sourceNow != null ? sourceNow.name : "?")} 类型 {hazardNow}：{(overridden ? "指定伤害" : $"{amount} 面具 × 每面具 {perMask}")}，原始 {raw}，实受 {damage}";
                Plugin.Log.LogInfo($"[受伤] {DevLastHit}");
                // Hazards are Terraria's touch and lava damage: no stars or bees for them.
                TerraHurt.AfterHurt(HeroController.SilentInstance, raw, damage, hazardNow <= 1);
                TerraHurt.LastDamage = damage;
                Hurt(__instance, ref amount, mod, damage);
            }
            finally
            {
                inTakeHealth = false;
            }
        }

        /// <summary>DamageHero.hazardType for spikes and thorns.</summary>
        const int SpikeHazard = 2;

        /// <summary>
        /// Spikes and thorns hurt as in Hollow Knight, by a share of the life rather than the room's enemy damage: each mask
        /// of the hit its share of the masks (one of five masks, a fifth of the life), past defense. The Dead Man's Sweater
        /// halves a trap's hit, at most 300 (deadMansSweater, P:14920-14924). A thorn also poisons as Terraria's poison dart
        /// does (Player.Poison).
        /// </summary>
        static void SpikeHit(PlayerData pd, ref int amount, TerraMod mod)
        {
            int masks = Mathf.Max(1, pd.maxHealth);
            int share = Mathf.Max(1, Mathf.RoundToInt(mod.Stats.LifeMax * (float)amount / masks));
            if (Items.Equipment.Current.DeadMansSweater)
            {
                share = Mathf.Max(1, Mathf.Min(share / 2, 300));
                DevSweater++;
            }
            TerraHurt.BeforeHurt();
            Items.ArmorEffects.CounterHit = false;
            int damage = mod.Stats.TakeShare(share);
            TerraHurt.AfterHurt(HeroController.SilentInstance, share, damage, false);
            TerraHurt.LastDamage = damage;
            bool thorn = Poison.IsThorn(sourceNow);
            if (thorn && !mod.Stats.IsDead && !Items.CharmEffects.SoftHitNow) Poison.Prick();
            DevSpikeHits++;
            Plugin.Log.LogInfo($"[尖刺] {(sourceNow != null ? sourceNow.name : "?")}：{amount} 面具 / {masks}，扣 {damage} 生命（上限 {mod.Stats.LifeMax}）{(thorn ? "，荆棘中毒" : "")}");
            Hurt(pd, ref amount, mod, damage);
        }

        public static int DevSpikeHits;
        /// <summary>The last hit taken through the enemy/hazard conversion: its source, hazard type, masks, damage per mask, the damage before and after defense.</summary>
        public static string DevLastHit = "";

        /// <summary>A hit taken: its number over the head, the hurt voice, and the masks spent only when it killed.</summary>
        static void Hurt(PlayerData pd, ref int amount, TerraMod mod, int damage)
        {
            var hero = HeroController.SilentInstance;
            // A hit a charm took its own way (the Stalwart Shell's): its own number and sound, no hurt voice.
            if (Items.CharmEffects.ShowSoftHit(hero))
            {
                amount = mod.Stats.IsDead ? pd.health + pd.healthBlue + 10 : 0;
                return;
            }
            if (hero != null) UI.CombatText.Show(hero.transform.position + Vector3.up * 0.9f, damage.ToString(), UI.CombatText.PlayerHurt);
            // Player.PlayHurtSound, the default (male) voice: one of three.
            // The werewolf yelps as a wolf does (NPC_Hit_6, Pl:38977-38981).
            if (!mod.Stats.IsDead) mod.PlaySound(Items.Equipment.WerewolfNow ? "NPC_Hit_6" : "Player_Hit_" + Random.Range(0, 3));
            amount = mod.Stats.IsDead ? pd.health + pd.healthBlue + 10 : 0;
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
