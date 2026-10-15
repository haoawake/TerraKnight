using HarmonyLib;
using TerraKnight.Effects;
using TerraKnight.Terraria;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight.Player
{
    /// <summary>
    /// A new game's start: no fall down King's Pass. The moment the game sets the player at the top
    /// entrance (hidden, as it does before the fall), the player is put on the ground straight below
    /// with the camera, and once the game has finished bringing the player in, a summoning plays:
    /// the Old One's Army's mysterious portal (NPC_549, its eight frames) swirls open with violet
    /// motes drawn into it and its glow (Extra_59), the player walks out of it fading in, and it
    /// folds shut in a flash: a ring (Extra_174), star sparkles and a burst of motes. The portal's
    /// own sounds (dd2_etherian_portal_open, _spawn_enemy, _dryad_touch). Control comes back after.
    /// A pylon's teleport arrives the same way (<see cref="Summon"/>), beside the pylon, the portal
    /// in the area's own colours: the portal recoloured from its violet, its glow, motes, ring and
    /// sparkles in that area's palette.
    /// </summary>
    [HarmonyPatch]
    internal static class OpeningPortal
    {
        enum Phase { None, Waiting, Opening, Holding, Emerging, Closing }

        static Phase phase;
        static float time, frameTicks, dustTicks;
        static int frame;
        static string scene;
        static Vector2 feet;
        static bool tookControl, fromPylon, afterEntry;
        static int walkDir = 1;
        /// <summary>The palette now: the portal's texture (recoloured for an area), its main colour and two more.</summary>
        static string portalTexture = "NPC_549";
        static Color c1 = Violet, c2 = Pink, c3 = Cyan;
        const float PortalScale = 0.55f, OpenTime = 0.8f, EmergeTime = 1.0f, CloseTime = 0.7f;
        /// <summary>A pylon's portal: quick, the whole trip about a second.</summary>
        const float PylonOpenTime = 0.2f, PylonEmergeTime = 0.3f;
        const float Px = TerraParticles.Px;
        static readonly Color Violet = new Color(0.72f, 0.38f, 1f), Pink = new Color(1f, 0.45f, 0.85f), Cyan = new Color(0.45f, 0.85f, 1f);

        public static int DevRuns, DevSummons;
        public static string DevPalette => $"{portalTexture} {c1}";
        public static Vector2 DevFeet => feet;
        public static bool Active => phase != Phase.None;
        public static string DevPhase => phase.ToString();

        /// <summary>The Terraria player isn't drawn until it comes out of the portal.</summary>
        public static bool HideHero => phase == Phase.Waiting || phase == Phase.Opening || phase == Phase.Holding || World.Pylons.Arriving;

        /// <summary>Fading in as it steps out.</summary>
        public static float HeroOpacity => phase == Phase.Emerging ? Mathf.Clamp01(time / (fromPylon ? PylonEmergeTime : 0.45f)) : 1f;

        [HarmonyPostfix]
        [HarmonyPatch(typeof(HeroController), "SendHeroInPosition")]
        static void AfterInPosition(HeroController __instance)
        {
            var gm = GameManager.instance;
            World.Pylons.WatchRespawnFade();
            // A pylon's teleport arriving: beside the pylon, out of its portal.
            if (World.Pylons.Arrive(__instance)) return;
            // A boss seal's: in by the arena's door, then out of its portal.
            if (Items.BossSummons.Arrive(__instance)) return;
            if (gm == null || __instance == null || gm.sceneName != "Tutorial_01" || gm.entryGateName != "top1" || phase != Phase.None) return;
            var col = __instance.GetComponent<BoxCollider2D>();
            Vector2 from = col.bounds.center;
            var hit = Physics2D.Raycast(from, Vector2.down, 300f, 1 << ChunkColliders.TerrainLayer);
            if (hit.collider == null) return;
            float lift = __instance.transform.position.y - col.bounds.min.y;
            var p = __instance.transform.position;
            __instance.transform.position = new Vector3(from.x, hit.point.y + lift + 0.02f, p.z);
            var rb = __instance.GetComponent<Rigidbody2D>();
            if (rb != null) rb.linearVelocity = Vector2.zero;
            gm.cameraCtrl?.PositionToHero(true);
            // The opening's long hold at the top entrance (its entryDelay) has nothing left to wait for.
            if (__instance.sceneEntryGate != null)
            {
                __instance.sceneEntryGate.entryDelay = 0f;
                // Nor its hard landing: the player steps out of the portal, not off a fall.
                __instance.sceneEntryGate.hardLandOnExit = false;
            }
            feet = new Vector2(from.x, hit.point.y);
            scene = gm.sceneName;
            SetPalette(null);
            fromPylon = false;
            walkDir = 1;
            phase = Phase.Waiting;
            time = 0f;
            tookControl = false;
            Plugin.Log.LogInfo($"[开局] 不再从顶上落下：直接放到地面 {feet:F2}，等进场结束后从传送门召唤出来");
        }

        /// <summary>
        /// A pylon's arrival: the player, hidden, stands at feet; the portal opens in the area's
        /// colours once the game has brought him in, and he walks out of it toward dir.
        /// </summary>
        public static void Summon(HeroController hero, Vector2 at, string area, int dir)
        {
            var gm = GameManager.instance;
            if (gm == null || hero == null) return;
            if (phase != Phase.None) End(hero);
            feet = at;
            scene = gm.sceneName;
            SetPalette(area);
            fromPylon = true;
            walkDir = dir >= 0 ? 1 : -1;
            phase = Phase.Waiting;
            time = 0f;
            tookControl = false;
            DevSummons++;
            Plugin.Log.LogInfo($"[晶塔] 从{area}的传送门里走出来：{feet:F2}，朝{(walkDir > 0 ? "右" : "左")}");
        }

        /// <summary>
        /// A boss seal's arrival (Items.BossSummons): the game brings the player in by the arena's own door (its run
        /// in, drop or jump, ending where the game itself sets him down), unseen; then the portal opens where he
        /// stands, control held, and he steps out of it.
        /// </summary>
        public static void SummonAfterEntry(HeroController hero, string area)
        {
            var gm = GameManager.instance;
            if (gm == null || hero == null) return;
            if (phase != Phase.None) End(hero);
            scene = gm.sceneName;
            SetPalette(area);
            fromPylon = true;
            afterEntry = true;
            walkDir = hero.cState.facingRight ? 1 : -1;
            phase = Phase.Waiting;
            time = 0f;
            tookControl = false;
            DevSummons++;
        }

        /// <summary>Each area's portal colour (the opening's violet when none): Greenpath's green, the City's blue, Crystal Peak's pink...</summary>
        internal static Color AreaColor(string area)
        {
            switch (area)
            {
                case "国王山道": case "德特茅斯": case "呼啸悬崖": return new Color(0.7f, 0.82f, 1f);
                case "遗忘十字路": return new Color(0.45f, 0.62f, 1f);
                case "苍绿之径": return new Color(0.3f, 1f, 0.42f);
                case "王后花园": return new Color(0.25f, 0.9f, 0.7f);
                case "雾之峡谷": return new Color(1f, 0.5f, 0.95f);
                case "真菌荒地": return new Color(0.8f, 0.95f, 0.25f);
                case "泪水之城": return new Color(0.3f, 0.5f, 1f);
                case "皇家水道": return new Color(0.4f, 0.9f, 0.55f);
                case "水晶山峰": return new Color(1f, 0.4f, 0.8f);
                case "安息之地": return new Color(1f, 0.92f, 0.6f);
                case "深巢": case "野兽巢穴": return new Color(0.62f, 0.55f, 0.85f);
                case "远古盆地": return new Color(0.6f, 0.68f, 0.9f);
                case "王国边缘": return new Color(1f, 0.72f, 0.5f);
                case "蜂巢": return new Color(1f, 0.72f, 0.15f);
                case "深渊": case "黑卵神殿": return new Color(0.5f, 0.4f, 0.9f);
                case "白色宫殿": return new Color(1f, 1f, 0.88f);
                case "神居": return new Color(1f, 0.85f, 0.4f);
                case "愚人斗兽场": return new Color(1f, 0.45f, 0.3f);
                case "梦境": return new Color(1f, 0.62f, 0.3f);
                case "格林剧团": return new Color(1f, 0.25f, 0.25f);
                default: return Violet;
            }
        }

        static void SetPalette(string area)
        {
            if (string.IsNullOrEmpty(area))
            {
                portalTexture = "NPC_549";
                c1 = Violet;
                c2 = Pink;
                c3 = Cyan;
                return;
            }
            c1 = AreaColor(area);
            Color.RGBToHSV(c1, out float h, out float s, out float v);
            c2 = Color.Lerp(c1, Color.white, 0.45f);
            c3 = Color.HSVToRGB(Mathf.Repeat(h + 0.08f, 1f), s * 0.8f, 1f);
            portalTexture = "NPC_549@" + area;
            if (!TerrariaAssets.Has(portalTexture)) TerrariaAssets.Put(portalTexture, Recolored(c1));
        }

        /// <summary>The portal's frames in another colour: each pixel's brightness laid on a ramp from the colour, dark, to nearly white.</summary>
        static Texture2D Recolored(Color main)
        {
            var px = TerrariaAssets.Pixels("NPC_549", out int w, out int h);
            var light = Color.Lerp(main, Color.white, 0.8f);
            var dark = main * 0.18f;
            for (int i = 0; i < px.Length; i++)
            {
                var p = px[i];
                if (p.a == 0) continue;
                float v = Mathf.Max(p.r, Mathf.Max(p.g, p.b)) / 255f;
                var c = v < 0.55f ? Color.Lerp(dark, main, v / 0.55f) : Color.Lerp(main, light, (v - 0.55f) / 0.45f);
                px[i] = new Color32((byte)(Mathf.Clamp01(c.r) * 255f), (byte)(Mathf.Clamp01(c.g) * 255f), (byte)(Mathf.Clamp01(c.b) * 255f), p.a);
            }
            return TerrariaAssets.Make("NPC_549 recoloured", px, w, h);
        }

        /// <summary>Each frame (TerraMod): the sequence, its drawing and its effects.</summary>
        public static void Tick(HeroController hero)
        {
            if (phase == Phase.None) return;
            var gm = GameManager.UnsafeInstance;
            if (hero == null || gm == null || gm.sceneName != scene || hero.cState.dead)
            {
                End(hero);
                return;
            }
            float dt = Mathf.Min(Time.deltaTime, 0.1f);
            time += dt;
            hero.cState.willHardLand = false;
            var rb = hero.GetComponent<Rigidbody2D>();
            switch (phase)
            {
                case Phase.Waiting:
                    if (afterEntry)
                    {
                        // In by the door first; the portal then opens where he stands, control held through it.
                        if (hero.cState.transitioning && time < 8f) return;
                        afterEntry = false;
                        var col = hero.GetComponent<BoxCollider2D>();
                        feet = new Vector2(col.bounds.center.x, col.bounds.min.y);
                        walkDir = hero.cState.facingRight ? 1 : -1;
                        hero.RelinquishControl();
                        tookControl = true;
                        if (rb != null) rb.linearVelocity = Vector2.zero;
                        time = 0f;
                        Plugin.Log.LogInfo($"[首领召唤物] 进场后从传送门里走出来：{feet:F2}");
                    }
                    // The portal opens as the scene fades in (a pylon's at once).
                    if (time >= (fromPylon ? 0f : 0.35f))
                    {
                        Next(Phase.Opening);
                        TerraMod.Instance?.PlaySound("Custom/dd2_etherian_portal_open", 0.8f);
                    }
                    return;
                case Phase.Opening:
                    if (time >= (fromPylon ? PylonOpenTime : OpenTime)) Next(Phase.Holding);
                    break;
                case Phase.Holding:
                    // Open and turning until the game's own entry (the drop it thinks is a fall, a respawn's
                    // getting up) is over; control held by it after that comes back when the summoning hands it back.
                    if ((!hero.cState.transitioning && !(fromPylon && hero.controlReqlinquished && !tookControl) && time > (fromPylon ? 0f : 0.15f)) || time > 8f)
                    {
                        hero.RelinquishControl();
                        tookControl = true;
                        if (walkDir > 0) hero.FaceRight();
                        else hero.FaceLeft();
                        Next(Phase.Emerging);
                        TerraMod.Instance?.PlaySound("Custom/dd2_etherian_portal_spawn_enemy_0", 0.8f);
                    }
                    break;
                case Phase.Emerging:
                    // Steps out, walking (out of a pylon's, fading in where he stands).
                    if (rb != null && !fromPylon) rb.linearVelocity = new Vector2(time > 0.1f && time < 0.75f ? 4f * walkDir : 0f, rb.linearVelocity.y);
                    if (time >= (fromPylon ? PylonEmergeTime : EmergeTime))
                    {
                        if (rb != null) rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
                        Next(Phase.Closing);
                        // A pylon's traveller moves on at once; the portal folds shut behind.
                        if (fromPylon && tookControl && !hero.cState.dead)
                        {
                            hero.RegainControl();
                            tookControl = false;
                            World.Pylons.DevTravelTook = Time.realtimeSinceStartup - World.Pylons.DevTravelStart;
                            Plugin.Log.LogInfo($"[晶塔] 点下到能动 {World.Pylons.DevTravelTook:F2}s");
                        }
                        Burst();
                        TerraMod.Instance?.PlaySound("Custom/dd2_etherian_portal_dryad_touch", 0.9f);
                    }
                    break;
                case Phase.Closing:
                    if (time >= CloseTime)
                    {
                        End(hero);
                        return;
                    }
                    break;
            }
            Draw(dt);
        }

        static void Next(Phase p)
        {
            phase = p;
            time = 0f;
        }

        static void End(HeroController hero)
        {
            if (phase != Phase.None && tookControl && hero != null && !hero.cState.dead)
            {
                hero.RegainControl();
                hero.AcceptInput();
            }
            if (phase != Phase.None) DevRuns++;
            phase = Phase.None;
            tookControl = false;
            fromPylon = false;
        }

        /// <summary>How open the portal is (0 shut, 1 full), easing out with a little overshoot, and closing in.</summary>
        static float Openness()
        {
            switch (phase)
            {
                case Phase.Opening:
                    float x = Mathf.Clamp01(time / OpenTime) - 1f;
                    const float c1 = 1.70158f, c3 = c1 + 1f;
                    return 1f + c3 * x * x * x + c1 * x * x;
                case Phase.Holding:
                case Phase.Emerging: return 1f + 0.03f * Mathf.Sin(Time.time * 9f);
                case Phase.Closing:
                    float k = Mathf.Clamp01(time / CloseTime);
                    return 1f - k * k;
                default: return 0f;
            }
        }

        static Vector2 CentrePx => TerraParticles.ToPx(feet) - new Vector2(0f, 204f * PortalScale * 0.5f - 6f);

        static void Draw(float dt)
        {
            var fx = FxBatch.Instance;
            var parts = TerraParticles.Instance;
            if (fx == null) return;
            float open = Mathf.Max(0f, Openness());
            var c = CentrePx;
            frameTicks += dt * 60f;
            if (frameTicks >= 5f)
            {
                frameTicks = 0f;
                frame = (frame + 1) % 8;
            }
            if (open > 0.01f)
            {
                float pulse = 0.85f + 0.15f * Mathf.Sin(Time.time * 6f);
                // The glow behind, then the portal itself, then its fire added over it once more.
                fx.Draw(FxBatch.Whole("Extra_59"), c, new Color(c1.r * 0.55f * pulse, c1.g * 0.55f * pulse, c1.b * 0.55f * pulse, 0f), 0f, new Vector2(1.5f * open, 1.9f * open));
                var sprite = FxBatch.Frame(portalTexture, 0, 204 * frame, 142, 204, 71f, 102f);
                fx.Draw(sprite, c, Color.white, 0f, new Vector2(PortalScale * open, PortalScale * Mathf.Min(open * 1.15f, 1.05f)));
                fx.Draw(sprite, c, new Color(c1.r * 0.48f * pulse, c1.g * 0.48f * pulse, c1.b * 0.48f * pulse, 0f), 0f, new Vector2(PortalScale * open * 1.04f, PortalScale * Mathf.Min(open * 1.2f, 1.1f)));
                fx.Light(c, c1 * (0.85f * open), 6f);
            }
            // Motes swirling in toward the centre while it is open.
            dustTicks += dt * 60f;
            if (parts != null && open > 0.2f && phase != Phase.Closing)
                for (; dustTicks >= 1f; dustTicks -= 1f)
                    for (int n = 0; n < 3; n++)
                    {
                        float a = Random.value * Mathf.PI * 2f, r = Random.Range(60f, 95f) * open;
                        var at = c + new Vector2(Mathf.Cos(a) * r * 0.65f, Mathf.Sin(a) * r);
                        int d = parts.NewDust(at, 0, 0, Random.Range(0, 3) == 0 ? 15 : 27, 0f, 0f, 0, Random.Range(0.9f, 1.5f));
                        if (d < 0) continue;
                        ref var dust = ref parts[d];
                        dust.Position = at;
                        var inward = (c - at).normalized;
                        dust.Velocity = inward * Random.Range(1.6f, 2.6f) + new Vector2(-inward.y, inward.x) * 2.2f;
                        dust.NoGravity = true;
                        dust.Additive = true;
                        var tint = n == 0 ? c1 : n == 1 ? c2 : c3;
                        dust.Color = new Color(tint.r, tint.g, tint.b, 1f);
                    }
            else dustTicks = 0f;
            // Star sparkles about the rim, now and then.
            if (open > 0.3f && Random.value < dt * 10f)
            {
                float a = Random.value * Mathf.PI * 2f;
                var at = c + new Vector2(Mathf.Cos(a) * 40f * open, Mathf.Sin(a) * 58f * open);
                fx.Sparkle(1f, at, Color.white, Random.value < 0.5f ? c2 : c3, 0.5f, 0f, 0.5f, 0.5f, 1f, 0f, new Vector2(1.6f, 1.6f), new Vector2(1f, 1f));
            }
            // The closing ring.
            if (phase == Phase.Closing)
            {
                float k = Mathf.Clamp01(time / CloseTime);
                float s = 0.4f + 1.6f * k, fade = 1f - k;
                fx.Draw(FxBatch.Whole("Extra_174"), c, new Color(c1.r * fade, c1.g * fade, c1.b * fade, 0f), 0f, new Vector2(s * 0.8f, s));
                fx.Sparkle(1f, c, Color.white, c2, 0.5f, 0f, 0.5f, 0.5f, 1f, time * 2f, new Vector2(3f * fade + 0.5f, 3f * fade + 0.5f), new Vector2(1.5f, 1.5f));
            }
        }

        /// <summary>The flash as it shuts: motes flung out all round in the portal's colours.</summary>
        static void Burst()
        {
            var parts = TerraParticles.Instance;
            if (parts == null) return;
            var c = CentrePx;
            for (int i = 0; i < 60; i++)
            {
                float a = Random.value * Mathf.PI * 2f, v = Random.Range(2.5f, 7f);
                int d = parts.NewDust(c, 0, 0, i % 3 == 0 ? 15 : 27, 0f, 0f, 0, Random.Range(1.1f, 1.8f));
                if (d < 0) continue;
                ref var dust = ref parts[d];
                dust.Position = c + new Vector2(Mathf.Cos(a) * 10f, Mathf.Sin(a) * 16f);
                dust.Velocity = new Vector2(Mathf.Cos(a) * v * 0.8f, Mathf.Sin(a) * v);
                dust.NoGravity = true;
                dust.Additive = true;
                var tint = i % 3 == 0 ? c3 : i % 3 == 1 ? c2 : c1;
                dust.Color = new Color(tint.r, tint.g, tint.b, 1f);
            }
        }
    }
}
