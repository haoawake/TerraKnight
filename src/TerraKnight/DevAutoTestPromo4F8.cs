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
    /// Dev (suite "p4_v5F_deco_zh"): recorder F's furnishing takes. Two empty halls on Dirtmouth's street (their frames and
    /// back walls set up outside the take), each furnished piece by piece from one area's set (Crystal Peak's, the Queen's
    /// Gardens'): piano, bookcase, clock, sofa, bathtub, lamp, chandelier; then a row of Terraria's animated stations with
    /// torches between them, walked past; and area chests of two areas opened.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator P4_v5F_deco(HeroController hero)
        {
            F5Safe();
            StoryBeats.SetOn(false);
            yield return P4Kit();
            F5Safe();
            mod.TrySwitchLoadout(0);
            mod.DevSetSmartCursor(false);
            yield return F5TownReady();
            yield return F5Hall("v5F_furniture_crystal", 10, 64f);
            yield return F5Hall("v5F_furniture_gardens", 7, 78f);
            yield return F5StationRow("v5F_tile_anim", "Crossroads_04");
            F5CamOff();
            Plugin.Log.LogInfo("[AutoTest] 录制F 储物箱计划：" + RegionalChests.DevDescribe());
            yield return C4ChestClip("v5F_chest_fungal", "真菌荒地", false);
            yield return C4ChestClip("v5F_chest_edge", "王国边缘", false);
            yield return C4ChestClip("v5F_chest_cliffs", "呼啸悬崖", false);
            yield return C4ChestClip("v5F_chest_resting", "安息之地", false);
        }

        /// <summary>
        /// A hall 22 cells wide and 10 high of the area's blocks and back wall, its floor the street from x (world), then
        /// furnished from the area's set: each piece set down where the hero can reach, walking left to right.
        /// </summary>
        IEnumerator F5Hall(string clip, int area, float atX)
        {
            var hero = HeroController.instance;
            var inv = mod.Inventory;
            var st = TownNpcs.DevStreet;
            int x0 = CellGrid.WorldToCell(new Vector2(atX, st.y)).x;
            // Clear of the area chest and anything set down on the street.
            for (int guard = 0; guard < 80; guard++)
            {
                bool clear = true;
                for (int x = x0 - 1; x <= x0 + 22 && clear; x++)
                    for (int y = CellGrid.WorldToCell(new Vector2(0f, st.y)).y - 1; y <= CellGrid.WorldToCell(new Vector2(0f, st.y)).y + 12 && clear; y++)
                        if (Stations.Covers(new Vector2Int(x, y)) || Chests.Covers(new Vector2Int(x, y), orUnder: true)) clear = false;
                if (clear) break;
                x0 += 2;
            }
            int x1 = x0 + 21;
            int g = F5GroundRow(CellGrid.CellCenter(x0 + 10, 0).x, st.y + 1f);
            F5Shoo(atX - 6f, atX + 30f, 1, 14f);
            var block = AreaBlocks.Kind(area);
            int wall = AreaBlocks.WallId(area);
            for (int x = x0; x <= x1; x++)
                for (int y = g + 1; y <= g + 7; y++)
                {
                    bool frame = x == x0 || x == x1 || y == g + 7;
                    // A doorway in the left wall.
                    if (x == x0 && y <= g + 3) frame = false;
                    TerraWorld.Set(x, y, frame ? block : CellKind.Empty);
                    TerraWorld.SetWall(x, y, x > x0 && x < x1 && y < g + 7 ? wall : 0);
                }
            // The pieces: (piece index, cell x the hero aims at, cell y, where the hero stands).
            var plan = new (int Piece, int X, int Y, int Stand)[]
            {
                (13, x0 + 3, g + 1, x0 + 7),   // piano
                (7, x0 + 7, g + 1, x0 + 10),   // bookcase
                (12, x0 + 10, g + 1, x0 + 12), // clock
                (8, x0 + 13, g + 1, x0 + 16),  // sofa
                (16, x0 + 13, g + 6, x0 + 12), // chandelier
                (9, x0 + 17, g + 1, x0 + 16),  // bathtub
                (18, x0 + 20, g + 1, x0 + 17), // lamp
            };
            var saved = F5Hotbar(plan.Select(p => RegionalFurniture.Item(area, p.Piece)).ToArray());
            int accSlot = Inventory.AccessorySlots - 1;
            var savedAcc = inv.Accessories[accSlot];
            inv.Accessories[accSlot] = ItemStack.Of(ItemType.ArchitectGizmoPack, 1);
            inv.RefreshEffective();
            Equipment.Update(inv);
            var col = hero.GetComponent<BoxCollider2D>();
            C4PutOn(hero, new Vector2Int(x0 - 3, g + 1));
            C4Face(hero, 1);
            F5CamOn(Vector2.zero);
            f5CamFix = new Vector2(CellGrid.CellCenter(x0 + 10, 0).x, CellGrid.CellCenter(0, g).y + 3.8f);
            var bootsSaved = F5NoBoots();
            yield return new WaitForSeconds(0.8f);
            F5CamSnap();
            aimInit = false;
            SmoothAim(CellGrid.CellCenter(x0 + 3, g + 2), 99f);
            int placed0 = Stations.DevPlaced, placed = 0;
            StartRec(clip);
            yield return new WaitForSeconds(0.3f);
            for (int k = 0; k < plan.Length; k++)
            {
                var p = plan[k];
                var aim = CellGrid.CellCenter(p.X, p.Y) + new Vector2(0f, p.Piece == 16 ? 0f : 0.1f);
                float standX = CellGrid.CellCenter(p.Stand, 0).x;
                if (Mathf.Abs(col.bounds.center.x - standX) > 0.4f) yield return F5WalkTo(hero, standX, 2f, () => aim);
                C4Face(hero, aim.x >= col.bounds.center.x ? 1 : -1);
                inv.Selected = k;
                yield return new WaitForSeconds(0.12f);
                int before = Stations.DevPlaced;
                yield return C4PlaceAt(aim, 0.25f, 10f);
                if (Stations.DevPlaced > before) placed++;
                else F5Log($"{clip}：{RegionalFurniture.Pieces[p.Piece].Name} 没放下（{Stations.DevWhy}）");
                yield return new WaitForSeconds(0.25f);
            }
            inv.Selected = 0;
            // A step back to look the room over.
            yield return F5WalkTo(hero, CellGrid.CellCenter(x0 + 19, 0).x, 1.5f, () => CellGrid.CellCenter(x0 + 11, g + 4));
            for (float t = 0f; t < 1.4f; t += Time.deltaTime) { SmoothAim(CellGrid.CellCenter(x0 + 11, g + 5), 2f); yield return null; }
            StopRec();
            F5Accessories(bootsSaved);
            F5Log($"{clip}：{AreaBlocks.All[area].Name} 家具放下 {placed}/{plan.Length}（x {CellGrid.CellCenter(x0, 0).x:F1}）");
            for (int i = 0; i < Inventory.HotbarSize; i++) inv.Slots[i] = saved[i];
            inv.Accessories[accSlot] = savedAcc;
            inv.RefreshEffective();
            Equipment.Update(inv);
            mod.DevAim = null;
        }

        /// <summary>Terraria's animated stations set in a row on the street (outside the take) with torches between, walked past.</summary>
        IEnumerator F5StationRow(string clip, string scene)
        {
            yield return EnterRoom(scene);
            float entered = Time.time;
            var hero = HeroController.instance;
            F5Safe();
            QuietAround(hero.transform.position, 400f);
            int run = F5Floor(out var start);
            if (run < 30) { F5Log($"{clip}：{scene} 平地只有 {run} 格"); yield break; }
            var st = new Vector4(0f, CellGrid.CellCenter(start.x, start.y).y - 0.25f, 0f, 0f);
            float atX = CellGrid.CellCenter(start.x + 6, 0).x;
            var kinds = new[] { StationKind.Campfire, StationKind.Furnace, StationKind.Hellforge, StationKind.Sawmill, StationKind.GlassKiln, StationKind.AdamantiteForge,
                StationKind.LihzahrdFurnace, StationKind.SkyMill, StationKind.SteampunkBoiler, StationKind.BoneWelder, StationKind.AncientManipulator, StationKind.VoidVault, StationKind.Campfire };
            float x = atX;
            int set = 0;
            var torches = new List<Vector2Int>();
            foreach (var k in kinds)
            {
                bool ok = false;
                for (float d = 0f; d < 2f && !ok; d += 0.25f)
                    if (Stations.TryPlaceNatural(k, new Vector2(x + d, st.y + 0.3f), out _)) ok = true;
                var box = Stations.DevBoxOf(k);
                if (ok) set++;
                float right = ok && box.HasValue ? Mathf.Max(box.Value.xMax, x) : x + 1f;
                // A torch on the floor beside each.
                int g = F5GroundRow(right + 0.5f, st.y + 1f);
                var c = CellGrid.WorldToCell(new Vector2(right + 0.5f, CellGrid.CellCenter(0, g + 1).y));
                if (TerraWorld.Get(c.x, g + 1) == CellKind.Empty && !Stations.Covers(new Vector2Int(c.x, g + 1)))
                {
                    TerraWorld.Set(c.x, g + 1, CellKind.Torch);
                    RegionalTorches.Placed(new Vector2Int(c.x, g + 1), ItemType.Torch);
                    torches.Add(new Vector2Int(c.x, g + 1));
                }
                x = right + 1.25f;
            }
            var col = hero.GetComponent<BoxCollider2D>();
            var inv = mod.Inventory;
            var accSaved = (ItemStack[])inv.Accessories.Clone();
            for (int i = 0; i < inv.Accessories.Length; i++)
                if (!inv.Accessories[i].IsEmpty && (inv.Accessories[i].Type == ItemType.TerrasparkBoots || inv.Accessories[i].Def.Name.Contains("靴"))) inv.Accessories[i] = default;
            inv.RefreshEffective();
            Equipment.Update(inv);
            C4PutOn(hero, new Vector2Int(start.x + 1, start.y));
            C4Face(hero, 1);
            F5CamOn(new Vector2(2.5f, 2.2f), 4f);
            while (Time.time - entered < 9.5f) { QuietAround(hero.transform.position, 400f); yield return null; }
            F5ClearChat();
            F5CamSnap();
            aimInit = false;
            SmoothAim((Vector2)col.bounds.center + new Vector2(3f, 0.5f), 99f);
            StartRec(clip);
            yield return new WaitForSeconds(0.5f);
            // Along them, looking: a few steps, the cursor on the next station a moment, on again.
            float end = x - 2f;
            var boxes = new List<Rect>();
            foreach (var k in kinds) { var bb = Stations.DevBoxOf(k); if (bb.HasValue && !boxes.Contains(bb.Value)) boxes.Add(bb.Value); }
            boxes.Sort((p, q) => p.x.CompareTo(q.x));
            int next = 0;
            for (float t = 0f; t < 16f && col.bounds.center.x < end; )
            {
                float goal = Mathf.Min(end, col.bounds.center.x + 3.2f);
                while (next < boxes.Count && boxes[next].center.x < col.bounds.center.x + 1f) next++;
                Vector2 look = next < boxes.Count ? boxes[next].center + new Vector2(0f, 0.3f) : (Vector2)col.bounds.center + new Vector2(3f, 0.4f);
                float t0 = Time.time;
                yield return F5WalkTo(hero, goal, 1.6f, () => look);
                for (float u = 0f; u < 0.55f; u += Time.deltaTime) { SmoothAim(look, 6f); yield return null; }
                t += Time.time - t0 + 0.55f;
            }
            Player.TerraMovement.DevMove = 0;
            yield return new WaitForSeconds(0.8f);
            StopRec();
            for (int i = 0; i < accSaved.Length; i++) inv.Accessories[i] = accSaved[i];
            inv.RefreshEffective();
            Equipment.Update(inv);
            F5Log($"{clip}：工作站 {set}/{kinds.Length}，火把 {torches.Count}，到 {x:F1}");
        }
    }
}
