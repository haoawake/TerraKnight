using System.Collections;
using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "holdpose"): how weapons are held and where their shots start. Each one used for a
    /// moment aimed right and a little up, a close picture of the player taken while it fires.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        static readonly ItemType[] poseWeapons =
        {
            ItemType.SDMG, ItemType.VortexBeater, ItemType.Phantasm, ItemType.Tsunami, ItemType.LastPrism, ItemType.ChargedBlasterCannon,
            ItemType.NebulaArcanum, ItemType.RazorbladeTyphoon, ItemType.LunarFlareBook, ItemType.StardustDragonStaff, ItemType.RainbowCrystalStaff,
            ItemType.Zenith, ItemType.DayBreak, ItemType.Xenopopper,
        };

        IEnumerator HoldPoseTest(HeroController hero)
        {
            PlayerData.instance.isInvincible = true;
            mod.ApplyCreative(true);
            mod.Stats.ManaCrystals = TerraKnight.Player.TerraStats.MaxManaCrystals;
            var inv = mod.Inventory;
            yield return new WaitForSeconds(1f);
            foreach (var t in poseWeapons)
            {
                inv.Slots[0] = ItemStack.Of(t);
                inv.Slots[Inventory.Size - 1] = ItemStack.Of(ItemType.LuminiteBullet, 999);
                inv.Slots[Inventory.Size - 2] = ItemStack.Of(ItemType.LuminiteArrow, 999);
                inv.Selected = 0;
                mod.Stats.Mana = 400;
                Vector2 c = hero.GetComponent<BoxCollider2D>().bounds.center;
                var aim = c + new Vector2(6f, 1.5f);
                mod.DevAim = aim;
                mod.DevHold = true;
                yield return null;
                yield return null;
                TerraProjectiles.DevLastKind = ProjKind.None;
                mod.DevUseSelected(aim);
                var kind = TerraProjectiles.DevLastKind;
                var from = TerraProjectiles.DevLastFrom;
                // The frame of the use, as it is drawn: where the shot first shows.
                yield return CloseUpAt(GameManager.instance.sceneName, "pose_" + t + "_0", c + new Vector2(0.9f, 0.3f), 110, 3);
                yield return null;
                var first = kind != ProjKind.None ? TerraProjectiles.Instance.DevFirstDrawn(kind) : null;
                bool pictured = false;
                for (float s = 0f; s < 0.6f; s += Time.deltaTime)
                {
                    if (mod.DevReady && !mod.IsUsingItem) mod.DevUseSelected(aim);
                    mod.Stats.Mana = 400;
                    if (!pictured && s > 0.15f)
                    {
                        pictured = true;
                        yield return CloseUpAt(GameManager.instance.sceneName, "pose_" + t + "_1", c + new Vector2(0.9f, 0.3f), 110, 3);
                    }
                    yield return null;
                }
                mod.DevHold = null;
                mod.DevAim = null;
                float px = Effects.TerraParticles.Px;
                string at(Vector2? v) => v.HasValue ? $"({(v.Value.x - c.x) / px:F0},{(v.Value.y - c.y) / px:F0})px" : "无";
                Plugin.Log.LogInfo($"[AutoTest] 持握 {ItemDb.Get(t).Name}：贴图 {ItemDb.Get(t).Texture}，弹幕 {kind} 起点 {at(kind != ProjKind.None ? from : (Vector2?)null)} 首帧画在 {at(first)}");
                yield return new WaitForSeconds(0.8f);
            }
        }
    }
}
