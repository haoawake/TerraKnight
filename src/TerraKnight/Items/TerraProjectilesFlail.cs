using System.Collections.Generic;
using TerraKnight.Effects;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// The flails (aiStyle 15, AI_015_Flails, yoyo/spec-yoyo.md Y.2; Terraria's space, px y down):
    /// spun round the player while the button is held, thrown on release, retracted, dragged when
    /// the button is pressed again, back through walls at the end; their chains; Flower Pow's
    /// petals, the Drippler Crippler's extra ball, Flairon's bubbles; Blue Moon and Sunfury
    /// wielded together (1.4.5).
    /// </summary>
    internal sealed partial class TerraProjectiles
    {
        /// <summary>
        /// A flail's numbers (P:44552-44629): throw ticks, launch speed, retract top speed, return
        /// top speed (and the radius it's caught in), spin cooldown, spin radius; its item, chain.
        /// </summary>
        sealed class FlailInfo
        {
            public ItemType Item;
            public int Tex, Throw, SpinCooldown;
            public float Launch, Retract, Return, SpinRadius;
            /// <summary>The chain's texture, a link's step (px), its frames down the texture (the Drippler's six).</summary>
            public string Chain;
            public int ChainW, ChainH, ChainFrames = 1, ChainExtra;
        }

        static readonly Dictionary<ProjKind, FlailInfo> flails = new Dictionary<ProjKind, FlailInfo>
        {
            [ProjKind.MaceFlail] = new FlailInfo { Item = ItemType.Mace, Tex = 947, Throw = 13, Launch = 12, Retract = 8, Return = 13, SpinCooldown = 15, SpinRadius = 30, Chain = "Chain41", ChainW = 8, ChainH = 10 },
            [ProjKind.FlamingMaceFlail] = new FlailInfo { Item = ItemType.FlamingMace, Tex = 948, Throw = 13, Launch = 12, Retract = 8, Return = 13, SpinCooldown = 15, SpinRadius = 30, Chain = "Chain41", ChainW = 8, ChainH = 10 },
            [ProjKind.BallOHurtFlail] = new FlailInfo { Item = ItemType.BallOHurt, Tex = 25, Throw = 15, Launch = 14, Retract = 10, Return = 15, SpinCooldown = 15, SpinRadius = 30, Chain = "Chain2", ChainW = 12, ChainH = 12 },
            [ProjKind.MeatballFlail] = new FlailInfo { Item = ItemType.TheMeatball, Tex = 154, Throw = 15, Launch = 15, Retract = 11, Return = 16, SpinCooldown = 15, SpinRadius = 30, Chain = "Chain13", ChainW = 10, ChainH = 18 },
            [ProjKind.BlueMoonFlail] = new FlailInfo { Item = ItemType.BlueMoon, Tex = 26, Throw = 15, Launch = 16, Retract = 13, Return = 17, SpinCooldown = 15, SpinRadius = 32, Chain = "Chain3", ChainW = 10, ChainH = 12 },
            [ProjKind.SunfuryFlail] = new FlailInfo { Item = ItemType.Sunfury, Tex = 35, Throw = 15, Launch = 17, Retract = 14, Return = 18, SpinCooldown = 15, SpinRadius = 37, Chain = "Chain6", ChainW = 10, ChainH = 12 },
            [ProjKind.DaoOfPowFlail] = new FlailInfo { Item = ItemType.DaoofPow, Tex = 63, Throw = 13, Launch = 21, Retract = 20, Return = 24, SpinCooldown = 12, SpinRadius = 30, Chain = "Chain7", ChainW = 10, ChainH = 10 },
            [ProjKind.DripplerFlail] = new FlailInfo { Item = ItemType.DripplerFlail, Tex = 757, Throw = 13, Launch = 22, Retract = 22, Return = 26, SpinCooldown = 12, SpinRadius = 35, Chain = "Extra_99", ChainW = 16, ChainH = 18, ChainFrames = 6, ChainExtra = -2 },
            [ProjKind.FlowerPowFlail] = new FlailInfo { Item = ItemType.FlowerPow, Tex = 247, Throw = 13, Launch = 23, Retract = 16, Return = 48, SpinCooldown = 12, SpinRadius = 40, Chain = "Chain19", ChainW = 14, ChainH = 18 },
            [ProjKind.FlaironFlail] = new FlailInfo { Item = ItemType.FlaironFlail, Tex = 1058, Throw = 16, Launch = 23, Retract = 16, Return = 48, SpinCooldown = 10, SpinRadius = 45, Chain = "Chain37", ChainW = 10, ChainH = 16 },
        };

        static void RegisterFlails()
        {
            // DefaultToFlail (P:10755-10764): through everything, melee, its own 10 ticks (by state).
            foreach (var kv in flails)
            {
                var f = kv.Value;
                int size = kv.Key == ProjKind.MaceFlail || kv.Key == ProjKind.FlamingMaceFlail ? 20 : kv.Key == ProjKind.FlowerPowFlail || kv.Key == ProjKind.DripplerFlail ? 34
                         : kv.Key == ProjKind.FlaironFlail ? 26 : 22;
                float scale = kv.Key == ProjKind.BallOHurtFlail ? 0.9f : kv.Key == ProjKind.BlueMoonFlail || kv.Key == ProjKind.SunfuryFlail || kv.Key == ProjKind.MeatballFlail ? 0.8f : 1f;
                specs[kv.Key] = new Spec
                {
                    Ai = Ai.Custom, Texture = "Projectile_" + f.Tex, W = size, H = size, Scale = scale, Time = 2, Penetrate = -1, LocalCooldown = 10, PushFromPlayer = true, Held = true,
                    AlphaStart = kv.Key == ProjKind.FlaironFlail ? 1f : 0f,
                    TileBox = kv.Key == ProjKind.FlowerPowFlail || kv.Key == ProjKind.DripplerFlail ? new Vector2(size - 12, size - 12) : Vector2.zero,
                    CanDamage = p => !(p.Ai0 == 0f && p.LocalAi1 <= 12f),
                    HitArea = p => FlailArea(p), HitTest = (p, col) => FlailTouch(p, col),
                    ModifyHit = (t, p, hm, d, c) => (FlailDamage(t, p, d), c), KnockbackScale = p => p.Ai0 == 0f ? 0.35f : p.Ai0 == 6f ? 0.5f : 1f,
                    Update = (t, p) => t.StepFlail(p), Draw = (t, fx, p, px) => t.DrawFlail(fx, p, px), OnKill = (t, p) => { },
                    TileHit = (t, p, x, y, v) => FlailTile(p, x, y, v), OnHit = (t, p, hm) => FlailDebuffs(p, hm),
                };
            }
            // P:3308-3314: Flower Pow's petal (AI_001 without gravity), straight on until it hits.
            specs[ProjKind.FlowerPowPetal] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_248", W = 18, H = 18, Time = 600, TileBox = new Vector2(6f, 6f),
                Update = (t, p) => { if (p.Updates == 0) Sound("Item_17"); var v = TV(p); p.TRot += (Mathf.Abs(v.x) + Mathf.Abs(v.y)) * 0.05f * (v.x < 0f ? -1f : 1f); },
                Draw = (t, fx, p, px) => fx.Draw(Sprite(p.Spec.Texture), px, new Color(L, L, L, 1f), p.TRot, p.Scale),
            };
            // P:8795-8804: the Drippler Crippler's second ball (aiStyle 14), bouncing, through two, 250 ticks.
            specs[ProjKind.DripplerBall] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_928", W = 34, H = 34, Time = 250, Penetrate = 2, TileBox = new Vector2(22f, 22f),
                Update = (t, p) => StepDripplerBall(p), Draw = (t, fx, p, px) => fx.Draw(Sprite(p.Spec.Texture), px, new Color(L, L, L, 1f), p.TRot, p.Scale),
                TileHit = (t, p, x, y, v) => t.DripplerBallTile(p, x, y, v), OnKill = (t, p) => KillDripplerBall(p),
            };
            // P:4859-4868: Flairon's bubble (aiStyle 70), seeking, one hit, 120 ticks.
            specs[ProjKind.FlaironBubble] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_405", W = 14, H = 14, Time = 120, AlphaStart = 1f,
                Update = (t, p) => t.StepFlaironBubble(p), Draw = (t, fx, p, px) => fx.Draw(Sprite(p.Spec.Texture), px, new Color(L, L, L, 1f) * (1f - p.Alpha), p.TRot, p.Scale),
                TileHit = (t, p, x, y, v) => { if (++p.LocalAi0 >= 2f) t.Kill(p); else { if (x) p.Vel.x = -v.x; if (y) p.Vel.y = -v.y; } },
                OnKill = (t, p) => KillFlaironBubble(p),
            };
            // P:4849-4858: Flairon's other form (2611), thrown and drawn back (aiStyle 69): 26 px, through everything, held while out.
            specs[ProjKind.FlaironHead] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_404", W = 26, H = 26, Time = 3600, Penetrate = -1, AlphaStart = 1f, Held = true,
                Update = (t, p) => t.StepFlaironHead(p), Draw = (t, fx, p, px) => DrawFlaironHead(fx, p, px), OnKill = (t, p) => { },
                // P:14779-14783, 19627-19688: back it comes from the first enemy or block it meets (the block chipped, a thud).
                OnHit = (t, p, hm) => p.Ai0 = Mathf.Max(p.Ai0, 1f),
                TileHit = (t, p, x, y, v) =>
                {
                    HitTileDust(p);
                    Sound("Dig_" + Random.Range(0, 3));
                    p.Ai0 = Mathf.Max(p.Ai0, 1f);
                },
            };
        }

        public static int DevFlaironHeads, DevFlaironHeadBubbles;

        /// <summary>
        /// AI_069 (P:30731-30789): it faces the player, flies straight out (no gravity) and past
        /// 400 px turns home through everything at 20 px a tick (40 beyond 600; lost beyond 1500),
        /// gone on reaching the hand; shown from its sixth tick, it lets a seeking bubble go outward
        /// every fourth (4.5-6.5 px a tick, up to 45 degrees off).
        /// </summary>
        void StepFlaironHead(Proj p)
        {
            if (!Holder(out var owner))
            {
                Kill(p);
                return;
            }
            if (p.Ai1 == 0f) DevFlaironHeads++;
            var c = CenterPx(p);
            var to = owner - c;
            p.TRot = Mathf.Atan2(to.y, to.x) - 1.57f;
            p.Dir = to.x > 0f ? -1f : 1f;
            if (p.Ai0 == 0f && to.magnitude > 400f) p.Ai0 = 1f;
            if (p.Ai0 >= 1f)
            {
                float d = to.magnitude;
                if (d > 1500f)
                {
                    Kill(p);
                    return;
                }
                if (d > 600f) p.Ai0 = 2f;
                p.Collide = false;
                float speed = p.Ai0 == 2f ? 40f : 20f;
                p.Vel = Flip(Norm(to) * speed);
                if (d < speed)
                {
                    Kill(p);
                    return;
                }
            }
            if (++p.Ai1 > 5f) p.Alpha = 0f;
            if ((int)p.Ai1 % 4 == 0)
            {
                var b = RotateBy(Norm(-to) * Random.Range(45, 65) * 0.1f, (Random.value - 0.5f) * Mathf.PI / 2f);
                var keep = SpawnClass;
                SpawnClass = DamageClass.Melee;
                var bubble = Spawn(ProjKind.FlaironBubble, p.Pos, Flip(b), p.Damage, p.Knockback, p.Crit);
                SpawnClass = keep;
                if (bubble != null)
                {
                    bubble.Ai0 = -10f;
                    bubble.Melee = true;
                    DevFlaironHeadBubbles++;
                }
            }
        }

        /// <summary>M:29543-29696: the Chain37 links from the hand out to it every 16 px, then the head, mirrored by which side the player is.</summary>
        static void DrawFlaironHead(FxBatch fx, Proj p, Vector2 px)
        {
            if (!Holder(out var owner)) return;
            var to = owner - px;
            if (to.sqrMagnitude > 1e-4f)
            {
                var d = to.normalized;
                float remaining = to.magnitude;
                var at = px;
                for (int link = 0; remaining >= 17f && link < 200; link++)
                {
                    at += d * 16f;
                    remaining -= 16f;
                    fx.Draw(FxBatch.Frame("Chain37", 0, 0, 10, 16, 5f, 8f), at, new Color(L, L, L, 1f) * (1f - p.Alpha), Mathf.Atan2(d.y, d.x) - 1.57f, 1f);
                }
            }
            fx.Draw(Sprite(p.Spec.Texture), px, new Color(L, L, L, 1f) * (1f - p.Alpha), p.TRot, p.Scale, flipX: p.Dir < 0f);
        }

        static bool IsFlail(ProjKind k) => flails.ContainsKey(k);

        /// <summary>The flail's partner (Blue Moon with Sunfury), if it's out.</summary>
        Proj FlailPartner(Proj p)
        {
            foreach (var q in live)
                if (q != p && !q.Dead && IsFlail(q.Kind) && (q.Kind == ProjKind.BlueMoonFlail || q.Kind == ProjKind.SunfuryFlail) && q.Kind != p.Kind) return q;
            return null;
        }

        /// <summary>
        /// AI_015_Flails (P:44530-44985), one update a tick: 0 spinning on the held button, 1 thrown,
        /// 2 retracting, 4 coming back through walls, 5 stunned by a wall, 6 dragged.
        /// </summary>
        void StepFlail(Proj p)
        {
            var mod = TerraMod.Instance;
            var f = flails[p.Kind];
            if (mod == null || !Holder(out var owner))
            {
                Kill(p);
                return;
            }
            var def = ItemDb.Get(f.Item);
            if (p.Item == ItemType.None)
            {
                // Thrown by the item: latched on its button; Blue Moon with Sunfury in the bag throws both (Pl:49931-49942).
                p.Item = f.Item;
                p.Channel = true;
                p.Melee = true;
                if ((p.Kind == ProjKind.BlueMoonFlail || p.Kind == ProjKind.SunfuryFlail) && !p.OffHand)
                {
                    var otherItem = p.Kind == ProjKind.BlueMoonFlail ? ItemType.Sunfury : ItemType.BlueMoon;
                    bool inBag = false;
                    foreach (var slot in mod.Inventory.Slots) if (!slot.IsEmpty && slot.Type == otherItem) inBag = true;
                    if (inBag)
                    {
                        var other = ItemDb.Get(otherItem);
                        var keep = SpawnClass;
                        SpawnClass = DamageClass.Melee;
                        var partner = Spawn(other.Shoot, p.Pos, -p.Vel, Equipment.Damage(other), Equipment.Knockback(other), Equipment.Crit(other));
                        SpawnClass = keep;
                        if (partner != null)
                        {
                            partner.OffHand = true;
                            partner.Item = otherItem;
                            partner.Channel = true;
                            partner.Melee = true;
                            p.Dual = true;
                            partner.Dual = true;
                        }
                    }
                }
            }
            float ms = Equipment.UseScale(def), k = 1f / ms;
            float launch = f.Launch * k, retract = f.Retract * k, ret = f.Return * k, retractAcc = 3f * k, returnAcc = 6f * k;
            float maxLen = launch * f.Throw;
            var c = CenterPx(p);
            var v = TV(p);
            var toPlayer = owner - c;
            if (toPlayer.magnitude > 900f / ms || hurtLocked())
            {
                Kill(p);
                return;
            }
            bool wasSpinning = p.Ai0 == 0f;
            bool pressed = Channel(p.Item) || (p.OffHand && Channel(ItemType.BlueMoon)) || (p.OffHand && Channel(ItemType.Sunfury));
            bool fast = false;
            var aim = AimPx() - owner;
            aim = aim.sqrMagnitude > 1e-6f ? aim.normalized : new Vector2(TerraMod.Instance.UseDirection, 0f);
            bool mirror = p.OffHand;
            switch ((int)p.Ai0)
            {
                case 0:
                {
                    p.OwnerCheck = true;
                    p.Channel &= pressed;
                    if (!p.Channel)
                    {
                        p.Ai0 = 1f;
                        p.Ai1 = 0f;
                        var rb = HeroController.SilentInstance.GetComponent<Rigidbody2D>();
                        var pv = rb != null ? new Vector2(rb.linearVelocity.x, -rb.linearVelocity.y) / Player.TerraMovement.PxT : Vector2.zero;
                        v = aim * launch + pv;
                        if (mirror) v.x = -v.x;
                        c = owner;
                        p.Immune.Clear();
                        p.Cooldown = 10;
                        break;
                    }
                    p.LocalAi1++;
                    float dir = (mirror ? -aim.x : aim.x) < 0f ? -1f : 1f;
                    var o = RotateBy(new Vector2(dir, dir), Mathf.PI * 10f * (p.LocalAi1 / 60f) * dir);
                    o.y *= 0.8f;
                    if (o.y > 0f) o.y *= 0.5f;
                    c = owner + o * f.SpinRadius;
                    v = Vector2.zero;
                    p.Cooldown = f.SpinCooldown;
                    mod.KeepUsing(def, Flip(mirror ? new Vector2(-aim.x, aim.y) : aim));
                    break;
                }
                case 1:
                {
                    fast = true;
                    bool done = p.Ai1++ >= f.Throw || toPlayer.magnitude >= 800f;
                    if (pressed)
                    {
                        p.Ai0 = 6f;
                        p.Ai1 = 0f;
                        v *= 0.2f;
                        if (p.Kind == ProjKind.DripplerFlail) DripplerExtra(p, v);
                        break;
                    }
                    if (done)
                    {
                        p.Ai0 = 2f;
                        p.Ai1 = 0f;
                        v *= 0.3f;
                        if (p.Kind == ProjKind.DripplerFlail) DripplerExtra(p, v);
                    }
                    p.Cooldown = 10;
                    break;
                }
                case 2:
                {
                    if (toPlayer.magnitude <= retract)
                    {
                        Kill(p);
                        return;
                    }
                    if (pressed)
                    {
                        p.Ai0 = 6f;
                        p.Ai1 = 0f;
                        v *= 0.2f;
                    }
                    else
                    {
                        v *= 0.98f;
                        v = Vector2.MoveTowards(v, toPlayer.normalized * retract, retractAcc);
                    }
                    break;
                }
                case 4:
                {
                    p.Collide = false;
                    if (toPlayer.magnitude <= ret)
                    {
                        Kill(p);
                        return;
                    }
                    v *= 0.98f;
                    v = Vector2.MoveTowards(v, toPlayer.normalized * ret, returnAcc);
                    var after = owner - (c + v);
                    if (Vector2.Dot(toPlayer.normalized, after.normalized) < 0f)
                    {
                        Kill(p);
                        return;
                    }
                    break;
                }
                case 5:
                {
                    if (p.Ai1++ >= f.Throw + 5)
                    {
                        p.Ai0 = 6f;
                        p.Ai1 = 0f;
                    }
                    else
                    {
                        p.Cooldown = 10;
                        v.y += 0.6f;
                        v.x *= 0.95f;
                    }
                    break;
                }
                case 6:
                {
                    if (!pressed || toPlayer.magnitude > maxLen + 160f)
                    {
                        p.Ai0 = 4f;
                        p.Ai1 = 0f;
                        break;
                    }
                    v.y += 0.8f;
                    v.x *= 0.95f;
                    break;
                }
            }
            if (p.Ai0 != 0f) mod.KeepUsing(def, Flip(c - owner));
            if (p.Kind == ProjKind.FlowerPowFlail) FlowerPowPetals(p, c, v, wasSpinning);
            if (p.Kind == ProjKind.FlaironFlail) FlaironBubbles(p, c, owner);
            if (p.Kind != ProjKind.FlaironFlail) p.Dir = v.x > 0f ? 1f : -1f;
            p.OwnerCheck = wasSpinning;
            if (p.Kind != ProjKind.FlowerPowFlail && p.Kind != ProjKind.FlaironFlail)
                p.TRot = v.magnitude > 1f ? Mathf.Atan2(v.y, v.x) + v.x * 0.1f : p.TRot + v.x * 0.1f;
            p.TimeLeft = 2;
            if (p.Ai0 != 4f) p.Collide = p.Ai0 != 0f;
            FlailDust(p, c, v, fast, owner);
            p.Pos = TerraParticles.ToWorld(c);
            p.Vel = Flip(v);
        }

        /// <summary>player.CCed for a flail: no control (hurt and stunned) kills it (P:44533-44537).</summary>
        static bool hurtLocked() => !GameHooks.PlayerHasControl();

        /// <summary>Spinning: the box round the player (an ellipse 55 px out, flatter below); else its own box.</summary>
        static (Vector2, Vector2) FlailArea(Proj p)
        {
            if (p.Ai0 == 0f && Holder(out var owner)) return (TerraParticles.ToWorld(owner), new Vector2(110f, 110f) * Px);
            var s = p.Spec;
            return (p.Pos, new Vector2((int)(s.W * s.Scale), (int)(s.H * s.Scale)) * Px);
        }

        /// <summary>Colliding (P:15421-15441): spinning, the enemy box's point nearest the player within 55 px, its height /0.8 (/0.4 below).</summary>
        static bool FlailTouch(Proj p, Collider2D col)
        {
            if (p.Ai0 != 0f || !Holder(out var owner)) return true;
            var close = TerraParticles.ToPx(col.ClosestPoint(TerraParticles.ToWorld(owner)));
            var d = close - owner;
            d.y /= d.y > 0f ? 0.4f : 0.8f;
            return d.magnitude <= 55f;
        }

        /// <summary>P:14502-14535: spinning ×1.2 (×0.96 with a partner out); thrown or retracting ×2.</summary>
        static int FlailDamage(TerraProjectiles t, Proj p, int dmg)
        {
            if (p.Ai0 == 0f) return (int)(dmg * (p.Dual && t.FlailPartner(p) != null ? 1.2f * 0.8f : 1.2f));
            if (p.Ai0 == 1f || p.Ai0 == 2f) return dmg * 2;
            return dmg;
        }

        /// <summary>StatusNPC (P:11621-11659): the Flaming Mace 1/6 and Sunfury 1/2 set on fire, Dao of Pow confuses 4 in 5.</summary>
        static void FlailDebuffs(Proj p, HealthManager hm)
        {
            if (p.Kind == ProjKind.FlamingMaceFlail && Random.Range(0, 6) == 0) EnemyDebuffs.Add(hm, EnemyDebuffs.OnFire, 60 * Random.Range(2, 4));
            if (p.Kind == ProjKind.SunfuryFlail && Random.Range(0, 2) == 0) EnemyDebuffs.Add(hm, EnemyDebuffs.OnFire, 300);
            if (p.Kind == ProjKind.DaoOfPowFlail && Random.Range(0, 5) != 0) EnemyDebuffs.Add(hm, EnemyDebuffs.Confused, 60 * Random.Range(2, 5));
        }

        /// <summary>
        /// AI_015_HandleMovementCollision (P:44461-44523): bounces (0.4 thrown or stunned, 0 dragged,
        /// else 0.2) off blocks above 4 px a tick; the first wall while thrown stuns it with a puff of
        /// dust and a thud; after ten bounces it comes home.
        /// </summary>
        static void FlailTile(Proj p, bool hitX, bool hitY, Vector2 oldVel)
        {
            float bounce = p.Ai0 == 1f || p.Ai0 == 5f ? 0.4f : p.Ai0 == 6f ? 0f : 0.2f;
            int impact = 0;
            if (hitX)
            {
                if (Mathf.Abs(oldVel.x) > 4f) { impact = 1; p.Vel.x = -oldVel.x * bounce; p.LocalAi0++; }
                else p.Vel.x = 0f;
            }
            if (hitY)
            {
                if (Mathf.Abs(oldVel.y) > 4f) { impact = 1; p.Vel.y = -oldVel.y * bounce; p.LocalAi0++; }
                else p.Vel.y = 0f;
            }
            var c = CenterPx(p);
            if (p.Ai0 == 1f)
            {
                p.Ai0 = 5f;
                p.Ai1 = 0f;
                p.Cooldown = 10;
                impact = 2;
                var dir = Flip(oldVel).sqrMagnitude > 1e-6f ? Flip(oldVel).normalized : Vector2.zero;
                for (int i = 0; i < 8; i++)
                {
                    float a = i / 8f * Mathf.PI * 2f;
                    int d = PointDust(c + new Vector2(Mathf.Cos(a) * 7f, Mathf.Sin(a) * 7f * 0.7f), 31, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 0.8f + dir * 2f, 0, 0.4f);
                    if (d < 0) continue;
                    D(d).FadeIn = Random.Range(0.3f, 0.7f);
                    D(d).Velocity.y -= 0.8f;
                }
                p.Pos -= oldVel * Px;
            }
            if (impact > 0)
            {
                for (int i = 0; i < 4 * impact; i++) DustBox(p, 1, Vector2.zero, 0, 1f, false);
                Sound("Dig_0", 0.6f);
            }
            if (p.Ai0 != 0f && p.Ai0 != 3f && p.Ai0 != 5f && p.Ai0 != 6f && p.LocalAi0 >= 10f) p.Ai0 = 4f;
        }

        /// <summary>AI_015_Flails_Dust (P:45219-45293): each ball's motes, more while thrown.</summary>
        static void FlailDust(Proj p, Vector2 c, Vector2 v, bool fast, Vector2 owner)
        {
            int d;
            switch (p.Kind)
            {
                case ProjKind.BallOHurtFlail:
                    if (fast || Random.Range(0, 15) == 0) DustBox(p, 14, Vector2.zero, 150, 1.3f, false);
                    break;
                case ProjKind.DripplerFlail:
                    for (int k = 0; k < 2; k++)
                    {
                        int n = v.magnitude < 8f ? 10 : 4;
                        if (fast) n /= 2;
                        if (Random.Range(0, n) == 0 && (d = DustBox(p, 5, Vector2.zero, 0, 0.8f, false)) >= 0)
                        {
                            D(d).Velocity += v / 4f;
                            D(d).FadeIn = 1.3f;
                        }
                    }
                    for (int k = 0; k < 10; k++)
                        if (Random.Range(0, fast ? 20 : 40) == 0 && (d = BoxDust(Vector2.Lerp(owner, c, Random.value) - new Vector2(8f, 8f), 16, 16, 5, 0f, 0f, 0, 1.3f)) >= 0)
                            D(d).Velocity += v / 4f;
                    break;
                case ProjKind.BlueMoonFlail:
                    if ((d = DustBox(p, 172, v * 0.4f, 100, 1.5f, true)) >= 0) D(d).Velocity /= 2f;
                    break;
                case ProjKind.FlamingMaceFlail:
                    if ((d = DustBox(p, 6, v * 0.4f, 100, 1.2f, true)) >= 0) D(d).Velocity = (D(d).Velocity * 4f + v) / 2f;
                    break;
                case ProjKind.SunfuryFlail:
                    if ((d = DustBox(p, 6, v * 0.4f, 100, 3f, true)) >= 0) D(d).Velocity *= 2f;
                    break;
                case ProjKind.MeatballFlail:
                    if ((d = DustBox(p, 115, v * 0.4f, 140, 1.5f, true)) >= 0) D(d).Velocity *= 0.25f;
                    break;
            }
        }

        // ------------------------------------------------------------------ the children

        /// <summary>P:44914-44956: Flower Pow spins with its speed (faster while spun) and every 20 ticks shoots a petal at an enemy within 600 px.</summary>
        void FlowerPowPetals(Proj p, Vector2 c, Vector2 v, bool spinning)
        {
            p.TRot += (Mathf.Abs(v.x) + Mathf.Abs(v.y)) * 0.01f * (v.x < 0f ? -1f : 1f);
            if (spinning) p.TRot += Mathf.PI * 2f / 15f * TerraMod.Instance.UseDirection;
            if (++p.Timer2 < 20f) return;
            p.Timer2 = 17f;
            HealthManager best = null;
            float bestD = 600f;
            foreach (var hm in AccessoryEffects.Enemies(p.Pos, 600f, accNear))
            {
                if (!Chaseable(hm)) continue;
                var e = EnemyCenterPx(hm);
                float d = Vector2.Distance(e, c);
                if (d >= bestD || !TerraWorld.LineClearPx(c, e)) continue;
                bestD = d;
                best = hm;
            }
            if (best == null) return;
            p.Timer2 = 0f;
            var keep = SpawnClass;
            SpawnClass = DamageClass.Melee;
            var petal = Spawn(ProjKind.FlowerPowPetal, p.Pos, Flip(Norm(EnemyCenterPx(best) - c) * 14f), (int)(p.Damage / 1.5f), p.Knockback / 2f, p.Crit);
            SpawnClass = keep;
            if (petal != null) petal.Melee = true;
        }

        /// <summary>P:44722-44738: the Drippler Crippler leaves a second ball as it stops flying out.</summary>
        void DripplerExtra(Proj p, Vector2 v)
        {
            var keep = SpawnClass;
            SpawnClass = DamageClass.Melee;
            var ball = Spawn(ProjKind.DripplerBall, p.Pos, Flip(v * 1.3f), p.Damage, p.Knockback, p.Crit);
            SpawnClass = keep;
            if (ball != null) ball.Melee = true;
        }

        /// <summary>AI_014 (P:25449-25466): falling after five ticks, rolling to a stop on the ground.</summary>
        static void StepDripplerBall(Proj p)
        {
            var v = TV(p);
            if (++p.Ai0 > 5f)
            {
                p.Ai0 = 5f;
                v.y += 0.2f;
            }
            if (v.y > 16f) v.y = 16f;
            p.TRot += v.x * 0.1f;
            p.Vel = Flip(v);
        }

        /// <summary>P:19742-19860: half back off walls and off the floor when coming down; the oldest of more than three goes.</summary>
        void DripplerBallTile(Proj p, bool hitX, bool hitY, Vector2 oldVel)
        {
            if (hitX) p.Vel.x = -oldVel.x * 0.5f;
            if (hitY)
            {
                if (oldVel.y < -1f) p.Vel.y = -oldVel.y * 0.5f;
                else
                {
                    p.Vel.y = 0f;
                    p.Vel.x *= 0.97f;
                    if (Mathf.Abs(p.Vel.x) < 0.01f) p.Vel.x = 0f;
                }
            }
            var balls = new List<Proj>();
            foreach (var q in live) if (q.Kind == ProjKind.DripplerBall && !q.Dead && q != p) balls.Add(q);
            if (balls.Count >= 2)
            {
                balls.Sort((a, b) => a.TimeLeft.CompareTo(b.TimeLeft));
                for (int i = 0; i < balls.Count - 1; i++) if (balls[i].TimeLeft > 30) balls[i].TimeLeft = 2;
            }
        }

        /// <summary>P:75927-75933: a thud and a splash of blood.</summary>
        static void KillDripplerBall(Proj p)
        {
            Sound("Dig_0", 0.6f);
            for (int i = 0; i < 25; i++)
            {
                int d = DustBox(p, 5, TV(p), 0, 1.3f, false);
            }
        }

        /// <summary>P:44864-44912: Flairon faces the player, fading in, and streams seeking bubbles outward (every 3-5 ticks spun, 6 dragged, else 4).</summary>
        void FlaironBubbles(Proj p, Vector2 c, Vector2 owner)
        {
            p.Alpha = Mathf.Max(0f, p.Alpha - 50f / 255f);
            var v = owner - c;
            p.TRot = Mathf.Atan2(v.y, v.x) - 1.57f;
            p.Dir = v.x < 0f ? 1f : -1f;
            if (--p.SoundDelay > 0) return;
            bool spinning = p.Ai0 == 0f;
            p.SoundDelay = spinning ? Random.Range(3, 6) : p.Ai0 == 6f ? 6 : 4;
            var b = RotateBy(Norm(-v) * Random.Range(45, 65) * 0.1f, (Random.value - 0.5f) * Mathf.PI / 2f);
            var keep = SpawnClass;
            SpawnClass = DamageClass.Melee;
            var bubble = Spawn(ProjKind.FlaironBubble, p.Pos, Flip(b), p.Damage, spinning ? p.Knockback * 0.5f : p.Knockback, p.Crit);
            SpawnClass = keep;
            if (bubble != null)
            {
                bubble.Ai0 = -10f;
                bubble.Melee = true;
            }
        }

        /// <summary>AI_070 (P:30798-30841): drifting 10 ticks, then on to the nearest enemy within 650 px it can see.</summary>
        void StepFlaironBubble(Proj p)
        {
            var v = TV(p);
            var c = CenterPx(p);
            if (p.Ai1 == 0f) p.Ai1 = Random.Range(80, 121) / 100f;
            p.Scale = p.Ai1;
            if (p.Ai0 == 0f)
            {
                HealthManager best = null;
                float bestD = 650f;
                foreach (var hm in AccessoryEffects.Enemies(p.Pos, 650f, accNear))
                {
                    if (!Chaseable(hm)) continue;
                    var e = EnemyCenterPx(hm);
                    float d = Vector2.Distance(e, c);
                    if (d >= bestD || !TerraWorld.LineClearPx(c, e)) continue;
                    bestD = d;
                    best = hm;
                }
                if (best != null)
                {
                    p.Target = best;
                    p.Ai0 = 1f;
                    float s = Random.Range(35, 75) / 30f;
                    v = (v * 20f + Norm(EnemyCenterPx(best) - c + new Vector2(Random.Range(-100, 101), Random.Range(-100, 101))) * s) / 21f;
                }
                else p.Ai0 = -15f;
            }
            else if (p.Ai0 > 0f)
            {
                if (p.Target == null || !Chaseable(p.Target)) p.Ai0 = 0f;
                else v = (v * 40f + Norm(EnemyCenterPx(p.Target) - c) * 12f) / 41f;
            }
            else
            {
                p.Ai0++;
                p.Alpha = Mathf.Max(50f / 255f, p.Alpha - 25f / 255f);
                v *= 0.95f;
            }
            p.Vel = Flip(v);
        }

        /// <summary>P:74184-74207: a pop and a ring of twenty sea-green motes.</summary>
        static void KillFlaironBubble(Proj p)
        {
            Sound("Item_54", 0.6f);
            var c = CenterPx(p);
            for (int i = 0; i < 20; i++)
            {
                float a = i / 20f * Mathf.PI * 2f;
                var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                int d = PointDust(c + dir * 10f * p.Scale, 212, dir, 0, 0.7f);
                if (d < 0) continue;
                D(d).NoGravity = true;
                D(d).Color = Color.Lerp(FxParticles.HslToRgb(0.4f + 0.2f * Random.value, 0.9f, 0.5f), Color.white, 0.3f);
            }
        }

        // ------------------------------------------------------------------ the draw (M:35949-36038, 29543-29696)

        /// <summary>
        /// The chain from the ball to the player's arm (lit links, the Flaming Mace's first ones
        /// burning), then the ball turning, over the player while held (drawLayer 7; a dual pair
        /// behind).
        /// </summary>
        void DrawFlail(FxBatch fx, Proj p, Vector2 px)
        {
            var f = flails[p.Kind];
            if (!Holder(out var owner)) return;
            bool over = !p.Dual;
            var dirAim = px - owner;
            float facing = dirAim.x < 0f ? -1f : 1f;
            var arm = p.Kind == ProjKind.FlaironFlail ? owner : ArmPx(owner, facing, AimFrame(dirAim));
            var to = Vector2.MoveTowards(arm, px, 4f) - px;
            if (to.sqrMagnitude > 1e-4f)
            {
                var d = to.normalized;
                float step = f.ChainH + f.ChainExtra;
                float rot = Mathf.Atan2(d.y, d.x) + Mathf.PI / 2f;
                float remaining = to.magnitude + step / 2f;
                var at = px;
                int link = 0;
                if (p.Kind == ProjKind.FlaironFlail)
                {
                    remaining = to.magnitude;
                    while (remaining >= 17f && link < 200)
                    {
                        at += d * 16f;
                        remaining -= 16f;
                        fx.Draw(FxBatch.Frame("Chain37", 0, 0, 10, 16, 5f, 8f), at, new Color(L, L, L, 1f) * (1f - p.Alpha), Mathf.Atan2(d.y, d.x) - 1.57f, 1f, overPlayer: over);
                        link++;
                    }
                }
                else
                    while (remaining > 0f && link < 200)
                    {
                        string tex = f.Chain;
                        int w = f.ChainW, h = f.ChainH, frame = f.ChainFrames > 1 ? link % f.ChainFrames : 0;
                        var col = new Color(L, L, L, 1f);
                        if (p.Kind == ProjKind.FlamingMaceFlail)
                        {
                            if (link < 4) { tex = "Chain43"; col = Color.white; }
                            else if (link < 6) { tex = "Chain42"; col = new Color(Mathf.Max(L, 140f / 255f), Mathf.Max(L, 140f / 255f), Mathf.Max(L, 140f / 255f), 1f); }
                        }
                        fx.Draw(FxBatch.Frame(tex, 0, frame * h, w, h, w * 0.5f, h * 0.5f), at, col, rot, 1f, overPlayer: over);
                        at += d * step;
                        remaining -= step;
                        link++;
                    }
            }
            var ball = p.Kind == ProjKind.FlamingMaceFlail ? Color.white : new Color(L, L, L, 1f);
            fx.Draw(Sprite(p.Spec.Texture), px, ball * (1f - p.Alpha), p.TRot, p.Scale, flipX: p.Dir < 0f, overPlayer: over);
        }
    }
}
