using System;
using System.Collections.Generic;
using HarmonyLib;
using TerraKnight.Effects;
using TerraKnight.UI;
using UnityEngine;

namespace TerraKnight.Player
{
    /// <summary>
    /// Terraria's movement on Hollow Knight's controller, from Player.HorizontalMovement,
    /// JumpMovement, DashMovement and WallslideMovement. Terraria values are in pixels per tick
    /// at 60 ticks per second; one 16 px tile is 0.5 units here, so 1 px/tick = 1.875 u/s.
    /// The Knight's own jump, wall slide and double jump code paths stay (they own the state
    /// machine and the animations the game expects) but run on Terraria's numbers, and the
    /// accessories bring Terraria's own effects: Hermes Boots sprint and dust, the cloud puff of
    /// the Cloud in a Bottle, Climbing Claws slide dust, and the Shield of Cthulhu dash and bonk.
    /// </summary>
    [HarmonyPatch]
    internal static class TerraMovement
    {
        /// <summary>Terraria pixels per tick in world units per second.</summary>
        public const float PxT = 60f / 32f;
        const float Px = TerraParticles.Px;

        public static bool Enabled = true;

        // Player defaults.
        const float MaxRunSpeed = 3f, RunAcceleration = 0.08f, RunSlowdown = 0.2f;
        const float JumpSpeed = 5.01f;
        const int JumpHeight = 15;
        const float MaxFallSpeed = 10f;
        // One climbing item: slow slide, faster with down held; wall jumps push off at 3 px/tick.
        const float SlideSpeed = 0.5f, SlideDownSpeed = 4f, WallJumpPush = 3f;
        // Shield of Cthulhu, dashType 2; Tabi's ninja dash, dashType 1.
        const float ShieldDashSpeed = 14.5f, NinjaDashSpeed = 16.9f, SolarDashSpeed = 21.9f;
        const int ShieldBashDamage = 30;
        // Terraria's player hitbox, centred on the hero's collider and standing on its bottom.
        const int Width = 20, Height = 42;

        // --- Set from the equipped accessories every frame. ---

        /// <summary>Top running speed in px/tick (accRunSpeed); 6 with Hermes Boots.</summary>
        public static float AccRunSpeed = MaxRunSpeed;
        /// <summary>dashType: 1 the ninja dash (Tabi, Master Ninja Gear), 2 the Shield of Cthulhu's; 0 none.</summary>
        public static int DashKind;
        public static bool ShieldDash => DashKind == 2;
        /// <summary>Terraria's moveSpeed: multiplies run speeds and acceleration (Aglet, Anklet, Shadow set).</summary>
        public static float MoveSpeed = 1f;
        /// <summary>Shiny Red Balloon: Player.UpdateJumpHeight's jumpBoost.</summary>
        public static bool JumpBoost;
        /// <summary>rocketBoots: 1 Rocket Boots, 2 Spectre or Lightning Boots; 0 none.</summary>
        public static int RocketKind;
        static bool RocketBoots => RocketKind > 0;
        /// <summary>Frog Leg: jumpSpeedBoost, and auto-jump (holding jump jumps again on landing).</summary>
        public static float JumpSpeedBoost;
        public static bool AutoJump;
        /// <summary>The extra jumps owned (hasJumpOption_*).</summary>
        public static bool JumpCloud, JumpSandstorm, JumpBlizzard, JumpFart, JumpSail;
        /// <summary>Magiluminescence (a faster run on the ground) and the Portable Stool.</summary>
        public static bool Magiluminescence, PortableStool;
        /// <summary>spikedBoots: one slides down walls, two cling to them.</summary>
        public static int SpikedBoots;
        /// <summary>Wings: their slot's numbers (null = none), wingTimeMax and the air speed they allow.</summary>
        public static WingDef Wing;
        public static int WingTimeMax;
        public static float WingRunSpeed;
        public static bool HasWings => WingTimeMax > 0 && Wing != null;
        /// <summary>The Soaring Insignia.</summary>
        public static bool EmpressBrooch;
        /// <summary>The boots' run trail, the Fairy Boots' rocket dust, the sand boots, the flame trail.</summary>
        public static Items.RunDustKind RunDust;
        public static bool FairyDust, DesertBoots, FlameTrail;

        // UpdateJumpHeight (Pl:19719-19738): the werewolf jumps 2 ticks longer and 0.2 faster; the Soaring Insignia 1.8 faster.
        static float JumpSpeedNow => (JumpBoost ? 6.51f : JumpSpeed) + JumpSpeedBoost + (Werewolf ? 0.2f : 0f) + (EmpressBrooch ? 1.8f : 0f);
        static int JumpHeightNow => (JumpBoost ? 20 : JumpHeight) + (Werewolf ? 2 : 0);

        /// <summary>The werewolf (the Moon Charm at night).</summary>
        public static bool Werewolf;

        // Player.dashDelay (negative while dashing, then a cooldown), eocDash and eocHit, in ticks.
        public static float DashDelay { get; private set; }
        public static float EocDash { get; private set; }
        static bool eocHit;
        static int requestedDash;
        // Player.dash: the kind latched when the dash started (dash = dashType only while dashDelay is 0).
        static int dashing;
        static float dashDustTicks;
        static float runSoundDelay, cloudTicks, trailTicks, slideDustTicks, immuneTicks;
        static bool setInvulnerable;
        static readonly HashSet<HealthManager> bashed = new HashSet<HealthManager>();

        /// <summary>
        /// Afterimages: the Shield of Cthulhu's while dashing (eocDash / 4, at most 3); three
        /// through the ninja dash (armorEffectDrawShadow, Pl:37387-37394) and the Ram Rune's.
        /// </summary>
        public static int ShadowCount => DownDashing || (dashing != 2 && IsDashing) ? 3 : Mathf.Min(3, (int)(EocDash / 4f));
        public static bool IsDashing => DashDelay < 0f;
        /// <summary>The few ticks of immunity after a bonk; not a hit, so no blinking.</summary>
        public static bool IsBonkImmune => setInvulnerable;
        /// <summary>The Shield of Cthulhu dash, before it has bonked anything: the next enemy touched gets bonked.</summary>
        public static bool BonkReady => ShieldDash && EocDash > 0f && !eocHit;

        static bool Active => Enabled && TerraMod.Instance != null && World.TerrainSystem.Current != null;

        static readonly Action<HeroController> cancelWallsliding =
            AccessTools.MethodDelegate<Action<HeroController>>(AccessTools.Method(typeof(HeroController), "CancelWallsliding"));
        static readonly Action<HeroController> resetLook =
            AccessTools.MethodDelegate<Action<HeroController>>(AccessTools.Method(typeof(HeroController), "ResetLook"));
        static readonly AccessTools.FieldRef<HeroController, int> doubleJumpSteps = AccessTools.FieldRefAccess<HeroController, int>("doubleJump_steps");
        static readonly AccessTools.FieldRef<HeroController, bool> doubleJumped = AccessTools.FieldRefAccess<HeroController, bool>("doubleJumped");
        static readonly AccessTools.FieldRef<HeroController, float> walljumpSpeedDecel = AccessTools.FieldRefAccess<HeroController, float>("walljumpSpeedDecel");
        static readonly Func<HeroController, bool> canJump = AccessTools.MethodDelegate<Func<HeroController, bool>>(AccessTools.Method(typeof(HeroController), "CanJump"));
        static readonly Action<HeroController> heroJump = AccessTools.MethodDelegate<Action<HeroController>>(AccessTools.Method(typeof(HeroController), "HeroJump"));

        /// <summary>Retunes the Knight's jump, fall and wall moves to Terraria's; cheap enough to call every frame.</summary>
        public static void Apply(HeroController hero)
        {
            if (!Enabled) return;
            hero.JUMP_SPEED = JumpSpeedNow * PxT;
            // Off the Portable Stool the jump is 5 ticks longer (Pl:19739-19742).
            hero.JUMP_STEPS = Mathf.RoundToInt((JumpHeightNow + (stoolJump ? 5 : 0)) / 60f / Time.fixedDeltaTime);
            hero.JUMP_STEPS_MIN = 3;
            hero.MAX_FALL_VELOCITY = MaxFallSpeed * PxT;
            hero.WJ_KICKOFF_SPEED = WallJumpPush * PxT;
            hero.WJLOCK_STEPS_SHORT = 1;
            hero.WJLOCK_STEPS_LONG = 1;
        }

        /// <summary>A double tap of a direction (Terraria's dash input); acted on in the next physics step.</summary>
        public static void RequestDash(int direction) => requestedDash = direction;

        static int forcedKind;

        /// <summary>A dash now whatever the last one's cooldown (the Shade Cloak's shadow dash): the worn kind, the Shield's without one.</summary>
        public static void ForceDash(int direction)
        {
            if (DashDelay > 0f) DashDelay = 0f;
            forcedKind = 2;
            requestedDash = direction;
        }

        /// <summary>Dev: a held direction (-1/1) and jump button for the autotest, which has no keyboard.</summary>
        public static int DevMove;

        static readonly HarmonyLib.AccessTools.FieldRef<HeroController, bool> slidingL = HarmonyLib.AccessTools.FieldRefAccess<HeroController, bool>("wallSlidingL");
        static readonly HarmonyLib.AccessTools.FieldRef<HeroController, bool> slidingR = HarmonyLib.AccessTools.FieldRefAccess<HeroController, bool>("wallSlidingR");

        /// <summary>The wall the player slides down: 1 on the right, -1 on the left, 0 none.</summary>
        public static int WallSide(HeroController hero) =>
            hero == null || !hero.cState.wallSliding ? 0 : slidingR(hero) ? 1 : slidingL(hero) ? -1 : 0;
        public static bool DevJump;
        /// <summary>Dev: the last look for a gap in a wall alongside.</summary>
        public static string DevLastOpening = "";

        static bool JumpHeld => DevJump || GameHooks.Input.inputActions.jump.IsPressed;
        public static bool JumpHeldNow => JumpHeld;

        public static void ResetState()
        {
            DashDelay = 0f;
            EocDash = 0f;
            eocHit = false;
            requestedDash = 0;
            cloudTicks = 0f;
            activeJump = Extra.None;
            wingTime = 0f;
        }

        /// <summary>
        /// A room entered: an extra jump still lifting (the lift ran on from the last room and kept the player rising)
        /// ends, as does the Knight's double jump it rode on.
        /// </summary>
        public static void OnSceneEntered(HeroController hero)
        {
            cloudTicks = 0f;
            activeJump = Extra.None;
            if (hero != null) hero.cState.doubleJumping = false;
        }

        // --- Horizontal movement and the dash, after the Knight's own Move each physics step. ---

        /// <summary>
        /// The speed Terraria's run carries on from. A belt's (Crystal Peak's conveyors) the game adds
        /// after each step's move (FixedUpdate: on one standing, or in a conveyor zone) isn't the run's:
        /// left in, it was added again every step and the belt flung the player off.
        /// </summary>
        /// <summary>
        /// A belt under a Knight the game takes no input from (no_input, out of any transition or
        /// recoil: the moment after a hazard respawn): nothing moves him then, so nothing takes the
        /// speed the game's FixedUpdate adds each step back off, and it piled up (8, 16, 24... a belt of
        /// 8 flung him at 64). He rides at the belt's speed instead (twice it in a conveyor zone too).
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(HeroController), "FixedUpdate")]
        static void BeltWithoutInput(HeroController __instance, Rigidbody2D ___rb2d)
        {
            if (!Active || ___rb2d == null || __instance.hero_state != GlobalEnums.ActorStates.no_input) return;
            var cs = __instance.cState;
            if (cs.transitioning || cs.recoiling || cs.dead) return;
            bool belt = cs.onConveyor && cs.onGround && !cs.superDashing;
            if (!belt && !cs.inConveyorZone) return;
            float carried = (belt ? __instance.conveyorSpeed : 0f) + (cs.inConveyorZone ? __instance.conveyorSpeed : 0f);
            var v = ___rb2d.linearVelocity;
            if (Mathf.Abs(v.x - carried) > 0.01f) ___rb2d.linearVelocity = new Vector2(carried, v.y);
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(HeroController), "Move")]
        static void BeforeMove(HeroController __instance, Rigidbody2D ___rb2d, out float __state)
        {
            float vx = ___rb2d.linearVelocity.x;
            var cs = __instance.cState;
            // What the game's FixedUpdate added after the last step's move, by its own two rules: once for
            // standing on a belt (or landing hard on one), once more inside a conveyor zone. Taken off
            // again, so the run isn't carried on top of it step after step (a belt in a zone flung him).
            if (cs.onConveyor && ((cs.onGround && !cs.superDashing) || __instance.hero_state == GlobalEnums.ActorStates.hard_landing)) vx -= __instance.conveyorSpeed;
            if (cs.inConveyorZone) vx -= __instance.conveyorSpeed;
            __state = vx;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(HeroController), "Move")]
        static void AfterMove(HeroController __instance, float move_direction, Rigidbody2D ___rb2d, float __state)
        {
            if (!Active || !__instance.acceptingInput) return;
            float ticks = Time.fixedDeltaTime * 60f;
            var cs = __instance.cState;
            float vx = __state / PxT;
            if (DevMove != 0)
            {
                move_direction = DevMove;
                // Turned the way he walks, as the key would turn him (a side gate puts back one walked into backwards).
                if (!cs.wallSliding && !cs.attacking)
                {
                    if (DevMove > 0 && !cs.facingRight) __instance.FaceRight();
                    else if (DevMove < 0 && cs.facingRight) __instance.FaceLeft();
                }
            }
            bool left = move_direction < -0.1f, right = move_direction > 0.1f;
            if (!cs.wallSliding) vx = Horizontal(__instance, vx, left, right, cs.onGround, ticks);
            vx = Dash(__instance, ___rb2d, vx, left, right, ticks);
            if (cs.onGround && (left || right)) StepUp(__instance, ___rb2d, left ? -1 : 1);
            // In the air beside a wall, holding towards it: slip into a gap in it. Not only while
            // wall sliding: the Knight's own wall check lets go as soon as its rays reach the gap.
            if (!cs.onGround && (left || right) && EnterOpening(__instance, ___rb2d, left ? -1 : 1))
                vx = (left ? -1f : 1f) * Mathf.Max(Mathf.Abs(vx), 1.5f);
            ___rb2d.linearVelocity = new Vector2(vx * PxT, ___rb2d.linearVelocity.y);
        }

        /// <summary>
        /// Sliding down (or falling past) a wall and holding towards it, as in Terraria the player
        /// goes into a gap in the wall he fits through rather than sliding on past it: a gap whose
        /// floor is within half a block of his feet is enough. True when he was moved into one.
        /// </summary>
        static bool EnterOpening(HeroController hero, Rigidbody2D rb, int dir)
        {
            const float c = World.CellGrid.CellSize;
            var b = hero.GetComponent<BoxCollider2D>().bounds;
            if (World.TerraWorld.OwnGroundAt(b.center)) return false;
            int front = Mathf.FloorToInt((dir > 0 ? b.max.x + 0.04f : b.min.x - 0.04f) / c);
            int own0 = Mathf.FloorToInt(b.min.x / c), own1 = Mathf.FloorToInt((b.max.x - 0.001f) / c);
            int rows = Mathf.CeilToInt(b.size.y / c - 0.01f);
            int feet = Mathf.FloorToInt(b.min.y / c);
            // Already free in front all the way up: the wall isn't here, nothing to do.
            bool blocked = false;
            int top = Mathf.FloorToInt((b.max.y - 0.02f) / c);
            for (int y = Mathf.FloorToInt((b.min.y + 0.02f) / c); y <= top && !blocked; y++) blocked = World.TerraWorld.IsSolid(front, y);
            DevLastOpening = $"front={front} rows={rows} feet={feet} min.y={b.min.y:F2} blocked={blocked}";
            if (!blocked) return false;
            bool rising = rb.linearVelocity.y > 0.1f;
            foreach (int y0 in new[] { feet, feet + 1, feet - 1 })
            {
                float target = y0 * c + 0.01f;
                if (Mathf.Abs(target - b.min.y) > c * 1.2f) continue;
                // On the way up a gap below the feet has been passed; pulling him down would cut the jump short.
                if (rising && target < b.min.y - 0.05f) continue;
                bool fits = true;
                for (int k = 0; k < rows && fits; k++)
                {
                    if (World.TerraWorld.IsSolid(front, y0 + k)) fits = false;
                    for (int x = own0; x <= own1 && fits; x++) if (World.TerraWorld.IsSolid(x, y0 + k)) fits = false;
                }
                // Something to stand on in the gap.
                if (!fits || !World.TerraWorld.IsSolid(front, y0 - 1)) continue;
                var p = (Vector2)hero.transform.position + Vector2.up * (target - b.min.y);
                hero.transform.position = new Vector3(p.x, p.y, hero.transform.position.z);
                rb.position = p;
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);
                if (hero.cState.wallSliding) cancelWallsliding(hero);
                return true;
            }
            return false;
        }

        static readonly System.Action<HeroController> cancelJump =
            AccessTools.MethodDelegate<System.Action<HeroController>>(AccessTools.Method(typeof(HeroController), "CancelJump"));
        static readonly AccessTools.FieldRef<HeroController, bool> jumpQueuing = AccessTools.FieldRefAccess<HeroController, bool>("jumpQueuing");
        static readonly AccessTools.FieldRef<HeroController, bool> doubleJumpQueuing = AccessTools.FieldRefAccess<HeroController, bool>("doubleJumpQueuing");

        /// <summary>
        /// Letting go of jump. The Knight stops dead in the air (upward speed set to zero) for a
        /// short hop; in Terraria letting go only ends the jump's lift, and the speed already gained
        /// carries on and is worn away by gravity. The same goes for speed from anything else (a
        /// grappling hook's pull).
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(HeroController), "JumpReleased")]
        static bool JumpReleased(HeroController __instance)
        {
            if (!Active) return true;
            // Dev: the autotest's held jump isn't the real button the game sees let go.
            if (DevJump) return false;
            if (__instance.cState.jumping) cancelJump(__instance);
            jumpQueuing(__instance) = false;
            doubleJumpQueuing(__instance) = false;
            if (__instance.cState.swimming) __instance.cState.swimming = false;
            return false;
        }

        /// <summary>How far the drawn player still lags below the body after a step up (Terraria's gfxOffY).</summary>
        public static float StepOffset { get; private set; }

        /// <summary>
        /// Collision.StepUp: walking into a ledge one block high lifts the player onto it, as long
        /// as there is room above. Terrain is blocks here, so every slope is a staircase.
        /// </summary>
        static void StepUp(HeroController hero, Rigidbody2D rb, int dir)
        {
            const float c = World.CellGrid.CellSize;
            var b = hero.GetComponent<BoxCollider2D>().bounds;
            if (World.TerraWorld.OwnGroundAt(b.center)) return;
            float frontX = dir > 0 ? b.max.x + 0.04f : b.min.x - 0.04f;
            int fx = Mathf.FloorToInt(frontX / c);
            int feet = Mathf.FloorToInt((b.min.y + 0.04f) / c);
            if (!World.TerraWorld.IsSolid(fx, feet)) return;
            int height = Mathf.CeilToInt(b.size.y / c);
            for (int k = 1; k <= height; k++)
                if (World.TerraWorld.IsSolid(fx, feet + k)) return;
            // Head room over where the player stands now, for the lift itself.
            int ownX0 = Mathf.FloorToInt(b.min.x / c), ownX1 = Mathf.FloorToInt((b.max.x - 0.001f) / c);
            int top = Mathf.FloorToInt((b.max.y + c) / c);
            for (int x = ownX0; x <= ownX1; x++)
                if (World.TerraWorld.IsSolid(x, top)) return;
            float lift = (feet + 1) * c + 0.01f - b.min.y;
            if (lift <= 0.01f || lift > c + 0.06f) return;
            var p = (Vector2)hero.transform.position + Vector2.up * lift;
            hero.transform.position = new Vector3(p.x, p.y, hero.transform.position.z);
            rb.position = p;
            StepOffset -= lift;
        }

        /// <summary>The drawn player catches up with the body over a few ticks after a step up.</summary>
        public static void TickVisuals(float dt) => StepOffset = Mathf.MoveTowards(StepOffset, 0f, 5f * dt);

        static float stuckTime;

        /// <summary>
        /// The grid's colliders are hollow outlines, so a player who ends up inside solid cells
        /// (pushed by something, or placed there) could wander around in the rock. Pop them up to
        /// the first space they fit in.
        /// </summary>
        static void Unstick(HeroController hero, Rigidbody2D rb, float dt)
        {
            const float c = World.CellGrid.CellSize;
            var b = hero.GetComponent<BoxCollider2D>().bounds;
            var cell = World.CellGrid.WorldToCell(b.center);
            if (!World.TerraWorld.IsSolid(cell.x, cell.y) || !World.TerraWorld.InWorld(cell.x, cell.y) || World.TerraWorld.OwnGroundAt(b.center))
            {
                stuckTime = 0f;
                return;
            }
            stuckTime += dt;
            if (stuckTime < 0.15f) return;
            stuckTime = 0f;
            int height = Mathf.CeilToInt(b.size.y / c) + 1;
            int x0 = Mathf.FloorToInt(b.min.x / c), x1 = Mathf.FloorToInt((b.max.x - 0.001f) / c);
            for (int y = Mathf.FloorToInt(b.min.y / c); y < Mathf.FloorToInt(b.min.y / c) + 40; y++)
            {
                bool fits = true;
                for (int x = x0; x <= x1 && fits; x++)
                    for (int k = 0; k < height && fits; k++)
                        if (World.TerraWorld.IsSolid(x, y + k)) fits = false;
                if (!fits) continue;
                float lift = y * c + 0.02f - b.min.y;
                var p = (Vector2)hero.transform.position + Vector2.up * lift;
                hero.transform.position = new Vector3(p.x, p.y, hero.transform.position.z);
                rb.position = p;
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);
                Plugin.Log.LogInfo($"[移动] 主角卡在实心格里，向上挪出 {lift:F2}");
                return;
            }
        }

        /// <summary>Player.HorizontalMovement: accelerate to maxRunSpeed, then (boots) slowly on to accRunSpeed.</summary>
        static float Horizontal(HeroController hero, float vx, bool left, bool right, bool grounded, float t)
        {
            // Player.cs:26202: moveSpeed multiplies maxRunSpeed and runAcceleration, not the boots' accRunSpeed. The Vortex
            // stealth slows both run speeds ×0.3 before that (Pl:26022-26091).
            float stealthSlow = Items.ArmorEffects.RunSpeedMult;
            // The raised shield's move speed debuff (a third, Pl:26153-26156) is a moveSpeed.
            float moveSpeed = MoveSpeed * Items.MeleeSpecials.MoveSpeedMult;
            float maxRun = MaxRunSpeed * stealthSlow * moveSpeed, accel = RunAcceleration * moveSpeed;
            // The Sand Boots on sand (desertBoots, Pl:26740-26747): run speeds and acceleration ×1.75.
            float slowdown = RunSlowdown;
            bool sand = DesertBoots && grounded && OnSand(hero);
            // While an extra jump's lift lasts it speeds the run (Player.cs:26771).
            switch (activeJump)
            {
                case Extra.Sandstorm:
                    if (!HasWings) { accel *= 1.5f; maxRun *= 2f; }
                    break;
                case Extra.Blizzard: accel *= 3f; maxRun *= 1.5f; break;
                case Extra.Fart: accel *= 3f; maxRun *= 1.75f; break;
                case Extra.Sail: accel *= 1.5f; maxRun *= 1.25f; break;
            }
            // WingAirLogicTweaks (Pl:29534-29558): wings in the air allow their own top speed and acceleration; hovering down
            // with the hover wings, their hover speed (assigned) and acceleration.
            float accRun = AccRunSpeed * stealthSlow;
            // The Psycho Knife hidden (Pl:26816-26821): maxRunSpeed less half of it as hidden, and the boots' speed only that.
            float psycho = Items.MeleeSpecials.PsychoSlow;
            if (psycho > 0f)
            {
                maxRun -= maxRun / 2f * psycho;
                accRun = maxRun;
            }
            if (HasWings && !grounded)
            {
                if (Wing.HoverSpeed > 0f && DownHeld && JumpHeld && wingTime > 0f && !DownDashing)
                {
                    accRun = Wing.HoverSpeed;
                    accel *= Wing.HoverAccel;
                }
                else
                {
                    accRun = Mathf.Max(accRun, WingRunSpeed);
                    accel *= Wing.Accel;
                }
            }
            // The Soaring Insignia: runAcceleration ×1.75, always (Pl:26698-26701).
            if (EmpressBrooch) accel *= 1.75f;
            // Magiluminescence on the ground (Pl:26702-26708): acceleration and slowdown ×1.75, both run speeds ×1.15.
            if (Magiluminescence && grounded)
            {
                accel *= 1.75f;
                maxRun *= 1.15f;
                accRun *= 1.15f;
                slowdown *= 1.75f;
            }
            // Terraria shadowArmor: multiply both run-speed caps, acceleration and slowdown.
            if (Items.Equipment.FullSet == Items.ArmorSet.Shadow)
            {
                accel *= 1.75f;
                maxRun *= 1.15f;
                accRun *= 1.15f;
                slowdown *= 1.75f;
            }
            if (sand)
            {
                maxRun *= 1.75f;
                accRun *= 1.75f;
                accel *= 1.75f;
                slowdown *= 1.75f;
            }
            accRun = Mathf.Max(accRun, maxRun);
            if (OnCarpet)
            {
                maxRun *= 1.5f;
                accRun = Mathf.Max(accRun, maxRun);
                accel *= 1.25f;
            }
            float sprintDust = (accRun + maxRun) / 2f;
            if (left && vx > -maxRun)
            {
                if (vx > slowdown) vx -= slowdown * t;
                vx -= accel * t;
            }
            else if (right && vx < maxRun)
            {
                if (vx < -slowdown) vx += slowdown * t;
                vx += accel * t;
            }
            else if (left && vx > -accRun && DashDelay >= 0f)
            {
                // Above the run speed only the ground (or wings, twice as hard) speeds the player on.
                if (grounded || HasWings)
                {
                    if (vx > slowdown) vx -= slowdown * t;
                    vx -= accel * 0.2f * t;
                    if (HasWings) vx -= accel * 0.2f * t;
                }
                if (vx < -sprintDust && grounded) FastRunParticles(hero, vx, t);
            }
            else if (right && vx < accRun && DashDelay >= 0f)
            {
                if (grounded || HasWings)
                {
                    if (vx < -slowdown) vx += slowdown * t;
                    vx += accel * 0.2f * t;
                    if (HasWings) vx += accel * 0.2f * t;
                }
                if (vx > sprintDust && grounded) FastRunParticles(hero, vx, t);
            }
            else
            {
                // The Starboard stops on a dime in the air (runSlowdown ×6, Pl:29554-29557).
                float slow = grounded ? slowdown : slowdown * 0.5f;
                if (!grounded && HasWings && Wing.Slot == 45) slow *= 6f;
                vx = Mathf.MoveTowards(vx, 0f, slow * t);
            }
            return vx;
        }

        /// <summary>Whether the cell under the middle of the feet is sand (Conversion.Sand: placed sand here).</summary>
        static bool OnSand(HeroController hero)
        {
            var b = hero.GetComponent<BoxCollider2D>().bounds;
            var cell = World.CellGrid.WorldToCell(new Vector2(b.center.x, b.min.y - 0.05f));
            return World.TerraWorld.Get(cell.x, cell.y) == World.CellKind.Sand;
        }

        /// <summary>
        /// Player.SpawnFastRunParticles (Pl:20349-20436): the Run sound every 9 ticks and the
        /// boots' trail a tick at a time: Red's wings' dust 186, the sailfish's spray (253), desert
        /// sand (32), frost left hanging (76), fairy dust, fire (6); else Hermes's dust 16.
        /// </summary>
        static void FastRunParticles(HeroController hero, float vx, float t)
        {
            if (runSoundDelay <= 0f)
            {
                TerraMod.Instance.PlaySound("Run", 0.6f);
                runSoundDelay = 9f;
            }
            var p = TerraParticles.Instance;
            if (p == null) return;
            trailTicks += t;
            while (trailTicks >= 1f)
            {
                trailTicks -= 1f;
                var pos = TopLeft(hero);
                var feet = new Vector2(pos.x - 4f, pos.y + Height);
                int i;
                if (HasWings && Wing.Slot == 3)
                {
                    for (int k = 0; k < 2; k++)
                        if ((i = p.NewDust(feet, Width + 8, 4, 186, -vx * 0.5f, 0f, 50, 1.5f)) >= 0)
                            p[i].Velocity = new Vector2(p[i].Velocity.x * 0.025f, p[i].Velocity.y * 0.2f);
                    continue;
                }
                switch (RunDust)
                {
                    case Items.RunDustKind.Sail:
                        for (int k = 0; k < 4; k++)
                            if ((i = p.NewDust(new Vector2(pos.x - 4f, pos.y), Width + 8, Height, 253, 0f, 0f, 100, 1.5f + UnityEngine.Random.Range(-5, 3) * 0.1f)) >= 0)
                            {
                                p[i].NoGravity = true;
                                p[i].Velocity *= 0.2f;
                            }
                        continue;
                    case Items.RunDustKind.Desert:
                        if ((i = p.NewDust(feet, Width + 8, 4, 32, -vx * 0.5f, 0f)) >= 0)
                        {
                            p[i].Velocity *= 0.2f;
                            p[i].Velocity.y -= 2f;
                        }
                        continue;
                    case Items.RunDustKind.Cold:
                        for (int k = 0; k < 2; k++)
                            if ((i = p.NewDust(new Vector2(pos.x + (k == 0 ? 0f : Width / 2f), pos.y + Height), Width / 2, 6, 76, 0f, 0f, 0, 1.35f * UnityEngine.Random.Range(1.2f, 1.39f))) >= 0)
                            {
                                p[i].NoGravity = true;
                                p[i].Velocity *= 0.001f;
                                p[i].Velocity.y -= 0.003f;
                            }
                        continue;
                    case Items.RunDustKind.Fairy:
                        for (int k = 0; k < 2; k++)
                            if ((i = p.NewDust(feet, Width + 8, 4, FairyDustType(), 0f, 0f, 0, k == 0 ? 1.5f : 1f)) >= 0)
                            {
                                p[i].NoGravity = true;
                                p[i].Velocity *= 1.5f;
                                if (k == 1) p[i].Position += p[i].Velocity;
                            }
                        continue;
                    case Items.RunDustKind.Hellfire:
                        if ((i = p.NewDust(feet, Width + 8, 4, 6, 0f, 0f, 50, 2f)) >= 0)
                        {
                            p[i].Velocity.x *= 0.2f;
                            p[i].Velocity.y = -1.5f - UnityEngine.Random.value * 0.5f;
                            p[i].FadeIn = 0.5f;
                            p[i].NoGravity = true;
                        }
                        continue;
                }
                i = p.NewDust(feet, Width + 8, 4, 16, -vx * 0.5f, 0f, 50, 1.5f);
                if (i < 0) continue;
                ref var d = ref p[i];
                d.Velocity *= 0.2f;
            }
        }

        static int FairyDustType() => new[] { 61, 61, 61, 242, 64, 63 }[UnityEngine.Random.Range(0, 6)];

        static float flameTicks;

        /// <summary>
        /// The Hellfire Treads' trail (DoBootsEffect_PlaceFlamesOnTile, Pl:14178-14200): every other
        /// tick on the ground while moving faster than 1 px a tick, three flames on the cell under
        /// the feet.
        /// </summary>
        public static void FlameTrailStep(HeroController hero, Vector2 vPx, float t)
        {
            if (!FlameTrail || !hero.cState.onGround || GrapplingHook.IsLatched || Mathf.Abs(vPx.x) + Mathf.Abs(vPx.y) <= 1f) return;
            flameTicks += t;
            if (flameTicks < 2f) return;
            flameTicks -= 2f;
            var pos = TopLeft(hero);
            int cx = Mathf.FloorToInt((pos.x + Width / 2f) / 16f);
            Effects.FxParticles.FlameWaders(new Vector2(cx * 16f + 8f, pos.y + Height));
        }

        /// <summary>
        /// Player.DashMovement: the ninja dash (Tabi, dash 1: 16.9 px/tick, bleeding off ×0.992
        /// above 12 and ×0.96 above the run speed, 20 ticks' cooldown, smoke) and the Shield of
        /// Cthulhu's (dash 2: 14.5, ×0.985 / ×0.94, 30 ticks, the bonk).
        /// </summary>
        static float Dash(HeroController hero, Rigidbody2D rb, float vx, bool left, bool right, float t)
        {
            int request = requestedDash;
            requestedDash = 0;

            // A shadow dash (the Shade Cloak) goes through what it meets: no bonk, and none of a bonk's slowing.
            if (EocDash > 0f && !eocHit) { if (!Items.AbilityAccessories.ShadowDashing) Bash(hero, rb, ref vx); }
            else if (EocDash > 0f && !(left && vx < 0f) && !(right && vx > 0f)) vx *= Mathf.Pow(0.95f, t);

            // Pl:21427: the current maxRunSpeed, moveSpeed and all.
            float top = Mathf.Max(AccRunSpeed, MaxRunSpeed * MoveSpeed);
            // Dash 1 and the Crystal Assassin's 5 share the defaults; the Solar dash 3 bleeds from 14.
            bool ninja = dashing == 1 || dashing == 5;
            float cap = dashing == 3 ? 14f : 12f;
            if (DashDelay > 0f)
            {
                if (EocDash > 0f) EocDash = Mathf.Max(0f, EocDash - t);
                if (EocDash <= 0f) eocHit = false;
                DashDelay = Mathf.Max(0f, DashDelay - t);
            }
            else if (DashDelay < 0f)
            {
                if (dashing == 1) NinjaDashDust(hero, t);
                else if (dashing == 5) CrystalDashDust(hero, t);
                else if (dashing == 3)
                {
                    SolarDashDust(hero, t);
                    // The solar dash hits what it meets (Pl:21291-21348) and the player is left alone 4 ticks.
                    if (Items.ArmorEffects.SolarDashTouch(hero, vx)) immuneTicks = Mathf.Max(immuneTicks, 4f);
                }
                // Bleed the dash off quickly above 12 (14) px/tick, then faster down to running speed.
                if (Mathf.Abs(vx) > cap) vx *= Mathf.Pow(ninja ? 0.992f : 0.985f, t);
                else if (Mathf.Abs(vx) > top) vx *= Mathf.Pow(ninja ? 0.96f : 0.94f, t);
                else
                {
                    DashDelay = ninja || dashing == 3 ? 20f : 30f;
                    if (vx != 0f) vx = Mathf.Sign(vx) * top;
                }
            }
            else if ((DashKind != 0 || forcedKind != 0) && request != 0)
            {
                dashing = DashKind != 0 ? DashKind : forcedKind;
                forcedKind = 0;
                vx = (dashing == 3 ? SolarDashSpeed : dashing == 2 ? ShieldDashSpeed : NinjaDashSpeed) * request;
                // Half speed when starting the dash into a wall.
                var col = hero.GetComponent<BoxCollider2D>().bounds;
                float ahead = col.center.x + request * (Width / 2f + 2f) * Px;
                if (World.TerraWorld.IsSolidAt(new Vector2(ahead, col.center.y + (Height / 2f - 2f) * Px))
                    || World.TerraWorld.IsSolidAt(new Vector2(ahead, col.center.y)))
                    vx /= 2f;
                DashDelay = -1f;
                if (dashing == 2)
                {
                    EocDash = 15f;
                    eocHit = false;
                    bashed.Clear();
                }
                else if (dashing == 1) NinjaDashStart(hero);
                else if (dashing == 5) CrystalDashStart(hero);
                else SolarDashStart(hero);
                Items.ArmorEffects.OnDash(dashing);
                Items.AbilityAccessories.OnDashStart(hero, request);
                DevDashes++;
            }
            return vx;
        }

        /// <summary>Dev: dashes started.</summary>
        public static int DevDashes;
        /// <summary>Dev: the dash kind running now (0 when none).</summary>
        public static int DevDashKind => IsDashing ? dashing : 0;

        /// <summary>The ninja dash's start (Pl:21594-21609): twenty smoke dust over the body and two smoke gores, no sound.</summary>
        static void NinjaDashStart(HeroController hero)
        {
            var p = TerraParticles.Instance;
            if (p == null) return;
            var pos = TopLeft(hero);
            for (int k = 0; k < 20; k++)
            {
                int i = p.NewDust(pos, Width, Height, 31, 0f, 0f, 100, 2f);
                if (i < 0) continue;
                p[i].Position += new Vector2(UnityEngine.Random.Range(-5, 6), UnityEngine.Random.Range(-5, 6));
                p[i].Velocity *= 0.2f;
                p[i].Scale *= 1f + UnityEngine.Random.Range(0, 20) * 0.01f;
            }
            foreach (float y in new[] { -34f, -14f })
            {
                var v = new Vector2(UnityEngine.Random.Range(-50, 51) * 0.01f, UnityEngine.Random.Range(-50, 51) * 0.01f) * 0.4f;
                p.NewGore(new Vector2(pos.x + Width / 2f - 24f, pos.y + Height / 2f + y), v, UnityEngine.Random.Range(61, 64), exact: true);
            }
        }

        /// <summary>The Crystal Assassin's start (Pl:21675-21686): twenty crystal dust (68-70) flung out from the body.</summary>
        static void CrystalDashStart(HeroController hero)
        {
            var p = TerraParticles.Instance;
            if (p == null) return;
            var pos = TopLeft(hero);
            var c = pos + new Vector2(Width / 2f, Height / 2f);
            for (int k = 0; k < 20; k++)
            {
                int i = p.NewDust(pos, Width, Height, UnityEngine.Random.Range(68, 71), 0f, 0f, 0, 1f);
                if (i < 0) continue;
                p[i].Velocity = (p[i].Position - c).normalized * 2f;
                p[i].NoGravity = true;
            }
        }

        /// <summary>Its trail (Pl:21501-21516): two crystal dust a tick, slow, fading in.</summary>
        static void CrystalDashDust(HeroController hero, float t)
        {
            var p = TerraParticles.Instance;
            if (p == null) return;
            dashDustTicks += t;
            while (dashDustTicks >= 1f)
            {
                dashDustTicks -= 1f;
                var pos = TopLeft(hero);
                for (int k = 0; k < 2; k++)
                {
                    int i = p.NewDust(pos, Width, Height, UnityEngine.Random.Range(68, 71), 0f, 0f, 0, 1f);
                    if (i < 0) continue;
                    p[i].Velocity *= 0.2f;
                    p[i].FadeIn = UnityEngine.Random.Range(0.5f, 0.7f);
                    p[i].NoGravity = true;
                }
            }
        }

        /// <summary>The solar dash's start (Pl:21649-21659): twenty flames.</summary>
        static void SolarDashStart(HeroController hero)
        {
            var p = TerraParticles.Instance;
            if (p == null) return;
            var pos = TopLeft(hero);
            for (int k = 0; k < 20; k++) p.NewDust(pos, Width, Height, 6, 0f, 0f, 100, 2f);
        }

        /// <summary>Its trail (Pl:21459-21475): four floating flames a tick, half of them fading in.</summary>
        static void SolarDashDust(HeroController hero, float t)
        {
            var p = TerraParticles.Instance;
            if (p == null) return;
            dashDustTicks += t;
            while (dashDustTicks >= 1f)
            {
                dashDustTicks -= 1f;
                var pos = TopLeft(hero);
                for (int k = 0; k < 4; k++)
                {
                    int i = p.NewDust(pos, Width, Height, 6, 0f, 0f, 100, 1.7f);
                    if (i < 0) continue;
                    p[i].NoGravity = true;
                    if (UnityEngine.Random.Range(0, 2) == 0) p[i].FadeIn = 0.5f;
                }
            }
        }

        /// <summary>The ninja dash's trail (Pl:21432-21443): two smoke dust a tick, at the waist in the air and the feet on the ground.</summary>
        static void NinjaDashDust(HeroController hero, float t)
        {
            var p = TerraParticles.Instance;
            if (p == null) return;
            dashDustTicks += t;
            while (dashDustTicks >= 1f)
            {
                dashDustTicks -= 1f;
                var pos = TopLeft(hero);
                bool air = !hero.cState.onGround;
                for (int k = 0; k < 2; k++)
                {
                    int i = air ? p.NewDust(new Vector2(pos.x, pos.y + Height / 2f - 8f), Width, 16, 31, 0f, 0f, 100, 1.4f)
                                : p.NewDust(new Vector2(pos.x, pos.y + Height - 4f), Width, 8, 31, 0f, 0f, 100, 1.4f);
                    if (i < 0) continue;
                    p[i].Velocity *= 0.1f;
                    p[i].Scale *= 1f + UnityEngine.Random.Range(0, 20) * 0.01f;
                }
            }
        }

        /// <summary>
        /// The bonk: the first enemy the dashing player touches takes 30 damage, and the player
        /// bounces back off it (velocity -9 x direction, 4 px/tick up) with a moment of immunity.
        /// </summary>
        static void Bash(HeroController hero, Rigidbody2D rb, ref float vx)
        {
            var col = hero.GetComponent<BoxCollider2D>().bounds;
            var v = rb.linearVelocity / PxT; // px/tick, y up
            var center = (Vector2)col.center + v * 0.5f * Px;
            var size = new Vector2(Width + 8, Height + 8) * Px;
            int dir = vx != 0f ? Math.Sign(vx) : hero.cState.facingRight ? 1 : -1;
            foreach (var h in Physics2D.OverlapBoxAll(center, size, 0f, 1 << 11))
            {
                var hm = h.GetComponentInParent<HealthManager>();
                if (hm == null || hm.isDead || !bashed.Add(hm)) continue;
                // A Terraria hit like any other (scaled to the enemy's health, numbers and blood included).
                TerraMod.Instance.Melee.HitWith(h, ShieldBashDamage, 9f, 4f, dir, col.center);
                eocHit = true;
            }
            if (!eocHit) return;
            EocDash = 10f;
            DashDelay = 30f;
            vx = -dir * 9f;
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, 4f * PxT);
            immuneTicks = 4f;
        }

        // --- Wall slide (Climbing Claws), and the bonk's immunity, before each physics step. ---

        [HarmonyPrefix]
        [HarmonyPatch(typeof(HeroController), "FixedUpdate")]
        static void BeforeFixedUpdate(HeroController __instance, bool ___wallSlidingL, Rigidbody2D ___rb2d)
        {
            if (!Active) return;
            float t = Time.fixedDeltaTime * 60f;
            if (runSoundDelay > 0f) runSoundDelay -= t;
            var cs = __instance.cState;
            if (!cs.transitioning && !cs.dead && __instance.acceptingInput) Unstick(__instance, ___rb2d, Time.fixedDeltaTime);

            if (immuneTicks > 0f)
            {
                immuneTicks -= t;
                if (!cs.invulnerable)
                {
                    cs.invulnerable = true;
                    setInvulnerable = true;
                }
            }
            else if (setInvulnerable)
            {
                cs.invulnerable = false;
                setInvulnerable = false;
            }

            // The Frog Leg's autoJump: holding jump on the ground jumps again (Player.cs:20843).
            if (AutoJump && JumpHeld && cs.onGround && !cs.jumping && !cs.wallSliding && __instance.acceptingInput && canJump(__instance))
            {
                heroJump(__instance);
                if (HasWings) wingTime = WingTimeMax;
            }

            if (!cs.wallSliding) return;
            var input = GameHooks.Input.inputActions;
            int slideDir = ___wallSlidingL ? -1 : 1;
            // Terraria slides only while the player holds towards the wall.
            if (!(slideDir < 0 ? input.left.IsPressed || DevMove < 0 : input.right.IsPressed || DevMove > 0))
            {
                cancelWallsliding(__instance);
                return;
            }
            // WallslideMovement (Pl:21891, 21964): a slide starts the fall again where the player is, every tick of it.
            TerraMod.Instance?.ResetFall();
            // spikedBoots 2 (the claws and the spikes): a dead stop on the wall, without dust, until down is held.
            bool down = input.down.IsPressed || DevDown;
            bool cling = SpikedBoots >= 2 && !down;
            __instance.WALLSLIDE_SPEED = -(down ? SlideDownSpeed : cling ? 0f : SlideSpeed) * PxT;

            var p = TerraParticles.Instance;
            if (p == null || cling) return;
            slideDustTicks += t;
            while (slideDustTicks >= 1f)
            {
                slideDustTicks -= 1f;
                var pos = TopLeft(__instance);
                float x = pos.x + Width / 2f + (Width / 2f - 4f) * slideDir - (slideDir < 0 ? 10f : 0f);
                float y = pos.y + Height / 2f + (Height / 2f - 4f);
                int i = p.NewDust(new Vector2(x, y), 8, 8, 31);
                if (i < 0) continue;
                ref var d = ref p[i];
                d.Velocity *= 0.1f;
                d.Scale *= 1.2f;
                d.NoGravity = true;
            }
        }

        /// <summary>
        /// Terraria sets the slide speed outright. The Knight eases towards it by WALLSLIDE_DECEL
        /// per step, and pushing that up to make it instant overshoots upwards when falling
        /// faster than the slide (the hero shot up the wall), so set it after the step instead.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(HeroController), "FixedUpdate")]
        static void AfterFixedUpdate(HeroController __instance, Rigidbody2D ___rb2d)
        {
            if (!Active || !__instance.cState.wallSliding || __instance.cState.onConveyorV) return;
            // The physics step still adds its gravity after this: pay it in advance, so the slide is the slide speed.
            float g = -Physics2D.gravity.y * ___rb2d.gravityScale * Time.fixedDeltaTime;
            ___rb2d.linearVelocity = new Vector2(___rb2d.linearVelocity.x, __instance.WALLSLIDE_SPEED + g);
        }

        /// <summary>
        /// The Knight lets go of a wall when down is pressed; in Terraria, holding down slides
        /// faster instead. Undo that one release.
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(HeroController), "LookForInput")]
        static void BeforeLookForInput(HeroController __instance, bool ___wallSlidingL, bool ___wallSlidingR, out (bool sliding, bool l, bool r) __state) =>
            __state = (__instance.cState.wallSliding, ___wallSlidingL, ___wallSlidingR);

        [HarmonyPostfix]
        [HarmonyPatch(typeof(HeroController), "LookForInput")]
        static void AfterLookForInput(HeroController __instance, (bool sliding, bool l, bool r) __state,
            ref bool ___wallSlidingL, ref bool ___wallSlidingR, ref bool ___touchingWallL, ref bool ___touchingWallR)
        {
            if (!Active || !__state.sliding || __instance.cState.wallSliding) return;
            if (!GameHooks.Input.inputActions.down.WasPressed) return;
            __instance.cState.wallSliding = true;
            ___wallSlidingL = ___touchingWallL = __state.l;
            ___wallSlidingR = ___touchingWallR = __state.r;
            __instance.FlipSprite();
        }

        // --- Platforms and rope. ---

        static readonly AccessTools.FieldRef<HeroController, int> ledgeBufferSteps = AccessTools.FieldRefAccess<HeroController, int>("ledgeBufferSteps");
        /// <summary>Holding on to a rope.</summary>
        public static bool OnRope { get; private set; }
        const float RopeClimbUp = 2.5f, RopeClimbDown = 3f;

        internal static bool DownHeld => GameHooks.Input.inputActions.down.IsPressed || DevDown;
        static bool UpHeld => GameHooks.Input.inputActions.up.IsPressed || DevUp;
        /// <summary>Dev: held up/down for the autotest.</summary>
        public static bool DevDown, DevUp;

        /// <summary>
        /// Standing on platforms and holding down drops through them (Terraria lets the player
        /// fall through while down is held). Touching rope and pressing up or down grabs it: the
        /// player hangs without falling, climbs, and jumps off as from the ground.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(HeroController), "FixedUpdate")]
        static void PlatformsAndRope(HeroController __instance, Rigidbody2D ___rb2d)
        {
            // Terraria falls up to 10 px a tick: discrete steps sink the player more than half a
            // platform deep in one, and a one-way platform takes that for coming up through it (the
            // fall goes on through). Swept collision meets the platform's top as it is reached.
            if (Active && ___rb2d != null && ___rb2d.collisionDetectionMode != CollisionDetectionMode2D.Continuous)
                ___rb2d.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            if (!Active || !__instance.acceptingInput || __instance.cState.dead || __instance.cState.transitioning) { OnRope = false; return; }
            var cs = __instance.cState;
            var b = __instance.GetComponent<BoxCollider2D>().bounds;
            const float c = World.CellGrid.CellSize;
            int x0 = Mathf.FloorToInt(b.min.x / c), x1 = Mathf.FloorToInt((b.max.x - 0.001f) / c);
            int below = Mathf.FloorToInt((b.min.y - 0.05f) / c);

            // Dropping through.
            if (DownHeld && !OnRope && (cs.onGround || ___rb2d.linearVelocity.y <= 0.01f))
            {
                bool platform = false, solid = false;
                for (int x = x0; x <= x1; x++)
                {
                    if (World.TerraWorld.IsPlatform(x, below)) platform = true;
                    if (World.TerraWorld.IsSolid(x, below)) solid = true;
                }
                if (platform && !solid)
                {
                    var area = new RectInt(x0 - 1, below - 1, x1 - x0 + 3, 4);
                    foreach (var t in World.TerrainSystem.All) t.Region?.SuspendPlatforms(area, 0.3f);
                    if (___rb2d.linearVelocity.y > -2f * PxT) ___rb2d.linearVelocity = new Vector2(___rb2d.linearVelocity.x, -2f * PxT);
                }
                // A Work Bench or an anvil stood on: through its top the same way.
                else if (!solid && World.Stations.DropThrough(b, 0.3f) && ___rb2d.linearVelocity.y > -2f * PxT)
                    ___rb2d.linearVelocity = new Vector2(___rb2d.linearVelocity.x, -2f * PxT);
            }

            // Rope: any of the hero's rows in the column under his centre.
            int col = Mathf.FloorToInt(b.center.x / c);
            bool touching = false;
            for (int y = Mathf.FloorToInt(b.min.y / c); y <= Mathf.FloorToInt((b.max.y - 0.001f) / c) && !touching; y++)
                touching = World.TerraWorld.Get(col, y) == World.CellKind.Rope;
            if (!OnRope && touching && (UpHeld || DownHeld) && !cs.jumping && !GrapplingHook.IsLatched) OnRope = true;
            if (!OnRope) return;
            if (!touching || cs.jumping || cs.onGround && !UpHeld || GrapplingHook.IsLatched)
            {
                OnRope = false;
                return;
            }
            float vy = UpHeld ? RopeClimbUp : DownHeld ? -RopeClimbDown : 0f;
            float g = -Physics2D.gravity.y * ___rb2d.gravityScale * Time.fixedDeltaTime;
            // Centred on the rope, no fall: the next physics step's gravity is cancelled in advance.
            float ropeX = (col + 0.5f) * c;
            var p = __instance.transform.position;
            __instance.transform.position = new Vector3(Mathf.MoveTowards(p.x, p.x + (ropeX - b.center.x), 0.08f), p.y, p.z);
            ___rb2d.linearVelocity = new Vector2(0f, vy * PxT + g);
            if (cs.wallSliding) cancelWallsliding(__instance);
            // A jump from the rope is a jump from the ground (the Knight's coyote time lets it start).
            ledgeBufferSteps(__instance) = 2;
            doubleJumped(__instance) = false;
            cloudTicks = 0f;
            activeJump = Extra.None;
            RefreshJumps();
            wingTime = WingTimeMax;
            rocketFuel = RocketFuel;
            canCarpet = true;
            TerraMod.Instance.ResetFall();
        }

        // --- Rocket Boots (rocketBoots 1, rocketTimeMax 7) and the Flying Carpet. ---

        const int RocketFuel = 7;
        const float PulseTicks = 10f, CarpetTicks = 300f;
        static int rocketFuel = RocketFuel;
        static float pulseLeft, rocketSoundDelay, rocketDustTicks;
        /// <summary>rocketRelease: jump has been let go since the last jump of any kind started.</summary>
        static bool rocketRelease;

        public static bool FlyingCarpet;
        static bool canCarpet;
        static float carpetTime, carpetFrameCounter;
        /// <summary>The carpet's animation frame (0-5), or -1 when not riding it.</summary>
        public static int CarpetFrame { get; private set; } = -1;
        public static bool OnCarpet => CarpetFrame >= 0;

        /// <summary>
        /// Player.RefreshMovementAbilities as hanging on a grappling hook does it, every tick: the
        /// boots' fuel back and any burst cut off, jump to be let go before they fire again, and
        /// the carpet ready for another ride (not drawn while hanging).
        /// </summary>
        public static void RefreshFlight()
        {
            RefreshJumps();
            wingTime = WingTimeMax;
            rocketFuel = RocketFuel;
            pulseLeft = 0f;
            rocketRelease = false;
            canCarpet = true;
            CarpetFrame = -1;
        }

        /// <summary>
        /// One step of the rocket and wing thrust (y up, px/tick): 0.1 a tick, 0.5 more while
        /// falling, 0.1 more below half jump speed, never faster than 1.5 times jump speed up.
        /// </summary>
        static float Thrust(float vy, float t)
        {
            float before = vy;
            vy += 0.1f * t;
            if (before < 0f) vy += 0.5f * t;
            else if (before < JumpSpeedNow * 0.5f) vy += 0.1f * t;
            return Mathf.Min(vy, JumpSpeedNow * 1.5f);
        }

        /// <summary>Any jump starting (from the ground, a rope, a cloud) disarms the boots until jump is let go (Player.cs:20923).</summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(HeroController), "HeroJump")]
        static void AfterHeroJump(HeroController __instance, Rigidbody2D ___rb2d)
        {
            rocketRelease = false;
            // Off the Portable Stool the jump starts from its top (position.Y -= 26, Pl:20941-20950).
            if (StoolActive)
            {
                StoolActive = false;
                stoolJump = true;
                var p = (Vector2)__instance.transform.position + Vector2.up * StoolLift;
                __instance.transform.position = new Vector3(p.x, p.y, __instance.transform.position.z);
                ___rb2d.position = p;
            }
            // A ground jump gives every extra jump back (RefreshDoubleJumps, Player.cs:20928).
            RefreshJumps();
            activeJump = Extra.None;
        }

        /// <summary>
        /// The boots fire in bursts: holding jump in the air (having let go of it since the last
        /// jump, and not rising faster than a jump) starts a ten-tick burst of thrust that runs its
        /// course whether or not jump stays held, seven to a flight, refilled on the ground, on a
        /// wall, on a rope and on a hook. Thrust 0.1 a tick (0.5 more while still falling, 0.1 more
        /// below half jump speed) up to 1.5 times jump speed, gravity off; Item_13 at most every
        /// 30 ticks; a flame from each boot every tick (Player.cs:26966-27160).
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(HeroController), "FixedUpdate")]
        static void Rockets(HeroController __instance, Rigidbody2D ___rb2d)
        {
            if (!Active) return;
            float t = Time.fixedDeltaTime * 60f;
            var cs = __instance.cState;
            if (rocketSoundDelay > 0f) rocketSoundDelay -= t;
            if (!JumpHeld) rocketRelease = true;
            DownDashStep(__instance, ___rb2d, t);
            StoolStep(__instance, ___rb2d);
            // RefreshDoubleJumps on the ground or a wall; an option no longer owned is gone.
            canSandstorm &= JumpSandstorm; canBlizzard &= JumpBlizzard; canFart &= JumpFart; canSail &= JumpSail; canCloud &= JumpCloud;
            if (cs.onGround || cs.wallSliding) RefreshJumps();
            miscTicks += t;
            while (miscTicks >= 1f)
            {
                miscTicks -= 1f;
                if (++miscCounter % 4 == 0) SpinFacing = -SpinFacing;
            }
            // The Soaring Insignia: the rocket boots never run out (Pl:27063-27066).
            if (EmpressBrooch && RocketBoots) rocketFuel = RocketFuel;
            Wings(__instance, ___rb2d, t);
            Carpet(__instance, ___rb2d, t);
            // No rocket boots while the Ram Rune's dash is on (Pl:3972-3984).
            if (DownDashing) pulseLeft = 0f;
            if (!RocketBoots)
            {
                // rocketTime = 0 without boots: putting them back on in the air gives no fresh fuel.
                pulseLeft = 0f;
                rocketFuel = 0;
                return;
            }
            if (cs.onGround || cs.wallSliding)
            {
                rocketFuel = RocketFuel;
                pulseLeft = 0f;
                return;
            }
            if (!__instance.acceptingInput || GrapplingHook.IsLatched || cs.dead)
            {
                pulseLeft = 0f;
                return;
            }
            if (DownDashing) return;
            var v = ___rb2d.linearVelocity / PxT;
            bool canRocket = v.y < JumpSpeedNow;
            if (pulseLeft <= 0f && JumpHeld && canRocket && rocketRelease && rocketFuel > 0)
            {
                rocketFuel--;
                pulseLeft = PulseTicks;
                // Pl:27084-27093: the Rocket Boots and the Hellfire Treads roar Item_13 at most every 30 ticks; the rest Item_24 every 15.
                bool roar = RocketKind == 1 || RocketKind == 5;
                if (rocketSoundDelay <= 0f)
                {
                    rocketSoundDelay = roar ? 30f : 15f;
                    TerraMod.Instance.PlaySound(roar ? "Item_13" : "Item_24");
                }
            }
            if (pulseLeft <= 0f) return;
            pulseLeft -= t;
            // Thrust with no gravity: the speed it should end the step at, with this step's gravity paid in advance.
            float g = -Physics2D.gravity.y * ___rb2d.gravityScale * Time.fixedDeltaTime / PxT;
            v.y = Thrust(v.y, t) + g;
            ___rb2d.linearVelocity = v * PxT;
            TerraMod.Instance.ResetFall();

            // Player.RocketBootVisuals: a flame from each boot every tick.
            var p = TerraParticles.Instance;
            if (p == null) return;
            rocketDustTicks += t;
            int dir = __instance.cState.facingRight ? 1 : -1;
            while (rocketDustTicks >= 1f)
            {
                rocketDustTicks -= 1f;
                var pos = TopLeft(__instance);
                float shift = dir < 0 ? -4f : 0f;
                for (int foot = 0; foot < 2; foot++)
                {
                    float fx = (foot == 0 ? pos.x - 4f : pos.x + Width - 4f) + shift;
                    var rect = new Vector2(fx, pos.y + Height - 10f);
                    // v2: thrown away from the boots and behind the player's own motion (y down).
                    var v2 = new Vector2((foot == 0 ? -2f : 2f) - v.x * 0.3f, 2f + v.y * 0.3f);
                    RocketDust(p, rect, v2, new Vector2(v.x, -v.y));
                }
            }
        }

        /// <summary>
        /// Player.RocketBootVisuals (Pl:29560-29687), one foot: the Rocket Boots' and the Hellfire
        /// Treads' flame (6), the Spectre / Lightning boots' grey cloud (16) or the Fairy Boots'
        /// fairy dust, the Frostspark's frosty mist (76), the Terraspark's green sparks (278, 107).
        /// </summary>
        static void RocketDust(TerraParticles p, Vector2 rect, Vector2 v2, Vector2 vel)
        {
            int i;
            switch (RocketKind)
            {
                case 2:
                    if (FairyDust)
                    {
                        if ((i = p.NewDust(rect, 8, 8, FairyDustType(), 0f, 0f, 120, 2f)) < 0) return;
                        p[i].Velocity *= 0.1f;
                        p[i].NoGravity = true;
                        return;
                    }
                    if ((i = p.NewDust(rect, 8, 8, 16, 0f, 0f, 20, 1.5f)) < 0) return;
                    p[i].Velocity = (p[i].Velocity + v2) * 0.1f;
                    return;
                case 3:
                    if ((i = p.NewDust(rect, 8, 8, 76, 0f, 0f, 20, 1f)) < 0) return;
                    p[i].Velocity = (p[i].Velocity + v2) * 0.05f;
                    p[i].Velocity.y += 0.15f;
                    if (UnityEngine.Random.Range(0, 2) == 0)
                    {
                        p[i].NoGravity = true;
                        p[i].Scale = 1.75f;
                    }
                    return;
                case 4:
                    if (UnityEngine.Random.Range(0, 6) < 2)
                    {
                        if ((i = p.NewDust(rect + new Vector2(0f, 2f), 8, 8, 278, 0f, 0f, 0, 0.66f)) < 0) return;
                        p[i].Color = Color.Lerp(new Color(50f / 255f, 205f / 255f, 50f / 255f), Color.white, UnityEngine.Random.value * 0.3f);
                        p[i].NoGravity = true;
                        p[i].Velocity = p[i].Velocity * 0.25f - vel * 0.5f + v2 * 0.5f;
                        p[i].Position += p[i].Velocity * 4f;
                        if (UnityEngine.Random.Range(0, 5) == 0) p[i].FadeIn = 0.8f;
                        return;
                    }
                    if ((i = p.NewDust(rect + new Vector2(0f, 2f), 8, 8, 107, 0f, 0f, 100, 0.7f)) < 0) return;
                    p[i].Velocity = (p[i].Velocity + v2) * 0.5f;
                    return;
                default:
                    if ((i = p.NewDust(rect, 8, 8, 6, 0f, 0f, 100, 2.5f)) < 0) return;
                    p[i].NoGravity = true;
                    p[i].Velocity += v2;
                    return;
            }
        }

        /// <summary>
        /// Player.CarpetMovement: with every double jump used, the boots spent and the jump's own
        /// lift over, holding jump in the air rolls the carpet out: five seconds of level flight
        /// while jump is held (the clock pauses when it's let go), run speed half again. It comes
        /// back on landing, on a wall, on a rope or on a hook.
        /// </summary>
        static void Carpet(HeroController hero, Rigidbody2D rb, float t)
        {
            var cs = hero.cState;
            if (!FlyingCarpet || DownDashing)
            {
                canCarpet = false;
                CarpetFrame = -1;
                return;
            }
            if (cs.onGround || cs.wallSliding || OnRope)
            {
                canCarpet = true;
                CarpetFrame = -1;
                carpetTime = 0f;
                carpetFrameCounter = 0f;
                return;
            }
            if (GrapplingHook.IsLatched)
            {
                canCarpet = true;
                CarpetFrame = -1;
                return;
            }
            bool jumpsUsed = !PlayerHooks.Abilities.DoubleJump || doubleJumped(hero);
            bool liftOver = !cs.jumping && !cs.doubleJumping;
            bool rocketsSpent = (!RocketBoots || (rocketFuel <= 0 && pulseLeft <= 0f)) && (!HasWings || wingTime <= 0f);
            bool riding = false;
            if (jumpsUsed && liftOver && rocketsSpent && hero.acceptingInput && !cs.dead)
            {
                if (JumpHeld && canCarpet)
                {
                    canCarpet = false;
                    carpetTime = CarpetTicks;
                }
                if (carpetTime > 0f && JumpHeld)
                {
                    TerraMod.Instance.ResetFall();
                    carpetTime -= t;
                    var v = rb.linearVelocity / PxT;
                    // Falling or barely rising: hold level (gravity this step is cancelled in advance).
                    if (v.y < 0.4f)
                    {
                        float g = -Physics2D.gravity.y * rb.gravityScale * Time.fixedDeltaTime;
                        rb.linearVelocity = new Vector2(rb.linearVelocity.x, g);
                    }
                    carpetFrameCounter += (1f + Mathf.Abs(v.x * 0.5f)) * t;
                    if (CarpetFrame < 0) CarpetFrame = 0;
                    while (carpetFrameCounter > 8f)
                    {
                        carpetFrameCounter -= 8f;
                        CarpetFrame = (CarpetFrame + 1) % 6;
                    }
                    riding = true;
                }
            }
            if (!riding) CarpetFrame = -1;
        }

        // --- Wall jump: Terraria's push of 3 px/tick away from the wall, then free control. ---

        [HarmonyPostfix]
        [HarmonyPatch(typeof(HeroController), "DoWallJump")]
        static void AfterWallJump(HeroController __instance)
        {
            if (!Active) return;
            walljumpSpeedDecel(__instance) = 0f;
            if (__instance.wallPuffPrefab != null) __instance.wallPuffPrefab.SetActive(false);
        }

        // --- The Portable Stool. ---

        /// <summary>The player stands on the stool: drawn 26 px up, reaching 26 px higher.</summary>
        public static bool StoolActive { get; private set; }
        /// <summary>The stool's 26 px, in world units.</summary>
        public static float StoolLift => 26f * Px;
        static bool stoolJump;

        /// <summary>
        /// UpdatePortableStoolUsage (Pl:29423-29435): holding UP standing perfectly still, not on a
        /// rope or a hook, with a clear 26 px over the head; the feet stay put.
        /// </summary>
        static void StoolStep(HeroController hero, Rigidbody2D rb)
        {
            var cs = hero.cState;
            if (cs.onGround && rb.linearVelocity.y <= 0f && !cs.jumping) stoolJump = false;
            bool use = PortableStool && UpHeld && cs.onGround && rb.linearVelocity.sqrMagnitude < 1e-4f && !OnRope && !GrapplingHook.IsLatched
                       && hero.acceptingInput && !cs.dead;
            if (use)
            {
                var b = hero.GetComponent<BoxCollider2D>().bounds;
                var room = new Vector2(b.center.x, b.max.y + StoolLift / 2f + 0.02f);
                use = Physics2D.OverlapBox(room, new Vector2(Width * Px, StoolLift), 0f, 1 << World.ChunkColliders.TerrainLayer) == null;
            }
            StoolActive = use;
        }

        // --- The extra jumps: Sandstorm in a Bottle, Blizzard in a Bottle, Fart in a Jar, Tsunami in a Bottle, Cloud in a Bottle. ---

        enum Extra { None, Sandstorm, Blizzard, Fart, Sail, Cloud }
        /// <summary>The extra jump whose lift is running (isPerformingJump_*), and those left this flight (canJumpAgain_*).</summary>
        static Extra activeJump;
        static bool canSandstorm, canBlizzard, canFart, canSail, canCloud;
        static float miscTicks;
        static int miscCounter;

        /// <summary>The sandstorm's spin: which way the player faces this tick (Player.PlayerFrame flips it every 4 ticks).</summary>
        public static float SpinFacing { get; private set; } = 1f;
        /// <summary>The sandstorm jump is lifting: the player spins (body frame 6, legs 0); not with wings (sandStorm is cleared).</summary>
        public static bool Sandstorm => activeJump == Extra.Sandstorm && !HasWings;

        static void RefreshJumps()
        {
            canSandstorm = JumpSandstorm;
            canBlizzard = JumpBlizzard;
            canFart = JumpFart;
            canSail = JumpSail;
            canCloud = JumpCloud;
        }

        static bool AnyJumpLeft => canSandstorm || canBlizzard || canFart || canSail || canCloud;

        /// <summary>Dev: the extra jump lifting now, and the wings' time left (ticks).</summary>
        public static string DevJumpName => activeJump.ToString();
        public static float DevWingTime => wingTime;
        /// <summary>Dev: a jump from the ground as the button would start it.</summary>
        public static void DevGroundJump(HeroController hero) => heroJump(hero);

        /// <summary>
        /// Player.JumpMovement's extra jumps (the Knight's double jump spreads wings): the first one
        /// left of Sandstorm, Blizzard, Fart, Sail and Cloud, each with its own lift (jumpHeight × 3,
        /// 1.5, 2, 1.25 and 0.75 ticks) at jump speed, its sound and its burst; the Knight's double
        /// jump is armed again while any is left.
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(HeroController), "DoDoubleJump")]
        static bool DoDoubleJump(HeroController __instance, Rigidbody2D ___rb2d)
        {
            if (!Active) return true;
            // The Ram Rune's dash comes before the extra jumps and uses none of them (Pl:20893-20896).
            if (DownDash && DownHeld && !DownDashing)
            {
                downDashRequest = true;
                doubleJumped(__instance) = !AnyJumpLeft;
                return false;
            }
            var pick = canSandstorm ? Extra.Sandstorm : canBlizzard ? Extra.Blizzard : canFart ? Extra.Fart : canSail ? Extra.Sail : canCloud ? Extra.Cloud : Extra.None;
            if (pick == Extra.None)
            {
                doubleJumped(__instance) = true;
                return false;
            }
            switch (pick)
            {
                case Extra.Sandstorm: canSandstorm = false; cloudTicks = JumpHeightNow * 3; break;
                case Extra.Blizzard: canBlizzard = false; cloudTicks = (int)(JumpHeightNow * 1.5f); break;
                case Extra.Fart: canFart = false; cloudTicks = JumpHeightNow * 2; break;
                case Extra.Sail: canSail = false; cloudTicks = (int)(JumpHeightNow * 1.25f); break;
                default: canCloud = false; cloudTicks = (int)(JumpHeightNow * 0.75f); break;
            }
            activeJump = pick;
            SpinFacing = __instance.cState.facingRight ? 1f : -1f;
            resetLook(__instance);
            var cs = __instance.cState;
            cs.jumping = false;
            cs.doubleJumping = true;
            doubleJumpSteps(__instance) = 0;
            doubleJumped(__instance) = !AnyJumpLeft;
            rocketRelease = false;
            trailTicks = 0f;
            ___rb2d.linearVelocity = new Vector2(___rb2d.linearVelocity.x, JumpSpeedNow * PxT);
            TerraMod.Instance.PlaySound(pick == Extra.Fart ? "Item_16" : "Double_Jump");
            if (pick == Extra.Fart || pick == Extra.Cloud) Burst(__instance, ___rb2d, pick == Extra.Fart ? 188 : 16, pick == Extra.Fart ? 435 : 11);
            if (pick == Extra.Sail) SailBurst(__instance, ___rb2d);
            DevExtraJumps++;
            return false;
        }

        /// <summary>Dev: extra jumps taken.</summary>
        public static int DevExtraJumps;

        /// <summary>The Sail jump's launch (Pl:20991-21007): thirty water dust sprayed out to both sides under the feet.</summary>
        static void SailBurst(HeroController hero, Rigidbody2D rb)
        {
            var p = TerraParticles.Instance;
            if (p == null) return;
            var pos = TopLeft(hero);
            var v = new Vector2(rb.linearVelocity.x, -rb.linearVelocity.y) / PxT; // px/tick, y down
            for (int k = 0; k < 30; k++)
            {
                int i = p.NewDust(new Vector2(pos.x, pos.y + Height), Width, 12, 253, v.x * 0.3f, v.y * 0.3f, 100, 1.5f);
                if (i < 0) continue;
                ref var d = ref p[i];
                d.Velocity.x += (k % 2 == 0 ? 1f : -1f) * UnityEngine.Random.Range(30, 71) * 0.1f;
                d.Velocity.y += UnityEngine.Random.Range(-10, 31) * 0.1f;
                d.NoGravity = true;
                d.Scale += UnityEngine.Random.Range(-10, 41) * 0.01f;
                d.Velocity *= d.Scale * 0.7f;
            }
        }

        /// <summary>The cloud's and the fart's launch: ten dust under the feet and three gores spreading out.</summary>
        static void Burst(HeroController hero, Rigidbody2D rb, int dust, int gore)
        {
            var p = TerraParticles.Instance;
            if (p == null) return;
            var pos = TopLeft(hero);
            var v = new Vector2(rb.linearVelocity.x, -rb.linearVelocity.y) / PxT; // px/tick, y down
            for (int k = 0; k < 10; k++)
            {
                int i = p.NewDust(new Vector2(pos.x - 34f, pos.y + Height - 16f), 102, 32, dust, -v.x * 0.5f, v.y * 0.5f, 100, 1.5f);
                if (i < 0) continue;
                ref var d = ref p[i];
                d.Velocity = new Vector2(d.Velocity.x * 0.5f - v.x * 0.1f, d.Velocity.y * 0.5f - v.y * 0.3f);
            }
            var add = new Vector2(-v.x * 0.1f, -v.y * 0.05f);
            p.NewGore(new Vector2(pos.x + Width / 2f - 16f, pos.y + Height - 16f), -v, gore + UnityEngine.Random.Range(0, 3), 1f, 0.1f, add);
            p.NewGore(new Vector2(pos.x - 36f, pos.y + Height - 16f), -v, gore + UnityEngine.Random.Range(0, 3), 1f, 0.1f, add);
            p.NewGore(new Vector2(pos.x + Width + 4f, pos.y + Height - 16f), -v, gore + UnityEngine.Random.Range(0, 3), 1f, 0.1f, add);
        }

        /// <summary>An extra jump's rise: jump speed while the button is held, for its ticks, with its trail (Player.DoubleJumpVisuals).</summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(HeroController), "DoubleJump")]
        static bool DoubleJump(HeroController __instance, Rigidbody2D ___rb2d)
        {
            if (!Active) return true;
            var cs = __instance.cState;
            float t = Time.fixedDeltaTime * 60f;
            if (cloudTicks <= 0f || cs.onGround || !JumpHeld)
            {
                // The lift is over: CancelAllJumpVisualEffects.
                cs.doubleJumping = false;
                cloudTicks = 0f;
                activeJump = Extra.None;
                return false;
            }
            ___rb2d.linearVelocity = new Vector2(___rb2d.linearVelocity.x, JumpSpeedNow * PxT);
            cloudTicks -= t;

            var p = TerraParticles.Instance;
            if (p == null) return false;
            var v = new Vector2(___rb2d.linearVelocity.x, -___rb2d.linearVelocity.y) / PxT;
            trailTicks += t;
            while (trailTicks >= 1f)
            {
                trailTicks -= 1f;
                Trail(__instance, p, v);
            }
            return false;
        }

        /// <summary>One tick of the rising extra jump's trail, v the player's px/tick (y down).</summary>
        static void Trail(HeroController hero, TerraParticles p, Vector2 v)
        {
            var pos = TopLeft(hero);
            int i;
            switch (activeJump)
            {
                case Extra.Cloud:
                    // The cloud's trail shows only while the sandstorm is still to come (or isn't owned).
                    if (!canSandstorm && JumpSandstorm) break;
                    if ((i = p.NewDust(new Vector2(pos.x - 4f, pos.y + Height), Width + 8, 4, 16, -v.x * 0.5f, v.y * 0.5f, 100, 1.5f)) < 0) break;
                    p[i].Velocity = new Vector2(p[i].Velocity.x * 0.5f - v.x * 0.1f, p[i].Velocity.y * 0.5f - v.y * 0.3f);
                    break;
                case Extra.Sail:
                    // Two water dust a tick, left behind (DoubleJumpVisuals, Pl:22126-22149).
                    for (int n = 0; n < 2; n++)
                    {
                        if ((i = p.NewDust(new Vector2(pos.x, pos.y + Height - 6f), Width, 12, 253, v.x * 0.3f, v.y * 0.3f, 100, 1.5f)) < 0) continue;
                        p[i].Scale += UnityEngine.Random.Range(-5, 3) * 0.1f;
                        p[i].Velocity -= v / 5f;
                        p[i].NoGravity = true;
                    }
                    break;
                case Extra.Fart:
                    if ((i = p.NewDust(new Vector2(pos.x - 4f, pos.y + Height), Width + 8, 4, 188, -v.x * 0.5f, v.y * 0.5f, 100, 1.5f)) < 0) break;
                    p[i].Velocity = new Vector2(p[i].Velocity.x * 0.5f - v.x * 0.1f, p[i].Velocity.y * 0.5f - v.y * 0.3f) * 0.5f;
                    break;
                case Extra.Sandstorm:
                {
                    // Sand that thins out as the lift runs down, and a sand gore every third tick.
                    float k = (cloudTicks / 75f + 1f) / 2f;
                    for (int n = 0; n < 3; n++)
                    {
                        if ((i = p.NewDust(new Vector2(pos.x, pos.y + Height / 2), Width, 32, 124, v.x * 0.3f, v.y * 0.3f, 150, k)) < 0) continue;
                        p[i].Velocity *= 0.5f * k;
                        p[i].FadeIn = 1.5f * k;
                    }
                    if (miscCounter % 3 == 0)
                        p.NewGore(new Vector2(pos.x + Width / 2 - 18f, pos.y + Height / 2), v * 0.3f * k, UnityEngine.Random.Range(220, 223), k, exact: true, alpha: 100);
                    break;
                }
                case Extra.Blizzard:
                {
                    var corner = new Vector2(pos.x, pos.y + Height - 6f);
                    for (int n = 0; n < 2; n++)
                    {
                        if ((i = p.NewDust(corner, Width, 12, 76, v.x * 0.3f, v.y * 0.3f)) < 0) continue;
                        p[i].Velocity = p[i].Velocity * 0.1f + (n == 0 ? v * 0.03f : -v * 0.03f) - v * 0.1f;
                        p[i].NoGravity = true;
                    }
                    for (int n = 0; n < 6; n++)
                    {
                        if ((i = p.NewDust(corner, Width, 12, 76, v.x * 0.3f, v.y * 0.3f)) < 0) continue;
                        p[i].FadeIn = 1.5f;
                        p[i].Velocity = p[i].Velocity * 0.6f + (n < 3 ? v * 0.8f : -v * 0.8f);
                        p[i].NoGravity = true;
                    }
                    break;
                }
            }
        }

        // --- Wings (WingMovement, the default branch: Fledgling, Angel and Demon Wings). ---

        static float wingTime, wingDustTicks, wingSound;
        static bool flapSound;
        /// <summary>The wings' frame on their sheet (TerraWings.Animate).</summary>
        public static int WingFrame => TerraWings.Frame;
        public static bool Flying { get; private set; }
        /// <summary>Dev: the wings' time left (ticks).</summary>
        public static float WingTimeLeft => wingTime;
        /// <summary>The player stands on the wings in the air (the hoverboard, Lazure's platform, the Starboard: body and legs frame 0).</summary>
        public static bool WingStanding(HeroController hero) => HasWings && Wing.Standing && !hero.cState.onGround;

        /// <summary>
        /// Holding jump in the air once the jump's lift is over: thrust like the rocket boots'
        /// (0.1 a tick, 0.5 more while falling, 0.1 more below half jump speed, up to 1.5 times jump
        /// speed) with no gravity, for wingTime ticks; after that, holding jump while falling glides
        /// at a third of the gravity, at most a third of the fall speed (holding down lets go of the
        /// cap). They fill again on the ground or a wall once jump is let go, on a rope and a hook;
        /// rocket boots worn too turn their fuel into wing time (6 ticks a unit). They flap every 5
        /// ticks while flying or jumping, with Item_32 on each down stroke.
        /// </summary>
        // --- The Ram Rune's down-dash (hasDeadCellsDownDash; Pl:20843-20958, 20729-20762, 29002-29038). ---

        /// <summary>The Ram Rune worn.</summary>
        public static bool DownDash;
        /// <summary>isPerformingJump_DownDash.</summary>
        public static bool DownDashing { get; private set; }
        static float downDashTime;
        static bool downDashRequest;
        static readonly Dictionary<int, float> stompImmune = new Dictionary<int, float>();
        static readonly List<int> stompKeys = new List<int>();
        static readonly List<Collider2D> stompHits = new List<Collider2D>();
        /// <summary>Dev: dashes started, ground pounds, stomps; ask for a dash as if Down and jump were pressed.</summary>
        public static int DevDownDashes, DevPounds, DevStomps;
        public static bool DevDownDash;

        /// <summary>Each frame: Down held and a fresh jump press in the air ask for the dash.</summary>
        public static void PollDownDash(HeroController hero)
        {
            bool press = DevDownDash || (GameHooks.Input != null && GameHooks.Input.inputActions.jump.WasPressed);
            DevDownDash = false;
            if (!press || !DownDash || DownDashing || hero.cState.onGround || !DownHeld) return;
            downDashRequest = true;
        }

        /// <summary>
        /// The dash (each physics step): straight down at the fall cap (16 px a tick, held to 10);
        /// no wings, rockets or carpet; landing on an enemy below hurts it (40 × summon damage, kb
        /// 5, once in 10 ticks, 6 ticks' immunity); landing on the ground takes no fall damage and
        /// pounds: 30 damage, kb 10, to every enemy within 128 px (a dash of 10 ticks or more:
        /// 176 px, kb 20, a crit). It ends on the ground or a rope.
        /// </summary>
        static void DownDashStep(HeroController hero, Rigidbody2D rb, float t)
        {
            var cs = hero.cState;
            if (downDashRequest)
            {
                downDashRequest = false;
                if (DownDash && !DownDashing && !cs.onGround && !OnRope && !GrapplingHook.IsLatched && !cs.dead)
                {
                    DownDashing = true;
                    downDashTime = 0f;
                    stompImmune.Clear();
                    cs.jumping = false;
                    cs.doubleJumping = false;
                    cloudTicks = 0f;
                    activeJump = Extra.None;
                    rocketRelease = false;
                    pulseLeft = 0f;
                    rb.linearVelocity = new Vector2(rb.linearVelocity.x, -MaxFallSpeed * PxT);
                    DevDownDashes++;
                }
            }
            if (!DownDashing) return;
            if (!DownDash || OnRope || GrapplingHook.IsLatched || cs.dead)
            {
                DownDashing = false;
                return;
            }
            downDashTime += t;
            TerraMod.Instance.ResetFall();
            if (stompImmune.Count > 0)
            {
                stompKeys.Clear();
                stompKeys.AddRange(stompImmune.Keys);
                foreach (var k in stompKeys) if ((stompImmune[k] -= t) <= 0f) stompImmune.Remove(k);
            }
            if (cs.onGround)
            {
                GroundPound(hero);
                DownDashing = false;
                RefreshJumps();
                return;
            }
            if (rb.linearVelocity.y < 0f) Stomp(hero, rb);
        }

        /// <summary>Pl:20729-20762: the hitbox's bottom 2 px grown 12 px each side and 6 up and down (44×14 at the feet).</summary>
        static void Stomp(HeroController hero, Rigidbody2D rb)
        {
            var melee = TerraMod.Instance?.Melee;
            if (melee == null) return;
            var b = hero.GetComponent<BoxCollider2D>().bounds;
            var centre = new Vector2(b.center.x, b.min.y + 1f * Px);
            stompHits.Clear();
            var filter = new ContactFilter2D { useTriggers = true, useLayerMask = true, layerMask = Items.MeleeHits.HitMask };
            Physics2D.OverlapBox(centre, new Vector2(b.size.x + 24f * Px, 14f * Px), 0f, filter, stompHits);
            foreach (var col in stompHits)
            {
                var hm = col.GetComponentInParent<HealthManager>();
                if (hm == null || hm.isDead || stompImmune.ContainsKey(hm.GetInstanceID())) continue;
                float vx = rb.linearVelocity.x;
                float dir = vx < 0f ? -1f : vx > 0f ? 1f : hero.cState.facingRight ? 1f : -1f;
                melee.HitWith(col, (int)(40f * (1f + Items.Equipment.Current.MinionDamage)), 5f, 0f, dir, centre);
                stompImmune[hm.GetInstanceID()] = 10f;
                TerraHurt.Invulnerable(hero, 6);
                DevStomps++;
                break;
            }
        }

        static readonly List<HealthManager> poundNear = new List<HealthManager>();

        /// <summary>DoDeadCellsGroundPoundEffect and its particles (PO:2128-2190): the monk staff's impact, forty sparks thrown up.</summary>
        static void GroundPound(HeroController hero)
        {
            bool strong = 32f * downDashTime > 300f;
            float radius = strong ? 176f : 128f, kb = strong ? 20f : 10f;
            var b = hero.GetComponent<BoxCollider2D>().bounds;
            // The floor it lands on gives way where the Knight's dive would break it.
            World.DiveFloors.Pound(b);
            Vector2 c = b.center;
            var at = TerraParticles.ToPx(new Vector2(c.x, b.min.y)) + new Vector2(0f, -4f);
            TerraMod.Instance.PlaySound("Custom/dd2_monk_staff_ground_impact_" + UnityEngine.Random.Range(0, 3));
            var p = TerraParticles.Instance;
            if (p != null)
                for (int i = 0; i < 40; i++)
                {
                    var v = Vector2.Scale(UnityEngine.Random.insideUnitCircle, new Vector2(5f, 3f));
                    if (v.y > 0f) v.y = -v.y;
                    if (strong) v *= 1.5f;
                    int d = p.NewDust(at - new Vector2(4f, 4f), 0, 0, 306);
                    if (d < 0) continue;
                    p[d].Position = at + new Vector2(UnityEngine.Random.Range(-10f, 10f), 0f);
                    p[d].Velocity = v;
                    p[d].Color = Effects.FxParticles.HslToRgb(strong ? 0.01f : 0.55f, 1f, 0.5f);
                    p[d].Scale = 1.9f + 0.8f * UnityEngine.Random.value;
                    int w = p.Clone(d);
                    if (w < 0) continue;
                    p[w].Scale -= 0.6f;
                    p[w].Color = Color.white;
                }
            var melee = TerraMod.Instance?.Melee;
            if (melee != null)
                foreach (var hm in Items.AccessoryEffects.Enemies(c, radius + 64f, poundNear))
                {
                    var col = hm.GetComponent<Collider2D>() ?? hm.GetComponentInChildren<Collider2D>();
                    if (col == null) continue;
                    var e = (Vector2)col.bounds.center;
                    if (Vector2.Distance(e, c) / Px > radius) continue;
                    melee.HitWith(col, 30, kb, strong ? 100f : 0f, e.x > c.x ? 1f : -1f, c);
                }
            DevPounds++;
        }

        /// <summary>
        /// Player.WingMovement and the glide (sec-G1 G1.1): holding jump in the air after the
        /// jump's lift flies on the wings' thrust (down + jump with the hover wings flies at once,
        /// damped to a hover); the jetpack's UP climbs harder for twice the time; the Empress's
        /// and the Starboard's UP adds 0.4; held jump while falling glides. Then WingFrame, the
        /// flap's sound, the wings' dust and light.
        /// </summary>
        static void Wings(HeroController hero, Rigidbody2D rb, float t)
        {
            var cs = hero.cState;
            Flying = false;
            if (DownDashing) return;
            var w = Wing;
            if (!HasWings)
            {
                wingTime = 0f;
                TerraWings.Frame = 0;
                return;
            }
            bool grounded = cs.onGround;
            if ((grounded || cs.wallSliding) && !JumpHeld) wingTime = WingTimeMax;
            if (RocketBoots && !grounded && rocketFuel > 0)
            {
                int add = rocketFuel * 6;
                wingTime = Mathf.Min(wingTime + add, WingTimeMax + add);
                if (!EmpressBrooch) rocketFuel = 0;
            }
            bool lift = cs.jumping || cs.doubleJumping || cloudTicks > 0f;
            bool gliding = false;
            bool canAct = hero.acceptingInput && !cs.dead && !GrapplingHook.IsLatched && !OnRope && !cs.wallSliding;
            var v = rb.linearVelocity / PxT;
            float g = -Physics2D.gravity.y * rb.gravityScale * Time.fixedDeltaTime / PxT;
            bool hoverDown = DownHeld && JumpHeld && wingTime > 0f;
            var input = GameHooks.Input != null ? GameHooks.Input.inputActions : null;
            bool sideways = input != null && (input.left.IsPressed || input.right.IsPressed);
            // flag19 (Pl:26975-26996): in the air, the jump's lift over; the hover wings also from a lift or the ground with down held.
            bool fly = canAct && JumpHeld && wingTime > 0f && ((!grounded && !lift) || (w.HoverStart && DownHeld));
            if (fly)
            {
                Flying = true;
                if (lift)
                {
                    cs.jumping = false;
                    cs.doubleJumping = false;
                    cloudTicks = 0f;
                }
                float js = JumpSpeedNow;
                if (w.JetUp && UpHeld)
                {
                    // The jetpack's UP (Pl:22203-22238): 0.2 a tick (1.2 falling, 0.4 below jump speed), up to three times jump speed, twice the time.
                    float b = v.y + 0.2f * t;
                    if (b < 0f) b += 1f * t;
                    else if (b < js) b += 0.2f * t;
                    v.y = Mathf.Min(b, js * 3f);
                    wingTime -= 2f * t;
                }
                else
                {
                    if (w.UpBoost && UpHeld)
                    {
                        float b = v.y + 0.4f * t;
                        if (b < 0f) b += 1f * t;
                        else if (b < js) b += 0.2f * t;
                        v.y = Mathf.Min(b, js * 3f);
                    }
                    // Pl:22376-22406: num a tick, num2 more while falling, num5 more below num4 × jump speed, up to num3 × jump speed.
                    float num = (w.Slot == 30 || w.Slot == 31) && DownHeld ? 0.1f : w.Num;
                    v.y += num * t;
                    if (v.y < 0f) v.y += w.Num2 * t;
                    else if (v.y < js * w.Num4) v.y += w.Num5 * t;
                    v.y = Mathf.Min(v.y, js * w.Num3);
                    // Half the time hovering still with the half-cost wings (Pl:22408-22415).
                    wingTime -= (w.HalfCost && DownHeld && !sideways ? 0.5f : 1f) * t;
                }
                if (EmpressBrooch && wingTime != 0f) wingTime = WingTimeMax;
                // The hover's damping (Pl:27604-27625): nearly still holds the height.
                if (w.HoverDamp > 0f && hoverDown)
                {
                    v.y *= Mathf.Pow(w.HoverDamp, t);
                    if (w.HoverDamp2 > 0f) v.y *= Mathf.Pow(w.HoverDamp2, t);
                    if (v.y > -1f && v.y < 2f) v.y = 0f;
                }
                v.y += g;
                rb.linearVelocity = v * PxT;
                TerraMod.Instance.ResetFall();
            }
            else if (canAct && !grounded && JumpHeld && v.y < 0f)
            {
                gliding = true;
                // A third of the gravity, falling at most a third of the fall speed (the step's gravity paid in advance).
                float want = v.y - g / 3f;
                if (want < -MaxFallSpeed / 3f && !DownHeld) want = -MaxFallSpeed / 3f;
                v.y = want + g;
                rb.linearVelocity = v * PxT;
                TerraMod.Instance.ResetFall();
            }
            TerraWings.Animate(w, Flying || lift, !grounded, JumpHeld, gliding, wingTime, v.x, hoverDown, t);
            // Item_32 as the flap frame comes round (Pl:27035-27057); the jetpack's roar and the Starboard's hum.
            if (Flying && w.FlapFrame >= 0 && TerraWings.Frame == w.FlapFrame)
            {
                if (!flapSound) TerraMod.Instance.PlaySound("Item_32", 0.7f);
                flapSound = true;
            }
            else flapSound = false;
            if (wingSound > 0f) wingSound -= 2f * t;
            if (w.Slot == 4 && (Flying || lift || gliding) && wingSound <= 0f)
            {
                TerraMod.Instance.PlaySound("Item_13");
                wingSound = 60f;
            }
            if (w.Slot == 45 && !grounded && wingTime > 0f && wingSound <= 0f)
            {
                TerraMod.Instance.PlaySound("Item_24");
                wingSound = 30f;
            }
            // WingAirVisuals and the glide's dust, once a tick; the Mothron's and Lazure's light.
            wingDustTicks += t;
            var top = TopLeft(hero);
            int dir = cs.facingRight ? 1 : -1;
            var vd = new Vector2(v.x, -v.y);
            while (wingDustTicks >= 1f)
            {
                wingDustTicks -= 1f;
                if (Flying) TerraWings.FlightDust(w, top, dir, vd, UpHeld);
                else if (gliding) TerraWings.GlideDust(w, top, dir);
            }
            var fx = FxBatch.Instance;
            if (fx != null && w.Slot == 27) fx.Light(top + new Vector2(10f, 21f), new Color(0.4f, 0.36f, 0.08f), 3f);
            if (fx != null && w.Slot == 28 && !grounded) fx.Light(top + new Vector2(10f, 42f), new Color(0.3f, 0.1f, 0.4f), 3f);
        }

        /// <summary>The top-left of Terraria's 20x42 hitbox around the hero, in Terraria pixels (y down).</summary>
        public static Vector2 TopLeft(HeroController hero)
        {
            var b = hero.GetComponent<BoxCollider2D>().bounds;
            var feet = TerraParticles.ToPx(new Vector2(b.center.x, b.min.y));
            return new Vector2(feet.x - Width / 2f, feet.y - Height);
        }
    }
}
