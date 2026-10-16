using System.Collections.Generic;
using HarmonyLib;
using TerraKnight.Effects;
using TerraKnight.Player;
using UnityEngine;

namespace TerraKnight.Items
{
    internal static partial class ItemDb
    {
        /// <summary>
        /// The Shade Cloak and the Crystal Heart as accessories (Items.AbilityAccessories), given into the bag with the
        /// abilities as the Mothwing Cloak gives the Shield of Cthulhu (PlayerHooks.AbilityItems), drawn with the game's own
        /// pictures, reforged as any accessory; and the Hallownest Shield, the three in one (the Tinkerer's Workshop).
        /// </summary>
        static void AddAbilityAccessories(List<ItemDef> list)
        {
            list.Add(new ItemDef
            {
                Type = ItemType.ShadeCloak, Name = "暗影披风", Texture = HkItems.TexturePrefix + ItemType.ShadeCloak, Accessory = AccessoryEffect.Passive, Rare = 8,
                Tooltip = "按暗影冲刺键，或按住冲刺键时双击方向键：暗影冲刺\n冲刺时穿过敌人和它们的攻击，并能穿过暗影之门\n冷却 10 秒",
            });
            list.Add(new ItemDef
            {
                Type = ItemType.CrystalHeart, Name = "水晶之心", Texture = HkItems.TexturePrefix + ItemType.CrystalHeart, Accessory = AccessoryEffect.Passive, Rare = 5,
                Tooltip = "在地上或贴着墙长按冲刺键：蓄力后松开，像原版那样超级冲刺\n轻点冲刺键仍是普通冲刺\n冷却 20 秒",
            });
            list.Add(new ItemDef
            {
                Type = ItemType.HallownestShield, Name = "圣巢之盾", Texture = Relics.TexturePrefix + "item_hallownest_shield", Accessory = AccessoryEffect.ShieldDash,
                Bonus = new Bonuses { DashKind = 2 }, Defense = 4, Rare = 8,
                Tooltip = "点按冲刺键：克苏鲁护盾的冲刺，撞击敌人\n在地上或贴着墙长按冲刺键：水晶之心蓄力，松开超级冲刺（冷却 20 秒）\n按住冲刺键时双击方向键，或按暗影冲刺键：暗影冲刺，穿过敌人、攻击和暗影之门（冷却 10 秒）",
            });
        }

        static IEnumerable<(ItemType, int, ItemSource, int)> AbilityAccessorySources()
        {
            yield return (ItemType.CrystalHeart, 3, ItemSource.Drop, 0);
            yield return (ItemType.ShadeCloak, 5, ItemSource.Drop, 0);
            yield return (ItemType.HallownestShield, 5, ItemSource.Craft, 200000);
        }
    }

    internal static partial class AbilityAccessories
    {
        /// <summary>The Hallownest Shield: the Shield of Cthulhu, the Shade Cloak and the Crystal Heart at the Tinkerer's Workshop.</summary>
        public static IEnumerable<Recipe> Recipes()
        {
            yield return new Recipe(ItemType.HallownestShield, 1, "工匠作坊", 2, (ItemType.ShieldOfCthulhu, 1), (ItemType.ShadeCloak, 1), (ItemType.CrystalHeart, 1));
        }
    }

    /// <summary>
    /// The Knight's two dashes Terraria has no item for. The Shade Cloak worn gives a shadow dash, once in 10 s, on its own
    /// key (TerraKeys' Shadow Dash) or on a double tap of a direction while the Dash key is held (the Crystal Heart charging
    /// is broken off for it): a dash of the worn kind (the Shield's without one) during which the game's own
    /// cState.shadowDashing is set, so enemies and their attacks pass through the player (TakeDamage, HeroBox) while spikes
    /// still hurt, the Shield of Cthulhu doesn't bounce off what it meets, and the shade gates (ShadowGateColliderControl)
    /// open as in the game. The Crystal Heart worn turns a long press of the Dash key, on the ground or on a wall, into the
    /// game's own Super Dash (the hero's "Superdash" FSM: charge, release, fly until a wall), once in 20 s; a short press
    /// is still a dash. The Hallownest Shield is all three: the Shield's dash on a tap.
    /// </summary>
    [HarmonyPatch]
    internal static partial class AbilityAccessories
    {
        const float ShadowCooldown = 10f;
        const float DoubleTapWindow = 15f / 60f;
        const float SuperCooldown = 20f;
        /// <summary>How long the Dash key is held before it is the Super Dash's (shorter: a dash on its release).</summary>
        const float HoldTime = 0.25f;

        static bool Worn(ItemType t)
        {
            var inv = TerraMod.Instance?.Inventory;
            if (inv == null) return false;
            foreach (var s in inv.EffectiveAccessories) if (!s.IsEmpty && s.Type == t) return true;
            return false;
        }

        public static bool ShadeWorn => Worn(ItemType.ShadeCloak) || Worn(ItemType.HallownestShield);
        public static bool CrystalWorn => Worn(ItemType.CrystalHeart) || Worn(ItemType.HallownestShield);

        // ------------------------------------------------------------------ the shadow dash

        /// <summary>The dash under way is a shadow dash.</summary>
        public static bool ShadowDashing { get; private set; }
        static float shadowReady;
        public static int DevShadowDashes;

        static readonly AccessTools.FieldRef<HeroController, AudioClip> shadowClip = AccessTools.FieldRefAccess<HeroController, AudioClip>("shadowDashClip");
        static readonly AccessTools.FieldRef<HeroController, GameObject> shadowBurst = AccessTools.FieldRefAccess<HeroController, GameObject>("shadowdashBurstPrefab");
        static readonly AccessTools.FieldRef<HeroController, GameObject> shadowParticles = AccessTools.FieldRefAccess<HeroController, GameObject>("shadowdashParticlesPrefab");
        static readonly AccessTools.FieldRef<HeroController, GameObject> shadowRing = AccessTools.FieldRefAccess<HeroController, GameObject>("shadowRingPrefab");

        /// <summary>The Knight's own shadow dash as it starts (HeroController.HeroDash): its black burst ahead, its ring, its trail of smoke.</summary>
        static void KnightShadowFx(HeroController hero, float dir)
        {
            try
            {
                var at = hero.transform.position;
                var burst = shadowBurst(hero);
                if (burst != null)
                {
                    var b = burst.Spawn(new Vector3(at.x + 5.21f * dir, at.y - 0.58f, at.z + 0.00101f));
                    b.transform.localScale = new Vector3(1.919591f * dir, b.transform.localScale.y, b.transform.localScale.z);
                }
                var ring = shadowRing(hero);
                if (ring != null) ring.Spawn(at);
                var trail = shadowParticles(hero);
                var ps = trail != null ? trail.GetComponent<ParticleSystem>() : null;
                if (ps != null) { var em = ps.emission; em.enabled = true; }
            }
            catch (System.Exception e) { Plugin.Log.LogWarning("[暗影冲刺] 原版特效：" + e.Message); }
        }

        static void KnightShadowFxEnd(HeroController hero)
        {
            var trail = shadowParticles(hero);
            var ps = trail != null ? trail.GetComponent<ParticleSystem>() : null;
            if (ps != null) { var em = ps.emission; em.enabled = false; }
        }

        static bool shadowAsked;
        static int lastTapDir;
        static float lastTapTime = -10f;

        /// <summary>Seconds until the shadow dash may go again.</summary>
        public static float ShadowCooldownLeft => Mathf.Max(0f, shadowReady - Time.time);

        /// <summary>Dev: the Shadow Dash key pressed for a test.</summary>
        public static bool DevShadowKey;

        /// <summary>
        /// A shadow dash asked for (its key, or a double tap while Dash is held): the Super Dash's charge broken off, a dash
        /// of the worn kind now whatever its own cooldown, made a shadow dash as it starts.
        /// </summary>
        static bool TryShadowDash(HeroController hero, int dir)
        {
            if (hero == null || !ShadeWorn || Time.time < shadowReady || hero.cState.dead || hero.cState.transitioning || hero.cState.superDashing) return false;
            if (!GameHooks.PlayerHasControl() && !Charging(hero)) return false;
            if (Charging(hero))
            {
                HeroFsm(hero, "Superdash")?.SendEvent("FSM CANCEL");
                // Its Cancel clears the charge away but leaves the hero held (the game's own callers hand control back themselves).
                if (hero.controlReqlinquished) hero.RegainControl();
                hero.StartAnimationControl();
            }
            // The press that was waiting for the Super Dash is spent: no dash on its release, no charge.
            deferring = false;
            shadowAsked = true;
            TerraMovement.ForceDash(dir);
            return true;
        }

        static bool Charging(HeroController hero)
        {
            var f = HeroFsm(hero, "Superdash");
            return f != null && f.ActiveStateName != "Inactive";
        }

        static PlayMakerFSM HeroFsm(HeroController hero, string name)
        {
            foreach (var f in hero.GetComponents<PlayMakerFSM>()) if (f.FsmName == name) return f;
            return null;
        }

        /// <summary>The Shadow Dash key, and a direction tapped twice while the Dash key is held.</summary>
        static void ShadowKeys(HeroController hero)
        {
            if (!ShadeWorn || Net.NetChat.Typing) return;
            int facing = hero.cState.facingRight ? 1 : -1;
            int held = (TerraKeys.Held(TerraKeys.Action.Right) ? 1 : 0) - (TerraKeys.Held(TerraKeys.Action.Left) ? 1 : 0);
            if (TerraKeys.Pressed(TerraKeys.Action.ShadowDash) || DevShadowKey)
            {
                DevShadowKey = false;
                TryShadowDash(hero, held != 0 ? held : facing);
                return;
            }
            int tap = TerraKeys.Pressed(TerraKeys.Action.Left) ? -1 : TerraKeys.Pressed(TerraKeys.Action.Right) ? 1 : DevTap;
            DevTap = 0;
            if (tap == 0) return;
            if (DashKeyHeld && tap == lastTapDir && Time.unscaledTime - lastTapTime < DoubleTapWindow)
            {
                lastTapDir = 0;
                TryShadowDash(hero, tap);
                return;
            }
            lastTapDir = tap;
            lastTapTime = Time.unscaledTime;
        }

        /// <summary>Dev: a direction tapped (-1, 1) for a test.</summary>
        public static int DevTap;

        /// <summary>The Dash key held (the real one, or a test's).</summary>
        public static bool DashKeyHeld => DevDashKey ?? (TerraKeys.Held(TerraKeys.Action.Dash) && !Net.NetChat.Typing);

        /// <summary>TerraMovement starting a dash: a shadow one when one was asked for.</summary>
        public static void OnDashStart(HeroController hero, int direction)
        {
            if (hero == null || !shadowAsked) return;
            shadowAsked = false;
            shadowReady = Time.time + ShadowCooldown;
            ShadowDashing = true;
            hero.cState.shadowDashing = true;
            // Wrapped in shadow at once, with the Knight's own burst, ring and smoke.
            TerraPlayerSprite.DashShade = 1f;
            KnightShadowFx(hero, direction < 0 ? -1f : 1f);
            DevShadowDashes++;
            var clip = shadowClip(hero);
            var audio = hero.GetComponent<AudioSource>();
            if (clip != null && audio != null) audio.PlayOneShot(clip, 1f);
        }

        static void TickShadow(HeroController hero)
        {
            if (!ShadowDashing) return;
            if (TerraMovement.IsDashing && !hero.cState.dead) return;
            ShadowDashing = false;
            hero.cState.shadowDashing = false;
            KnightShadowFxEnd(hero);
        }

        // ------------------------------------------------------------------ how they look

        static float fxTicks;

        /// <summary>
        /// The shadow dash wraps the player in darkness: the body shaded nearly black (TerraPlayerSprite.DashShade) in a
        /// cloud of black smoke (DustID.Wraith) shot through with shadowflame sparks (27), left trailing behind. The
        /// Super Dash lights him white: crystal light drawn in to him as it charges, the body white and a white light about
        /// him (LightMap) as it flies, a wake of white sparks.
        /// </summary>
        static void DashLooks(HeroController hero, float dt)
        {
            var fsm = HeroFsm(hero, "Superdash");
            string st = fsm != null ? fsm.ActiveStateName : "Inactive";
            bool charging = st.Contains("Charge") && !st.Contains("Cancel");
            bool flying = hero.cState.superDashing && !charging;
            float glowTo = flying ? 1f : charging ? (st.EndsWith("Charged") ? 0.8f : 0.45f) : 0f;
            TerraPlayerSprite.DashGlow = Mathf.MoveTowards(TerraPlayerSprite.DashGlow, glowTo, dt * (glowTo > TerraPlayerSprite.DashGlow ? 6f : 3f));
            TerraPlayerSprite.DashShade = Mathf.MoveTowards(TerraPlayerSprite.DashShade, ShadowDashing ? 1f : 0f, dt * (ShadowDashing ? 12f : 4f));
            var p = TerraParticles.Instance;
            if (p == null || hero.cState.dead) return;
            var tl = TerraMovement.TopLeft(hero);
            var centre = tl + new Vector2(10f, 21f);
            float dir = hero.cState.facingRight ? 1f : -1f;
            if (TerraPlayerSprite.DashGlow > 0.05f)
                World.LightMap.Add(hero.GetComponent<BoxCollider2D>().bounds.center, new Color(1.1f, 1.1f, 1.25f) * TerraPlayerSprite.DashGlow);
            fxTicks += dt * 60f;
            for (; fxTicks >= 1f; fxTicks -= 1f)
            {
                if (ShadowDashing || TerraPlayerSprite.DashShade > 0.3f)
                {
                    // The cloud about the body, drifting back off the dash and a little up.
                    for (int k = 0; k < 4; k++)
                    {
                        int d = p.NewDust(tl - new Vector2(8f, 6f), 36, 54, 54, -dir * Random.Range(1f, 3f), Random.Range(-0.8f, 0.3f), 40, Random.Range(1.3f, 2f));
                        if (d < 0) continue;
                        p[d].NoGravity = true;
                    }
                    for (int k = 0; k < 2; k++)
                    {
                        int s2 = p.NewDust(tl - new Vector2(4f, 4f), 28, 50, 27, -dir * Random.Range(0.5f, 2.5f), Random.Range(-1f, 0.5f), 80, Random.Range(1f, 1.5f));
                        if (s2 >= 0) p[s2].NoGravity = true;
                    }
                }
                if (charging)
                {
                    // Crystal light gathering in from round him.
                    for (int k = 0; k < 2; k++)
                    {
                        float a = Random.value * Mathf.PI * 2f, r = Random.Range(34f, 52f);
                        var from = centre + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                        int d = p.NewDust(from - new Vector2(2f, 2f), 4, 4, 43, 0f, 0f, 0, Random.Range(0.8f, 1.2f));
                        if (d < 0) continue;
                        p[d].NoGravity = true;
                        p[d].Velocity = (centre - from) / 14f;
                        p[d].Color = Random.value < 0.25f ? new Color(1f, 0.75f, 0.95f) : Color.white;
                        p[d].Additive = true;
                    }
                }
                else if (flying)
                {
                    // A wake of white sparks, and some flung out round him.
                    for (int k = 0; k < 4; k++)
                    {
                        int d = p.NewDust(tl - new Vector2(4f, 2f), 28, 46, 43, -dir * Random.Range(2f, 5f), Random.Range(-0.7f, 0.7f), 0, Random.Range(1.2f, 1.9f));
                        if (d < 0) continue;
                        p[d].NoGravity = true;
                        p[d].Color = k == 0 ? new Color(0.8f, 0.92f, 1f) : Color.white;
                        p[d].Additive = true;
                    }
                }
            }
        }

        /// <summary>A shade gate stays open while a shadow dash runs and until the player is out of it.</summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(ShadowGateColliderControl), "FixedUpdate")]
        static bool ShadeGate(ShadowGateColliderControl __instance)
        {
            if (TerraMod.Instance == null || __instance.disableCollider == null) return true;
            var c = __instance.disableCollider;
            var hero = HeroController.SilentInstance;
            bool open = ShadowDashing;
            if (!open && !c.enabled && hero != null && c is BoxCollider2D box)
            {
                // Still inside it: not shut on the player (a switched-off collider has no bounds: its box worked out).
                var b = hero.GetComponent<BoxCollider2D>().bounds;
                Vector2 centre = box.transform.TransformPoint(box.offset);
                var s = box.transform.lossyScale;
                var half = new Vector2(Mathf.Abs(box.size.x * s.x), Mathf.Abs(box.size.y * s.y)) * 0.5f;
                if (b.min.x < centre.x + half.x + 0.1f && b.max.x > centre.x - half.x - 0.1f && b.min.y < centre.y + half.y && b.max.y > centre.y - half.y) open = true;
            }
            if (c.enabled == open) c.enabled = !open;
            return false;
        }

        // ------------------------------------------------------------------ the Super Dash

        static float superReady;
        static int computedFrame = -1;
        static bool keyWasHeld, deferring, superHeld, superPrev, tapReleased;
        static float holdTime;
        static bool wasSuperDashing;

        /// <summary>Dev: what the last press of the Dash key was taken for.</summary>
        public static string DevPress = "";

        /// <summary>Dev: the Dash key held for a test (null: the real key).</summary>
        public static bool? DevDashKey;
        public static int DevSuperDashes;

        /// <summary>Seconds until the Crystal Heart may go again.</summary>
        public static float SuperCooldownLeft => Mathf.Max(0f, superReady - Time.time);

        static void Compute()
        {
            if (computedFrame == Time.frameCount) return;
            computedFrame = Time.frameCount;
            var hero = HeroController.SilentInstance;
            bool key = DashKeyHeld;
            if (key && !keyWasHeld)
            {
                holdTime = 0f;
                // A press that may become the Super Dash: the heart worn and back, standing or on a wall, not mid-dash.
                deferring = hero != null && CrystalWorn && Time.time >= superReady && !TerraMovement.IsDashing
                            && (hero.cState.onGround || hero.cState.wallSliding) && GameHooks.PlayerHasControl();
                DevPress = $"按下：等长按 {deferring}（戴着 {CrystalWorn}，冷却剩 {SuperCooldownLeft:F1}，冲刺中 {TerraMovement.IsDashing}，地上 {hero?.cState.onGround}，墙上 {hero?.cState.wallSliding}，控制 {GameHooks.PlayerHasControl()}：输入 {hero?.acceptingInput} 放手 {hero?.controlReqlinquished} 暂停 {GameManager.UnsafeInstance?.isPaused} {GameManager.UnsafeInstance?.gameState} 切换 {GameManager.UnsafeInstance?.IsInSceneTransition} 时速 {Time.timeScale} 场景 {GameManager.UnsafeInstance?.sceneName}）";
            }
            if (key) holdTime += Time.deltaTime;
            tapReleased = !key && keyWasHeld && deferring && holdTime < HoldTime;
            if (!key && keyWasHeld) DevPress += $"；松开：按了 {holdTime:F2} 秒，算轻点 {tapReleased}";
            superPrev = superHeld;
            superHeld = key && deferring && holdTime >= HoldTime;
            if (!key) deferring = false;
            keyWasHeld = key;
        }

        /// <summary>The Dash key is being held for the Crystal Heart: no dash on it now (a short press dashes on its release).</summary>
        public static bool HoldsDashKey
        {
            get { Compute(); return deferring; }
        }

        /// <summary>A short press held back for the Crystal Heart was let go this frame: its dash now.</summary>
        public static bool TapReleased
        {
            get { Compute(); return tapReleased; }
        }

        /// <summary>The Superdash FSM's button (ListenForSuperdash): the Dash key held long enough, with the heart worn.</summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(HutongGames.PlayMaker.Actions.ListenForSuperdash), nameof(HutongGames.PlayMaker.Actions.ListenForSuperdash.OnUpdate))]
        static bool SuperdashButton(HutongGames.PlayMaker.Actions.ListenForSuperdash __instance)
        {
            if (TerraMod.Instance == null) return true;
            var gm = GameManager.UnsafeInstance;
            if (gm == null || gm.isPaused) return false;
            Compute();
            if (superHeld && !superPrev) __instance.Fsm.Event(__instance.wasPressed);
            if (!superHeld && superPrev) __instance.Fsm.Event(__instance.wasReleased);
            if (superHeld) __instance.Fsm.Event(__instance.isPressed);
            else __instance.Fsm.Event(__instance.isNotPressed);
            return false;
        }

        /// <summary>HeroController.CanSuperDash with the heart worn and its time come instead of the save's Crystal Heart.</summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(HeroController), nameof(HeroController.CanSuperDash))]
        static bool CanSuperDash(HeroController __instance, ref bool __result, out int __state)
        {
            // -1: the record untouched; else what it was, put back after.
            __state = -1;
            if (TerraMod.Instance == null) return true;
            var pd = PlayerData.instance;
            if (pd == null || !CrystalWorn || Time.time < superReady)
            {
                __result = false;
                return false;
            }
            __state = pd.hasSuperDash ? 1 : 0;
            pd.hasSuperDash = true;
            return true;
        }

        [HarmonyFinalizer]
        [HarmonyPatch(typeof(HeroController), nameof(HeroController.CanSuperDash))]
        static void AfterCanSuperDash(int __state)
        {
            var pd = PlayerData.instance;
            if (__state >= 0 && pd != null) pd.hasSuperDash = __state == 1;
        }

        static void TickSuper(HeroController hero)
        {
            Compute();
            bool now = hero.cState.superDashing;
            // Its cooldown from the moment it is launched.
            if (now && !wasSuperDashing)
            {
                superReady = Time.time + SuperCooldown;
                DevSuperDashes++;
            }
            wasSuperDashing = now;
            if (now) SuperDashWall(hero);
        }

        public static int DevWallHits;

        /// <summary>
        /// The game ends a Super Dash at a wall from the hero's collision callbacks (OnCollisionEnter2D/Stay2D: "HIT WALL"
        /// while CheckStillTouchingWall), which the mod's movement doesn't raise: the wall looked for here, ahead by this
        /// frame's travel on the terrain layer, the hero set against it.
        /// </summary>
        static void SuperDashWall(HeroController hero)
        {
            var rb = hero.GetComponent<Rigidbody2D>();
            var col = hero.GetComponent<BoxCollider2D>();
            if (rb == null || col == null || Mathf.Abs(rb.linearVelocity.x) < 1f) return;
            float dir = Mathf.Sign(rb.linearVelocity.x);
            var b = col.bounds;
            float reach = 0.1f + Mathf.Abs(rb.linearVelocity.x) * Mathf.Max(Time.deltaTime, Time.fixedDeltaTime);
            const int mask = 1 << World.ChunkColliders.TerrainLayer;
            float edge = dir > 0f ? b.max.x : b.min.x;
            RaycastHit2D best = default;
            // A wall faces back along the dash (a floor's step or a slope doesn't, nor a one-way platform's side); one the ray starts in isn't met.
            foreach (float y in new[] { b.center.y, b.min.y + 0.3f })
            {
                var hit = Physics2D.Raycast(new Vector2(edge, y), new Vector2(dir, 0f), reach, mask);
                if (hit.collider == null || hit.collider.isTrigger || hit.collider.usedByEffector || hit.distance <= 0f || hit.normal.x * dir > -0.7f) continue;
                if (best.collider == null || hit.distance < best.distance) best = hit;
            }
            if (best.collider == null) return;
            hero.transform.position += new Vector3(dir * Mathf.Max(0f, best.distance - 0.02f), 0f, 0f);
            HeroFsm(hero, "Superdash")?.SendEvent("HIT WALL");
            DevWallHits++;
        }

        /// <summary>Each frame in play.</summary>
        public static void Tick(HeroController hero)
        {
            if (hero == null) return;
            ShadowKeys(hero);
            TickShadow(hero);
            TickSuper(hero);
            DashLooks(hero, Mathf.Min(Time.deltaTime, 0.1f));
        }

        /// <summary>Dev: the cooldowns cleared.</summary>
        public static void DevReset()
        {
            shadowReady = superReady = 0f;
            shadowAsked = false;
        }
    }
}
