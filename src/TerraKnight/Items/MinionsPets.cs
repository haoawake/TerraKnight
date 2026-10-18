using System.Collections.Generic;
using TerraKnight.Effects;
using TerraKnight.Terraria;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// The pets out (Items/Pets.cs): a vanity pet and a light pet (the Resplendent Dessert's two slime royals as one), moved
    /// and drawn beside the minions but no minions themselves (no slots, no hits), each by its own Terraria AI: AI_026's
    /// walkers and flyers (MinionsPets026.cs), aiStyles 11, 67, 90, 124, 144 and 174 (MinionsPetsAI.cs); Hallownest's own
    /// pets held by the player's side.
    /// </summary>
    internal sealed partial class Minions
    {
        static readonly KindSpec petSpec = new KindSpec { Minion = false, Tracked = false };
        /// <summary>The pet out of each kind (vanity, light) and the item it came from.</summary>
        readonly M[] pets = new M[2];
        /// <summary>A second pet of that kind: the Resplendent Dessert's Slime Princess beside the Slime Prince (Pl:11284-11288).</summary>
        readonly M[] petTwins = new M[2];
        readonly ItemType[] petItems = { ItemType.None, ItemType.None };

        /// <summary>Dev: the pet out of a kind, its projectile id and where it is (px), or null.</summary>
        public static (int id, Vector2 at, int frame, bool flying)? DevPet(bool light)
        {
            var m = Instance?.pets[light ? 1 : 0];
            if (m == null) return null;
            return (m.Pet.Id, m.Center, m.Frame, m.Ai0 == 1f);
        }

        /// <summary>Dev: a pet's motion (the first of its kind, or the second with `twin`): velocity (px a tick, y down), ai[0], rotation, sprite direction.</summary>
        public static (int id, Vector2 vel, float ai0, float rot, int spriteDir, Vector2 at)? DevPetMotion(bool light, bool twin = false)
        {
            var i = Instance;
            var m = i == null ? null : twin ? i.petTwins[light ? 1 : 0] : i.pets[light ? 1 : 0];
            if (m == null) return null;
            return (m.Pet.Id, m.Vel, m.Ai0, m.Rot, m.SpriteDir, m.Center);
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
                    if (petTwins[k] != null) wormTrails.Remove(petTwins[k]);
                    pets[k] = null;
                    petTwins[k] = null;
                    var def = Pets.Of(t);
                    // Player.BuffHandle_SpawnPetIfNeeded (Pl:12321-12345): the Resplendent Dessert calls both royals, the
                    // Prince 10 px behind the player and the Princess 10 ahead; the Fairy Bell one of its three fairies.
                    if (def != null && def.Buff == 341 && def.Projs.Length > 1)
                    {
                        pets[k] = NewPet(def.Projs[0], -10f * pDir);
                        petTwins[k] = NewPet(def.Projs[1], 10f * pDir);
                    }
                    else if (def != null) pets[k] = NewPet(def.Projs[Random.Range(0, def.Projs.Length)], 0f);
                }
                if (pets[k] != null) StepPet(pets[k]);
                if (petTwins[k] != null) StepPet(petTwins[k]);
            }
            ChesterTick();
        }

        /// <summary>The Terraria AI a pet's projectile runs (its SetDefaults aiStyle); -1 for Hallownest's own.</summary>
        static int PetAi(PetProj p)
        {
            switch (p.Id)
            {
                case 0: return -1;
                case 18: case 72: case 86: case 87: return 11;
                case 500: case 653: case 1018: case 1056: case 1090: return 67;
                case 492: return 90;
                case 650: case 882: case 888: case 895: case 896: case 898: case 957: return 124;
                case 701: case 702: case 703: case 764: case 1050: return 144;
                case 883: case 887: case 893: return 174;
                default: return 26;
            }
        }

        /// <summary>Projectile.NewProjectile at the player's middle (dx across), still, frame 0; blocks stop it unless its SetDefaults says not.</summary>
        M NewPet(PetProj p, float dx)
        {
            var m = new M { S = petSpec, Pet = p, W = p.W, H = p.H, Scale = p.Scale, Alpha = p.Alpha / 255f, TimeLeft = int.MaxValue };
            m.Center = pCenter + new Vector2(dx, 0f);
            m.OldPos = m.Pos;
            int ai = PetAi(p);
            m.TileCollide = ai == 26 || ai == 67 || ai == 90;
            m.Frame = 0;
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
            // P:17588-17596: a pet drops through platforms while the player stands 12 px lower (AI_067's set their own).
            m.FallThrough = pBox.yMax - 12f > m.Bottom;
            switch (PetAi(p))
            {
                case 26: Pet026(m, p); break;
                case 11: PetAI011(m, p); break;
                case 67: PetAI067(m, p); break;
                case 90: PetAI090(m, p); break;
                case 124: PetAI124(m, p); break;
                case 144: PetAI144(m, p); break;
                case 174: PetAI174(m, p); break;
                default: PetHeld(m, p); break;
            }
            m.OldPos = m.Pos;
            if (m.TileCollide) TileCollision(m, m.FallThrough);
            else m.Pos += m.Vel;
            // Projectile.light / Lighting.AddLight each tick (the Cursed Sapling's only while it flies: FlyFrames026).
            if (p.Id != 324 && p.Light.r + p.Light.g + p.Light.b > 0.01f) FxBatch.Instance?.Light(m.Center, p.Light, 5f);
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

        /// <summary>Hallownest's own pets by the player's side (as aiStyle 124): a tenth of the way to their place a tick, gently bobbing; past 1000 px there.</summary>
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

        /// <summary>A worm pet's path, newest first (Projectile.oldPos, as centres), for its segments.</summary>
        readonly Dictionary<M, List<Vector2>> wormTrails = new Dictionary<M, List<Vector2>>();

        /// <summary>
        /// The pets as Main.DrawProjDirect draws them: the general branch (DrawGeneric's offsets; the pig's, Spiffo's and the
        /// Cave Gardener's by their facing, Main.cs:30482-30508), the centred one (the frame's middle on the hitbox's;
        /// Glommer's three columns and its bob, the princess's, lantern's and queen's bob, Main.cs:33756-33878), a single-frame
        /// sheet whole, the worms' segments (DrawMultisegmentPet), the Twins' two eyes (DrawTwinsPet), Skeletron's hands and
        /// Skeletron Prime's arms (Main.cs:34311-34426), the Suspicious Looking Eye's pupil (Main.cs:34599) and the glow masks.
        /// </summary>
        void DrawPets(FxBatch fx)
        {
            for (int k = 0; k < 2; k++)
            {
                DrawPet(fx, pets[k]);
                DrawPet(fx, petTwins[k]);
            }
        }

        /// <summary>Main.GlobalTimeWrappedHourly: seconds, wrapped at an hour.</summary>
        static float GlobalTime => Time.time % 3600f;

        void DrawPet(FxBatch fx, M m)
        {
            if (m == null) return;
            var p = m.Pet;
            // A pet drawn by the game itself (the Grimmchild): nothing of the mod's.
            if (p.Texture == "") return;
            string name = p.Texture ?? "Projectile_" + p.Id;
            var tex = TerrariaAssets.Texture(name);
            if (tex == null) return;
            int frames = p.Whole ? 1 : Mathf.Max(1, p.Frames);
            int cols = p.Id == 957 ? 3 : 1;
            int texW = tex.width / cols, frameH = tex.height / frames;
            float scale = p.DrawHeight > 0f ? m.Scale * p.DrawHeight / Mathf.Max(1, frameH) : m.Scale;
            var c = p.Light.r + p.Light.g + p.Light.b > 0.01f ? Color.white : Lit;
            c.a = 1f - m.Alpha;
            if (PetAi(p) == 174 && p.Segments > 1)
            {
                DrawWorm(fx, m, p, name, texW, frameH, frames, c);
                return;
            }
            if (p.Id == 888)
            {
                // DrawTwinsPet (Main.cs:37725): Retinazer and Spazmatism (18 frames on) going round each other, 18 px out.
                int f = Mathf.Clamp(m.Frame, 0, frames - 19);
                var round = new Vector2(Mathf.Cos(GlobalTime * 2f), Mathf.Sin(GlobalTime * 2f)) * 18f;
                fx.Draw(FxBatch.Frame(name, 0, frameH * f, texW, frameH, texW / 2f, frameH / 2f), m.Center + round, c, m.Rot, scale, flipX: m.SpriteDir == -1);
                fx.Draw(FxBatch.Frame(name, 0, frameH * (f + 18), texW, frameH, texW / 2f, frameH / 2f), m.Center - round, c, m.Rot, scale, flipX: m.SpriteDir == -1);
                return;
            }
            int frame = Mathf.Clamp(m.Frame, 0, frames * cols - 1);
            int sx = frame / frames * texW, sy = frame % frames * frameH;
            Vector2 pos;
            Sprite spr;
            int sh = frameH;
            float ox, oy;
            if (p.Centered)
            {
                pos = m.Center + new Vector2(0f, m.GfxOffY);
                if (p.Id == 957)
                {
                    // Glommer bobs with Main.mouseTextColor once it has rested, and squashes through its bounce.
                    float bob = Mathf.Cos(mouseTextColor / 255f * (Mathf.PI * 2f) * 2f) * 4f * Mathf.Clamp01(m.LocalAi0 / 100f), squash = 0f;
                    if (m.Frame > frames * 2)
                        switch (m.Frame % frames)
                        {
                            case 5: case 11: squash = 2f; break;
                            case 6: case 10: squash = 6f; break;
                            case 7: squash = 8f; break;
                            case 8: case 9: squash = 10f; break;
                        }
                    pos.y += -2f + bob + squash;
                }
                if (p.Id == 895 || p.Id == 896 || p.Id == 898) pos.y += -2f + Mathf.Cos(mouseTextColor / 255f * (Mathf.PI * 2f) * 2f) * 4f;
                ox = texW / 2f;
                oy = frameH / 2f + p.OriginY;
            }
            else
            {
                int offX = p.OffX;
                if (p.Id == 959 && m.SpriteDir == -1) offX -= 8;
                if (p.Id == 1003 && m.SpriteDir == 1) offX += 8;
                if (p.Id == 1004 && m.SpriteDir == 1) offX += 6;
                pos = new Vector2(m.Pos.x + texW / 2 + offX, m.Pos.y + m.H / 2 + m.GfxOffY);
                if (!p.Whole) sh = frameH - 1;
                ox = texW / 2 + p.OriginX;
                oy = m.H / 2 + p.OriginY;
            }
            spr = FxBatch.Frame(name, sx, sy, texW, sh, ox, oy);
            fx.Draw(spr, pos, c, m.Rot, scale, flipX: m.SpriteDir == -1);
            // The glow masks over the frame: Everscream's and the Martian's (279, 281) white, the Toy Golem's (277) pulsing,
            // the Jack 'O Lantern's (278) flickering, Skeletron Prime's (276).
            string glow = p.Id == 897 ? "Glow_279" : p.Id == 899 ? "Glow_281" : p.Id == 891 ? "Glow_277" : p.Id == 896 ? "Glow_278" : p.Id == 889 ? "Glow_276" : null;
            if (glow != null && TerrariaAssets.Texture(glow) != null)
            {
                var gs = FxBatch.Frame(glow, sx, sy, texW, sh, ox, oy);
                if (p.Id == 891)
                {
                    // Utils.WrappedLerp(0.6, 1) over Main.timeForVisualEffects % 100 (a tick each).
                    float t = discoTick % 100 / 100f;
                    float v = t <= 0.5f ? Mathf.Lerp(0.6f, 1f, t * 2f) : Mathf.Lerp(1f, 0.6f, t * 2f - 1f);
                    fx.Draw(gs, pos, new Color(v, v, v, 1f), m.Rot, scale, flipX: m.SpriteDir == -1);
                }
                else if (p.Id == 896)
                {
                    for (int k = 0; k < 2; k++)
                        fx.Draw(gs, pos + new Vector2(Random.Range(-10, 11) * 0.1f, Random.Range(-10, 11) * 0.1f), new Color(150 / 255f, 150 / 255f, 150 / 255f, 100 / 255f), m.Rot, scale,
                            flipX: m.SpriteDir == -1);
                    fx.Draw(gs, pos, Color.white, m.Rot, scale, flipX: m.SpriteDir == -1);
                }
                else fx.Draw(gs, pos, Color.white, m.Rot, scale, flipX: m.SpriteDir == -1);
            }
            if (p.Id == 885) DrawSkeletronHands(fx, m, name, texW, frameH, c, scale);
            if (p.Id == 889) DrawPrimeArms(fx, m, name, texW, frameH, c, scale);
            if (p.Id == 650) DrawEyePupil(fx, m, c, pos);
        }

        /// <summary>Main.cs:34391-34426: Skeletron's two hands (frames 7-9 by localAI[0], 7 while it spins) 30 px either side, swaying.</summary>
        void DrawSkeletronHands(FxBatch fx, M m, string name, int texW, int frameH, Color c, float scale)
        {
            var center = m.Center + new Vector2(0f, m.GfxOffY);
            for (int k = 0; k < 2; k++)
            {
                int pose = (int)m.LocalAi0;
                if (m.Frame == 6) pose = 0;
                else if (k == 1) pose = 2 - pose;
                pose += 7;
                var at = center + new Vector2(k == 1 ? 30f : -30f, 0f);
                var sway = new Vector2(Mathf.Cos(GlobalTime * 2f), Mathf.Sin(GlobalTime * 2f)) * 4f + m.Vel * -1.5f;
                var drift = new Vector2(Mathf.Cos(GlobalTime), Mathf.Sin(GlobalTime)) * 3f;
                at = k == 1 ? at + sway + drift : at - sway;
                fx.Draw(FxBatch.Frame(name, 0, frameH * pose, texW, frameH, texW / 2f, frameH / 2f), at, c, 0f, scale, flipX: k == 1);
            }
        }

        /// <summary>
        /// Main.cs:34311-34389: Skeletron Prime's four arms (frames 7-10, the saw's glow on 8) held round the head, swinging,
        /// and drawn out to circle it (28 px) as localAI[0] reaches 100 while it flies.
        /// </summary>
        void DrawPrimeArms(FxBatch fx, M m, string name, int texW, int frameH, Color c, float scale)
        {
            var center = m.Center + new Vector2(0f, m.GfxOffY);
            bool glow = TerrariaAssets.Texture("Glow_276") != null;
            for (int k = 0; k < 4; k++)
            {
                const int r = 28;
                int frame = 7 + k;
                const float full = 100f;
                float spin = GlobalTime * 2f + k * (Mathf.PI / 2f);
                spin *= 3f;
                var orbit = center + new Vector2(Mathf.Cos(spin), Mathf.Sin(spin)) * r;
                bool flip = false;
                Vector2 at;
                float rot;
                if (m.LocalAi0 == full)
                {
                    at = orbit;
                    rot = spin;
                }
                else
                {
                    var rest = new Vector2(r, -16f) + m.Vel * 1.5f;
                    float wave = 4f, tilt = -0.35f;
                    switch (k)
                    {
                        case 1:
                            rest.x *= -1f;
                            flip = true;
                            tilt = 0.35f;
                            wave = -3f;
                            break;
                        case 2:
                            rest.y = 16f;
                            tilt = 0.35f;
                            wave = 2f;
                            break;
                        case 3:
                            rest.x *= -1f;
                            rest.y = 16f;
                            flip = true;
                            tilt = -0.35f;
                            wave = -1f;
                            break;
                    }
                    rest += new Vector2(Mathf.Cos(GlobalTime * wave), Mathf.Sin(GlobalTime * wave)) * 4f;
                    float t = m.LocalAi0 / full;
                    at = Vector2.Lerp(center + rest, orbit, t);
                    rot = t > 0.5f ? spin : tilt;
                }
                fx.Draw(FxBatch.Frame(name, 0, frameH * frame, texW, frameH, texW / 2f, frameH / 2f), at, c, rot, scale, flipX: flip);
                if (frame == 8 && glow) fx.Draw(FxBatch.Frame("Glow_276", 0, frameH * frame, texW, frameH, texW / 2f, frameH / 2f), at, Color.white, rot, scale, flipX: flip);
            }
        }

        /// <summary>Main.cs:34599-34618: the eye's pupil (Extra_66) out along localAI[0]'s angle by localAI[1], sized by it.</summary>
        void DrawEyePupil(FxBatch fx, M m, Color c, Vector2 eye)
        {
            var tex = TerrariaAssets.Texture("Extra_66");
            if (tex == null) return;
            int what = (int)(m.LocalAi0 / (Mathf.PI * 2f));
            float angle = m.LocalAi0 % (Mathf.PI * 2f) - Mathf.PI;
            float reach = (float)System.Math.IEEERemainder(m.LocalAi1, 1.0);
            if (reach < 0f) reach++;
            int steps = (int)Mathf.Floor(m.LocalAi1);
            float size = 1f + steps * 0.02f, far = what == 1 ? 7f : 5f;
            var off = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * reach * far * m.Scale;
            fx.Draw(FxBatch.Frame("Extra_66", 0, 0, tex.width, tex.height, tex.width / 2f, tex.height / 2f), eye + off, c, m.Rot, size);
        }

        /// <summary>
        /// DrawMultisegmentPet (Main.cs:37745-37808): each segment a fixed step (16 px; the Phantasmal dragon's 20) from the one
        /// before, toward where the head was 10 ticks per segment ago; the bodies frame 1 (the dragon's 2 but for its 2nd and
        /// 5th), the tail the last; the head over them; the Destroyer's glow mask (275) on all.
        /// </summary>
        void DrawWorm(FxBatch fx, M m, PetProj p, string name, int texW, int frameH, int frames, Color c)
        {
            if (!wormTrails.TryGetValue(m, out var trail) || trail.Count == 0) return;
            string glow = p.Id == 887 && TerrariaAssets.Texture("Glow_275") != null ? "Glow_275" : null;
            var gc = Color.white * (mouseTextColor / 255f);
            float step = p.Spacing;
            var at = m.Center;
            for (int i = 1; i < p.Segments; i++)
            {
                int frameY = 1;
                if (i == p.Segments - 1) frameY = frames - 1;
                else if (p.Id == 893 && i != 2 && i != 5) frameY = 2;
                var old = trail[Mathf.Min(i * 10, trail.Count - 1)];
                float a = Mathf.Atan2(at.y - old.y, at.x - old.x);
                var seg = at - new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * step;
                float rot = Mathf.Atan2(at.y - seg.y, at.x - seg.x) + Mathf.PI / 2f;
                bool flip = !(seg.x < at.x);
                at = seg;
                fx.Draw(FxBatch.Frame(name, 0, frameH * frameY, texW, frameH, texW / 2f, frameH / 2f), seg, c, rot, m.Scale, flipX: flip);
                if (glow != null) fx.Draw(FxBatch.Frame(glow, 0, frameH * frameY, texW, frameH, texW / 2f, frameH / 2f), seg, gc, rot, m.Scale, flipX: flip);
            }
            fx.Draw(FxBatch.Frame(name, 0, 0, texW, frameH, texW / 2f, frameH / 2f), m.Center, c, m.Rot, m.Scale, flipX: m.SpriteDir == -1);
            if (glow != null) fx.Draw(FxBatch.Frame(glow, 0, 0, texW, frameH, texW / 2f, frameH / 2f), m.Center, gc, m.Rot, m.Scale, flipX: m.SpriteDir == -1);
        }
    }
}
