using System.Collections;
using TerraKnight.Items;
using TerraKnight.Player;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    internal sealed partial class DevAutoTest
    {
        IEnumerator RopeTest(string room, HeroController hero)
        {
            void Check(string label, bool ok, string detail) => Plugin.Log.LogInfo($"[AutoTest] 绳子 {label}：{detail} {(ok ? "通过" : "不通过")}");
            var inv = mod.Inventory;
            inv.Infinite = false;
            InventoryGui.Open = false;
            HarmonyLib.AccessTools.Property(typeof(TerraMod), nameof(TerraMod.SmartCursor)).SetValue(mod, false);
            mod.DevNoFallDamage = true;
            var rb = hero.GetComponent<Rigidbody2D>();
            var hc = hero.GetComponent<BoxCollider2D>();
            var cell = CellGrid.WorldToCell(hc.bounds.center);
            int x = cell.x + 6, floor = Mathf.FloorToInt(hc.bounds.min.y / CellGrid.CellSize) - 1;
            int bottom = floor + 1, top = bottom + 16;
            for (int cy = floor; cy <= top + 3; cy++)
                for (int cx = x - 4; cx <= x + 4; cx++) TerraWorld.Set(cx, cy, cy == floor ? CellKind.Stone : CellKind.Empty);
            TerraWorld.Set(x, top, CellKind.Stone);
            foreach (var d in Object.FindObjectsByType<DamageHero>(FindObjectsSortMode.None))
                if (Vector2.Distance(d.transform.position, CellGrid.CellCenter(x, bottom + 8)) < 20f) Object.Destroy(d);
            yield return null;
            inv.Slots[0] = ItemStack.Of(ItemType.Rope, 50);
            inv.Selected = 0;
            var constraints = rb.constraints;
            rb.constraints = RigidbodyConstraints2D.FreezeAll;
            void PutCenter(Vector2 center)
            {
                hero.transform.position += (Vector3)(center - (Vector2)hc.bounds.center);
                rb.position = hero.transform.position;
                rb.linearVelocity = Vector2.zero;
                Physics2D.SyncTransforms();
            }
            IEnumerator PlaceAt(int cx, int cy)
            {
                Vector2 aim = CellGrid.CellCenter(cx, cy);
                PutCenter(aim + Vector2.left * 1.5f);
                yield return null;
                mod.DevUseSelected(aim, target: true);
                yield return new WaitForSeconds(0.15f);
            }
            try
            {
                int before = inv.Slots[0].Count;
                yield return PlaceAt(x + 3, bottom + 7);
                Check("空中无支撑拒绝放置", TerraWorld.Get(x + 3, bottom + 7) == CellKind.Empty && inv.Slots[0].Count == before, $"格子={TerraWorld.Get(x + 3, bottom + 7)} 数量={inv.Slots[0].Count}/{before}");
                yield return PlaceAt(x, top - 1);
                Check("挂在方块下方", TerraWorld.Get(x, top - 1) == CellKind.Rope && inv.Slots[0].Count == before - 1, $"格子={TerraWorld.Get(x, top - 1)} 数量={inv.Slots[0].Count}");
                for (int cy = top - 2; cy >= bottom; cy--) yield return PlaceAt(x, cy);
                int ropeCount = 0;
                for (int cy = bottom; cy < top; cy++) if (TerraWorld.Get(x, cy) == CellKind.Rope) ropeCount++;
                Check("逐节向下接长和消耗", ropeCount == 16 && inv.Slots[0].Count == before - 16, $"连续={ropeCount}/16 库存={inv.Slots[0].Count}/{before - 16}");
                // Remove the support and the top segment, then replace that segment above the next rope.
                TerraWorld.Set(x, top, CellKind.Empty);
                TerraWorld.Set(x, top - 1, CellKind.Empty);
                yield return PlaceAt(x, top - 1);
                Check("沿已有绳子向上接长", TerraWorld.Get(x, top - 1) == CellKind.Rope && inv.Slots[0].Count == before - 17, $"格子={TerraWorld.Get(x, top - 1)} 库存={inv.Slots[0].Count}");
                int aboveBefore = inv.Slots[0].Count;
                for (int cy = bottom; cy < bottom + 4; cy++) yield return PlaceAt(x + 3, cy);
                bool above = true;
                for (int cy = bottom; cy < bottom + 4; cy++) above &= TerraWorld.Get(x + 3, cy) == CellKind.Rope;
                Check("物块上方起放并连续向上接", above && inv.Slots[0].Count == aboveBefore - 4, $"4节={above} 消耗={aboveBefore - inv.Slots[0].Count}");
                // Mine a real Original cell through the pickaxe use path; it leaves a background wall.
                int wallX = x + 3, wallY = bottom + 7;
                TerraWorld.Set(wallX, wallY, CellKind.Original);
                inv.Slots[1] = ItemStack.Of(ItemType.CopperPickaxe);
                inv.Selected = 1;
                Vector2 wallAim = CellGrid.CellCenter(wallX, wallY);
                PutCenter(wallAim + Vector2.left * 1.5f);
                for (int i = 0; i < 12 && TerraWorld.Get(wallX, wallY) != CellKind.Empty; i++)
                {
                    mod.DevUseSelected(wallAim, target: true);
                    yield return new WaitForSeconds(0.15f);
                }
                bool dug = TerraWorld.IsDug(wallX, wallY) && TerraWorld.Get(wallX, wallY) == CellKind.Empty;
                inv.Selected = 0;
                int wallBefore = inv.Slots[0].Count;
                yield return PlaceAt(wallX, wallY);
                Check("挖掘背景墙内无邻接物块放置", dug && TerraWorld.Get(wallX, wallY) == CellKind.Rope && inv.Slots[0].Count == wallBefore - 1, $"挖出背景={dug} 格子={TerraWorld.Get(wallX, wallY)} 消耗={wallBefore - inv.Slots[0].Count}");
                PutCenter(CellGrid.CellCenter(x, bottom + 5));
            }
            finally { rb.constraints = constraints; }
            yield return null;
            IEnumerator HoldKey(TerraKeys.Action action, float seconds)
            {
                float end = Time.time + seconds;
                while (Time.time < end) { KeysDown(TerraKeys.Get(action)); yield return null; }
                KeysDown();
            }
            float y0 = hero.transform.position.y;
            yield return HoldKey(TerraKeys.Action.Up, 0.45f);
            float climbed = hero.transform.position.y - y0;
            Check("按上键攀爬", TerraMovement.OnRope && climbed > 1f, $"OnRope={TerraMovement.OnRope} 上升={climbed:F2}");
            Shot(room, "rope_1_climb");
            KeysDown();
            yield return new WaitForSeconds(0.2f);
            y0 = hero.transform.position.y;
            yield return new WaitForSeconds(0.5f);
            float drift = Mathf.Abs(hero.transform.position.y - y0);
            Check("松键悬停", TerraMovement.OnRope && drift < 0.08f, $"OnRope={TerraMovement.OnRope} 漂移={drift:F3}");
            y0 = hero.transform.position.y;
            yield return HoldKey(TerraKeys.Action.Down, 0.6f);
            float descended = y0 - hero.transform.position.y;
            Check("按下键攀爬", TerraMovement.OnRope && descended > 1f, $"OnRope={TerraMovement.OnRope} 下降={descended:F2}");
            KeysDown();
            yield return null;
            var jumpInput = InputHandler.Instance.inputActions.jump;
            Plugin.Log.LogInfo($"[AutoTest] 绳子 跳前诊断：输入启用={jumpInput.Enabled} heroState={HarmonyLib.AccessTools.Field(typeof(HeroController), "hero_state").GetValue(hero)} 地面={hero.cState.onGround} ledge={HarmonyLib.AccessTools.Field(typeof(HeroController), "ledgeBufferSteps").GetValue(hero)} head={HarmonyLib.AccessTools.Field(typeof(HeroController), "headBumpSteps").GetValue(hero)}");
            yield return HoldKey(TerraKeys.Action.Jump, 0.15f);
            Check("跳离绳子", !TerraMovement.OnRope && rb.linearVelocity.y > 0f, $"OnRope={TerraMovement.OnRope} 速度={rb.linearVelocity:F2}");
            KeysDown();
            // Under a platform (the common anchor: a platform is solid), and a rope above it clicked goes on at the bottom of the
            // line under it (PlaceThing_Tiles_CheckRopeUsability, through up to five platforms).
            int px = x - 3, plat = bottom + 10;
            TerraWorld.Set(px, plat, CellKind.Platform);
            int platBefore = inv.Slots[0].Count;
            yield return PlaceAt(px, plat - 1);
            Check("挂在平台下方", TerraWorld.Get(px, plat - 1) == CellKind.Rope && inv.Slots[0].Count == platBefore - 1, $"格子={TerraWorld.Get(px, plat - 1)} 数量={inv.Slots[0].Count}/{platBefore - 1}");
            yield return PlaceAt(px, plat + 1);
            yield return PlaceAt(px, plat + 1);
            Check("点平台上方的绳子 隔着平台接到下面那条的末端", TerraWorld.Get(px, plat + 1) == CellKind.Rope && TerraWorld.Get(px, plat - 2) == CellKind.Rope,
                $"上方={TerraWorld.Get(px, plat + 1)} 平台={TerraWorld.Get(px, plat)} 下面第二节={TerraWorld.Get(px, plat - 2)}");
            // Persist exactly the placed rope cells across a scene unload and a real save reload.
            yield return GoTo("Town", EntryGateOf("Town"));
            yield return GoTo(room, EntryGateOf(room));
            bool Intact()
            {
                for (int cy = bottom; cy < top; cy++) if (TerraWorld.Get(x, cy) != CellKind.Rope) return false;
                for (int cy = bottom; cy < bottom + 4; cy++) if (TerraWorld.Get(x + 3, cy) != CellKind.Rope) return false;
                if (TerraWorld.Get(x + 3, bottom + 7) != CellKind.Rope) return false;
                return true;
            }
            Check("离开场景再回来", Intact(), "检查全部16节");
            yield return QuitTest(1);
            yield return GoTo(room, EntryGateOf(room));
            Check("退出读档后保留", Intact(), "检查全部16节");
            Shot(room, "rope_2_reloaded");
            yield return null;
        }
    }
}
