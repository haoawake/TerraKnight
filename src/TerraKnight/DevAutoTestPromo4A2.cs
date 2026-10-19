using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using TerraKnight.Items;
using TerraKnight.World;
using UnityEngine;
using static TerraKnight.Items.ItemType;

namespace TerraKnight
{
    /// <summary>
    /// Dev, the 4.0 trailer's bosses (recorder A), part two: Godhome's own fights (the Absolute Radiance, the Sisters of
    /// Battle) entered as a Hall of Gods statue enters them, the Hall of Gods walked through, and the finale's long takes.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        /// <summary>Every statue of the Hall of Gods seen, unlocked and its three tiers done (their plaques lit).</summary>
        static int P4AStatuesLit()
        {
            int n = 0;
            var pd = PlayerData.instance;
            foreach (var f in typeof(PlayerData).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (f.FieldType.Name != "Completion") continue;
                object v = f.GetValue(pd);
                foreach (var name in new[] { "hasBeenSeen", "isUnlocked", "completedTier1", "completedTier2", "completedTier3", "seenTier3Unlock" })
                {
                    var g = f.FieldType.GetField(name);
                    if (g != null && g.FieldType == typeof(bool)) g.SetValue(v, true);
                }
                f.SetValue(pd, v);
                n++;
            }
            return n;
        }

        /// <summary>Into a Godhome fight as a statue's challenge goes (its boss scene set up at a level, in by the dream door).</summary>
        IEnumerator P4AGodhome(string room, int level, System.Action<bool> done)
        {
            var gm = GameManager.instance;
            if (gm.sceneName != "Town") yield return SealTravel("Town", "left1");
            BossSceneController.SetupEvent = self => { self.BossLevel = level; self.DreamReturnEvent = "DREAM RETURN"; };
            PlayerData.instance.dreamReturnScene = "Town";
            gm.BeginSceneTransition(new GameManager.SceneLoadInfo
            {
                SceneName = room, EntryGateName = "door_dreamEnter", HeroLeaveDirection = GlobalEnums.GatePosition.unknown,
                EntryDelay = 0f, WaitForSceneTransitionCameraFade = true, Visualization = GameManager.SceneLoadVisualizations.GodsAndGlory,
            });
            for (float until = Time.realtimeSinceStartup + 25f; Time.realtimeSinceStartup < until && (gm.sceneName != room || gm.IsInSceneTransition);) { P4AKeepAlive(); yield return null; }
            if (gm.sceneName != room) { Plugin.Log.LogWarning($"[AutoTest] 录制A 进不去 {room}，在 {gm.sceneName}"); done(false); yield break; }
            yield return new WaitForSeconds(0.8f);
            var fade = GameCameras.instance != null ? GameCameras.instance.cameraFadeFSM : null;
            if (fade != null && fade.ActiveStateName != "Normal" && fade.ActiveStateName != "FadeIn") gm.FadeSceneIn();
            done(true);
        }

        // ------------------------------------------------------------------ the kill clips

        /// <summary>The kill clips' bosses: a loadout other than its boss clip's, its weapons for the whole fight.</summary>
        static readonly P4ACfg[] P4AKills =
        {
            P4AC("FalseKnight", "假骑士", 1, new[] { SDMG, ElectrosphereLauncher }),
            P4AC("BigFly", "格鲁兹之母", 2, new[] { NebulaArcanum, BetsysWrath }),
            P4AC("MageLord", "灵魂大师", 0, new[] { StarWrath, Meowmere }),
            P4AC("Hornet", "大黄蜂", 1, new[] { Phantasm, Xenopopper }),
            P4AArena(P4AC("MantisLord", "螳螂领主", 2, new[] { RazorbladeTyphoon, NebulaBlaze })),
            P4AC("InfectedKnight", "破碎容器", 1, new[] { CelebrationMk2, VortexBeater }),
            P4AC("Grimm", "格林", 0, new[] { TerraBlade, DayBreak }),
            P4AC("HiveKnight", "蜂巢骑士", 2, new[] { LaserMachinegun, LunarFlareBook }),
        };

        static P4ACfg P4AArena(P4ACfg c) { c.FocusArena = true; return c; }
        static P4ACfg P4AParts(P4ACfg c, int n) { c.UntilParts = n; return c; }

        IEnumerator P4AKillRun(HeroController hero, IEnumerable<string> keys)
        {
            yield return P4ASetup();
            var report = new List<string>();
            foreach (var key in keys)
            {
                var c = System.Array.Find(P4AKills, k => k.Key == key);
                if (c == null) { report.Add(key + "：没有设置"); continue; }
                bool ok = false;
                yield return P4AEnter(c, r => ok = r);
                if (!ok) { report.Add(key + "：进不去"); yield return P4ALeave(); continue; }
                yield return P4AFight(c, P4AClipName("kill", key), 50f, 1, r => report.Add(key + "：" + r));
                yield return P4ALeave();
            }
            P4AEnd();
            foreach (var r in report) Plugin.Log.LogInfo("[AutoTest] 录制A 结果 " + r);
        }

        IEnumerator P4_A_kills(HeroController hero)
        {
            string keys = System.Environment.GetEnvironmentVariable("P4A_KEYS");
            yield return P4AKillRun(hero, keys != null ? keys.Split('.') : new[] { "FalseKnight", "BigFly", "MageLord", "Hornet", "MantisLord", "InfectedKnight" });
        }

        // ------------------------------------------------------------------ the finale

        /// <summary>A finale take: the four classes in turn on one of the last bosses, the loadout changed mid-fight.</summary>
        sealed class P4AFinaleCfg
        {
            public string Clip, Key, Room;
            public int Level;
            public P4ACfg Cfg;
            public P4ASeg[] Segs;
        }

        static readonly P4AFinaleCfg[] P4AFinales =
        {
            new P4AFinaleCfg { Clip = "finale_AbsoluteRadiance", Key = "FinalBoss", Room = "GG_Radiance", Level = 0,
                Cfg = P4AC("FinalBoss", "无上辐光", 2, new[] { LastPrism }, zoom: 1.2f, dist: 7f),
                Segs = new[] { P4AS(2, 6f, new[] { LastPrism }), P4AS(0, 6f, new[] { Zenith, StarWrath }), P4AS(1, 5f, new[] { CelebrationMk2 }), P4AS(3, 5f, new[] { Kaleidoscope }, new[] { StardustDragonStaff }) } },
            new P4AFinaleCfg { Clip = "finale_NightmareGrimm", Key = "NightmareGrimm",
                Cfg = P4AC("NightmareGrimm", "梦魇之王格林", 0, new[] { Zenith }, zoom: 1.3f, dist: 6.5f),
                Segs = new[] { P4AS(0, 6f, new[] { Zenith }), P4AS(1, 6f, new[] { SDMG, Phantasm }), P4AS(2, 5f, new[] { NebulaBlaze }), P4AS(3, 5f, new[] { MoonLordWhip }, new[] { Terraprisma }) } },
            new P4AFinaleCfg { Clip = "finale_PureVessel", Key = "HollowKnightPrime",
                Cfg = P4AC("HollowKnightPrime", "纯粹容器", 1, new[] { Phantasm }, zoom: 1.35f, dist: 6.5f),
                Segs = new[] { P4AS(1, 6f, new[] { Phantasm }), P4AS(0, 6f, new[] { Meowmere, DayBreak }), P4AS(3, 5f, new[] { Kaleidoscope }, new[] { StardustDragonStaff }), P4AS(2, 5f, new[] { LastPrism }) } },
            new P4AFinaleCfg { Clip = "finale_Radiance", Key = "FinalBoss",
                Cfg = P4AC("FinalBoss", "辐光", 3, new[] { Kaleidoscope }, zoom: 1.2f, dist: 7f),
                Segs = new[] { P4AS(3, 6f, new[] { Kaleidoscope }, new[] { Terraprisma, StardustDragonStaff }), P4AS(2, 6f, new[] { LunarFlareBook }), P4AS(0, 5f, new[] { Zenith }), P4AS(1, 5f, new[] { CelebrationMk2 }) } },
            new P4AFinaleCfg { Clip = "finale_SistersOfBattle", Key = "MantisLord", Room = "GG_Mantis_Lords_V", Level = 0,
                Cfg = P4AParts(P4AC("MantisLord", "战斗姐妹", 0, new[] { StarWrath }, zoom: 1.35f, dist: 6f), 3),
                Segs = new[] { P4AS(0, 6f, new[] { StarWrath, Terrarian }), P4AS(2, 5f, new[] { RazorbladeTyphoon }), P4AS(1, 5f, new[] { Xenopopper }), P4AS(3, 5f, new[] { MoonLordWhip }, new[] { RavenStaff }) } },
        };

        IEnumerator P4AFinaleRun(HeroController hero, IEnumerable<string> clips)
        {
            yield return P4ASetup();
            var report = new List<string>();
            foreach (var name in clips)
            {
                var f = System.Array.Find(P4AFinales, x => x.Clip == name);
                if (f == null) { report.Add(name + "：没有设置"); continue; }
                bool ok = false;
                if (f.Room != null) yield return P4AGodhome(f.Room, f.Level, r => ok = r);
                else yield return P4AEnter(f.Cfg, r => ok = r);
                if (!ok) { report.Add(name + "：进不去"); yield return P4ALeave(); continue; }
                yield return P4AFight(f.Cfg, f.Clip, 0f, 2, r => report.Add(name + "：" + r), f.Segs);
                yield return P4ALeave();
            }
            P4AEnd();
            foreach (var r in report) Plugin.Log.LogInfo("[AutoTest] 录制A 结果 " + r);
        }

        IEnumerator P4_A_finale(HeroController hero)
        {
            string keys = System.Environment.GetEnvironmentVariable("P4A_KEYS");
            var all = new List<string>();
            foreach (var f in P4AFinales) all.Add(f.Clip);
            yield return P4AFinaleRun(hero, keys != null ? keys.Split('.') : all.ToArray());
        }

        /// <summary>The second round: two kill clips again, the Moss Charger longer, the Hall of Gods and the Colosseum.</summary>
        string p4aColoSuffix = "";
        IEnumerator P4_A_colo2(HeroController hero) { p4aColoSuffix = "2"; yield return P4_A_colosseum(hero); }

        IEnumerator P4_A_extra2(HeroController hero)
        {
            yield return P4AFinaleRun(hero, new[] { "finale_SistersOfBattle" });
            yield return P4_A_colosseum(hero);
            yield return P4_A_hall(hero);
        }

        IEnumerator P4_A_extra(HeroController hero)
        {
            yield return P4AKillRun(hero, new[] { "InfectedKnight", "Hornet" });
            yield return P4ABossRun(hero, new[] { "MegaMossCharger" });
            yield return P4_A_colosseum(hero);
            yield return P4_A_hall(hero);
        }

        // ------------------------------------------------------------------ the Hall of Gods

        /// <summary>The Hall of Gods walked through, its statues lit: in from the Atrium's side, on along the hall.</summary>
        IEnumerator P4_A_hall(HeroController hero)
        {
            yield return P4ASetup();
            int lit = P4AStatuesLit();
            var inv = mod.Inventory;
            if (inv.CurrentLoadout != 0) mod.TrySwitchLoadout(0);
            yield return EnterRoom("GG_Workshop");
            hero = HeroController.instance;
            var gm = GameManager.instance;
            Plugin.Log.LogInfo($"[AutoTest] 录制A 众神堂：点亮雕像 {lit} 座，在 {gm.sceneName} {(Vector2)hero.transform.position:F1}");
            if (gm.sceneName != "GG_Workshop") { P4AEnd(); yield break; }
            // A walk, not a sprint: the boots off for it.
            int boots = System.Array.FindIndex(inv.Accessories, a => !a.IsEmpty && a.Type == TerrasparkBoots);
            var keep = boots >= 0 ? inv.Accessories[boots] : default;
            if (boots >= 0) { inv.Accessories[boots] = default; inv.RefreshEffective(); }
            inv.Selected = 0;
            Player.TerraCamera.Zoom = 1.6f;
            var col = hero.GetComponent<BoxCollider2D>();
            var rb = hero.GetComponent<Rigidbody2D>();
            const int terrain = 1 << ChunkColliders.TerrainLayer;
            yield return new WaitForSeconds(1f);
            P4AClearChat();
            aimInit = false;
            StartRec("godhome_hall");
            float t0 = Time.time;
            Vector2 p0 = col.bounds.center;
            float nextHop = 3.5f;
            bool light0 = World.LightMap.Enabled;
            World.LightMap.Enabled = false;
            while (Time.time - t0 < 18f && gm.sceneName == "GG_Workshop")
            {
                P4AKeepAlive();
                float t = Time.time - t0;
                Vector2 c = col.bounds.center;
                SmoothAim(c + new Vector2(5f, 1.5f + Mathf.Sin(t * 0.8f)), 4f);
                // Walk a little, stop a moment as at a statue, on again.
                Player.TerraMovement.DevMove = (t % 2.6f) < 1.5f ? 1 : 0;
                // A wall ahead or the hop's time: up on the wings a moment.
                bool wall = Physics2D.Raycast(c, Vector2.right, 1.5f, terrain).collider != null;
                if ((t > nextHop || wall) && hero.cState.onGround)
                {
                    Player.TerraMovement.DevGroundJump(hero);
                    nextHop = t + 4.5f;
                }
                Player.TerraMovement.DevJump = wall || t < nextHop - 3.6f;
                yield return null;
            }
            Player.TerraMovement.DevMove = 0;
            Player.TerraMovement.DevJump = false;
            yield return new WaitForSeconds(0.6f);
            StopRec();
            World.LightMap.Enabled = light0;
            mod.DevAim = null;
            if (boots >= 0) { inv.Accessories[boots] = keep; inv.RefreshEffective(); }
            Plugin.Log.LogInfo($"[AutoTest] 录制A 众神堂：走了 {(Vector2)col.bounds.center - p0:F1}，现在 {gm.sceneName}");
            P4AEnd();
        }
                // ------------------------------------------------------------------ the Colosseum

        /// <summary>The Colosseum's summon used in Dirtmouth: in its hall (one spent), as a player gets there.</summary>
        IEnumerator P4AColosseumTicket(System.Action<bool> done)
        {
            var gm = GameManager.instance;
            var inv = mod.Inventory;
            if (gm.sceneName != "Town") yield return SealTravel("Town", "left1");
            for (float t = Time.realtimeSinceStartup + 15f; !GameHooks.PlayerHasControl() && Time.realtimeSinceStartup < t;) yield return null;
            var keep = inv.Slots[1];
            inv.Slots[1] = ItemStack.Of(ColosseumTicket.Type, 3);
            inv.Selected = 1;
            inv.Override = -1;
            yield return null;
            mod.DevUseSelected(HeroController.instance.transform.position);
            for (float t = Time.realtimeSinceStartup + 30f; Time.realtimeSinceStartup < t && (gm.IsInSceneTransition || gm.sceneName != ColosseumTicket.Scene || !GameHooks.PlayerHasControl());) yield return null;
            inv.Slots[1] = keep;
            Plugin.Log.LogInfo($"[AutoTest] 录制A 斗兽场召唤物：到了 {gm.sceneName}，{ColosseumTicket.DevLast}");
            done(gm.sceneName == ColosseumTicket.Scene);
        }

            /// <summary>
        /// The Colosseum: its summon used in Dirtmouth (the hall, one spent), then into the Trial of the Conqueror's arena as
        /// its board's gate lets the player in, the trial started (the hall's walk-in and the arena's centre), its waves of
        /// fools fought; then on to its last waves, the Oblobbles'.
        /// </summary>
        IEnumerator P4_A_colosseum(HeroController hero)
        {
            yield return P4ASetup();
            var report = new List<string>();
            bool ok = false;
            yield return P4AColosseumTicket(r => ok = r);
            if (!ok) { Plugin.Log.LogWarning("[AutoTest] 录制A 斗兽场召唤物没到"); P4AEnd(); yield break; }
            var pd = PlayerData.instance;
            pd.SetBool("colosseumSilverOpened", true);
            pd.SetBool("colosseumSilverCompleted", false);
            const string room = "Room_Colosseum_Silver";
            var gm = GameManager.instance;
            gm.BeginSceneTransition(new GameManager.SceneLoadInfo { SceneName = room, EntryGateName = "left1", HeroLeaveDirection = GlobalEnums.GatePosition.unknown, EntryDelay = 0f, WaitForSceneTransitionCameraFade = true });
            for (float t = Time.realtimeSinceStartup + 25f; Time.realtimeSinceStartup < t && (gm.IsInSceneTransition || gm.sceneName != room || !GameHooks.PlayerHasControl());) { P4AKeepAlive(); yield return null; }
            yield return new WaitForSeconds(0.5f);
            PlayMakerFSM manager = null, battle = null;
            foreach (var f in Object.FindObjectsByType<PlayMakerFSM>(FindObjectsSortMode.None))
            {
                if (f.gameObject.scene.name != room || f.gameObject.name != "Colosseum Manager") continue;
                if (f.FsmName == "Manager") manager = f;
                if (f.FsmName == "Battle Control") battle = f;
            }
            if (manager == null || battle == null) { Plugin.Log.LogWarning("[AutoTest] 录制A 斗兽场：找不到管理器"); P4AEnd(); yield break; }
            // Walked in to the arena's middle (where its manager stands), the floor under it.
            {
                const int terrain = 1 << ChunkColliders.TerrainLayer;
                hero = HeroController.instance;
                var mid = (Vector2)manager.transform.position;
                var down = Physics2D.Raycast(mid + Vector2.up * 6f, Vector2.down, 40f, terrain);
                Plugin.Log.LogInfo($"[AutoTest] 录制A 斗兽场：管理器在 {mid:F1}，地面 {(down.collider != null ? down.point.ToString("F1") : "无")}，主角 {(Vector2)hero.transform.position:F1}");
                if (down.collider != null)
                {
                    hero.transform.position = new Vector3(mid.x, down.point.y + 1.4f, hero.transform.position.z);
                    hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
                    yield return new WaitForSeconds(0.6f);
                }
            }
            manager.SendEvent("HERO ENTER");
            yield return new WaitForSeconds(0.5f);
            manager.SendEvent("HERO CENTRE");
            for (float t = Time.realtimeSinceStartup + 20f; Time.realtimeSinceStartup < t && battle.ActiveStateName == "Idle";) { P4AKeepAlive(); yield return null; }
            Plugin.Log.LogInfo($"[AutoTest] 录制A 斗兽场开赛：管理器 {manager.ActiveStateName}，战斗 {battle.ActiveStateName}");
            var waves = P4AC("Colosseum", "愚人斗兽场", 2, new[] { NebulaBlaze, LunarFlareBook }, zoom: 1.4f, dist: 6f);
            waves.AnyEnemy = true;
            waves.NoLight = true;
            waves.Seconds = 10f;
            yield return P4AFight(waves, "boss_Colosseum" + p4aColoSuffix, 10f, 0, r => report.Add("Colosseum：" + r));
            // On to its end: the Oblobbles' waves.
            for (float t = Time.realtimeSinceStartup + 3f; Time.realtimeSinceStartup < t;) { P4AKeepAlive(); yield return null; }
            foreach (var hm in Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None))
                if (hm.gameObject.scene.name == room && hm.gameObject.activeInHierarchy && !hm.isDead && hm.hp > 0)
                {
                    var hc = hm.GetComponent<Collider2D>();
                    if (hc != null && hc.enabled) mod.Melee.HitWith(hc, 99999, 0f, 0f, 1f, hc.bounds.center);
                }
            battle.SetState("Arena Final Obble");
            Plugin.Log.LogInfo("[AutoTest] 录制A 斗兽场：跳到奥波波的波次");
            var obble = P4AC("Oblobble", "奥波波", 0, new[] { Zenith, StarWrath }, zoom: 1.4f, dist: 6.5f);
            obble.AnyEnemy = true;
            obble.NoLight = true;
            yield return P4AFight(obble, "boss_Oblobble" + p4aColoSuffix, 9f, 0, r => report.Add("Oblobble：" + r));
            P4AEnd();
            foreach (var r in report) Plugin.Log.LogInfo("[AutoTest] 录制A 结果 " + r);
        }
    }
}
