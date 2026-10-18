using TerraKnight.Effects;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// The chained weapons (sweep S1.G; Terraria's space, px y down): the Golem Fist (262), the KO
    /// Cannon's glove (271), the Chain Knife (273) and the Chain Guillotines (481), AI_013 heads
    /// shot out on a chain and pulled back off whatever they hit or once far enough; the Anchor
    /// (383), an AI_003 throw that drops and drags its chain; and the Solar Eruption (611, AI_075):
    /// a fiery flail lashed out and back along a flattened loop in half a second, bursting where
    /// it strikes (612, the AI_117 solar blast) and leaving its enemies Daybroken.
    /// </summary>
    internal sealed partial class TerraProjectiles
    {
        static void RegisterChains()
        {
            // P:3465-3475, 3569-3579, 3590-3599, 5563-5572: shown after 5 updates, through everything.
            Spec Head(string tex, int w, float scale = 1f, int extra = 0) => new Spec
            {
                Ai = Ai.Custom, Texture = tex, W = w, H = w, Scale = scale, Extra = extra, Time = 3600, Penetrate = -1, AlphaStart = 1f,
                Update = (t, p) => t.StepChainHead(p), Draw = (t, fx, p, px) => DrawChainHead(fx, p, px), OnHit = (t, p, hm) => p.Ai0 = 1f,
                TileHit = (t, p, hx, hy, before) => ChainHeadTile(p), OnKill = (t, p) => { },
            };
            specs[ProjKind.GolemFistProj] = Head("Projectile_262", 30, 1f, 1);
            specs[ProjKind.BoxingGlove] = Head("Projectile_271", 20, 1.2f);
            specs[ProjKind.ChainKnifeProj] = Head("Projectile_273", 26);
            specs[ProjKind.ChainGuillotine] = Head("Projectile_481", 22);
            // P:4657-4665: the Anchor, 34×34, through everything; it sinks and swings its chain.
            specs[ProjKind.AnchorProj] = new Spec
            {
                Ai = Ai.Boomerang, Texture = "Projectile_383", W = 34, H = 34, Time = 3600, Penetrate = -1, SpinRate = 0f, ReturnSpeed = 16f, ReturnAccel = 4f, ReturnVector = true,
                TileHit = (t, p, hx, hy, before) => t.AnchorTile(p), Draw = (t, fx, p, px) => DrawAnchor(fx, p, px),
            };
            // P:6569-6585, 14171-14175, 16094-16106: the flail, unseen at its own place, each enemy every 6 ticks along a line to its end and 48 px more.
            specs[ProjKind.SolarEruptionProj] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_611", W = 16, H = 16, Time = 2, Penetrate = -1, TileCollide = false, OwnMove = true, Held = true, AlphaStart = 1f,
                LocalCooldown = 6, Debuffs = new[] { (EnemyDebuffs.Daybroken, 300, 1) },
                Beam = p =>
                {
                    var v = TV(p);
                    var n = v.sqrMagnitude > 1e-6f ? v.normalized : Vector2.zero;
                    return (CenterPx(p), CenterPx(p) + v + n * 48f, 16f * p.Scale);
                },
                Update = (t, p) => t.StepSolarEruption(p), Draw = (t, fx, p, px) => DrawSolarEruption(fx, p, px), OnHit = (t, p, hm) => t.SolarEruptionHit(p, hm),
                OnKill = (t, p) => { },
            };
            // P:6586-6598: the blast where it strikes: as Daybreak's (953) but each enemy every 6 ticks.
            specs[ProjKind.SolarEruptionBlast] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_612", W = 8, H = 8, Time = 60, Penetrate = -1, TileCollide = false, OwnMove = true, AlphaStart = 1f, Frames = 5,
                LocalCooldown = 6, PushFromPlayer = true, Debuffs = new[] { (EnemyDebuffs.Daybroken, 300, 1) },
                CanDamage = p => p.Ai0 >= 1f, HitArea = p => (p.Pos, p.HitSize * Px),
                Update = (t, p) => StepSolarBlast(p), Draw = (t, fx, p, px) => DrawSolarBlast(fx, p, px), OnKill = (t, p) => { },
            };
        }

        static ItemType ChainItem(ProjKind k) => k switch
        {
            ProjKind.GolemFistProj => ItemType.GolemFist,
            ProjKind.BoxingGlove => ItemType.KOCannon,
            ProjKind.ChainKnifeProj => ItemType.ChainKnife,
            _ => ItemType.ChainGuillotines,
        };

        // ------------------------------------------------------------------ 262, 271, 273, 481 (AI_013)

        /// <summary>
        /// AI_013 (P:24928-25080): gone 2000 px from the player. Out: turned along its flight,
        /// shown after 5 updates, the player turned to it; it comes back from 700 px (the Golem
        /// Fist 500, the glove 200, the Chain Knife 150, the Guillotines 525) or off whatever it
        /// meets. Back: straight at the player at 20 px an update (the Fist 30), through walls,
        /// gone within 50 px; the Fist, called back from more than 150 px off, cracks the ground
        /// round it (200 px across) with a boom. The Guillotines go twice a tick out, three times
        /// back; the others hold the item out at the head while they're out.
        /// </summary>
        void StepChainHead(Proj p)
        {
            if (!Holder(out var centre))
            {
                Kill(p);
                return;
            }
            var c = CenterPx(p);
            var toPlayer = centre - c;
            float dist = toPlayer.magnitude;
            if (dist > 2000f)
            {
                Kill(p);
                return;
            }
            var mod = TerraMod.Instance;
            bool guillotine = p.Kind == ProjKind.ChainGuillotine;
            if (!guillotine && mod != null) mod.KeepUsing(ItemDb.Get(ChainItem(p.Kind)), Flip(-toPlayer));
            if (guillotine) p.Extra = p.Ai0 == 0f ? 1 : 2;
            var v = TV(p);
            if (p.Ai0 == 0f)
            {
                float reach = p.Kind switch { ProjKind.GolemFistProj => 500f, ProjKind.BoxingGlove => 200f, ProjKind.ChainKnifeProj => 150f, _ => 525f };
                if (dist > 700f || dist > reach) p.Ai0 = 1f;
                p.TRot = ToRot(v) + 1.57f;
                if (++p.Ai1 > 5f) p.Alpha = 0f;
                if (p.Ai1 > 8f) p.Ai1 = 8f;
                if (p.Kind == ProjKind.GolemFistProj || p.Kind == ProjKind.BoxingGlove) p.Dir = v.x < 0f ? -1f : 1f;
                // Pulled back by its reach this update: no crack.
                if (p.Kind == ProjKind.GolemFistProj && p.Ai0 != 0f) p.LocalAi0 = -1f;
                return;
            }
            if (p.Kind == ProjKind.GolemFistProj && p.LocalAi0 == 0f)
            {
                if (dist < 150f) p.LocalAi0 = -1f;
                else
                {
                    p.LocalAi0 = 200f;
                    CrackGround(c, 100f);
                    Sound("Item_14");
                    DevFistCracks++;
                }
            }
            p.Collide = false;
            p.TRot = Mathf.Atan2(toPlayer.y, toPlayer.x) - 1.57f;
            if (dist < 50f)
            {
                Kill(p);
                return;
            }
            float speed = p.Kind == ProjKind.GolemFistProj ? 30f : 20f;
            v = toPlayer / dist * speed;
            p.Vel = Flip(v);
            if (p.Kind == ProjKind.GolemFistProj || p.Kind == ProjKind.BoxingGlove) p.Dir = v.x < 0f ? 1f : -1f;
        }

        /// <summary>P:19627-19690: off a block it is pulled back (Collision.HitTiles, the dig sound).</summary>
        static void ChainHeadTile(Proj p)
        {
            var c = CenterPx(p);
            int w = (int)(p.Spec.W * p.Spec.Scale);
            for (int k = 0; k < 5; k++) BoxDust(c - new Vector2(w / 2f, w / 2f), w, w, 1);
            Sound("Dig_" + Random.Range(0, 3));
            p.Ai0 = 1f;
        }

        /// <summary>Collision.HitTilesInACircle: the blocks within a radius (px) throw their dust, as a pick's miss does.</summary>
        static void CrackGround(Vector2 at, float radius)
        {
            var pp = TerraParticles.Instance;
            if (pp == null) return;
            int i0 = Mathf.FloorToInt((at.x - radius) / 16f), i1 = Mathf.FloorToInt((at.x + radius) / 16f);
            int j0 = Mathf.FloorToInt((at.y - radius) / 16f), j1 = Mathf.FloorToInt((at.y + radius) / 16f);
            for (int i = i0; i <= i1; i++)
                for (int j = j0; j <= j1; j++)
                {
                    if (Vector2.Distance(at, new Vector2(i * 16f + 8f, j * 16f + 8f)) > radius || !TerraWorld.IsSolid(i, -j - 1)) continue;
                    int type = TerraMod.TileDustType(TerraWorld.Get(i, -j - 1));
                    for (int k = 0; k < 3; k++) pp.NewDust(new Vector2(i * 16f, j * 16f), 16, 16, type);
                }
        }

        /// <summary>
        /// Main.DrawProj (M:29730-29990): the chain from 4 px behind the head (ahead of it on the way
        /// back) to the player, a link every 12 px stopping 25 px short (Chain22 the Fist, Chain18 the
        /// glove, Chain23 the knife); the Guillotines' Chain40 at 85% every 85% of a link, stopping a
        /// link and a half short and slackening as the player moves; then the head, lit, as it shows.
        /// </summary>
        static void DrawChainHead(FxBatch fx, Proj p, Vector2 px)
        {
            if (Holder(out var player))
            {
                var v = TV(p);
                var n = v.sqrMagnitude > 1e-6f ? v.normalized * 4f : Vector2.zero;
                var at = p.Ai0 == 0f ? px - n : px + n;
                string tex = p.Kind switch { ProjKind.GolemFistProj => "Chain22", ProjKind.BoxingGlove => "Chain18", ProjKind.ChainKnifeProj => "Chain23", _ => "Chain40" };
                var link = FxBatch.Whole(tex);
                if (link != null)
                {
                    var d = player - at;
                    float rot = Mathf.Atan2(d.y, d.x) - 1.57f;
                    var lit = new Color(L, L, L, 1f);
                    if (p.Kind != ProjKind.ChainGuillotine)
                    {
                        for (int g = 0; g < 400 && d.magnitude >= 25f; g++)
                        {
                            at += d.normalized * 12f;
                            d = player - at;
                            fx.Draw(link, at, lit, rot, 1f);
                        }
                    }
                    else
                    {
                        float h = link.rect.height;
                        var hero = HeroController.SilentInstance;
                        var pv = hero != null ? Flip(hero.GetComponent<Rigidbody2D>().linearVelocity / Px / 60f) : Vector2.zero;
                        for (int g = 0; g < 400; g++)
                        {
                            float len = Vector2.Distance(at, player);
                            if (len < h * 1.5f || (d.x == 0f && d.y == 0f)) break;
                            float total = len;
                            d *= h * 0.85f / len;
                            at += d;
                            d = player - at;
                            if (total > h * 2f)
                                for (int axis = 0; axis < 2; axis++)
                                {
                                    float k = Mathf.Min(10f, Mathf.Abs(axis == 0 ? pv.x : pv.y)) / 10f * 0.75f * Mathf.Min(1f, total / 80f);
                                    if (axis == 0 && ((pv.x < 0f && px.x < player.x) || (pv.x > 0f && px.x > player.x))) d.y *= 1f - k;
                                    if (axis == 1 && ((pv.y < 0f && px.y < player.y) || (pv.y > 0f && px.y > player.y))) d.x *= 1f - k;
                                }
                            fx.Draw(link, at, lit, rot, 0.85f);
                        }
                    }
                }
            }
            DrawGeneric(fx, p, px, new Color(L, L, L, 1f), p.Dir < 0f && (p.Kind == ProjKind.GolemFistProj || p.Kind == ProjKind.BoxingGlove));
        }

        // ------------------------------------------------------------------ 383: the Anchor (AI_003)

        /// <summary>
        /// AI_003 for 383 (P:39818-39836): after 10 updates out it sinks (0.5 an update, 0.35 more
        /// while still rising) and slows 5% a tick, and comes back from 800 px; pointing along its
        /// flight, and away from the player on the way back (P:40288-40300).
        /// </summary>
        void StepAnchor(Proj p)
        {
            var v = TV(p);
            if (p.Ai1 >= 10f)
            {
                v.y += 0.5f;
                if (v.y < 0f) v.y += 0.35f;
                v.x *= 0.95f;
                if (v.y > 16f) v.y = 16f;
                p.Vel = Flip(v);
                if (Holder(out var centre) && Vector2.Distance(CenterPx(p), centre) > 800f) p.Returning = true;
            }
        }

        /// <summary>The Anchor's turn: along its flight going out, away from the player coming back.</summary>
        static void AnchorTurn(Proj p)
        {
            if (!p.Returning)
            {
                var v = TV(p);
                if (v.sqrMagnitude > 1e-6f) p.TRot = Mathf.Atan2(v.y, v.x) + 1.57f;
            }
            else if (Holder(out var centre))
            {
                var away = CenterPx(p) - centre;
                if (away.sqrMagnitude > 1e-6f) p.TRot = Mathf.Atan2(away.y, away.x) + 1.57f;
            }
            // Main.DrawProj's chain sway: the velocity's average, pulled back towards the player (M:29630-29641).
            var vv = TV(p);
            if (!p.Returning) p.Point = (-vv + p.Point) / 2f;
            else p.Point = (vv + p.Point * 4f) / 5f;
        }

        /// <summary>P:19627-19690: off a block it turns for home without bouncing; after its first 10 updates it smashes the blocks round it (300 px across) with a boom.</summary>
        void AnchorTile(Proj p)
        {
            if (p.Ai1 >= 10f)
            {
                CrackGround(CenterPx(p), 150f);
                Sound("Item_14");
                DevAnchorSmashes++;
            }
            var c = CenterPx(p);
            for (int k = 0; k < 5; k++) BoxDust(c - new Vector2(17f, 17f), 34, 34, 1);
            Sound("Dig_" + Random.Range(0, 3));
            p.Returning = true;
        }

        /// <summary>
        /// M:29543-29690 for 383: Chain34 from 14 px behind the anchor (and half a link) to the player,
        /// a link per link's length, bending toward where it has been swinging the further it reaches
        /// (up to 40% at 400 px); then the anchor, lit.
        /// </summary>
        static void DrawAnchor(FxBatch fx, Proj p, Vector2 px)
        {
            if (Holder(out var player))
            {
                var link = FxBatch.Whole("Chain34");
                if (link != null)
                {
                    float h = link.rect.height;
                    var back = new Vector2(Mathf.Cos(p.TRot - 1.57f), Mathf.Sin(p.TRot - 1.57f));
                    var at = px - back * 14f;
                    var d = (player - at).normalized;
                    at -= d * h / 2f;
                    var rest = player - at;
                    float rot = Mathf.Atan2(rest.y, rest.x) - 1.57f;
                    var lit = new Color(L, L, L, 1f);
                    for (int g = 0; g < 400 && rest.magnitude >= h + 1f; g++)
                    {
                        var step = rest.normalized;
                        float len = rest.magnitude;
                        if (len > 12f)
                        {
                            float k = Mathf.Min(0.4f, len / 1000f);
                            var toward = (player - px).normalized;
                            var sway = p.Point.sqrMagnitude > 1e-6f ? p.Point.normalized : toward;
                            step = toward * (1f - k) + sway * k;
                            rot = Mathf.Atan2(step.y, step.x) - 1.57f;
                        }
                        at += step * h;
                        rest = player - at;
                        fx.Draw(link, at, lit, rot, 1f);
                    }
                }
            }
            DrawGeneric(fx, p, px, new Color(L, L, L, 1f));
        }

        // ------------------------------------------------------------------ 611: the Solar Eruption (AI_075)

        /// <summary>
        /// AI_075 for 611 (P:67878-67915, 68381-68425): its roar as it starts; over 30 ticks its
        /// reach grows by 48 px a tick along a loop flattened by ai[1] (−π/8 to π/8) and turned to
        /// the throw, out and back; held at the player's hand, its far end 48 px past its reach;
        /// fire (6) at its end and along it. An enemy struck sets off a solar blast at most every
        /// 4 ticks.
        /// </summary>
        void StepSolarEruption(Proj p)
        {
            var mod = TerraMod.Instance;
            if (mod == null || !Holder(out var centre) || mod.Inventory.Current.Type != ItemType.SolarEruption)
            {
                Kill(p);
                return;
            }
            p.TimeLeft = 2;
            var v = TV(p);
            if (p.LocalAi0 == 0f)
            {
                Sound("Item_116");
                p.LocalAi0 = ToRot(v);
                if (p.LocalAi0 == 0f) p.LocalAi0 = 1e-4f;
            }
            if (p.LocalAi1 > 0f) p.LocalAi1--;
            p.Alpha = Mathf.Max(0f, p.Alpha - 42f / 255f);
            float aim = p.LocalAi0;
            float sign = Mathf.Cos(aim) >= 0f ? 1f : -1f;
            if (p.Ai1 <= 0f) sign = -sign;
            float a = sign * (p.Ai0 / 30f * Mathf.PI * 2f - Mathf.PI / 2f);
            var loop = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            loop.y *= Mathf.Sin(p.Ai1);
            if (p.Ai1 <= 0f) loop.y = -loop.y;
            loop = RotateBy(loop, aim);
            if (++p.Ai0 >= 30f)
            {
                Kill(p);
                return;
            }
            v += 48f * loop;
            p.Vel = Flip(v);
            float face = Mathf.Cos(aim) >= 0f ? 1f : -1f;
            p.Dir = face;
            // ArmPosition: the hand (the arm points along the whip).
            var hand = TerraParticles.ToPx(mod.HandWorld);
            p.Pos = TerraParticles.ToWorld(hand);
            mod.KeepUsing(ItemDb.Get(ItemType.SolarEruption), Flip(v), face: face);
            if (p.Alpha > 0f) return;
            var dirAim = new Vector2(Mathf.Cos(aim), Mathf.Sin(aim));
            for (int k = 0; k < 2; k++)
            {
                int d = BoxDust(hand + v - new Vector2(8f, 8f), 16, 16, 6, 0f, 0f, 100, 2f);
                if (d < 0) continue;
                D(d).NoGravity = true;
                D(d).Velocity *= 2f;
                D(d).Velocity += dirAim;
                D(d).FadeIn = 1.5f;
            }
            for (int k = 0; k < 18; k++)
            {
                if (Random.Range(0, 4) != 0) continue;
                int d = BoxDust(hand + v * (k / 18f) - new Vector2(8f, 8f), 16, 16, 6, 0f, 0f, 100);
                if (d < 0) continue;
                D(d).NoGravity = true;
                D(d).FadeIn = 0.5f;
                D(d).Velocity += dirAim;
            }
        }

        /// <summary>P:14250-14256: a solar blast on the enemy (0.85-2 its size, 10 knockback), not again for 4 ticks.</summary>
        void SolarEruptionHit(Proj p, HealthManager hm)
        {
            if (p.LocalAi1 <= 0f)
            {
                var blast = MeleeChild(ProjKind.SolarEruptionBlast, EnemyCenterPx(hm), Vector2.zero, p.Damage, 10f, p.Crit);
                if (blast != null) blast.Ai1 = 0.85f + Random.value * 1.15f;
                DevEruptionBlasts++;
            }
            p.LocalAi1 = 4f;
        }

        /// <summary>
        /// M:32910-32970: the hilt at the hand, then the chain (Projectile_611's rows 68-86) along the
        /// whip to its reach, the flame links (rows 46-64) spread over it 18 to a whip (9 when short),
        /// and the head (rows 90-138) at the end; white at alpha 200.
        /// </summary>
        static void DrawSolarEruption(FxBatch fx, Proj p, Vector2 px)
        {
            var whole = FxBatch.Whole("Projectile_611");
            if (whole == null) return;
            var v = TV(p);
            if (v.sqrMagnitude < 1e-6f) return;
            int w = (int)whole.rect.width;
            var c = new Color(1f, 1f, 1f, 200f / 255f);
            float s = p.Scale;
            float len = v.magnitude + 16f;
            bool shortOne = len < 100f;
            var n = v.normalized;
            float rot = ToRot(v) + Mathf.PI / 2f + Mathf.PI;
            var origin = new Vector2(Mathf.Floor(px.x), Mathf.Floor(px.y));
            fx.Draw(FxBatch.Frame("Projectile_611", 0, 2, w, 40, w / 2f, 16f), origin, c, rot, s, overPlayer: true);
            len -= 40f * s;
            var at = origin + n * s * 24f;
            if (len > 0f)
            {
                float done = 0f;
                for (int g = 0; g < 200 && done + 1f < len; g++)
                {
                    int hgt = len - done < 18f ? (int)(len - done) : 18;
                    if (hgt <= 0) break;
                    fx.Draw(FxBatch.Frame("Projectile_611", 0, 68, w, hgt, w / 2f, 0f), at, c, rot, s, overPlayer: true);
                    done += hgt * s;
                    at += n * hgt * s;
                }
            }
            var end = at;
            at = origin + n * s * 24f;
            int links = shortOne ? 9 : 18;
            if (len > 0f)
            {
                float step = len / links;
                at += n * step * 0.25f;
                for (int k = 0; k < links; k++)
                {
                    float gap = k == 0 ? step * 0.75f : step;
                    fx.Draw(FxBatch.Frame("Projectile_611", 0, 46, w, 18, w / 2f, 0f), at, c, rot, s, overPlayer: true);
                    at += n * gap;
                }
            }
            fx.Draw(FxBatch.Frame("Projectile_611", 0, 90, w, 48, w / 2f, 0f), end, c, rot, s, overPlayer: true);
        }

        public static int DevFistCracks, DevAnchorSmashes, DevEruptionBlasts;
    }
}
