using System.Collections;
using TerraKnight.Items;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "wallswing"): a wall raised in Dirtmouth and the player sliding down it (the Climbing Claws); a boomerang
    /// thrown with the mouse on the open side and on the wall's side: which way the swing faces and where the boomerang goes.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        static readonly HarmonyLib.AccessTools.FieldRef<HeroController, bool> wallSlidingL = HarmonyLib.AccessTools.FieldRefAccess<HeroController, bool>("wallSlidingL");
        static readonly HarmonyLib.AccessTools.FieldRef<HeroController, bool> wallTouchingL = HarmonyLib.AccessTools.FieldRefAccess<HeroController, bool>("touchingWallL");

        IEnumerator WallSwingTest()
        {
            int ok = 0, all = 0;
            void Check(string what, bool pass, string detail = "")
            {
                all++;
                if (pass) ok++;
                Plugin.Log.LogInfo($"[AutoTest] 贴墙挥动 {what}：{detail} {(pass ? "通过" : "不通过")}");
            }
            yield return GoTo("Town", EntryGateOf("Town"));
            PlayerData.instance.isInvincible = true;
            yield return new WaitForSeconds(1.5f);
            var hero = HeroController.instance;
            var inv = mod.Inventory;
            var saved = (ItemStack[])inv.Slots.Clone();
            int selected = inv.Selected;
            for (int side = 1; side >= -1; side -= 2)
            {
                // A wall two cells beside the player, ten high from the ground.
                var feet = hero.GetComponent<BoxCollider2D>().bounds;
                var baseCell = CellGrid.WorldToCell(new Vector2(feet.center.x + side * 1.6f, feet.min.y + 0.1f));
                for (int k = 0; k < 26; k++) Place(new Vector2Int(baseCell.x, baseCell.y + k), CellKind.Stone);
                for (int k = 0; k < 26; k++) Place(new Vector2Int(baseCell.x + side, baseCell.y + k), CellKind.Stone);
                yield return new WaitForSeconds(0.3f);
                // Up against it, falling: the game starts a slide from the real controls, so the slide is set as it sets it,
                // holding towards the wall.
                var col = hero.GetComponent<BoxCollider2D>();
                float half = col.bounds.extents.x, offset = hero.transform.position.x - col.bounds.center.x;
                float face = side > 0 ? baseCell.x * CellGrid.CellSize - half - 0.02f : (baseCell.x + 1) * CellGrid.CellSize + half + 0.02f;
                hero.transform.position = new Vector3(face + offset, feet.min.y + 6f, hero.transform.position.z);
                hero.GetComponent<Rigidbody2D>().linearVelocity = new Vector2(0f, -3f);
                if (side > 0) hero.FaceRight(); else hero.FaceLeft();
                yield return new WaitForFixedUpdate();
                if (side > 0) { wallTouchingR(hero) = true; wallSlidingR(hero) = true; }
                else { wallTouchingL(hero) = true; wallSlidingL(hero) = true; }
                hero.cState.wallSliding = true;
                hero.cState.touchingWall = true;
                Player.TerraMovement.DevMove = side;
                yield return new WaitForSeconds(0.3f);
                bool sliding = hero.cState.wallSliding;
                inv.Slots[0] = ItemStack.Of(ItemType.WoodenBoomerang);
                inv.Selected = 0;
                inv.Override = -1;
                foreach (var (label, aimSide) in new[] { ("鼠标在墙外", -side), ("鼠标在墙这边", side) })
                {
                    var c = (Vector2)hero.GetComponent<BoxCollider2D>().bounds.center;
                    var aim = c + new Vector2(aimSide * 6f, 2f);
                    mod.DevUseSelected(aim);
                    yield return new WaitForSeconds(0.25f);
                    var b = TerraProjectiles.Instance != null ? TerraProjectiles.Instance.DevNewest(ProjKind.WoodenBoomerang) : null;
                    float useDir = mod.UseDirection;
                    float went = b.HasValue ? b.Value.x - c.x : 0f;
                    Check($"{(side > 0 ? "右墙" : "左墙")} {label}", sliding && useDir == -side && went * -side > 0.5f,
                        $"贴墙 {sliding}，挥动朝 {(useDir > 0 ? "右" : "左")}，回旋镖在主角{(went > 0 ? "右" : "左")} {Mathf.Abs(went):F1}，朝向 {(hero.cState.facingRight ? "右" : "左")}");
                    Shot("Town", $"wallswing_{(side > 0 ? "R" : "L")}_{(aimSide == side ? "wall" : "open")}");
                    yield return new WaitForSeconds(1.2f);
                }
                Player.TerraMovement.DevMove = 0;
                for (int k = 0; k < 26; k++) { TerraWorld.Set(baseCell.x, baseCell.y + k, CellKind.Empty); TerraWorld.Set(baseCell.x + side, baseCell.y + k, CellKind.Empty); }
                yield return new WaitForSeconds(1f);
            }
            System.Array.Copy(saved, inv.Slots, saved.Length);
            inv.Selected = selected;
            Plugin.Log.LogInfo($"[AutoTest] 贴墙挥动 合计 {ok}/{all} {(ok == all ? "通过" : "不通过")}");
        }
    }
}
