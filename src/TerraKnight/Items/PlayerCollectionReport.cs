using System.Collections.Generic;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>Read-only native collection counters; spent or stored items are not invented lifetime totals.</summary>
    internal static class PlayerCollectionReport
    {
        internal static List<PlayerStatReport.Row> Capture()
        {
            var rows = new List<PlayerStatReport.Row>();
            var pd = PlayerData.instance;
            var st = TerraMod.Instance?.Stats;
            if (pd == null || st == null) return rows;
            void Add(string label, object value) => rows.Add(new PlayerStatReport.Row(label, value.ToString()));
            int masks = Mathf.Max(0, pd.maxHealthBase - 5) * 4 + Mathf.Max(0, pd.heartPieces);
            int vessels = Mathf.Max(0, pd.MPReserveMax / 33);
            Add("当前生命血（蓝虫）计数", pd.healthBlue);
            Add("生命血蓝门", pd.blueVineDoor ? "已打开" : "未打开；带生命血到深渊蓝门前");
            Add("生命血计数规则", "收集累加；护盾破碎不扣；休息或死亡重置");
            Add("当前生命血银心护盾", st.Shield);
            Add("当前可用梦境精华", pd.dreamOrbs);
            Add("已消耗梦境精华", pd.dreamOrbsSpent);
            Add("精华统计范围", "可用量含随身及个人存储；普通箱内不计");
            Add("面具碎片收集", masks + " / 16");
            Add("原版基础面具", pd.maxHealthBase + " / 9");
            Add("下一面具碎片", pd.maxHealthBase >= pd.maxHealthCap ? "已集齐" : pd.heartPieces + " / 4");
            Add("灵魂容器碎片收集", (vessels * 3 + Mathf.Max(0, pd.vesselFragments)) + " / 9");
            Add("已组成灵魂容器", vessels + " / 3");
            Add("下一容器碎片", pd.MPReserveMax >= pd.MPReserveCap ? "已集齐" : pd.vesselFragments + " / 3");
            Add("已救幼虫", pd.grubsCollected + " / 46");
            Add("已获得护符", Charms.Owned + " / " + Charms.Count);
            Add("护符凹槽", pd.charmSlots);
            Add("生命水晶已使用", st.LifeCrystals + " / 15");
            Add("生命果已使用", st.LifeFruits + " / 20");
            Add("魔力水晶已使用", st.ManaCrystals + " / " + Player.TerraStats.MaxManaCrystals);
            Add("骨钉强化次数", pd.nailSmithUpgrades + " / 4");
            Add("随身苍白矿石", pd.GetInt("ore"));
            Add("随身简单钥匙", pd.GetInt("simpleKeys"));
            Add("随身腐臭蛋", pd.GetInt("rancidEggs"));
            Add("随身流浪者日记", pd.GetInt("trinket1"));
            Add("随身圣巢印章", pd.GetInt("trinket2"));
            Add("随身国王雕像", pd.GetInt("trinket3"));
            Add("随身神秘蛋", pd.GetInt("trinket4"));
            Add("物品计数规则", "矿石、钥匙、蛋及遗物显示随身现存；不冒充累计获取量");
            return rows;
        }
    }
}
