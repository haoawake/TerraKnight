using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TerraKnight.Items;
using TerraKnight.Town;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "fx_17build"): building as Terraria builds. The smart cursor's walls stay in the room the Knight
    /// stands in (Step_Walls: in his sight, the house's walls and door hiding the porch), what it finds nothing for going
    /// where the mouse is; the smart cursor's torches (Step_Torch: a spot that holds one, nearest the mouse, none within
    /// eight tiles of another, off over a torch); a shut door walked into opens and shuts behind him, from either side,
    /// one opened by a right click staying open; chairs, beds and relics face the way he faces (the placing preview
    /// too), through a save; townsfolk walk past chairs, benches and tables on the floor and open and shut a door across
    /// the street; furniture is drawn behind minions, shots and townsfolk, and a slime follows him past it.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator Build17()
        {
            const float c = CellGrid.CellSize;
            int stageWas = Shop.DevStage;
            Shop.DevStage = 5;
            Angler.Saved = true;
            mod.DevNoFallDamage = true;
            // A death on the way (Crossroads_01's arrival) respawns the Knight elsewhere: Dirtmouth again.
            for (int tries = 0; tries < 3 && GameManager.instance.sceneName != "Town"; tries++)
            {
                PlayerData.instance.isInvincible = true;
                yield return GoTo("Town", "left1");
            }
            PlayerData.instance.isInvincible = true;
            yield return new WaitForSeconds(3f);
            var hero = HeroController.instance;
            var rb = hero.GetComponent<Rigidbody2D>();
            var col = hero.GetComponent<BoxCollider2D>();
            var inv = mod.Inventory;
            bool infinite = inv.Infinite;
            inv.Infinite = true;
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
            bool Free(int x0, int y0, int w, int h)
            {
                for (int x = x0 - 2; x < x0 + w + 2; x++)
                    for (int y = y0 - 2; y < y0 + h + 2; y++)
                    {
                        var r = TerraWorld.RegionAt(x, y);
                        if (r == null || r.SceneName != "Town" || TerraWorld.Get(x, y) != CellKind.Empty || BossRooms.Locked(x, y)) return false;
                        if (Stations.Covers(new Vector2Int(x, y)) || Chests.Covers(new Vector2Int(x, y), orUnder: true)) return false;
                    }
                return true;
            }
            bool FindFree(int w, int h, Vector2Int from, out Vector2Int at)
            {
                for (int dy = 4; dy <= 90; dy += 2)
                    for (int dx = 0; dx <= 120; dx += 3)
                        foreach (int sx in new[] { 1, -1 })
                        {
                            var p = new Vector2Int(from.x + sx * dx, from.y + dy);
                            if (Free(p.x, p.y, w, h)) { at = p; return true; }
                        }
                at = default;
                return false;
            }
            void Face(int dir)
            {
                if (dir > 0 && !hero.cState.facingRight) hero.FaceRight();
                else if (dir < 0 && hero.cState.facingRight) hero.FaceLeft();
            }
            float HeroLeft() => col.bounds.min.x;
            float HeroRight() => col.bounds.max.x;

            // ---- the site: a wooden house (12 x 8 cells, its door on the left, a porch of 14 before it), open air
            // round it, and a stone floor 40 cells long below for the torches, the furniture and the slime.
            const int W = 12, H = 8, Porch = 14;
            var heroCell = CellGrid.WorldToCell(col.bounds.center);
            if (!FindFree(W + Porch + 50, H + 26, heroCell, out var spot))
            {
                FxCheck("德特茅斯上空找到空地", false, $"主角格 {heroCell}");
                Shop.DevStage = stageWas;
                yield break;
            }
            int hx = spot.x + Porch + 2, hy = spot.y + 14;
            var built = new List<Vector2Int>();
            void SetCell(int x, int y, CellKind k)
            {
                TerraWorld.Set(x, y, k);
                built.Add(new Vector2Int(x, y));
            }
            for (int x = hx; x < hx + W; x++)
                for (int y = hy; y < hy + H; y++)
                {
                    bool edge = x == hx || y == hy || x == hx + W - 1 || y == hy + H - 1;
                    if (edge) SetCell(x, y, CellKind.Wood);
                    TerraWorld.SetWall(x, y, 0);
                }
            for (int x = hx - Porch; x < hx; x++) SetCell(x, hy, CellKind.Wood);
            for (int y = hy + 1; y <= hy + 3; y++) TerraWorld.Set(hx, y, CellKind.Empty);
            int fy = spot.y, fx0 = spot.x, fx1 = spot.x + 46;
            for (int x = fx0; x <= fx1; x++) SetCell(x, fy, CellKind.Stone);
            yield return new WaitForSeconds(0.5f);
            Put(Cell(hx + 2, hy + 1));
            yield return Land();
            bool door = Stations.TryPlace(StationKind.Door, hero, Cell(hx, hy + 1));
            yield return null;
            Plugin.Log.LogInfo($"[AutoTest] 修复1016 建造17 房子 ({hx},{hy}) {W}x{H}，门 {door}（{Stations.DevWhy}），地板 {fx0}..{fx1} @ {fy}");

            // ---- 22. Step_Walls: with the smart cursor on, a wall goes in the room the Knight stands in.
            var wallDef = ItemDb.Get(ItemType.WoodWall);
            var outside = Cell(hx - 2, hy + 1);
            bool Inside(Vector2Int v) => v.x > hx && v.x < hx + W - 1 && v.y > hy && v.y < hy + H - 1;
            mod.DevSetSmartCursor(false);
            bool plain = mod.DevCanUse(ItemType.WoodWall, outside, out var plainCell, out bool plainSmart);
            mod.DevSetSmartCursor(true);
            bool smartOk = mod.DevCanUse(ItemType.WoodWall, outside, out var smartCell, out bool smartPicked);
            FxCheck("22 智能光标放墙：鼠标指在门外，墙落在当前房间里（视线被墙和门挡住）", door && plain && !plainSmart && plainCell == CellGrid.WorldToCell(outside)
                && smartOk && smartPicked && Inside(smartCell),
                $"门 {door}，关掉智能光标：{plain} {plainCell}（鼠标格 {CellGrid.WorldToCell(outside)}）；打开：{smartOk} 智能 {smartPicked} {smartCell}，房间里 {Inside(smartCell)}");
            Stations.DevToggleDoor(Cell(hx, hy + 2));
            var doorKind = Stations.DevDoorNear(Cell(hx, hy + 2), out _);
            smartOk = mod.DevCanUse(ItemType.WoodWall, Cell(hx - 1, hy + 2), out smartCell, out smartPicked);
            FxCheck("22 门开着时也只放房间里（开门的门轴一列挡视线，门口外面不算）", doorKind != StationKind.Door && smartOk && smartPicked && Inside(smartCell),
                $"门 {doorKind}，鼠标在门板外 ({hx - 1},{hy + 2})：{smartOk} 智能 {smartPicked} {smartCell}");
            Stations.DevToggleDoor(Cell(hx, hy + 2));
            yield return null;
            // Every cell of the room in reach takes its wall, one use at a time; none goes outside.
            var b0 = col.bounds;
            int roomInReach = 0;
            for (int x = hx + 1; x < hx + W - 1; x++)
                for (int y = hy + 1; y < hy + H - 1; y++)
                    if (TerraMod.InReach(b0, wallDef, new Vector2Int(x, y))) roomInReach++;
            int insideWalls = 0, outsideWalls = 0;
            for (int k = 0; k < roomInReach; k++) mod.DevUse(ItemType.WoodWall, outside);
            yield return null;
            for (int x = hx - Porch; x < hx + W + 3; x++)
                for (int y = hy - 2; y < hy + H + 2; y++)
                {
                    if (TerraWorld.GetWall(x, y) <= 0) continue;
                    if (Inside(new Vector2Int(x, y))) insideWalls++;
                    else outsideWalls++;
                }
            Shot("Town", "fx17_build_walls");
            yield return null;
            FxCheck("22 指着门外连放：房间里够得着的格子全铺上，门外一格没有", insideWalls == roomInReach && outsideWalls == 0,
                $"够得着的房间格 {roomInReach}，房间里的墙 {insideWalls}，房外的墙 {outsideWalls}");
            // Nothing left in sight: Terraria's tile target stays the mouse's, and the wall goes there.
            smartOk = mod.DevCanUse(ItemType.WoodWall, outside, out smartCell, out smartPicked);
            mod.DevUse(ItemType.WoodWall, outside);
            yield return null;
            var outCell = CellGrid.WorldToCell(outside);
            FxCheck("22 房间里都铺满了：智能光标不再选格，墙放在鼠标处（同原版）", smartOk && !smartPicked && smartCell == outCell && TerraWorld.GetWall(outCell.x, outCell.y) == 4,
                $"{smartOk} 智能 {smartPicked} {smartCell}，鼠标格的墙 {TerraWorld.GetWall(outCell.x, outCell.y)}");
            TerraWorld.SetWall(outCell.x, outCell.y, 0);

            // ---- 23. Step_Torch on the stone floor.
            Put(Cell(fx0 + 8, fy + 1));
            yield return Land();
            var air = Cell(fx0 + 9, fy + 4);
            mod.DevSetSmartCursor(false);
            bool torchPlain = mod.DevCanUse(ItemType.Torch, air, out _, out _);
            mod.DevSetSmartCursor(true);
            bool torchSmart = mod.DevCanUse(ItemType.Torch, air, out var torchCell, out bool torchPicked);
            mod.DevUse(ItemType.Torch, air);
            yield return null;
            bool placed1 = TerraWorld.Get(torchCell.x, torchCell.y) == CellKind.Torch;
            FxCheck("23 智能光标拿火把：鼠标在半空（手动放不了），火把落到最近能放的地面格", !torchPlain && torchSmart && torchPicked && torchCell.y == fy + 1 && Mathf.Abs(torchCell.x - (fx0 + 9)) <= 1 && placed1,
                $"手动 {torchPlain}；智能 {torchSmart}/{torchPicked} 格 {torchCell}（地面上一格 y={fy + 1}），放下 {placed1}");
            bool second = mod.DevCanUse(ItemType.Torch, Cell(fx0 + 12, fy + 4), out var secondCell, out bool secondPicked);
            FxCheck("23 附近 8 格内已有火把：不再选格（手动也放不了半空）", !second && !secondPicked, $"{second}/{secondPicked} {secondCell}");
            bool hover = mod.DevCanUse(ItemType.Torch, Cell(torchCell.x, torchCell.y), out var hoverCell, out bool hoverPicked);
            FxCheck("23 鼠标指着火把：智能光标关闭（DisableSmartCursor），用的是鼠标格", !hoverPicked && hoverCell == torchCell, $"{hover}/{hoverPicked} {hoverCell}");
            Put(Cell(fx0 + 20, fy + 1));
            yield return Land();
            bool far = mod.DevCanUse(ItemType.Torch, Cell(fx0 + 15, fy + 4), out var farCell, out bool farPicked);
            mod.DevUse(ItemType.Torch, Cell(fx0 + 15, fy + 4));
            yield return null;
            int apart = Mathf.Max(Mathf.Abs(farCell.x - torchCell.x), Mathf.Abs(farCell.y - torchCell.y));
            FxCheck("23 走远再放：第二支离第一支 9 格以上，最靠近鼠标的那格", far && farPicked && apart > 8 && TerraWorld.Get(farCell.x, farCell.y) == CellKind.Torch,
                $"{far}/{farPicked} {farCell}，相隔 {apart} 格");
            Shot("Town", "fx17_build_torches");
            yield return null;
            mod.DevSetSmartCursor(false);
            TerraWorld.Set(torchCell.x, torchCell.y, CellKind.Empty);
            if (far) TerraWorld.Set(farCell.x, farCell.y, CellKind.Empty);

            // ---- 24. DoorOpeningHelper: walked into, the shut door opens, and shuts behind him.
            Put(Cell(hx + 3, hy + 1));
            yield return Land();
            int opened0 = Stations.DevPushedOpen, shut0 = Stations.DevPushedShut;
            var seen = new HashSet<StationKind>();
            Player.TerraMovement.DevMove = -1;
            bool shotOpen = false;
            for (float t = 0f; t < 4f && HeroRight() > (hx - 2) * c; t += Time.deltaTime)
            {
                yield return null;
                var k = Stations.DevDoorNear(Cell(hx, hy + 2), out _);
                seen.Add(k);
                if (!shotOpen && k == StationKind.DoorOpenLeft && HeroLeft() < (hx + 0.5f) * c) { Shot("Town", "fx17_build_door_push"); shotOpen = true; }
            }
            Player.TerraMovement.DevMove = 0;
            yield return new WaitForSeconds(0.4f);
            var after = Stations.DevDoorNear(Cell(hx, hy + 2), out _);
            FxCheck("24 往关着的门走：门朝走的方向推开（只开一次，不来回开关），走出门后自己关上", seen.Contains(StationKind.DoorOpenLeft) && HeroRight() < hx * c && after == StationKind.Door
                && Stations.DevPushedOpen - opened0 == 1 && Stations.DevPushedShut - shut0 == 1,
                $"见到 {string.Join("/", seen)}，现在 {after}，主角右边 {HeroRight():F2}（门 {hx * c:F2}），推开 {Stations.DevPushedOpen - opened0} 关上 {Stations.DevPushedShut - shut0}");
            seen.Clear();
            opened0 = Stations.DevPushedOpen;
            shut0 = Stations.DevPushedShut;
            Player.TerraMovement.DevMove = 1;
            for (float t = 0f; t < 4f && HeroLeft() < (hx + 2) * c; t += Time.deltaTime)
            {
                yield return null;
                seen.Add(Stations.DevDoorNear(Cell(hx, hy + 2), out _));
            }
            Player.TerraMovement.DevMove = 0;
            yield return new WaitForSeconds(0.4f);
            after = Stations.DevDoorNear(Cell(hx, hy + 2), out _);
            FxCheck("24 从外面往里走：门朝里推开，进门后关上", seen.Contains(StationKind.DoorOpenRight) && HeroLeft() > (hx + 1) * c && after == StationKind.Door
                && Stations.DevPushedOpen - opened0 == 1 && Stations.DevPushedShut - shut0 == 1,
                $"见到 {string.Join("/", seen)}，现在 {after}，主角左边 {HeroLeft():F2}，推开 {Stations.DevPushedOpen - opened0} 关上 {Stations.DevPushedShut - shut0}");
            // Standing still at the shut door, nothing opens it.
            Put(Cell(hx + 1, hy + 1));
            yield return Land();
            int opened1 = Stations.DevPushedOpen;
            yield return new WaitForSeconds(0.8f);
            FxCheck("24 站在门边不动：门不开", Stations.DevPushedOpen == opened1 && Stations.DevDoorNear(Cell(hx, hy + 2), out _) == StationKind.Door, $"推开 {Stations.DevPushedOpen - opened1}");
            // Opened by a right click, it stays open as he walks through (DoorOpeningHelper shuts only what it opened).
            Stations.DevToggleDoor(Cell(hx, hy + 2));
            Player.TerraMovement.DevMove = -1;
            for (float t = 0f; t < 4f && HeroRight() > (hx - 2) * c; t += Time.deltaTime) yield return null;
            Player.TerraMovement.DevMove = 0;
            yield return new WaitForSeconds(0.5f);
            after = Stations.DevDoorNear(Cell(hx, hy + 2), out _);
            FxCheck("24 右键打开的门：走过去后还开着（原版自动关门只管推开的门）", after != StationKind.Door && HeroRight() < hx * c, $"门 {after}，主角右边 {HeroRight():F2}");
            Stations.DevToggleDoor(Cell(hx, hy + 2));
            yield return null;

            // ---- 25. Facing: chairs, a bed and a relic face the way he faces, the preview too, through a save.
            Put(Cell(fx0 + 4, fy + 1));
            yield return Land();
            Face(1);
            yield return null;
            bool chairR = Stations.TryPlace(StationKind.Chair, hero, Cell(fx0 + 6, fy + 1));
            Face(-1);
            yield return null;
            bool chairL = Stations.TryPlace(StationKind.Chair, hero, Cell(fx0 + 2, fy + 1));
            yield return null;
            int faceR = Stations.DevFacingNear(Cell(fx0 + 6, fy + 1), StationKind.Chair), faceL = Stations.DevFacingNear(Cell(fx0 + 2, fy + 1), StationKind.Chair);
            int frameR = Stations.DevFrameXNear(Cell(fx0 + 6, fy + 1), StationKind.Chair), frameL = Stations.DevFrameXNear(Cell(fx0 + 2, fy + 1), StationKind.Chair);
            FxCheck("25 椅子：面朝右放下用右朝向（frameX 18），面朝左用左朝向（frameX 0）", chairR && chairL && faceR == 1 && faceL == -1 && frameR == 18 && frameL == 0,
                $"右 {chairR} 朝 {faceR} 帧 {frameR}；左 {chairL} 朝 {faceL} 帧 {frameL}");
            // The preview: the chair in hand over the floor shows the way it would go.
            int slot = 0;
            var savedSlot = inv.Slots[slot];
            int savedSelected = inv.Selected;
            inv.Slots[slot] = ItemStack.Of(ItemType.Chair, 5);
            inv.Selected = slot;
            mod.DevAim = Cell(fx0 + 7, fy + 1);
            Face(1);
            yield return null;
            yield return null;
            bool ghostR = PlacementGhost.DevShown && PlacementGhost.DevRight;
            Face(-1);
            yield return null;
            yield return null;
            bool ghostL = PlacementGhost.DevShown && !PlacementGhost.DevRight;
            mod.DevAim = null;
            inv.Slots[slot] = savedSlot;
            inv.Selected = savedSelected;
            FxCheck("25 放置预览也按主角朝向画", ghostR && ghostL, $"朝右时预览朝右 {ghostR}，朝左时朝左 {ghostL}");
            Put(Cell(fx0 + 26, fy + 1));
            yield return Land();
            Face(1);
            yield return null;
            bool bedR = Stations.TryPlace(StationKind.Bed, hero, Cell(fx0 + 26, fy + 1));
            string bedWhy = Stations.DevWhy;
            Face(-1);
            yield return null;
            bool relicL = Stations.TryPlace(StationKind.RelicFalseKnight, hero, Cell(fx0 + 21, fy + 1));
            string relicWhy = Stations.DevWhy;
            Face(1);
            yield return null;
            bool relicR = Stations.TryPlace(StationKind.RelicMantisLord, hero, Cell(fx0 + 31, fy + 1));
            relicWhy += " / " + Stations.DevWhy;
            yield return null;
            yield return null;
            int bedFrame = Stations.DevFrameXNear(Cell(fx0 + 26, fy + 1), StationKind.Bed);
            var relics = Object.FindObjectsByType<RelicDisplay>(FindObjectsSortMode.None);
            bool RelicTurned(StationKind k)
            {
                var box = Stations.DevBoxOf(k);
                if (!box.HasValue) return false;
                foreach (var r in relics) if (r != null && box.Value.Contains((Vector2)r.transform.position + new Vector2(0f, 0.1f))) return r.Facing;
                return false;
            }
            bool turnedR = RelicTurned(StationKind.RelicMantisLord), turnedL = RelicTurned(StationKind.RelicFalseKnight);
            FxCheck("25 床（右朝向 frameX 72）和圣物（朝右时雕像翻转）也按朝向放", bedR && bedFrame == 72 && relicL && relicR && turnedR && !turnedL,
                $"床 {bedR}（{bedWhy}）帧 {bedFrame}；圣物 左 {relicL} 翻转 {turnedL}，右 {relicR} 翻转 {turnedR}（{relicWhy}）");
            Shot("Town", "fx17_build_facing");
            yield return null;
            int profile = GameManager.instance.profileID;
            Save.ModSave.Write(profile, mod);
            Save.ModSave.Read(profile, mod);
            yield return new WaitForSeconds(1.5f);
            FxCheck("25 存档读回后朝向不变", Stations.DevFacingNear(Cell(fx0 + 6, fy + 1), StationKind.Chair) == 1 && Stations.DevFacingNear(Cell(fx0 + 2, fy + 1), StationKind.Chair) == -1
                && Stations.DevFrameXNear(Cell(fx0 + 26, fy + 1), StationKind.Bed) == 72 && Stations.DevFacingNear(Cell(fx0 + 31, fy + 1), StationKind.RelicMantisLord) == 1,
                $"椅子 {Stations.DevFacingNear(Cell(fx0 + 6, fy + 1), StationKind.Chair)}/{Stations.DevFacingNear(Cell(fx0 + 2, fy + 1), StationKind.Chair)}，床帧 {Stations.DevFrameXNear(Cell(fx0 + 26, fy + 1), StationKind.Bed)}，圣物 {Stations.DevFacingNear(Cell(fx0 + 31, fy + 1), StationKind.RelicMantisLord)}");
            Stations.DevRemoveNear(Cell(fx0 + 26, fy + 1), StationKind.Bed);
            Stations.DevRemoveNear(Cell(fx0 + 21, fy + 1), StationKind.RelicFalseKnight);
            Stations.DevRemoveNear(Cell(fx0 + 31, fy + 1), StationKind.RelicMantisLord);
            Stations.DevRemoveNear(Cell(fx0 + 2, fy + 1), StationKind.Chair);
            yield return null;

            // ---- 27. Drawn behind what Terraria draws over its tiles; a slime follows him past furniture.
            FxCheck("27 家具画在召唤物/弹幕、居民和主角后面，背景光和方块前面",
                Stations.DevZ > Effects.FxBatch.BehindZ && Stations.DevZ > Effects.FxBatch.TownZ && Stations.DevZ < World.Glow.Z && Stations.DevZ < CellVisuals.BlockZ,
                $"家具 z {Stations.DevZ:F5}，召唤物/弹幕 {Effects.FxBatch.BehindZ:F5}，居民 {Effects.FxBatch.TownZ:F5}，光 {World.Glow.Z:F5}，方块 {CellVisuals.BlockZ:F5}");
            Put(Cell(fx0 + 9, fy + 1));
            yield return Land();
            Face(1);
            bool rowBench = Stations.TryPlace(StationKind.WorkBench, hero, Cell(fx0 + 9, fy + 1));
            bool rowTable = Stations.TryPlace(StationKind.Table, hero, Cell(fx0 + 12, fy + 1));
            Plugin.Log.LogInfo($"[AutoTest] 修复1016 建造17 一排家具：椅子在 {fx0 + 6}，工作台 {rowBench}，桌子 {rowTable}（{Stations.DevWhy}）");
            Put(Cell(fx0 + 3, fy + 1));
            yield return Land();
            var minions = Minions.Instance;
            int staffSlot = 1;
            var savedStaff = inv.Slots[staffSlot];
            inv.Slots[staffSlot] = ItemStack.Of(ItemType.SlimeStaff);
            inv.Selected = staffSlot;
            mod.DevAim = Cell(fx0 + 4, fy + 2);
            mod.DevUseSelected(Cell(fx0 + 4, fy + 2));
            mod.DevAim = null;
            inv.Slots[staffSlot] = savedStaff;
            inv.Selected = savedSelected;
            yield return new WaitForSeconds(1f);
            Player.TerraMovement.DevMove = 1;
            float lastFurniture = (fx0 + 14) * c;
            bool slimeShot = false;
            for (float t = 0f; t < 4f && col.bounds.center.x < (fx0 + 22) * c; t += Time.deltaTime)
            {
                yield return null;
                var at = minions != null ? minions.DevWhere(Minions.Kind.BabySlime) : null;
                if (!slimeShot && at.HasValue && Stations.DevBoxNear(at.Value).Contains(at.Value)) { Shot("Town", "fx17_build_slime_front"); slimeShot = true; }
            }
            Player.TerraMovement.DevMove = 0;
            yield return new WaitForSeconds(3f);
            var slime = minions != null ? minions.DevWhere(Minions.Kind.BabySlime) : null;
            FxCheck("27 史莱姆宝宝跟着主角走过椅子、工作台、桌子", slime.HasValue && slime.Value.x > lastFurniture,
                $"史莱姆 {(slime.HasValue ? slime.Value.ToString("F2") : "没有")}，最后一件家具右边 {lastFurniture:F2}，{minions?.DevDescribe()}");
            Shot("Town", "fx17_build_slime");
            yield return null;
            Stations.DevRemoveNear(Cell(fx0 + 6, fy + 1), StationKind.Chair);
            Stations.DevRemoveNear(Cell(fx0 + 9, fy + 1), StationKind.WorkBench);
            Stations.DevRemoveNear(Cell(fx0 + 12, fy + 1), StationKind.Table);

            // ---- 27. The townsfolk on Dirtmouth's street: past a chair, a bench and a table on the floor; a door opened and shut.
            yield return StreetFurniture17(hero);

            foreach (var v in built) TerraWorld.Set(v.x, v.y, CellKind.Empty);
            for (int x = hx; x < hx + W; x++) for (int y = hy; y < hy + H; y++) TerraWorld.SetWall(x, y, 0);
            Stations.DevRemoveNear(Cell(hx, hy + 2), StationKind.Door);
            mod.DevSetSmartCursor(false);
            mod.DevNoFallDamage = false;
            inv.Infinite = infinite;
            Shop.DevStage = stageWas;
        }

        IEnumerator StreetFurniture17(HeroController hero)
        {
            const float c = CellGrid.CellSize;
            var street = TownNpcs.DevStreet;
            float benchX = street.x, benchY = street.y;
            string placed = TownNpcs.DevPlaceAt("Guide", new Vector2(benchX, benchY + 0.2f));
            yield return new WaitForSeconds(0.5f);
            if (!TownNpcs.DevFeet("Guide").HasValue)
            {
                FxCheck("27 街上有向导", false, placed);
                yield break;
            }
            // A flat run of the street right of the bench, clear of everything: chair, work bench, table, then a door.
            const int mask = 1 << ChunkColliders.TerrainLayer;
            float? Ground(float x)
            {
                var hit = Physics2D.Raycast(new Vector2(x, benchY + 1.5f), Vector2.down, 4f, mask);
                return hit.collider != null ? hit.point.y : (float?)null;
            }
            float start = 0f, floor = 0f;
            bool found = false;
            for (float x0 = benchX + 3f; x0 < benchX + 14f && !found; x0 += 0.5f)
            {
                var g0 = Ground(x0);
                if (!g0.HasValue) continue;
                bool flat = true;
                for (float x = x0; x <= x0 + 7f && flat; x += 0.25f)
                {
                    var g = Ground(x);
                    flat = g.HasValue && Mathf.Abs(g.Value - g0.Value) < 0.05f && !Stations.Overlaps(new Rect(x - 0.1f, g0.Value, 0.2f, 1.5f));
                }
                // Each piece would stand there (FindSpot as the world's own placing reads it).
                flat = flat && Stations.FindSpot(StationKind.Chair, null, new Vector2(x0 + 1f, g0.Value + 0.2f), out _)
                    && Stations.FindSpot(StationKind.WorkBench, null, new Vector2(x0 + 2.5f, g0.Value + 0.2f), out _)
                    && Stations.FindSpot(StationKind.Table, null, new Vector2(x0 + 4.25f, g0.Value + 0.2f), out _)
                    && Stations.FindSpot(StationKind.Bottle, null, new Vector2(x0 + 6.25f, g0.Value + 0.2f), out _)
                    && Stations.FindSpot(StationKind.Bottle, null, new Vector2(x0 + 6.75f, g0.Value + 0.2f), out _);
                if (flat) { start = x0; floor = g0.Value; found = true; }
            }
            if (!found)
            {
                FxCheck("27 德特茅斯街上找到一段平地", false, $"长椅 ({benchX:F1},{benchY:F1})");
                yield break;
            }
            float chairX = start + 1f, benchAt = start + 2.5f, tableAt = start + 4.25f, doorAt = start + 6.25f;
            // The chair set down by the Knight facing right, beside it.
            hero.transform.position = Clear(hero, new Vector2(chairX - 1f, floor + 0.1f));
            hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
            if (!hero.cState.facingRight) hero.FaceRight();
            yield return null;
            bool chair = Stations.TryPlace(StationKind.Chair, hero, new Vector2(chairX, floor + 0.2f));
            var chairBox = Stations.DevBoxOf(StationKind.Chair);
            var chairBottom = chairBox.HasValue ? new Vector2(chairBox.Value.center.x, chairBox.Value.yMin) : new Vector2(chairX, floor);
            bool wb = Stations.TryPlaceNatural(StationKind.WorkBench, new Vector2(benchAt, floor + 0.2f), out _);
            bool table = Stations.TryPlaceNatural(StationKind.Table, new Vector2(tableAt, floor + 0.2f), out _);
            var doorCell = CellGrid.WorldToCell(new Vector2(doorAt, floor + 0.2f));
            TerraWorld.Set(doorCell.x, doorCell.y + 3, CellKind.Wood);
            yield return null;
            bool door = Stations.TryPlaceNatural(StationKind.Door, CellGrid.CellCenter(doorCell.x, doorCell.y), out var doorBottom);
            string why = Stations.DevWhy;
            hero.transform.position = Clear(hero, new Vector2(start - 5f, floor + 0.1f));
            hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
            yield return new WaitForSeconds(1f);
            Plugin.Log.LogInfo($"[AutoTest] 修复1016 建造17 街道 长椅 ({benchX:F1},{benchY:F1})，平地 {start:F2} 地面 {floor:F2}；椅子 {chair} {chairBottom}，工作台 {wb}，桌子 {table}，门 {door} {doorBottom}（{why}）；{placed}");
            TownNpcs.DevSend("Guide", start - 0.5f, 1);
            float worst = 0f;
            bool overChair = false, shot = false;
            float t0 = Time.time;
            var feet = TownNpcs.DevFeet("Guide");
            while (Time.time - t0 < 8f && feet.HasValue && feet.Value.x < tableAt + 1.2f)
            {
                yield return null;
                feet = TownNpcs.DevFeet("Guide");
                if (!feet.HasValue) break;
                if (Mathf.Abs(feet.Value.x - chairBottom.x) < 0.25f)
                {
                    overChair = true;
                    worst = Mathf.Max(worst, Mathf.Abs(feet.Value.y - floor));
                    if (!shot) { Shot("Town", "fx17_build_npc_chair"); shot = true; }
                }
            }
            FxCheck("27 居民走过椅子、工作台、桌子：在地面上走过去，不被挡也不踩上椅子", chair && wb && table && overChair && worst < 0.06f && feet.HasValue && feet.Value.x >= tableAt + 1.2f,
                $"椅子 {chair} 工作台 {wb} 桌子 {table}；走到椅子上方 {overChair}，那时脚离地最多 {worst:F2}；现在脚 {(feet.HasValue ? feet.Value.ToString("F2") : "无")}（要过 {tableAt + 1.2f:F2}），{Time.time - t0:F1} 秒");
            int open0 = Stations.DevNpcDoorsOpened, shut0 = Stations.DevNpcDoorsShut;
            TownNpcs.DevSend("Guide", doorAt - 1.2f, 1);
            var seen = new HashSet<StationKind>();
            t0 = Time.time;
            bool doorShot = false;
            feet = TownNpcs.DevFeet("Guide");
            while (Time.time - t0 < 8f && feet.HasValue && feet.Value.x < doorAt + 1.6f)
            {
                yield return null;
                feet = TownNpcs.DevFeet("Guide");
                var k = Stations.DevDoorNear(new Vector2(doorAt, floor + 0.7f), out _);
                seen.Add(k);
                if (!doorShot && k != StationKind.Door && feet.HasValue && Mathf.Abs(feet.Value.x - doorAt) < 0.3f) { Shot("Town", "fx17_build_npc_door"); doorShot = true; }
            }
            for (float t = 0f; t < 2f && Stations.DevNpcDoorsShut == shut0; t += Time.deltaTime) yield return null;
            var now = Stations.DevDoorNear(new Vector2(doorAt, floor + 0.7f), out _);
            FxCheck("27 居民走到关着的门前：推开走过去，走出两格后把门关上", door && Stations.DevNpcDoorsOpened > open0 && seen.Contains(StationKind.DoorOpenRight)
                && feet.HasValue && feet.Value.x >= doorAt + 1.6f && Stations.DevNpcDoorsShut > shut0 && now == StationKind.Door,
                $"门 {door}（{why}），开 {Stations.DevNpcDoorsOpened - open0} 关 {Stations.DevNpcDoorsShut - shut0}，见到 {string.Join("/", seen)}，现在 {now}，脚 {(feet.HasValue ? feet.Value.ToString("F2") : "无")}");
            // Put on the chair (the townsfolk panel's click on it), at rest it sits facing the way the chair faces.
            string onChair = TownNpcs.DevPlaceAt("Guide", chairBottom + new Vector2(0f, 0.6f));
            TownNpcs.DevSend("Guide", chairBottom.x, 0);
            yield return new WaitForSeconds(0.6f);
            var seated = TownNpcs.DevFeet("Guide");
            int seatedFace = TownNpcs.DevFacing("Guide");
            Shot("Town", "fx17_build_npc_seated");
            yield return null;
            FxCheck("25 居民放到朝右的椅子上：坐在椅面上，朝椅子的方向", seated.HasValue && Mathf.Abs(seated.Value.y - (chairBottom.y + c)) < 0.06f && seatedFace == 1
                && Stations.DevFacingNear(chairBottom, StationKind.Chair) == 1,
                $"{onChair}；脚 {(seated.HasValue ? seated.Value.ToString("F2") : "无")}（椅面 {chairBottom.y + c:F2}），朝 {seatedFace}，椅子朝 {Stations.DevFacingNear(chairBottom, StationKind.Chair)}");
            Stations.DevRemoveNear(chairBottom, StationKind.Chair);
            Stations.DevRemoveNear(new Vector2(benchAt, floor), StationKind.WorkBench);
            Stations.DevRemoveNear(new Vector2(tableAt, floor), StationKind.Table);
            Stations.DevRemoveNear(new Vector2(doorAt, floor), StationKind.Door);
            TerraWorld.Set(doorCell.x, doorCell.y + 3, CellKind.Empty);
        }
    }
}
