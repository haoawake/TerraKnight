using System.Collections;
using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "steadydash", the user's 10-26 "黑冲+稳定灵体护符会导致移动速度超级快，光脚也快"): Steady Body and the Shade
    /// Cloak worn, no boots, Dirtmouth: the run held right, its speed; a shadow dash; the speed one and two seconds after while
    /// right is still held (TK_SD_BEFORE=1: the kept speed as before). Lines "[AutoTest] 稳定冲刺 …".
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator SteadyDashTest()
        {
            Player.TerraStats.DevImmortal = true;
            CharmEffects.DevOldKeep = System.Environment.GetEnvironmentVariable("TK_SD_BEFORE") == "1";
            yield return GoTo("Town", "left1");
            yield return new WaitForSeconds(2f);
            var hero = HeroController.instance;
            var rb = hero.GetComponent<Rigidbody2D>();
            var inv = mod.Inventory;
            for (int k = 0; k < inv.Accessories.Length; k++) inv.Accessories[k] = default;
            inv.Accessories[0] = ItemStack.Of(ItemType.ShadeCloak);
            inv.RefreshEffective();
            PlayerData.instance.hasShadowDash = true;
            WearOnly(CharmEffects.SteadyBody);
            yield return new WaitForSeconds(0.5f);
            hero.transform.position = Clear(hero, (Vector2)hero.transform.position + Vector2.left * 20f);
            yield return new WaitForSeconds(0.5f);
            Player.TerraMovement.DevMove = 1;
            yield return new WaitForSeconds(1.2f);
            float run = Mathf.Abs(rb.linearVelocity.x);
            AbilityAccessories.DevShadowKey = true;
            float fastest = 0f;
            for (float t = 0f; t < 0.5f; t += Time.deltaTime) { fastest = Mathf.Max(fastest, Mathf.Abs(rb.linearVelocity.x)); yield return null; }
            yield return new WaitForSeconds(0.5f);
            float after1 = Mathf.Abs(rb.linearVelocity.x);
            yield return new WaitForSeconds(1f);
            float after2 = Mathf.Abs(rb.linearVelocity.x);
            Player.TerraMovement.DevMove = 0;
            bool ok = fastest > run * 1.5f && after2 <= run * 1.15f + 0.1f;
            Plugin.Log.LogInfo($"[AutoTest] 稳定冲刺 跑步 {run:F1}，暗影冲刺最快 {fastest:F1}，冲刺后 1 秒 {after1:F1}，2 秒 {after2:F1}，暗影冲刺 {AbilityAccessories.DevShadowDashes} 次 {(ok ? "通过" : "不通过")}");
            CharmEffects.DevOldKeep = false;
        }
    }
}
