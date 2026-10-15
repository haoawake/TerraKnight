using TerraKnight.Effects;
using TerraKnight.Player;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// Petting the Pals (1.4.5: Main.TryPetting, Player.PetAnimal, Projectile.GetPettingInfo): a
    /// right click on a Cattiva, a Clay Bud or a Foxsparks in reach puts the player beside it
    /// (24 px off, the Foxsparks 32, on the player's side of it), facing it, still; the Pal gives its
    /// joy call (the Cattivas' and the Foxsparks'; the Clay Bud is silent) and shows a heart
    /// in a bubble for 60 ticks; let go early and a "!" shows for what is left of them. Any
    /// move or jump ends it (UpdatePettingAnimal).
    /// Not vanilla: the Pal stands still facing the player while patted. In Terraria its idle
    /// spot stays behind the player (AI_067's player.direction offset), so it walks off and
    /// the pat ends within a few ticks.
    /// </summary>
    internal sealed partial class Minions
    {
        M petted;
        float petOffsetX;
        public static int DevPets;
        /// <summary>Dev: why the last pat didn't happen.</summary>
        public static string DevPetWhy = "";
        public static bool Petting => Instance != null && Instance.petted != null;

        static bool Pettable(Kind k) => k == Kind.Cattiva || k == Kind.TrustyCattiva || k == Kind.ClayPot || k == Kind.Foxsparks || k == Kind.TrustyFoxsparks;

        /// <summary>A right click on a Pal within reach (IsProjectileInteractableAndInInteractionRange): pet it.</summary>
        public bool TryPet(HeroController hero, Vector2 mouseWorld)
        {
            RefreshOwner();
            if (!hasOwner || hero == null) return false;
            var mouse = TerraParticles.ToPx(mouseWorld);
            var reach = new Rect(pBox.x - 80f, pBox.y - 64f, pBox.width + 160f, pBox.height + 128f);
            foreach (var m in live)
            {
                if (m.Dead || !Pettable(m.Kind) || m.Alpha > 0.5f) continue;
                var box = m.Box;
                // The Foxsparks' box is 6 px bigger all round for the cursor.
                if (IsFox(m.Kind)) box = new Rect(box.x - 6f, box.y - 6f, box.width + 12f, box.height + 12f);
                if (!box.Contains(mouse) || !reach.Overlaps(m.Box)) continue;
                Pet(hero, m);
                return true;
            }
            return false;
        }

        /// <summary>Dev: pet the first Pal of a kind.</summary>
        public bool DevPet(HeroController hero, Kind kind)
        {
            RefreshOwner();
            foreach (var m in live)
                if (!m.Dead && m.Kind == kind) return Pet(hero, m);
            DevPetWhy = "没有这种伙伴";
            return false;
        }

        bool Pet(HeroController hero, M m)
        {
            int side = m.Center.x > pCenter.x ? 1 : -1;
            float dist = IsFox(m.Kind) ? 32f : 24f;
            float offX = -side * dist;
            // Where the player's feet go: beside the Pal's bottom, floored to whole pixels.
            var feetPx = new Vector2(Mathf.Floor(m.Center.x + offX), Mathf.Floor(m.Bottom));
            var col = hero.GetComponent<BoxCollider2D>();
            var b = col.bounds;
            var feet = TerraParticles.ToWorld(feetPx);
            if (petted == m && Vector2.Distance(new Vector2(b.center.x, b.min.y), feet) < 2f * Px)
            {
                petted = null;
                return true;
            }
            // CanSnapToPosition and ground under the spot (SolidTileAllowBottomSlope).
            const int mask = 1 << ChunkColliders.TerrainLayer;
            var centre = feet + new Vector2(0f, b.size.y / 2f + 0.02f);
            if (Physics2D.OverlapBox(centre, b.size * 0.95f, 0f, mask) != null) { DevPetWhy = $"站不下 {feet:F2}"; return false; }
            if (Physics2D.Raycast(feet + new Vector2(0f, 0.05f), Vector2.down, 0.2f, mask).collider == null) { DevPetWhy = $"脚下没地 {feet:F2}（伙伴 ai0={m.Ai0} vy={m.Vel.y:F1}）"; return false; }
            if (Physics2D.Linecast(b.center, centre, mask).collider != null) { DevPetWhy = "中间有墙"; return false; }
            DevPetWhy = "";
            GrapplingHook.Instance?.ReleaseAll();
            hero.transform.position += (Vector3)(feet - new Vector2(b.center.x, b.min.y));
            hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
            if (side > 0) hero.FaceRight();
            else hero.FaceLeft();
            petted = m;
            petOffsetX = offX;
            DevPets++;
            return true;
        }

        /// <summary>A patted Pal: standing (frame 0) on the ground, facing the player, its summon kept.</summary>
        void HoldForPat(M m)
        {
            if (PlayerBuffs.Flag(m.S.Buff) && !ownerDead) m.TimeLeft = 2;
            m.TileCollide = true;
            m.Vel.x = 0f;
            m.Vel.y = Mathf.Min(m.Vel.y + 0.4f, 10f);
            m.Dir = m.SpriteDir = pCenter.x > m.Center.x ? 1 : -1;
            m.Frame = 0;
            m.Rot = 0f;
            m.Friendly = false;
        }

        /// <summary>Each tick: the pat ends with any input or once the Pal is no longer beside the player; the joy timers run.</summary>
        void PetTick()
        {
            if (petted != null)
            {
                var hero = HeroController.SilentInstance;
                var input = GameHooks.Input != null ? GameHooks.Input.inputActions : null;
                bool moved = input != null && (input.left.IsPressed || input.right.IsPressed || input.up.IsPressed || input.down.IsPressed || input.jump.IsPressed);
                var feet = new Vector2(Mathf.Floor(petted.Center.x + petOffsetX), Mathf.Floor(petted.Bottom));
                var mine = new Vector2(pBox.center.x, pBox.yMax);
                int face = petOffsetX < 0f ? 1 : -1;
                if (petted.Dead || !live.Contains(petted) || hero == null || ownerDead || moved || Vector2.Distance(mine, feet) > 2f + 0.5f
                    || (hero.cState.facingRight ? 1 : -1) != face || GrapplingHook.IsLatched)
                    petted = null;
            }
            foreach (var m in live)
            {
                if (m.Dead || !Pettable(m.Kind)) continue;
                bool now = petted == m;
                if (now && m.PetJoy == 0)
                {
                    m.PetJoy = 60;
                    if (m.Kind == Kind.Cattiva || m.Kind == Kind.TrustyCattiva) TerraMod.Instance?.PlaySound("Custom/pal_cattiva_joy", 0.35f);
                    else if (IsFox(m.Kind)) TerraMod.Instance?.PlaySound("Custom/pal_foxparks_joy", 0.35f);
                }
                if (m.PetJoy > (now ? 1 : 0)) m.PetJoy--;
            }
        }

        /// <summary>
        /// EmoteBubble.DrawTemporaryBubble (emote 0, the heart; Extra_48 cut 8×39: the small bubble
        /// its first and last 6 ticks, then the big one with the emote's two frames), or after the
        /// pat the "!" (UI_quickicon1) at the Pal's centre + (8, −14), rocking.
        /// </summary>
        void DrawPetting(FxBatch fx)
        {
            foreach (var m in live)
            {
                if (m.Dead || !Pettable(m.Kind) || m.PetJoy <= 1 || m.PetJoy >= 60) continue;
                bool now = petted == m;
                if (now || IsFox(m.Kind))
                {
                    var tex = Terraria.TerrariaAssets.Texture("Extra_48");
                    int fw = tex.width / 8, fh = tex.height / 39;
                    bool small = m.PetJoy < 6 || 60 - m.PetJoy < 6;
                    int dir = m.SpriteDir;
                    var at = new Vector2(Mathf.Floor(m.Center.x - dir * m.W * 0.75f), Mathf.Floor(m.Pos.y + 2f));
                    fx.Draw(FxBatch.Frame("Extra_48", (small ? 0 : 1) * fw, 0, fw, fh, fw / 2f, fh), at, Color.white, 0f, 1f, flipX: dir == 1);
                    if (!small)
                    {
                        int frame = (60 - m.PetJoy) / 8 % 2;
                        fx.Draw(FxBatch.Frame("Extra_48", (0 * 2 % 8 + frame) * fw, fh, fw, fh, fw / 2f, fh), at, Color.white, 0f, 1f, flipX: dir == 1);
                    }
                }
                else
                {
                    var icon = Terraria.TerrariaAssets.Texture("UI/UI_quickicon1");
                    // Drawn with no rocking here (rotationMultiplier 0).
                    const float rot = 0f;
                    var at = m.Center + new Vector2(8f, -14f) + new Vector2(-7f, -6f);
                    fx.Draw(FxBatch.Frame("UI/UI_quickicon1", 0, 0, icon.width, icon.height, icon.width / 2f, icon.height / 2f), at, Lit, rot, 1f);
                }
            }
        }
    }
}
