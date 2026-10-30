using System.Collections;
using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "wardrobechat", the user's 10-30 "获得新时装的时候别在屏幕正中间弹出来…改为左下角的泰拉瑞亚播报" and
    /// "重复获得就不用播报了，直接卖掉"): a vanity piece not yet in the Wardrobe got twice: the first one line in the chat and no
    /// centre notice, the second sold with no line at all. Lines "[AutoTest] 衣柜播报 …".
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator WardrobeChatTest()
        {
            yield return new WaitForSeconds(1f);
            var inv = mod.Inventory;
            ItemType type = ItemType.None;
            foreach (var d in ItemDb.All)
                if (d.Vanity && !inv.WardrobeItems.Contains(d.Type)) { type = d.Type; break; }
            if (type == ItemType.None) { Plugin.Log.LogInfo("[AutoTest] 衣柜播报 没有未收藏的时装 不通过"); yield break; }
            string notice0 = UI.CenterNotice.DevLast;
            int lines0 = Net.NetChat.DevLines;
            long purse0 = Shop.Purse;
            int geo0 = Shop.Geo;
            inv.Add(type, 1);
            yield return null;
            int lines1 = Net.NetChat.DevLines;
            string line = Net.NetChat.DevLastLine;
            inv.Add(type, 1);
            yield return null;
            int lines2 = Net.NetChat.DevLines;
            bool collected = inv.WardrobeItems.Contains(type);
            bool chatFirst = lines1 == lines0 + 1 && line.Contains(ItemDb.Get(type).Name);
            bool quietSecond = lines2 == lines1;
            bool noCentre = UI.CenterNotice.DevLast == notice0;
            bool sold = Shop.Purse > purse0 || Shop.Geo > geo0;
            bool pass = collected && chatFirst && quietSecond && noCentre && sold;
            Plugin.Log.LogInfo($"[AutoTest] 衣柜播报 {type}：收藏 {collected}，第一次聊天 {chatFirst}（{line}），第二次不播报 {quietSecond}，屏幕中间没弹 {noCentre}，重复的卖了 {sold}（钱 {purse0}→{Shop.Purse}，吉欧 {geo0}→{Shop.Geo}） {(pass ? "通过" : "不通过")}");
        }
    }
}
