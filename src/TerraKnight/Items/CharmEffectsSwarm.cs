using System.Collections.Generic;
using HarmonyLib;
using TerraKnight.Effects;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// The charms that send creatures out (护符设计记录.md):
    /// Flukenest (11): its reach (an Inferno potion's, 200 px) shown round the player as a faint ring of dots; with an
    /// enemy in it, three flukes fly out every two seconds and seek the nearest enemies, each fluke's hit taking back 1%
    /// of the life (3% a volley), a soft green ring at each heal.
    /// Glowing Womb (22): every four seconds in a fight, 20 mana makes a hatchling (four at most); it flies by the player
    /// until an enemy is near, then darts at it and bursts for one and a half blows of the weapon in hand (the nail's if
    /// that is none).
    /// Thorns of Agony (12): a hit from an enemy bursts eight thorny vines out of the player, each enemy within 120 px
    /// taking one and a half nail blows (the game's own thorns, which hold the Knight still and are drawn on him, unused).
    /// The game's own hatchlings, flukes, spores and thorns are left off while the charm bag is on.
    /// </summary>
    internal static partial class CharmEffects
    {
        public const int Flukenest = 11, Thorns = 12, SporeShroom = 17, GlowingWomb = 22;
        const float FlukeRangePx = 200f, FlukeGap = 2f, FlukeHeal = 0.01f, FlukeShare = 0.35f, FlukeLife = 2.5f;
        const float WombGap = 4f, WombShare = 1.5f, WombSeekPx = 400f, WombFightPx = 640f, WombBlastPx = 52f, WombCharge = 2f;
        const int FlukeCount = 3, WombMana = 20, WombMax = 4;
        const float ThornRangePx = 120f, ThornShare = 1.5f;
        static readonly Color FlukeColor = new Color(1f, 0.86f, 0.62f), HealColor = new Color(0.6f, 1f, 0.7f), WombColor = new Color(1f, 0.62f, 0.25f),
            ThornColor = new Color(0.35f, 0.62f, 0.22f);
        public static int DevFlukes, DevFlukeHits, DevFlukeHealed, DevHatched, DevHatchBursts, DevThornBursts, DevThornHits;

        /// <summary>A share of the stage's plain nail blow (NailItem.DamageNow, before any bonus).</summary>
        static int StageDamage(float share) => Mathf.Max(1, Mathf.RoundToInt(NailItem.DamageNow() * share));

        static readonly List<HealthManager> swarmFound = new List<HealthManager>();

        /// <summary>The nearest live enemy within a distance (px) of a point, in sight of it; null: none.</summary>
        static HealthManager NearestEnemy(Vector2 at, float radiusPx, HealthManager not = null)
        {
            HealthManager best = null;
            float bestD = float.MaxValue;
            foreach (var hm in AccessoryEffects.Enemies(at, radiusPx, swarmFound))
            {
                if (hm == not) continue;
                var c = TargetCentre(hm);
                float d = (c - at).sqrMagnitude;
                if (d < bestD && !WallBetween(at, c, out _)) { best = hm; bestD = d; }
            }
            return best;
        }

        // ------------------------------------------------------------------ Flukenest

        sealed class Fluke
        {
            public Vector2 At, Vel;
            public float Born;
            public HealthManager Target;
            public int Damage;
            public string Scene;
        }
        static readonly List<Fluke> flukes = new List<Fluke>();
        static float flukeAt, flukeHealAt = -10f;
        static bool flukeEnemyNear;

        static void TickFlukes(HeroController hero)
        {
            string scene = SceneNow;
            flukeEnemyNear = false;
            if (On(Flukenest) && Playing(hero))
            {
                var c = Centre(hero);
                flukeEnemyNear = NearestEnemy(c, FlukeRangePx) != null;
                if (flukeEnemyNear && Time.time >= flukeAt)
                {
                    flukeAt = Time.time + FlukeGap;
                    int damage = Mathf.Max(1, Mathf.RoundToInt(StageDamage(FlukeShare) * (1f + Equipment.Current.MinionDamage)));
                    for (int k = 0; k < FlukeCount; k++)
                    {
                        float a = Random.Range(0f, Mathf.PI * 2f);
                        flukes.Add(new Fluke { At = c, Vel = new Vector2(Mathf.Cos(a), Mathf.Sin(a) * 0.6f + 0.5f) * Random.Range(6f, 8f), Born = Time.time + k * 0.06f, Damage = damage, Scene = scene });
                    }
                    DevFlukes += FlukeCount;
                    Mod.PlaySound("NPC_Hit_" + Random.Range(1, 3), 0.15f, 1.8f);
                }
            }
            for (int i = flukes.Count - 1; i >= 0; i--)
            {
                var f = flukes[i];
                if (f.Scene != scene || Time.time - f.Born > FlukeLife) { flukes.RemoveAt(i); continue; }
                if (Time.time < f.Born) continue;
                if (f.Target == null || f.Target.isDead) f.Target = NearestEnemy(f.At, FlukeRangePx + 160f);
                float age = Time.time - f.Born;
                if (f.Target != null && age > 0.12f)
                {
                    var want = (TargetCentre(f.Target) - f.At).normalized * Mathf.Lerp(9f, 15f, age / FlukeLife);
                    f.Vel = Vector2.Lerp(f.Vel, want, Mathf.Clamp01(Time.deltaTime * 6f));
                }
                else f.Vel *= Mathf.Pow(0.4f, Time.deltaTime);
                // A little flutter on its way.
                var side = new Vector2(-f.Vel.y, f.Vel.x).normalized * Mathf.Sin((Time.time + i) * 22f) * 1.2f;
                var next = f.At + (f.Vel + side) * Time.deltaTime;
                if (WallBetween(f.At, next, out _)) { Pop(f.At, FlukeColor); flukes.RemoveAt(i); continue; }
                f.At = next;
                foreach (var hm in AccessoryEffects.Enemies(f.At, 12f, swarmFound))
                {
                    var col = hm.GetComponent<Collider2D>() ?? hm.GetComponentInChildren<Collider2D>();
                    if (col == null) continue;
                    ArmorEffects.Context = new ArmorEffects.Hit { Class = DamageClass.Summon };
                    if (Mod.Melee.HitWith(col, f.Damage, 1f, 0f, f.Vel.x >= 0f ? 1f : -1f, f.At, autonomous: true))
                    {
                        DevFlukeHits++;
                        FlukeHealOnce(hero);
                    }
                    Pop(f.At, FlukeColor);
                    flukes.RemoveAt(i);
                    break;
                }
            }
        }

        /// <summary>A fluke's hit: 1% of the life back, a soft green ring round the player.</summary>
        static void FlukeHealOnce(HeroController hero)
        {
            var st = Stats;
            if (st == null || hero == null || st.Life <= 0 || st.Life >= st.LifeMax) return;
            int healed = st.Heal(Mathf.Max(1, Mathf.RoundToInt(st.LifeMax * FlukeHeal)), recovery: true);
            if (healed <= 0) return;
            DevFlukeHealed += healed;
            flukeHealAt = Time.time;
            UI.CombatText.Show(hero.transform.position + Vector3.up * 0.9f, healed.ToString(), UI.CombatText.Heal);
        }

        // ------------------------------------------------------------------ Glowing Womb

        sealed class Hatchling
        {
            public Vector2 At, Vel;
            public float Born, ChargeAt = -1f;
            public HealthManager Target;
            public int Slot;
            public string Scene;
        }
        static readonly List<Hatchling> hatchlings = new List<Hatchling>();
        static float hatchAt;

        static void TickWomb(HeroController hero)
        {
            if (!On(GlowingWomb) || hero == null) { hatchlings.Clear(); return; }
            string scene = SceneNow;
            var c = Centre(hero);
            var st = Stats;
            if (Playing(hero) && Time.time >= hatchAt && hatchlings.Count < WombMax && st != null && st.Mana >= WombMana && NearestEnemy(c, WombFightPx) != null)
            {
                hatchAt = Time.time + WombGap;
                if (st.SpendMana(WombMana))
                {
                    int slot = 0;
                    while (hatchlings.Exists(h => h.Slot == slot)) slot++;
                    hatchlings.Add(new Hatchling { At = c, Vel = Vector2.up * 4f, Born = Time.time, Slot = slot, Scene = scene });
                    DevHatched++;
                    UI.CombatText.Show(hero.transform.position + Vector3.up * 0.9f, WombMana.ToString(), UI.CombatText.Mana);
                    Mod.PlaySound("Item_" + 17, 0.3f, 1.4f);
                }
            }
            for (int i = hatchlings.Count - 1; i >= 0; i--)
            {
                var h = hatchlings[i];
                // Another room: it comes along, by the player.
                if (h.Scene != scene) { h.Scene = scene; h.At = c; h.Vel = Vector2.zero; h.ChargeAt = -1f; h.Target = null; }
                if (h.ChargeAt < 0f)
                {
                    var foe = NearestEnemy(h.At, WombSeekPx);
                    if (foe != null && Playing(hero)) { h.Target = foe; h.ChargeAt = Time.time; Mod.PlaySound("Item_" + 97, 0.2f, 1.6f); }
                }
                if (h.ChargeAt >= 0f)
                {
                    if (h.Target == null || h.Target.isDead) h.Target = NearestEnemy(h.At, WombSeekPx);
                    if (h.Target == null) { h.ChargeAt = -1f; continue; }
                    var want = (TargetCentre(h.Target) - h.At).normalized * 17f;
                    h.Vel = Vector2.Lerp(h.Vel, want, Mathf.Clamp01(Time.deltaTime * 8f));
                    var next = h.At + h.Vel * Time.deltaTime;
                    bool wall = WallBetween(h.At, next, out var wallAt);
                    h.At = wall ? wallAt : next;
                    bool touch = AccessoryEffects.Enemies(h.At, 16f, swarmFound).Count > 0;
                    if (touch || wall || Time.time - h.ChargeAt > WombCharge)
                    {
                        Burst(hero, h.At);
                        hatchlings.RemoveAt(i);
                    }
                    continue;
                }
                // Flying by the player: each to its place at the shoulder, bobbing.
                float side = (h.Slot % 2 == 0 ? -1f : 1f) * (0.9f + 0.45f * (h.Slot / 2));
                var home = c + new Vector2(side, 0.9f + Mathf.Sin(Time.time * 3f + h.Slot) * 0.2f);
                var to = home - h.At;
                h.Vel = Vector2.Lerp(h.Vel, to * 5f, Mathf.Clamp01(Time.deltaTime * 4f));
                h.At += h.Vel * Time.deltaTime;
                if (to.sqrMagnitude > 400f) { h.At = home; h.Vel = Vector2.zero; }
            }
        }

        /// <summary>A hatchling bursting: one and a half blows of the weapon in hand on everything round it.</summary>
        static void Burst(HeroController hero, Vector2 at)
        {
            var mod = Mod;
            if (mod == null) return;
            var def = mod.Inventory.Current.Def;
            if (def == null || !def.IsWeapon || def.PickPower > 0 || def.AxePower > 0 || def.HammerPower > 0)
            {
                def = ItemDb.Get(ItemType.KnightNail);
                def.Damage = NailItem.DamageNow();
            }
            int damage = Mathf.Max(1, Mathf.RoundToInt(Equipment.Damage(def) * WombShare));
            float crit = Equipment.Crit(def);
            int hit = 0;
            foreach (var hm in AccessoryEffects.Enemies(at, WombBlastPx, swarmFound))
            {
                var col = hm.GetComponent<Collider2D>() ?? hm.GetComponentInChildren<Collider2D>();
                if (col == null) continue;
                ArmorEffects.Context = new ArmorEffects.Hit { Class = def.Class, FromProjectile = true };
                if (mod.Melee.HitWith(col, damage, 4f, crit, hm.transform.position.x >= at.x ? 1f : -1f, at)) hit++;
            }
            DevHatchBursts++;
            Pop(at, WombColor);
            booms.Add((at, Time.time, SceneNow));
            mod.PlaySound("Item_14", 0.35f, 1.3f);
            var p = TerraParticles.Instance;
            if (p != null)
            {
                var px = TerraParticles.ToPx(at);
                for (int k = 0; k < 10; k++)
                {
                    float a = k / 10f * Mathf.PI * 2f;
                    int d = p.NewDust(px - new Vector2(3f, 3f), 6, 6, 6, Mathf.Cos(a) * 3.5f, Mathf.Sin(a) * 3.5f, 0, 1.2f);
                    if (d >= 0) { ref var dust = ref p[d]; dust.NoGravity = true; }
                }
            }
            Plugin.Log.LogInfo($"[护符] 发光子宫：幼崽炸开，{def.Name} 的 {damage}，打中 {hit} 个");
        }

        static readonly List<(Vector2 At, float Time, string Scene)> booms = new List<(Vector2, float, string)>();

        // ------------------------------------------------------------------ Thorns of Agony

        static float thornsAt = -10f;
        static Vector2 thornsFrom;
        static string thornsScene;
        static float thornsSpin;

        /// <summary>The player hurt (TerraHurt.AfterHurt): the thorns burst out; Hiveblood's state.</summary>
        public static void OnHurt(HeroController hero, int damage, bool byEnemy)
        {
            if (!Charms.Enabled || hero == null) return;
            HurtHive();
            if (!On(Thorns) || !byEnemy || damage <= 0) return;
            thornsAt = Time.time;
            thornsFrom = Centre(hero);
            thornsScene = SceneNow;
            thornsSpin = Random.Range(0f, Mathf.PI / 4f);
            DevThornBursts++;
            var mod = Mod;
            var nail = ItemDb.Get(ItemType.KnightNail);
            nail.Damage = NailItem.DamageNow();
            int dealt = Mathf.Max(1, Mathf.RoundToInt(Equipment.Damage(nail) * ThornShare));
            foreach (var hm in AccessoryEffects.Enemies(thornsFrom, ThornRangePx, swarmFound))
            {
                var col = hm.GetComponent<Collider2D>() ?? hm.GetComponentInChildren<Collider2D>();
                if (col == null) continue;
                ArmorEffects.Context = new ArmorEffects.Hit { Class = DamageClass.Melee };
                if (mod.Melee.HitWith(col, dealt, 6f, Equipment.Crit(nail), hm.transform.position.x >= thornsFrom.x ? 1f : -1f, thornsFrom)) DevThornHits++;
            }
            mod.PlaySound("Item_17", 0.4f, 0.8f);
        }

        // ------------------------------------------------------------------ the looks

        static void DrawSwarm(FxBatch fx, Vector2 c, float opacity)
        {
            float t = Time.time;
            if (On(Flukenest))
            {
                // The reach: a faint ring of dots, slowly turning, brighter with an enemy in it.
                fx.OnTop = true;
                float k = (flukeEnemyNear ? 0.6f : 0.3f) * opacity;
                const int n = 40;
                for (int i = 0; i < n; i++)
                {
                    float a = i * Mathf.PI * 2f / n + t * 0.15f;
                    // Every other dot a little brighter: a dotted ring, not a line.
                    fx.Draw(Dot, c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * FlukeRangePx, Glow(FlukeColor, k * (i % 2 == 0 ? 1f : 0.6f)), 0f, 0.32f, overPlayer: false);
                }
                float heal = Mathf.Clamp01(1f - (t - flukeHealAt) / 0.45f);
                if (heal > 0f)
                {
                    fx.OnTop = true;
                    fx.Draw(Ring, c, Glow(HealColor, 0.7f * heal * opacity), 0f, Mathf.Lerp(44f, 18f, heal) / 64f, overPlayer: true);
                    fx.Draw(Dot, c, Glow(HealColor, 0.25f * heal * opacity), 0f, 1.6f, overPlayer: true);
                }
            }
        }

        static void DrawSwarmWorld(FxBatch fx)
        {
            bool top = fx.OnTop;
            fx.OnTop = true;
            float t = Time.time;
            foreach (var f in flukes)
            {
                if (t < f.Born) continue;
                var px = TerraParticles.ToPx(f.At);
                int frame = (int)(t * 18f + f.Born * 7f) % 4;
                var spr = FxBatch.Frame("NPC_598", 0, frame * 12, 20, 12, 10f, 6f);
                float fade = Mathf.Clamp01((FlukeLife - (t - f.Born)) / 0.3f);
                fx.Draw(Dot, px, Glow(FlukeColor, 0.35f * fade), 0f, 0.6f);
                fx.Draw(spr, px, new Color(fade, fade, fade, fade), 0f, 0.9f, flipX: f.Vel.x < 0f);
            }
            foreach (var h in hatchlings)
            {
                var px = TerraParticles.ToPx(h.At);
                bool charging = h.ChargeAt >= 0f;
                int frame = (int)(t * (charging ? 20f : 12f) + h.Slot) % 4;
                var spr = FxBatch.Frame("Projectile_189", 0, frame * 14, 14, 14, 7f, 7f);
                float pulse = charging ? 0.75f + 0.25f * Mathf.Sin(t * 30f) : 0.4f;
                fx.Draw(Dot, px, Glow(WombColor, pulse), 0f, charging ? 0.9f : 0.6f);
                // Pale, as the game's are: the wasp drawn light over its own colours.
                fx.Draw(spr, px, new Color(1f, 0.95f, 0.85f, 1f), 0f, 1.1f, flipX: h.Vel.x < 0f);
                fx.Draw(spr, px, Glow(new Color(1f, 0.9f, 0.75f), 0.35f), 0f, 1.1f, flipX: h.Vel.x < 0f);
                fx.Light(px, WombColor * 0.5f, 2f);
            }
            string scene = SceneNow;
            for (int i = booms.Count - 1; i >= 0; i--)
            {
                var b = booms[i];
                float k = Mathf.Clamp01((t - b.Time) / 0.4f);
                if (k >= 1f || b.Scene != scene) { booms.RemoveAt(i); continue; }
                var px = TerraParticles.ToPx(b.At);
                float r = Mathf.Lerp(10f, WombBlastPx, 1f - (1f - k) * (1f - k));
                fx.Draw(Ring, px, Glow(WombColor, 0.9f * (1f - k)), 0f, r * 2f / 64f);
                fx.Draw(Dot, px, Glow(new Color(1f, 0.85f, 0.5f), 0.8f * (1f - k) * (1f - k)), 0f, r * 1.4f / 32f);
            }
            // The thorns: eight vines thrust out, held a moment, drawn back.
            float age = t - thornsAt;
            if (age < 0.4f && thornsScene == scene)
            {
                float reach = age < 0.1f ? age / 0.1f : age < 0.22f ? 1f : 1f - (age - 0.22f) / 0.18f;
                reach = Mathf.Clamp01(reach);
                var c = TerraParticles.ToPx(thornsFrom);
                for (int i = 0; i < 8; i++)
                {
                    float a = thornsSpin + i * Mathf.PI / 4f;
                    var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                    float len = ThornRangePx * (i % 2 == 0 ? 1f : 0.8f) * reach;
                    // A dark green vine, its barbs along it, a pale tip.
                    for (int s = 1; s <= 8; s++)
                    {
                        float u = s / 8f;
                        var at = c + d * (len * u);
                        float w = Mathf.Lerp(0.75f, 0.25f, u);
                        // The vine (dark, its own), then a faint green light along it so it reads in the dark.
                        fx.Draw(Dot, at, new Color(0.1f, 0.2f, 0.06f, 0.95f), a, new Vector2(len / 8f / 32f * 2.4f, w));
                        fx.Draw(Dot, at, Glow(ThornColor, 0.35f * reach), a, new Vector2(len / 8f / 32f * 2f, w * 0.5f));
                        if (s % 2 == 0)
                        {
                            float barb = a + (s % 4 == 0 ? 0.8f : -0.8f);
                            fx.Draw(Dot, at, new Color(0.55f, 0.7f, 0.35f, 0.9f), barb, new Vector2(0.42f, 0.1f));
                        }
                    }
                    fx.Draw(Dot, c + d * len, Glow(new Color(0.75f, 1f, 0.55f), 0.6f * reach), 0f, 0.25f);
                }
            }
            fx.OnTop = top;
        }

        // ------------------------------------------------------------------ the game's own left off

        /// <summary>
        /// With the charm bag on, the game's own Glowing Womb hatchlings, Spore Shroom cloud, Flukenest flukes and Thorns of
        /// Agony counter don't come (their machines ask PlayerData.GetBool): these charms are the bag's own. Their hits would
        /// be the game's raw health, a share of a Terraria enemy's life.
        /// </summary>
        [HarmonyPatch(typeof(PlayerData), nameof(PlayerData.GetBool))]
        static class NoVanillaCreatures
        {
            static void Postfix(string boolName, ref bool __result)
            {
                if (!__result || !Charms.Enabled) return;
                if (boolName == "equippedCharm_22" || boolName == "equippedCharm_17" || boolName == "equippedCharm_11" || boolName == "equippedCharm_12") __result = false;
            }
        }
    }
}
