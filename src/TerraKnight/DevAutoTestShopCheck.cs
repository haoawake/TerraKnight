using System.Collections;
using UnityEngine;
using TerraKnight.Items;
using TerraKnight.Town;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "shopcheck"): in Dirtmouth, each townsperson with a shop talked to and its shop opened as a
    /// player does (right click, then the shop button): its stock logged and the open shop photographed.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator ShopCheckTest()
        {
            yield return GoTo("Town", "left1");
            yield return new WaitForSeconds(2f);
            PlayerData.instance.isInvincible = true;
            var hero = HeroController.instance;
            Plugin.Log.LogInfo($"[AutoTest] 商店 德特茅斯（阶段 {Shop.UnlockedStage()}）：{TownNpcs.Count} 位 NPC：{TownNpcs.DevDescribe()}");
            int ok = 0, all = 0;
            foreach (var who in new[] { "Merchant", "ArmsDealer", "Demolitionist", "GoblinTinkerer" })
            {
                var pos = TownNpcs.DevPos(who);
                if (!pos.HasValue) { Plugin.Log.LogInfo($"[AutoTest] 商店 {who}：不在德特茅斯"); continue; }
                float bottom = hero.GetComponent<BoxCollider2D>().bounds.min.y - hero.transform.position.y;
                hero.transform.position = Clear(hero, new Vector2(pos.Value.x - 1.5f, pos.Value.y - 20f / 32f - bottom + 0.05f));
                hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
                yield return new WaitForSeconds(0.6f);
                pos = TownNpcs.DevPos(who);
                bool talked = TownNpcs.TryRightClick(pos.Value);
                yield return new WaitForSeconds(0.3f);
                bool hasShop = TownNpcs.TalkingHasShop;
                TownNpcs.OpenShop();
                yield return new WaitForSeconds(0.5f);
                string stock = TownNpcs.DevStock();
                Shot("Town", "shopcheck_" + who);
                yield return null;
                yield return null;
                all++;
                bool pass = talked && hasShop && stock != "空" && InventoryGui.Open;
                if (pass) ok++;
                Plugin.Log.LogInfo($"[AutoTest] 商店 {who}：对话 {talked}，有商店 {hasShop}，背包开着 {InventoryGui.Open}，货：{stock} {(pass ? "通过" : "不通过")}");
                TownNpcs.Close(true);
                InventoryGui.Open = false;
                yield return new WaitForSeconds(0.4f);
            }
            Plugin.Log.LogInfo($"[AutoTest] 商店 合计 {ok}/{all}");
        }
    }
}
