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

        internal static bool Cooling(int charm) => Player.PlayerBuffs.Has(CooldownBase + charm);

        /// <summary>How long a press of the key is kept while the game holds the player (seconds).</summary>
        const float PressKept = 0.4f;
        static float pressUntil = -1f, coolingSaidAt = -10f;
        public static int DevPressWaits, DevCoolingSaid;
        public static string DevCoolingText;

        /// <summary>The key pressed while the chosen skill cools: "护符技还剩 N 秒" floating up over the player.</summary>
        internal static void SayCooling(HeroController hero, int charm)
        {
            if (hero == null || Time.unscaledTime - coolingSaidAt < 0.25f) return;
            coolingSaidAt = Time.unscaledTime;
            int left = Mathf.Max(1, Mathf.CeilToInt(Player.PlayerBuffs.TimeLeft(CooldownBase + charm) / 60f));
            var box = hero.GetComponent<BoxCollider2D>();
            var at = box != null ? new Vector3(box.bounds.center.x, box.bounds.max.y + 0.35f, hero.transform.position.z) : hero.transform.position + Vector3.up;
            DevCoolingText = Lang.F("护符技还剩 {0} 秒", left);
            UI.CombatText.Show(at, DevCoolingText, new Color(0.78f, 0.84f, 0.95f));
            DevCoolingSaid++;
        }

        internal static void Cool(int charm, float seconds)
        {
            Player.PlayerBuffs.Define(CooldownBase + charm, Charms.Name(charm) + Lang.T("冷却"), Lang.T("护符技冷却"), debuff: true);
            Player.PlayerBuffs.Add(CooldownBase + charm, Mathf.RoundToInt(seconds * 60f));
        }

        /// <summary>A charm's effect running for a while: a buff of this number plus the charm's, its picture and its time.</summary>
        public const int ActiveBase = 9200;

        internal static void Active(int charm, string name, string tip, float seconds)
        {
            Player.PlayerBuffs.Define(ActiveBase + charm, name, tip);
            Player.PlayerBuffs.Add(ActiveBase + charm, Mathf.Max(1, Mathf.RoundToInt(seconds * 60f)));
        }

        internal static void EndActive(int charm) { if (Player.PlayerBuffs.Has(ActiveBase + charm)) Player.PlayerBuffs.Remove(ActiveBase + charm); }

        /// <summary>A charm's state shown in the buff bar without a time while it holds (a mode, a lasting effect): kept there.</summary>
        internal static void Hold(int charm, string name, string tip)
        {
            Player.PlayerBuffs.Redefine(ActiveBase + charm, name, tip, noTime: true);
            if (!Player.PlayerBuffs.Has(ActiveBase + charm)) Player.PlayerBuffs.Add(ActiveBase + charm, Player.PlayerBuffs.Forever);
        }

        /// <summary>The cooldown debuff's picture: the charm's (TerrariaAssets asks for Buff_9101 and the like).</summary>
        public static Texture2D CooldownIcon(string buffName)
        {
            if (!int.TryParse(buffName.Substring("Buff_".Length), out int type)) return null;
            int charm = type > ActiveBase ? type - ActiveBase : type - CooldownBase;
            return charm >= 1 && charm <= Charms.Count ? Charms.Icon(charm) : null;
        }

        public static bool IsCooldownBuff(string name) =>
            Terraria.TerrariaAssets.NumberAfter(name, "Buff_", out int t) && (t > CooldownBase && t <= CooldownBase + Charms.Count || t > ActiveBase && t <= ActiveBase + Charms.Count);

        /// <summary>Every frame in play: the ripple spreading, and the key.</summary>
        public static void Tick(HeroController hero)
        {
            TickRipple();
            CharmEffects.Tick(hero);
            // The chosen one taken off: chosen no more.
            int chosen = Chosen;
            if (chosen > 0 && !Charms.Worn(chosen)) Choose(0);
            if (hero == null || !Charms.Enabled || PlayerChallenges.AccessoriesLocked || Net.NetChat.Typing || hero.cState.dead) { pressUntil = -1f; return; }
            if (Player.TerraKeys.Pressed(Player.TerraKeys.Action.CharmSkill) || DevPress) pressUntil = Time.unscaledTime + PressKept;
            DevPress = false;
            if (Time.unscaledTime > pressUntil) return;
            // A press while the game holds the player (a hit's recoil, a nail art, a scene's edge) waits a moment for control.
            if (!GameHooks.PlayerHasControl()) { DevPressWaits++; return; }
            pressUntil = -1f;
            chosen = Chosen;
            if (chosen <= 0)
            {
                // Skills worn and none chosen: how to choose one.
                if (AnySkillWorn()) UI.CenterNotice.Show(Lang.F("先在护符背包里按住 {0} 点击一个已装备的护符，把它收藏为护符技",
                    Player.TerraKeys.KeyName(Player.TerraKeys.Get(Player.TerraKeys.Action.Favorite))));
                return;
            }
            if (chosen == GatheringSwarm) { if (!Cooling(GatheringSwarm)) Swarm(hero); else SayCooling(hero, GatheringSwarm); return; }
            CharmEffects.Press(hero, chosen);
        }

        // ------------------------------------------------------------------ the one chosen

        /// <summary>The charms that have a skill.</summary>
        static readonly int[] withSkill = { GatheringSwarm, CharmEffects.Stalwart, CharmEffects.Baldur, CharmEffects.Fury, CharmEffects.LifebloodHeart, CharmEffects.SteadyBody,
            CharmEffects.SporeShroom, CharmEffects.SoulEater, CharmEffects.ShapeOfUnn, CharmEffects.DeepFocus };
        static readonly string[] chosenKey = new string[Charms.Count + 1];
        static string ChosenKey(int id) => chosenKey[id] ?? (chosenKey[id] = "charm.skill.chosen.v1." + id);

        public static bool HasSkill(int id) => System.Array.IndexOf(withSkill, id) >= 0;

        static bool AnySkillWorn()
        {
            foreach (int id in withSkill) if (Charms.Worn(id)) return true;
            return false;
        }

        /// <summary>
        /// The worn charm favourited in the charm bag (the favourite key held, a click on it): the one whose skill the key
        /// uses, one at a time, kept in the save. 0: none.
        /// </summary>
        public static int Chosen
        {
            get
            {
                var mod = TerraMod.Instance;
                if (mod == null) return 0;
                foreach (int id in withSkill) if (mod.GrantedAbilities.Contains(ChosenKey(id))) return id;
                return 0;
            }
        }

        /// <summary>This one chosen (0: none): the one chosen before, unchosen.</summary>
        public static void Choose(int id)
        {
            var mod = TerraMod.Instance;
            if (mod == null) return;
            foreach (int other in withSkill) mod.GrantedAbilities.Remove(ChosenKey(other));
            if (id > 0 && HasSkill(id)) mod.GrantedAbilities.Add(ChosenKey(id));
        }

        /// <summary>The charm skill key held now (the Baldur Shell's guard); Dev: held by a test.</summary>
        public static bool Held => !PlayerChallenges.AccessoriesLocked && (DevHeld || Player.TerraKeys.Held(Player.TerraKeys.Action.CharmSkill) && !Net.NetChat.Typing);
        public static bool DevHeld;

        /// <summary>What the charm's skill does, for its tooltip (null: it has none).</summary>
        public static string Describe(int charm)
        {
            string key = Player.TerraKeys.KeyName(Player.TerraKeys.Get(Player.TerraKeys.Action.CharmSkill));
            return charm == GatheringSwarm ? Lang.F("护符技（{0}）：从身上荡漾出一圈光晕扫过整个房间，捡起碰到的所有掉落物，只在当前房间。冷却 5 秒", key)
                // Passive (UI.WorldMap's compass).
                : charm == 2 ? Lang.T("戴着时：地图上指出最近的下一个首领（本阶段还没打过的），小地图上画出去那里的路线")
                : CharmEffects.Describe(charm, key);
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
            var hero = HeroController.SilentInstance;
            if (!Charms.Enabled || !Charms.Worn(GatheringSwarm) || hero == null || hero.cState.dead || World.TerraWorld.Live?.SceneName != rippleScene)
            {
                ripple.SetActive(false);
                return;
            }
            float s = Time.time - rippleStart;
            if (s >= rippleTime)
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
            alpha *= Effects.WeaponEffects.Opacity;
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
