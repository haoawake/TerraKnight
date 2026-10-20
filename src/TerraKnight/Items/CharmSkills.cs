using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// The charm skill key (TerraKeys.Action.CharmSkill, C by default): a worn charm that has a skill uses it. Each skill
    /// has its cooldown, shown as a debuff with its time in the buff bar (the charm's own picture). Only with the charm page.
    ///
    /// The Gathering Swarm's: a ripple of half-clear white light spreads from the player over the whole room the player is
    /// in, and every item lying there that it reaches is picked up as one touching the player would be (as far as there is
    /// room); the rooms round it keep theirs. Five seconds before it can be used again.
    /// </summary>
    internal static class CharmSkills
    {
        public const int GatheringSwarm = 1;
        /// <summary>A charm's skill cooldown is the debuff of this number plus the charm's.</summary>
        public const int CooldownBase = 9100;
        const float SwarmCooldown = 5f;
        /// <summary>The ripple's speed (units a second) as it sets out, and as it reaches the room's far corner: it gathers pace like a wave.</summary>
        const float RippleStartSpeed = 3f, RippleEndSpeed = 45f, RippleMinTime = 1.2f, RippleMaxTime = 4f;

        static GameObject ripple;
        static Mesh rippleMesh;
        static Material rippleMaterial;
        static Vector2 rippleAt;
        static float rippleStart, rippleTime, rippleReach, rippleV0, rippleV1;
        static string rippleScene;
        public static int DevUses, DevTaken;

        static bool Cooling(int charm) => Player.PlayerBuffs.Has(CooldownBase + charm);

        static void Cool(int charm, float seconds)
        {
            Player.PlayerBuffs.Define(CooldownBase + charm, Charms.Name(charm) + Lang.T("冷却"), Lang.T("护符技冷却"), debuff: true);
            Player.PlayerBuffs.Add(CooldownBase + charm, Mathf.RoundToInt(seconds * 60f));
        }

        /// <summary>The cooldown debuff's picture: the charm's (TerrariaAssets asks for Buff_9101 and the like).</summary>
        public static Texture2D CooldownIcon(string buffName)
        {
            if (!int.TryParse(buffName.Substring("Buff_".Length), out int type)) return null;
            int charm = type - CooldownBase;
            return charm >= 1 && charm <= Charms.Count ? Charms.Icon(charm) : null;
        }

        public static bool IsCooldownBuff(string name) =>
            name != null && name.StartsWith("Buff_") && int.TryParse(name.Substring(5), out int t) && t > CooldownBase && t <= CooldownBase + Charms.Count;

        /// <summary>Every frame in play: the ripple spreading, and the key.</summary>
        public static void Tick(HeroController hero)
        {
            TickRipple();
            if (hero == null || !Charms.Enabled || Net.NetChat.Typing || hero.cState.dead) return;
            if (!Player.TerraKeys.Pressed(Player.TerraKeys.Action.CharmSkill) && !DevPress) return;
            DevPress = false;
            if (!GameHooks.PlayerHasControl()) return;
            if (Charms.Worn(GatheringSwarm) && !Cooling(GatheringSwarm)) Swarm(hero);
        }

        /// <summary>What the charm's skill does, for its tooltip (null: it has none).</summary>
        public static string Describe(int charm)
        {
            string key = Player.TerraKeys.KeyName(Player.TerraKeys.Get(Player.TerraKeys.Action.CharmSkill));
            return charm == GatheringSwarm ? Lang.F("护符技（{0}）：从身上荡漾出一圈光晕扫过整个房间，捡起碰到的所有掉落物，只在当前房间。冷却 5 秒", key) : null;
        }

        /// <summary>Dev: the key pressed once.</summary>
        public static bool DevPress;

        // ------------------------------------------------------------------ the Gathering Swarm

        static void Swarm(HeroController hero)
        {
            var room = World.TerraWorld.Live;
            if (room == null) return;
            rippleAt = hero.GetComponent<BoxCollider2D>().bounds.center;
            rippleScene = room.SceneName;
            // Far enough to reach the room's farthest corner.
            Vector2 lo = (Vector2)room.CellOffset * World.CellGrid.CellSize, hi = lo + new Vector2(room.Grid.Width, room.Grid.Height) * World.CellGrid.CellSize;
            rippleReach = 0f;
            foreach (var c in new[] { lo, hi, new Vector2(lo.x, hi.y), new Vector2(hi.x, lo.y) })
                rippleReach = Mathf.Max(rippleReach, (c - rippleAt).magnitude);
            // v(t) = v0 + (v1 − v0)(t/T)²: the reach is T (v0 + (v1 − v0) / 3). A room too big for 4 s, or so small it'd be
            // over in a moment, has the whole curve sped up or slowed down to fit.
            float natural = rippleReach / (RippleStartSpeed + (RippleEndSpeed - RippleStartSpeed) / 3f);
            rippleTime = Mathf.Clamp(natural, RippleMinTime, RippleMaxTime);
            float k = natural / rippleTime;
            rippleV0 = RippleStartSpeed * k;
            rippleV1 = RippleEndSpeed * k;
            rippleStart = Time.time;
            EnsureRipple();
            ripple.SetActive(true);
            Cool(GatheringSwarm, SwarmCooldown);
            TerraMod.Instance?.PlaySound("Item_4", 0.6f);
            DevUses++;
            Plugin.Log.LogInfo($"[护符技] 蜂群集结：从 {rippleAt:F1} 扩散到 {rippleReach:F1}，{rippleTime:F2} 秒，只在 {rippleScene}");
        }

        /// <summary>How far the wave has come t seconds out: the integral of its speed.</summary>
        static float RippleRadius(float t)
        {
            float T = rippleTime;
            return rippleV0 * t + (rippleV1 - rippleV0) * t * t * t / (3f * T * T);
        }

        static void TickRipple()
        {
            if (ripple == null || !ripple.activeSelf) return;
            float s = Time.time - rippleStart;
            if (s >= rippleTime || World.TerraWorld.Live?.SceneName != rippleScene)
            {
                // The last of it reaches the far corners.
                if (s >= rippleTime) DevTaken += WorldItems.SweepPickup(rippleAt, rippleReach, rippleScene);
                ripple.SetActive(false);
                return;
            }
            float radius = Mathf.Max(0.2f, RippleRadius(s));
            DevTaken += WorldItems.SweepPickup(rippleAt, radius, rippleScene);
            float u = s / rippleTime;
            // In over the first quarter second, out over the last fifth of the way.
            float alpha = Mathf.Clamp01(s / 0.25f) * Mathf.Clamp01((1f - u) / 0.2f);
            ripple.transform.position = new Vector3(rippleAt.x, rippleAt.y, -0.5f);
            BuildWaves(radius, alpha);
        }

        const int Segments = 160;
        static readonly System.Collections.Generic.List<Vector3> waveVerts = new System.Collections.Generic.List<Vector3>();
        static readonly System.Collections.Generic.List<Color> waveColors = new System.Collections.Generic.List<Color>();
        static readonly System.Collections.Generic.List<int> waveTris = new System.Collections.Generic.List<int>();

        /// <summary>
        /// The wave as a ring of fixed soft width (not a picture scaled up, whose rim would thicken and harden as it grew):
        /// clear at both edges, brightest a little behind the front, widening a little as it spreads; a fainter swell after it.
        /// </summary>
        static void BuildWaves(float radius, float alpha)
        {
            waveVerts.Clear();
            waveColors.Clear();
            waveTris.Clear();
            float width = Mathf.Lerp(0.9f, 2.2f, Mathf.Clamp01(radius / 40f));
            Wave(radius, width, 0.42f * alpha);
            float after = radius - width * 1.6f;
            if (after > 0.3f) Wave(after, width * 0.8f, 0.16f * alpha);
            rippleMesh.Clear();
            rippleMesh.SetVertices(waveVerts);
            rippleMesh.SetColors(waveColors);
            rippleMesh.SetTriangles(waveTris, 0);
            rippleMesh.RecalculateBounds();
        }

        /// <summary>One ring: outer edge at the radius, its brightest line a third of the way in, the inner edge clear.</summary>
        static void Wave(float outer, float width, float a)
        {
            float inner = Mathf.Max(0f, outer - width), mid = outer - width * 0.35f;
            int b = waveVerts.Count;
            var clear = new Color(1f, 1f, 1f, 0f);
            var lit = new Color(1f, 1f, 1f, a);
            for (int i = 0; i <= Segments; i++)
            {
                float ang = i * Mathf.PI * 2f / Segments;
                var d = new Vector3(Mathf.Cos(ang), Mathf.Sin(ang), 0f);
                waveVerts.Add(d * inner);
                waveVerts.Add(d * mid);
                waveVerts.Add(d * outer);
                waveColors.Add(clear);
                waveColors.Add(lit);
                waveColors.Add(clear);
            }
            for (int i = 0; i < Segments; i++)
            {
                int v = b + i * 3, w = v + 3;
                for (int band = 0; band < 2; band++)
                {
                    waveTris.Add(v + band); waveTris.Add(w + band); waveTris.Add(w + band + 1);
                    waveTris.Add(v + band); waveTris.Add(w + band + 1); waveTris.Add(v + band + 1);
                }
            }
        }

        static void EnsureRipple()
        {
            if (ripple != null) return;
            ripple = new GameObject("TerraKnight Gathering Ripple");
            Object.DontDestroyOnLoad(ripple);
            rippleMesh = new Mesh { name = "Gathering Ripple" };
            rippleMesh.MarkDynamic();
            ripple.AddComponent<MeshFilter>().sharedMesh = rippleMesh;
            var mr = ripple.AddComponent<MeshRenderer>();
            rippleMaterial = new Material(Shader.Find("Sprites/Default")) { name = "TerraKnight Gathering Ripple" };
            mr.sharedMaterial = rippleMaterial;
            // Over the light map: a glow, not darkened with the room.
            mr.sortingOrder = World.LightMap.Order + 2;
        }
    }
}
