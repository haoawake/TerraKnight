using System.Collections;
using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "benchcharm"): seated on a bench (PlayerData.atBench, as the bench's Resting state keeps it) with the
    /// nail and an art in hand, the game's attack input is off, so a click on the charm bag can't stand the player up
    /// (the bench's Resting: ListenForAttack → GET UP); up again, it is on. Seated, the drawn player faces right whichever
    /// way the Knight faced.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator BenchCharmTest()
        {
            charmPass = charmAll = 0;
            charmTag = "benchcharm";
            var pd = PlayerData.instance;
            pd.isInvincible = true;
            var hero = HeroController.instance;
            yield return CharmPlace();
            pd.SetBool("hasNailArt", true);
            pd.SetBool("hasDashSlash", true);
            InHand(ItemType.KnightNail);
            yield return new WaitForSeconds(0.5f);
            var input = GameHooks.Input != null ? GameHooks.Input.inputActions : null;
            CharmCheck("站着拿骨钉（学会技艺）时游戏的攻击键开着", input != null && NailArts.Ready && input.attack.Enabled, $"技艺可用 {NailArts.Ready}");
            hero.FaceLeft();
            pd.atBench = true;
            yield return null;
            yield return null;
            CharmCheck("坐着时游戏的攻击键关着（拿骨钉点背包也不会站起来）", input != null && !input.attack.Enabled, $"攻击键开着 {(input != null && input.attack.Enabled)}");
            CharmCheck("坐着时朝右（骑士朝左也一样）", mod.DevPoseFacing > 0f, $"朝向 {mod.DevPoseFacing}，骑士朝右 {hero.cState.facingRight}");
            CharmShot("seated");
            pd.atBench = false;
            yield return null;
            yield return null;
            CharmCheck("站起来后攻击键又开着", input != null && input.attack.Enabled, "");
            CharmCheck("站起来后照常朝骑士的方向", mod.DevPoseFacing < 0f, $"朝向 {mod.DevPoseFacing}");
            foreach (var f in new[] { "hasNailArt", "hasDashSlash" }) pd.SetBool(f, false);
            // A fragile charm broken: its story beat, the Leg Eater's price in it.
            Charms.DevSetPage(true);
            bool broken0 = pd.brokenCharm_23, unbreak0 = pd.fragileHealth_unbreakable;
            pd.fragileHealth_unbreakable = false;
            pd.brokenCharm_23 = false;
            yield return new WaitForSeconds(1f);
            string text = StoryBeats.DevText("heartbroken") ?? "";
            CharmCheck("碎了的小剧情写着食腿者和修理价", text.Contains("食腿者") && text.Contains((200 * HkShopPrices.Factor).ToString()), text.Length > 120 ? text.Substring(0, 120) : text);
            pd.brokenCharm_23 = true;
            // Other beats may be queued before it (the nail art's, set just above): its own lines waited for.
            bool heard = false;
            for (float t = Time.time + 40f; Time.time < t && !heard;)
            {
                if (StoryBeats.DevLast != null && (StoryBeats.DevLast.Contains("胸口") || StoryBeats.DevLast.Contains("易碎心脏"))) heard = true;
                yield return null;
            }
            CharmCheck("易碎心脏碎了时说出小剧情", heard, StoryBeats.DevLast);
            yield return new WaitForSeconds(1.5f);
            CharmShot("broken");
            pd.brokenCharm_23 = broken0;
            pd.fragileHealth_unbreakable = unbreak0;
            mod.Inventory.Slots[0] = default;
            Plugin.Log.LogInfo($"[AutoTest] 护符{charmTag} 结果 {charmPass}/{charmAll}");
        }
    }
}
