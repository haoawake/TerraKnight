using TerraKnight.Effects;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// The Howling Wraiths' weapons' shots (Items/Howl.cs; Terraria's space, px y down), all in the pack's bone white and
    /// pale blue:
    /// - the Wailing Deathbell (DeathBell): thrown free toward the cursor like a boomerang, tumbling, it bounces off three
    ///   things (enemies or ground) with a shock at each (Howl.ShockPx all round, SoulBurst's ring and the wail's ripples),
    ///   then flies back to the hand; touching the player once it has bounced is a catch, and the next throw is free at once;
    /// - the Ghost Pipe Organ's roars (OrganBurst the use's clock; ShriekWave the wave, a fan of pieces that each turn back
    ///   off walls, each enemy struck once a roar): the first two break on the enemies they meet, the third, after a short
    ///   charge, goes through everything till it fades;
    /// - the Howling Wraiths (WraithTriad): the three called up at the cursor (walls or not), shrieking out a spiral
    ///   of soul beams (WraithBeam) that home through walls;
    /// - the choir soul's shout (ChoirShout, a cone struck every Howl.ShoutCooldown; ShoutRipple its rings, drawn only).
    /// </summary>
    internal sealed partial class TerraProjectiles
    {
        public static int DevBellThrows, DevBellImpacts, DevBellShockHits, DevBellCatches, DevBellEarlyCatches, DevBellRecalls;
        public static int DevRoars, DevRoarShort, DevReflections, DevWraithCalls, DevWraithBeams, DevShouts;

        static void RegisterHowl()
        {
            specs[ProjKind.DeathBell] = new Spec
            {
                Ai = Ai.Custom, Texture = Howl.Tex("deathbell"), W = 16, H = 16, TileBox = new Vector2(14f, 14f), Time = 900, Penetrate = -1, Extra = 1,
                // Two out at once (the user's): the item holds back a third (Holds, MaxOut).
                LocalCooldown = 14, Held = true, MaxOut = 2, TrailLength = 6, Light = new Color(0.32f, 0.4f, 0.55f),
                Update = (t, p) => t.StepDeathbell(p), Draw = (t, fx, p, px) => DrawDeathbell(fx, p, px),
                OnHit = (t, p, hm) => t.BellStrikes(p, hm), TileHit = (t, p, hx, hy, before) => t.BellBounces(p, hx, hy, before), OnKill = (t, p) => { },
            };
            specs[ProjKind.OrganBurst] = new Spec
            {
                Ai = Ai.Custom, W = 4, H = 4, Time = Howl.OrganUse + 12, Penetrate = -1, TileCollide = false, OwnMove = true, Held = true, CanDamage = p => false,
                Update = (t, p) => t.StepOrganBurst(p), Draw = (t, fx, p, px) => DrawOrganBurst(fx, p, px), OnKill = (t, p) => { },
            };
            specs[ProjKind.ShriekWave] = new Spec
            {
                Ai = Ai.Custom, Texture = Howl.Tex("ripple"), W = 14, H = 14, TileBox = new Vector2(6f, 6f), Time = 60, Penetrate = 1, OncePerNpc = true,
                MaxTravel = Howl.WaveReachPx, Light = new Color(0.3f, 0.35f, 0.45f),
                Update = (t, p) => t.StepShriek(p), Draw = (t, fx, p, px) => DrawShriek(fx, p, px),
                OnHit = (t, p, hm) => t.ShriekStrikes(p, hm), TileHit = (t, p, hx, hy, before) => t.ShriekTurns(p, hx, hy, before), OnKill = (t, p) => ShriekFades(p),
            };
            specs[ProjKind.WraithTriad] = new Spec
            {
                Ai = Ai.Custom, Texture = Howl.Tex("triad"), W = 4, H = 4, Time = 48, Penetrate = -1, TileCollide = false, OwnMove = true, CanDamage = p => false,
                Light = new Color(0.4f, 0.48f, 0.62f),
                Update = (t, p) => t.StepTriad(p), Draw = (t, fx, p, px) => DrawTriad(fx, p, px), OnKill = (t, p) => { },
            };
            specs[ProjKind.WraithBeam] = new Spec
            {
                Ai = Ai.Custom, Texture = Howl.Tex("beam"), W = 10, H = 10, Time = 120, Penetrate = 1, TileCollide = false, OncePerNpc = true, TrailLength = 8,
                Light = new Color(0.32f, 0.4f, 0.55f),
                Update = (t, p) => t.StepSoulBeam(p), Draw = (t, fx, p, px) => DrawSoulBeam(fx, p, px),
                OnHit = (t, p, hm) => BeamStrikes(p), OnKill = (t, p) => BeamFades(p),
            };
            // A pulse of the choir soul's shout: the cone from its mouth (Point), along TRot, Ai0 px long, for a tick or two.
            specs[ProjKind.ChoirShout] = new Spec
            {
                Ai = Ai.Custom, W = 4, H = 4, Time = 2, Penetrate = -1, TileCollide = false, OwnMove = true, OncePerNpc = true,
                HitArea = p => ShoutArea(p), HitTest = (p, col) => InShout(p, col),
                Update = (t, p) => { }, Draw = (t, fx, p, px) => { }, OnHit = (t, p, hm) => ShoutStrikes(p, hm), OnKill = (t, p) => { },
            };
            specs[ProjKind.ShoutRipple] = new Spec
            {
                Ai = Ai.Custom, Texture = Howl.Tex("ripple"), W = 4, H = 4, Time = 30, Penetrate = -1, TileCollide = false, CanDamage = p => false,
                Update = (t, p) => { }, Draw = (t, fx, p, px) => DrawShoutRipple(fx, p, px), OnKill = (t, p) => { },
            };
        }

        /// <summary>Dev: the first live shot of a kind, where it is (world units); and how many of a roar's pieces live.</summary>
        public Vector2? DevPos(ProjKind kind)
        {
            foreach (var q in live) if (!q.Dead && q.Kind == kind) return q.Pos;
            return null;
        }

        public int DevRoarAlive(int roar)
        {
            int n = 0;
            foreach (var q in live) if (!q.Dead && q.Kind == ProjKind.ShriekWave && q.Ai0 == roar) n++;
            return n;
        }

        static Vector2 Rotate(Vector2 v, float degrees)
        {
            float a = degrees * Mathf.Deg2Rad, c = Mathf.Cos(a), s = Mathf.Sin(a);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }

        // ------------------------------------------------------------------ the Wailing Deathbell

        /// <summary>The bell thrown toward the cursor from the player (two updates a tick, Howl.BellSpeed px a tick).</summary>
        public void ThrowDeathbell(HeroController hero, Vector2 mouse, int damage, float knockback, float crit)
        {
            if (hero == null) return;
            var centre = TerraParticles.ToPx(hero.GetComponent<BoxCollider2D>().bounds.center);
            var dir = TerraParticles.ToPx(mouse) - centre;
            dir = dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector2.right;
            var p = Spawn(ProjKind.DeathBell, TerraParticles.ToWorld(centre + dir * 14f), Flip(dir) * (Howl.BellSpeed * 0.5f), damage, knockback, crit);
            if (p == null) return;
            p.Ai0 = 0f;
            p.Ai1 = 0f;
            DevBellThrows++;
        }

        /// <summary>
        /// The bell (Ai0 its bounces, Ai1 1 coming back, LocalAi0 its updates, LocalAi1 its updates coming back, SoundDelay
        /// a few updates between shocks): out, falling a little after its first moments; back once it has bounced three
        /// times, gone far or flown four seconds, or the moment the button is let go (or another item taken), straight home
        /// through everything and quickening; caught whenever it touches the player once it has bounced or is coming back
        /// (only the throw itself has a few updates' grace).
        /// </summary>
        void StepDeathbell(Proj p)
        {
            p.LocalAi0++;
            if (p.SoundDelay > 0) p.SoundDelay--;
            if (!Holder(out var owner))
            {
                Kill(p);
                return;
            }
            var me = CenterPx(p);
            float toOwner = Vector2.Distance(me, owner);
            if (p.LocalAi0 > 4f && (p.Ai0 >= 1f || p.Ai1 == 1f) && toOwner < 24f)
            {
                Caught(p);
                return;
            }
            var mod = TerraMod.Instance;
            if (p.Ai1 == 0f && p.LocalAi0 > 2f && (mod == null || !mod.UseHeld || mod.Inventory.Current.Type != ItemType.WailingDeathbell))
            {
                DevBellRecalls++;
                BellReturns(p);
            }
            if (p.Ai1 == 0f)
            {
                if (p.LocalAi0 > 14f) p.Vel.y -= 0.11f;
                if (p.Vel.magnitude > 9f) p.Vel = p.Vel.normalized * 9f;
                if (p.LocalAi0 > 480f || toOwner > 900f) BellReturns(p);
            }
            else
            {
                p.LocalAi1++;
                p.Collide = false;
                var to = owner - me;
                float speed = Mathf.Min(9f, 4f + p.LocalAi1 * 0.18f);
                p.Vel = Vector2.Lerp(p.Vel, Flip(to.normalized) * speed, 0.3f);
            }
            // Tumbling as a boomerang spins, the way it goes, quicker coming back.
            float spin = (p.Ai1 == 1f ? 0.55f : 0.4f) * (p.Vel.x >= 0f ? 1f : -1f);
            p.TRot = Mathf.Repeat(p.TRot + spin + Mathf.PI, Mathf.PI * 2f) - Mathf.PI;
            if (Random.value < 0.25f) SoulDust(me + Random.insideUnitCircle * 8f, -TV(p) * 0.1f, Random.Range(0.6f, 0.9f));
        }

        void BellReturns(Proj p)
        {
            if (p.Ai1 == 1f) return;
            p.Ai1 = 1f;
            p.LocalAi1 = 0f;
            p.Collide = false;
        }

        void Caught(Proj p)
        {
            DevBellCatches++;
            if (p.Ai1 == 0f) DevBellEarlyCatches++;
            Sound("tk_bell_catch", 0.6f);
            Kill(p);
        }

        /// <summary>Off the ground: back the way it came on the side it met (85% of it), and a shock.</summary>
        void BellBounces(Proj p, bool hitX, bool hitY, Vector2 before)
        {
            if (hitX) p.Vel.x = -before.x * 0.85f;
            if (hitY) p.Vel.y = -before.y * 0.85f;
            if (p.Vel.magnitude < 3f) p.Vel = (p.Vel.sqrMagnitude > 1e-4f ? p.Vel.normalized : Vector2.up) * 3.5f;
            BellImpact(p, null);
        }

        /// <summary>Off an enemy (the bell's own blow landed): sent away from it, and a shock round it; on the way back it only strikes.</summary>
        void BellStrikes(Proj p, HealthManager hm)
        {
            if (p.Ai1 == 1f || hm == null) return;
            var away = CenterPx(p) - TerraParticles.ToPx(Centre(hm));
            away = away.sqrMagnitude > 1e-4f ? away.normalized : -TV(p).normalized;
            away = (away + new Vector2(0f, -0.35f)).normalized;
            p.Vel = Flip(away) * Mathf.Max(p.Vel.magnitude * 0.85f, 4.5f);
            BellImpact(p, hm);
        }

        void BellImpact(Proj p, HealthManager struck)
        {
            if (p.SoundDelay > 0 || p.Ai1 == 1f) return;
            p.SoundDelay = 6;
            p.Ai0++;
            DevBellImpacts++;
            BellShock(p, struck);
            if (p.Ai0 >= Howl.BellBounces) BellReturns(p);
        }

        /// <summary>The shock: everyone else within Howl.ShockPx takes a share of the bell's blow; the ring, the wail's ripples, souls, the toll.</summary>
        void BellShock(Proj p, HealthManager struck)
        {
            var melee = TerraMod.Instance?.Melee;
            overlaps.Clear();
            var filter = new ContactFilter2D { useTriggers = true, useLayerMask = true, layerMask = MeleeHits.HitMask };
            Physics2D.OverlapBox(p.Pos, Vector2.one * (Howl.ShockPx * 2f * Px), 0f, filter, overlaps);
            var done = new System.Collections.Generic.HashSet<int>();
            if (struck != null) done.Add(struck.GetInstanceID());
            int damage = Mathf.Max(1, Mathf.RoundToInt(p.Damage * Howl.ShockShare));
            if (melee != null)
                foreach (var col in overlaps.ToArray())
                {
                    var hm = col.GetComponentInParent<HealthManager>();
                    if (hm == null || hm.isDead || !done.Add(hm.GetInstanceID())) continue;
                    if (Vector2.Distance(TerraParticles.ToPx(Centre(hm)), CenterPx(p)) > Howl.ShockPx + 24f) continue;
                    float facing = col.bounds.center.x >= p.Pos.x ? 1f : -1f;
                    if (melee.HitWith(col, damage, p.Knockback * 0.5f, p.Crit, facing, p.Pos, Equipment.Current.ArmorPen)) DevBellShockHits++;
                }
            var burst = Spawn(ProjKind.SoulBurst, p.Pos, Vector2.zero, 0, 0f, 0f);
            if (burst != null)
            {
                burst.Ai0 = Howl.ShockPx;
                burst.Ai1 = 1f;
            }
            var at = CenterPx(p);
            for (int k = 0; k < 6; k++)
            {
                var d = new Vector2(Mathf.Cos(k / 6f * Mathf.PI * 2f), Mathf.Sin(k / 6f * Mathf.PI * 2f));
                var r = Spawn(ProjKind.ShoutRipple, TerraParticles.ToWorld(at + d * 6f), Flip(d) * 4.5f, 0, 0f, 0f);
                if (r != null) { r.Point = at; r.TimeLeft = 12; r.Ai1 = 0.5f; }
            }
            for (int k = 0; k < 14; k++)
            {
                float a = k / 14f * Mathf.PI * 2f;
                SoulDust(at, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Random.Range(2.2f, 3.8f), Random.Range(0.9f, 1.3f), k % 3 == 0 ? 226 : 180);
            }
            Sound("tk_bell_toll_*", 0.7f);
            Player.TerraCamera.Punch(0.05f, 0.14f);
        }

        /// <summary>The bell: tumbling (TRot), its afterimages while it flies, a pale halo, its light.</summary>
        static void DrawDeathbell(FxBatch fx, Proj p, Vector2 px)
        {
            var bell = FxBatch.Frame(Howl.Tex("deathbell"), 0, 0, 40, 48, 20f, 24f);
            float rot = p.TRot, size = Howl.BellScale;
            int n2 = p.Trail.Count;
            for (int k = n2 - 1; k >= 1; k--)
            {
                float f = 1f - k / (float)(n2 + 1);
                fx.Draw(bell, TerraParticles.ToPx(p.Trail[k]), SoulWave.Glow(SoulWave.Pale, 0.22f * f), rot, size * (0.9f + 0.1f * f));
            }
            fx.Draw(bell, px, SoulWave.Glow(SoulWave.Cyan, 0.2f), rot, size * 1.1f);
            fx.Draw(bell, px, Color.white, rot, size);
            fx.Light(px, new Color(0.35f, 0.45f, 0.62f), 2.5f);
        }

        // ------------------------------------------------------------------ the Ghost Pipe Organ

        static int roarGroup;

        /// <summary>The organ's use: its first roar now (one round, paid), its clock for the other two (OrganBurst, held to the player).</summary>
        public void StartOrganBurst(HeroController hero, Vector2 mouse, int damage, float knockback, float crit)
        {
            if (hero == null) return;
            foreach (var q in live) if (!q.Dead && q.Kind == ProjKind.OrganBurst) return;
            var centre = TerraParticles.ToPx(hero.GetComponent<BoxCollider2D>().bounds.center);
            var p = Spawn(ProjKind.OrganBurst, TerraParticles.ToWorld(centre), Vector2.zero, damage, knockback, crit);
            if (p == null) return;
            p.Ai0 = 0f;
            Roar(hero, mouse, 0, damage, knockback, crit);
        }

        /// <summary>The use's clock (Ai0 its ticks, Ai1 1 once cut short): the second roar, the charge (the pipes glowing, souls drawn in, the organ shivering), the third.</summary>
        void StepOrganBurst(Proj p)
        {
            p.Ai0++;
            var mod = TerraMod.Instance;
            var hero = HeroController.SilentInstance;
            var def = ItemDb.Get(ItemType.GhostPipeOrgan);
            if (mod == null || hero == null || hero.cState.dead || mod.Inventory.Current.Type != def.Type || !Holder(out var centre))
            {
                Kill(p);
                return;
            }
            p.Pos = TerraParticles.ToWorld(centre);
            if (p.Ai1 == 1f)
            {
                if (p.Ai0 > Howl.ThirdRoar) Kill(p);
                return;
            }
            var aim = mod.DevAim ?? mod.AimPoint;
            if (p.Ai0 == Howl.SecondRoar || p.Ai0 == Howl.ThirdRoar)
            {
                int roar = p.Ai0 == Howl.SecondRoar ? 1 : 2;
                if (!PayRounds(mod, def, Howl.RoarRounds[roar], out int damage, out float kb))
                {
                    p.Ai1 = 1f;
                    DevRoarShort++;
                    mod.StopSound("tk_organ_charge");
                    Sound("tk_pulse_cancel", 0.6f);
                    UI.CombatText.Show(p.Pos, Lang.T("弹药不足"), new Color(0.75f, 0.8f, 0.9f));
                    return;
                }
                Roar(hero, aim, roar, damage, kb, p.Crit);
            }
            if (p.Ai0 > Howl.SecondRoar && p.Ai0 < Howl.ThirdRoar)
            {
                if (p.Ai0 == Howl.SecondRoar + 2) Sound("tk_organ_charge", 0.65f);
                Howl.Shiver = Mathf.Max(Howl.Shiver, 0.6f * Remap01(p.Ai0, Howl.SecondRoar, Howl.ThirdRoar));
                var dir = (TerraParticles.ToPx(aim) - centre).normalized;
                float facing = dir.x < 0f ? -1f : 1f;
                var mouth = Howl.OrganPoint(centre, dir, facing, Howl.Pipes[Random.Range(0, 3)]);
                var from = mouth + Random.insideUnitCircle.normalized * Random.Range(14f, 22f);
                SoulDust(from, (mouth - from) * 0.18f, Random.Range(0.6f, 0.9f));
            }
            if (p.Ai0 >= Howl.ThirdRoar + 8) Kill(p);
        }

        /// <summary>Rounds for a roar, all of them or none: the first's damage and knockback for it (TerraMod.PickAmmo's, the weapon's with the round's).</summary>
        static bool PayRounds(TerraMod mod, ItemDef def, int rounds, out int damage, out float knockback)
        {
            damage = 0;
            knockback = 0f;
            if (mod.Inventory.Infinite) rounds = 0;
            if (rounds > 0 && RoundsFor(mod, def) < rounds) return false;
            for (int k = 0; k < Mathf.Max(1, rounds); k++)
            {
                var kind = def.Shoot;
                float speed = 0f;
                if (!mod.PickAmmo(def, ref kind, ref speed, out int dmg, out float kb)) return k > 0;
                if (k == 0) { damage = dmg; knockback = kb; }
            }
            return true;
        }

        /// <summary>
        /// A roar from the organ's middle pipe (from the player's middle if a wall is in the way): a fan of the wave's pieces
        /// (7 over 40°, the third 9 over 32°, faster and longer), each its own way, one group; the kick, souls out of the pipes.
        /// </summary>
        void Roar(HeroController hero, Vector2 mouse, int roar, int damage, float knockback, float crit)
        {
            var centre = TerraParticles.ToPx(hero.GetComponent<BoxCollider2D>().bounds.center);
            var dir = TerraParticles.ToPx(mouse) - centre;
            dir = dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector2.right;
            float facing = dir.x < 0f ? -1f : 1f;
            var mouth = Howl.OrganPoint(centre, dir, facing, Howl.Pipes[1]);
            if (Physics2D.Linecast(TerraParticles.ToWorld(centre), TerraParticles.ToWorld(mouth), 1 << ChunkColliders.TerrainLayer)) mouth = centre;
            bool last = roar == 2;
            int n = last ? 9 : 7, life = Howl.WaveLife;
            float spread = last ? 16f : 20f, speed = last ? 10.5f : 9f;
            int dmg = Mathf.Max(1, Mathf.RoundToInt(damage * Howl.RoarDamage[roar]));
            int group = ++roarGroup;
            for (int k = 0; k < n; k++)
            {
                float a = Mathf.Lerp(-spread, spread, k / (float)(n - 1));
                var v = Rotate(dir, a) * speed;
                var q = Spawn(ProjKind.ShriekWave, TerraParticles.ToWorld(mouth + v.normalized * 6f), Flip(v), dmg, knockback * (last ? 1.6f : 1f), crit);
                if (q == null) continue;
                q.Ai0 = roar;
                q.Ai1 = group;
                q.LocalAi0 = 2f * spread / (n - 1);
                q.TimeLeft = life;
                q.Penetrate = last ? -1 : 1;
                q.Point = mouth;
                q.Class = DamageClass.Ranged;
            }
            DevRoars++;
            Howl.Kick(last ? 8f : 3f, last ? 11f : 4f);
            if (last) Player.TerraCamera.Punch(0.1f, 0.2f);
            Sound("tk_organ_roar_" + roar, last ? 0.85f : 0.7f);
            for (int i = 0; i < 3; i++)
            {
                var pipe = Howl.OrganPoint(centre, dir, facing, Howl.Pipes[i]);
                for (int k = 0; k < (last ? 5 : 2); k++) SoulDust(pipe, dir * Random.Range(1.5f, 4f) + Random.insideUnitCircle, Random.Range(0.7f, 1.1f));
            }
        }

        /// <summary>The use's clock drawn: the three pipe mouths glowing more and more through the charge.</summary>
        static void DrawOrganBurst(FxBatch fx, Proj p, Vector2 px)
        {
            if (p.Ai1 == 1f || p.Ai0 <= Howl.SecondRoar || p.Ai0 >= Howl.ThirdRoar || !Holder(out var centre)) return;
            var mod = TerraMod.Instance;
            if (mod == null) return;
            var dir = TerraParticles.ToPx(mod.DevAim ?? mod.AimPoint) - centre;
            dir = dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector2.right;
            float facing = dir.x < 0f ? -1f : 1f;
            float k = Remap01(p.Ai0, Howl.SecondRoar, Howl.ThirdRoar);
            var orb = FxBatch.Whole(SoulWave.Tex("orb"));
            foreach (var pipe in Howl.Pipes)
            {
                var at = Howl.OrganPoint(centre, dir, facing, pipe);
                fx.Draw(orb, at, SoulWave.Glow(SoulWave.White, 0.35f + 0.45f * k), 0f, 0.25f + 0.45f * k, overPlayer: true);
                fx.Light(at, new Color(0.45f, 0.55f, 0.7f) * k, 2f);
            }
        }

        /// <summary>A wave's piece: on at its speed till it leaves the room, has flown Howl.WaveReachPx (the spec's MaxTravel) or its time is up.</summary>
        void StepShriek(Proj p)
        {
            p.TRot = Mathf.Atan2(-p.Vel.y, p.Vel.x);
            var cell = CellGrid.WorldToCell(p.Pos);
            if (!TerraWorld.InWorld(cell.x, cell.y)) Kill(p);
        }

        /// <summary>Struck: the rest of its roar's pieces spare that enemy (one blow a roar); a burst of white where it struck.</summary>
        void ShriekStrikes(Proj p, HealthManager hm)
        {
            if (hm == null) return;
            int id = hm.GetInstanceID();
            foreach (var q in live)
                if (q.Kind == ProjKind.ShriekWave && q.Ai1 == p.Ai1) q.Immune[id] = int.MaxValue;
            var at = CenterPx(p);
            for (int k = 0; k < 4; k++) SoulDust(at, Random.insideUnitCircle * 2.5f, Random.Range(0.7f, 1f));
            Sound("tk_spirit_hit_*", 0.25f);
        }

        /// <summary>A wall: turned back off it (the side it met), up to four times, then gone.</summary>
        void ShriekTurns(Proj p, bool hitX, bool hitY, Vector2 before)
        {
            if (++p.Bounces > 4)
            {
                Kill(p);
                return;
            }
            if (hitX) p.Vel.x = -before.x;
            if (hitY) p.Vel.y = -before.y;
            DevReflections++;
        }

        static void ShriekFades(Proj p)
        {
            var at = CenterPx(p);
            for (int k = 0; k < 2; k++) SoulDust(at, TV(p) * 0.15f + Random.insideUnitCircle, Random.Range(0.5f, 0.8f));
        }

        /// <summary>
        /// A wave's piece: its stretch of the white ripple (Howl's three arcs), long enough to meet its neighbours at the
        /// distance it has gone (its share of the fan's angle, LocalAi0); a glow under it; the third roar's thicker; fading in
        /// its last part.
        /// </summary>
        static void DrawShriek(FxBatch fx, Proj p, Vector2 px)
        {
            var ripple = FxBatch.Frame(Howl.Tex("ripple"), 0, 0, 14, 36, 7f, 18f);
            bool last = p.Ai0 == 2f;
            float life = Mathf.Max(1f, p.TimeLeft + p.Ticks);
            float o = Remap01(p.Ticks, 0f, 3f) * Remap01(p.TimeLeft, 0f, life * 0.35f) * Remap01(Howl.WaveReachPx - p.Travel, 0f, 200f);
            float dist = Mathf.Max(8f, Vector2.Distance(px, p.Point));
            float along = dist * p.LocalAi0 * Mathf.Deg2Rad * 1.2f;
            var scale = new Vector2(last ? 1.35f : 1f, Mathf.Clamp(along / 36f, 0.3f, last ? 1.8f : 1.3f));
            fx.Draw(ripple, px, SoulWave.Glow(SoulWave.Pale, 0.35f * o), p.TRot, scale * 1.15f);
            fx.Draw(ripple, px, Color.white * o, p.TRot, scale);
        }

        // ------------------------------------------------------------------ the Howling Wraiths

        /// <summary>The three called up at the cursor, however far and in a wall or not (the user's: their beams go through walls anyway); turned to the aim.</summary>
        public void CallWraiths(HeroController hero, Vector2 mouse, int damage, float knockback, float crit)
        {
            if (hero == null) return;
            var centre = TerraParticles.ToPx(hero.GetComponent<BoxCollider2D>().bounds.center);
            var to = TerraParticles.ToPx(mouse) - centre;
            var dir = to.sqrMagnitude > 1e-6f ? to.normalized : Vector2.right;
            var at = centre + dir * to.magnitude;
            var p = Spawn(ProjKind.WraithTriad, TerraParticles.ToWorld(at), Vector2.zero, damage, knockback, crit);
            if (p == null) return;
            p.TRot = Mathf.Atan2(dir.y, dir.x) + Mathf.PI / 2f;
            p.Class = DamageClass.Magic;
            DevWraithCalls++;
            for (int k = 0; k < 12; k++)
            {
                float a = k / 12f * Mathf.PI * 2f;
                SoulDust(at, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 2.4f, 1.1f);
            }
        }

        /// <summary>
        /// The three (Ai0 their ticks): appearing (frames 0-3, 5 ticks each), shrieking (4-5) while a spiral of
        /// Howl.WraithBeams soul beams goes out all round, one a tick from the aim's way on; going back (6-7).
        /// </summary>
        void StepTriad(Proj p)
        {
            p.Ai0++;
            int t = (int)p.Ai0;
            p.Frame = t < 20 ? t / 5 : t < 32 ? 4 + (t / 5) % 2 : Mathf.Min(7, 6 + (t - 32) / 6);
            if (t == 20) Sound("tk_wraiths_shriek", 0.75f);
            if (t >= 20 && t < 20 + Howl.WraithBeams)
            {
                int k = t - 20;
                float a = p.TRot - Mathf.PI / 2f + k / (float)Howl.WraithBeams * Mathf.PI * 2f;
                var v = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 7f;
                var at = CenterPx(p) + v.normalized * 18f;
                var b = Spawn(ProjKind.WraithBeam, TerraParticles.ToWorld(at), Flip(v), Mathf.Max(1, Mathf.RoundToInt(p.Damage * Howl.BeamShare)), p.Knockback * 0.5f, p.Crit);
                if (b != null)
                {
                    b.Class = DamageClass.Magic;
                    DevWraithBeams++;
                }
                if (k % 3 == 0) Sound("tk_wraith_beam", 0.4f);
            }
        }

        /// <summary>The three: the pack's frame (64×64), turned to the aim, a glow under it growing to the shriek, its light.</summary>
        static void DrawTriad(FxBatch fx, Proj p, Vector2 px)
        {
            var frame = FxBatch.Frame(Howl.Tex("triad"), 0, Mathf.Clamp(p.Frame, 0, 7) * 64, 64, 64, 32f, 32f);
            float shriek = p.Frame >= 4 && p.Frame <= 5 ? 1f : p.Frame < 4 ? p.Frame / 4f : 0.4f;
            fx.Draw(frame, px, SoulWave.Glow(SoulWave.Pale, 0.18f + 0.25f * shriek), p.TRot, 1.12f, overPlayer: true);
            fx.Draw(frame, px, Color.white, p.TRot, 1f, overPlayer: true);
            fx.Light(px, new Color(0.45f, 0.55f, 0.72f) * (0.5f + 0.5f * shriek), 3.5f);
        }

        /// <summary>A soul beam (Ai0 its updates): out its own way for 10, then after the nearest enemy within 40 tiles, through walls.</summary>
        void StepSoulBeam(Proj p)
        {
            p.Ai0++;
            if (p.Ai0 > 10f)
            {
                var target = p.Target;
                if (target == null || target.isDead || !target.gameObject.activeInHierarchy || p.Immune.ContainsKey(target.GetInstanceID())) target = p.Target = NearestThrough(p, 640f);
                if (target != null)
                {
                    var to = TerraParticles.ToPx(Centre(target)) - CenterPx(p);
                    p.Vel = (p.Vel * 8f + Flip(to.normalized) * 10f) / 9f;
                }
            }
            p.TRot = Mathf.Atan2(-p.Vel.y, p.Vel.x);
            if (Random.value < 0.4f) SoulDust(CenterPx(p), -TV(p) * 0.1f, Random.Range(0.5f, 0.8f));
        }

        /// <summary>The nearest enemy that can be struck within a reach (px), walls or not.</summary>
        static HealthManager NearestThrough(Proj p, float withinPx)
        {
            HealthManager best = null;
            float bestD = withinPx * Px;
            foreach (var col in Physics2D.OverlapCircleAll(p.Pos, withinPx * Px, 1 << 11))
            {
                var hm = col.GetComponentInParent<HealthManager>();
                if (hm == null || hm.isDead || !hm.gameObject.activeInHierarchy || (EnemyStats.Untouchable(hm) && !BossWake.Asleep(hm)) || ArenaGuard.Outside(hm) || ArenaGuard.OffMap(hm) || p.Immune.ContainsKey(hm.GetInstanceID())) continue;
                float d = Vector2.Distance(Centre(hm), p.Pos);
                if (d >= bestD) continue;
                bestD = d;
                best = hm;
            }
            return best;
        }

        static void BeamStrikes(Proj p)
        {
            var at = CenterPx(p);
            for (int k = 0; k < 5; k++) SoulDust(at, Random.insideUnitCircle * 3f, Random.Range(0.8f, 1.2f));
            Sound("tk_soul_hit_*", 0.25f);
        }

        static void BeamFades(Proj p)
        {
            var at = CenterPx(p);
            for (int k = 0; k < 3; k++) SoulDust(at, Random.insideUnitCircle * 1.5f, Random.Range(0.6f, 0.9f));
        }

        /// <summary>A soul beam: its streak along its flight, its afterimages, a pale glow.</summary>
        static void DrawSoulBeam(FxBatch fx, Proj p, Vector2 px)
        {
            var streak = FxBatch.Frame(Howl.Tex("beam"), 0, 0, 24, 7, 18f, 3.5f);
            float o = Remap01(p.Ticks, 0f, 3f) * Remap01(p.TimeLeft, 0f, 12f);
            int n = p.Trail.Count;
            for (int k = n - 1; k >= 1; k--)
            {
                float f = 1f - k / (float)(n + 1);
                fx.Draw(streak, TerraParticles.ToPx(p.Trail[k]), SoulWave.Glow(SoulWave.Pale, 0.3f * f * o), p.TRot, 0.7f + 0.3f * f);
            }
            fx.Draw(streak, px, SoulWave.Glow(SoulWave.Cyan, 0.35f * o), p.TRot, 1.25f);
            fx.Draw(streak, px, Color.white * o, p.TRot, 1f);
            fx.Light(px, new Color(0.35f, 0.45f, 0.62f) * o, 2f);
        }

        // ------------------------------------------------------------------ the choir soul's shout

        /// <summary>
        /// One pulse of a choir soul's shout (Items/MinionsHowl.cs): the cone from its mouth (px) along a rotation (Terraria
        /// radians), Howl.ShoutReachPx long, Howl.ShoutHalfAngle each side, through every enemy in it and through walls (the
        /// user's: the far end struck nothing where a wall stood between); its ring, which ends where the cone does.
        /// </summary>
        public void ChoirPulse(Vector2 mouthPx, float rot, int damage, float knockback, bool strike)
        {
            var dir = new Vector2(Mathf.Cos(rot), Mathf.Sin(rot));
            if (strike)
            {
                var p = SpawnMinionShot(ProjKind.ChoirShout, mouthPx, Vector2.zero, damage, knockback);
                if (p != null)
                {
                    p.Point = mouthPx;
                    p.TRot = rot;
                    p.Ai0 = Howl.ShoutReachPx;
                    DevShouts++;
                }
            }
            var r = Spawn(ProjKind.ShoutRipple, TerraParticles.ToWorld(mouthPx + dir * 6f), Flip(dir) * 10f, 0, 0f, 0f);
            if (r != null)
            {
                r.Point = mouthPx;
                r.TimeLeft = Mathf.RoundToInt(Howl.ShoutReachPx / 10f);
                r.Ai1 = 1f;
            }
        }

        /// <summary>A box round the cone (what the hit test then narrows down).</summary>
        static (Vector2, Vector2) ShoutArea(Proj p)
        {
            var dir = new Vector2(Mathf.Cos(p.TRot), Mathf.Sin(p.TRot));
            var mid = p.Point + dir * (p.Ai0 * 0.5f);
            return (TerraParticles.ToWorld(mid), Vector2.one * (p.Ai0 + 48f) * Px);
        }

        /// <summary>In the cone: its middle within the reach and the angle (an allowance for its size); walls or not, as the sound goes through them.</summary>
        static bool InShout(Proj p, Collider2D col)
        {
            var c = TerraParticles.ToPx(col.bounds.center);
            var v = c - p.Point;
            float d = v.magnitude, size = Mathf.Max(col.bounds.extents.x, col.bounds.extents.y) / Px;
            if (d > p.Ai0 + size) return false;
            if (d > 1f)
            {
                var dir = new Vector2(Mathf.Cos(p.TRot), Mathf.Sin(p.TRot));
                float angle = Vector2.Angle(dir, v);
                if (angle > Howl.ShoutHalfAngle + Mathf.Atan2(size, d) * Mathf.Rad2Deg) return false;
            }
            return true;
        }

        static void ShoutStrikes(Proj p, HealthManager hm)
        {
            if (hm == null) return;
            var at = TerraParticles.ToPx(Centre(hm));
            for (int k = 0; k < 2; k++) SoulDust(at + Random.insideUnitCircle * 6f, Random.insideUnitCircle * 2f, Random.Range(0.6f, 0.9f));
        }

        /// <summary>
        /// A ring of sound: the white ripple at the distance it has gone from where it started (Point), as wide as the cone
        /// is there (Ai1 1: the choir's, its 30°) or a short ring piece (a bell's shock), fading.
        /// </summary>
        static void DrawShoutRipple(FxBatch fx, Proj p, Vector2 px)
        {
            var ripple = FxBatch.Frame(Howl.Tex("ripple"), 0, 0, 14, 36, 7f, 18f);
            float life = Mathf.Max(1f, p.TimeLeft + p.Ticks);
            float o = Remap01(p.Ticks, 0f, 2f) * Remap01(p.TimeLeft, 0f, life * 0.5f);
            float dist = Mathf.Max(6f, Vector2.Distance(px, p.Point));
            float width = p.Ai1 == 1f ? 2f * dist * Mathf.Tan(Howl.ShoutHalfAngle * Mathf.Deg2Rad) : dist * 0.9f;
            var scale = new Vector2(0.8f, Mathf.Clamp(width / 36f, 0.25f, 3f));
            float rot = Mathf.Atan2(-p.Vel.y, p.Vel.x);
            fx.Draw(ripple, px, SoulWave.Glow(SoulWave.Pale, 0.25f * o), rot, scale * 1.15f);
            fx.Draw(ripple, px, Color.white * (0.85f * o), rot, scale);
        }
    }
}
