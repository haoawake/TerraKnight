using System.Collections.Generic;
using TerraKnight.Effects;
using TerraKnight.Terraria;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// The pets out (Items/Pets.cs): a vanity pet and a light pet, moved and drawn beside the minions but no minions
    /// themselves (no slots, no hits). Walkers go by AI_026's walking and flying (P:59086-63340), orbs and fairies by
    /// aiStyle 11 (P:24651), flyers after the player, the held ones at the player's side, worms winding after the player.
    /// </summary>
    internal sealed partial class Minions
    {
        static readonly KindSpec petSpec = new KindSpec { Minion = false, Tracked = false };
        /// <summary>The pet out of each kind (vanity, light) and the item it came from.</summary>
        readonly M[] pets = new M[2];
        readonly ItemType[] petItems = { ItemType.None, ItemType.None };

        /// <summary>Dev: the pet out of a kind, its projectile id and where it is (px), or null.</summary>
        public static (int id, Vector2 at, int frame, bool flying)? DevPet(bool light)
        {
            var m = Instance?.pets[light ? 1 : 0];
            if (m == null) return null;
            return (m.Pet.Id, m.Center, m.Frame, m.Ai0 == 1f);
        }

        void PetsTick()
        {
            Pets.Tick(TerraMod.Instance?.Inventory, ownerDead);
            for (int k = 0; k < 2; k++)
            {
                var t = ownerDead ? ItemType.None : Pets.Current(k == 1);
                if (t != petItems[k])
                {
                    petItems[k] = t;
                    if (pets[k] != null) wormTrails.Remove(pets[k]);
                    pets[k] = null;
                    var def = Pets.Of(t);
                    if (def != null) pets[k] = NewPet(def.Projs[Random.Range(0, def.Projs.Length)]);
                }
                var m = pets[k];
                if (m == null) continue;
                StepPet(m);
            }
        }

        M NewPet(PetProj p)
        {
            var m = new M { S = petSpec, Pet = p, W = p.W, H = p.H, Scale = p.Scale, Alpha = p.Alpha / 255f, TimeLeft = int.MaxValue };
            m.Center = pCenter;
            m.OldPos = m.Pos;
            m.TileCollide = p.Move == PetMove.Ground;
            m.Frame = p.Move == PetMove.Ground ? p.Idle : p.FlyStart;
            if (p.Move == PetMove.Worm) m.Vel = new Vector2(0f, -4f);
            return m;
        }

        void StepPet(M m)
        {
            var p = m.Pet;
            if (m.GfxOffY != 0f)
            {
                float k = (1f + Mathf.Abs(m.Vel.x) / 3f) * m.StepSpeed;
                m.GfxOffY = m.GfxOffY > 0f ? Mathf.Max(0f, m.GfxOffY - k) : Mathf.Min(0f, m.GfxOffY + k);
                m.GfxOffY = Mathf.Clamp(m.GfxOffY, -16f, 16f);
            }
            switch (p.Move)
            {
                case PetMove.Ground: PetWalker(m, p); break;
                case PetMove.Orb: PetOrb(m, p); break;
                case PetMove.Flyer: PetFlyer(m, p); break;
                case PetMove.Anchor: PetHeld(m, p); break;
                case PetMove.Worm: PetWorm(m, p); break;
            }
            m.OldPos = m.Pos;
            if (m.TileCollide) TileCollision(m, m.FallThrough);
            else m.Pos += m.Vel;
            if (p.Light.r + p.Light.g + p.Light.b > 0.01f) FxBatch.Instance?.Light(m.Center, p.Light, 5f);
        }

        /// <summary>The sprite turned by a move across: most pets' art faces left, flipped going right.</summary>
        static void FacePet(M m, PetProj p, float vx, float dead)
        {
            int dir = vx > dead ? 1 : vx < -dead ? -1 : 0;
            if (dir != 0) m.SpriteDir = p.FacesRight ? dir : -dir;
        }

        /// <summary>A loop of frames: from start, back to `to` at `wrap`, a frame every `ticks`.</summary>
        static void LoopPet(M m, int start, int wrap, int to, int ticks)
        {
            if (m.Frame < start || m.Frame >= wrap) m.Frame = to;
            if (++m.FrameCounter < ticks) return;
            m.FrameCounter = 0;
            if (++m.Frame >= wrap) m.Frame = to;
        }

        void PetFlyFrames(M m, PetProj p)
        {
            if (p.FlyFixed >= 0)
            {
                m.Frame = p.FlyFixed;
                m.FrameCounter = 0;
            }
            else LoopPet(m, p.FlyStart, p.FlyWrap, p.FlyTo, p.FlyTicks);
            m.Rot = p.FlyTurn == PetTurn.VelX ? m.Vel.x * p.FlyTurnK
                : p.FlyTurn == PetTurn.Along && m.Vel.sqrMagnitude > 0.01f ? Mathf.Atan2(m.Vel.y, m.Vel.x) + (m.SpriteDir == 1 ? Mathf.PI : 0f) : 0f;
        }

        /// <summary>
        /// AI_026 for a pet: walking after the player until it is more than `StopDist` across (P:59993-59999), hopping a wall
        /// ahead (P:63196-63260); taking to the air when over FlyAt away or 300 px up or down (P:61256-61316), flying back
        /// through anything (P:61386-61615) and landing within LandAt once the player stands (P:61490-61497); past 2000 px it
        /// is at the player.
        /// </summary>
        void PetWalker(M m, PetProj p)
        {
            var d = pCenter - m.Center;
            float dist = d.magnitude;
            if (dist > 2000f)
            {
                m.Center = pCenter;
                m.Vel = Vector2.zero;
            }
            else if (m.Ai0 == 0f && (dist > p.FlyAt || Mathf.Abs(d.y) > 300f))
            {
                if (d.y > 0f && m.Vel.y < 0f) m.Vel.y = 0f;
                if (d.y < 0f && m.Vel.y > 0f) m.Vel.y = 0f;
                m.Ai0 = 1f;
            }
            if (m.Ai0 == 1f)
            {
                m.TileCollide = false;
                if (dist < p.LandAt && ownerGrounded && m.Bottom <= pBox.yMax && !SolidBox(m.Box))
                {
                    m.Ai0 = 0f;
                    if (m.Vel.y < -6f) m.Vel.y = -6f;
                }
                float speed = p.FlySpeed, accel = p.FlyAccel;
                Vector2 want = dist < 60f ? m.Vel : d * (speed / Mathf.Max(dist, 0.001f));
                m.Vel.x = Steer(m.Vel.x, want.x, accel);
                m.Vel.y = Steer(m.Vel.y, want.y, accel);
                FacePet(m, p, m.Vel.x, 0.5f);
                PetFlyFrames(m, p);
                return;
            }

            m.TileCollide = true;
            m.Rot = 0f;
            bool left = pCenter.x < m.Center.x - p.StopDist, right = pCenter.x > m.Center.x + p.StopDist;
            float acc = p.WalkAccel, max = p.WalkSpeed;
            if (left) m.Vel.x -= m.Vel.x > -3.5f ? acc : acc * 0.25f;
            else if (right) m.Vel.x += m.Vel.x < 3.5f ? acc : acc * 0.25f;
            else
            {
                m.Vel.x *= 0.9f;
                if (Mathf.Abs(m.Vel.x) <= acc) m.Vel.x = 0f;
            }
            bool wallAhead = false;
            int wi = TileOf(m.Center.x) + (left ? -1 : 0) + (right ? 1 : 0) + (int)m.Vel.x, wj = TileOf(m.Center.y);
            if (left || right) wallAhead = SolidTile(wi, wj);
            // Lower than the player's feet by 8 px: it drops through platforms after him.
            m.FallThrough = pBox.yMax - 8f > m.Bottom;
            StepUp(m, m.FallThrough);
            if (m.Vel.y == 0f && wallAhead)
            {
                int fi = TileOf(m.Center.x), fj = TileOf(m.Bottom);
                if (SolidTile(fi, fj) || PlatformTile(fi, fj)) m.Vel.y = WallJump(wi, wj);
            }
            m.Vel.x = Mathf.Clamp(m.Vel.x, -max, max);
            if (m.Vel.x < 0f) m.Dir = -1;
            if (m.Vel.x > 0f) m.Dir = 1;
            if (m.Vel.x > acc && right) m.Dir = 1;
            if (m.Vel.x < -acc && left) m.Dir = -1;
            m.SpriteDir = p.FacesRight ? m.Dir : -m.Dir;
            bool still = m.Pos.x - m.OldPos.x == 0f;
            if (m.Vel.y == 0f)
            {
                if (still || Mathf.Abs(m.Vel.x) <= 0.8f)
                {
                    m.Frame = p.Idle;
                    m.FrameCounter = 0;
                }
                else
                {
                    if (m.Frame < p.WalkStart || m.Frame >= p.WalkWrap) m.Frame = p.WalkTo;
                    m.FrameCounter += (p.WalkBySpeed ? (int)Mathf.Abs(m.Vel.x) : 0) + 1;
                    if (m.FrameCounter > p.WalkCounter)
                    {
                        m.Frame++;
                        m.FrameCounter = 0;
                    }
                    if (m.Frame >= p.WalkWrap) m.Frame = p.WalkTo;
                }
            }
            else if (m.Vel.y < 0f)
            {
                m.FrameCounter = 0;
                if (p.Up >= 0) m.Frame = p.Up;
            }
            else
            {
                m.FrameCounter = 0;
                if (p.Down >= 0) m.Frame = p.Down;
            }
            m.Vel.y = Mathf.Min(m.Vel.y + 0.4f, 10f);
        }

        /// <summary>AI_026's flying pull on one axis: toward the wanted speed, harder when turning back (P:61574-61615).</summary>
        static float Steer(float v, float want, float accel)
        {
            if (v < want)
            {
                v += accel;
                if (v < 0f) v += accel * 1.5f;
            }
            if (v > want)
            {
                v -= accel;
                if (v > 0f) v -= accel * 1.5f;
            }
            return v;
        }

        /// <summary>
        /// aiStyle 11 (P:24651-24760): the Shadow Orb turning slowly, 3 px a tick toward the player until 70 px off (4 above the
        /// head or below the feet while up or down is held); the fairies at 6 px until 50 off, never overshooting, tilting with
        /// their speed, four frames of 4 ticks. Past 800 px it is at the player.
        /// </summary>
        void PetOrb(M m, PetProj p)
        {
            bool fairy = p.Frames >= 4;
            if (fairy)
            {
                FacePet(m, p, m.Vel.x, 0f);
                m.Rot = m.Vel.x * 0.1f;
                if (++m.FrameCounter >= 4)
                {
                    m.Frame++;
                    m.FrameCounter = 0;
                }
                if (m.Frame >= 4) m.Frame = 0;
            }
            else m.Rot += 0.02f;
            float speed = fairy ? 6f : 3f;
            var d = pCenter - m.Center;
            int stop = fairy ? 50 : 70;
            var hero = HeroController.SilentInstance;
            if (!fairy && hero != null && (hero.cState.lookingUp || hero.cState.lookingDown))
            {
                d.y = hero.cState.lookingUp ? pBox.yMin - 40f - m.Center.y : pBox.yMax + 40f - m.Center.y;
                d.x -= 6f;
                stop = 4;
            }
            float dist = d.magnitude;
            if (dist > 800f)
            {
                m.Center = pCenter;
                return;
            }
            if (dist > stop)
            {
                float left = dist - stop;
                m.Vel = d * (speed / dist);
                if (fairy && m.Vel.magnitude > left) m.Vel = m.Vel.normalized * left;
            }
            else m.Vel = Vector2.zero;
        }

        /// <summary>A flying pet (AI_026's flyers): to its place by the player (behind and above), easing in, faster when the player is.</summary>
        void PetFlyer(M m, PetProj p)
        {
            var goal = pCenter + new Vector2(p.Anchor.x * pDir, p.Anchor.y);
            var d = goal - m.Center;
            float dist = d.magnitude;
            if (dist > 2000f)
            {
                m.Center = goal;
                m.Vel = Vector2.zero;
            }
            float speed = Mathf.Max(p.Speed, Mathf.Abs(pVel.x) + Mathf.Abs(pVel.y));
            if (dist < p.Slack) m.Vel *= 0.9f;
            else
            {
                var want = d * (Mathf.Min(speed, dist * p.Pull * 2f) / dist);
                m.Vel.x = Steer(m.Vel.x, want.x, p.FlyAccel);
                m.Vel.y = Steer(m.Vel.y, want.y, p.FlyAccel);
            }
            if (p.FacesPlayer) m.SpriteDir = p.FacesRight ? pDir : -pDir;
            else FacePet(m, p, m.Vel.x, 0.5f);
            PetFlyFrames(m, p);
        }

        /// <summary>Held by the player's side (aiStyles 124, 144, 90): a tenth of the way to its place a tick, gently bobbing; past 1000 px it is there.</summary>
        void PetHeld(M m, PetProj p)
        {
            m.LocalAi0++;
            var goal = pCenter + new Vector2(p.Anchor.x * pDir, p.Anchor.y + Mathf.Cos(m.LocalAi0 * Mathf.PI / 60f) * 2f);
            var d = goal - m.Center;
            if (d.magnitude > 1000f) m.Center = goal;
            m.Vel = d * p.Pull;
            if (d.magnitude < 4f) m.Vel *= 0.25f;
            if (p.FacesPlayer || Mathf.Abs(m.Vel.x) < 0.5f) m.SpriteDir = p.FacesRight ? pDir : -pDir;
            else FacePet(m, p, m.Vel.x, 0.5f);
            PetFlyFrames(m, p);
        }

        /// <summary>AI_174's worm: as fast as the player (4 to 12), turning at most 5° a tick toward him once 120 px off, drifting up; along its path.</summary>
        void PetWorm(M m, PetProj p)
        {
            var d = pCenter - m.Center;
            float dist = d.magnitude;
            if (dist > 2000f) m.Center = pCenter;
            float speed = Mathf.Clamp(pVel.magnitude, 4f, 12f);
            var v = m.Vel.sqrMagnitude > 0.0001f ? m.Vel.normalized : Vector2.up * -1f;
            if (dist > 120f)
            {
                float turn = Vector2.SignedAngle(v, d);
                v = Quaternion.Euler(0f, 0f, Mathf.Clamp(turn, -5f, 5f)) * v;
            }
            m.Vel = v * speed;
            if (Mathf.Abs(m.Vel.y) < 1f) m.Vel.y -= 0.1f;
            m.Rot = Mathf.Atan2(m.Vel.y, m.Vel.x) + Mathf.PI / 2f;
            if (p.Segments <= 1) LoopPet(m, p.FlyStart, p.FlyWrap, p.FlyTo, p.FlyTicks);
            if (!wormTrails.TryGetValue(m, out var trail)) wormTrails[m] = trail = new List<Vector2>();
            trail.Insert(0, m.Center);
            if (trail.Count > 200) trail.RemoveAt(trail.Count - 1);
        }

        /// <summary>A worm pet's path, newest first (Projectile.oldPos), for its segments.</summary>
        readonly Dictionary<M, List<Vector2>> wormTrails = new Dictionary<M, List<Vector2>>();

        /// <summary>
        /// The pets as Main.DrawProjDirect draws them: the general branch (DrawGeneric's offsets), the centred one (the frame's
        /// middle on the hitbox's), a single-frame sheet whole, a worm's segments along its path (DrawMultisegmentPet: the head
        /// frame 0, the body 1, the tail the last).
        /// </summary>
        void DrawPets(FxBatch fx)
        {
            foreach (var m in pets)
            {
                if (m == null) continue;
                var p = m.Pet;
                string name = "Projectile_" + p.Id;
                var tex = TerrariaAssets.Texture(name);
                if (tex == null) continue;
                int frames = p.Whole ? 1 : Mathf.Max(1, p.Frames);
                int texW = tex.width, frameH = tex.height / frames;
                var c = p.Light.r + p.Light.g + p.Light.b > 0.01f ? Color.white : Lit;
                c.a = 1f - m.Alpha;
                if (p.Move == PetMove.Worm && p.Segments > 1)
                {
                    DrawWorm(fx, m, p, name, texW, frameH, frames, c);
                    continue;
                }
                int frame = Mathf.Clamp(m.Frame, 0, frames - 1);
                Vector2 pos;
                Sprite spr;
                if (p.Centered)
                {
                    pos = m.Center + new Vector2(0f, m.GfxOffY);
                    spr = FxBatch.Frame(name, 0, frameH * frame, texW, frameH, texW / 2f, frameH / 2f + p.OriginY);
                }
                else
                {
                    pos = new Vector2(m.Pos.x + texW / 2 + p.OffX, m.Pos.y + m.H / 2 + m.GfxOffY);
                    spr = FxBatch.Frame(name, 0, frameH * frame, texW, p.Whole ? frameH : frameH - 1, texW / 2 + p.OriginX, m.H / 2 + p.OriginY);
                }
                fx.Draw(spr, pos, c, m.Rot, m.Scale, flipX: m.SpriteDir == -1);
            }
        }

        void DrawWorm(FxBatch fx, M m, PetProj p, string name, int texW, int frameH, int frames, Color c)
        {
            if (!wormTrails.TryGetValue(m, out var trail)) return;
            // Tail first, the head over the rest.
            for (int i = p.Segments - 1; i >= 0; i--)
            {
                var at = PointAlong(trail, i * p.Spacing, out float rot);
                int frame = i == 0 ? 0 : i == p.Segments - 1 ? frames - 1 : Mathf.Min(1, frames - 1);
                var spr = FxBatch.Frame(name, 0, frameH * frame, texW, frameH, texW / 2f, frameH / 2f);
                fx.Draw(spr, at, c, i == 0 ? m.Rot : rot + Mathf.PI / 2f, m.Scale);
            }
        }

        /// <summary>The point a distance back along a path (newest first) and the way the path runs there.</summary>
        static Vector2 PointAlong(List<Vector2> path, float dist, out float rot)
        {
            rot = 0f;
            if (path.Count == 0) return Vector2.zero;
            for (int i = 1; i < path.Count; i++)
            {
                float seg = Vector2.Distance(path[i - 1], path[i]);
                if (seg >= dist && seg > 0f)
                {
                    var d = path[i - 1] - path[i];
                    rot = Mathf.Atan2(d.y, d.x);
                    return Vector2.Lerp(path[i - 1], path[i], dist / seg);
                }
                dist -= seg;
            }
            return path[path.Count - 1];
        }
    }
}
