using TerraKnight.Effects;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// The darts and the Sandgun's sand (sweep R4b3; Terraria's space, px y down, per update): the
    /// Seed (51) and the Poisoned, Crystal, Cursed and Ichor Darts (267, 477-479, AI_001: the
    /// crystal one off walls and on to the next enemy, the cursed one dropping cursed flames, 480,
    /// the ichor one splitting), and the Sandgun's balls of sand, ebonsand, pearlsand and crimsand
    /// (42, 65, 68, 354, AI_010) that settle as sand where they land.
    /// </summary>
    internal sealed partial class TerraProjectiles
    {
        public static int DevCursedFlames, DevIchorSplits, DevCrystalBounces, DevSandPlaced;

        static void RegisterDarts()
        {
            // P:1339: the seed, a plain AI_001 shot.
            specs[ProjKind.SeedProj] = new Spec
            {
                Ai = Ai.Arrow, Texture = "Projectile_51", W = 8, H = 8, Time = 3600, PivotBack = 4f,
                OnKill = (t, p) => KillDartChips(p),
            };
            // P:3531, 5520-5551: the darts, shown after three updates; the poisoned, cursed and ichor ones fall 0.075 after 20.
            Spec Dart(int id, int pierce, int extra, int time) => new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_" + id, W = 14, H = 14, Time = time, Penetrate = pierce, Extra = extra, AlphaStart = 1f,
                Update = (t, p) => t.StepDart(p), Draw = (t, fx, p, px) => DrawGenericLit(fx, p, px), OnKill = (t, p) => t.KillDart(p),
            };
            specs[ProjKind.PoisonDartProj] = Dart(267, 1, 0, 3600);
            specs[ProjKind.PoisonDartProj].OnHit = (t, p, hm) => EnemyDebuffs.Add(hm, EnemyDebuffs.Poisoned, Random.Range(0, 3) == 0 ? 3600 : 1800);
            specs[ProjKind.CrystalDartProj] = Dart(477, 5, 1, 3600);
            specs[ProjKind.CrystalDartProj].OnHit = (t, p, hm) => t.CrystalDartHit(p, hm);
            specs[ProjKind.CrystalDartProj].TileHit = (t, p, hx, hy, before) => t.CrystalDartTile(p, hx, hy, before);
            specs[ProjKind.CursedDartProj] = Dart(478, 3, 0, 300);
            specs[ProjKind.CursedDartProj].OnHit = (t, p, hm) => EnemyDebuffs.Add(hm, EnemyDebuffs.CursedInferno, 60 * Random.Range(3, 7));
            specs[ProjKind.IchorDartProj] = Dart(479, 1, 0, 3600);
            specs[ProjKind.IchorDartProj].OnHit = (t, p, hm) => EnemyDebuffs.Add(hm, EnemyDebuffs.Ichor, 60 * Random.Range(7, 15));
            // P:5551, 25540-25560: the cursed flame, unseen, an AI_014 roller of cursed fire, 3 s.
            specs[ProjKind.CursedDartFlame] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_480", W = 12, H = 12, Time = 180, Penetrate = 1, AlphaStart = 1f,
                Update = (t, p) => StepCursedDartFlame(p), Draw = (t, fx, p, px) => { },
                TileHit = (t, p, hx, hy, before) => RollerBounce(p, hx, hy, before, 0.5f), OnKill = (t, p) => { },
                OnHit = (t, p, hm) => EnemyDebuffs.Add(hm, EnemyDebuffs.CursedInferno, 60 * Random.Range(3, 7)),
            };
            // P:1250-1490, 4442: the Sandgun's sand, AI_010 at twice a tick; the plain sand stops at an enemy, the rest go through.
            foreach (var (kind, id, pierce) in new[] { (ProjKind.SandBall, 42, 1), (ProjKind.EbonsandBall, 65, -1), (ProjKind.PearlsandBall, 68, -1), (ProjKind.CrimsandBall, 354, -1) })
                specs[kind] = new Spec
                {
                    Ai = Ai.Custom, Texture = "Projectile_" + id, W = 10, H = 10, Time = 3600, Extra = 1, Penetrate = pierce,
                    Update = (t, p) => StepSandBall(p), Draw = (t, fx, p, px) => DrawGenericLit(fx, p, px),
                    TileHit = (t, p, hx, hy, before) => t.MudBallTile(p, hx, hy, before), OnKill = (t, p) => t.KillSandBall(p),
                };
        }

        /// <summary>P:77144-77150: the seed and the poisoned dart crack (the dig sound) into dirt (0).</summary>
        static void KillDartChips(Proj p)
        {
            Sound("Dig_" + Random.Range(0, 3));
            var tl = CenterPx(p) - new Vector2(p.Spec.W / 2f, p.Spec.H / 2f);
            for (int k = 0; k < 5; k++) BoxDust(tl, p.Spec.W, p.Spec.H, 0, 0f, 0f, 0, 0.7f);
        }

        /// <summary>
        /// AI_001 for 267, 477-479 (P:58270-58341, 58664-58672): shown after three updates; after 20
        /// falling 0.075 (not the crystal one); turned along its flight (the crystal one only its first
        /// five); the cursed one drops a cursed flame every 5-19 updates, burning shorter for it; the
        /// ichor one, still whole, goes through everything and after 5-29 updates splits into two or
        /// three (four, one time in four) at 90% damage, each with a streak of ichor (170).
        /// </summary>
        void StepDart(Proj p)
        {
            var v = TV(p);
            if (++p.LocalAi0 > 3f) p.Alpha = 0f;
            if (p.Kind != ProjKind.CrystalDartProj && ++p.Ai0 >= 20f)
            {
                p.Ai0 = 20f;
                v.y += 0.075f;
            }
            if (p.Kind == ProjKind.CrystalDartProj)
            {
                if (p.LocalAi1 < 5f)
                {
                    p.TRot = ToRot(v) + 1.57f;
                    p.LocalAi1++;
                }
            }
            else p.TRot = ToRot(v) + 1.57f;
            if (p.Kind == ProjKind.IchorDartProj)
            {
                if (p.Ai1 >= 0f) p.Penetrate = -1;
                else if (p.Penetrate < 0) p.Penetrate = 1;
                if (p.Ai1 >= 0f) p.Ai1++;
                if (p.Ai1 > Random.Range(5, 30))
                {
                    p.Ai1 = -1000f;
                    float speed = v.magnitude;
                    var n = v.sqrMagnitude > 1e-6f ? v.normalized : Vector2.right;
                    int count = Random.Range(2, 4) + (Random.Range(0, 4) == 0 ? 1 : 0);
                    var c = CenterPx(p);
                    for (int k = 0; k < count; k++)
                    {
                        var d = new Vector2(Random.Range(-100, 101), Random.Range(-100, 101));
                        d = (d.sqrMagnitude > 1e-6f ? d.normalized : Vector2.up) + n * 2f;
                        d = d.normalized * speed;
                        var q = Spawn(ProjKind.IchorDartProj, p.Pos, Flip(d), (int)(p.Damage * 0.9f), p.Knockback, p.Crit);
                        if (q != null)
                        {
                            q.Ai1 = -1000f;
                            q.Penetrate = 1;
                            q.Class = p.Class;
                            DevIchorSplits++;
                        }
                        for (int j = 0; j < 4; j++)
                        {
                            var z = d * (0.4f + j * 0.075f);
                            int i = BoxDust(c + z * 1.5f, 2, 2, 170, z.x, z.y);
                            if (i >= 0) D(i).NoGravity = true;
                        }
                        var back = d * 0.2f;
                        int b = BoxDust(c, 2, 2, 170, -back.x, -back.y);
                        if (b >= 0) D(b).NoGravity = true;
                    }
                }
            }
            if (p.Kind == ProjKind.CursedDartProj && ++p.Ai1 > Random.Range(5, 20))
            {
                if (p.TimeLeft > 40) p.TimeLeft -= 20;
                p.Ai1 = 0f;
                CursedDartFlame(p);
            }
            p.Vel = Flip(v);
        }

        void CursedDartFlame(Proj p)
        {
            var q = Spawn(ProjKind.CursedDartFlame, p.Pos, Vector2.zero, (int)(p.Damage * 0.8f), p.Knockback * 0.5f, p.Crit);
            if (q == null) return;
            q.Class = p.Class;
            DevCursedFlames++;
        }

        /// <summary>P:77144-77162: the poisoned dart chips; the cursed one leaves a last flame; the crystal and ichor ones only scatter the block's dust.</summary>
        void KillDart(Proj p)
        {
            if (p.Kind == ProjKind.PoisonDartProj) KillDartChips(p);
            else if (p.Kind == ProjKind.CursedDartProj) CursedDartFlame(p);
            else HitTileDust(p);
        }

        /// <summary>P:13584-13617, 18757-18810: the crystal dart's next enemy, 20-700 px off and in sight (any of the first nine), at its speed.</summary>
        void CrystalRetarget(Proj p, HealthManager not)
        {
            var c = CenterPx(p);
            var picks = new System.Collections.Generic.List<HealthManager>();
            foreach (var hm in EnemiesNear(c, 700f))
            {
                if (hm == not || !Chaseable(hm)) continue;
                var e = EnemyCenterPx(hm);
                float d = (c - e).magnitude;
                if (d > 20f && d < 700f && InSight(c, e))
                {
                    picks.Add(hm);
                    if (picks.Count >= 9) break;
                }
            }
            if (picks.Count == 0) return;
            var to = EnemyCenterPx(picks[Random.Range(0, picks.Count)]) - c;
            var v = TV(p);
            v = (to.sqrMagnitude > 1e-6f ? to.normalized : v.normalized) * v.magnitude;
            p.TRot = ToRot(v) + Mathf.PI / 2f;
            p.Vel = Flip(v);
            DevCrystalBounces++;
        }

        /// <summary>P:13584-13617: with pierce left it springs on to another enemy.</summary>
        void CrystalDartHit(Proj p, HealthManager hm)
        {
            if (p.Penetrate > 1) CrystalRetarget(p, hm);
        }

        /// <summary>P:18757-18810: off a block it loses a pierce (gone at none) and 15% of its damage, then looks for an enemy.</summary>
        void CrystalDartTile(Proj p, bool hx, bool hy, Vector2 before)
        {
            if (--p.Penetrate <= 0)
            {
                Kill(p);
                return;
            }
            if (hx) p.Vel.x = -before.x;
            if (hy) p.Vel.y = -before.y;
            p.TRot = ToRot(TV(p)) + Mathf.PI / 2f;
            p.Damage = (int)(p.Damage * 0.85f);
            CrystalRetarget(p, null);
        }

        /// <summary>AI_014 for 480 (P:25540-25560): an unseen roller, cursed fire (75) rising off it.</summary>
        static void StepCursedDartFlame(Proj p)
        {
            StepRoller(p);
            var tl = CenterPx(p) - new Vector2(6f, 6f);
            int i = BoxDust(tl, 12, 12, 75, 0f, 0f, 100);
            if (i >= 0)
            {
                D(i).Position += new Vector2(-2f, 2f);
                D(i).Scale += Random.Range(0, 50) * 0.01f;
                D(i).NoGravity = true;
                D(i).Velocity.y -= 2f;
            }
            if (Random.Range(0, 2) != 0) return;
            i = BoxDust(tl, 12, 12, 75, 0f, 0f, 100);
            if (i < 0) return;
            D(i).Position += new Vector2(-2f, 2f);
            D(i).Scale += 0.3f + Random.Range(0, 50) * 0.01f;
            D(i).NoGravity = true;
            D(i).Velocity *= 0.1f;
        }

        // ------------------------------------------------------------------ 42, 65, 68, 354: the Sandgun's sand (AI_010)

        /// <summary>AI_010 for the Sandgun's shots (P:58855-58995): its sand trickling off (32, 14, 51, 36); after 60 updates falling 0.2 (to 16).</summary>
        static void StepSandBall(Proj p)
        {
            var v = TV(p);
            int dust = p.Kind switch { ProjKind.EbonsandBall => 14, ProjKind.PearlsandBall => 51, ProjKind.CrimsandBall => 36, _ => 32 };
            if (Random.Range(0, 2) == 0)
            {
                int i = BoxDust(CenterPx(p) - new Vector2(5f, 5f), 10, 10, dust);
                if (i >= 0) D(i).Velocity.x *= 0.4f;
            }
            if (++p.Ai1 >= 60f)
            {
                p.Ai1 = 60f;
                v.y += 0.2f;
            }
            if (v.y > 16f) v.y = 16f;
            p.TRot = v.x * 0.1f;
            p.Vel = Flip(v);
        }

        /// <summary>
        /// Kill for AI_010 (P:79119-79250): where it lands it settles as its sand (and falls if
        /// nothing holds it); the corrupt, hallowed and crimson sands aren't blocks here, so those
        /// only scatter.
        /// </summary>
        void KillSandBall(Proj p)
        {
            int dust = p.Kind switch { ProjKind.EbonsandBall => 14, ProjKind.PearlsandBall => 51, ProjKind.CrimsandBall => 36, _ => 32 };
            var tl = CenterPx(p) - new Vector2(5f, 5f);
            if (p.Kind == ProjKind.SandBall)
            {
                var cell = CellGrid.WorldToCell(p.Pos);
                if (TerraMod.CanPlace(cell, CellKind.Sand))
                {
                    TerraWorld.Set(cell.x, cell.y, CellKind.Sand);
                    FallingSand.Instance?.Check(cell.x, cell.y);
                    Sound("Dig_" + Random.Range(0, 3));
                    DevSandPlaced++;
                    return;
                }
            }
            for (int k = 0; k < 5; k++) BoxDust(tl, 10, 10, dust);
        }
    }
}
