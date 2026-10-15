using System.Collections;
using System.Linq;
using HutongGames.PlayMaker;
using TerraKnight.Items;
using TerraKnight.Net;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    internal sealed partial class DevAutoTest
    {
        IEnumerator NetWorldWait(System.Func<bool> condition, string label, float seconds = 30f)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (!condition() && Time.realtimeSinceStartup < until) yield return null;
            NetCheck(condition(), label);
        }

        static int NetWorldCount(Inventory inv, ItemType type) => inv.Slots.Where(s => s.Type == type).Sum(s => s.Count);

        static WorldSync.Record NetWorldChest() => Chests.NetRecords().FirstOrDefault();

        IEnumerator NetWorldHost(string scene, HeroController hero)
        {
            PlayerData.instance.isInvincible = true;
            yield return NetWorldWait(() => WorldSync.Ready, "共享世界主机就绪");
            var region = TerrainSystem.Current.Region;
            var centre = (Vector2)hero.GetComponent<BoxCollider2D>().bounds.center;
            var local = centre - (Vector2)region.Root.position;
            Chests.Clear(); Stations.Clear(); WorldItems.Clear();
            LifeCrystals.NewWorld(87654321);
            // Furniture near both starting players. The dedicated test world need not persist.
            NetCheck(Stations.NetPlace(new WorldSync.Action { Scene = scene, X = local.x + 1.1f, Y = local.y - 0.6f, Kind = (int)StationKind.WorkBench }), "预置工作台");
            NetCheck(Chests.NetPlace(new WorldSync.Action { Scene = scene, X = local.x - 1.1f, Y = local.y - 0.6f }), "预置箱子");
            var chest = NetWorldChest();
            var contents = Chests.NetContents(chest.Key);
            contents[0] = WorldSync.Stack.From(ItemStack.Of(ItemType.Wood, 17));
            Chests.NetContents(chest.Key, contents);
            // A terrain edit made before the second client connects must be in its snapshot.
            int tx = Mathf.Clamp(CellGrid.WorldToCell(centre).x - region.CellOffset.x + 6, 0, region.Grid.Width - 1);
            int ty = Mathf.Clamp(CellGrid.WorldToCell(centre).y - region.CellOffset.y + 6, 0, region.Grid.Height - 1);
            region.Grid.ApplyNetwork(ty * region.Grid.Width + tx, CellKind.Rope, true, 0);
            Plugin.Log.LogInfo("[AutoTest] world: seeded");
            yield return NetWorldWait(() => Other() != null && Other().Visible, "客机进入已预置的世界", 150f);
            yield return WaitChat("world: snapshot ok", 60f);
            var bench = Stations.NetRecords().First(r => r.Kind == (int)StationKind.WorkBench);
            Stations.NetRemove(bench.Key);
            Stations.NetPlace(new WorldSync.Action { Scene = bench.Scene, X = bench.X, Y = bench.Y, Kind = (int)StationKind.IronAnvil });
            NetChat.Say("world: anvil replaced");
            yield return WaitChat("world: anvil seen", 30f);
            Stations.NetRemove(bench.Key);
            Stations.NetPlace(new WorldSync.Action { Scene = bench.Scene, X = bench.X, Y = bench.Y, Kind = (int)StationKind.WorkBench });
            NetChat.Say("world: bench restored");
            yield return WaitChat("world: bench seen", 30f);
            yield return WaitChat("world: chest locked", 30f);
            Chests.DevOpenNear(centre);
            yield return new WaitForSecondsRealtime(0.5f);
            NetCheck(!Chests.IsOpen, "客机持有箱锁时主机不能开箱");
            NetChat.Say("world: host lock checked");
            yield return WaitChat("world: chest taken", 30f);
            NetCheck(Chests.NetContents(chest.Key)[0].Count == 12, "客机取走5木材，主机箱内17变12");
            Chests.DevOpenNear(centre);
            yield return NetWorldWait(() => Chests.IsOpen, "客机释放后主机可开箱");
            Chests.OpenItems[1] = ItemStack.Of(ItemType.StoneBlock, 9);
            NetChat.Say("world: host owns chest");
            yield return WaitChat("world: denied while host owns", 30f);
            Chests.Close(false);
            NetChat.Say("world: host deposited");
            yield return WaitChat("world: deposit seen", 30f);

            // Exercise actual Throw, including sender-only pickup delay, in both directions.
            int wood = NetWorldCount(mod.Inventory, ItemType.Wood);
            WorldItems.Throw(ItemStack.Of(ItemType.StoneBlock, 7), hero);
            NetChat.Say("world: host threw");
            yield return WaitChat("world: client picked", 30f);
            NetChat.Say("world: reverse ready");
            yield return NetWorldWait(() => NetWorldCount(mod.Inventory, ItemType.Wood) >= wood + 3, "主机拾取客机扔出的3木材");
            NetChat.Say("world: host picked");
            yield return WaitChat("world: client built", 30f);
            NetCheck(Stations.NetRecords().Any(r => r.Kind == (int)StationKind.Furnace), "客机放置的熔炉在主机注册");
            NetChat.Say("world: host build seen");
            yield return WaitChat("world: client mined", 30f);
            NetCheck(!Stations.NetRecords().Any(r => r.Kind == (int)StationKind.Furnace), "客机挖走的熔炉在主机移除");

            // Build a deterministic support for a crystal at the edge of the starting area.
            int cx = Mathf.Clamp(CellGrid.WorldToCell(centre).x - region.CellOffset.x + 10, 2, region.Grid.Width - 4);
            int cy = Mathf.Clamp(CellGrid.WorldToCell(centre).y - region.CellOffset.y, 2, region.Grid.Height - 5);
            for (int x = cx - 1; x <= cx + 2; x++)
                for (int y = cy; y <= cy + 3; y++) region.Grid.Set(x, y, y == cy ? CellKind.Wood : CellKind.Empty);
            var bottom = new Vector2((cx + 1f) * CellGrid.CellSize, (cy + 1f) * CellGrid.CellSize);
            LifeCrystals.NetApply(new WorldSync.Record { Key = "l:" + scene, Scene = scene, Placed = true, X = bottom.x, Y = bottom.y });
            NetChat.Say("world: crystal ready");
            yield return WaitChat("world: crystal mined", 30f);
            NetCheck(LifeCrystals.NetRecords().Any(r => r.Key == "l:" + scene && r.Taken), "客机挖取水晶同步到主机");
            NetCheck(WorldItems.NetRecords().Count(r => r.Kind == (int)ItemType.LifeCrystal) <= 1, "水晶最多只有一份共享掉落");
            NetChat.Say("world: crystal confirmed");
            yield return WaitChat("world: client away", 60f);
            NetCheck(!NetScenes.IsLoaded("Crossroads_01"), "房主未加载客机独自进入的场景");
            NetChat.Say("world: remote ready");
            yield return WaitChat("world: away physics", 30f);
            NetCheck(!WorldItems.NetRecords().Any(r => r.Kind == (int)ItemType.DirtBlock && r.Seed == 11), "远处场景的共享物品拾取在房主结算并移除");
            NetChat.Say("world: away confirmed");
            yield return WaitChat("world: client done", 30f);
            var peerBefore = Other()?.Latest ?? default;
            NetCheck(BossArena.Fight("Hornet"), "房主请求进入真实黄蜂战场");
            string bossRoom = BossArena.SceneOf("Hornet");
            yield return NetWorldWait(() => GameManager.instance.sceneName == bossRoom && !GameManager.instance.IsInSceneTransition
                && TerrainSystem.Find(bossRoom) != null, "房主加载真实黄蜂战斗场景", 45f);
            var bossRegion = TerrainSystem.Find(bossRoom);
            hero = HeroController.SilentInstance;
            if (bossRegion?.OwnZone != null)
            {
                var zone = bossRegion.OwnZone.Value;
                var bounds = bossRegion.CellRect;
                var corners = new[] { new Vector2(bounds.xMin + 2, bounds.yMin + 2) * CellGrid.CellSize,
                    new Vector2(bounds.xMax - 2, bounds.yMin + 2) * CellGrid.CellSize,
                    new Vector2(bounds.xMin + 2, bounds.yMax - 2) * CellGrid.CellSize };
                var outside = corners.FirstOrDefault(p => !zone.Contains(p));
                var ins = new System.Collections.Generic.List<string>();
                var loaded = new System.Collections.Generic.List<string>();
                if (outside != Vector2.zero)
                {
                    hero.transform.position = new Vector3(outside.x, outside.y, hero.transform.position.z);
                    NetScenes.Collect(ins, loaded);
                    NetCheck(!ins.Contains(bossRoom) && loaded.Contains(bossRoom), "战场外等待不占用Boss战斗控制权");
                    var benchObject = new GameObject("NetWorld ordinary RestBench");
                    UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(benchObject, NetScenes.FindScene(bossRoom));
                    var benchFsm = benchObject.AddComponent<PlayMakerFSM>();
                    bool hadOwner = NetSession.Owners.TryGetValue(bossRoom, out int previousOwner);
                    NetSession.Owners[bossRoom] = Other().Slot;
                    NetCheck(BattleIsolation.AllowsTarget(benchFsm.Fsm, hero.gameObject), "Boss隔离不阻止同房间战场外的普通长椅位置动作");
                    if (hadOwner) NetSession.Owners[bossRoom] = previousOwner;
                    else NetSession.Owners.Remove(bossRoom);
                    Object.Destroy(benchObject);
                }
                hero.transform.position = new Vector3(zone.center.x, zone.center.y, hero.transform.position.z);
                NetScenes.Collect(ins, loaded);
                NetCheck(ins.Contains(bossRoom), "实际进入战场才报告参与Boss战斗");
            }
            yield return new WaitForSecondsRealtime(1f);
            var peerAfter = Other()?.Latest ?? default;
            NetCheck(peerAfter.Scene == peerBefore.Scene && Vector2.Distance(peerAfter.BoxCenter, peerBefore.BoxCenter) < 1.5f,
                "房主进入Boss战场未改变远处队友的场景和位置");
            NetChat.Say("world: host done");
            yield return new WaitForSecondsRealtime(2f);
        }

        IEnumerator NetWorldClient(string scene, HeroController hero)
        {
            PlayerData.instance.isInvincible = true;
            yield return NetWorldWait(() => WorldSync.Ready && WorldSync.DevSnapshots > 0, "客机安装完整世界快照", 60f);
            yield return new WaitForSecondsRealtime(1f);
            NetCheck(Stations.Count == 1 && Chests.Count == 1, "迟加入可见已有工作台和箱子");
            NetCheck(Stations.Near("工作台", hero), "同步工作台参与附近合成判定");
            NetCheck(LifeCrystals.Seed == 87654321 && WorldSync.SaveSeed != 87654321, "共享水晶种子且保留个人世界存档种子");
            NetCheck(WorldEdits.All.Any(s => s.Value.Any(c => { WorldEdits.Unpack(c.Value, out var k, out bool d); return k == CellKind.Rope && d; })), "入场补发已放置的绳子与挖掘背景标志");
            var centre = (Vector2)hero.GetComponent<BoxCollider2D>().bounds.center;
            var chest = NetWorldChest();
            NetCheck(chest != null && chest.Slots[0].Count == 17, "迟加入箱内内容17木材");
            NetWorldShot(scene, "initial");
            NetChat.Say("world: snapshot ok");
            yield return WaitChat("world: anvil replaced", 30f);
            yield return NetWorldWait(() => Stations.Near("砧", hero), "同一位置快速更换家具后，客机更新种类和合成条件");
            NetChat.Say("world: anvil seen");
            yield return WaitChat("world: bench restored", 30f);
            yield return NetWorldWait(() => Stations.Near("工作台", hero), "同一位置恢复工作台后，客机恢复合成条件");
            NetChat.Say("world: bench seen");
            Chests.DevOpenNear(centre);
            yield return NetWorldWait(() => Chests.IsOpen, "客机获得箱子独占访问");
            NetChat.Say("world: chest locked");
            yield return WaitChat("world: host lock checked", 30f);
            if (Chests.IsOpen) { Chests.OpenItems[0].Count -= 5; mod.Inventory.Pickup(ItemType.Wood, 5); }
            Chests.Close(false);
            NetChat.Say("world: chest taken");
            yield return WaitChat("world: host owns chest", 30f);
            Chests.DevOpenNear(centre);
            yield return new WaitForSecondsRealtime(0.5f);
            NetCheck(!Chests.IsOpen, "主机持箱锁时客机不能开箱");
            NetCheck(Chests.ForCrafting(hero).Count == 0, "附近合成不能绕过箱锁");
            NetChat.Say("world: denied while host owns");
            yield return WaitChat("world: host deposited", 30f);
            yield return NetWorldWait(() => Chests.NetContents(chest.Key)[1].Count == 9, "主机存入9石块同步到客机");
            NetChat.Say("world: deposit seen");

            int stones = NetWorldCount(mod.Inventory, ItemType.StoneBlock);
            yield return WaitChat("world: host threw", 30f);
            yield return NetWorldWait(() => NetWorldCount(mod.Inventory, ItemType.StoneBlock) >= stones + 7, "客机拾取主机扔出的7石块");
            NetChat.Say("world: client picked");
            yield return WaitChat("world: reverse ready", 30f);
            WorldItems.Throw(ItemStack.Of(ItemType.Wood, 3), hero);
            yield return WaitChat("world: host picked", 30f);

            // A request from a client must change the authority and then be echoed back.
            var region = TerrainSystem.Current.Region;
            var at = centre - (Vector2)region.Root.position + new Vector2(3f, 0f);
            WorldSync.Submit(new WorldSync.Action { Op = "station", Scene = scene, X = at.x, Y = at.y, Kind = (int)StationKind.Furnace });
            yield return NetWorldWait(() => Stations.NetRecords().Any(r => r.Kind == (int)StationKind.Furnace), "客机放置熔炉经主机确认后可见");
            NetChat.Say("world: client built");
            yield return WaitChat("world: host build seen", 30f);
            var furnace = Stations.NetRecords().FirstOrDefault(r => r.Kind == (int)StationKind.Furnace);
            if (furnace != null) WorldSync.Submit(new WorldSync.Action { Op = "mineStation", Key = furnace.Key, Power = 100 });
            yield return NetWorldWait(() => !Stations.NetRecords().Any(r => r.Kind == (int)StationKind.Furnace), "客机挖除家具经主机确认后消失");
            NetChat.Say("world: client mined");
            yield return WaitChat("world: crystal ready", 30f);
            yield return NetWorldWait(() => LifeCrystals.DevBox(scene).HasValue, "客机看到房主定位的生命水晶");
            var box = LifeCrystals.DevBox(scene);
            if (box.HasValue)
            {
                hero.transform.position += (Vector3)(box.Value.center + Vector2.left - (Vector2)hero.GetComponent<BoxCollider2D>().bounds.center);
                yield return new WaitForSecondsRealtime(0.5f);
                NetCheck(LifeCrystals.Lying().Any(), "共享水晶有实际可见渲染对象");
                for (int i = 0; i < 12 && LifeCrystals.DevBox(scene).HasValue; i++)
                {
                    bool hit = LifeCrystals.TryMine(ItemDb.Get(ItemType.CopperPickaxe), box.Value.center, hero);
                    Plugin.Log.LogInfo($"[联机测试] 客机挖水晶 {i} hit={hit} hero={hero.GetComponent<BoxCollider2D>().bounds} box={box.Value}");
                    yield return new WaitForSecondsRealtime(0.15f);
                }
            }
            yield return NetWorldWait(() => LifeCrystals.NetRecords().Any(r => r.Key == "l:" + scene && r.Taken), "客机水晶挖取状态同步消失");
            NetChat.Say("world: crystal mined");
            yield return WaitChat("world: crystal confirmed", 30f);
            NetWorldShot(scene, "final");

            // Test the guard against a boss scene FSM that addresses this local hero from afar.
            var remoteScene = UnityEngine.SceneManagement.SceneManager.CreateScene("GG_Hornet_1");
            var go = new GameObject("NetWorld foreign boss controller");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, remoteScene);
            var fsm = go.AddComponent<PlayMakerFSM>();
            NetCheck(!BattleIsolation.Allows(fsm.Fsm), "未参与玩家拒绝远端Boss状态机控制");
            var teleport = new HutongGames.PlayMaker.Actions.SetPosition
            {
                gameObject = new FsmOwnerDefault { OwnerOption = OwnerDefaultOption.SpecifyGameObject, GameObject = new FsmGameObject { Value = hero.gameObject } },
                vector = new FsmVector3 { Value = hero.transform.position + Vector3.right * 100f },
                x = new FsmFloat { UseVariable = true }, y = new FsmFloat { UseVariable = true }, z = new FsmFloat { UseVariable = true },
                space = Space.World,
            };
            teleport.Init(fsm.Fsm.States[0]);
            var beforeTeleport = hero.transform.position;
            teleport.OnEnter();
            NetCheck(hero.transform.position == beforeTeleport, "实际SetPosition状态机动作未传送未参与的本地主角");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, NetScenes.FindScene(scene));
            teleport.vector.Value = beforeTeleport + Vector3.right * 0.1f;
            teleport.OnEnter();
            NetCheck(Vector3.Distance(hero.transform.position, beforeTeleport + Vector3.right * 0.1f) < 0.01f, "本地场景的正常位置动作仍可执行");
            hero.transform.position = beforeTeleport;
            Object.Destroy(go);
            UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync(remoteScene);

            // The host does not need to load every room for client-owned item physics to advance.
            var gm = GameManager.instance;
            gm.BeginSceneTransition(new GameManager.SceneLoadInfo { SceneName = "Crossroads_01", EntryGateName = "left1",
                HeroLeaveDirection = GlobalEnums.GatePosition.unknown, EntryDelay = 0f, WaitForSceneTransitionCameraFade = true });
            yield return NetWorldWait(() => gm.sceneName == "Crossroads_01" && !gm.IsInSceneTransition && GameHooks.PlayerHasControl(), "客机独自进入远处场景", 35f);
            hero = HeroController.SilentInstance;
            yield return NetWorldWait(() => NetSession.Owners.TryGetValue("Crossroads_01", out int owner) && owner == NetSession.MySlot, "客机负责独自所在场景的模拟");
            NetChat.Say("world: client away");
            yield return WaitChat("world: remote ready", 30f);
            var awayRegion = TerrainSystem.Current.Region;
            var spawn = (Vector2)hero.GetComponent<BoxCollider2D>().bounds.center - (Vector2)awayRegion.Root.position;
            int dirt = NetWorldCount(mod.Inventory, ItemType.DirtBlock);
            WorldSync.Submit(new WorldSync.Action { Op = "drop", Scene = "Crossroads_01", X = spawn.x, Y = spawn.y,
                Kind = (int)ItemType.DirtBlock, Count = 11, Vy = 2f, Power = 100 });
            yield return NetWorldWait(() => WorldItems.NetRecords().Any(r => r.Kind == (int)ItemType.DirtBlock && r.Seed == 11 && r.Age > 5), "房主未加载场景时，共享掉落物仍推进物理和拾取延迟");
            yield return NetWorldWait(() => NetWorldCount(mod.Inventory, ItemType.DirtBlock) == dirt + 11
                || WorldItems.NetRecords().Any(r => r.Kind == (int)ItemType.DirtBlock && r.Seed == 11 && r.Delay == 0), "远处场景的抛出者拾取延迟正常结束");
            var awayDrop = WorldItems.NetRecords().FirstOrDefault(r => r.Kind == (int)ItemType.DirtBlock && r.Seed == 11);
            if (awayDrop != null)
            {
                Plugin.Log.LogInfo($"[联机测试] 远处物品 age={awayDrop.Age} delay={awayDrop.Delay} pos={awayDrop.X},{awayDrop.Y} owner={awayDrop.Owner}；{WorldItems.DevDescribe()}");
                var world = (Vector2)awayRegion.Root.position + new Vector2(awayDrop.X, awayDrop.Y);
                hero.transform.position += (Vector3)(world - (Vector2)hero.GetComponent<BoxCollider2D>().bounds.center);
            }
            yield return NetWorldWait(() => NetWorldCount(mod.Inventory, ItemType.DirtBlock) == dirt + 11, "客机可在房主未加载的场景拾回共享物品");
            NetChat.Say("world: away physics");
            yield return WaitChat("world: away confirmed", 30f);
            Save.ModSave.Write(slot, mod);
            var saved = Newtonsoft.Json.Linq.JObject.Parse(System.IO.File.ReadAllText(System.IO.Path.Combine(TerraMod.SaveDir, "TerraKnight_slot" + slot + ".json")));
            NetCheck((int)saved["CrystalSeed"] == WorldSync.SaveSeed && (int)saved["CrystalSeed"] != 87654321, "客机实际写盘保留个人世界种子");
            NetCheck(saved["Chests"].Count() == 0 && saved["Stations"].Count() == 0, "客机实际写盘不混入房主箱子和工作台");
            NetChat.Say("world: client done");
            yield return WaitChat("world: host done", 90f);
            NetCheck(GameManager.instance.sceneName == "Crossroads_01" && !GameManager.instance.IsInSceneTransition, "房主进入黄蜂战场时客机仍留在原场景");
        }

        void NetWorldShot(string scene, string label)
        {
            var cam = GameCameras.instance.mainCamera;
            var target = new RenderTexture(960, 540, 24);
            var before = cam.targetTexture;
            var active = RenderTexture.active;
            var tex = new Texture2D(960, 540, TextureFormat.RGB24, false);
            try
            {
                cam.targetTexture = target;
                cam.Render();
                RenderTexture.active = target;
                tex.ReadPixels(new Rect(0, 0, 960, 540), 0, 0);
                tex.Apply();
                var pixels = tex.GetPixels32();
                if (!pixels.Any(p => p.r > 20 || p.g > 20 || p.b > 20))
                    Plugin.Log.LogWarning("[AutoTest] 隐藏窗口的镜头输出为黑色，画面检查未完成 " + label);
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(outDir, scene + "_networld_" + label + ".png"), tex.EncodeToPNG());
            }
            finally
            {
                cam.targetTexture = before;
                RenderTexture.active = active;
                Object.Destroy(tex); Object.Destroy(target);
            }
        }
    }
}
