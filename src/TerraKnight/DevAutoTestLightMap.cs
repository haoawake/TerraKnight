using System.Collections;
using UnityEngine;
using TerraKnight.Items;
using TerraKnight.World;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "lightmap"): Terraria's lighting (World.LightMap). On Dirtmouth's street: the
    /// open room as the game paints it; a cave dug under its floor, dark; torches on its floor, their
    /// light falling off tile by tile (×0.91) and soon gone in the rock; a shaft opened to the room,
    /// the room's light coming down it; an Amethyst bolt lighting the cave as it goes; then the room
    /// made dark (the game's darkness 2): the Knight's own little light, the mining helmet's, a torch
    /// in hand. Each step's light values logged and the view photographed.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator LightMapTest()
        {
            PlayerData.instance.isInvincible = true;
            mod.DevNoFallDamage = true;
            // Dirtmouth's street: open and lit, deep ground under it (the room's bottom 10 units down).
            const string room = "Town";
            yield return GoTo(room, "left1");
            yield return new WaitForSeconds(1.5f);
            var hero = HeroController.instance;
            var col = hero.GetComponent<BoxCollider2D>();
            var rb = hero.GetComponent<Rigidbody2D>();
            hero.transform.position = Clear(hero, new Vector2(112f, 11.5f));
            rb.linearVelocity = Vector2.zero;
            yield return new WaitForSeconds(1f);
            // Entering a scene puts the game's damage mode back (HeroController.SetDamageMode clears isInvincible).
            PlayerData.instance.isInvincible = true;
            var inv = mod.Inventory;
            inv.Infinite = true;
            int ok = 0, all = 0;
            void Check(bool pass, string what)
            {
                all++;
                if (pass) ok++;
                Plugin.Log.LogInfo($"[AutoTest] 光照 {what} {(pass ? "通过" : "不通过")}");
            }
            float L(Vector2 p) => LightMap.DevLightAt(p);
            Plugin.Log.LogInfo($"[AutoTest] 光照 材质 {LightMap.DevShader}，开着 {LightMap.Enabled}，暗度 {LightMap.Darkness}");

            // The open room: as the game paints it.
            var surface = (Vector2)col.bounds.center;
            Shot(room, "lightmap_0_room");
            yield return null;
            Check(LightMap.Active && L(surface) > 0.97f, $"房间里：主角处 {L(surface):F2}，盖暗层 {LightMap.DevOverlay}，算一帧 {LightMap.DevMs:F2} 毫秒（{LightMap.DevCells} 格）");

            // A closed cave under the floor: dark.
            var top = CellGrid.WorldToCell(new Vector2(col.bounds.center.x, col.bounds.min.y - 0.1f));
            for (int y = top.y - 14; y <= top.y - 4; y++)
                for (int x = top.x - 22; x <= top.x + 22; x++)
                    TerraWorld.Set(x, y, CellKind.Empty);
            hero.transform.position = Clear(hero, CellGrid.CellCenter(top.x, top.y - 13));
            rb.linearVelocity = Vector2.zero;
            yield return new WaitForSeconds(1f);
            var cave = (Vector2)col.bounds.center;
            float caveDark = L(cave), caveFar = L(CellGrid.CellCenter(top.x + 18, top.y - 9));
            Shot(room, "lightmap_1_cave");
            yield return null;
            Check(LightMap.DevOverlay && caveDark < 0.15f && caveFar < 0.15f, $"封闭的洞：主角处 {caveDark:F2}，洞的另一头 {caveFar:F2}");

            // Torches on its floor, 12 tiles apart: soft light falling off by 0.91 a tile, quickly gone in the rock.
            var feet = CellGrid.WorldToCell(new Vector2(col.bounds.center.x, col.bounds.min.y + 0.1f));
            int floorY = top.y - 14;
            var torches = new System.Collections.Generic.List<Vector2Int>();
            foreach (int dx in new[] { -18, -6, 6, 18 })
            {
                var c = new Vector2Int(feet.x + dx, floorY);
                if (TerraWorld.Get(c.x, c.y) != CellKind.Empty || !TerraWorld.IsSolid(c.x, c.y - 1)) continue;
                TerraWorld.Set(c.x, c.y, CellKind.Torch);
                torches.Add(c);
            }
            yield return new WaitForSeconds(0.6f);
            Shot(room, "lightmap_2_torches");
            yield return null;
            if (torches.Count > 0)
            {
                var t = torches[torches.Count / 2];
                var row = new System.Text.StringBuilder();
                float prev = 2f;
                bool falls = true;
                for (int d = 0; d <= 6; d++)
                {
                    float v = L(CellGrid.CellCenter(t.x + d, t.y + 1));
                    row.Append($"{v:F2} ");
                    if (v > prev + 0.01f) falls = false;
                    prev = v;
                }
                float at = L(CellGrid.CellCenter(t.x, t.y)), six = L(CellGrid.CellCenter(t.x + 6, t.y + 1));
                // A 5×5 mound of dirt put 3 tiles from it: its face lit almost as the air, its middle much less.
                var mound = new System.Collections.Generic.List<Vector2Int>();
                for (int y = t.y; y <= t.y + 4; y++)
                    for (int x = t.x + 3; x <= t.x + 7; x++)
                        if (TerraWorld.Get(x, y) == CellKind.Empty) { TerraWorld.Set(x, y, CellKind.Dirt); mound.Add(new Vector2Int(x, y)); }
                yield return new WaitForSeconds(0.3f);
                float face = L(CellGrid.CellCenter(t.x + 3, t.y + 2)), middle = L(CellGrid.CellCenter(t.x + 5, t.y + 2));
                Shot(room, "lightmap_2b_mound");
                yield return null;
                foreach (var m in mound) TerraWorld.Set(m.x, m.y, CellKind.Empty);
                Check(at > 0.9f && falls && six > 0.4f && six < 0.75f && middle < face * 0.5f,
                    $"火把 {torches.Count} 支：火把处 {at:F2}，往右每格 {row}；旁边放的土堆（{mound.Count} 格）朝火把的一面 {face:F2}，中间 {middle:F2}；挂着的光晕贴图 {LightMap.DevSpritesShown} 个");
            }
            else Check(false, "洞底放不下火把");

            // A shaft opened from the cave's left end up into the room: its light coming down.
            for (int y = top.y - 4; y <= top.y + 1; y++)
                for (int x = top.x - 22; x <= top.x - 19; x++)
                    TerraWorld.Set(x, y, CellKind.Empty);
            foreach (var t in torches) TerraWorld.Set(t.x, t.y, CellKind.Empty);
            yield return new WaitForSeconds(0.6f);
            float under = L(CellGrid.CellCenter(top.x - 20, top.y - 13)), beside = L(CellGrid.CellCenter(top.x - 10, top.y - 13));
            Player.TerraCamera.DevLook = CellGrid.CellCenter(top.x - 18, top.y - 8);
            yield return new WaitForSeconds(0.4f);
            Shot(room, "lightmap_3_shaft");
            yield return null;
            Player.TerraCamera.DevLook = null;
            Check(under > 0.25f && beside < under && beside > 0.02f, $"打通到房间的竖井：井底 {under:F2}，往里 10 格 {beside:F2}");

            // An Amethyst bolt along the dark cave: lights it as it goes.
            inv.Slots[0] = ItemStack.Of(ItemType.AmethystStaff);
            inv.Selected = 0;
            hero.transform.position = Clear(hero, CellGrid.CellCenter(top.x + 4, top.y - 13));
            yield return new WaitForSeconds(0.5f);
            float before = 0f, during = 0f;
            int shown = 0;
            for (int x = top.x + 10; x <= top.x + 21; x++) before = Mathf.Max(before, L(CellGrid.CellCenter(x, top.y - 12)));
            bool shot = false;
            for (float t = 0f; t < 0.9f; t += Time.deltaTime)
            {
                var aim = CellGrid.CellCenter(top.x + 22, top.y - 12);
                mod.DevAim = aim;
                if (t < 0.1f && mod.DevReady && !mod.IsUsingItem) mod.DevUseSelected(aim);
                yield return null;
                for (int x = top.x + 10; x <= top.x + 21; x++) during = Mathf.Max(during, L(CellGrid.CellCenter(x, top.y - 12)));
                shown = Mathf.Max(shown, LightMap.DevSpritesShown);
                if (!shot && during > before + 0.3f) { shot = true; Shot(room, "lightmap_4_bolt"); }
            }
            mod.DevAim = null;
            Check(during > 0.5f && before < 0.2f, $"紫晶法杖的弹：之前那段洞 {before:F2}，飞过时最亮 {during:F2}");

            // The room made dark (the game's darkness 2, as Deepnest without the lantern): little light but the Knight's own.
            hero.transform.position = Clear(hero, surface);
            rb.linearVelocity = Vector2.zero;
            yield return new WaitForSeconds(0.6f);
            int sceneDark = GameManager.instance.sm != null ? GameManager.instance.sm.darknessLevel : 0;
            bool lantern = PlayerData.instance.hasLantern;
            PlayerData.instance.hasLantern = false;
            hero.SetDarkness(2);
            yield return new WaitForSeconds(0.5f);
            var head = (Vector2)col.bounds.center;
            float darkHere = L(head), darkFar = L(head + new Vector2(8f, 0f));
            Shot(room, "lightmap_5_dark");
            yield return null;
            Check(LightMap.DevOverlay && darkHere > 0.3f && darkHere < 0.6f && darkFar < 0.2f, $"黑暗房间：主角处 {darkHere:F2}，8 格外 {darkFar:F2}");

            var hat = inv.Armor[0];
            inv.Armor[0] = ItemStack.Of(ItemType.MiningHelmet);
            yield return new WaitForSeconds(0.5f);
            head = col.bounds.center;
            float helmetHere = L(new Vector2(head.x, col.bounds.max.y - 0.1f)), helmetFar = L(head + new Vector2(4f, 0f));
            Shot(room, "lightmap_6_helmet");
            yield return null;
            Check(helmetHere > 0.85f && helmetFar > darkFar + 0.15f, $"戴挖矿头盔：头上 {helmetHere:F2}，4 格（8 块）外 {helmetFar:F2}；挂着的光晕贴图 {LightMap.DevSpritesShown} 个");
            inv.Armor[0] = hat;

            inv.Slots[1] = ItemStack.Of(ItemType.Torch, 99);
            inv.Selected = 1;
            yield return new WaitForSeconds(0.5f);
            head = col.bounds.center;
            float torchHere = Mathf.Max(L(head + new Vector2(0.5f, 0.3f)), L(head + new Vector2(-0.5f, 0.3f)));
            Shot(room, "lightmap_7_torch");
            yield return null;
            Check(torchHere > 0.85f, $"手持火把：火苗处 {torchHere:F2}；算一帧 {LightMap.DevMs:F2} 毫秒（{LightMap.DevCells} 格，{LightMap.DevSources} 个光源）");
            inv.Selected = 0;

            hero.SetDarkness(sceneDark);
            PlayerData.instance.hasLantern = lantern;
            yield return new WaitForSeconds(0.4f);
            Check(L((Vector2)col.bounds.center) > 0.97f, $"暗度改回 {sceneDark}：主角处 {L((Vector2)col.bounds.center):F2}");
            Plugin.Log.LogInfo($"[AutoTest] 光照 合计 {ok}/{all}");
            mod.DevNoFallDamage = false;
        }
    }
}
