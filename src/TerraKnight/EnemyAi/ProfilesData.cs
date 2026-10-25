namespace TerraKnight.EnemyAi
{
    internal static partial class Profiles
    {
        // §8.1-8.2 King's Pass, the Crossroads, the Resting Grounds.
        static void Crossroads()
        {
            // Struck from anywhere, it charges along its ground at the player (the user's, 2026-10-22).
            P("Crawler", sight: 8f, aimLead: 0.1f).Add(
                Leap(3f, 3f, 6f, antic: 0.4f, recover: 0.6f, cd: 3f, rel: Rel.Above),
                Chase(0f, 999f, 12f, 2.5f, cd: 2.5f));
            all["Crawler"].Moves[1].When = When.Struck;
            all["Crawler"].FarStruck = true;
            P("Buzzer", sight: 16f, flyer: true, alarm: 14f, aimLead: 0.3f, gap: 0.7f).Add(
                Lunge(10f, 36f, 0.4f, antic: 0.35f, recover: 0.5f, cd: 2.2f, min: 2f, vertical: true),
                Chase(8f, 24f, 22f, 0.8f, cd: 2.5f),
                Lunge(8f, 36f, 0.35f, antic: 0.25f, recover: 0.4f, cd: 2f, tier: Tier.Master, vertical: true, when: When.Struck));
            P("Bouncer", sight: 12f, flyer: true, aimLead: 0.2f).Add(
                Lunge(12f, 24f, 0.6f, antic: 0.5f, recover: 0.4f, cd: 4f, vertical: true),
                Lunge(10f, 30f, 0.5f, antic: 0.3f, recover: 0.3f, cd: 3f, tier: Tier.Expert, vertical: true, when: When.Struck));
            P("Climber", sight: 10f, aimLead: 0f).Add(
                Drop(6f, antic: 0.3f, cd: 4f),
                Drop(8f, antic: 0.25f, cd: 2.5f, tier: Tier.Master));
            P("Spitter", sight: 18f, flyer: true, alarm: 12f, aimLead: 0.35f).Add(
                Shot(Look.Infection, 14f, 22f, antic: 0.45f, cd: 2.6f, min: 4f),
                Shot(Look.Infection, 14f, 22f, count: 3, interval: 0.25f, antic: 0.5f, recover: 0.8f, cd: 3.5f, tier: Tier.Expert, min: 4f).Weight(1.5f));
            P("Hatcher", sight: 18f, flyer: true, aimLead: 0.25f).Add(
                Lunge(3f, 28f, 0.3f, antic: 0.35f, recover: 0.6f, cd: 4f, tier: Tier.Expert, vertical: true, when: When.Close),
                Lunge(10f, 24f, 0.45f, antic: 0.4f, cd: 3.5f, tier: Tier.Expert, vertical: true),
                Dive(12f, 4f, 24f, cd: 4f, tier: Tier.Master));
            P("Hatchling", sight: 14f, flyer: true, aimLead: 0.3f, gap: 0.6f).Add(
                Lunge(6f, 30f, 0.3f, antic: 0.25f, recover: 0.4f, cd: 2f, vertical: true),
                Lunge(8f, 32f, 0.3f, antic: 0.25f, cd: 2f, tier: Tier.Expert, vertical: true).Times(1),
                Dive(8f, 3f, 26f, antic: 0.3f, cd: 3f, tier: Tier.Master));
            P("ZombieRunner", sight: 12f, aimLead: 0.15f).Add(
                Lunge(3f, 24f, 0.3f, antic: 0.4f, recover: 0.6f, cd: 2.5f, rel: Rel.Level),
                Stalk(12f),
                Leap(5f, 4f, 14f, antic: 0.35f, cd: 3f, rel: Rel.Above, when: When.Air));
            P("ZombieHornhead", sight: 14f, aimLead: 0.15f).Add(
                Lunge(14f, 28f, 0.8f, antic: 0.5f, recover: 0.8f, cd: 3.5f, rel: Rel.Level).Heavy(),
                Leap(3f, 3f, 8f, antic: 0.3f, cd: 2.5f, tier: Tier.Expert, rel: Rel.Above));
            P("ZombieLeaper", sight: 14f, aimLead: 0.25f).Add(
                Leap(10f, 6f, 18f, antic: 0.4f, cd: 2.5f),
                Leap(10f, 7f, 20f, antic: 0.3f, cd: 2.5f, tier: Tier.Master, rel: Rel.Above, when: When.Air).Weight(2f),
                Lunge(10f, 26f, 0.5f, antic: 0.4f, cd: 3f, tier: Tier.Expert, rel: Rel.Level));
            P("ZombieBarger", sight: 12f, aimLead: 0.15f).Add(
                Lunge(4f, 24f, 0.3f, antic: 0.4f, cd: 2.5f).Cut().Heavy(),
                Lunge(4f, 24f, 0.3f, antic: 0.4f, cd: 3.5f, tier: Tier.Expert).Times(1).Cut().Heavy(),
                Lunge(10f, 26f, 0.5f, antic: 0.45f, cd: 3f, tier: Tier.Expert, rel: Rel.Level).Heavy());
            P("ZombieShield", sight: 14f, aimLead: 0.15f).Add(
                Chase(4f, 16f, 9f, 1.2f, cd: 3f),
                Lunge(4f, 26f, 0.3f, antic: 0.3f, recover: 0.6f, cd: 3f, tier: Tier.Master, when: When.Struck),
                Lunge(10f, 24f, 0.5f, antic: 0.45f, cd: 3f, tier: Tier.Expert, rel: Rel.Level));
            // Its spit is its own goop, each ball at where the player is, sometimes three (the user's, 2026-10-22); its short
            // wave is that goop popping up out of the ground toward him (BlockerSpit).
            P("Blocker", sight: 18f, aimLead: 0.3f).Add(
                Wave(12f, 14f, max: 14f, antic: 0.5f, cd: 4f, tier: Tier.Expert),
                Wave(12f, 16f, max: 14f, antic: 0.45f, cd: 2.8f, tier: Tier.Master));
            all["Blocker"].KeepWake = true;
            all["Blocker"].GoopSpit = true;
            P("Roller", sight: 14f, aimLead: 0.3f).Add(
                Lunge(12f, 26f, 1f, antic: 0.45f, recover: 0.6f, cd: 3f, rel: Rel.Level),
                Leap(6f, 4f, 14f, antic: 0.3f, cd: 3f, tier: Tier.Expert, rel: Rel.Above));
            P("ZombieGuard", sight: 20f, memory: 8f, aimLead: 0.2f).Add(
                Leap(12f, 6f, 16f, antic: 0.6f, recover: 1f, cd: 4f).Waves(8f, 18f).Heavy().Cut().Masks(2),
                Wave(10f, 18f, max: 6f, antic: 0.6f, cd: 4f, tier: Tier.Expert),
                Shot(Look.Rock, 10f, 22f, count: 3, spread: 40f, gravity: 40f, antic: 0.5f, cd: 4f, tier: Tier.Expert).Weight(0.8f),
                Lunge(10f, 24f, 0.7f, antic: 0.5f, recover: 0.8f, cd: 3.5f, tier: Tier.Expert, rel: Rel.Level).Heavy().Masks(2),
                Stalk(10f, tier: Tier.Master));
            P("Mummy", sight: 12f, aimLead: 0.15f).Add(
                Lunge(8f, 30f, 0.35f, antic: 0.5f, recover: 0.8f, cd: 3f, rel: Rel.Level),
                Leap(4f, 4f, 10f, antic: 0.4f, cd: 3f, tier: Tier.Expert, rel: Rel.Above));
        }

        // §8.3 Greenpath.
        static void Greenpath()
        {
            P("MossWalker", sight: 8f, aimLead: 0.1f).Add(
                Chase(0f, 6f, 14f, 1.5f, cd: 4f),
                Shot(Look.Thorn, 6f, 16f, gravity: 40f, antic: 0.4f, cd: 3f, tier: Tier.Expert, size: 0.6f),
                Lunge(6f, 16f, 0.5f, antic: 0.4f, cd: 3f, tier: Tier.Expert, rel: Rel.Level),
                Chase(0f, 8f, 16f, 1.5f, cd: 2.5f, tier: Tier.Master));
            all["MossWalker"].Moves[1].Rel = Rel.Above;
            P("MossmanRunner", sight: 14f, aimLead: 0.15f).Add(
                Lunge(12f, 28f, 0.6f, antic: 0.4f, cd: 3f, rel: Rel.Level),
                Leap(10f, 5f, 16f, antic: 0.4f, cd: 3.5f, tier: Tier.Expert, rel: Rel.Above),
                Lunge(12f, 32f, 0.6f, antic: 0.35f, cd: 2.5f, tier: Tier.Master, rel: Rel.Level).Times(1));
            P("MossmanShaker", sight: 12f, aimLead: 0.1f).Add(
                Cloud(Look.Spore, 3f, 4f, duration: 2f, drift: 1f),
                Shot(Look.Spore, 10f, 18f, gravity: 30f, antic: 0.45f, cd: 4f, tier: Tier.Expert, land: Land.Cloud),
                Cloud(Look.Spore, 3.5f, 4f, duration: 2.4f, drift: 1f, tier: Tier.Master).With(Poisoned, 3f).Weight(2f),
                Lunge(14f, 24f, 0.55f, antic: 0.45f, cd: 3f, rel: Rel.Level),
                Leap(10f, 5f, 14f, antic: 0.4f, cd: 3.5f, tier: Tier.Expert, rel: Rel.Above));
            all["MossmanShaker"].Moves[1].Rel = Rel.Above;
            P("Mosquito", sight: 18f, flyer: true, aimLead: 0.35f, gap: 0.7f).Add(
                Lunge(14f, 40f, 0.35f, antic: 0.4f, recover: 0.6f, cd: 2.2f, min: 2f, vertical: true),
                Lunge(14f, 40f, 0.35f, antic: 0.4f, recover: 0.8f, cd: 3f, tier: Tier.Expert, min: 2f, vertical: true).Times(1).Weight(1.5f),
                Lunge(14f, 40f, 0.35f, antic: 0.4f, recover: 0.8f, cd: 3f, tier: Tier.Master, min: 2f, vertical: true).Times(1).With(Bleeding, 3f).Weight(2f));
            P("BlobFlyer", sight: 14f, flyer: true, aimLead: 0.2f).Add(
                Shot(Look.Acid, 12f, 16f, count: 4, aim: Aim.Radial, antic: 0.5f, cd: 3f, tier: Tier.Expert, size: 0.6f),
                Lunge(10f, 22f, 0.5f, antic: 0.45f, cd: 3.5f, tier: Tier.Expert, vertical: true),
                Dive(10f, 4f, 24f, cd: 4f, tier: Tier.Master));
            P("MossCharger", sight: 14f, aimLead: 0.2f).Add(
                Burrow(14f, 22f, antic: 0.4f, look: Look.Thorn),
                Leap(6f, 4f, 14f, antic: 0.35f, cd: 3f, tier: Tier.Expert, rel: Rel.Above));
            P("SnapperTrap", sight: 10f, aimLead: 0.2f).Add(
                Pulse(Look.Thorn, 4f, 4f, antic: 0.5f, duration: 0.25f, cd: 2.5f),
                Shot(Look.Thorn, 12f, 18f, count: 3, interval: 0.2f, antic: 0.45f, cd: 3.5f, tier: Tier.Expert, min: 4f, size: 0.5f),
                Pulse(Look.Thorn, 4f, 4f, antic: 0.5f, duration: 0.25f, cd: 3f, tier: Tier.Master).Cut(),
                Own("Ready", 6f, hold: 1f, cd: 1.8f, tier: Tier.Expert, from: new[] { "Idle" }),
                Own("Ready", 6f, hold: 0.8f, cd: 1.2f, tier: Tier.Master, from: new[] { "Idle" }).Weight(1.5f));
            all["SnapperTrap"].KeepWake = true;
            P("PlantShooter", sight: 18f, aimLead: 0.35f).Add(
                Shot(Look.Thorn, 16f, 20f, antic: 0.5f, cd: 3f, tier: Tier.Expert, land: Land.Burst),
                Shot(Look.Thorn, 16f, 20f, antic: 0.5f, cd: 3f, tier: Tier.Master).Weight(0.6f),
                Own("Shoot Antic", 24f, hold: 1f, cd: 2.4f, tier: Tier.Expert, from: new[] { "Shot Pause", "Idle Anim", "Return Pause" }),
                Own("Shoot Antic", 24f, hold: 0.8f, cd: 1.5f, tier: Tier.Master, from: new[] { "Shot Pause", "Idle Anim", "Return Pause" }).Weight(1.5f));
            all["PlantShooter"].Moves[1].Bounces = 1;
            P("AcidWalker", sight: 10f, aimLead: 0.1f).Add(
                Shot(Look.Acid, 6f, 16f, count: 3, spread: 40f, gravity: 35f, aim: Aim.Up, antic: 0.45f, cd: 3f),
                Lunge(8f, 18f, 0.7f, antic: 0.5f, recover: 0.7f, cd: 3.5f, rel: Rel.Level),
                Leap(8f, 4f, 12f, antic: 0.45f, cd: 4f, tier: Tier.Expert, rel: Rel.Above),
                Lunge(10f, 20f, 0.7f, antic: 0.45f, cd: 3f, tier: Tier.Master, rel: Rel.Level).Times(1));
            all["AcidWalker"].Moves[0].Rel = Rel.Above;
            P("AcidFlyer", sight: 10f, flyer: true, aimLead: 0.1f).Add(
                Lunge(10f, 22f, 0.5f, antic: 0.5f, cd: 3f, vertical: true),
                Shot(Look.Acid, 6f, 10f, count: 2, interval: 0.3f, aim: Aim.Down, gravity: 30f, antic: 0.4f, cd: 3f, tier: Tier.Expert));
            all["AcidFlyer"].Moves[1].Rel = Rel.Below;
            P("MossKnight", sight: 16f, memory: 6f, aimLead: 0.2f).Add(
                Leap(6f, 5f, 14f, antic: 0.4f, cd: 3f, rel: Rel.Above),
                Shot(Look.Thorn, 12f, 20f, count: 2, interval: 0.2f, gravity: 40f, antic: 0.45f, cd: 3.5f, tier: Tier.Expert, min: 3f),
                Lunge(6f, 30f, 0.3f, antic: 0.3f, cd: 3f, tier: Tier.Master, when: When.PostDash));
            P("MossKnightFat", sight: 14f, aimLead: 0.2f).Add(
                Leap(10f, 4f, 12f, antic: 0.35f, cd: 4f).Times(2).Waves(4f, 14f),
                Lunge(10f, 22f, 0.6f, antic: 0.45f, cd: 3.5f, tier: Tier.Expert, rel: Rel.Level).Heavy().Masks(2),
                Leap(12f, 5f, 14f, antic: 0.35f, cd: 3f, tier: Tier.Master).Times(2).Waves(5f, 16f));
            P("MossFlyer", sight: 12f, flyer: true, aimLead: 0.3f).Add(
                Dive(10f, 3f, 30f, antic: 0.3f, cd: 3f),
                Lunge(10f, 32f, 0.35f, antic: 0.35f, cd: 2.5f, tier: Tier.Expert, vertical: true),
                Dive(10f, 3f, 32f, antic: 0.3f, cd: 2.5f, tier: Tier.Master));
        }

        // §8.4-8.5 Fog Canyon, the Fungal Wastes.
        static void FogAndFungus()
        {
            P("JellyCrawler", sight: 8f, flyer: true, aimLead: 0f).Add(
                Chase(2f, 8f, 4f, 2f, cd: 2.5f),
                Pulse(Look.Electric, 2f, 3f, antic: 0.6f, cd: 3f, tier: Tier.Expert).With(Electrified, 2f),
                Lunge(8f, 14f, 0.8f, antic: 0.5f, cd: 3.5f, tier: Tier.Expert, vertical: true),
                Lunge(10f, 16f, 0.8f, antic: 0.45f, cd: 3f, tier: Tier.Master, vertical: true).With(Electrified, 2f));
            P("Jellyfish", sight: 10f, flyer: true, aimLead: 0f).Add(
                Chase(3f, 10f, 4f, 2f, cd: 2.5f),
                Pulse(Look.Electric, 3f, 4f, antic: 0.7f, cd: 3.5f).With(Electrified, 2f),
                Lunge(10f, 14f, 0.9f, antic: 0.55f, cd: 3.5f, tier: Tier.Expert, vertical: true),
                Lunge(12f, 16f, 0.9f, antic: 0.5f, cd: 3f, tier: Tier.Master, vertical: true).With(Electrified, 2f));
            P("FungCrawler", sight: 8f, aimLead: 0.1f).Add(
                Lunge(6f, 20f, 0.6f, antic: 0.35f, cd: 3f, rel: Rel.Level),
                Cloud(Look.Spore, 1.5f, 3f, duration: 1f, drift: 0f, antic: 0.3f, cd: 3f, tier: Tier.Expert),
                Leap(8f, 4f, 14f, antic: 0.35f, cd: 3.5f, tier: Tier.Expert, rel: Rel.Above),
                Lunge(8f, 24f, 0.5f, antic: 0.3f, cd: 2.5f, tier: Tier.Master, rel: Rel.Level).Times(1));
            P("FungoonBaby", sight: 8f, flyer: true, aimLead: 0f).Add(
                Lunge(12f, 20f, 0.5f, antic: 0.35f, cd: 2.5f, vertical: true),
                new Move { Kind = Kind.Cloud, Look = Look.Spore, Radius = 2f, Max = 4f, Duration = 1.2f, Antic = 0.3f, Recover = 0.2f, Cooldown = 2f, Tier = Tier.Expert, When = When.Struck, Sight = false },
                Chase(1f, 12f, 9f, 1.2f, cd: 2.5f, tier: Tier.Expert),
                Lunge(12f, 24f, 0.5f, antic: 0.3f, cd: 2f, tier: Tier.Master, vertical: true).Times(1));
            P("FungusFlyer", sight: 12f, flyer: true, aimLead: 0.2f).Add(
                Shot(Look.Spore, 10f, 6f, aim: Aim.Down, gravity: 30f, antic: 0.4f, cd: 3f, tier: Tier.Expert, land: Land.Cloud),
                Shot(Look.Spore, 10f, 6f, aim: Aim.Down, gravity: 30f, antic: 0.4f, cd: 3f, tier: Tier.Master, land: Land.Cloud).With(Confused, 1f).Weight(2f),
                Lunge(10f, 24f, 0.45f, antic: 0.4f, cd: 3f, tier: Tier.Expert, vertical: true));
            all["FungusFlyer"].Moves[0].Rel = Rel.Below;
            all["FungusFlyer"].Moves[1].Rel = Rel.Below;
            P("MushroomTurret", sight: 18f, aimLead: 0.3f).Add(
                Shot(Look.Spore, 18f, 10f, homing: 90f, life: 4f, antic: 0.5f, cd: 3.5f, tier: Tier.Expert, land: Land.Cloud),
                Own("Attack Antic", 22f, hold: 1f, cd: 2.4f, tier: Tier.Expert, from: new[] { "Idle", "First Idle" }),
                Own("Attack Antic", 22f, hold: 0.8f, cd: 1.5f, tier: Tier.Master, from: new[] { "Idle", "First Idle" }).Weight(1.5f));
            P("FungifiedZombie", sight: 12f, aimLead: 0.15f).Add(
                Cloud(Look.Spore, 3f, 4f, duration: 1.6f, drift: 0.5f),
                Leap(6f, 5f, 12f, antic: 0.4f, cd: 3.5f, tier: Tier.Expert, rel: Rel.Above, when: When.Air),
                Lunge(10f, 24f, 0.5f, antic: 0.45f, cd: 3f, tier: Tier.Expert, rel: Rel.Level),
                Lunge(10f, 28f, 0.5f, antic: 0.4f, cd: 2.5f, tier: Tier.Master, rel: Rel.Level).Times(1));
            P("MushroomRoller", sight: 14f, aimLead: 0.25f).Add(
                Leap(10f, 5f, 16f, antic: 0.4f, cd: 3f),
                Lunge(10f, 26f, 0.8f, antic: 0.4f, cd: 3.5f, tier: Tier.Expert, rel: Rel.Level),
                Cloud(Look.Spore, 2.5f, 5f, duration: 3f, drift: 0f, antic: 0.3f, cd: 5f, tier: Tier.Master).With(Confused, 1f));
            P("MushroomBrawler", sight: 18f, memory: 7f, aimLead: 0.2f).Add(
                Wave(8f, 16f, max: 5f, antic: 0.6f, cd: 3.5f),
                Shot(Look.Honey, 12f, 20f, gravity: 40f, antic: 0.5f, cd: 4f, tier: Tier.Expert, land: Land.Honey, min: 3f),
                Leap(8f, 6f, 14f, antic: 0.5f, cd: 4f, tier: Tier.Master).Masks(2),
                Lunge(10f, 24f, 0.5f, antic: 0.45f, cd: 3f, tier: Tier.Expert, rel: Rel.Level));
        }

        // §8.6-8.8 the Mantis tribe, the City of Tears, the Soul Sanctum.
        static void City()
        {
            var youth = P("MantisFlyerChild", sight: 16f, flyer: true, aimLead: 0.35f).Add(
                Lunge(14f, 42f, 0.35f, antic: 0.35f, recover: 0.5f, cd: 2.2f, vertical: true),
                Lunge(14f, 42f, 0.3f, antic: 0.35f, recover: 0.6f, cd: 3f, tier: Tier.Expert, vertical: true).Times(1),
                Lunge(14f, 42f, 0.3f, antic: 0.35f, recover: 0.6f, cd: 3f, tier: Tier.Master, vertical: true).Times(1).Cut().Weight(2f));
            youth.Mantis = true;
            var mantis = P("Mantis", sight: 16f, aimLead: 0.25f).Add(
                Leap(6f, 5f, 16f, antic: 0.35f, cd: 2.5f, rel: Rel.Above),
                Shot(Look.Blade, 14f, 26f, antic: 0.45f, cd: 3.5f, tier: Tier.Expert, min: 5f, life: 1.4f),
                Lunge(8f, 34f, 0.3f, antic: 0.3f, cd: 2.5f, tier: Tier.Master, when: When.PostDash).Cut().With(Bleeding, 3f),
                Lunge(10f, 30f, 0.4f, antic: 0.35f, cd: 3f, tier: Tier.Expert, rel: Rel.Level));
            mantis.Mantis = true;

            P("Sentry", sight: 16f, alarm: 16f, aimLead: 0.2f).Add(
                Leap(6f, 5f, 16f, antic: 0.4f, cd: 3f, rel: Rel.Above),
                Lunge(5f, 30f, 0.25f, antic: 0.3f, cd: 3f, tier: Tier.Expert, when: When.Struck),
                Lunge(6f, 30f, 0.3f, antic: 0.4f, cd: 3f, tier: Tier.Master).Cut());
            P("SentryFat", sight: 16f, alarm: 16f, memory: 6f, aimLead: 0.2f).Add(
                Lunge(10f, 26f, 0.7f, antic: 0.55f, recover: 0.8f, cd: 3.5f, rel: Rel.Level).Heavy(),
                Leap(6f, 5f, 14f, antic: 0.45f, cd: 3.5f, rel: Rel.Above).Waves(4f, 14f),
                Wave(6f, 16f, max: 4f, antic: 0.55f, cd: 4f, tier: Tier.Expert));
            P("FlyingSentrySword", sight: 18f, flyer: true, alarm: 16f, aimLead: 0.35f).Add(
                Dive(12f, 4f, 34f, antic: 0.4f, cd: 2.8f),
                Lunge(5f, 30f, 0.25f, antic: 0.55f, cd: 3.5f, tier: Tier.Expert, vertical: true, rel: Rel.Above),
                Dive(12f, 4f, 36f, antic: 0.4f, cd: 3f, tier: Tier.Master).Cut().Weight(2f));
            P("FlyingSentryJavelin", sight: 20f, flyer: true, alarm: 16f, aimLead: 0.4f).Add(
                Shot(Look.Spike, 20f, 30f, antic: 0.5f, cd: 2.8f, min: 5f, size: 0.8f),
                Shot(Look.Spike, 20f, 30f, count: 2, interval: 0.2f, aim: Aim.Bracket, antic: 0.5f, cd: 3.5f, tier: Tier.Expert, min: 5f, size: 0.8f),
                Dive(14f, 5f, 40f, time: 0.5f, antic: 0.45f, cd: 4f, tier: Tier.Master).Cut());
            all["FlyingSentryJavelin"].Moves[1].CutHook = true;
            P("RoyalDandy", sight: 12f, aimLead: 0.2f).Add(
                Leap(5f, 2f, 12f, antic: 0.35f, cd: 3f).Times(2),
                Lunge(10f, 24f, 0.5f, antic: 0.4f, cd: 3f, tier: Tier.Expert, rel: Rel.Level),
                Leap(8f, 3f, 14f, antic: 0.3f, cd: 2.5f, tier: Tier.Master).Times(3));
            P("RoyalCoward", sight: 12f, aimLead: 0.15f).Add(
                Lunge(4f, 26f, 0.3f, antic: 0.35f, cd: 3f, when: When.Close),
                Shot(Look.Rock, 10f, 18f, gravity: 40f, antic: 0.35f, cd: 3f, tier: Tier.Expert, min: 3f, size: 0.6f),
                Lunge(10f, 24f, 0.5f, antic: 0.4f, cd: 3f, tier: Tier.Expert, rel: Rel.Level),
                Leap(8f, 4f, 14f, antic: 0.35f, cd: 3f, tier: Tier.Master, rel: Rel.Above));
            all["RoyalCoward"].Alarm = 16f;
            P("RoyalPlumper", sight: 12f, aimLead: 0.2f).Add(
                Leap(8f, 4f, 12f, antic: 0.45f, cd: 3.5f).Times(1),
                Leap(8f, 5f, 12f, antic: 0.5f, cd: 4f, tier: Tier.Expert).Waves(4f, 12f),
                Pull(10f, 12f, 0.8f, antic: 0.6f, cd: 7f, tier: Tier.Master));
            // Passive until struck (its sight and hearing nil): then it turns, leaping or charging from where it ran to.
            P("GorgeousHusk", sight: 0f, hear: 0f, memory: 12f, aimLead: 0.2f).Add(
                Leap(18f, 5f, 16f, antic: 0.5f, cd: 3f).Masks(2),
                Shot(Look.Coin, 12f, 22f, count: 5, spread: 50f, gravity: 35f, antic: 0.5f, cd: 4f, tier: Tier.Expert, size: 0.5f),
                Lunge(16f, 26f, 0.6f, antic: 0.45f, cd: 3f, rel: Rel.Level).Masks(2));
            P("GreatShieldZombie", sight: 18f, memory: 8f, alarm: 16f, aimLead: 0.2f).Add(
                Chase(3f, 16f, 8f, 1.2f, cd: 3f),
                Lunge(10f, 28f, 0.6f, antic: 0.6f, recover: 0.9f, cd: 4f, tier: Tier.Expert, rel: Rel.Level).Heavy().Masks(2),
                Lunge(6f, 30f, 0.3f, antic: 0.3f, cd: 3f, tier: Tier.Master, when: When.PostDash));

            P("MageBlob", sight: 18f, hear: 30f, aimLead: 0.2f).Add(
                Leap(4f, 3f, 10f, antic: 0.35f, cd: 2.5f),
                Burrow(14f, 18f, antic: 0.4f, cd: 5f, tier: Tier.Expert, look: Look.Soul));
            P("MageBalloon", sight: 18f, hear: 20f, flyer: true, aimLead: 0.3f).Add(
                Blink(5f, 7f, cd: 3f),
                Shot(Look.Soul, 16f, 12f, homing: 60f, life: 4f, antic: 0.5f, cd: 3.5f, tier: Tier.Expert, min: 6f),
                Lunge(10f, 26f, 0.4f, antic: 0.4f, cd: 3f, tier: Tier.Expert, vertical: true),
                Blink(5f, 7f, antic: 0.4f, cd: 2.5f, tier: Tier.Master));
            P("Mage", sight: 20f, hear: 20f, flyer: true, memory: 6f, aimLead: 0.3f).Add(
                Blink(8f, 12f, cd: 4f),
                Shot(Look.Soul, 20f, 14f, count: 3, spread: 40f, homing: 50f, life: 4f, antic: 0.55f, cd: 4f, tier: Tier.Expert),
                Shot(Look.Soul, 20f, 14f, count: 3, spread: 40f, homing: 50f, life: 4f, antic: 0.55f, cd: 4f, tier: Tier.Master).With(Silenced, 1.5f).Weight(1.5f),
                Pillar(Look.Soul, 1, 20f, delay: 0.8f, antic: 0.5f, cd: 5f, tier: Tier.Master));
        }

        // §8.9-8.11 the Royal Waterways, Crystal Peak, the infected Crossroads.
        static void WaterwaysAndPeak()
        {
            P("CeilingDropper", sight: 12f, flyer: true, aimLead: 0.3f);
            P("FlipHopper", sight: 12f, aimLead: 0.25f).Add(
                Leap(8f, 4f, 14f, antic: 0.35f, cd: 2.5f),
                Leap(4f, 7f, 8f, antic: 0.3f, cd: 3f, tier: Tier.Expert, rel: Rel.Above));
            P("Inflater", sight: 10f, flyer: true, aimLead: 0.2f).Add(
                Lunge(6f, 18f, 2f, antic: 0.5f, recover: 0.6f, cd: 4f, vertical: true),
                Shot(Look.Spike, 8f, 20f, count: 6, aim: Aim.Radial, antic: 0.5f, cd: 4f, tier: Tier.Expert, size: 0.6f),
                Dive(8f, 3f, 22f, antic: 0.4f, cd: 3.5f, tier: Tier.Expert),
                Lunge(8f, 22f, 1.2f, antic: 0.4f, cd: 3f, tier: Tier.Master, vertical: true).Times(1));
            P("Flukefly", sight: 14f, flyer: true, aimLead: 0.3f, gap: 0.7f).Add(
                Lunge(10f, 34f, 0.35f, antic: 0.3f, cd: 2.2f, vertical: true),
                Lunge(10f, 34f, 0.35f, antic: 0.3f, cd: 2.2f, tier: Tier.Master, vertical: true).With(Bleeding, 4f).Weight(2f),
                Dive(8f, 3f, 26f, antic: 0.3f, cd: 3f, tier: Tier.Expert));
            P("Flukeman", sight: 14f, aimLead: 0.2f).Add(
                Chase(3f, 14f, 14f, 1.2f, cd: 3f),
                Lunge(8f, 26f, 0.5f, antic: 0.4f, cd: 3f, rel: Rel.Level));
            P("FatFluke", sight: 20f, memory: 8f, aimLead: 0.2f).Add(
                Lunge(16f, 26f, 1.2f, antic: 0.6f, recover: 1f, cd: 4f, rel: Rel.Level).Times(1).Heavy().Masks(2),
                Shot(Look.Water, 14f, 16f, count: 3, spread: 40f, homing: 40f, life: 3f, antic: 0.5f, cd: 4f, tier: Tier.Expert),
                Leap(10f, 4f, 14f, antic: 0.5f, cd: 4f, tier: Tier.Expert, rel: Rel.Above).Masks(2),
                Lunge(16f, 30f, 1f, antic: 0.5f, recover: 0.8f, cd: 3f, tier: Tier.Master, rel: Rel.Level).Heavy().Masks(2));

            P("ZombieMiner", sight: 16f, aimLead: 0.35f).Add(
                Shot(Look.Rock, 16f, 24f, gravity: 40f, antic: 0.5f, cd: 3f, min: 4f),
                Shot(Look.Rock, 16f, 24f, count: 2, interval: 0.25f, gravity: 40f, aim: Aim.Bracket, antic: 0.5f, cd: 3.5f, tier: Tier.Expert, min: 4f),
                Shot(Look.Rock, 16f, 24f, gravity: 40f, antic: 0.5f, cd: 3f, tier: Tier.Master, min: 4f).Cut());
            P("MinesCrawler", sight: 10f, aimLead: 0.2f).Add(
                Shot(Look.Crystal, 8f, 22f, antic: 0.45f, cd: 2.8f, size: 0.6f),
                Shot(Look.Crystal, 3f, 18f, count: 6, aim: Aim.Radial, antic: 0.45f, cd: 3.5f, tier: Tier.Expert, size: 0.6f),
                Drop(6f, cd: 4f, tier: Tier.Master),
                Chase(0f, 12f, 10f, 2f, cd: 3f));
            all["MinesCrawler"].Moves[1].When = When.Close;
            P("CrystalCrawler", sight: 10f, aimLead: 0.15f).Add(
                Shot(Look.Crystal, 6f, 20f, count: 3, spread: 50f, aim: Aim.Up, antic: 0.45f, cd: 3f, size: 0.6f),
                Lunge(5f, 18f, 0.5f, antic: 0.4f, cd: 3.5f, tier: Tier.Expert, rel: Rel.Level),
                Leap(8f, 4f, 12f, antic: 0.4f, cd: 3.5f, tier: Tier.Expert, rel: Rel.Above),
                Lunge(8f, 22f, 0.5f, antic: 0.35f, cd: 2.5f, tier: Tier.Master, rel: Rel.Level).Times(1));
            all["CrystalCrawler"].Moves[0].Rel = Rel.Above;
            // Its crystal back turned to the blows now and then (Brain's shell).
            all["CrystalCrawler"].Reflect = true;
            P("CrystalFlyer", sight: 18f, flyer: true, aimLead: 0.35f).Add(
                Lunge(10f, 26f, 0.4f, antic: 0.45f, cd: 3.5f, tier: Tier.Expert, vertical: true),
                Shot(Look.Crystal, 16f, 22f, gravity: 30f, antic: 0.5f, cd: 3f, land: Land.Cluster),
                Shot(Look.Crystal, 16f, 22f, count: 3, spread: 40f, gravity: 30f, antic: 0.5f, cd: 4f, tier: Tier.Expert, land: Land.Cluster),
                Dive(10f, 4f, 26f, antic: 0.45f, cd: 3.5f, tier: Tier.Expert),
                Lunge(12f, 30f, 0.4f, antic: 0.4f, cd: 2.5f, tier: Tier.Master, vertical: true).Times(1));
            P("LaserBug", sight: 16f, aimLead: 0f).Add(
                Beam(Look.Crystal, 14f, 14f, delay: 0.6f, duration: 0.4f, antic: 0.4f, cd: 4f, tier: Tier.Expert),
                Beam(Look.Crystal, 14f, 14f, delay: 0.6f, duration: 0.6f, sweep: 20f, antic: 0.4f, cd: 4.5f, tier: Tier.Master).Weight(1.5f),
                Own("Beam Antic", 18f, hold: 1.6f, cd: 3f, tier: Tier.Expert, from: new[] { "Idle" }),
                Own("Beam Antic", 18f, hold: 1.4f, cd: 2f, tier: Tier.Master, from: new[] { "Idle" }).Weight(1.5f));
            P("BeamMiner", sight: 18f, aimLead: 0.25f).Add(
                Beam(Look.Crystal, 18f, 18f, delay: 0.7f, duration: 0.4f, sweep: 15f, antic: 0.45f, cd: 4.5f, tier: Tier.Expert),
                Beam(Look.Crystal, 18f, 18f, delay: 0.7f, duration: 0.4f, sweep: 15f, antic: 0.45f, cd: 4.5f, tier: Tier.Master).With(Slow, 2f).Weight(1.5f),
                Own("Face 2", 22f, hold: 2.6f, cd: 3.5f, tier: Tier.Expert, from: new[] { "Idle", "CD", "Check Facing" }),
                Lunge(10f, 24f, 0.5f, antic: 0.45f, cd: 3.5f, tier: Tier.Expert, rel: Rel.Level),
                Own("Face 2", 22f, hold: 2.4f, cd: 2.4f, tier: Tier.Master, from: new[] { "Idle", "CD", "Check Facing" }).Weight(1.5f));

            P("AngryBuzzer", sight: 16f, flyer: true, alarm: 14f, aimLead: 0.3f, gap: 0.7f).Add(
                Lunge(10f, 34f, 0.3f, antic: 0.3f, recover: 0.5f, cd: 2.5f, vertical: true).Times(1),
                Shot(Look.Infection, 14f, 20f, gravity: 30f, antic: 0.45f, cd: 3.5f, tier: Tier.Master, min: 6f),
                Dive(10f, 3f, 28f, antic: 0.4f, cd: 3.5f, tier: Tier.Expert),
                Lunge(12f, 36f, 0.35f, antic: 0.3f, cd: 2.2f, tier: Tier.Master, vertical: true).Times(2));
            P("BurstingBouncer", sight: 12f, flyer: true, aimLead: 0.2f).Add(
                Shot(Look.Infection, 8f, 6f, aim: Aim.Down, gravity: 30f, antic: 0.35f, cd: 2.5f, tier: Tier.Expert),
                Lunge(10f, 26f, 0.5f, antic: 0.4f, cd: 3f, tier: Tier.Expert, vertical: true),
                Lunge(12f, 30f, 0.45f, antic: 0.35f, cd: 2.5f, tier: Tier.Master, vertical: true).Times(1));
            all["BurstingBouncer"].Moves[0].Rel = Rel.Below;
            P("BurstingZombie", sight: 14f, aimLead: 0.2f).Add(
                Lunge(14f, 28f, 0.8f, antic: 0.5f, recover: 0.6f, cd: 3.5f, rel: Rel.Level),
                Leap(4f, 4f, 12f, antic: 0.4f, cd: 3f, rel: Rel.Above));
            P("SpittingZombie", sight: 16f, aimLead: 0.35f).Add(
                Shot(Look.Infection, 14f, 22f, gravity: 40f, antic: 0.5f, cd: 3f, land: Land.Pool, min: 3f),
                Shot(Look.Infection, 14f, 22f, count: 3, interval: 0.25f, gravity: 40f, aim: Aim.Bracket, antic: 0.5f, cd: 4f, tier: Tier.Expert, land: Land.Pool, min: 3f),
                Shot(Look.Infection, 14f, 22f, gravity: 40f, antic: 0.5f, cd: 3f, tier: Tier.Master, land: Land.Pool, min: 3f).With(BrokenArmor, 3f));
        }

        // §8.12-8.13 Deepnest, the Ancient Basin and the Abyss.
        static void Deepnest()
        {
            P("BabyCentipede", sight: 12f, aimLead: 0.2f).Add(
                Burrow(12f, 20f, antic: 0.4f, cd: 5f),
                Leap(6f, 4f, 16f, antic: 0.3f, cd: 3f, tier: Tier.Expert));
            P("CentipedeHatcher", sight: 18f, flyer: true, aimLead: 0.25f).Add(
                Dive(10f, 5f, 30f, antic: 0.45f, cd: 4f, tier: Tier.Expert),
                Lunge(10f, 24f, 0.5f, antic: 0.45f, cd: 3.5f, tier: Tier.Expert, vertical: true),
                Dive(10f, 5f, 30f, antic: 0.4f, cd: 3f, tier: Tier.Master));
            P("SpiderCorpse", sight: 12f, aimLead: 0.25f).Add(
                Leap(6f, 4f, 16f, antic: 0.35f, cd: 2.5f),
                Shot(Look.Web, 10f, 22f, antic: 0.45f, cd: 4f, tier: Tier.Expert, min: 3f).With(Webbed, 0.6f),
                Lunge(10f, 26f, 0.5f, antic: 0.4f, cd: 3f, tier: Tier.Expert, rel: Rel.Level),
                Leap(8f, 5f, 18f, antic: 0.3f, cd: 2.2f, tier: Tier.Master).Times(1));
            P("MiniSpider", sight: 10f, aimLead: 0.25f, gap: 0.6f).Add(
                Lunge(6f, 30f, 0.3f, antic: 0.3f, cd: 2.2f, rel: Rel.Level),
                Leap(6f, 4f, 16f, antic: 0.3f, cd: 2.5f, tier: Tier.Expert));
            P("ShootSpider", sight: 18f, aimLead: 0.35f).Add(
                Shot(Look.Poison, 16f, 24f, antic: 0.45f, cd: 2.8f).With(Poisoned, 4f),
                Shot(Look.Web, 14f, 22f, antic: 0.5f, cd: 4f, tier: Tier.Expert, land: Land.Web).With(Slow, 2f),
                Shot(Look.Web, 14f, 22f, antic: 0.5f, cd: 4f, tier: Tier.Master, land: Land.Web).With(Slow, 2f).Cut().Weight(1.5f));
            P("SpiderFlyer", sight: 10f, flyer: true, aimLead: 0.3f).Add(
                Lunge(8f, 30f, 0.35f, antic: 0.4f, cd: 2.5f, vertical: true),
                Pull(12f, 14f, 0.6f, antic: 0.5f, cd: 6f, tier: Tier.Expert),
                Shot(Look.Web, 10f, 20f, antic: 0.45f, cd: 4f, tier: Tier.Master, land: Land.Web).With(Slow, 2f),
                Dive(10f, 4f, 30f, antic: 0.4f, cd: 3.5f, tier: Tier.Expert),
                Lunge(10f, 32f, 0.35f, antic: 0.35f, cd: 2.2f, tier: Tier.Master, vertical: true).Times(1).With(Slow, 2f));
            P("SlashSpider", sight: 20f, memory: 8f, aimLead: 0.25f).Add(
                Leap(10f, 6f, 18f, antic: 0.5f, recover: 0.8f, cd: 3.5f).Masks(2),
                Lunge(5f, 30f, 0.35f, antic: 0.45f, recover: 0.7f, cd: 3f, tier: Tier.Expert),
                Lunge(8f, 34f, 0.3f, antic: 0.3f, cd: 3f, tier: Tier.Master, when: When.PostDash).Cut());

            var creeper = P("AbyssCrawler", sight: 10f, aimLead: 0f).Add(
                Drop(6f, cd: 5f, tier: Tier.Master),
                Drop(6f, cd: 4f, tier: Tier.Expert));
            creeper.TouchDebuff = Darkness; creeper.TouchTicks = 180; creeper.TouchTier = Tier.Expert;
            P("LesserMawlek", sight: 18f, aimLead: 0.35f).Add(
                Shot(Look.Infection, 16f, 22f, gravity: 40f, antic: 0.5f, cd: 3f, land: Land.Pool, min: 3f),
                Leap(6f, 5f, 14f, antic: 0.45f, cd: 3.5f, tier: Tier.Expert, rel: Rel.Above),
                Shot(Look.Infection, 16f, 22f, count: 3, spread: 40f, gravity: 40f, antic: 0.55f, cd: 4f, tier: Tier.Master, land: Land.Pool, min: 3f));
            P("MawlekTurret", sight: 22f, memory: 8f, aimLead: 0.3f).Add(
                Shot(Look.Infection, 22f, 24f, count: 6, interval: 0.12f, gravity: 35f, antic: 0.6f, recover: 0.8f, cd: 4f),
                Pull(16f, 12f, 0.8f, antic: 0.6f, cd: 7f, tier: Tier.Expert),
                Shot(Look.Infection, 22f, 24f, count: 6, interval: 0.12f, gravity: 35f, antic: 0.6f, recover: 0.8f, cd: 4.5f, tier: Tier.Master, land: Land.Pool).Weight(1.5f));
            P("OrangeBalloon", sight: 16f, flyer: true, aimLead: 0.35f).Add(
                Chase(2f, 16f, 10f, 1.5f, cd: 3f),
                Lunge(10f, 22f, 0.5f, antic: 0.45f, cd: 3f, tier: Tier.Expert, vertical: true),
                Chase(2f, 18f, 13f, 1.6f, cd: 2.5f, tier: Tier.Master));
            var sibling = P("Sibling", sight: 14f, flyer: true, aimLead: 0.3f).Add(
                Lunge(10f, 34f, 0.4f, antic: 0.4f, cd: 3f, vertical: true),
                Dive(10f, 4f, 30f, antic: 0.4f, cd: 3.5f, tier: Tier.Expert),
                Lunge(10f, 36f, 0.35f, antic: 0.35f, cd: 2.5f, tier: Tier.Master, vertical: true).Times(1));
            sibling.Sibling = true;
            sibling.TouchDebuff = Darkness; sibling.TouchTicks = 180; sibling.TouchTier = Tier.Expert;
        }

        // §8.14-8.15 the Kingdom's Edge and the Hive, the Queen's Gardens.
        static void EdgeAndGardens()
        {
            // Passive until struck: then it rams back, and hunts while it remembers.
            P("BlowFly", sight: 0f, hear: 0f, flyer: true, memory: 8f, aimLead: 0.3f).Add(
                Lunge(14f, 26f, 0.55f, antic: 0.35f, cd: 3f, vertical: true, when: When.Struck),
                Lunge(16f, 24f, 0.6f, antic: 0.45f, cd: 3.5f, vertical: true),
                Dive(14f, 5f, 26f, antic: 0.5f, cd: 4f, tier: Tier.Expert));
            P("SuperSpitter", sight: 20f, flyer: true, aimLead: 0.4f).Add(
                Shot(Look.Infection, 18f, 22f, count: 3, spread: 30f, antic: 0.5f, recover: 0.6f, cd: 4f, tier: Tier.Expert, min: 4f).Times(1),
                Shot(Look.Infection, 18f, 22f, count: 3, spread: 30f, antic: 0.5f, recover: 0.6f, cd: 4f, tier: Tier.Master, min: 4f, land: Land.Pool).Times(1).Weight(1.5f),
                Lunge(10f, 28f, 0.4f, antic: 0.4f, cd: 3f, tier: Tier.Expert, vertical: true));
            P("Hopper", sight: 16f, aimLead: 0.3f).Add(
                Leap(10f, 5f, 16f, antic: 0.3f, cd: 3f).Times(2),
                Leap(12f, 7f, 18f, antic: 0.3f, cd: 3f, tier: Tier.Expert, rel: Rel.Above),
                Leap(12f, 5f, 20f, antic: 0.25f, cd: 2.5f, tier: Tier.Master).Times(3));
            P("GiantHopper", sight: 22f, memory: 8f, aimLead: 0.3f).Add(
                Leap(16f, 9f, 20f, antic: 0.5f, recover: 0.6f, cd: 3.5f).Waves(8f, 18f).Cut().Heavy().Masks(2),
                Leap(16f, 7f, 22f, antic: 0.45f, cd: 3f, tier: Tier.Expert, rel: Rel.Above).Masks(2),
                Leap(16f, 9f, 24f, antic: 0.4f, cd: 3f, tier: Tier.Master).Times(1).Waves(8f, 20f).Heavy().Masks(2));
            P("BeeHatchling", sight: 14f, flyer: true, aimLead: 0.3f).Add(
                Lunge(10f, 28f, 0.35f, antic: 0.35f, cd: 2.5f, vertical: true),
                Lunge(10f, 30f, 0.35f, antic: 0.3f, cd: 2.5f, tier: Tier.Master, vertical: true).With(Poisoned, 4f).Weight(2f),
                Shot(Look.Stinger, 12f, 26f, antic: 0.35f, cd: 2f, size: 0.5f),
                Shot(Look.Stinger, 12f, 26f, antic: 0.35f, cd: 2f, tier: Tier.Master, size: 0.5f).With(Poisoned, 4f).Weight(2f),
                Dive(10f, 3f, 26f, antic: 0.35f, cd: 3f, tier: Tier.Expert));
            P("BeeStinger", sight: 18f, flyer: true, aimLead: 0.45f).Add(
                Lunge(14f, 40f, 0.4f, antic: 0.5f, recover: 0.6f, cd: 3.5f, tier: Tier.Expert, vertical: true),
                Lunge(14f, 40f, 0.4f, antic: 0.5f, recover: 0.6f, cd: 3.5f, tier: Tier.Master, vertical: true).With(Poisoned, 4f).Weight(2f),
                Dive(12f, 4f, 30f, antic: 0.45f, cd: 3.5f, tier: Tier.Expert));
            P("BigBee", sight: 20f, flyer: true, memory: 8f, aimLead: 0.35f).Add(
                Lunge(16f, 30f, 0.7f, antic: 0.55f, recover: 0.7f, cd: 3.5f, vertical: true).Heavy().Masks(2),
                Shot(Look.Honey, 14f, 18f, gravity: 40f, antic: 0.5f, cd: 4f, tier: Tier.Expert, land: Land.Honey, min: 3f),
                Lunge(16f, 30f, 0.6f, antic: 0.55f, recover: 0.9f, cd: 5f, tier: Tier.Master, vertical: true).Times(2).Heavy().Masks(2),
                Dive(12f, 5f, 26f, antic: 0.5f, cd: 4f, tier: Tier.Expert));
            P("ZombieHive", sight: 14f, aimLead: 0.25f).Add(
                Lunge(14f, 24f, 0.6f, antic: 0.45f, cd: 3.5f, rel: Rel.Level),
                Leap(10f, 5f, 14f, antic: 0.45f, cd: 4f, tier: Tier.Expert, rel: Rel.Above),
                Shot(Look.Stinger, 10f, 24f, count: 2, spread: 20f, antic: 0.4f, cd: 3f, tier: Tier.Expert, size: 0.5f),
                Shot(Look.Honey, 8f, 16f, gravity: 40f, antic: 0.4f, cd: 4f, tier: Tier.Master, land: Land.Honey));
            P("GrubMimic", sight: 14f, aimLead: 0.25f).Add(
                Leap(8f, 4f, 16f, antic: 0.35f, cd: 2.8f).Times(1),
                Lunge(8f, 26f, 0.6f, antic: 0.4f, cd: 3.5f, tier: Tier.Expert, rel: Rel.Level));

            P("GardenZombie", sight: 14f, aimLead: 0.25f).Add(
                Lunge(10f, 26f, 0.55f, antic: 0.45f, cd: 3f, rel: Rel.Level),
                Leap(8f, 5f, 14f, antic: 0.4f, cd: 3.5f, tier: Tier.Expert, rel: Rel.Above),
                Lunge(10f, 28f, 0.55f, antic: 0.4f, cd: 3f, tier: Tier.Master, rel: Rel.Level).With(Poisoned, 4f).Weight(1.5f),
                Shot(Look.Spike, 10f, 22f, count: 5, spread: 90f, antic: 0.5f, cd: 3.5f, size: 0.6f),
                Shot(Look.Spike, 10f, 22f, count: 5, spread: 90f, antic: 0.5f, cd: 4f, tier: Tier.Expert, size: 0.6f).Times(1),
                Shot(Look.Spike, 10f, 22f, count: 5, spread: 90f, antic: 0.5f, cd: 4f, tier: Tier.Master, size: 0.6f).Times(1).With(Poisoned, 4f).Weight(1.5f));
            P("GrassHopper", sight: 14f, aimLead: 0.3f).Add(
                Leap(12f, 6f, 18f, antic: 0.3f, cd: 2.8f).Times(1),
                Leap(12f, 6f, 18f, antic: 0.3f, cd: 3.2f, tier: Tier.Expert).Times(2));
            P("MantisHeavyFlyer", sight: 20f, flyer: true, aimLead: 0.4f).Add(
                Shot(Look.Blade, 18f, 22f, homing: 40f, life: 2.5f, antic: 0.5f, cd: 3f, min: 4f),
                Shot(Look.Blade, 18f, 22f, count: 2, spread: 60f, homing: 40f, life: 2.5f, antic: 0.5f, cd: 3.5f, tier: Tier.Expert, min: 4f),
                Shot(Look.Blade, 18f, 22f, count: 2, spread: 60f, homing: 40f, life: 2.5f, antic: 0.5f, cd: 3.5f, tier: Tier.Master, min: 4f).Cut().With(Bleeding, 3f).Weight(1.5f));
            P("HeavyMantis", sight: 18f, memory: 7f, aimLead: 0.3f).Add(
                Leap(10f, 7f, 18f, antic: 0.45f, cd: 3f),
                Shot(Look.Blade, 16f, 26f, life: 1.6f, antic: 0.5f, cd: 3.5f, tier: Tier.Expert, min: 5f),
                Lunge(8f, 36f, 0.3f, antic: 0.3f, cd: 2.5f, tier: Tier.Expert, when: When.PostDash),
                Leap(10f, 7f, 18f, antic: 0.45f, cd: 3f, tier: Tier.Master).Cut().With(Bleeding, 3f).Weight(1.5f));
        }

        // §8.16-8.17 the Colosseum, the Junk Pit, the White Palace.
        static void Colosseum()
        {
            Like("ColRoller", "Roller").Add(
                Shot(Look.Spike, 4f, 20f, count: 6, aim: Aim.Radial, antic: 0.45f, cd: 4f, tier: Tier.Expert, size: 0.6f));
            Like("ColMosquito", "Mosquito");
            Like("Blobble", "BlobFlyer").Add(
                Shot(Look.Acid, 12f, 16f, count: 8, aim: Aim.Radial, antic: 0.55f, cd: 4f, tier: Tier.Master, size: 0.6f));
            Like("ColShield", "ZombieShield").Add(
                Leap(6f, 5f, 14f, antic: 0.4f, cd: 3f, rel: Rel.Above));
            Like("ColMiner", "ZombieMiner").Add(
                Leap(8f, 5f, 16f, antic: 0.45f, cd: 3f));
            Like("ColFlyingSentry", "FlyingSentrySword");
            Like("ColWorm", "SentryFat").Moves[1].CutHook = true;
            Like("ColHopper", "GrassHopper");
            P("ElectricMage", sight: 24f, hear: 20f, flyer: true, memory: 8f, aimLead: 0.4f).Add(
                Pillar(Look.Electric, 3, 24f, delay: 0.6f, interval: 0.35f, antic: 0.5f, cd: 4f).With(Electrified, 2f),
                Blink(8f, 12f, cd: 4f, tier: Tier.Expert),
                Pillar(Look.Electric, 4, 24f, delay: 0.55f, interval: 0.3f, antic: 0.5f, cd: 4.5f, tier: Tier.Master).With(Electrified, 2f).Weight(1.5f),
                Lunge(12f, 26f, 0.4f, antic: 0.4f, cd: 3f, tier: Tier.Expert, vertical: true),
                Blink(8f, 12f, antic: 0.4f, cd: 3f, tier: Tier.Master));
            P("PaleLurker", sight: 20f, memory: 8f, aimLead: 0.3f).Add(
                Leap(12f, 7f, 20f, antic: 0.4f, cd: 3f),
                Shot(Look.Spike, 12f, 18f, count: 3, spread: 40f, gravity: 40f, antic: 0.45f, cd: 4f, tier: Tier.Expert, land: Land.Pool, size: 0.6f),
                Burrow(14f, 24f, antic: 0.4f, cd: 6f, tier: Tier.Master),
                Lunge(8f, 34f, 0.3f, antic: 0.3f, cd: 3f, tier: Tier.Master, when: When.PostDash));

            P("RoyalGuard", sight: 24f, memory: 8f, aimLead: 0.35f).Add(
                Shot(Look.Blade, 20f, 26f, life: 1.6f, antic: 0.55f, cd: 3.5f, size: 1f).Masks(2),
                Shot(Look.Blade, 20f, 26f, count: 2, spread: 30f, life: 1.6f, antic: 0.55f, cd: 4f, tier: Tier.Expert, size: 1f).Masks(2),
                Lunge(8f, 34f, 0.35f, antic: 0.45f, cd: 4f, tier: Tier.Master).Cut().Masks(2),
                Own("Throw Start", 24f, hold: 2f, cd: 3.5f, tier: Tier.Expert, fsm: "Guard", from: new[] { "Idle", "Wait", "Charge" }),
                Own("Slash Antic", 8f, hold: 1.2f, cd: 2.5f, tier: Tier.Expert, fsm: "Guard", from: new[] { "Idle", "Wait", "Charge" }));
        }

        // §8.18-8.19 the Grimmkin, the Zotelings.
        static void Others()
        {
            P("FlameBearerSmall", sight: 22f, hear: 20f, flyer: true, memory: 8f, aimLead: 0.35f).Add(
                Shot(Look.Fire, 20f, 22f, count: 3, spread: 30f, antic: 0.5f, cd: 3f),
                Shot(Look.Fire, 20f, 22f, count: 3, spread: 30f, antic: 0.5f, cd: 3f, tier: Tier.Expert).With(OnFire, 3f).Weight(1.5f),
                Blink(6f, 10f, cd: 4f, tier: Tier.Master));
            P("FlameBearerMed", sight: 24f, hear: 20f, flyer: true, memory: 8f, aimLead: 0.4f).Add(
                Shot(Look.Fire, 20f, 22f, count: 5, spread: 60f, antic: 0.5f, cd: 3.5f),
                Pillar(Look.Fire, 1, 24f, delay: 0.6f, antic: 0.45f, cd: 4f, tier: Tier.Expert).With(OnFire, 3f),
                Blink(6f, 10f, cd: 4f, tier: Tier.Master),
                Lunge(12f, 28f, 0.4f, antic: 0.4f, cd: 3f, tier: Tier.Expert, vertical: true));
            P("FlameBearerLarge", sight: 26f, hear: 20f, flyer: true, memory: 8f, aimLead: 0.45f).Add(
                Pillar(Look.Fire, 3, 26f, delay: 0.55f, interval: 0.35f, antic: 0.5f, cd: 4f).With(OnFire, 3f),
                Shot(Look.Fire, 20f, 18f, count: 8, aim: Aim.Radial, antic: 0.55f, cd: 4.5f, tier: Tier.Expert),
                Blink(6f, 10f, cd: 4f, tier: Tier.Master),
                Lunge(12f, 28f, 0.4f, antic: 0.4f, cd: 3f, tier: Tier.Expert, vertical: true));

            P("ZotelingBuzzer", sight: 16f, flyer: true, aimLead: 0.3f).Add(
                Dive(10f, 3f, 26f, antic: 0.45f, cd: 3.5f, tier: Tier.Expert),
                Lunge(10f, 30f, 0.35f, antic: 0.35f, cd: 2.5f, tier: Tier.Expert, vertical: true),
                Dive(10f, 3f, 30f, antic: 0.35f, cd: 2.5f, tier: Tier.Master));
            P("ZotelingHopper", sight: 16f, aimLead: 0.3f).Add(
                Leap(10f, 6f, 14f, antic: 0.4f, cd: 3f).Times(1),
                Leap(12f, 7f, 18f, antic: 0.35f, cd: 3f, tier: Tier.Expert, rel: Rel.Above),
                Lunge(10f, 24f, 0.4f, antic: 0.35f, cd: 2.5f, tier: Tier.Master, rel: Rel.Level));
            P("ZotelingBalloon", sight: 16f, flyer: true, aimLead: 0.3f).Add(
                Chase(1f, 12f, 8f, 1.2f, cd: 2.5f, tier: Tier.Expert),
                Lunge(10f, 20f, 0.5f, antic: 0.45f, cd: 3f, tier: Tier.Expert, vertical: true),
                Chase(1f, 14f, 10f, 1.4f, cd: 2f, tier: Tier.Master));
        }
    }
}
