using System.Collections;
using TerraKnight.Items;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "artcooldown", BladeSkills.ShowCooldown, the user's 10-27 "所有武器的右键还没冷却的时候就应该在角色身上弹出cd时间
    /// （包括其他职业武器）"): in Dirtmouth, weapons of each kind with a pressed art that cools down (a sword's, a bow's, a machine
    /// gun's, a boomerang's, an umbrella's, a glaive's): the art pressed, then pressed again at once: its seconds left popped
    /// over the player; pressed again after its cooldown: used, no popup. Shots Town_cd_&lt;weapon&gt;. Lines "[AutoTest] 右键冷却 …".
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator ArtCooldownTest()
        {
            int ok = 0, all = 0;
            yield return GoTo("Town", "left1");
            yield return new WaitForSeconds(2f);
            var hero = HeroController.instance;
            PlayerData.instance.isInvincible = true;
            BladeArts.Set(true);
            for (float t = Time.realtimeSinceStartup + 10f; TargetDummy.DevAt == null && Time.realtimeSinceStartup < t;) yield return null;
            var at = TargetDummy.DevAt ?? (Vector2)hero.transform.position + Vector2.right * 4f;
            var inv = mod.Inventory;
            bool savedInfinite = inv.Infinite;
            inv.Infinite = true;
            var weapons = new[] { ItemType.IceBlade, ItemType.CopperBow, ItemType.Minishark, ItemType.EnchantedBoomerang, ItemType.TragicUmbrella, ItemType.MonkStaffT2 };
            foreach (var type in weapons)
            {
                var def = ItemDb.Get(type);
                for (int i = 0; i < inv.Slots.Length; i++) inv.Slots[i] = default;
                inv.Slots[0] = ItemStack.Of(type);
                if (def.UseAmmo != AmmoKind.None) inv.Slots[1] = ItemStack.Of(def.UseAmmo == AmmoKind.Arrow ? ItemType.WoodenArrow : ItemType.MusketBall, 999);
                inv.Selected = 0;
                mod.DevAim = at + new Vector2(0f, 0.8f);
                yield return new WaitForSeconds(0.5f);
                int uses0 = BladeSkills.DevUses, shown0 = BladeSkills.DevCooldownShown;
                BladeSkills.DevPress = true;
                yield return new WaitForSeconds(0.3f);
                bool used = BladeSkills.DevUses > uses0;
                float left = BladeSkills.CooldownLeft(def);
                BladeSkills.DevPress = true;
                yield return null;
                yield return null;
                bool popped = BladeSkills.DevCooldownShown > shown0;
                string text = BladeSkills.DevLastCooldown;
                yield return new WaitForSeconds(0.15f);
                Shot("Town", $"cd_{type}");
                // After its cooldown: used again, nothing popped.
                yield return new WaitForSeconds(left + 0.3f);
                int uses1 = BladeSkills.DevUses, shown1 = BladeSkills.DevCooldownShown;
                BladeSkills.DevPress = true;
                yield return new WaitForSeconds(0.3f);
                bool again = BladeSkills.DevUses > uses1 && BladeSkills.DevCooldownShown == shown1;
                bool pass = used && left > 0.1f && popped && text.Contains(left < 10f ? left.ToString("0.0").Substring(0, 1) : "") && again;
                all++;
                if (pass) ok++;
                Plugin.Log.LogInfo($"[AutoTest] 右键冷却 {def.Name}（{BladeSkills.KindArtOf(def)?.Name ?? BladeSkills.Of(def).ToString()}）：用了 {used}，冷却 {left:F1} 秒，再按弹出 {popped}「{text}」，冷却后再用 {again} {(pass ? "通过" : "不通过")}");
                mod.DevAim = null;
                yield return new WaitForSeconds(1.5f);
                TerraProjectiles.Instance?.Clear();
            }
            inv.Infinite = savedInfinite;
            Plugin.Log.LogInfo($"[AutoTest] 右键冷却 合计 {ok}/{all}");
        }
    }
}
