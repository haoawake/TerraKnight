using TerraKnight.Effects;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// The hardmode spells' own ways of casting (Player.ItemCheck_Shoot): the Crystal Storm's
    /// spray, the Poison and Venom Staffs' fans of fangs, the Meteor Staff's rain.
    /// </summary>
    internal static partial class ShootPatterns
    {
        static readonly bool magic =
            Add(ItemType.CrystalStorm, CrystalStorm) & Add(ItemType.PoisonStaff, PoisonStaff) & Add(ItemType.VenomStaff, VenomStaff) &
            Add(ItemType.MeteorStaff, MeteorStaff) & Add(ItemType.BubbleGun, BubbleGun) & Add(ItemType.BlizzardStaff, BlizzardStaff) &
            Add(ItemType.SkyFracture, SkyFracture) & Add(ItemType.LeafBlower, LeafBlower) & Add(ItemType.BatScepter, BatScepter) &
            Add(ItemType.NimbusRod, NimbusRod) & Add(ItemType.MagicalHarp, MagicalHarp) & Add(ItemType.ClingerStaff, ClingerStaff) &
            Add(ItemType.SpiritFlame, SpiritFlame) & Add(ItemType.NebulaBlaze, NebulaBlaze) & Add(ItemType.LunarFlareBook, LunarFlare) &
            Add(ItemType.IceRod, IceRod) & Add(ItemType.CrimsonRod, CrimsonRod) & Add(ItemType.BeeGun, BeeGun) & Add(ItemType.WaspGun, BeeGun) &
            Add(ItemType.RainbowGun, RainbowGun) & Add(ItemType.Razorpine, Razorpine) & Add(ItemType.SoulDrain, SoulDrain) &
            Add(ItemType.BetsysWrath, BetsysWrath) & Add(ItemType.SharpTears, SharpTears) & Add(ItemType.ShadowFlameHexDoll, HexDoll) &
            Add(ItemType.BookStaff, BookStaff) & Add(ItemType.SparkleGuitar, SparkleGuitar) & Add(ItemType.Nightglow, Nightglow) &
            Add(ItemType.Petalstorm, Petalstorm) & Add(ItemType.KillingDeck, KillingDeck);

        /// <summary>Pl:47999-48018: three petals, their hue (unused by the look) by the use and the player's clock.</summary>
        /// <summary>Pl:49704-49775: the deck's throws in turn (TerraProjectiles.ShootKillingDeck).</summary>
        static void KillingDeck(in Shot s) => TerraProjectiles.Instance?.ShootKillingDeck(s.From, s.Vel, s.Damage, s.Knockback, s.Crit);

        static void Petalstorm(in Shot s) =>
            TerraProjectiles.Instance?.ShootPetalstorm(s.From, s.Vel, s.Damage, s.Knockback, s.Crit, s.Mouse.x < s.From.x ? -1f : 1f, Time.time / 5f % 1f);

        /// <summary>Pl:47955-47988: a curling tentacle; the use's third goes straight.</summary>
        static void HexDoll(in Shot s) =>
            TerraProjectiles.Instance?.ShootTentacle(s.From, s.Vel, s.Damage, s.Knockback, s.Crit, TerraMod.Instance != null && TerraMod.Instance.ShotsThisUse >= 3);

        static Vector2 bookAim;

        /// <summary>
        /// Pl:47613-47693, 49555-49566: pages, the first along the aim and the rest the same way
        /// (the item's rotation held); the right button's twister once a use.
        /// </summary>
        static void BookStaff(in Shot s)
        {
            var mod = TerraMod.Instance;
            var hero = s.Hero != null ? s.Hero : HeroController.SilentInstance;
            if (mod == null || hero == null) return;
            bool first = mod.ShotsThisUse <= 1;
            if (mod.AltFunction)
            {
                if (first)
                    TerraProjectiles.Instance?.ShootBookStaff(s.From, s.Vel, s.Damage, s.Knockback, s.Crit, true, s.Mouse.x < s.From.x ? -1f : 1f,
                        hero.GetComponent<BoxCollider2D>().bounds.min);
                return;
            }
            if (first) bookAim = s.Vel;
            TerraProjectiles.Instance?.ShootBookStaff(s.From, bookAim, s.Damage, s.Knockback, s.Crit, false, 0f, Vector2.zero);
        }

        /// <summary>Pl:46989-47003, 48446-48474: a chord as the use starts; a star at an enemy or the cursor.</summary>
        static void SparkleGuitar(in Shot s)
        {
            if (TerraMod.Instance != null && TerraMod.Instance.ShotsThisUse <= 1) TerraProjectiles.GuitarChord(s.From, s.Mouse);
            TerraProjectiles.Instance?.ShootStellarTune(s.From, s.Damage, s.Knockback, s.Crit, s.Mouse);
        }

        /// <summary>Pl:48333-48350: a streak, its hue by how far through the use (0.66 of the wheel over it) and the player's clock.</summary>
        static void Nightglow(in Shot s)
        {
            var mod = TerraMod.Instance;
            int k = mod != null ? Mathf.Max(0, mod.ShotsThisUse - 1) : 0;
            float hue = ((36f - 2f * k) / 36f * 0.66f + Time.time / 5f) % 1f;
            TerraProjectiles.Instance?.ShootNightglow(s.From, s.Damage, s.Knockback, s.Crit, s.Mouse.x < s.From.x ? -1f : 1f, hue);
        }

        /// <summary>Pl:49076-49080: the box as far toward the cursor as the player can see.</summary>
        static void SoulDrain(in Shot s) =>
            TerraProjectiles.Instance?.ShootSoulDrain(s.From, s.Vel, s.Damage, s.Knockback, s.Crit, s.Mouse, s.Mouse.x < s.From.x ? -1f : 1f);

        /// <summary>Pl:49265-49280: three fireballs fanned toward the player's back.</summary>
        static void BetsysWrath(in Shot s) =>
            TerraProjectiles.Instance?.ShootBetsysWrath(s.From, s.Vel, s.Damage, s.Knockback, s.Crit, s.Mouse.x < s.From.x ? -1f : 1f);

        /// <summary>Pl:48436-48444: a thorn out of the ground by the cursor.</summary>
        static void SharpTears(in Shot s) => TerraProjectiles.Instance?.ShootSharpTears(s.From, s.Damage, s.Knockback, s.Crit, s.Mouse);

        /// <summary>Pl:48743: the Crimson Rod's cloud, like the Nimbus Rod's.</summary>
        static void CrimsonRod(in Shot s) => TerraProjectiles.Instance?.ShootCloud(ProjKind.CrimsonCloudMoving, s.From, s.Vel, s.Damage, s.Knockback, s.Crit, s.Mouse);

        /// <summary>Pl:48781-48818: a handful of bees (or wasps), each a little off.</summary>
        static void BeeGun(in Shot s) => TerraProjectiles.Instance?.ShootBeeGun(s.From, s.Vel, s.Damage, s.Knockback, s.Crit, s.Def.Type == ItemType.WaspGun);

        /// <summary>Pl:47716-47725: the old rainbow goes as the new one starts.</summary>
        static void RainbowGun(in Shot s) => TerraProjectiles.Instance?.ShootRainbow(s.From, s.Vel, s.Damage, s.Knockback, s.Crit);

        /// <summary>Pl:48067-48085: 2-4 needles strung out along the aim.</summary>
        static void Razorpine(in Shot s) => TerraProjectiles.Instance?.ShootRazorpine(s.From, s.Vel, s.Damage, s.Knockback, s.Crit);

        /// <summary>Pl:49901: the bolt is told the cursor's cell (tileTargetX, tileTargetY), to freeze into ice there.</summary>
        static void IceRod(in Shot s) =>
            TerraProjectiles.Instance?.ShootIceRodBolt(s.From, s.Vel, s.Damage, s.Knockback, s.Crit, World.CellGrid.WorldToCell(s.Mouse));

        /// <summary>
        /// Pl:47524, 49282: one time in five the big bolt (635) at triple damage, else a bolt a
        /// pixel slower; turned up to ±π/8·0.7 off the aim (a way it can see 100 steps along), its
        /// speed 0.95 to 1.25 times.
        /// </summary>
        static void NebulaBlaze(in Shot s)
        {
            const float Px = TerraParticles.Px;
            bool big = Next(0, 100) < 20;
            var kind = big ? ProjKind.NebulaBlazeBig : ProjKind.NebulaBlaze;
            var v = s.Vel.normalized * (big ? s.Speed : s.Speed - 1f);
            float a = (Random.value - 0.5f) * Mathf.PI / 4f * 0.7f;
            for (int k = 0; k < 10; k++)
            {
                if (!Blocked(s.From, s.From + RotateY(v, a) * 100f * Px)) break;
                a = (Random.value - 0.5f) * Mathf.PI / 4f * 0.7f;
            }
            One(s, kind, s.From, RotateY(v, a) * (0.95f + Random.value * 0.3f), big ? s.Damage * 3 : s.Damage, s.Knockback);
        }

        /// <summary>
        /// Pl:48151: three bolts from 600, 700 and 800 px over the player, halfway between the
        /// cursor and the player and up to 200 px either way, down at the cursor (never upward) at
        /// half the speed; each solid only below the cursor's height.
        /// </summary>
        static void LunarFlare(in Shot s)
        {
            const float Px = TerraParticles.Px;
            var pr = TerraProjectiles.Instance;
            if (pr == null) return;
            float dir = s.Mouse.x < s.From.x ? -1f : 1f;
            for (int k = 0; k < 3; k++)
            {
                float x = (s.Mouse.x - s.From.x) / Px + 10f + Next(0, 201) * -dir;
                x = x / 2f + Next(-200, 201);
                float y = -600f - 100f * k;
                var to = new Vector2((s.Mouse.x - s.From.x) / Px - x, (s.From.y - s.Mouse.y) / Px - y);
                if (to.y < 0f) to.y = -to.y;
                if (to.y < 20f) to.y = 20f;
                var v = to.normalized * s.Speed / 2f;
                pr.ShootLunarFlare(s.From + new Vector2(x, -y) * Px, new Vector2(v.x, -v.y), s.Damage, s.Knockback, s.Crit, s.Mouse.y);
            }
        }

        /// <summary>
        /// Pl:47800: a note (one of three) sent at the cursor (up to 40 px off), at a speed from
        /// how far that is (its distance over 20-60 by 540-960 px, doubled), slowing by its own
        /// speed's share each update; its pitch from the cursor's distance against half the
        /// smaller side of the screen.
        /// </summary>
        static void MagicalHarp(in Shot s)
        {
            const float Px = TerraParticles.Px;
            var cam = Camera.main;
            float half = (cam != null ? cam.orthographicSize * 2f * Mathf.Min(1f, cam.aspect) : 17f) / Px / 2f;
            float dist = Mathf.Min(1f, Vector2.Distance(s.Mouse, s.From) / Px / half) * 1.75f;
            var to = new Vector2(s.Mouse.x - s.From.x, s.From.y - s.Mouse.y) / Px + Random.insideUnitCircle * 40f;
            float div = Mathf.Lerp(20f, 60f, Mathf.Clamp01((to.magnitude - 540f) / 420f));
            var v = to / div * 2f;
            float pitch = Mathf.Clamp(dist * 2f - 1f, -1f, 1f);
            TerraProjectiles.Instance?.ShootHarpNote(s.From, new Vector2(v.x, -v.y), s.Damage, s.Knockback, s.Crit, pitch, v.magnitude / div);
        }

        /// <summary>
        /// Pl:49082: the wall stands where the cursor is (or as far towards it as the player can
        /// see), on the first floor below it within 30 tiles, up to 15 tiles high or to the ceiling.
        /// </summary>
        static void ClingerStaff(in Shot s)
        {
            const float Px = TerraParticles.Px;
            var at = s.From;
            var step = s.Vel.sqrMagnitude > 1e-6f ? s.Vel * Px : Vector2.right * 15f * Px;
            for (int k = 0; k < 400 && !Blocked(s.From, at); k++)
            {
                if (Vector2.Distance(at, s.Mouse) < (20f + Mathf.Abs(s.Vel.x) + Mathf.Abs(s.Vel.y)) * Px)
                {
                    at = s.Mouse;
                    break;
                }
                at += step;
            }
            const float tile = 16f * Px;
            int col = Mathf.FloorToInt(at.x / tile);
            int row = Mathf.FloorToInt(at.y / tile);
            int floor = row;
            while (row - floor < 30 && !World.TerraWorld.IsSolidAt(new Vector2((col + 0.5f) * tile, (floor + 0.5f) * tile))) floor--;
            if (!World.TerraWorld.IsSolidAt(new Vector2((col + 0.5f) * tile, (floor + 0.5f) * tile))) return;
            int ceil = row;
            while (ceil - row < 30 && !World.TerraWorld.IsSolidAt(new Vector2((col + 0.5f) * tile, (ceil + 0.5f) * tile))) ceil++;
            // World y up: the floor block's top, the ceiling block's bottom.
            float floorTop = (floor + 1) * tile, ceilBottom = ceil * tile;
            float height = Mathf.Min(ceilBottom - floorTop, 15f * tile) / Px;
            var topPx = TerraParticles.ToPx(new Vector2(col * tile, floorTop + height * Px));
            TerraProjectiles.Instance?.ShootClingerWall(topPx.x, topPx.y, height, s.Damage, s.Knockback, s.Crit);
        }

        /// <summary>Pl:49297: the wisp appears in a random direction about 90 px off (one the player can see along), still.</summary>
        static void SpiritFlame(in Shot s)
        {
            const float Px = TerraParticles.Px;
            float a = Random.value * Mathf.PI * 2f;
            var v = s.Vel;
            for (int k = 0; k < 10; k++)
            {
                if (!Blocked(s.From, s.From + RotateY(v, a) * 100f * Px)) break;
                a = Random.value * Mathf.PI * 2f;
            }
            var off = RotateY(v, a) * (0.95f + Random.value * 0.3f);
            TerraProjectiles.Instance?.ShootSpiritFlame(s.From + off * 30f * Px, s.Damage, s.Knockback, s.Crit);
        }

        static Vector2 RotateY(Vector2 v, float a) => new Vector2(v.x * Mathf.Cos(a) - v.y * Mathf.Sin(a), v.x * Mathf.Sin(a) + v.y * Mathf.Cos(a));

        /// <summary>Pl:48820: two or three bats, each up to 1.75 px off on each axis.</summary>
        static void BatScepter(in Shot s)
        {
            for (int k = Next(2, 4); k > 0; k--) Spray(s, s.Kind, s.Vel + Jitter(35, 0.05f));
        }

        /// <summary>Pl:48743: the cloud heads for where the cursor was (ai[0], ai[1]).</summary>
        static void NimbusRod(in Shot s) => TerraProjectiles.Instance?.ShootNimbus(s.From, s.Vel, s.Damage, s.Knockback, s.Crit, s.Mouse);

        /// <summary>Pl:48877: three bubbles, each up to 4 px off on each axis.</summary>
        static void BubbleGun(in Shot s)
        {
            for (int k = 0; k < 3; k++) Spray(s, s.Kind, s.Vel + Jitter(40, 0.1f));
        }

        /// <summary>
        /// Pl:48087: two icicles from 600 and 700 px over the player, halfway between the cursor and
        /// the player and up to 200 px either way, down at the cursor (never upward), up to 0.8 px
        /// off on each axis; one of five shapes.
        /// </summary>
        static void BlizzardStaff(in Shot s)
        {
            const float Px = TerraParticles.Px;
            var pr = TerraProjectiles.Instance;
            if (pr == null) return;
            float dir = s.Mouse.x < s.From.x ? -1f : 1f;
            for (int k = 0; k < 2; k++)
            {
                float x = (s.Mouse.x - s.From.x) / Px + 10f + Next(0, 201) * -dir;
                x = x / 2f + Next(-200, 201);
                float y = -600f - 100f * k;
                var to = new Vector2((s.Mouse.x - s.From.x) / Px - x, (s.From.y - s.Mouse.y) / Px - y);
                if (to.y < 0f) to.y = -to.y;
                if (to.y < 20f) to.y = 20f;
                var v = to.normalized * s.Speed + Jitter(40, 0.02f);
                pr.ShootIcicle(s.From + new Vector2(x, -y) * Px, new Vector2(v.x, -v.y), s.Damage, s.Knockback, s.Crit, Next(0, 5));
            }
        }

        /// <summary>
        /// Pl:49312: each sword appears at a random spot 20-60 px round the player (one it can see),
        /// heading for the cursor, a quarter of the way bent towards the player's aim; three a use, 4
        /// ticks apart.
        /// </summary>
        static void SkyFracture(in Shot s)
        {
            var copy = s;
            FractureSword(copy, s.From, s.Mouse);
            var pr = TerraProjectiles.Instance;
            if (pr == null) return;
            for (int k = 1; k < 3; k++)
                pr.Later(4 * k, (from, d) =>
                {
                    var mod = TerraMod.Instance;
                    var aim = mod != null ? mod.DevAim ?? mod.AimPoint : from + d;
                    FractureSword(copy, from, aim);
                });
        }

        static void FractureSword(in Shot s, Vector2 from, Vector2 mouse)
        {
            const float Px = TerraParticles.Px;
            float a = Random.value * Mathf.PI * 2f;
            var at = from + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Mathf.Lerp(20f, 60f, Random.value) * Px;
            for (int k = 0; k < 50; k++)
            {
                at = from + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Mathf.Lerp(20f, 60f, Random.value) * Px;
                if (!Blocked(from, at + (at - from).normalized * 8f * Px)) break;
                a = Random.value * Mathf.PI * 2f;
            }
            var aimDir = (mouse - from).sqrMagnitude > 1e-6f ? (mouse - from).normalized : Vector2.right;
            var toMouse = (mouse - at).sqrMagnitude > 1e-6f ? (mouse - at).normalized : aimDir;
            var v = Vector2.Lerp(toMouse * s.Speed, aimDir * s.Speed, 0.25f);
            One(s, s.Kind, at, v, s.Damage, s.Knockback);
        }

        /// <summary>Pl:49944: a leaf leaves from up to 12 px round the player, one step back along its flight.</summary>
        static void LeafBlower(in Shot s)
        {
            const float Px = TerraParticles.Px;
            One(s, s.Kind, s.From + (Random.insideUnitCircle * 12f - s.Vel) * Px, s.Vel, s.Damage, s.Knockback);
        }

        /// <summary>Pl:48258: up to 1.6 px off on each axis.</summary>
        static void CrystalStorm(in Shot s) => Spray(s, s.Kind, s.Vel + Jitter(40, 0.04f));

        /// <summary>Pl:48577: three fangs (four, one time in three), each spread a little wider, all at the staff's speed.</summary>
        static void PoisonStaff(in Shot s) => Fangs(s, 3 + (Next(0, 3) == 0 ? 1 : 0));

        /// <summary>Pl:48545: four fangs, one more each one time in three, four and five.</summary>
        static void VenomStaff(in Shot s) => Fangs(s, 4 + (Next(0, 3) == 0 ? 1 : 0) + (Next(0, 4) == 0 ? 1 : 0) + (Next(0, 5) == 0 ? 1 : 0));

        static void Fangs(in Shot s, int n)
        {
            for (int k = 0; k < n; k++)
            {
                var v = s.Vel + Jitter(35, 0.05f * k);
                One(s, s.Kind, s.From, v.normalized * s.Speed, s.Damage, s.Knockback);
            }
        }

        /// <summary>
        /// Pl:48119: one meteor (any of the three) from 600 px over the player, halfway between the
        /// cursor and the player and up to 200 px either way, down at the cursor (never upward) at
        /// three quarters of the speed; its size 0.5 to 0.8.
        /// </summary>
        static void MeteorStaff(in Shot s)
        {
            const float Px = TerraParticles.Px;
            float dir = s.Mouse.x < s.From.x ? -1f : 1f;
            // Terraria's px, y down, from the player's centre.
            float x = (s.Mouse.x - s.From.x) / Px + 10f + Next(0, 201) * -dir;
            x = x / 2f + Next(-200, 201);
            const float y = -600f;
            var to = new Vector2((s.Mouse.x - s.From.x) / Px - x + Next(-40, 41) * 0.03f, (s.From.y - s.Mouse.y) / Px - y);
            if (to.y < 0f) to.y = -to.y;
            if (to.y < 20f) to.y = 20f;
            var v = to.normalized * s.Speed;
            v.y += Next(-40, 41) * 0.02f;
            v *= 0.75f;
            var pr = TerraProjectiles.Instance;
            if (pr == null) return;
            pr.ShootMeteor(s.From + new Vector2(x, -y) * Px, new Vector2(v.x, -v.y), s.Damage, s.Knockback, s.Crit, Next(0, 3), 0.5f + Random.value * 0.3f);
        }
    }
}
