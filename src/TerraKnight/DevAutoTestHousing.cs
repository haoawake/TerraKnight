using System.Collections;
using System.Collections.Generic;
using TerraKnight.Items;
using TerraKnight.Town;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "housing"): Terraria's housing. A house built in Dirtmouth's sky with the mod's own placing (wood blocks,
    /// wood walls, a torch, the Wooden Door, the Wooden Table, the Wooden Chair) is valid housing (the query says so); each
    /// thing taken away gives Terraria's own diagnosis (no light, no table, no chair, no door, no walls, a gap in the walls,
    /// too small, too big) and the head clicked in it moves no one in; the Guide moves in at once and stays inside; a
    /// second townsperson is refused (occupied) while a town pet shares; the shut door stops the Knight and the open one
    /// lets him by; the banners while the panel is up; a wall broken: the periodic look (7200 ticks) turns him out with
    /// Terraria's words; the house through a save and back; a house in the Forgotten Crossroads keeps its resident there
    /// and out of Dirtmouth; the panel's chair and bench for clicks in the open street.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator HousingTest()
        {
            int ok = 0, all = 0;
            void Check(string what, bool pass, string detail = "")
            {
                all++;
                if (pass) ok++;
                Plugin.Log.LogInfo($"[AutoTest] 房屋 {what}：{detail} {(pass ? "通过" : "不通过")}");
            }
            const float c = CellGrid.CellSize;
            int stageWas = Shop.DevStage;
            Shop.DevStage = 5;
            Angler.Saved = true;
            mod.DevNoFallDamage = true;
            yield return GoTo("Town", "left1");
            PlayerData.instance.isInvincible = true;
            yield return new WaitForSeconds(3f);
            var hero = HeroController.instance;
            var rb = hero.GetComponent<Rigidbody2D>();
            string T(string key) => World.Housing.Text(key);
            string Missing(string part) => string.Format(T("Game.HouseMissing_1"), T(part));
            bool Said(string line) => TownNpcs.DevSaid.Contains(line);
            string SaidAll() => string.Join(" / ", TownNpcs.DevSaid);
            Vector2 Cell(int x, int y) => CellGrid.CellCenter(x, y);
            IEnumerator Land(float seconds = 3f)
            {
                float t = Time.time;
                while (Time.time - t < 0.2f) yield return null;
                while (Time.time - t < seconds && !hero.cState.onGround) yield return null;
                yield return new WaitForSeconds(0.3f);
            }
            void Put(Vector2 at)
            {
                hero.transform.position = Clear(hero, at);
                rb.linearVelocity = Vector2.zero;
            }

            // A box of open air in a scene's own grid, room to spare around it.
            bool Free(int x0, int y0, int w, int h, string scene)
            {
                for (int x = x0 - 2; x < x0 + w + 2; x++)
                    for (int y = y0 - 2; y < y0 + h + 2; y++)
                    {
                        var r = TerraWorld.RegionAt(x, y);
                        if (r == null || r.SceneName != scene || TerraWorld.Get(x, y) != CellKind.Empty || BossRooms.Locked(x, y)) return false;
                        if (Stations.Covers(new Vector2Int(x, y)) || Chests.Covers(new Vector2Int(x, y), orUnder: true)) return false;
                    }
                return true;
            }
            bool FindFree(int w, int h, string scene, Vector2Int from, out Vector2Int at)
            {
                for (int dy = 4; dy <= 90; dy += 2)
                    for (int dx = 0; dx <= 120; dx += 3)
                        foreach (int sx in new[] { 1, -1 })
                        {
                            var p = new Vector2Int(from.x + sx * dx, from.y + dy);
                            if (Free(p.x, p.y, w, h, scene)) { at = p; return true; }
                        }
                at = default;
                return false;
            }
            // A frame of wood blocks round the box, wood walls behind all of it (wall: 0 for none at all).
            void Frame(int x0, int y0, int w, int h, int wall)
            {
                for (int x = x0; x < x0 + w; x++)
                    for (int y = y0; y < y0 + h; y++)
                    {
                        bool edge = x == x0 || y == y0 || x == x0 + w - 1 || y == y0 + h - 1;
                        TerraWorld.Set(x, y, edge ? CellKind.Wood : CellKind.Empty);
                        TerraWorld.SetWall(x, y, wall);
                    }
            }
            void Unbuild(int x0, int y0, int w, int h)
            {
                for (int x = x0; x < x0 + w; x++)
                    for (int y = y0; y < y0 + h; y++)
                    {
                        TerraWorld.Set(x, y, CellKind.Empty);
                        TerraWorld.SetWall(x, y, 0);
                    }
            }
            // The whole house, 12 by 8 cells (inside 10 by 6), the door on its left (a porch of 14 cells before it), a
            // torch, a table (cells 4..6) and a chair (cell 7).
            const int W = 12, H = 8, Porch = 14;
            IEnumerator House(int x0, int y0, string tag, bool porch = true)
            {
                Frame(x0, y0, W, H, 4);
                if (porch) for (int x = x0 - Porch; x < x0; x++) TerraWorld.Set(x, y0, CellKind.Wood);
                for (int y = y0 + 1; y <= y0 + 3; y++) TerraWorld.Set(x0, y, CellKind.Empty);
                TerraWorld.Set(x0 + 9, y0 + 4, CellKind.Torch);
                yield return new WaitForSeconds(0.5f);
                Put(Cell(x0 + 2, y0 + 1));
                yield return Land();
                bool door = Stations.TryPlace(StationKind.Door, hero, Cell(x0, y0 + 1));
                string whyDoor = Stations.DevWhy;
                bool table = Stations.TryPlace(StationKind.Table, hero, Cell(x0 + 5, y0 + 1));
                string whyTable = Stations.DevWhy;
                bool chair = Stations.TryPlace(StationKind.Chair, hero, Cell(x0 + 7, y0 + 1));
                string whyChair = Stations.DevWhy;
                yield return null;
                Check(tag + " 用 mod 自己的放置盖好房子（木块、木墙、火把、木门、木桌、木椅）", door && table && chair,
                    $"门 {door}（{whyDoor}），桌子 {table}（{whyTable}），椅子 {chair}（{whyChair}），框 ({x0},{y0}) {W}x{H}");
            }

            // 1. The house in Dirtmouth's sky.
            var heroCell = CellGrid.WorldToCell(hero.GetComponent<BoxCollider2D>().bounds.center);
            if (!FindFree(W + 70 + Porch + 2, H + 30, "Town", heroCell, out var spot))
            {
                Check("德特茅斯上空找到空地", false, $"主角格 {heroCell}");
                Shop.DevStage = stageWas;
                yield break;
            }
            int hx = spot.x + Porch + 2, hy = spot.y;
            yield return House(hx, hy, "德特茅斯");
            var inside = Cell(hx + 3, hy + 3);
            Plugin.Log.LogInfo($"[AutoTest] 房屋 房子在 ({hx},{hy})，世界 {Cell(hx, hy)}，点房间里 {inside}");

            // 2. The housing query on it: valid.
            string q = TownNpcs.DevQuery(inside);
            Check("房屋询问：合适的房子", Said(T("LegacyInterface.39")), q);

            // Even a valid room cannot summon a functional resident before its unlock.
            TownNpcs.Remove("Nurse");Shop.DevStage=0;
            string denied=TownNpcs.DevHouse("Nurse",inside);
            Check("合适房屋不能提前安置护士", !TownNpcs.Houses.ContainsKey("Nurse") && !TownNpcs.Present("Nurse") && TownNpcs.DevSaid.Count>0,denied);
            Shop.DevStage=5;
            string allowed=TownNpcs.DevHouse("Nurse",inside);
            Check("达到阶段后同一房屋可安置护士",TownNpcs.Houses.ContainsKey("Nurse") && TownNpcs.Present("Nurse"),allowed);
            TownNpcs.Remove("Nurse");

            if (suite == "skeletonhousing")
            {
                TownNpcs.Remove("SkeletonMerchant");
                Shop.DevStage=0;
                string skeletonDenied=TownNpcs.DevHouse("SkeletonMerchant",inside);
                Check("骷髅商人未解锁时不能借住房提前出现",!TownNpcs.Houses.ContainsKey("SkeletonMerchant")&&!TownNpcs.Present("SkeletonMerchant"),skeletonDenied);
                Shop.DevStage=5;
                string skeletonPlaced=TownNpcs.DevHouse("SkeletonMerchant",inside);
                bool SkeletonInside()
                {
                    var point=TownNpcs.DevFeet("SkeletonMerchant");
                    return point.HasValue&&point.Value.x>(hx+1)*c&&point.Value.x<(hx+W-1)*c
                        &&point.Value.y>(hy+1)*c-.3f&&point.Value.y<(hy+2)*c+.3f;
                }
                Check("骷髅商人通过住房面板真实入住",TownNpcs.Houses.ContainsKey("SkeletonMerchant")&&SkeletonInside(),skeletonPlaced);
                string secondResident=TownNpcs.DevHouse("Merchant",inside);
                Check("骷髅商人占住房间，其他居民不能重复入住",!TownNpcs.Houses.ContainsKey("Merchant")&&Said(T("LegacyInterface.41")),secondResident);
                yield return new WaitForSeconds(2f);
                Check("入住后在自己的房间内活动",SkeletonInside(),TownNpcs.DevFeet("SkeletonMerchant").ToString());
                TownNpcs.DevRecheckHouses();
                Check("定期房屋复查保留骷髅商人住房",TownNpcs.Houses.ContainsKey("SkeletonMerchant")&&SkeletonInside());
                var skeletonPosition=TownNpcs.DevPos("SkeletonMerchant");
                if(skeletonPosition.HasValue)Put(skeletonPosition.Value+Vector2.left*.8f);
                yield return new WaitForSeconds(.4f);
                bool skeletonTalk=skeletonPosition.HasValue&&TownNpcs.TryRightClick(TownNpcs.DevPos("SkeletonMerchant").Value);
                TownNpcs.OpenShop();yield return null;
                Check("入住后仍可对话打开商店",skeletonTalk&&TownNpcs.ShopOpen&&TownNpcs.DevStock().Contains("="),TownNpcs.DevStock());
                TownNpcs.Close(true);InventoryGui.Open=false;
                int skeletonSlot=GameManager.instance.profileID;
                string skeletonBefore=TownNpcs.DevHouseOf("SkeletonMerchant",out _);
                Save.ModSave.Write(skeletonSlot,mod);
                // Load the saved world before re-entering its room, as a normal game load does.
                yield return GoTo("Crossroads_01",EntryGateOf("Crossroads_01"));
                Save.ModSave.Read(skeletonSlot,mod);
                yield return GoTo("Town","left1");
                yield return new WaitForSeconds(1.5f);
                string skeletonAfter=TownNpcs.DevHouseOf("SkeletonMerchant",out _);
                Check("存读档后骷髅商人仍住在原房屋",skeletonBefore!=null&&skeletonAfter!=null&&SkeletonInside(),$"之前 {skeletonBefore}，之后 {skeletonAfter}，脚下 {TownNpcs.DevFeet("SkeletonMerchant")}");
                Put(Cell(hx+2,hy+1));yield return Land();
                InventoryGui.Open=true;if(!TownNpcs.PanelOpen)TownNpcs.TogglePanel();
                yield return new WaitForSeconds(.5f);
                Check("住房旗帜包含骷髅商人",TownNpcs.DevBannerAt("SkeletonMerchant").HasValue);
                Shot("Town","skeleton_merchant_housed");yield return null;yield return null;
                TownNpcs.ClosePanel();InventoryGui.Open=false;
                TerraWorld.Set(hx+W-1,hy+3,CellKind.Empty);
                TownNpcs.DevRecheckHouses();
                Check("房屋损坏后按普通居民规则失去住房",!TownNpcs.Houses.ContainsKey("SkeletonMerchant"));
                TownNpcs.Remove("SkeletonMerchant");Shop.DevStage=stageWas;mod.DevNoFallDamage=false;
                Plugin.Log.LogInfo($"[AutoTest] 房屋 合计 {ok}/{all} {(ok==all?"通过":"不通过")}");
                yield break;
            }

            // 3. What each missing thing gives (before anyone lives here, so the periodic look can't turn anyone out).
            TerraWorld.Set(hx + 9, hy + 4, CellKind.Empty);
            q = TownNpcs.DevQuery(inside);
            Check("缺光源", Said(Missing("Game.HouseLightSource")), q);
            string a = TownNpcs.DevHouse("Merchant", inside);
            Check("缺光源时选中商人点进去：不入住", !TownNpcs.Houses.ContainsKey("Merchant") && Said(Missing("Game.HouseLightSource")), a);
            TerraWorld.Set(hx + 9, hy + 4, CellKind.Torch);

            Stations.DevRemoveNear(Cell(hx + 5, hy + 1), StationKind.Table);
            yield return null;
            q = TownNpcs.DevQuery(inside);
            Check("缺桌子", Said(Missing("Game.HouseTable")), q);
            // A Work Bench is a table to Terraria (CountsAsTableTypes has 18).
            bool wb = Stations.TryPlace(StationKind.WorkBench, hero, Cell(hx + 5, hy + 1));
            yield return null;
            q = TownNpcs.DevQuery(inside);
            Check("工作台也算桌子", wb && Said(T("LegacyInterface.39")), q);
            Stations.DevRemoveNear(Cell(hx + 5, hy + 1), StationKind.WorkBench);
            Put(Cell(hx + 2, hy + 1));
            yield return Land();
            Stations.TryPlace(StationKind.Table, hero, Cell(hx + 5, hy + 1));
            yield return null;

            Stations.DevRemoveNear(Cell(hx + 7, hy + 1), StationKind.Chair);
            yield return null;
            q = TownNpcs.DevQuery(inside);
            Check("缺椅子", Said(Missing("Game.HouseChair")), q);
            Stations.TryPlace(StationKind.Chair, hero, Cell(hx + 7, hy + 1));
            yield return null;

            Stations.DevRemoveNear(Cell(hx, hy + 2), StationKind.Door);
            for (int y = hy + 1; y <= hy + 3; y++) TerraWorld.Set(hx, y, CellKind.Wood);
            yield return null;
            q = TownNpcs.DevQuery(inside);
            Check("缺门（没有入口）", Said(Missing("Game.HouseDoor")), q);
            // A platform for an entrance counts as a door (TileID.Sets.RoomNeeds.CountsAsDoor has 19).
            TerraWorld.Set(hx + 5, hy + H - 1, CellKind.Platform);
            q = TownNpcs.DevQuery(inside);
            Check("平台当入口也算门", Said(T("LegacyInterface.39")), q);
            TerraWorld.Set(hx + 5, hy + H - 1, CellKind.Wood);
            for (int y = hy + 1; y <= hy + 3; y++) TerraWorld.Set(hx, y, CellKind.Empty);
            yield return new WaitForSeconds(0.3f);
            bool doorBack = Stations.TryPlace(StationKind.Door, hero, Cell(hx, hy + 1));
            yield return null;
            q = TownNpcs.DevQuery(inside);
            Check("东西放回去又合适了", doorBack && Said(T("LegacyInterface.39")), q);
            a = TownNpcs.DevHouse("Truffle", inside);
            Check("松露人不在蘑菇地：此房屋不符合要求", !TownNpcs.Houses.ContainsKey("Truffle") && TownNpcs.DevSaid.Exists(l => l.StartsWith(T("LegacyInterface.55"))), a);

            for (int x = hx; x < hx + W; x++) for (int y = hy; y < hy + H; y++) TerraWorld.SetWall(x, y, 0);
            q = TownNpcs.DevQuery(inside);
            Check("没有背景墙", Said(T("TownNPCHousingFailureReasons.HoleInWallIsTooBig")), q);
            for (int x = hx; x < hx + W; x++) for (int y = hy; y < hy + H; y++) TerraWorld.SetWall(x, y, 4);
            for (int x = hx + 3; x <= hx + 7; x++) for (int y = hy + 1; y < hy + H - 1; y++) TerraWorld.SetWall(x, y, 0);
            q = TownNpcs.DevQuery(inside);
            Check("背景墙中间缺一块（5 格宽）", Said(T("TownNPCHousingFailureReasons.HoleInWallIsTooBig")), q);
            for (int x = hx + 4; x <= hx + 7; x++) for (int y = hy + 1; y < hy + H - 1; y++) TerraWorld.SetWall(x, y, 4);
            q = TownNpcs.DevQuery(inside);
            // CheckRoom wants a wall within two tiles across and within two tiles up or down: a whole column without
            // walls, six tiles high, fails in its middle (nothing two up or down), a single cell without one doesn't.
            Check("一整列竖着缺墙（6 格高）也不行", Said(T("TownNPCHousingFailureReasons.HoleInWallIsTooBig")), q);
            for (int x = hx; x < hx + W; x++) for (int y = hy; y < hy + H; y++) TerraWorld.SetWall(x, y, 4);
            TerraWorld.SetWall(hx + 3, hy + 3, 0);
            q = TownNpcs.DevQuery(inside);
            Check("只缺一格墙不要紧（左右、上下两格内都有墙）", Said(T("LegacyInterface.39")), q);
            for (int x = hx; x < hx + W; x++) for (int y = hy; y < hy + H; y++) TerraWorld.SetWall(x, y, 4);
            q = TownNpcs.DevQuery(Cell(hx, hy + 6));
            Check("点在方块上", Said(T("TownNPCHousingFailureReasons.RoomCheckStartedInASolidTile")), q);

            // Too small (inside 4 by 4) and too big (inside 64 by 12, 768 tiles), both walled all over.
            int sx0 = hx + W + 4, sy0 = hy;
            Frame(sx0, sy0, 6, 6, 4);
            q = TownNpcs.DevQuery(Cell(sx0 + 2, sy0 + 2));
            Check("太小", Said(T("TownNPCHousingFailureReasons.RoomIsTooSmall")), q);
            Unbuild(sx0, sy0, 6, 6);
            int bx0 = hx, by0 = hy + H + 4;
            Frame(bx0, by0, 66, 14, 4);
            q = TownNpcs.DevQuery(Cell(bx0 + 3, by0 + 3));
            Check("太大", Said(T("TownNPCHousingFailureReasons.RoomIsTooBig")), q);
            Unbuild(bx0, by0, 66, 14);
            // Open sky, no walls: the query says what Terraria says of it.
            q = TownNpcs.DevQuery(Cell(hx + W + 20, hy + H + 30));
            Check("询问空中", TownNpcs.DevSaid.Count == 1 && !Said(T("LegacyInterface.39")), q);
            yield return new WaitForSeconds(0.3f);

            // 4. The Guide moves in at once and stays in his room.
            Shot("Town", "housing_1_house");
            yield return null;
            a = TownNpcs.DevHouse("Guide", inside);
            var feet = TownNpcs.DevFeet("Guide");
            // Inside, on the floor (or on the chair's seat, which a townsperson steps on as it passes).
            bool In(Vector2? f) => f.HasValue && f.Value.x > (hx + 1) * c && f.Value.x < (hx + W - 1) * c && f.Value.y > (hy + 1) * c - 0.3f && f.Value.y < (hy + 2) * c + 0.3f;
            string home = TownNpcs.DevHouseOf("Guide", out var homeFeet);
            Check("向导立刻住进房子", TownNpcs.Houses.ContainsKey("Guide") && In(feet), $"{a}；家 {home} 站点 {homeFeet}，现在脚下 {feet}");
            bool stayed = true;
            float minX = float.MaxValue, maxX = float.MinValue;
            for (int i = 0; i < 20; i++)
            {
                yield return new WaitForSeconds(0.5f);
                var f = TownNpcs.DevFeet("Guide");
                if (!In(f)) stayed = false;
                if (f.HasValue) { minX = Mathf.Min(minX, f.Value.x); maxX = Mathf.Max(maxX, f.Value.x); }
            }
            Check("向导 10 秒里一直在房间里（走动）", stayed, $"x {minX:F2}..{maxX:F2}（房间里 {(hx + 1) * c:F2}..{(hx + W - 1) * c:F2}），走动范围 {TownNpcs.DevWalk("Guide")}");
            Put(Cell(hx + 7, hy + 1));
            yield return Land();
            Shot("Town", "housing_2_guide");
            yield return null;
            Check("向导不在德特茅斯街上", TownNpcs.DevFeet("Guide").HasValue && TownNpcs.DevFeet("Guide").Value.y > (hy) * c, $"{TownNpcs.DevFeet("Guide")}");

            // 5. One to a room: the Merchant is refused; the cat (a town pet, housingCategory 1) may share.
            a = TownNpcs.DevHouse("Merchant", inside);
            Check("同一间再住商人：已被占据", !TownNpcs.Houses.ContainsKey("Merchant") && Said(T("LegacyInterface.41")), a);
            a = TownNpcs.DevHouse("Guide", inside);
            Check("向导再点自己的房间：已被占据（Terraria 也这样）", Said(T("LegacyInterface.41")), a);
            a = TownNpcs.DevHouse("TownCat", inside);
            Check("猫咪（城镇宠物）可以合住", TownNpcs.Houses.ContainsKey("TownCat") && In(TownNpcs.DevFeet("TownCat")), a);
            a = TownNpcs.DevHouse("TownDog", inside);
            Check("狗狗（第二只宠物）不能再住", !TownNpcs.Houses.ContainsKey("TownDog") && Said(T("LegacyInterface.41")), a);

            // 6. Banners while the panel is up; a right click on the cat's turns it out.
            InventoryGui.Open = true;
            if (!TownNpcs.PanelOpen) TownNpcs.TogglePanel();
            yield return new WaitForSeconds(0.6f);
            Shot("Town", "housing_3_banners");
            yield return null;
            int shown = TownNpcs.DevBanners(Vector2.zero, out _);
            var catBanner = TownNpcs.DevBannerAt("TownCat");
            bool kicked = catBanner.HasValue && TownNpcs.DevBannerRightClick(catBanner.Value);
            Check("面板打开时房子上挂旗帜，右键猫的旗帜把它请出", shown == 2 && kicked && !TownNpcs.Houses.ContainsKey("TownCat"), $"旗帜 {shown} 面，猫的旗帜 {catBanner}，请出 {kicked}");
            TownNpcs.ClosePanel();
            InventoryGui.Open = false;
            yield return new WaitForSeconds(3.5f);
            Check("请出的猫回到街上", TownNpcs.DevFeet("TownCat").HasValue && !In(TownNpcs.DevFeet("TownCat")), $"{TownNpcs.DevFeet("TownCat")}");

            // 7. The door: shut it stops the Knight, open he walks out through it (doors opened by a right click only:
            // walked into, a shut door opens by itself, which suite fx_17build looks at).
            Stations.DevNoAutoDoors = true;
            Put(Cell(hx + 2, hy + 1));
            yield return Land();
            var kind = Stations.DevDoorNear(Cell(hx, hy + 2), out var doorBox);
            Player.TerraMovement.DevMove = -1;
            yield return new WaitForSeconds(1.5f);
            Player.TerraMovement.DevMove = 0;
            float heroLeft = hero.GetComponent<BoxCollider2D>().bounds.min.x;
            Check("关着的门挡住小骑士", kind == StationKind.Door && heroLeft >= doorBox.xMax - 0.05f, $"门 {kind} {doorBox.min:F2}..{doorBox.max:F2}，主角左边 {heroLeft:F2}");
            int toggles = Stations.DevDoorToggles;
            bool opened = Stations.DevToggleDoor(Cell(hx, hy + 2));
            kind = Stations.DevDoorNear(Cell(hx, hy + 2), out var openBox);
            yield return new WaitForSeconds(0.2f);
            Shot("Town", "housing_4_door_open");
            yield return null;
            q = TownNpcs.DevQuery(inside);
            Check("开着的门也算门（房子仍合适，只是有人住）", Said(T("LegacyInterface.41")) || Said(T("LegacyInterface.39")), q);
            Player.TerraMovement.DevMove = -1;
            yield return new WaitForSeconds(0.45f);
            Player.TerraMovement.DevMove = 0;
            yield return new WaitForSeconds(0.3f);
            float heroRight = hero.GetComponent<BoxCollider2D>().bounds.max.x;
            Check("开着的门可以走过去", opened && kind == StationKind.DoorOpenLeft && heroRight < hx * c, $"开门 {opened}（朝 {kind}，{openBox.min:F2}..{openBox.max:F2}），主角右边 {heroRight:F2}，门框左边 {hx * c:F2}");
            yield return Land();
            bool shut = Stations.DevToggleDoor(Cell(hx, hy + 2));
            kind = Stations.DevDoorNear(Cell(hx, hy + 2), out _);
            Check("在门外把门关上", shut && kind == StationKind.Door && Stations.DevDoorToggles == toggles + 2, $"关门 {shut}，门 {kind}");
            Player.TerraMovement.DevMove = 1;
            yield return new WaitForSeconds(1.5f);
            Player.TerraMovement.DevMove = 0;
            float heroRight2 = hero.GetComponent<BoxCollider2D>().bounds.max.x;
            Check("从外面也挡住", heroRight2 <= hx * c + 0.05f, $"主角右边 {heroRight2:F2}，门 {hx * c:F2}");
            Stations.DevNoAutoDoors = false;

            // 8. Through a save and back.
            int slot = GameManager.instance.profileID;
            string before = TownNpcs.DevHouseOf("Guide", out _);
            Save.ModSave.Write(slot, mod);
            Save.ModSave.Read(slot, mod);
            yield return new WaitForSeconds(1.5f);
            string after = TownNpcs.DevHouseOf("Guide", out _);
            Check("存档读回后房屋还在，向导回到房里", before != null && before == after && In(TownNpcs.DevFeet("Guide")), $"之前 {before}，之后 {after}，脚下 {TownNpcs.DevFeet("Guide")}");

            // 9. A wall broken: the next look at the homes (every 7200 ticks) turns him out, Terraria's words in the chat.
            TerraWorld.Set(hx + W - 1, hy + 3, CellKind.Empty);
            int evicted = TownNpcs.DevEvicted;
            float ticks = TownNpcs.DevTicksToCheck;
            float t0 = Time.time;
            while (TownNpcs.DevEvicted == evicted && Time.time - t0 < 130f) yield return null;
            float took = Time.time - t0;
            string why = SaidAll();
            Check("拆掉一块墙：定期检查（7200 帧）后失去房屋，Terraria 的话", TownNpcs.DevEvicted > evicted && !TownNpcs.Houses.ContainsKey("Guide")
                && why.Contains(T("LegacyInterface.55")) && Said(T("TownNPCHousingFailureReasons.HoleInWallIsTooBig")),
                $"等了 {took:F0} 秒（离检查还剩 {ticks:F0} 帧）：{why}");
            yield return new WaitForSeconds(4f);
            var gFeet = TownNpcs.DevFeet("Guide");
            Check("失去房屋的向导回到原来的地方（街上）", gFeet.HasValue && !In(gFeet), $"{gFeet}");
            TerraWorld.Set(hx + W - 1, hy + 3, CellKind.Wood);
            TownNpcs.DevRecheckHouses();

            // 10. The panel's own places in the open street: a chair, the bench.
            string bench = TownNpcs.DevPlaceAt("Painter", Cell(heroCell.x, heroCell.y));
            Check("街上空地：仍然放在长椅（默认位置）", TownNpcs.Present("Painter") && !TownNpcs.Houses.ContainsKey("Painter"), bench);

            // 11. A house in the Forgotten Crossroads: its resident lives there and not in Dirtmouth.
            yield return GoTo("Crossroads_01", EntryGateOf("Crossroads_01"));
            PlayerData.instance.isInvincible = true;
            yield return new WaitForSeconds(2f);
            hero = HeroController.instance;
            rb = hero.GetComponent<Rigidbody2D>();
            var cc = CellGrid.WorldToCell(hero.GetComponent<BoxCollider2D>().bounds.center);
            if (!FindFree(W + 2, H, "Crossroads_01", cc, out var cs))
            {
                Check("十字路找到空地", false, $"主角格 {cc}");
            }
            else
            {
                yield return House(cs.x, cs.y, "十字路", false);
                a = TownNpcs.DevHouse("Merchant", Cell(cs.x + 3, cs.y + 3));
                var mFeet = TownNpcs.DevFeet("Merchant");
                var mCell = mFeet.HasValue ? CellGrid.WorldToCell(mFeet.Value + Vector2.up * 0.1f) : default;
                var mRegion = mFeet.HasValue ? TerraWorld.RegionAt(mCell.x, mCell.y) : null;
                Check("商人住进十字路的房子", TownNpcs.Houses.ContainsKey("Merchant") && mRegion != null && mRegion.SceneName == "Crossroads_01", $"{a}；脚下 {mFeet} 在 {mRegion?.SceneName}");
                Shot("Crossroads_01", "housing_5_crossroads");
                yield return null;
                yield return GoTo("Town", "left1");
                PlayerData.instance.isInvincible = true;
                yield return new WaitForSeconds(4f);
                var tFeet = TownNpcs.DevFeet("Merchant");
                var tCell = tFeet.HasValue ? CellGrid.WorldToCell(tFeet.Value + Vector2.up * 0.1f) : default;
                var tRegion = tFeet.HasValue ? TerraWorld.RegionAt(tCell.x, tCell.y) : null;
                Check("回德特茅斯：商人不在德特茅斯", tRegion == null || tRegion.SceneName != "Town", $"商人 {tFeet}（{tRegion?.SceneName}），{TownNpcs.HouseArea("Merchant")}");
                yield return GoTo("Crossroads_01", EntryGateOf("Crossroads_01"));
                yield return new WaitForSeconds(2f);
                var back = TownNpcs.DevFeet("Merchant");
                Check("回十字路：商人在自己的房子里", back.HasValue && back.Value.x > (cs.x + 1) * c && back.Value.x < (cs.x + W - 1) * c, $"{back}");
            }
            Shop.DevStage = stageWas;
            mod.DevNoFallDamage = false;
            Plugin.Log.LogInfo($"[AutoTest] 房屋 合计 {ok}/{all} {(ok == all ? "通过" : "不通过")}");
        }
    }
}
