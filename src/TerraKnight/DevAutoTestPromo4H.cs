using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TerraKnight.EnemyAi.Bosses;
using TerraKnight.Items;
using TerraKnight.World;
using UnityEngine;
using static TerraKnight.Items.ItemType;

namespace TerraKnight
{
    /// <summary>
    /// Dev, the 4.4 trailer's new boss AI (recorder H): each boss come to by its seal (P4AEnter, the fight begun by
    /// BossSummonStart) with the new enemy AI on (EnemyAiMode.New) on Master, fought by one class in the gear a player has
    /// when he gets to that boss (its stage, ProgressionBalance.BossRewards: the class's best armour of that stage, its
    /// accessories, its weapons, as DevAutoTestBossLoadouts picks them; wings once the stage has them), and recorded. With
    /// wings the hero keeps a few tiles off on the floor, then takes off, sweeps over the boss (or flies across the room
    /// away from it) up in the air, high and long enough for its moves against the air; without, he keeps his distance on
    /// the floor, jumps now and then and vaults over it when it comes at him. The class's weapons in turn, the aim eased
    /// onto it, the camera leaning toward the boss so both are seen. Every change of the boss's state is logged with the
    /// clip's time (the kit's new states, its branches, its stuns), for the catalog. Suites p4_v5H_&lt;name&gt;_zh;
    /// p4_v5H_keys_zh takes its bosses from P4H_KEYS ("Key1.Key2#variant").
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        static readonly string[] P4HClassZh = { "战士", "射手", "法师", "召唤" };

        sealed class P4HCfg
        {
            public string Key, Zh, Variant;
            /// <summary>0 melee, 1 ranged, 2 magic, 3 summon.</summary>
            public int Cls;
            /// <summary>Over -1: this stage's gear instead of the boss's own stage's.</summary>
            public int Stage = -1;
            /// <summary>Weapons wished for beside the strongest (taken when the stage has them).</summary>
            public ItemType[] Prefer;
            public float Seconds = 28f, Zoom = 1.9f, Air = 4.6f, Lean = 0.45f;
            /// <summary>Over 0: this distance from it, whatever the weapon.</summary>
            public float Dist;
            /// <summary>On the floor this long before each flight, and the flight's length (with wings).</summary>
            public float GroundTime = 2.6f, AirTime = 5.6f;
            /// <summary>The boss's life let fall to this share of the clip's start at the most (a stun needs over 40% of its whole).</summary>
            public float Keep = 0.85f;
            /// <summary>Over 0: its life brought to this share of its whole before the clip (its later phase's moves).</summary>
            public float HpShare;
            /// <summary>Its stagger let happen (the stagger clip); else hits don't count toward it, so its moves are seen.</summary>
            public bool Stuns;
            public bool ClearAdds, NoLight, AnyEnemy;
            public int UntilParts;
            /// <summary>Kill clip: on until it falls (its health let go after a few seconds), then a moment on its body.</summary>
            public bool Kill;
        }

        static P4HCfg P4HC(string key, string zh, int cls, float zoom = 1.9f, ItemType[] prefer = null) =>
            new P4HCfg { Key = key, Zh = zh, Cls = cls, Zoom = zoom, Prefer = prefer };

        static P4HCfg P4HV(P4HCfg c, string variant, System.Action<P4HCfg> set)
        {
            c.Variant = variant;
            set?.Invoke(c);
            return c;
        }

        /// <summary>The bosses, one class each, the four in turn (a "Key#variant" for a second clip of one: its stagger, its later phase, its fall).</summary>
        static readonly P4HCfg[] P4HBosses =
        {
            P4HC("FalseKnight", "假骑士", 0),
            P4HC("Hornet", "大黄蜂", 1),
            P4HC("MantisLord", "螳螂领主", 2),
            P4HC("MageLord", "灵魂大师", 3, 1.7f),
            P4HC("DungDefender", "粪虫防御者", 0),
            P4HV(P4HC("DungDefender", "粪虫防御者", 0), "kill", c => { c.Kill = true; c.HpShare = 0.015f; c.Seconds = 14f; }),
            P4HC("InfectedKnight", "破碎容器", 1),
            P4HV(P4HC("InfectedKnight", "破碎容器", 1), "kill", c => { c.Kill = true; c.HpShare = 0.015f; c.Seconds = 14f; }),
            P4HC("MimicSpider", "诺斯克", 3),
            P4HC("BlackKnight", "守望者骑士", 2, 1.7f),
            P4HC("HiveKnight", "蜂巢骑士", 2),
            P4HC("Hornet@4", "大黄蜂（王国边缘）", 3),
            P4HV(P4HC("TraitorLord", "叛徒领主", 0), null, c => c.ClearAdds = true),
            P4HC("Grimm", "格林", 1, 1.7f),
            P4HV(P4HC("Grimm", "格林", 1, 1.8f), "stun", c => { c.Stuns = true; c.Keep = 0.6f; c.Seconds = 24f; c.Dist = 4.5f; c.GroundTime = 5f; c.Lean = 0.65f; }),
            P4HV(P4HC("Grimm", "格林", 1, 1.8f), "kill", c => { c.Kill = true; c.HpShare = 0.015f; c.Seconds = 14f; }),
            P4HC("NightmareGrimm", "梦魇之王格林", 3, 1.7f),
            P4HV(P4HC("NightmareGrimm", "梦魇之王格林", 3, 1.7f), "kill", c => { c.Kill = true; c.HpShare = 0.015f; c.Seconds = 14f; }),
            P4HV(P4HC("TraitorLord", "叛徒领主", 0), "kill", c => { c.ClearAdds = true; c.Kill = true; c.HpShare = 0.015f; c.Seconds = 14f; }),
            P4HV(P4HC("HiveKnight", "蜂巢骑士", 2, 1.8f), "stun", c => { c.Stuns = true; c.Keep = 0.6f; c.Seconds = 22f; c.Dist = 4.5f; c.Lean = 0.6f; }),
            P4HV(P4HC("HiveKnight", "蜂巢骑士", 2), "kill", c => { c.Kill = true; c.HpShare = 0.015f; c.Seconds = 14f; }),
            P4HC("HollowKnight", "空洞骑士", 2),
            P4HV(P4HC("HollowKnight", "空洞骑士", 2), "late", c => c.HpShare = 0.45f),
            P4HC("FinalBoss", "辐光", 0, 1.5f),
            P4HV(P4HC("FinalBoss", "辐光", 0, 1.5f), "late", c => c.HpShare = 0.6f),
            P4HC("HollowKnightPrime", "纯粹容器", 1),
            P4HC("GreyPrince", "灰色王子左特", 0),
            P4HC("Nailsage", "骨钉贤者斯莱", 1),
            P4HC("Paintmaster", "绘画大师席奥", 2),
            P4HC("NailBros", "骨钉大师奥罗与马托", 3),
            P4HC("WhiteDefender", "白色守卫者", 2),
            P4HC("MegaMossCharger", "大型苔藓冲锋者", 0),
            P4HC("Mawlek", "躁郁的毛里克", 2),
        };

        static P4HCfg P4HCfgOf(string key)
        {
            string k = key.Split('#')[0], v = key.Contains("#") ? key.Split('#')[1] : null;
            foreach (var c in P4HBosses) if (c.Key == k && c.Variant == v) return c;
            return null;
        }

        static int P4HStageOf(P4HCfg c) =>
            c.Stage >= 0 ? c.Stage : ProgressionBalance.BossRewards.TryGetValue(c.Key, out var r) ? r.Stage : 5;

        // ------------------------------------------------------------------ the stage's gear

        sealed class P4HGearSet
        {
            public int Stage, Cls;
            public ItemType[] Armor, Accessories;
            public ItemType Wings, Minion, Whip, Ammo;
            /// <summary>The weapons used in the clip, in turn (the strongest first; the summoner's whip).</summary>
            public List<ItemType> Rotation = new List<ItemType>();
            public override string ToString() =>
                $"阶段{Stage} {P4HClassZh[Cls]}：盔甲 {string.Join("、", Armor.Select(t => ItemDb.Get(t).Name))}；饰品 {string.Join("、", Accessories.Select(t => ItemDb.Get(t).Name))}" +
                $"{(Wings != None ? "、" + ItemDb.Get(Wings).Name : "，没有翅膀")}；武器 {string.Join("、", Rotation.Select(t => ItemDb.Get(t).Name))}" +
                $"{(Minion != None ? "；仆从 " + ItemDb.Get(Minion).Name : "")}{(Ammo != None ? "；弹药 " + ItemDb.Get(Ammo).Name : "")}";
        }

        /// <summary>
        /// The gear a player of this class has when he comes to this boss: every boss before it in Terraria's order beaten
        /// (VanillaBossProgression's steps), the boss's stage (Shop.DevStage); the class's best-scored armour of the stage,
        /// its accessories of the stage and before (none superseded, five at most), the stage's wings if any; the strongest
        /// of the class's weapons up to the stage (a summoner's minion, its whip), then one or two more of them to show.
        /// </summary>
        static P4HGearSet P4HGear(string key, int stage, int c, ItemType[] prefer)
        {
            if (key != null && VanillaBossProgression.All.TryGetValue(key, out var prof))
                VanillaBossProgression.DevClears = new HashSet<string>(VanillaBossProgression.All.Where(p => p.Value.Step < prof.Step && p.Value.Band <= stage).Select(p => p.Key));
            else
                // An area's enemies: the bosses of the stages before it beaten.
                VanillaBossProgression.DevClears = new HashSet<string>(VanillaBossProgression.All.Where(p => p.Value.Band < stage).Select(p => p.Key));
            Shop.DevStage = stage;
            bool Usable(ItemType t) => ProgressionBalance.Unlocked(t, stage, stage >= 4, false);
            float ArmorScore(ProgressionBalance.ArmorPath p)
            {
                var b = new Bonuses();
                foreach (var t in p.Items) b.Add(ItemDb.Get(t).Bonus);
                var h = ItemDb.Get(p.Items[0]);
                if (h.Set != ArmorSet.None && p.Items.All(t => ItemDb.Get(t).Set == h.Set))
                    b.Add(Equipment.SetBonus(h.Set == ArmorSet.Beetle ? ItemDb.Get(p.Items[1]).SetBonus : h.SetBonus != ArmorSet.None ? h.SetBonus : h.Set));
                if (c == 3) return (1 + b.MinionDamage) * (1 + b.MaxMinions);
                float damage = c == 0 ? b.MeleeDamage : c == 1 ? b.RangedDamage : b.MagicDamage;
                float crit = b.AllCrit + (c == 0 ? b.MeleeCrit : c == 1 ? b.RangedCrit : b.MagicCrit);
                return (1 + damage) * (1.04f + crit) * (c == 0 ? 1 + b.MeleeSpeed : 1);
            }
            var g = new P4HGearSet { Stage = stage, Cls = c };
            g.Armor = ProgressionBalance.ArmorPaths.Where(p => p.Stage <= stage && ProgressionBalance.ClassIndex(p.Class) == c && p.Items.All(Usable))
                .OrderByDescending(ArmorScore).ThenByDescending(p => p.Stage).First().Items;
            var candidates = ProgressionBalance.AccessoryPaths.Take(stage + 1).Reverse().SelectMany(r => r[c]).Where(Usable).Distinct().ToArray();
            var all = candidates.Select(t => ItemStack.Of(t)).ToArray();
            g.Accessories = candidates.Where(t => !ProgressionBalance.Superseded(t, all)).Take(5).ToArray();
            var wings = ItemDb.All.Where(d => d != null && d.Wing > 0 && d.IsAccessory && Usable(d.Type) && Player.TerraWings.Get(d.Wing) != null)
                .OrderByDescending(d => Player.TerraWings.Get(d.Wing).FlyTime).ThenByDescending(d => d.Stage).FirstOrDefault();
            g.Wings = wings != null ? wings.Type : None;
            int defense = Areas.Defense(stage);
            var pool = ProgressionBalance.Core.Take(stage + 1).SelectMany(r => r[c]).Where(Usable).Select(ItemDb.Get).Where(d => d != null).ToList();
            ItemDef main;
            if (c == 3)
            {
                main = pool.Where(d => d.IsSummon && !d.Sentry).OrderByDescending(d => ProgressionBalance.WeaponDps(d, defense)).First();
                g.Minion = main.Type;
                var whip = pool.Where(d => d.IsWhip).OrderByDescending(d => d.Stage).ThenByDescending(d => d.Damage).FirstOrDefault();
                g.Whip = whip != null ? whip.Type : None;
                if (whip != null) g.Rotation.Add(whip.Type);
            }
            else
            {
                main = pool.Where(d => d.IsWeapon && !d.IsSummon).OrderByDescending(d => ProgressionBalance.WeaponDps(d, defense)).First();
                g.Rotation.Add(main.Type);
            }
            // One or two more to show: the wished-for ones the stage has, else the stage's own row's with a projectile.
            var extra = new List<ItemType>();
            if (prefer != null) foreach (var t in prefer) if (Usable(t) && ItemDb.Get(t) != null && ProgressionBalance.ClassIndex(ItemDb.Get(t).Class) == c) extra.Add(t);
            for (int row = stage; row >= 0; row--)
                foreach (var d in ProgressionBalance.Core[row][c].Where(Usable).Select(ItemDb.Get).Where(d => d != null && d.IsWeapon && !d.IsSummon && !d.Sentry)
                             .OrderByDescending(d => d.Shoot != ProjKind.None ? 1 : 0).ThenByDescending(d => ProgressionBalance.WeaponDps(d, defense)))
                    extra.Add(d.Type);
            foreach (var t in extra)
            {
                if (g.Rotation.Count >= (c == 3 ? 2 : 3)) break;
                if (!g.Rotation.Contains(t) && t != g.Minion) g.Rotation.Add(t);
            }
            var shooter = g.Rotation.Select(ItemDb.Get).FirstOrDefault(d => d.UseAmmo != AmmoKind.None);
            if (shooter != null)
            {
                var ammo = ItemDb.All.Where(d => d != null && d.Ammo == shooter.UseAmmo && Usable(d.Type)).OrderByDescending(d => d.Damage).FirstOrDefault();
                g.Ammo = ammo != null ? ammo.Type : None;
            }
            return g;
        }

        /// <summary>The gear put on the first loadout (the other three emptied), its life, mana and abilities at the stage's.</summary>
        IEnumerator P4HWear(P4HGearSet g)
        {
            var inv = mod.Inventory;
            mod.DevHold = null;
            for (float t = 0f; t < 2f && inv.CurrentLoadout != 0; t += Time.deltaTime) { if (!mod.TrySwitchLoadout(0)) yield return null; }
            Minions.Instance?.Clear();
            for (int l = 0; l < Inventory.LoadoutCount; l++)
            {
                if (l == inv.CurrentLoadout) continue;
                var lo = inv.Loadouts[l];
                for (int i = 0; i < lo.Armor.Length; i++) lo.Armor[i] = default;
                for (int i = 0; i < lo.Accessories.Length; i++) lo.Accessories[i] = default;
                if (lo.Bag != null) for (int i = 0; i < lo.Bag.Length; i++) lo.Bag[i] = default;
            }
            for (int i = 0; i < inv.Slots.Length; i++) inv.Slots[i] = default;
            Equip(g.Armor, g.Accessories);
            if (g.Wings != None) inv.Accessories[5] = ItemStack.Of(g.Wings);
            int slot = 0;
            foreach (var t in g.Rotation) inv.Slots[slot++] = ItemStack.Of(t, ItemDb.Get(t).MaxStack > 1 ? ItemDb.Get(t).MaxStack : 1);
            if (g.Minion != None && !g.Rotation.Contains(g.Minion)) inv.Slots[slot++] = ItemStack.Of(g.Minion);
            if (g.Ammo != None) inv.Slots[Inventory.Size] = ItemStack.Of(g.Ammo, Mathf.Max(1, ItemDb.Get(g.Ammo).MaxStack));
            inv.Selected = 0;
            inv.Override = -1;
            var stats = mod.Stats;
            stats.Boosts.Clear();
            stats.LifeCrystals = new[] { 4, 8, 12, 15, 15, 15 }[g.Stage];
            stats.LifeFruits = new[] { 0, 0, 0, 0, 6, 14 }[g.Stage];
            stats.ManaCrystals = new[] { 1, 2, 3, 5, 7, 9 }[g.Stage];
            // The stage is the count of the movement abilities (ProgressionBalance.StageAbilityFlags, in the game's order).
            var pd = PlayerData.instance;
            for (int i = 0; i < ProgressionBalance.StageAbilityFlags.Length; i++) pd.SetBool(ProgressionBalance.StageAbilityFlags[i], i < g.Stage);
            inv.RefreshEffective();
            Equipment.Update(inv);
            stats.UpdateMaximums(pd, Equipment.Current.ManaMax);
            stats.FullHeal();
            stats.Mana = stats.ManaMax;
            yield return null;
            Plugin.Log.LogInfo($"[AutoTest] 录制H 配装 {g}，生命 {stats.LifeMax} 魔力 {stats.ManaMax}");
        }

        // ------------------------------------------------------------------ dev guards while recording

        /// <summary>Held bosses (by HealthManager instance): the least life a hit leaves them (no kill by a hit's burst between frames).</summary>
        static readonly Dictionary<int, int> p4hFloor = new Dictionary<int, int>();
        /// <summary>No hit counts toward a boss's stagger (its moves are what the clip shows).</summary>
        static bool p4hNoStun;
        static HarmonyLib.Harmony p4hHarmony;

        static void P4HPatch()
        {
            if (p4hHarmony != null) return;
            p4hHarmony = new HarmonyLib.Harmony("tk.dev.p4h");
            p4hHarmony.Patch(HarmonyLib.AccessTools.Method(typeof(HealthManager), "TakeDamage"),
                prefix: new HarmonyLib.HarmonyMethod(typeof(DevAutoTest), nameof(P4HTakeDamage)));
            p4hHarmony.Patch(HarmonyLib.AccessTools.Method(typeof(BossBalance), nameof(BossBalance.StunCounts)),
                prefix: new HarmonyLib.HarmonyMethod(typeof(DevAutoTest), nameof(P4HStunCounts)));
        }

        static void P4HTakeDamage(HealthManager __instance, HitInstance hitInstance)
        {
            if (__instance == null || !p4hFloor.TryGetValue(__instance.GetInstanceID(), out int floor)) return;
            int est = Mathf.CeilToInt(hitInstance.DamageDealt * Mathf.Max(1f, hitInstance.Multiplier)) + 1;
            if (__instance.hp - est < floor) __instance.hp = floor + est;
        }

        static bool P4HStunCounts(ref bool __result)
        {
            if (!p4hNoStun) return true;
            __result = false;
            return false;
        }

        static readonly System.Reflection.FieldInfo p4hControl = HarmonyLib.AccessTools.Field(typeof(BossKit), "Control");

        /// <summary>The seals and the world as P4ASetup makes them (its showcase gear replaced per boss), Master, the new enemy AI.</summary>
        IEnumerator P4HSetup()
        {
            yield return P4ASetup();
            // The creative save carries the Delicate Flower, which no seal takes past.
            PlayerData.instance.hasXunFlower = false;
            World.EnemyAiMode.Current = World.EnemyAiKind.New;
            GameDifficulty.Current = DifficultyLevel.Master;
            Plugin.Log.LogInfo($"[AutoTest] 录制H 设置：敌人AI {World.EnemyAiMode.Current}，难度 {GameDifficulty.Current}");
        }

        /// <summary>Every boss of a list: its stage's gear on, in by its seal, then its clip(s); on to the next whatever happens.</summary>
        IEnumerator P4HBossRun(IEnumerable<string> keys, string prefix, int takes = 1)
        {
            yield return P4HSetup();
            var report = new List<string>();
            foreach (var key in keys)
            {
                var c = P4HCfgOf(key);
                if (c == null) { report.Add(key + "：没有设置"); continue; }
                int stage = P4HStageOf(c);
                var g = P4HGear(c.Key, stage, c.Cls, c.Prefer);
                yield return P4HWear(g);
                var ac = P4AC(c.Key, c.Zh, 0, g.Rotation.ToArray());
                ac.ClearAdds = c.ClearAdds;
                ac.UntilParts = c.UntilParts;
                bool ok = false;
                World.EnemyAiMode.Current = World.EnemyAiKind.New;
                GameDifficulty.Current = DifficultyLevel.Master;
                yield return P4AEnter(ac, r => ok = r);
                // The seal's way in puts the stage back to what the seals need: the boss's own again.
                Shop.DevStage = stage;
                if (!ok) { report.Add(key + "：进不去"); yield return P4ALeave(); continue; }
                for (int take = 0; take < takes; take++)
                {
                    string clip = "v5H_" + prefix + "_" + key.Replace("@", "_").Replace("#", "_") + (take > 0 ? "_" + (take + 1) : "");
                    yield return P4HFight(c, g, clip, r => report.Add($"{key}（{P4HClassZh[c.Cls]}，阶段{stage}）：{r}"));
                }
                yield return P4ALeave();
            }
            P4AEnd();
            foreach (var r in report) Plugin.Log.LogInfo("[AutoTest] 录制H 结果 " + r);
        }

        BossKit P4HKit(string record)
        {
            foreach (var k in Object.FindObjectsByType<BossKit>(FindObjectsSortMode.None))
                if (k != null && k.Record == record && k.gameObject.activeInHierarchy && k.Hm != null && !k.Hm.isDead) return k;
            return null;
        }

        /// <summary>A boss's main script's state now (its kit's, else its Control) and its Stun Control's.</summary>
        static string P4HStates(HealthManager hm, BossKit kit)
        {
            if (hm == null) return "";
            PlayMakerFSM main = kit != null && kit.Hm == hm ? p4hControl.GetValue(kit) as PlayMakerFSM : null;
            PlayMakerFSM stun = null;
            foreach (var f in hm.GetComponents<PlayMakerFSM>())
            {
                if (f == null) continue;
                if (main == null && f.FsmName == "Control") main = f;
                if (f.FsmName == "Stun Control") stun = f;
            }
            return (main != null ? main.FsmName + ":" + main.ActiveStateName : "?") + (stun != null ? " | 硬直:" + stun.ActiveStateName : "");
        }

        /// <summary>
        /// A camera centre kept to what the game's own camera shows of this place (its camera lock area's bounds, for our
        /// view's size: the zoom included; else the room's grid), and then the hero kept well inside the view.
        /// </summary>
        static Vector2 P4HFrame(Vector2 want, Vector2 heroAt, Rect? local = null)
        {
            var gc = GameCameras.instance;
            var cam = gc != null ? gc.mainCamera : null;
            var cc = gc != null ? gc.cameraController : null;
            if (cam == null) return want;
            float halfH = Mathf.Abs(cam.transform.position.z) * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad), halfW = halfH * cam.aspect;
            if (halfH <= 0f) return want;
            Rect shown;
            var area = cc != null && cc.lockZoneList != null && cc.lockZoneList.Count > 0 ? cc.lockZoneList[cc.lockZoneList.Count - 1] : null;
            // A boss's own room from wall to wall first (a lock area can be the whole room's).
            if (local.HasValue) shown = local.Value;
            else if (area != null)
            {
                // The lock's bounds are for the camera's centre at the game's own view (zoom 2).
                float k = Player.TerraCamera.Zoom / 2f, gw = halfW * k, gh = halfH * k;
                float xMin = area.cameraXMin < 0f ? 14.6f : area.cameraXMin, xMax = area.cameraXMax < 0f ? cc.xLimit : area.cameraXMax;
                float yMin = area.cameraYMin < 0f ? 8.3f : area.cameraYMin, yMax = area.cameraYMax < 0f ? cc.yLimit : area.cameraYMax;
                shown = Rect.MinMaxRect(xMin - gw, yMin - gh, xMax + gw, yMax + gh);
            }
            else
            {
                var ts = World.TerrainSystem.Current;
                if (ts == null || ts.Region == null || ts.Region.Root == null) return want;
                shown = new Rect(ts.Region.Root.position, new Vector2(ts.Grid.Width, ts.Grid.Height) * World.CellGrid.CellSize);
            }
            Vector2 p = want;
            p.x = shown.width <= 2f * halfW ? shown.center.x : Mathf.Clamp(p.x, shown.xMin + halfW, shown.xMax - halfW);
            p.y = shown.height <= 2f * halfH ? shown.center.y : Mathf.Clamp(p.y, shown.yMin + halfH, shown.yMax - halfH);
            // The hero never off the screen for it.
            const float inside = 2f;
            p.x = Mathf.Clamp(p.x, heroAt.x - halfW + inside, heroAt.x + halfW - inside);
            p.y = Mathf.Clamp(p.y, heroAt.y - halfH + inside, heroAt.y + halfH - inside);
            return p;
        }

        /// <summary>The top of the Radiance's abyss (its "Abyss Pit", risen in her second arena), or none.</summary>
        static float P4HAbyssTop()
        {
            var pit = GameObject.Find("Abyss Pit");
            if (pit == null) return float.MinValue;
            float top = pit.transform.position.y;
            bool any = false;
            foreach (var cc in pit.GetComponentsInChildren<Collider2D>())
                if (cc != null && cc.enabled) { top = any ? Mathf.Max(top, cc.bounds.max.y) : cc.bounds.max.y; any = true; }
            return top;
        }

        /// <summary>How far off the boss a weapon is used: a sword's reach, a spear's, a whip's, else a shooter's distance.</summary>
        static float P4HReach(ItemDef d)
        {
            if (d == null) return 6f;
            if (d.IsWhip) return 2.8f;
            if (d.Class == DamageClass.Melee)
            {
                if (d.Shoot == ProjKind.None) return 1.6f;
                if (d.Thrust || d.HoldOut && !d.Channel) return 2.6f;
                if (d.Channel) return 4.5f;
                return d.NoMelee ? 5.5f : 2.2f;
            }
            return 6.5f;
        }

        /// <summary>
        /// The fight recorded (see the class's summary); the boss's health held; its states logged against the clip's time.
        /// </summary>
        IEnumerator P4HFight(P4HCfg c, P4HGearSet g, string clip, System.Action<string> result)
        {
            string record = c.Key.Split('@')[0];
            P4HPatch();
            p4hFloor.Clear();
            p4hNoStun = !c.Stuns;
            var hero = HeroController.instance;
            var inv = mod.Inventory;
            var rb = hero.GetComponent<Rigidbody2D>();
            var col = hero.GetComponent<BoxCollider2D>();
            const int terrain = 1 << ChunkColliders.TerrainLayer;
            string scene = GameManager.instance.sceneName;
            // Wings to stay up on (a flight of a second and a half or more; the Fledgling Wings only lift a jump).
            bool wings = g.Wings != None && Player.TerraWings.Get(ItemDb.Get(g.Wings).Wing) is Player.WingDef wd && wd.FlyTime >= 90;
            bool anyBoss = false;
            int patrolDir = 1;
            List<HealthManager> Parts()
            {
                if (c.AnyEnemy)
                {
                    var all = new List<HealthManager>();
                    foreach (var hm in Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None))
                        if (hm.gameObject.activeInHierarchy && !hm.isDead && hm.hp > 0 && hm.hp < 99999 && hm.gameObject.scene.name == scene) all.Add(hm);
                    return all;
                }
                var list = BossesOf(record, false);
                if (list.Count == 0 || anyBoss)
                {
                    list.Clear();
                    foreach (var hm in Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None))
                        if (hm.gameObject.activeInHierarchy && !hm.isDead && hm.hp > 0 && EnemyStats.Of(hm).Boss) list.Add(hm);
                    if (list.Count > 0) anyBoss = true;
                }
                return list;
            }
            HealthManager Pick()
            {
                HealthManager best = null;
                float bd = float.MaxValue;
                foreach (var hm in Parts())
                {
                    if (hm.isDead || hm.hp <= 0 || EnemyStats.Defeated(hm) || P4AWaitsPhase(hm)) continue;
                    float d = Vector2.Distance(P4ACentre(hm), col.bounds.center) + (hm.IsInvincible ? 6f : 0f);
                    // A room's crowd: the ones near and in sight (not through rock), the ones ahead first.
                    if (c.AnyEnemy)
                    {
                        if (d > 18f || Physics2D.Linecast(col.bounds.center, P4ACentre(hm), terrain).collider != null) continue;
                        if (Mathf.Sign(P4ACentre(hm).x - col.bounds.center.x) != patrolDir) d += 3f;
                    }
                    if (d < bd) { bd = d; best = hm; }
                }
                return best;
            }
            // Its health held from the first sight of it: none of it goes before the clip, then a little a second, never
            // under Keep of where it began.
            var floors = new Dictionary<int, (int Hp, float T)>();
            float recStart = float.MaxValue;
            bool letGo = false;
            void Hold()
            {
                if (letGo) p4hFloor.Clear();
                if (c.AnyEnemy || letGo) return;
                foreach (var hm in Parts())
                {
                    if (hm.isDead || hm.hp <= 0) continue;
                    int id = hm.GetInstanceID();
                    if (!floors.TryGetValue(id, out var f)) floors[id] = f = (hm.hp, Time.time);
                    float allowed = f.Hp * (recording ? Mathf.Max(c.Keep, 1f - 0.03f * (Time.time - Mathf.Max(f.T, recStart))) : 1f);
                    if (hm.hp < allowed) hm.hp = Mathf.CeilToInt(allowed);
                    p4hFloor[id] = Mathf.Max(1, Mathf.CeilToInt(f.Hp * c.Keep * 0.9f));
                }
            }
            // The boss up: there, near, the player free.
            float wait0 = Time.time, nextStrike = 0f, enough = -1f;
            HealthManager first = null;
            while (Time.time - wait0 < (c.ClearAdds || c.UntilParts > 1 ? 45f : 18f))
            {
                P4AKeepAlive();
                if (c.UntilParts > 1)
                {
                    int live = 0;
                    foreach (var hm in Parts()) if (!hm.isDead && hm.hp > 0) live++;
                    if (live >= c.UntilParts) { if (enough < 0f) enough = Time.time; }
                    else if (Time.time >= nextStrike && Time.time - wait0 > 2f)
                    {
                        nextStrike = Time.time + 0.3f;
                        foreach (var hm in Parts())
                        {
                            if (hm.isDead || hm.hp <= 0) continue;
                            var hc = hm.GetComponent<Collider2D>();
                            var info = EnemyStats.Of(hm);
                            if (hc != null && hc.enabled) mod.Melee.HitWith(hc, Mathf.CeilToInt(0.3f * hm.hp * (info != null ? info.Scale : 1f) + 50f), 0f, 0f, 1f, hc.bounds.center);
                        }
                    }
                    if (enough < 0f || Time.time - enough < 2.5f) { yield return null; continue; }
                    floors.Clear();
                }
                Hold();
                if (c.ClearAdds && Time.time >= nextStrike)
                {
                    nextStrike = Time.time + 0.3f;
                    foreach (var hm in Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None))
                    {
                        if (hm.isDead || hm.hp <= 0 || !hm.gameObject.activeInHierarchy || UI.EnemyHover.RecordOf(hm) == record || EnemyStats.Of(hm).Boss) continue;
                        var ac = hm.GetComponent<Collider2D>();
                        if (ac != null && ac.enabled) mod.Melee.HitWith(ac, 99999, 0f, 0f, 1f, ac.bounds.center);
                    }
                }
                if (UI.HudDialogue.DevOpen && Time.time - wait0 > 1f) UI.HudDialogue.Close();
                first = Pick();
                if (first != null && GameHooks.PlayerHasControl() && Vector2.Distance(P4ACentre(first), col.bounds.center) < 24f && Time.time - wait0 > 0.4f
                    && (OnScreen(P4ACentre(first)) && !first.IsInvincible || Time.time - wait0 > 9f)) break;
                yield return null;
            }
            if (first == null)
            {
                result($"{scene}：首领没出现（{BossSummonStart.DevLast}）");
                yield break;
            }
            var kit = P4HKit(record);
            Plugin.Log.LogInfo($"[AutoTest] 录制H {c.Key} 首领出场：{first.name}（{UI.EnemyHover.NameOf(first)}）hp {first.hp}/{EnemyStats.Of(first)?.MaxHp}，新版AI {(kit != null ? "有，场地 " + kit.Arena.ToString("F1") + " 档 " + kit.Rank : "没有")}，{scene}，等了 {Time.time - wait0:F1}s");
            if (c.HpShare > 0f)
            {
                foreach (var hm in Parts())
                {
                    var info = EnemyStats.Of(hm);
                    if (info == null) continue;
                    hm.hp = Mathf.Max(1, Mathf.RoundToInt(info.MaxHp * c.HpShare));
                    floors.Remove(hm.GetInstanceID());
                }
                // A phase the lower life brings on (its own scripts look at it on a hit) under way before the clip.
                for (float t = 0f; t < 1.5f; t += Time.deltaTime) { P4AKeepAlive(); Hold(); yield return null; }
            }
            aimInit = false;
            SmoothAim(P4ACentre(first), 99f);
            // A summoner's minions called before the clip, as many as he has room for.
            if (g.Minion != None)
            {
                int si = SlotOf(g.Minion);
                for (int k = 0; si >= 0 && k < 6; k++)
                {
                    inv.Selected = si;
                    var spot = (Vector2)col.bounds.center + new Vector2(k % 2 == 0 ? 1.5f : -1.5f, 1.8f);
                    for (float t = 0f; t < 0.15f; t += Time.deltaTime) { SmoothAim(spot, 14f); P4AKeepAlive(); Hold(); yield return null; }
                    for (float t = 0f; t < 1f && (!mod.DevReady || mod.IsUsingItem); t += Time.deltaTime) { Hold(); yield return null; }
                    mod.DevUseSelected(aimNow);
                    yield return new WaitForSeconds(0.15f);
                }
            }
            Player.TerraCamera.Zoom = c.Zoom;
            if (g.Rotation.Count > 0 && SlotOf(g.Rotation[0]) >= 0) inv.Selected = SlotOf(g.Rotation[0]);
            yield return null;
            for (float t = 0f; t < 3f && GameCameras.instance != null && GameCameras.instance.cameraFadeFSM.ActiveStateName != "Normal"; t += Time.deltaTime) { P4AKeepAlive(); Hold(); yield return null; }
            P4AClearChat();
            UI.HudDialogue.Close();
            bool light0 = World.LightMap.Enabled;
            if (c.NoLight) World.LightMap.Enabled = false;
            int moves0 = BossAi.DevMoves, new0 = BossAi.DevNewStates, branch0 = BossAi.DevBranches, air0 = BossAi.DevAntiAir, stuns0 = BossBalance.DevStuns;
            int kills0 = Bestiary.KillsOf(c.Key);
            // A boss's room seen as its own fight would show it (when no camera lock holds it): from wall to wall round it.
            Rect? localArena = null;
            if (!c.AnyEnemy)
            {
                Vector2 bc = P4ACentre(first);
                var fl = Physics2D.Raycast(bc, Vector2.down, 30f, terrain);
                float gy = fl.collider != null ? fl.point.y : bc.y - 2f;
                var probe = new Vector2(bc.x, gy + 1.5f);
                var lw = Physics2D.Raycast(probe, Vector2.left, 45f, terrain);
                var rw = Physics2D.Raycast(probe, Vector2.right, 45f, terrain);
                var cl = Physics2D.Raycast(probe, Vector2.up, 40f, terrain);
                if (lw.collider != null && rw.collider != null)
                {
                    float top = cl.collider != null ? cl.point.y : gy + 18f;
                    localArena = Rect.MinMaxRect(lw.point.x - 1.5f, gy - 3f, rw.point.x + 1.5f, Mathf.Max(top + 1.5f, gy + 12f));
                }
                Plugin.Log.LogInfo($"[AutoTest] 录制H {clip} 首领周围的场地 {(localArena.HasValue ? localArena.Value.ToString("F1") : "没找到墙")}");
            }
            Plugin.Log.LogInfo($"[AutoTest] 录制H {clip} 开始：{P4HClassZh[g.Cls]} 阶段{g.Stage}，{(wings ? "有翅膀" : "没翅膀，地面打法")}");
            StartRec(clip);
            recStart = Time.time;
            float start = Time.time;
            int side = col.bounds.center.x < P4ACentre(first).x ? -1 : 1, move = 0, frames = 0, onScreen = 0, still = 0;
            Vector2 last = P4ACentre(first), lastHero = col.bounds.center, lastAt = last;
            bool air = false;
            float modeAt = 0f, sweepFrom = 0f, airFromX = 0f, farX = 0f;
            int airs = 0;
            // Without wings: a vault over it (jump held, to its far side), a hop now and then.
            float vaultUntil = -1f, lastVault = -9f, hopUntil = -1f, hopAt = 1.5f, bossVx = 0f;
            var rng = new System.Random(clip.GetHashCode());
            string lastStates = "";
            int newMoves = 0;
            var cam = GameCameras.instance != null ? GameCameras.instance.mainCamera : null;
            Vector2 look = col.bounds.center;
            float abyssTop = float.MinValue, nextAbyssLook = 0f;
            var gates = new List<Collider2D>();
            if (c.AnyEnemy)
                foreach (var tp in Object.FindObjectsByType<TransitionPoint>(FindObjectsSortMode.None))
                    if (tp != null && tp.gameObject.scene.name == scene && tp.GetComponent<Collider2D>() is Collider2D gc) gates.Add(gc);
            bool cam0 = Player.TerraCamera.Enabled;
            float beatenAt = -1f;
            var stunTimes = new List<string>();
            bool stunNow = false;
            int lastWeapon = -1;
            float segLen = Mathf.Max(4.5f, c.Seconds / Mathf.Max(1, g.Rotation.Count));
            while (Time.time - start < c.Seconds || beatenAt > 0f && Time.time - beatenAt < 5f)
            {
                P4AKeepAlive();
                float t = Time.time - start;
                var target = Pick();
                if (c.Kill && !letGo && t > 4f) letGo = true;
                Hold();
                if (UI.HudDialogue.DevOpen) UI.HudDialogue.Close();
                if (kit == null || kit.Hm == null || kit.Hm.isDead) kit = P4HKit(record);
                if (c.Kill && beatenAt < 0f && (Bestiary.KillsOf(c.Key) > kills0 || target == null && t > 6f))
                {
                    beatenAt = Time.time;
                    Plugin.Log.LogInfo($"[AutoTest] 录制H {clip} 打倒在 {t:F2}s");
                }
                Vector2 at = target != null ? P4ACentre(target) : last;
                if (c.AnyEnemy && target == null)
                {
                    // None in sight: on toward the nearest one left in the room (a wall ahead turns him round), looking ahead.
                    Vector2 from = col.bounds.center;
                    HealthManager far = null;
                    float fd = 60f;
                    foreach (var hm in Parts())
                    {
                        if (hm.isDead || hm.hp <= 0) continue;
                        float d = Vector2.Distance(P4ACentre(hm), from);
                        if (d < fd) { fd = d; far = hm; }
                    }
                    if (far != null && Mathf.Abs(P4ACentre(far).x - from.x) > 2f) patrolDir = P4ACentre(far).x > from.x ? 1 : -1;
                    if (Physics2D.Raycast(from, Vector2.right * patrolDir, 2.5f, terrain).collider != null) patrolDir = -patrolDir;
                    at = from + new Vector2(patrolDir * 7f, 0.3f);
                    side = -patrolDir;
                }
                else if (c.AnyEnemy) patrolDir = at.x > col.bounds.center.x ? 1 : -1;
                if (Time.deltaTime > 0f) bossVx = Mathf.Lerp(bossVx, (at.x - lastAt.x) / Time.deltaTime, 0.3f);
                lastAt = at;
                last = at;
                // The boss's states against the clip's time.
                string states = target != null ? P4HStates(target, kit) : "";
                if (states != lastStates)
                {
                    lastStates = states;
                    Plugin.Log.LogInfo($"[AutoTest] 录制H {clip} {t:F2}s {states}{(kit != null ? $" 档{kit.Rank}{(kit.AirCamping ? " 对空中" : "")}" : "")}");
                    string mainState = states.Split('|')[0];
                    bool stun = mainState.IndexOf("Stun", System.StringComparison.OrdinalIgnoreCase) >= 0;
                    if (stun && !stunNow) stunTimes.Add(t.ToString("F1"));
                    stunNow = stun;
                }
                if (BossAi.DevNewStates + BossAi.DevBranches + BossAi.DevMoves != newMoves)
                {
                    newMoves = BossAi.DevNewStates + BossAi.DevBranches + BossAi.DevMoves;
                    Plugin.Log.LogInfo($"[AutoTest] 录制H {clip} {t:F2}s 新招计数 招{BossAi.DevMoves - moves0} 新状态{BossAi.DevNewStates - new0} 分支{BossAi.DevBranches - branch0} 对空{BossAi.DevAntiAir - air0}");
                }
                if (beatenAt > 0f)
                {
                    // The fall: the hero eases to a stop near it, the weapon let go, the view eased onto its body.
                    mod.DevHold = false;
                    Player.TerraMovement.DevMove = 0;
                    Player.TerraMovement.DevJump = false;
                    SmoothAim(at, 4f);
                    if (cam != null)
                    {
                        Vector2 hc2 = col.bounds.center;
                        var want2 = P4HFrame(Vector2.Lerp(hc2, at, 0.7f), hc2, localArena);
                        look = Vector2.Lerp(look, want2, 1f - Mathf.Exp(-2.5f * Time.deltaTime));
                        Player.TerraCamera.DevLook = look;
                    }
                    yield return null;
                    continue;
                }
                SmoothAim(at, 10f);
                int wi = g.Rotation.Count == 0 ? -1 : (int)(t / segLen) % g.Rotation.Count;
                var held = wi >= 0 ? ItemDb.Get(g.Rotation[wi]) : null;
                if (wi >= 0)
                {
                    int slotIndex = SlotOf(g.Rotation[wi]);
                    if (wi != lastWeapon && mod.IsUsingItem) mod.DevHold = false;
                    else
                    {
                        if (slotIndex >= 0) inv.Selected = slotIndex;
                        lastWeapon = wi;
                        mod.DevHold = target != null;
                        if (target != null && mod.DevReady && !mod.IsUsingItem) mod.DevUseSelected(aimNow);
                    }
                }
                float reach = c.Dist > 0f ? c.Dist : P4HReach(held);
                Vector2 hcen = col.bounds.center;
                frames++;
                if (target != null && OnScreen(at)) onScreen++;
                if ((hcen - lastHero).magnitude < 0.02f) still++;
                lastHero = hcen;
                // The room: the kit's arena, else round the boss.
                Rect arena = kit != null ? kit.Arena : new Rect(at.x - 14f, at.y - 6f, 28f, 20f);
                float ground = kit != null ? kit.GroundBelow(hcen + Vector2.up * 0.2f, 40f) : hcen.y - 1.2f;
                if (kit == null)
                {
                    var down = Physics2D.Raycast(hcen, Vector2.down, 40f, terrain);
                    if (down.collider != null) ground = down.point.y;
                }
                float ceiling = kit != null ? kit.CeilingAbove(hcen, 40f) : hcen.y + 30f;
                if (kit == null)
                {
                    var upHit = Physics2D.Raycast(hcen, Vector2.up, 40f, terrain);
                    if (upHit.collider != null) ceiling = upHit.point.y;
                }
                // The Radiance's second arena: the abyss risen under its platforms is the floor there (the first arena's
                // ground under it is gone); over a gap with the wings nearly spent, down onto the nearest platform; one who
                // falls in anyway is taken out by the abyss as the game does (his invincibility let go for it).
                bool overGap = false;
                float platformX = float.NaN;
                if (record == "FinalBoss")
                {
                    if (Time.time >= nextAbyssLook) { nextAbyssLook = Time.time + 0.5f; abyssTop = P4HAbyssTop(); }
                    if (abyssTop > ground + 0.5f)
                    {
                        overGap = true;
                        ground = abyssTop + 1.5f;
                        if (Player.TerraMovement.WingTimeLeft < 45f)
                            for (float d = 0.5f; d <= 16f && float.IsNaN(platformX); d += 0.5f)
                                foreach (float sx in new[] { d, -d })
                                {
                                    var from = new Vector2(hcen.x + sx, hcen.y + 0.5f);
                                    var hit = Physics2D.Raycast(from, Vector2.down, Mathf.Max(1f, from.y - abyssTop), terrain);
                                    if (hit.collider != null && hit.point.y > abyssTop + 0.5f) { platformX = from.x; break; }
                                }
                    }
                    if (abyssTop > float.MinValue && hcen.y < abyssTop + 0.3f) PlayerData.instance.isInvincible = false;
                }
                float top = target != null ? P4ATop(target) : at.y + 1.5f;
                float near = Mathf.Abs(hcen.x - at.x);
                float dist = reach + Mathf.Min(0.9f, reach * 0.15f) * Mathf.Sin(t * 1.3f);
                float xMin = arena.xMin + 1.3f, xMax = arena.xMax - 1.3f;
                float wantX, wantY;
                bool jump;
                if (wings)
                {
                    // The floor a while, then a flight: over it (sooner with it right on him), or every other one across the room.
                    if (!air && (t - modeAt > c.GroundTime || near < 2.2f && reach > 3f && t - modeAt > 0.8f && at.y < hcen.y + 2f))
                    {
                        air = true;
                        modeAt = t;
                        airs++;
                        airFromX = hcen.x;
                        farX = at.x > arena.center.x ? arena.xMin + 2.5f : arena.xMax - 2.5f;
                        // Not so far that it leaves the view.
                        farX = Mathf.Clamp(farX, at.x - 13f, at.x + 13f);
                        if (airs % 2 == 0 && Mathf.Abs(farX - at.x) > reach + 3f) side = farX > at.x ? 1 : -1;
                        else { airs |= 1; sweepFrom = side; side = -side; }
                    }
                    else if (air && t - modeAt > c.AirTime)
                    {
                        air = false;
                        modeAt = t;
                    }
                    if (!air)
                    {
                        wantX = at.x + side * dist;
                        wantY = ground + 1.0f;
                    }
                    else
                    {
                        float u = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - modeAt) / (c.AirTime * 0.8f)));
                        float sweep = Mathf.Max(dist, 4f);
                        wantX = airs % 2 == 0 ? Mathf.Lerp(airFromX, farX, u) : Mathf.Lerp(at.x + sweepFrom * sweep, at.x + side * sweep, u);
                        wantY = Mathf.Max(ground + c.Air, top + 2.4f) + 0.5f * Mathf.Sin(t * 1.7f);
                    }
                    if (!air && (wantX < xMin || wantX > xMax) && Mathf.Abs(at.x - (side > 0 ? xMax : xMin)) < 3f) side = -side;
                    if (overGap && !float.IsNaN(platformX)) { wantX = platformX; air = false; modeAt = t; }
                    wantX = Mathf.Clamp(wantX, xMin, xMax);
                    wantY = Mathf.Clamp(wantY, ground + 1.0f, Mathf.Max(ground + 1.0f, ceiling - 1.8f));
                    if (overGap && !float.IsNaN(platformX)) wantY = hcen.y;
                    float dy = wantY - hcen.y, vy = rb.linearVelocity.y;
                    jump = dy > 0.35f || (dy > -0.6f && vy < -1.2f && air);
                    if (jump && hero.cState.onGround) Player.TerraMovement.DevGroundJump(hero);
                }
                else
                {
                    // On foot: his distance on its side; over it when it comes at him (a charge, or right on him from a
                    // shooter's distance), its far side then; a short hop now and then.
                    bool coming = Mathf.Abs(bossVx) > 7f && Mathf.Sign(bossVx) == Mathf.Sign(hcen.x - at.x);
                    bool low = at.y < hcen.y + 2.2f;
                    bool onFloor = hero.cState.onGround;
                    if (onFloor && t >= vaultUntil && t - lastVault > 1.3f && low
                        && (coming && near < 5f || reach > 3f && near < 1.8f))
                    {
                        side = at.x > hcen.x ? 1 : -1;
                        vaultUntil = t + 0.5f;
                        lastVault = t;
                        Player.TerraMovement.DevGroundJump(hero);
                    }
                    else if (onFloor && t >= hopAt && t >= vaultUntil)
                    {
                        hopUntil = t + 0.12f + (float)rng.NextDouble() * 0.2f;
                        hopAt = t + 1.8f + (float)rng.NextDouble() * 1.8f;
                        Player.TerraMovement.DevGroundJump(hero);
                    }
                    wantX = at.x + side * (t < vaultUntil ? dist + 1.2f : dist);
                    if ((wantX < xMin || wantX > xMax) && t >= vaultUntil)
                    {
                        // Its back to the wall: the far side then, over it when it is near.
                        if (near < 3f && onFloor && t - lastVault > 1.3f && low)
                        {
                            side = -side;
                            vaultUntil = t + 0.5f;
                            lastVault = t;
                            Player.TerraMovement.DevGroundJump(hero);
                        }
                        else if (near >= 3f) side = -side;
                    }
                    wantX = Mathf.Clamp(wantX, xMin, xMax);
                    jump = t < vaultUntil || t < hopUntil;
                }
                float dx = wantX - hcen.x;
                if (Mathf.Abs(dx) > 0.9f) move = dx > 0 ? 1 : -1;
                else if (Mathf.Abs(dx) < 0.3f) move = 0;
                // Never out through a room's gate (a crowd's room is fought in, not left).
                if (c.AnyEnemy && move != 0)
                    foreach (var gate in gates)
                    {
                        if (gate == null) continue;
                        Vector2 cp = gate.ClosestPoint(hcen);
                        if (Vector2.Distance(cp, hcen) < 4.5f && Mathf.Sign(cp.x - hcen.x) == move) { move = 0; patrolDir = -patrolDir; side = -side; break; }
                    }
                Player.TerraMovement.DevMove = move;
                Player.TerraMovement.DevJump = jump;
                // The view leans toward the boss so both are in it, kept within the fight's own camera bounds.
                if (cam != null && c.Lean > 0f)
                {
                    var want = P4HFrame(hcen + Vector2.ClampMagnitude((at - hcen) * c.Lean, 6f), hcen, localArena);
                    look = t < 0.05f ? want : Vector2.Lerp(look, want, 1f - Mathf.Exp(-3f * Time.deltaTime));
                    Player.TerraCamera.DevLook = look;
                }
                yield return null;
            }
            Player.TerraMovement.DevMove = 0;
            Player.TerraMovement.DevJump = false;
            mod.DevHold = null;
            StopRec();
            p4hFloor.Clear();
            p4hNoStun = false;
            World.LightMap.Enabled = light0;
            Player.TerraCamera.DevLook = null;
            Player.TerraCamera.Enabled = cam0;
            mod.DevAim = null;
            string summary = $"{clip} {Time.time - start:F1}s，{scene}，首领在画面里 {(frames > 0 ? onScreen * 100 / frames : 0)}%，主角不动的帧 {still}/{frames}，" +
                             $"新招 {BossAi.DevMoves - moves0}、新状态 {BossAi.DevNewStates - new0}、分支 {BossAi.DevBranches - branch0}、对空 {BossAi.DevAntiAir - air0}、硬直计数 {BossBalance.DevStuns - stuns0}、眩晕在 {string.Join("/", stunTimes)}s" +
                             (c.Kill ? (beatenAt > 0f ? $"，打倒在 {beatenAt - start:F1}s" : "，没打倒") : "");
            Plugin.Log.LogInfo("[AutoTest] 录制H " + summary);
            result(summary);
        }

        // ------------------------------------------------------------------ the suites

        IEnumerator P4_v5H_keys(HeroController hero)
        {
            string keys = System.Environment.GetEnvironmentVariable("P4H_KEYS") ?? "Hornet";
            string prefix = System.Environment.GetEnvironmentVariable("P4H_PREFIX") ?? "boss";
            int takes = int.TryParse(System.Environment.GetEnvironmentVariable("P4H_TAKES"), out int n) ? n : 1;
            yield return P4HBossRun(keys.Split('.'), prefix, takes);
        }

        // ------------------------------------------------------------------ the rooms' crowds

        /// <summary>
        /// A room of each area fought through by one class at the area's stage (Areas.LevelOf), the new enemy AI on, on
        /// Master: the hero set down by its thickest crowd, then the nearest enemy in sight fought each moment, on through
        /// the room when none is about. P4H_MOBS: "Room:class[:zoom];Room:class" (class 0 melee .. 3 summon); P4H_SECONDS the length.
        /// </summary>
        IEnumerator P4HMobRun(IEnumerable<string> entries, float seconds)
        {
            yield return P4HSetup();
            var report = new List<string>();
            foreach (var e in entries)
            {
                var parts = e.Split(':');
                string room = parts[0];
                int cls = parts.Length > 1 && int.TryParse(parts[1], out int k) ? k : 0;
                float zoom = parts.Length > 2 && float.TryParse(parts[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float z) ? z : 2f;
                int stage = Areas.LevelOf(room);
                var g = P4HGear(null, stage, cls, null);
                yield return P4HWear(g);
                World.EnemyAiMode.Current = World.EnemyAiKind.New;
                GameDifficulty.Current = DifficultyLevel.Master;
                // The gameplay setting's three enemies to an original (1-4; 2 by default): a crowd to cut through.
                int mult0 = EnemyRespawn.Multiplier;
                EnemyRespawn.Multiplier = 3;
                yield return EnterRoom(room);
                EnemyRespawn.Multiplier = mult0;
                Shop.DevStage = stage;
                var hero = HeroController.instance;
                if (GameManager.instance.sceneName != room) { report.Add(room + "：没进去"); continue; }
                for (float t = 0f; t < 1f; t += Time.deltaTime) { P4AKeepAlive(); yield return null; }
                var at = BCluster(room, out int most);
                hero.transform.position = Clear(hero, at + new Vector2(-7f, 1f));
                var rb = hero.GetComponent<Rigidbody2D>();
                if (rb != null) rb.linearVelocity = Vector2.zero;
                for (float t = 0f; t < 1.2f; t += Time.deltaTime) { P4AKeepAlive(); yield return null; }
                Plugin.Log.LogInfo($"[AutoTest] 录制H 小怪 {room}（{Areas.AreaName(room)}，阶段{stage}，{P4HClassZh[cls]}）：一屏最多 {most} 只，站到 {hero.transform.position}");
                var c = new P4HCfg { Key = room, Zh = Areas.AreaName(room), Cls = cls, AnyEnemy = true, Seconds = seconds, Zoom = zoom, GroundTime = 2.4f, AirTime = 4f, Air = 3.8f };
                yield return P4HFight(c, g, "v5H_mob_" + room, r => report.Add($"{room}（{Areas.AreaName(room)}，{P4HClassZh[cls]}，阶段{stage}）：{r}"));
            }
            P4AEnd();
            foreach (var r in report) Plugin.Log.LogInfo("[AutoTest] 录制H 结果 " + r);
        }

        IEnumerator P4_v5H_mobs(HeroController hero)
        {
            string list = System.Environment.GetEnvironmentVariable("P4H_MOBS") ?? "Crossroads_21:0";
            float seconds = float.TryParse(System.Environment.GetEnvironmentVariable("P4H_SECONDS"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float s) ? s : 22f;
            yield return P4HMobRun(list.Split(';'), seconds);
        }

        /// <summary>Dev: every boss's gear in the log, nothing recorded.</summary>
        IEnumerator P4_v5H_gear(HeroController hero)
        {
            yield return P4HSetup();
            foreach (var c in P4HBosses)
            {
                var g = P4HGear(c.Key, P4HStageOf(c), c.Cls, c.Prefer);
                Plugin.Log.LogInfo($"[AutoTest] 录制H 配装表 {c.Key}{(c.Variant != null ? "#" + c.Variant : "")}：{g}");
            }
        }
    }
}
