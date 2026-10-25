using System.Collections;
using System.Linq;
using TerraKnight.Items;
using TerraKnight.Net;
using TerraKnight.Town;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    internal sealed partial class DevAutoTest
    {
        const int NetReleasePort = 7804;
        IEnumerator NetReleaseBoot()
        {
            if (suite == "netreleasehost")
            {
                NetCheck(NetSession.Host(NetReleasePort, "", 4, out var error), "发布联机开服 " + error);
                yield return NetWorldWait(() => NetSession.Active, "发布联机主机握手", 30f);
            }
            else
            {
                float until = Time.realtimeSinceStartup + 120f;
                while (!NetSession.Active && Time.realtimeSinceStartup < until)
                {
                    NetSession.Join("127.0.0.1", NetReleasePort);
                    for (float t = Time.realtimeSinceStartup + 6f; !NetSession.Active && NetSession.State != NetSession.Phase.Off && Time.realtimeSinceStartup < t;) yield return null;
                    if (!NetSession.Active) yield return new WaitForSecondsRealtime(1f);
                }
                NetCheck(NetSession.Active, "发布联机客机握手");
            }
        }

        void NetReleaseCrafts()
        {
            var inv = mod.Inventory;
            inv.Infinite = false; inv.Override = -1; InventoryGui.Open = true;
            bool Step(ItemType result, ItemType lower)
            {
                var r = Recipes.All.First(x => x.Result == result && x.Ingredients.Any(i => i.Type == lower));
                foreach (var i in r.Ingredients.Where(i => i.Type != lower)) inv.Add(i.Type, i.Count);
                bool made = InventoryGui.DevCraft(result) && inv.Mouse.Type == result && inv.Mouse.Count == r.Count
                    && r.Ingredients.All(i => Recipes.Count(inv, i.Type) == 0);
                if (made) { inv.Add(inv.Mouse.Type, inv.Mouse.Count); inv.Mouse = default; }
                return made;
            }
            foreach (var boot in new[] { ItemType.HermesBoots, ItemType.FlurryBoots, ItemType.SailfishBoots, ItemType.SandBoots })
            {
                System.Array.Clear(inv.Slots, 0, inv.Slots.Length); inv.Mouse = default; inv.Add(boot, 1);
                NetCheck(Step(ItemType.SpectreBoots, boot) && Step(ItemType.LightningBoots, ItemType.SpectreBoots), "共享工匠作坊跑鞋升级 " + boot);
            }
            foreach (var emblem in new[] { ItemType.WarriorEmblem, ItemType.RangerEmblem, ItemType.SorcererEmblem, ItemType.SummonerEmblem })
            {
                System.Array.Clear(inv.Slots, 0, inv.Slots.Length); inv.Mouse = default; inv.Add(emblem, 1);
                NetCheck(Step(ItemType.AvengerEmblem, emblem) && Step(ItemType.DestroyerEmblem, ItemType.AvengerEmblem), "共享工匠作坊纹章升级 " + emblem);
            }
            InventoryGui.Open = false;
        }

        IEnumerator NetReleaseTest(bool host, string scene, HeroController hero)
        {
            if (!DevCopy.Verified) throw new System.InvalidOperationException("isolated saves required");
            mod.DevNoFallDamage = true; Player.TerraStats.DevImmortal = true;
            PlayerData.instance.isInvincible = true;
            Shop.DevStage = 5; mod.ApplyCreative(false);
            var inv = mod.Inventory; inv.Infinite = false; inv.Override = -1;
            yield return NetWorldWait(() => WorldSync.Ready && Other() != null && Other().Visible, "发布联机两人进入世界", 150f);
            Angler.Saved = true; Angler.QuestDay = -1; Angler.QuestDone = false;
            Angler.Pending = host ? 1 : 3;
            Angler.Quest = host ? ItemType.HkQuestFish01 : ItemType.HkQuestFish02;
            var region = TerrainSystem.Current.Region;
            var centre = (Vector2)hero.GetComponent<BoxCollider2D>().bounds.center;
            if (host)
            {
                Stations.Clear(); Chests.Clear(); WorldItems.Clear();
                var local = centre - (Vector2)region.Root.position;
                NetCheck(Stations.NetPlace(new WorldSync.Action { Scene = scene, X = local.x + 1f, Y = local.y - 0.6f, Kind = (int)StationKind.TinkerersWorkshop }), "发布联机放置工匠作坊");
                NetCheck(Chests.NetPlace(new WorldSync.Action { Scene = scene, X = local.x - 1f, Y = local.y - 0.6f }), "发布联机放置重连箱子");
                NetChat.Say("release: fixtures");
            }
            else yield return WaitChat("release: fixtures", 90f);
            yield return NetWorldWait(() => Stations.NetRecords().Any(r => r.Kind == (int)StationKind.TinkerersWorkshop), "两端均收到共享工匠作坊");
            var station = Stations.NetRecords().First(r => r.Kind == (int)StationKind.TinkerersWorkshop);
            hero.transform.position = Clear(hero, (Vector2)region.Root.position + new Vector2(station.X - 0.5f, station.Y + 1f));
            yield return new WaitForSeconds(0.5f);
            NetCheck(Stations.Near("工匠作坊", hero), "联机工匠作坊真实距离判定");
            NetReleaseCrafts();
            if (host)
            {
                yield return WaitChat("release: client locked", 90f);
                NetCheck(Angler.Pending == 1 && Angler.Quest == ItemType.HkQuestFish01, "客机交鱼不改变房主个人委托");
                Chests.DevOpenNear(hero.GetComponent<BoxCollider2D>().bounds.center);
                yield return new WaitForSecondsRealtime(0.5f);
                NetCheck(!Chests.IsOpen, "客机占箱时房主不能打开");
                NetChat.Say("release: leave now");
                yield return NetWorldWait(() => Other() == null, "真实断线移除远端角色", 45f);
                Chests.DevOpenNear(hero.GetComponent<BoxCollider2D>().bounds.center);
                yield return NetWorldWait(() => Chests.IsOpen, "断线自动释放共享箱锁");
                Chests.Close(false);
                yield return WaitChat("release: rejoined", 120f);
                NetCheck(NetSession.Hosting && Other() != null && Other().Visible, "断线重连后双方角色可见");
                NetChat.Say("release: reunion ok");
                yield return GoTo(Shimmer.Scene, EntryGateOf(Shimmer.Scene));
                NetChat.Say("release: host pool");
                yield return WaitChat("release: client pool", 90f);
            }
            else
            {
                System.Array.Clear(inv.Slots, 0, inv.Slots.Length); inv.Mouse = default;
                int done = Angler.QuestsDone, rewards = Angler.DevRewards;
                inv.Add(ItemType.HkQuestFish02, 1);
                string text = Angler.QuestButton(inv, "Angler");
                NetCheck(Angler.Pending == 2 && Angler.QuestsDone == done + 1 && Angler.DevRewards == rewards + 1 && !string.IsNullOrEmpty(text), "客机新任务鱼真实交付一次");
                var nextQuest = Angler.Quest;
                Chests.DevOpenNear(hero.GetComponent<BoxCollider2D>().bounds.center);
                yield return NetWorldWait(() => Chests.IsOpen, "客机获得断线前箱锁");
                NetChat.Say("release: client locked");
                yield return WaitChat("release: leave now", 45f);
                NetSession.Leave(null);
                yield return new WaitForSecondsRealtime(3f);
                NetSession.Join("127.0.0.1", NetReleasePort);
                yield return NetWorldWait(() => NetSession.Active && WorldSync.Ready && Other() != null && Other().Visible, "真实 TCP 重连与世界快照", 120f);
                NetCheck(Angler.Pending == 2 && Angler.Quest == nextQuest && Angler.QuestsDone == done + 1 && Angler.DevRewards == rewards + 1, "重连不增委托不重复奖励不换当前鱼");
                NetCheck(Stations.NetRecords().Count(r => r.Kind == (int)StationKind.TinkerersWorkshop) == 1 && Chests.NetRecords().Count() == 1, "重连家具和箱子没有重复");
                Save.ModSave.Write(slot, mod);
                var save = Newtonsoft.Json.Linq.JObject.Parse(System.IO.File.ReadAllText(System.IO.Path.Combine(TerraMod.SaveDir, "TerraKnight_slot" + slot + ".json")));
                NetCheck((int)save["AnglerPending"] == 2 && (int)save["AnglerQuests"] == done + 1, "客机个人渔夫进度实际写盘");
                NetChat.Say("release: rejoined");
                yield return WaitChat("release: reunion ok", 45f);
                yield return WaitChat("release: host pool", 90f);
                yield return GoTo(Shimmer.Scene, EntryGateOf(Shimmer.Scene));
                NetChat.Say("release: client pool");
            }
            yield return new WaitForSeconds(1f);
            WorldItems.DevNoGrab = true;
            var pool = Shimmer.Pool;
            NetCheck(pool.HasValue, "两端原生微光池存在");
            if (!pool.HasValue) yield break;
            Vector2 drop = new Vector2(pool.Value.center.x, pool.Value.yMax - 0.3f);
            hero = HeroController.instance;
            hero.transform.position = Clear(hero, new Vector2(drop.x, pool.Value.yMax + 1f));
            if (host)
            {
                WorldItems.Clear();
                var local = drop - (Vector2)TerrainSystem.Find(Shimmer.Scene).Root.position;
                NetCheck(WorldItems.NetSpawn(new WorldSync.Action { Scene = Shimmer.Scene, X = local.x, Y = local.y, Kind = (int)ItemType.TinOre, Count = 9 }, -1), "房主将共享锡矿投入微光");
                NetChat.Say("release: host tin");
            }
            else yield return WaitChat("release: host tin", 45f);
            yield return NetWorldWait(() => WorldItems.NetRecords().Where(r => r.Kind == (int)ItemType.CopperOre).Sum(r => r.Seed) == 9, "房主模拟微光转化同步9铜矿", 8f);
            NetCheck(!WorldItems.NetRecords().Any(r => r.Kind == (int)ItemType.TinOre), "微光转化原料两端消失");
            if (!host)
            {
                int copper = Recipes.Count(inv, ItemType.CopperOre);
                WorldItems.SweepPickup(drop, 20f, Shimmer.Scene);
                yield return NetWorldWait(() => Recipes.Count(inv, ItemType.CopperOre) == copper + 9, "客机拾取转化产物恰好一次", 8f);
                NetChat.Say("release: host shimmer checked");
                yield return WaitChat("release: host away", 90f);
                yield return NetWorldWait(() => Enemies.Owns(Shimmer.Scene), "房主离开后客机取得微光场景模拟权");
                var local = drop - (Vector2)TerrainSystem.Find(Shimmer.Scene).Root.position;
                WorldSync.Submit(new WorldSync.Action { Op = "drop", Scene = Shimmer.Scene, X = local.x, Y = local.y, Kind = (int)ItemType.TinOre, Count = 7 });
                NetChat.Say("release: client tin");
            }
            else
            {
                yield return WaitChat("release: host shimmer checked", 45f);
                NetCheck(!WorldItems.NetRecords().Any(r => r.Kind == (int)ItemType.CopperOre), "客机拾取后房主不保留重复产物");
                WorldItems.Clear();
                yield return GoTo("Town", "left1");
                NetChat.Say("release: host away");
                yield return WaitChat("release: client tin", 90f);
            }
            yield return NetWorldWait(() => WorldItems.NetRecords().Where(r => r.Kind == (int)ItemType.CopperOre).Sum(r => r.Seed) == 7, "客机模拟且房主异地时微光转化同步7铜矿", 8f);
            if (!host)
            {
                var local = drop - (Vector2)TerrainSystem.Find(Shimmer.Scene).Root.position;
                WorldSync.Submit(new WorldSync.Action { Op = "drop", Scene = Shimmer.Scene, X = local.x, Y = local.y, Kind = (int)ItemType.WoodPlatform, Count = 4 });
                NetChat.Say("release: decraft");
            }
            else yield return WaitChat("release: decraft", 45f);
            yield return NetWorldWait(() => WorldItems.NetRecords().Where(r => r.Kind == (int)ItemType.Wood).Sum(r => r.Seed) == 2, "房主异地时4平台拆解为2木材且两端一致", 8f);
            NetCheck(!WorldItems.NetRecords().Any(r => r.Kind == (int)ItemType.WoodPlatform)
                && WorldItems.NetRecords().Where(r => r.Kind == (int)ItemType.Wood).All(r => r.Scene == Shimmer.Scene && r.Shimmered), "拆解原料移除、产物场景与浮起状态同步");
            yield return new WaitForSeconds(3f);
            NetCheck(WorldItems.NetRecords().Where(r => r.Kind == (int)ItemType.CopperOre).Sum(r => r.Seed) == 7
                && WorldItems.NetRecords().Where(r => r.Kind == (int)ItemType.Wood).Sum(r => r.Seed) == 2, "转化产物在池内不继续循环或重复拆解");
            if (!host) NetChat.Say("release: all checked");
            else { yield return WaitChat("release: all checked", 45f); NetChat.Say("release: done"); }
            if (!host) yield return WaitChat("release: done", 45f);
            yield return new WaitForSecondsRealtime(1f);
        }
    }
}
