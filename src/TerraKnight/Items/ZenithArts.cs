using TerraKnight.Effects;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// The Zenith's remade flights (the user's 10-26 "继续升级剩下的这些…特效，动画，粒子效果，新颖，多样"; with "近战挥砍：新版" on):
    /// each use's three swords fly one pattern, the next use the next: the game's loop; a crossing star (from three sides
    /// straight through the cursor's spot and curling back past it); a spiral tightening on it; a rain from above, each
    /// sword landing in a ring of its colour; a wheel round the player loosed at it. Each keeps the game's hits (its blade
    /// and four points back along its trail, an enemy once in 11 ticks a sword), its ribbon, afterimages and sparks. The right
    /// button held gathers swords of every kind the Zenith is forged from round the player (three, up to sixteen at a full
    /// charge) and loosed they converge on the enemies by the cursor (BladeSkillsZenith).
    /// </summary>
    internal sealed partial class ZenithSwords
    {
        internal enum Pattern : byte { Loop, Cross, Spiral, Rain, Wheel, Orbit, Converge }

        static readonly Pattern[] cycle = { Pattern.Loop, Pattern.Cross, Pattern.Spiral, Pattern.Rain, Pattern.Wheel };
        static int cycleAt = -1;
        static float lastUse = -10f;
        Pattern usePattern;

        /// <summary>Dev: the last use's pattern, how many uses had one; the swords gathered and loosed by the right button; hits by pattern.</summary>
        public static string DevPattern = "";
        public static readonly System.Collections.Generic.Dictionary<string, int> DevHitsBy = new System.Collections.Generic.Dictionary<string, int>();
        static void DevHit(Pattern p) { var k = NameOf(p); DevHitsBy[k] = (DevHitsBy.TryGetValue(k, out int n) ? n : 0) + 1; }
        public static int DevPatterns, DevGathered, DevLoosed;

        static string NameOf(Pattern p) => p == Pattern.Cross ? "交叉星" : p == Pattern.Spiral ? "收束螺旋" : p == Pattern.Rain ? "剑雨" : p == Pattern.Wheel ? "环舞" : p == Pattern.Converge ? "万剑归宗" : "回环";

        /// <summary>The use's pattern: the game's loop with the remade swings off; else the next in turn (from the first after a pause).</summary>
        static Pattern NextPattern()
        {
            if (!BladeArts.Enabled) return Pattern.Loop;
            if (Time.time - lastUse > 1.2f) cycleAt = -1;
            lastUse = Time.time;
            cycleAt = (cycleAt + 1) % cycle.Length;
            var p = DevForce ?? cycle[cycleAt];
            DevPattern = NameOf(p);
            DevPatterns++;
            return p;
        }

        /// <summary>Dev: the patterns from the first again.</summary>
        public static void DevPatternReset() => cycleAt = -1;

        /// <summary>Dev (suite zenithrain): every use this pattern; the rain as before (held to the player, its point 34 px under the spot, the second and third sword on an enemy or a random spot); where each rain sword's point struck (world).</summary>
        internal static Pattern? DevForce;
        internal static bool DevOldRain;
        internal static readonly System.Collections.Generic.List<Vector2> DevRainTips = new System.Collections.Generic.List<Vector2>();

        /// <summary>A thrown sword's pattern, its place in its use and the cursor's spot (px from the player).</summary>
        static void Shape(Sword s, Pattern pattern, int n, Vector2 v)
        {
            s.Pattern = pattern;
            s.Index = n;
            s.V = v;
            s.From = Vector2.zero;
            s.Burst = false;
            // The rain's swords come down one after another, the star's set out one after another (meeting on the spot in turn,
            // not all at once); the spiral's and the wheel's three from three sides.
            s.Phase = pattern == Pattern.Rain ? n * 7f : pattern == Pattern.Cross ? n * 14f : n * 120f;
        }

        /// <summary>
        /// How bright a pattern's sword is drawn (its ribbon, glow, light and sparkle): those that meet on one spot fainter each,
        /// so where they cross their added light keeps the swords' own colours instead of turning white (the user's 10-26 "莫名其妙的
        /// 不必要的闪光"): the crossing star and the wheel's three a little, the seventeen loosed together much more.
        /// </summary>
        static float Brightness(Pattern p) =>
            p == Pattern.Loop ? 1f : p == Pattern.Cross || p == Pattern.Wheel ? 0.5f : p == Pattern.Converge ? 0.28f : p == Pattern.Orbit ? 0.4f : 0.8f;

        /// <summary>How far into its flight a sword shows (the game's fade by its age), its start put off by its turn (the star's, the volley's).</summary>
        static float FadeOf(Sword s, float t)
        {
            switch (s.Pattern)
            {
                case Pattern.Orbit: return 1f;
                case Pattern.Converge: return t < 20f ? 1f : Fade(t);
                case Pattern.Cross: return Fade(t - s.Phase);
                default: return Fade(t);
            }
        }

        /// <summary>A sword's sparkle put off with its start.</summary>
        static float SparkleDelay(Sword s) => s.Pattern == Pattern.Cross ? s.Phase : 0f;

        static Vector2 Bezier(Vector2 a, Vector2 b, Vector2 c, float t) => (1f - t) * (1f - t) * a + 2f * (1f - t) * t * b + t * t * c;

        static Vector2 Unit(Vector2 v) => v.sqrMagnitude > 1e-4f ? v.normalized : Vector2.right;

        /// <summary>A sword of a remade pattern, a step: its place (px from the player), its way (it flies point first), its fade.</summary>
        void StepPattern(Sword s)
        {
            float length = s.V.magnitude;
            float near = Mathf.Clamp01(1f - length / 900f);
            // About as long in flight as the game's loop (some 30 ticks), each sword's eleven-tick hits spread over its passes.
            s.Increment = s.Pattern == Pattern.Orbit ? 0f : Mathf.Lerp(0.6f, 1.15f, near);
            s.T += s.Increment;
            float end = s.Pattern == Pattern.Rain ? 92f : s.Pattern == Pattern.Cross ? 70f + s.Phase : 70f;
            if (s.T >= end)
            {
                Stop(s);
                return;
            }
            var u = Unit(s.V);
            var across = new Vector2(-u.y, u.x);
            int side = s.V.x >= 0f ? 1 : -1;
            float t = Mathf.Clamp01((s.T - (s.Pattern == Pattern.Cross ? s.Phase : 0f)) / 60f);
            var was = s.Rel;
            switch (s.Pattern)
            {
                case Pattern.Cross:
                {
                    // From beside the player, straight through the spot, then curling back past it on the other side.
                    float turn = s.Index == 0 ? -0.95f : s.Index == 1 ? 0.95f : 0f;
                    var start = (Vector2)(Quaternion.Euler(0f, 0f, turn * Mathf.Rad2Deg) * u) * (s.Index == 2 ? -40f : 50f);
                    var far = s.V + Unit(s.V - start) * 150f;
                    if (t < 0.5f) s.Rel = Vector2.Lerp(start, far, 1f - (1f - t / 0.5f) * (1f - t / 0.5f));
                    else s.Rel = Bezier(far, s.V + across * (s.Index == 1 ? -1f : 1f) * 90f, Vector2.zero, (t - 0.5f) / 0.5f);
                    break;
                }
                case Pattern.Spiral:
                {
                    // Round the spot, closing in two and a half turns, through it, and home.
                    float a0 = Mathf.Atan2(-u.y, -u.x) + s.Phase * Mathf.Deg2Rad;
                    if (t < 0.65f)
                    {
                        float k = t / 0.65f;
                        float r = Mathf.Lerp(Mathf.Max(70f, length * 0.4f), 6f, k);
                        float a = a0 + side * k * Mathf.PI * 5f;
                        s.Rel = s.V + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                    }
                    else
                    {
                        float k = (t - 0.65f) / 0.35f;
                        s.Rel = Vector2.Lerp(s.V, Vector2.zero, k * k);
                    }
                    break;
                }
                case Pattern.Rain:
                {
                    // Down from high over the cursor's spot as thrown (held in the world: Anchor), faster and faster, the three
                    // from a little apart, each point stuck in the spot itself (the user's 10-27 "应该严格插在鼠标位置"); held there, fading.
                    var spot = s.Anchored ? s.Anchor - playerPx : s.V;
                    float k = Mathf.Clamp01((s.T - s.Phase) / 34f);
                    float x = (s.Index - 1) * 48f;
                    var top = spot + new Vector2(x * 1.4f, -300f);
                    var bottom = DevOldRain ? spot + new Vector2(x * 0.3f, 34f) : spot - Unit(spot - top) * (s.Profile.Blade / 2f);
                    s.Rel = Vector2.Lerp(top, bottom, k * k);
                    if (k >= 1f && !s.Burst)
                    {
                        s.Burst = true;
                        Landing(s);
                    }
                    break;
                }
                case Pattern.Wheel:
                {
                    // Round the player at 72 px for a turn and a quarter, then loosed straight through the spot.
                    float a0 = s.Phase * Mathf.Deg2Rad + Mathf.Atan2(u.y, u.x);
                    // Loosed one after another.
                    float wind = 0.3f + s.Index * 0.07f;
                    var orbit = new Vector2(Mathf.Cos(a0 + side * Mathf.PI * 2.5f), Mathf.Sin(a0 + side * Mathf.PI * 2.5f)) * 72f;
                    if (t < wind)
                    {
                        float a = a0 + side * (t / wind) * Mathf.PI * 2.5f;
                        s.Rel = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 72f;
                    }
                    else
                    {
                        // Through the spot and curling back through it from the side.
                        float k = (t - wind) / (1f - wind);
                        var far = s.V + u * 130f;
                        if (k < 0.5f) s.Rel = Vector2.Lerp(orbit, far, 1f - (1f - k / 0.5f) * (1f - k / 0.5f));
                        else s.Rel = Bezier(far, s.V + across * side * 110f, s.V - u * 60f, (k - 0.5f) / 0.5f);
                    }
                    break;
                }
                case Pattern.Orbit:
                {
                    // Gathered: round the player, each its own place on a slowly turning ring, a little bob.
                    s.Phase += 2.2f;
                    float a = (s.Phase + s.Index * 22.5f) * Mathf.Deg2Rad;
                    float r = 64f + (s.Index % 3) * 14f;
                    s.Rel = new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r * 0.6f - 24f);
                    break;
                }
                case Pattern.Converge:
                {
                    // From its place on the ring straight through its enemy, curling back through it from the side, then home.
                    var far = s.V + Unit(s.V - s.From) * 140f;
                    float sideOf = s.Index % 2 == 0 ? 1f : -1f;
                    if (t < 0.35f) s.Rel = Vector2.Lerp(s.From, far, 1f - (1f - t / 0.35f) * (1f - t / 0.35f));
                    else if (t < 0.72f) s.Rel = Bezier(far, s.V + across * sideOf * 120f, s.V - Unit(s.V - s.From) * 50f, (t - 0.35f) / 0.37f);
                    else s.Rel = Vector2.Lerp(s.V - Unit(s.V - s.From) * 50f, Vector2.zero, (t - 0.72f) / 0.28f);
                    break;
                }
            }
            var moved = s.Rel - was;
            if (s.Pattern == Pattern.Orbit) s.BladeAngle = Mathf.Atan2(s.Rel.y, s.Rel.x);
            else if (moved.sqrMagnitude > 0.25f && s.TrailCount > 0) s.BladeAngle = Mathf.Atan2(moved.y, moved.x);
            else if (s.TrailCount == 0) s.BladeAngle = Mathf.Atan2(u.y, u.x);
            s.Spin = s.Pattern == Pattern.Orbit ? 1 : side;
            float shown = s.T - (s.Pattern == Pattern.Cross ? s.Phase : 0f);
            s.Opacity = s.Pattern == Pattern.Orbit || s.Pattern == Pattern.Converge && s.T < 5f ? 1f : Mathf.Clamp01(shown / 5f) * Mathf.Clamp01((end - s.T) / 8f);
            if (s.Pattern != Pattern.Orbit && s.T < 60f) Sparks(s, t);
            int n = Mathf.Min(s.TrailCount, TrailLength - 1);
            System.Array.Copy(s.TrailPos, 0, s.TrailPos, 1, n);
            System.Array.Copy(s.TrailAngle, 0, s.TrailAngle, 1, n);
            s.TrailPos[0] = s.Rel;
            s.TrailAngle[0] = s.BladeAngle;
            s.TrailCount = n + 1;
        }

        /// <summary>A rain sword striking the ground: a ring of motes of its colour thrown out flat, a few up.</summary>
        void Landing(Sword s)
        {
            var fall = Unit(s.Rel - s.TrailPos[s.TrailCount > 1 ? 1 : 0]);
            DevRainTips.Add(TerraParticles.ToWorld(playerPx + s.Rel + fall * s.Profile.Blade / 2f));
            var dust = TerraParticles.Instance;
            if (dust == null) return;
            // Round its point: the spot it is stuck in.
            var at = s.Anchored ? s.Anchor : playerPx + s.Rel;
            for (int k = 0; k < 18; k++)
            {
                float a = k / 18f * Mathf.PI * 2f;
                int d = dust.NewDust(at, 0, 0, 278, 0f, 0f, 100);
                if (d < 0) continue;
                ref var m = ref dust[d];
                m.Position = at;
                m.Velocity = new Vector2(Mathf.Cos(a) * 4f, Mathf.Sin(a) * 1.4f - 0.6f);
                m.Color = Color.Lerp(s.Profile.Trail, Color.white, Random.value * 0.35f);
                m.Scale = 0.6f;
                m.FadeIn = 0.6f + Random.value * 0.3f;
                m.NoGravity = true;
            }
        }

        // ------------------------------------------------------------------ the right button: swords gathered, then loosed

        ItemDef gatherItem;

        /// <summary>While the right button charges the Zenith's art: swords of its kinds gathered round the player, more as it fills.</summary>
        void Gather()
        {
            bool charging = BladeSkills.Charging && BladeSkills.ChargeItem == ItemType.Zenith && BladeArts.Enabled;
            int have = 0;
            foreach (var s in swords) if (s.Active && s.Pattern == Pattern.Orbit) have++;
            if (!charging)
            {
                // Let go short of a loosing (or the charge dropped): the gathered ones fade back.
                if (have > 0) foreach (var s in swords) if (s.Active && s.Pattern == Pattern.Orbit) Stop(s);
                return;
            }
            // Sixteen at a full charge: loosed as a volley, about 1.15 times the swing's damage over the charge and the flight.
            int want = 3 + Mathf.FloorToInt(13f * BladeSkills.Charge);
            if (gatherItem == null) gatherItem = ItemDb.Get(ItemType.Zenith);
            while (have < want)
            {
                var s = Get();
                s.Active = true;
                s.Root.SetActive(true);
                s.Pattern = Pattern.Orbit;
                s.Index = have;
                s.Phase = Time.time * 120f;
                s.T = 30f;
                s.TrailCount = 0;
                s.Cooldowns.Clear();
                s.Item = gatherItem;
                s.D = Vector2.zero;
                s.V = Vector2.right;
                s.Profile = have == 0 ? profiles[0] : profiles[1 + (have - 1) % (profiles.Length - 2)];
                s.Main.sprite = SwordSprite(s.Profile.Item);
                s.Glow.sprite = s.Main.sprite;
                foreach (var e in s.Echoes) e.sprite = s.Main.sprite;
                have++;
                DevGathered++;
                if (have % 3 == 0) Chime(0.5f + 0.1f * have / 3f);
            }
        }

        void Chime(float pitchUp)
        {
            var clip = Terraria.TerrariaAssets.Sound("Item_169");
            if (voice.outputAudioMixerGroup == null && TerraMod.Instance != null) voice.outputAudioMixerGroup = TerraMod.Instance.SoundGroup;
            voice.Stop();
            voice.clip = clip;
            voice.pitch = Mathf.Pow(2f, -0.4f + pitchUp * 0.6f);
            voice.Play();
        }

        /// <summary>The gathered swords loosed: each straight through an enemy by the cursor (in turn), else round the cursor's spot; true when any flew.</summary>
        public bool Loose(Vector2 aimWorld)
        {
            var targets = new System.Collections.Generic.List<Vector2>();
            var aim = TerraParticles.ToPx(aimWorld) - playerPx;
            foreach (var col in Physics2D.OverlapCircleAll(aimWorld, 300f * Px, 1 << EnemyLayer))
            {
                var hm = col.GetComponentInParent<HealthManager>();
                if (hm == null || hm.isDead || !hm.gameObject.activeInHierarchy || EnemyStats.Untouchable(hm) || World.ArenaGuard.Outside(hm)) continue;
                var c = TerraParticles.ToPx(col.bounds.center) - playerPx;
                if (!targets.Contains(c)) targets.Add(c);
            }
            int i = 0;
            foreach (var s in swords)
            {
                if (!s.Active || s.Pattern != Pattern.Orbit) continue;
                var to = targets.Count > 0 ? targets[i % targets.Count] : aim + Random.insideUnitCircle * 60f;
                s.Pattern = Pattern.Converge;
                s.From = s.Rel;
                s.V = to + Random.insideUnitCircle * 12f;
                s.D = s.V / 2f;
                s.Index = i;
                // A volley: each leaves the ring a moment after the last.
                s.T = -i * 1.6f;
                s.Cooldowns.Clear();
                i++;
                DevLoosed++;
            }
            if (i > 0)
            {
                DevPattern = NameOf(Pattern.Converge);
                Chime(1.2f);
            }
            return i > 0;
        }
    }
}
