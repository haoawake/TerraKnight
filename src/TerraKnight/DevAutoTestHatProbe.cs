using System.Collections;
using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "hatprobe"): hats of each of Terraria's hair rules worn in the social head slot over a long hairstyle, the
    /// player photographed close for each (TK_HATS: ';'-separated item names, else a set of every kind).
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator HatProbeTest()
        {
            PlayerData.instance.isInvincible = true;
            yield return GoTo("Town", "left1");
            yield return new WaitForSeconds(2.5f);
            var inv = mod.Inventory;
            var hero = HeroController.instance;
            var saved = inv.VanityArmor[0];
            Player.TerraStats.DevImmortal = true;
            mod.DevNoFallDamage = true;
            hero.transform.position = Clear(hero, (Vector2)hero.transform.position + Vector2.right * 26f);
            var rb = hero.GetComponent<Rigidbody2D>();
            var keep = rb.constraints;
            rb.linearVelocity = Vector2.zero;
            rb.constraints = RigidbodyConstraints2D.FreezeAll;
            yield return new WaitForSeconds(1f);
            string pick = System.Environment.GetEnvironmentVariable("TK_HATS");
            var hats = string.IsNullOrEmpty(pick)
                ? new[] { "None", "CopperHelmet", "MiningHelmet", "WizardHat", "Goggles", "RainHat", "GreenCap", "AnglerHat", "MagicHat", "JungleHat", "NinjaHood" }
                : pick.Split(';');
            foreach (var name in hats)
            {
                inv.VanityArmor[0] = System.Enum.TryParse<ItemType>(name, out var t) ? ItemStack.Of(t) : default;
                yield return new WaitForSeconds(0.4f);
                var sheet = inv.VanityArmor[0].IsEmpty ? 0 : inv.VanityArmor[0].Def.ArmorSheet;
                Player.Appearance.HairSettings(sheet, out bool full, out bool hat, out _);
                Plugin.Log.LogInfo($"[AutoTest] 帽子探针 {name}：头 {sheet}，完整发型 {full}，帽下发型 {hat}");
                var sp = Camera.main.WorldToScreenPoint(hero.GetComponent<BoxCollider2D>().bounds.center);
                Plugin.Log.LogInfo($"[AutoTest] 帽子探针 屏幕 {sp.x / Screen.width:F3},{1f - sp.y / Screen.height:F3}");
                Shot("Town", "hat_" + name);
                yield return null;
            }
            inv.VanityArmor[0] = saved;
            rb.constraints = keep;
            Player.TerraStats.DevImmortal = false;
        }
    }
}
