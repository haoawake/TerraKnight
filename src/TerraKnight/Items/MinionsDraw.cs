using TerraKnight.Effects;
using TerraKnight.Terraria;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// The minions drawn as Main.DrawProjDirect draws them (spec-minions-ground §7, spec-minions-
    /// flying §§4-10): Terraria px, y down, clockwise radians straight into <see cref="FxBatch"/>,
    /// lit by the scene's constant light. The dragon goes behind the rest, the finches over the
    /// player; then the reticle on the minion target while a summon item is held.
    /// </summary>
    internal sealed partial class Minions
    {
        /// <summary>Main.mouseTextColor: a 190↔255 triangle wave, a step a tick (Abigail's bob).</summary>
        static int mouseTextColor = 255, mouseTextStep = -1;

        static Color Lit => new Color(L, L, L, 1f);

        void Draw()
        {
            var fx = FxBatch.Instance;
            if (fx == null) return;
            if (pets[0] != null || pets[1] != null)
            {
                RefreshOwner();
                if (hasOwner) DrawPets(fx);
            }
            if (live.Count > 0 || Target != null)
            {
                RefreshOwner();
                if (hasOwner)
                {
                    DrawMinions(fx, false);
                    DrawReticle(fx);
                }
            }
            DrawGhosts(fx);
        }

        /// <summary>
        /// The minions of the list being drawn (the player's own, or another player's ghosts: no petting for those). Those that
        /// pass through walls (TileCollide off; the souls and the Stardust Dragon always) over the ground, as Terraria draws
        /// its projectiles over its tiles, so one inside a wall still shows (the user's, 2026-10-20).
        /// </summary>
        void DrawMinions(FxBatch fx, bool ghosts)
        {
            bool previous = fx.WeaponEffect;
            fx.WeaponEffect = true;
            try { DrawMinionsCore(fx, ghosts); }
            finally { fx.WeaponEffect = previous; }
        }

        void DrawMinionsCore(FxBatch fx, bool ghosts)
        {
            fx.OnTop = true;
            try { DrawDragon(fx); }
            finally { fx.OnTop = false; }
            if (!ghosts) DrawPetting(fx);
            try
            {
            foreach (var m in live)
            {
                if (m.Dead) continue;
                fx.OnTop = !m.TileCollide || m.Kind == Kind.WanderingSoul || m.Kind == Kind.ChoirSoul || m.Kind == Kind.ShadeSoul || m.Kind == Kind.DirgeSoul;
                switch (m.Kind)
                {
                    case Kind.BabySlime: DrawGeneric(fx, m, 6, -13); break;
                    case Kind.Pygmy1:
                    case Kind.Pygmy2:
                    case Kind.Pygmy3:
                    case Kind.Pygmy4: DrawGeneric(fx, m, 26, m.Dir == 1 ? -10 : -22); break;
                    case Kind.VenomSpider:
                    case Kind.JumperSpider: DrawGeneric(fx, m, 0, 4 * m.Dir); break;
                    case Kind.DangerousSpider: DrawGeneric(fx, m, -2, 4 * m.Dir); break;
                    case Kind.OneEyedPirate:
                    case Kind.SoulscourgePirate:
                    case Kind.PirateCaptain: DrawPirate(fx, m); break;
                    case Kind.VampireFrog:
                        fx.Draw(FxBatch.Frame(m.S.Texture, 0, 54 * m.Frame, 136, 54, 68f, 50f), m.Center + new Vector2(0f, m.GfxOffY + m.H / 2), Lit, m.Rot, 1f, flipX: m.SpriteDir == -1);
                        break;
                    case Kind.Flinx:
                        fx.Draw(FxBatch.Frame(m.S.Texture, 0, 42 * m.Frame, 42, 42, 21f, 21f), m.Center + new Vector2(0f, m.GfxOffY + m.H / 2 - 21 + 4), Lit, m.Rot, 1f, flipX: m.SpriteDir == -1);
                        break;
                    case Kind.Hornet: DrawGeneric(fx, m, 6, -10); break;
                    case Kind.Imp: DrawGeneric(fx, m, 12, -11); break;
                    case Kind.Abigail: DrawAbigail(fx, m); break;
                    case Kind.Retanimini:
                    {
                        var at = m.Pos + new Vector2(25f, 10f + m.GfxOffY);
                        fx.Draw(FxBatch.Frame(m.S.Texture, 0, 24 * m.Frame, 50, 23, 25f, 10f), at, Lit, m.Rot, 1f);
                        fx.Draw(FxBatch.Frame("Eye_Laser_Small", 0, 24 * m.Frame, 50, 24, 25f, 10f), at, new Color(1f, 1f, 1f, 0f), m.Rot, 1f);
                        break;
                    }
                    case Kind.Spazmamini:
                    {
                        var spr = FxBatch.Frame(m.S.Texture, 0, 24 * m.Frame, 38, 24, 19f, 10f);
                        int n = m.Ai0 == 2f ? 10 : 3;
                        for (int k = 1; k < n; k++)
                            fx.Draw(spr, m.OldPosRing[k] + new Vector2(19f, 10f + m.GfxOffY), Lit * ((n - k) / 15f), m.Rot, 1f);
                        fx.Draw(spr, m.Pos + new Vector2(19f, 10f + m.GfxOffY), Lit, m.Rot, 1f);
                        break;
                    }
                    case Kind.Bat: DrawBat(fx, m); break;
                    case Kind.Finch:
                    {
                        // A perched finch sits exactly on the head as it is drawn (SortBabyBirdProjectiles).
                        var c = m.Center;
                        if (m.Frame == 4 && m.Vel == Vector2.zero) c = FinchHome(PerchIndex(m));
                        fx.Draw(FxBatch.Frame(m.S.Texture, 0, 24 * m.Frame, 30, 24, 14f, 22f), c + new Vector2(0f, m.GfxOffY), Lit, m.Rot, 1f, flipX: m.SpriteDir == -1, overPlayer: true);
                        break;
                    }
                    case Kind.Snowflake: DrawSnowflake(fx, m); break;
                    case Kind.DeadlySphere: DrawDeadlySphere(fx, m); break;
                    case Kind.Sharknado:
                    case Kind.UFO:
                    case Kind.StardustCell: DrawAI062b(fx, m); break;
                    case Kind.Terraprisma: DrawTerraprisma(fx, m); break;
                    case Kind.BladeStaff: DrawBladeStaff(fx, m); break;
                    case Kind.Flameburst1:
                    case Kind.Flameburst2:
                    case Kind.Flameburst3: DrawFlameburst(fx, m); break;
                    case Kind.Ballista1:
                    case Kind.Ballista2:
                    case Kind.Ballista3: DrawBallista(fx, m); break;
                    case Kind.Trap1:
                    case Kind.Trap2:
                    case Kind.Trap3: DrawTrap(fx, m); break;
                    case Kind.TrapBlast1:
                    case Kind.TrapBlast2:
                    case Kind.TrapBlast3: DrawTrapBlast(fx, m); break;
                    case Kind.Aura1:
                    case Kind.Aura2:
                    case Kind.Aura3: DrawAura(fx, m); break;
                    case Kind.SpiderHiver:
                    case Kind.FrostHydra:
                    case Kind.Houndius: DrawAI053(fx, m); break;
                    case Kind.SpiderEgg: DrawSpiderEgg(fx, m); break;
                    case Kind.BabySpider: DrawBabySpider(fx, m); break;
                    case Kind.LunarPortal: DrawLunarPortal(fx, m); break;
                    case Kind.PortalLaser: DrawPortalLaser(fx, m); break;
                    case Kind.RainbowCrystal: DrawRainbowCrystal(fx, m); break;
                    case Kind.CrystalBlast: DrawCrystalBlast(fx, m); break;
                    case Kind.Barnacle: DrawBarnacle(fx, m); break;
                    case Kind.MushroomBoi:
                    case Kind.Cattiva:
                    case Kind.TrustyCattiva:
                    case Kind.ClayPot: DrawPal067(fx, m); break;
                    case Kind.Foxsparks:
                    case Kind.TrustyFoxsparks: DrawFox(fx, m); break;
                    case Kind.ForbiddenRelic: DrawForbidden(fx, m); break;
                    case Kind.WanderingSoul: DrawSoul(fx, m); break;
                    case Kind.ChoirSoul: DrawChoir(fx, m); break;
                    case Kind.ShadeSoul: DrawShade(fx, m); break;
                    case Kind.DirgeSoul: DrawDirge(fx, m); break;
                    case Kind.TigerGem:
                    case Kind.Tiger1:
                    case Kind.Tiger2:
                    case Kind.Tiger3: DrawTiger(fx, m); break;
                    case Kind.Raven:
                        fx.Draw(FxBatch.Frame("Projectile_317", 0, 32 * m.Frame, 28, 32, 14f, 16f), m.Center + new Vector2(0f, m.GfxOffY), Lit, m.Rot, 1f, flipX: m.SpriteDir == -1);
                        break;
                    case Kind.CobSpider:
                        // M:34836: the pirate-style frame draw, origin 4 px up the 14 px frame.
                        fx.Draw(FxBatch.Frame(m.S.Texture, 0, 14 * m.Frame, 24, 14, 12f, 3f), m.Center + new Vector2(0f, m.GfxOffY - 2f), Lit, 0f, 1f, flipX: m.SpriteDir == -1);
                        break;
                }
                // Soul Eater's minion mode: a halo round each of the player's own.
                if (!ghosts && CharmEffects.EaterMinionHalo) CharmEffects.EaterHalo(fx, m.Center + new Vector2(0f, m.GfxOffY), Mathf.Max(m.W, m.H), m.GetHashCode());
            }
            }
            finally { fx.OnTop = false; }
        }

        /// <summary>The generic multi-frame branch: the frame's last row skipped, offsets num151 (origin y) and num152 (x).</summary>
        static void DrawGeneric(FxBatch fx, M m, int num151, int num152)
        {
            var tex = TerrariaAssets.Texture(m.S.Texture);
            int texW = tex.width, frameH = tex.height / m.S.Frames;
            var pos = new Vector2(m.Pos.x + texW / 2 + num152, m.Pos.y + m.H / 2 + m.GfxOffY);
            var spr = FxBatch.Frame(m.S.Texture, 0, frameH * m.Frame, texW, frameH - 1, texW / 2, m.H / 2 + num151);
            fx.Draw(spr, pos, Lit, m.Rot, m.Scale, flipX: m.SpriteDir == -1);
        }

        static void DrawPirate(FxBatch fx, M m)
        {
            // The grappling hook's chain, two links further each tick of the throw.
            if (m.Ai0 == 5f && m.Ai1 != 0f)
            {
                var seg = m.Vel * m.LocalAi0;
                float ang = Mathf.Atan2(seg.y, seg.x) - 1.57f;
                var end = m.Center + seg;
                var c = m.Center;
                int n = Mathf.Min((int)(m.Ai1 * 2f) + 3, (int)(seg.magnitude / 12f)) - 3;
                var step = seg.sqrMagnitude > 0f ? seg.normalized * 12f : Vector2.zero;
                var chain = FxBatch.Frame("Chain", 0, 0, 12, 12, 6f, 6f);
                while (n-- > 0 && (end - c).magnitude >= 25f)
                {
                    c += step;
                    fx.Draw(chain, c, Lit, ang, 1f);
                }
                fx.Draw(FxBatch.Frame("Projectile_13", 0, 0, 18, 18, 9f, 9f), c, Lit, ang + Mathf.PI, 1f);
            }
            fx.Draw(FxBatch.Frame(m.S.Texture, 0, 42 * m.Frame, 52, 42, 26f, 21f), m.Center + new Vector2(0f, m.GfxOffY - 2f), Lit, m.Rot, 1f, flipX: m.SpriteDir == -1);
        }

        /// <summary>Abigail: bobbing, the petal fan while she attacks, the body (a little see-through, reddish attacking) and her flowers' tier.</summary>
        void DrawAbigail(FxBatch fx, M m)
        {
            var pos = m.Center + new Vector2(0f, m.GfxOffY - 2f + Mathf.Cos(mouseTextColor / 255f * Mathf.PI * 4f) * 4f);
            bool attacking = m.Ai0 >= 2f;
            float l = m.LocalAi1;
            if (l >= 0f)
            {
                var baseC = attacking ? FxBatch.C(216, 130, 130) : FxBatch.C(216, 187, 187);
                var c = baseC * ((1f - l) * (1f - l) * 0.8f);
                c.a = 0f;
                float b = m.Vel.magnitude < 0.1f ? (m.Dir == 1 ? 0f : Mathf.PI) : Mathf.Atan2(m.Vel.y, m.Vel.x);
                var up = new Vector2(Mathf.Cos(b + Mathf.PI / 2f), Mathf.Sin(b + Mathf.PI / 2f));
                float t = Time.time;
                var petal = FxBatch.Frame("Projectile_536", 0, 0, 20, 100, 10f, 100f);
                for (int i = 0; i < 9; i++)
                {
                    float s = i % 2 == 1 ? 1f : -1f;
                    float ang = (i + 1) * s * 0.2f * (0.2f + 2f * l) + b + Mathf.PI / 2f;
                    var dirA = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                    float w = (Vector2.Dot(dirA, up) + 1f) / 2f;
                    float k = m.Scale * (0.15f + 0.6f * Mathf.Sin(t + 0.739f * i)) * w;
                    var off = Rotate(new Vector2(Mathf.Cos(m.Rot), Mathf.Sin(m.Rot)), 2f * Mathf.PI * i / 9f + t) * 4f * m.Scale;
                    if (Mathf.Abs(k) > 0.001f) fx.Draw(petal, pos + off, c * w, ang, new Vector2(1.5f * k, k));
                }
            }
            float g = attacking ? 0.7f : 1f;
            fx.Draw(FxBatch.Frame(m.S.Texture, 0, 64 * m.Frame, 44, 64, 22f, 32f), pos, new Color(L * 0.85f, L * 0.85f * g, L * 0.85f * g, 0.85f), m.Rot, m.Scale, flipX: m.SpriteDir == -1);
            int n = Mathf.Max(0, DevCount(Kind.AbigailCounter) - 1);
            fx.Draw(FxBatch.Frame(m.S.Texture, 44 * (1 + n % 3), 64 * m.Frame, 44, 64, 22f, 32f), pos, FlowerTint(n / 3) * L, m.Rot, m.Scale, flipX: m.SpriteDir == -1);
        }

        /// <summary>The flower overlay's colour by tier: red, lilac, then round the hue wheel.</summary>
        static Color FlowerTint(int tier)
        {
            if (tier <= 0) return FxBatch.C(255, 51, 51);
            if (tier == 1) return FxBatch.C(198, 156, 255);
            float h = Mathf.Repeat(1f - 0.13f * tier, 1f);
            var c = Color.HSVToRGB(h, 1f, 1f);
            // hsl(h, 1, 0.6): between the pure hue and white.
            return Color.Lerp(c, Color.white, 0.2f);
        }

        /// <summary>The Sanguine Bat: a short crimson smear behind, a crimson outline, an almost white bat.</summary>
        static void DrawBat(FxBatch fx, M m)
        {
            var spr = FxBatch.Frame(m.S.Texture, 0, 32 * m.Frame, 44, 32, 22f, 16f);
            bool dir = m.SpriteDir == -1;
            var half = new Vector2(m.W, m.H) / 2f;
            for (int k = 5; k >= 1; k--)
            {
                var c = FxBatch.C(220, 20, 60) * ((5 - k) / 15f);
                bool flip = dir ^ (m.OldSpriteDir[k] == -1);
                fx.Draw(spr, m.OldPosRing[k] + half + new Vector2(0f, m.GfxOffY), c, m.OldRot[k], Mathf.Lerp(m.Scale, 2.6f, k / 15f), flipX: flip);
            }
            var glow = FxBatch.C(220, 20, 60, 120);
            foreach (var o in new[] { new Vector2(2f, 0f), new Vector2(0f, 2f), new Vector2(-2f, 0f), new Vector2(0f, -2f) })
                fx.Draw(spr, m.Center + o, glow, 0f, m.Scale, flipX: dir);
            fx.Draw(spr, m.Center + new Vector2(0f, m.GfxOffY), Color.Lerp(Lit, Color.white, 0.75f), m.Rot, m.Scale, flipX: dir);
            if (!SolidTile(TileOf(m.Center.x), TileOf(m.Center.y))) fx.Light(m.Center, new Color(0.863f, 0.078f, 0.235f), 2.5f);
        }

        /// <summary>The Stardust Dragon: bodies from the head back, the tail, then the head on top; full-bright, half added.</summary>
        void DrawDragon(FxBatch fx)
        {
            M head = null;
            foreach (var m in live) if (!m.Dead && m.Kind == Kind.DragonHead) head = m;
            if (head == null) return;
            var order = new System.Collections.Generic.List<M>();
            var cur = Follower(head);
            for (int guard = 0; guard < 200 && cur != null; guard++)
            {
                order.Add(cur);
                cur = Follower(cur);
            }
            // The tail last of the followers (it is already), then the head.
            order.Add(head);
            foreach (var m in order)
            {
                float k = 1f - m.Alpha;
                fx.Draw(FxBatch.Frame(m.S.Texture, 0, 0, 32, 66, 16f, 33f), m.Center + new Vector2(0f, m.GfxOffY), new Color(k, k, k, k * 0.5f), m.Rot, m.Scale, flipX: m.SpriteDir == -1);
            }
        }

        static void DrawSnowflake(FxBatch fx, M m)
        {
            var spr = FxBatch.Whole(m.S.Texture);
            for (int k = 0; k < 4; k++)
                fx.Draw(spr, m.Center - m.Vel * k, FxBatch.C(127, 127, 127, 0) * ((4 - k) / 4f), m.Rot, 1f);
            for (int k = 0; k < 4; k++)
            {
                float a = k * Mathf.PI / 2f + m.Rot;
                fx.Draw(spr, m.Center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 4f, FxBatch.C(127, 127, 127, 127), m.Rot, 1f);
            }
            fx.Draw(spr, m.Center, FxBatch.C(255, 255, 255, 127), m.Rot, 1f);
        }

        /// <summary>The minion target's reticle (Extra_199, 10 fps), while alive and holding a summon item.</summary>
        void DrawReticle(FxBatch fx)
        {
            if (Target == null || Target.isDead || ownerDead) return;
            var held = TerraMod.Instance?.Inventory.Current.Def;
            if (held == null || held.Class != DamageClass.Summon) return;
            var col = Target.GetComponent<Collider2D>() ?? Target.GetComponentInChildren<Collider2D>();
            if (col == null) return;
            int f = (int)(Time.time * 10f) % 4;
            fx.Draw(FxBatch.Frame("Extra_199", 0, 18 * f, 16, 16, 8f, 8f), TerraParticles.ToPx(col.bounds.center), FxBatch.C(178, 178, 178, 89), 0f, 1f, overPlayer: true);
        }

        /// <summary>Main.mouseTextColor's step, once a tick.</summary>
        static void StepMouseTextColor()
        {
            mouseTextColor += mouseTextStep;
            if (mouseTextColor <= 190) mouseTextStep = 1;
            if (mouseTextColor >= 255) mouseTextStep = -1;
        }
    }
}
