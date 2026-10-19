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
    /// Dev, the 4.0 trailer's bosses (recorder A): every boss a seal reaches, each come to as a player comes to it (the seal
    /// held and used in Dirtmouth, straight into its arena, the fight started by BossSummonStart), then 6-10 s of the fight
    /// recorded: the hero on wings around the boss, the four class loadouts in turn, two or three of the class's weapons.
    /// The boss's health is held from falling faster than a share a second while recording, so an early boss isn't gone
    /// in a second. Suites p4_A_&lt;batch&gt;_zh; p4_A_keys_zh takes its bosses from the environment (P4A_KEYS="Key1.Key2").
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        sealed class P4ACfg
        {
            public string Key, Zh;
            public int Loadout;
            public ItemType[] Weapons, Summons;
            public float Seconds = 9f, Zoom = 1.5f, Dist = 6.5f;
            /// <summary>The arena's waves before its boss (the Traitor Lord's mantises): struck down out of the clip.</summary>
            public bool ClearAdds;
            /// <summary>Every enemy of the room fought, none held (the Colosseum's waves).</summary>
            public bool AnyEnemy;
            /// <summary>A kill's close-up on the arena's middle, not the bodies (the Mantis Lords bow on their thrones).</summary>
            public bool FocusArena;
            /// <summary>A fight whose first phase is struck down before the clip, until this many bodies are up at once (the Sisters of Battle).</summary>
            public int UntilParts;
            /// <summary>Terraria's light map off for the clip (a room the mod lights as dark: the Colosseum's arena, the Hall of Gods).</summary>
            public bool NoLight;
        }

        static P4ACfg P4AWithAdds(P4ACfg c) { c.ClearAdds = true; return c; }

        static P4ACfg P4AC(string key, string zh, int loadout, ItemType[] weapons, ItemType[] summons = null, float seconds = 9f, float zoom = 1.5f, float dist = 6.5f) =>
            new P4ACfg { Key = key, Zh = zh, Loadout = loadout, Weapons = weapons, Summons = summons, Seconds = seconds, Zoom = zoom, Dist = dist };

        /// <summary>The bosses in the seals' order; F1 melee, F2 ranged, F3 magic, F4 summon in turn.</summary>
        static readonly P4ACfg[] P4ABosses =
        {
            P4AC("FalseKnight", "假骑士", 0, new[] { Meowmere, StarWrath }),
            P4AC("BigFly", "格鲁兹之母", 1, new[] { Phantasm, CelebrationMk2 }),
            P4AC("Mawlek", "躁郁的毛里克", 2, new[] { NebulaBlaze, RazorbladeTyphoon }),
            P4AC("Hornet", "大黄蜂", 3, new[] { Kaleidoscope }, new[] { StardustDragonStaff, Terraprisma }, dist: 4.5f),
            P4AC("MegaMossCharger", "大型苔藓冲锋者", 0, new[] { Terrarian, SolarEruption }, seconds: 15f),
            P4AC("GhostAladar", "戈布", 1, new[] { VortexBeater, ElectrosphereLauncher }),
            P4AC("GhostNoEyes", "无眼", 2, new[] { LastPrism, NebulaArcanum }),
            P4AC("DungDefender", "粪虫防御者", 3, new[] { MoonLordWhip }, new[] { StardustCellStaff, RavenStaff }, dist: 4.5f),
            P4AWithAdds(P4AC("TraitorLord", "叛徒领主", 0, new[] { Zenith, DayBreak })),
            P4AC("MantisLord", "螳螂领主", 1, new[] { SDMG, Tsunami }),
            P4AC("MageLord", "灵魂大师", 2, new[] { LunarFlareBook, NebulaBlaze }),
            P4AC("MegaJellyfish", "乌姆", 3, new[] { Kaleidoscope }, new[] { XenoStaff, Terraprisma }, zoom: 1.3f, dist: 5f),
            P4AC("FlukeMother", "吸虫之母", 0, new[] { DayBreak, InfluxWaver }),
            P4AC("MegaBeamMiner", "水晶守卫", 1, new[] { ChainGun, SnowmanCannon }),
            P4AC("GhostHu", "胡长老", 2, new[] { ChargedBlasterCannon, BetsysWrath }),
            P4AC("GhostMarmu", "马尔穆", 3, new[] { MoonLordWhip }, new[] { TempestStaff, DeadlySphereStaff }, dist: 4.5f),
            P4AC("BlackKnight", "守望者骑士", 0, new[] { StarWrath, TerraBlade }, zoom: 1.35f),
            P4AC("HiveKnight", "蜂巢骑士", 1, new[] { Xenopopper, CelebrationMk2 }),
            P4AC("JarCollector", "收藏家", 2, new[] { LaserMachinegun, SparkleGuitar }),
            P4AC("MimicSpider", "诺斯克", 3, new[] { Kaleidoscope }, new[] { StardustDragonStaff, PygmyStaff }, dist: 4.5f),
            P4AC("GhostGalien", "加利安", 0, new[] { PiercingStarlight, Meowmere }),
            P4AC("GhostXero", "泽若", 1, new[] { Phantasm, Eventide }),
            P4AC("Hornet@4", "大黄蜂（王国边缘）", 2, new[] { NebulaBlaze, LastPrism }),
            P4AC("GhostMarkoth", "马科斯", 3, new[] { DarkHarvest }, new[] { Terraprisma, BladeStaff }, dist: 4.5f),
            P4AC("InfectedKnight", "破碎容器", 0, new[] { Zenith, SolarEruption }),
            P4AC("Grimm", "格林", 1, new[] { SDMG, ElectrosphereLauncher }, zoom: 1.35f),
            P4AC("HollowKnight", "空洞骑士", 2, new[] { LunarFlareBook, RazorbladeTyphoon }),
            P4AC("FalseKnight@5", "失败的冠军", 3, new[] { MoonLordWhip }, new[] { StardustDragonStaff, LunarPortalStaff }, dist: 4.5f),
            P4AC("MageLord@5", "灵魂暴君", 0, new[] { Terrarian, DayBreak }),
            P4AC("InfectedKnight@5", "失落近亲", 1, new[] { VortexBeater, Tsunami }),
            P4AC("WhiteDefender", "白色守卫者", 2, new[] { NebulaArcanum, ChargedBlasterCannon }),
            P4AC("GreyPrince", "灰色王子左特", 3, new[] { Kaleidoscope }, new[] { XenoStaff, DesertTigerStaff }, dist: 4.5f),
            P4AC("NightmareGrimm", "梦魇之王格林", 0, new[] { Zenith, StarWrath }, zoom: 1.35f),
            P4AC("FinalBoss", "辐光", 1, new[] { CelebrationMk2, SDMG }, zoom: 1.25f),
            P4AC("HollowKnightPrime", "纯粹容器", 2, new[] { LastPrism, LunarFlareBook }),
            P4AC("NailBros", "骨钉大师奥罗与马托", 3, new[] { MoonLordWhip }, new[] { Terraprisma, StardustDragonStaff }, dist: 4.5f),
            P4AC("Paintmaster", "画家大师席奥", 0, new[] { Meowmere, InfluxWaver }),
            P4AC("Nailsage", "骨钉贤者斯莱", 1, new[] { Phantasm, ChainGun }),
        };

        static P4ACfg P4ACfgOf(string key)
        {
            foreach (var c in P4ABosses) if (c.Key == key) return c;
            return null;
        }

        float p4aZoom0 = -1f;
        bool p4aReady;

        /// <summary>The showcase kit, every seal usable (each boss beaten once, the last stage, the Dream Nail), the hero kept alive.</summary>
        IEnumerator P4ASetup()
        {
            if (p4aReady) yield break;
            yield return P4Kit();
            var pd = PlayerData.instance;
            pd.hasDreamNail = true;
            pd.hasAcidArmour = true;
            // Every area visited: no area's title card over a boss clip (the bosses' own title cards stay).
            int visited = 0;
            foreach (var f in typeof(PlayerData).GetFields(BindingFlags.Instance | BindingFlags.Public))
                if (f.FieldType == typeof(bool) && f.Name.StartsWith("visited")) { f.SetValue(pd, true); visited++; }
            Plugin.Log.LogInfo($"[AutoTest] 录制A 去过的地区记上 {visited} 个");
            Player.TerraStats.DevImmortal = true;
            mod.DevNoFallDamage = true;
            Shop.DevStage = 5;
            foreach (var s in BossSummons.All) if (Bestiary.KillsOf(s.Key) <= 0) Bestiary.Kills[s.Key] = 1;
            if (p4aZoom0 < 0f) p4aZoom0 = Player.TerraCamera.Zoom;
            // The talk not told in the chat (the bottom-left stays clean); put back at the end.
            if (UI.DialogueBroadcast.Setting != null)
            {
                p4aBroadcast0 = UI.DialogueBroadcast.Setting.Value;
                UI.DialogueBroadcast.Setting.Value = false;
            }
            World.ArenaGuard.Enabled = false;
            p4aReady = true;
        }

        bool p4aBroadcast0 = true;

        /// <summary>The chat emptied (its lines and the talk still waiting to be told), before a clip starts.</summary>
        static void P4AClearChat()
        {
            const BindingFlags any = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;
            (typeof(Net.NetChat).GetField("lines", any)?.GetValue(null) as IList)?.Clear();
            var waiting = typeof(UI.DialogueBroadcast).GetField("waiting", any)?.GetValue(null);
            waiting?.GetType().GetMethod("Clear")?.Invoke(waiting, null);
        }

        void P4AEnd()
        {
            p4aReady = false;
            World.ArenaGuard.Enabled = true;
            if (p4aArrow != null) p4aArrow.enabled = true;
            Player.TerraCamera.Zoom = p4aZoom0 > 0f ? p4aZoom0 : 2f;
            if (UI.DialogueBroadcast.Setting != null) UI.DialogueBroadcast.Setting.Value = p4aBroadcast0;
        }

        /// <summary>The screen faded in (a dream's or Godhome's way in, a world room's), up to a few seconds.</summary>
        static IEnumerator P4AFadedIn(float seconds)
        {
            var fade = GameCameras.instance != null ? GameCameras.instance.cameraFadeFSM : null;
            for (float t = 0f; fade != null && fade.ActiveStateName != "Normal" && t < seconds; t += Time.unscaledDeltaTime) yield return null;
        }

        static Texture2D p4aBlank;
        static float p4aCursorAt;

        /// <summary>
        /// The game's own pointer (drawn into the frame where the real mouse rests over the window) hidden while recording:
        /// the drawn Terraria cursor is the only one.
        /// </summary>
        void P4AHideCursor()
        {
            Cursor.visible = false;
            if (Time.unscaledTime < p4aCursorAt) return;
            p4aCursorAt = Time.unscaledTime + 0.5f;
            if (p4aBlank == null)
            {
                p4aBlank = new Texture2D(16, 16, TextureFormat.RGBA32, false) { name = "P4A Empty Cursor" };
                p4aBlank.SetPixels32(new Color32[16 * 16]);
                p4aBlank.Apply();
            }
            Cursor.SetCursor(p4aBlank, Vector2.zero, CursorMode.ForceSoftware);
        }

        static Renderer p4aArrow;

        /// <summary>
        /// The dialogue box's page arrow (the HUD's DialogueManager/Arrow), left showing in the middle of the screen when a
        /// boss's words (Hornet's) are put down before their end: hidden while no words are up.
        /// </summary>
        static void P4AHideDialogueArrow()
        {
            if (p4aArrow == null)
            {
                var hud = GameCameras.instance != null && GameCameras.instance.hudCamera != null ? GameCameras.instance.hudCamera.transform : null;
                var t = hud != null ? hud.Find("DialogueManager/Arrow") : null;
                p4aArrow = t != null ? t.GetComponent<Renderer>() : null;
                if (p4aArrow == null) return;
            }
            if (p4aArrow.enabled && !UI.HudDialogue.DevOpen) p4aArrow.enabled = false;
        }

        void P4AKeepAlive()
        {
            if (InventoryGui.Open) InventoryGui.Open = false;
            P4AHideCursor();
            P4AHideDialogueArrow();
            PlayerData.instance.isInvincible = true;
            Player.TerraStats.DevImmortal = true;
            mod.Stats.FullHeal();
            mod.Stats.Mana = mod.Stats.ManaMax;
        }

        static string P4AClipName(string prefix, string key) => prefix + "_" + key.Replace("@", "_");

        /// <summary>Every boss of a list: in by its seal, then its clip; on to the next whatever happens.</summary>
        IEnumerator P4ABossRun(HeroController hero, IEnumerable<string> keys, string prefix = "boss", int mode = 0)
        {
            yield return P4ASetup();
            var report = new List<string>();
            foreach (var key in keys)
            {
                var c = P4ACfgOf(key);
                if (c == null) { report.Add(key + "：没有设置"); continue; }
                bool ok = false;
                yield return P4AEnter(c, r => ok = r);
                if (!ok) { report.Add(key + "：进不去"); yield return P4ALeave(); continue; }
                yield return P4AFight(c, P4AClipName(prefix, key), mode == 1 ? 40f : c.Seconds, mode, r => report.Add(key + "：" + r));
                yield return P4ALeave();
            }
            P4AEnd();
            foreach (var r in report) Plugin.Log.LogInfo("[AutoTest] 录制A 结果 " + r);
        }

        IEnumerator P4ALeave()
        {
            Player.TerraMovement.DevMove = 0;
            Player.TerraMovement.DevJump = false;
            mod.DevHold = null;
            mod.DevAim = null;
            UI.TerraCursor.DevAt = null;
            // Not away in the middle of a boss's entrance (it leaves the player held in Dirtmouth).
            for (float ctl = Time.realtimeSinceStartup + 15f; !GameHooks.PlayerHasControl() && Time.realtimeSinceStartup < ctl;) { P4AKeepAlive(); yield return null; }
            yield return SealTravel("Town", "left1");
            for (float clean = Time.realtimeSinceStartup + 10f; (BossArena.Visiting || BossSummons.Pending) && Time.realtimeSinceStartup < clean;) yield return null;
            P4AKeepAlive();
        }

        /// <summary>In Dirtmouth with the boss's loadout on, its seal held and used: in its arena, the fight begun.</summary>
        IEnumerator P4AEnter(P4ACfg c, System.Action<bool> done)
        {
            var gm = GameManager.instance;
            var inv = mod.Inventory;
            var seal = System.Array.Find(BossSummons.All, s => s.Key == c.Key);
            if (seal == null) { done(false); yield break; }
            if (gm.sceneName != "Town") yield return SealTravel("Town", "left1");
            for (float t = Time.realtimeSinceStartup + 15f; !GameHooks.PlayerHasControl() && Time.realtimeSinceStartup < t;) yield return null;
            Minions.Instance?.Clear();
            for (float t = Time.realtimeSinceStartup + 3f; inv.CurrentLoadout != c.Loadout && Time.realtimeSinceStartup < t;)
            {
                mod.DevHold = null;
                if (!mod.TrySwitchLoadout(c.Loadout)) yield return null;
            }
            yield return new WaitForSeconds(0.3f);
            var keep = inv.Slots[1];
            inv.Slots[1] = ItemStack.Of(seal.Type, 3);
            inv.Selected = 1;
            inv.Override = -1;
            yield return null;
            string why = BossSummons.Refusal(seal);
            if (why != null)
            {
                Plugin.Log.LogWarning($"[AutoTest] 录制A {c.Key} 召唤物不能用：{why}");
                inv.Slots[1] = keep;
                done(false);
                yield break;
            }
            int started = BossSummonStart.DevStarted;
            float t0 = Time.realtimeSinceStartup;
            P4AKeepAlive();
            mod.DevUseSelected(HeroController.instance.transform.position);
            for (float there = t0 + 30f; !BossArena.Arrived(c.Key) && Time.realtimeSinceStartup < there;) yield return null;
            yield return new WaitForSecondsRealtime(0.3f);
            inv.Slots[1] = keep;
            if (!BossArena.Arrived(c.Key)) { Plugin.Log.LogWarning($"[AutoTest] 录制A {c.Key} 没到竞技场：{BossSummons.DevLast}"); done(false); yield break; }
            string scene = gm.sceneName;
            bool dreamOrGod = scene.StartsWith("GG_") || scene.StartsWith("Dream_") || scene.StartsWith("Grimm_Nightmare");
            for (float fight = Time.realtimeSinceStartup + 25f; !dreamOrGod && BossSummonStart.DevStarted == started && Time.realtimeSinceStartup < fight
                 && !BossSummonStart.DevLast.StartsWith(c.Key + " 未确认");) { P4AKeepAlive(); yield return null; }
            if (dreamOrGod && !scene.StartsWith("GG_"))
                for (float t = Time.realtimeSinceStartup + 8f; !BossSummonStart.DevLast.StartsWith(c.Key) && Time.realtimeSinceStartup < t;) { P4AKeepAlive(); yield return null; }
            Plugin.Log.LogInfo($"[AutoTest] 录制A {c.Key} 到了 {scene}（{Time.realtimeSinceStartup - t0:F1} 秒）：{BossSummonStart.DevLast}");
            done(true);
        }

        static Vector2 P4ACentre(HealthManager hm)
        {
            var c = hm.GetComponent<Collider2D>();
            return c != null && c.enabled ? (Vector2)c.bounds.center : (Vector2)hm.transform.position;
        }

        sealed class P4ASeg
        {
            public int Loadout;
            public ItemType[] Weapons, Summons;
            public float Seconds;
        }

        static P4ASeg P4AS(int loadout, float seconds, ItemType[] weapons, ItemType[] summons = null) =>
            new P4ASeg { Loadout = loadout, Seconds = seconds, Weapons = weapons, Summons = summons };

        ItemStack[] P4ABagOf(int loadout) => loadout == mod.Inventory.CurrentLoadout ? mod.Inventory.Slots : mod.Inventory.Loadouts[loadout].Bag;

        /// <summary>A weapon brought into a loadout's hotbar if it isn't there (swapped with a slot the clip doesn't use).</summary>
        static void P4AHotbar(ItemStack[] bag, ItemType t, ICollection<ItemType> inUse)
        {
            int at = -1;
            for (int i = 0; i < Inventory.Size; i++) if (!bag[i].IsEmpty && bag[i].Type == t) { at = i; break; }
            if (at < 0 || at < Inventory.HotbarSize) return;
            for (int i = Inventory.HotbarSize - 1; i >= 2; i--)
            {
                if (!bag[i].IsEmpty && inUse.Contains(bag[i].Type)) continue;
                var tmp = bag[i];
                bag[i] = bag[at];
                bag[at] = tmp;
                return;
            }
        }

        /// <summary>
        /// The fight recorded: the hero flies around the boss on wings at a changing distance and height, crossing over it
        /// to its other side every few seconds, the aim eased onto it, the weapons in turn. Its segments (a finale's) change
        /// the loadout as they go, a summoner's staffs used on the way in; a single loadout's minions are called before the
        /// clip. mode 0: a boss clip (the boss's health held from falling faster than 6% a second, never under 40%); 1: a
        /// kill clip (on until it is beaten and its loot down, then gathered); 2: a finale clip (as 0, slower).
        /// </summary>
        IEnumerator P4AFight(P4ACfg c, string clip, float seconds, int mode, System.Action<string> result, P4ASeg[] segs = null)
        {
            string record = c.Key.Split('@')[0];
            var hero = HeroController.instance;
            var inv = mod.Inventory;
            var rb = hero.GetComponent<Rigidbody2D>();
            var col = hero.GetComponent<BoxCollider2D>();
            const int terrain = 1 << ChunkColliders.TerrainLayer;
            string scene = GameManager.instance.sceneName;
            ItemType[] pre = null;
            if (segs == null)
            {
                segs = new[] { P4AS(c.Loadout, seconds, c.Weapons) };
                pre = c.Summons;
            }
            float total = 0f;
            foreach (var sg in segs) total += sg.Seconds;
            if (mode != 1) seconds = total;
            foreach (var sg in segs)
            {
                var used = new List<ItemType>(sg.Weapons);
                if (sg.Summons != null) used.AddRange(sg.Summons);
                if (pre != null && sg == segs[0]) used.AddRange(pre);
                var bag = P4ABagOf(sg.Loadout);
                foreach (var w in used) P4AHotbar(bag, w, used);
            }
            if (inv.CurrentLoadout != segs[0].Loadout)
            {
                mod.DevHold = null;
                for (float t = 0f; t < 2f && !mod.TrySwitchLoadout(segs[0].Loadout); t += Time.deltaTime) yield return null;
            }
            bool anyBoss = false;
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
                    // Godhome's own copy may go by another record: any boss of the room.
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
                    if (d < bd) { bd = d; best = hm; }
                }
                return best;
            }

            // Its health held from the first sight of it (minions called before the clip strike it too).
            var floors = new Dictionary<int, (int Hp, float T)>();
            float rate = mode == 2 ? 0.03f : 0.06f, least = mode == 2 ? 0.3f : 0.4f;
            float recStart = float.MaxValue;
            void Hold()
            {
                if (c.AnyEnemy) return;
                foreach (var hm in Parts())
                {
                    if (hm.isDead || hm.hp <= 0) continue;
                    int id = hm.GetInstanceID();
                    if (!floors.TryGetValue(id, out var f)) floors[id] = f = (hm.hp, Time.time);
                    // A kill clip: after a few seconds its health let go a little faster (never to nothing: a hit ends it).
                    if (mode == 1 && recording) continue;
                    // Not before the clip: until it starts, none of it goes.
                    float allowed = f.Hp * (recording ? Mathf.Max(least, 1f - rate * (Time.time - Mathf.Max(f.T, recStart))) : 1f);
                    if (hm.hp < allowed) hm.hp = Mathf.CeilToInt(allowed);
                }
            }
            // The boss up: there, near, the player free.
            float wait0 = Time.time;
            HealthManager first = null;
            float nextStrike = 0f, enough = -1f;
            while (Time.time - wait0 < (c.ClearAdds || c.UntilParts > 1 ? 45f : 18f))
            {
                P4AKeepAlive();
                if (c.UntilParts > 1)
                {
                    // The first phase out of the way: struck down (out of the clip) until the bodies of the next are up.
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
                // Near, the player free; and in view and open to hits (a boss come down into its fight), or after a while anyway.
                if (first != null && GameHooks.PlayerHasControl() && Vector2.Distance(P4ACentre(first), col.bounds.center) < 24f && Time.time - wait0 > 0.4f
                    && (OnScreen(P4ACentre(first)) && !first.IsInvincible || Time.time - wait0 > 9f)) break;
                yield return null;
            }
            if (first == null)
            {
                result($"{scene}：首领没出现（{BossSummonStart.DevLast}）");
                yield break;
            }
            Plugin.Log.LogInfo($"[AutoTest] 录制A {c.Key} 首领出场：{first.name}（{UI.EnemyHover.NameOf(first)}，记录 {UI.EnemyHover.RecordOf(first)}）hp {first.hp}，等了 {Time.time - wait0:F1}s，{scene}");
            var names = new List<string>();
            foreach (var sg in segs)
            {
                foreach (var w in sg.Weapons) names.Add(ItemDb.Get(w)?.Name ?? w.ToString());
                if (sg.Summons != null) foreach (var w in sg.Summons) names.Add(ItemDb.Get(w)?.Name ?? w.ToString());
            }
            if (pre != null) foreach (var w in pre) names.Add(ItemDb.Get(w)?.Name ?? w.ToString());
            Plugin.Log.LogInfo($"[AutoTest] 录制A {clip} 武器：{string.Join("、", names)}");
            aimInit = false;
            SmoothAim(P4ACentre(first), 99f);
            // A single loadout's minions first, out of the clip.
            if (pre != null)
                foreach (var s in pre)
                {
                    int si = SlotOf(s);
                    if (si < 0) continue;
                    inv.Selected = si;
                    for (int k = 0; k < 2; k++)
                    {
                        var spot = (Vector2)col.bounds.center + new Vector2(k == 0 ? 1.5f : -1.5f, 1.8f);
                        for (float t = 0f; t < 0.15f; t += Time.deltaTime) { SmoothAim(spot, 14f); P4AKeepAlive(); Hold(); yield return null; }
                        for (float t = 0f; t < 1f && (!mod.DevReady || mod.IsUsingItem); t += Time.deltaTime) { Hold(); yield return null; }
                        mod.DevUseSelected(aimNow);
                        yield return new WaitForSeconds(0.12f);
                    }
                }
            Player.TerraCamera.Zoom = mode == 1 ? Mathf.Max(c.Zoom, 1.75f) : c.Zoom;
            int weaponSlot = SlotOf(segs[0].Weapons[0]);
            if (weaponSlot >= 0) inv.Selected = weaponSlot;
            yield return null;
            int kills0 = Bestiary.KillsOf(c.Key);
            for (float t = 0f; t < 3f && GameCameras.instance != null && GameCameras.instance.cameraFadeFSM.ActiveStateName != "Normal"; t += Time.deltaTime) { P4AKeepAlive(); Hold(); yield return null; }
            P4AClearChat();
            UI.HudDialogue.Close();
            bool light0 = World.LightMap.Enabled;
            if (c.NoLight) World.LightMap.Enabled = false;
            StartRec(clip);
            recStart = Time.time;
            float start = Time.time, nextFlip = 3.4f, gone = -1f, lootSeen = -1f, segStart = 0f, switchHold = 0f;
            int itemsAtDeath = 0;
            int side = col.bounds.center.x < P4ACentre(first).x ? -1 : 1, move = 0, frames = 0, onScreen = 0, still = 0, seg = 0;
            var calls = new Queue<ItemType>();
            if (segs[0].Summons != null) foreach (var s in segs[0].Summons) { calls.Enqueue(s); calls.Enqueue(s); }
            Vector2 last = P4ACentre(first), lastHero = col.bounds.center;
            bool beaten = false;
            float noTarget = 0f, nextAssist = 0f, phaseWait = 0f;
            bool phaseCalled = false, camSet = false, cam0 = Player.TerraCamera.Enabled;
            Vector2 camFrom = Vector2.zero, focus = Vector2.zero;
            float zoomFrom = Player.TerraCamera.Zoom, viewFrom = 0f;
            while (Time.time - start < seconds + (beaten ? 25f : 0f))
            {
                P4AKeepAlive();
                float t = Time.time - start;
                var target = Pick();
                Hold();
                if (UI.HudDialogue.DevOpen) UI.HudDialogue.Close();
                noTarget = target == null ? noTarget + Time.deltaTime : 0f;
                // A kill clip after its first seconds: the weapons' blows helped along (each part struck through the game's
                // own hit, an eighth of its life at a time), so every phase, the stun and the head included, goes by in time.
                if (mode == 1 && !beaten && t > 4f && Time.time >= nextAssist)
                {
                    nextAssist = Time.time + 0.35f;
                    string here = GameManager.instance.sceneName;
                    foreach (var hm in Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None))
                    {
                        if (hm.isDead || hm.hp <= 0 || !hm.gameObject.activeInHierarchy || EnemyStats.Defeated(hm)) continue;
                        if (!hm.gameObject.scene.name.StartsWith(here.Split('_')[0])) continue;
                        var info = EnemyStats.Of(hm);
                        if (UI.EnemyHover.RecordOf(hm) != record && !(info != null && info.Boss)) continue;
                        var hc = hm.GetComponent<Collider2D>();
                        if (hc == null || !hc.enabled) continue;
                        int hp0 = floors.TryGetValue(hm.GetInstanceID(), out var f0) ? f0.Hp : hm.hp;
                        int dmg = Mathf.CeilToInt(0.125f * hp0 * (info != null ? info.Scale : 1f) + (info != null ? info.Defense * 0.5f : 0f));
                        mod.Melee.HitWith(hc, Mathf.Max(1, dmg), 0f, 0f, Mathf.Sign(hc.bounds.center.x - col.bounds.center.x), col.bounds.center);
                    }
                }
                bool allWait = false;
                {
                    int live = 0, waiting = 0;
                    foreach (var hm in Parts())
                    {
                        if (hm.isDead || hm.hp <= 0) continue;
                        live++;
                        if (P4AWaitsPhase(hm)) waiting++;
                    }
                    allWait = live > 0 && waiting == live;
                }
                phaseWait = allWait ? phaseWait + Time.deltaTime : 0f;
                if (mode == 1 && !beaten && (noTarget > 4f || phaseWait > 3f) && !phaseCalled)
                {
                    phaseCalled = true;
                    foreach (var hm in Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None))
                    {
                        if (UI.EnemyHover.RecordOf(hm) != record || hm.isDead) continue;
                        foreach (var f in hm.GetComponents<PlayMakerFSM>())
                        {
                            var st = f.Fsm?.ActiveState;
                            if (st == null) continue;
                            foreach (var tr in st.Transitions)
                                if (tr.EventName == "PHASE 2")
                                {
                                    f.SendEvent("PHASE 2");
                                    noTarget = 0f;
                                    Plugin.Log.LogInfo($"[AutoTest] 录制A {c.Key} 第二阶段叫出来：{hm.name}/{f.FsmName}");
                                    break;
                                }
                        }
                    }
                }
                if (mode == 1 && !beaten && (Bestiary.KillsOf(c.Key) > kills0 || noTarget > 12f))
                {
                    beaten = true;
                    gone = Time.time;
                    itemsAtDeath = WorldItems.Count;
                    Plugin.Log.LogInfo($"[AutoTest] 录制A {c.Key} 打倒了：{t:F1}s，图鉴 +{Bestiary.KillsOf(c.Key) - kills0}");
                }
                if (beaten)
                {
                    // The death and the loot: the view eased in on the body (the game's own camera lock frames the whole
                    // arena), then each stack gathered.
                    var bodies = BossesOf(record, false);
                    Vector2 at2 = last;
                    if (bodies.Count > 0)
                    {
                        at2 = Vector2.zero;
                        foreach (var b in bodies) at2 += (Vector2)b.transform.position;
                        at2 /= bodies.Count;
                    }
                    var gcam = GameCameras.instance != null ? GameCameras.instance.mainCamera : null;
                    if (!camSet && gcam != null)
                    {
                        camSet = true;
                        camFrom = gcam.transform.position;
                        focus = at2;
                        if (c.FocusArena) focus = camFrom;
                        zoomFrom = Player.TerraCamera.Zoom;
                        viewFrom = Player.TerraCamera.DevViewHeight;
                        Player.TerraCamera.Enabled = true;
                    }
                    if (!c.FocusArena) focus = Vector2.Lerp(focus, at2, 1f - Mathf.Exp(-2.5f * Time.deltaTime));
                    float u = Mathf.SmoothStep(0f, 1f, (Time.time - gone - 0.4f) / 1.6f);
                    // Kept inside the wide view it came from (the arena's lock), so the close view never shows past the room.
                    float zTo = c.FocusArena ? 1.85f : 2f;
                    float spare = Mathf.Max(0f, viewFrom - viewFrom * zoomFrom / zTo) * 0.5f;
                    var aim = focus + new Vector2(0f, 1.2f);
                    aim.x = Mathf.Clamp(aim.x, camFrom.x - spare * gcam.aspect, camFrom.x + spare * gcam.aspect);
                    aim.y = Mathf.Clamp(aim.y, camFrom.y - spare, camFrom.y + spare);
                    Player.TerraCamera.DevLook = Vector2.Lerp(camFrom, aim, u);
                    if (Time.frameCount % 3 == 0 || u >= 1f) Player.TerraCamera.Zoom = Mathf.Lerp(zoomFrom, zTo, u);
                    mod.DevHold = false;
                    Vector2? drop = null;
                    float bd2 = 20f * 20f;
                    foreach (var dp in WorldItems.Positions())
                    {
                        float d2 = (dp - last).sqrMagnitude;
                        if (d2 < bd2) { bd2 = d2; drop = dp; }
                    }
                    if (WorldItems.Count > itemsAtDeath && lootSeen < 0f) lootSeen = Time.time;
                    Vector2 goal = lootSeen > 0f && Time.time - lootSeen > 1.6f && drop != null ? drop.Value : last + new Vector2(side * 3.5f, 1.5f);
                    SmoothAim(drop ?? last, 5f);
                    Vector2 hc = col.bounds.center;
                    float gx = goal.x - hc.x;
                    if (Mathf.Abs(gx) > 0.6f) move = gx > 0 ? 1 : -1; else if (Mathf.Abs(gx) < 0.25f) move = 0;
                    Player.TerraMovement.DevMove = move;
                    bool up = goal.y > hc.y + 0.8f;
                    if (up && hero.cState.onGround) Player.TerraMovement.DevGroundJump(hero);
                    Player.TerraMovement.DevJump = up;
                    if (Time.time - gone > (lootSeen > 0f ? Mathf.Min(25f, Mathf.Max(7f, lootSeen - gone + 4.5f)) : 25f)) break;
                    frames++;
                    yield return null;
                    continue;
                }
                Vector2 at = target != null ? P4ACentre(target) : last;
                last = at;
                SmoothAim(at, 10f);
                // The segment: its loadout put on (the item let go a moment for it), its minions called.
                if (seg + 1 < segs.Length && t - segStart >= segs[seg].Seconds)
                {
                    seg++;
                    segStart = t;
                    switchHold = segs[seg].Loadout != inv.CurrentLoadout ? 1f : 0f;
                    calls.Clear();
                    if (segs[seg].Summons != null) foreach (var s in segs[seg].Summons) { calls.Enqueue(s); calls.Enqueue(s); }
                }
                var sgNow = segs[seg];
                if (switchHold > 0f)
                {
                    mod.DevHold = false;
                    switchHold -= Time.deltaTime;
                    if (!mod.IsUsingItem && mod.DevReady && mod.TrySwitchLoadout(sgNow.Loadout)) switchHold = 0f;
                }
                else if (calls.Count > 0)
                {
                    int si = SlotOf(calls.Peek());
                    if (si < 0) calls.Dequeue();
                    else
                    {
                        inv.Selected = si;
                        mod.DevHold = false;
                        if (mod.DevReady && !mod.IsUsingItem)
                        {
                            mod.DevUseSelected((Vector2)col.bounds.center + new Vector2(calls.Count % 2 == 0 ? 1.5f : -1.5f, 1.8f));
                            calls.Dequeue();
                        }
                    }
                }
                else
                {
                    float segLen = mode == 1 ? seconds : sgNow.Seconds;
                    int wi = Mathf.Min(sgNow.Weapons.Length - 1, (int)((t - segStart) / Mathf.Max(2.5f, segLen / sgNow.Weapons.Length)));
                    int slotIndex = SlotOf(sgNow.Weapons[wi]);
                    if (slotIndex >= 0) inv.Selected = slotIndex;
                    mod.DevHold = target != null;
                    if (target != null && mod.DevReady && !mod.IsUsingItem) mod.DevUseSelected(aimNow);
                }
                Vector2 hcen = col.bounds.center;
                frames++;
                if (target != null && OnScreen(at)) onScreen++;
                if ((hcen - lastHero).magnitude < 0.02f) still++;
                lastHero = hcen;
                // Crossing over to its other side every few seconds; a wall close behind, at once.
                if (t > nextFlip) { side = -side; nextFlip = t + (side > 0 ? 3.6f : 4.3f); }
                if (Physics2D.Raycast(hcen, Vector2.right * side, 1.4f, terrain).collider != null && Mathf.Sign(hcen.x - at.x) == side) { side = -side; nextFlip = t + 3.6f; }
                float dist = c.Dist + 1.1f * Mathf.Sin(t * 1.6f);
                // Between a kill's phases (the Soul Master's soul): up to where it was.
                if (mode == 1 && target == null) dist = 1.2f;
                float wantX = at.x + side * dist;
                var down = Physics2D.Raycast(hcen, Vector2.down, 40f, terrain);
                var upHit = Physics2D.Raycast(hcen, Vector2.up, 40f, terrain);
                float ground = down.collider != null ? down.point.y : hcen.y - 6f;
                float ceiling = upHit.collider != null ? upHit.point.y : hcen.y + 30f;
                float top = target != null ? P4ATop(target) : at.y + 1.5f;
                float wantY = at.y + 1.4f + 1.6f * Mathf.Sin(t * 1.15f + 0.6f);
                bool crossing = Mathf.Sign(hcen.x - at.x) != side && Mathf.Abs(hcen.x - at.x) < dist - 0.5f;
                if (crossing) wantY = Mathf.Max(wantY, top + 2.2f);
                wantY = Mathf.Clamp(wantY, ground + 1.6f, Mathf.Max(ground + 1.6f, ceiling - 1.8f));
                float dx = wantX - hcen.x;
                if (Mathf.Abs(dx) > 1.0f) move = dx > 0 ? 1 : -1;
                else if (Mathf.Abs(dx) < 0.3f) move = 0;
                Player.TerraMovement.DevMove = move;
                float dy = wantY - hcen.y, vy = rb.linearVelocity.y;
                bool rise = dy > 0.35f || (dy > -0.6f && vy < -1.2f);
                if (rise && hero.cState.onGround) Player.TerraMovement.DevGroundJump(hero);
                Player.TerraMovement.DevJump = rise;
                yield return null;
            }
            Player.TerraMovement.DevMove = 0;
            Player.TerraMovement.DevJump = false;
            mod.DevHold = null;
            StopRec();
            World.LightMap.Enabled = light0;
            Player.TerraCamera.DevLook = null;
            Player.TerraCamera.Enabled = cam0;
            mod.DevAim = null;
            string summary = $"{clip} {Time.time - start:F1}s，{scene}，首领在画面里 {(frames > 0 ? onScreen * 100 / frames : 0)}%，主角不动的帧 {still}/{frames}" +
                             (mode == 1 ? (beaten ? $"，打倒在 {gone - start:F1}s，掉落出现 {(lootSeen > 0f ? (lootSeen - start).ToString("F1") + "s" : "没看到")}" : "，没打倒") : "");
            Plugin.Log.LogInfo("[AutoTest] 录制A " + summary);
            result(summary);
        }

        /// <summary>A boss part waiting on its phase's call ("PHASE 2": the Soul Master's second, kept under the floor until then).</summary>
        static bool P4AWaitsPhase(HealthManager hm)
        {
            foreach (var f in hm.GetComponents<PlayMakerFSM>())
            {
                var st = f.Fsm?.ActiveState;
                if (st == null) continue;
                foreach (var tr in st.Transitions) if (tr.EventName == "PHASE 2") return true;
            }
            return false;
        }

        static float P4ATop(HealthManager hm)
        {
            var c = hm.GetComponent<Collider2D>();
            return c != null && c.enabled ? c.bounds.max.y : hm.transform.position.y + 1.5f;
        }

        static IEnumerable<string> P4AKeysOf(int from, int count)
        {
            for (int i = from; i < from + count && i < P4ABosses.Length; i++) yield return P4ABosses[i].Key;
        }

        // ------------------------------------------------------------------ the suites

        IEnumerator P4_A_keys(HeroController hero)
        {
            string keys = System.Environment.GetEnvironmentVariable("P4A_KEYS") ?? "FalseKnight";
            string mode = System.Environment.GetEnvironmentVariable("P4A_MODE") ?? "boss";
            yield return P4ABossRun(hero, keys.Split('.'), mode == "kill" ? "kill" : "boss", mode == "kill" ? 1 : 0);
        }

        IEnumerator P4_A_b1(HeroController hero) { yield return P4ABossRun(hero, P4AKeysOf(0, 10)); }
        IEnumerator P4_A_b2(HeroController hero) { yield return P4ABossRun(hero, P4AKeysOf(10, 10)); }
        IEnumerator P4_A_b3(HeroController hero) { yield return P4ABossRun(hero, P4AKeysOf(20, 10)); }
        IEnumerator P4_A_b4(HeroController hero) { yield return P4ABossRun(hero, P4AKeysOf(30, 10)); }
    }
}
