using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight
{
    internal sealed partial class DevAutoTest
    {
        IEnumerator BookStageTest()
        {
            yield return GoTo("Crossroads_01",EntryGateOf("Crossroads_01"));
            PlayerData.instance.isInvincible=true;
            InventoryGui.Open=true;
            InventoryGui.DevBestiary(true);
            yield return new WaitForSeconds(.5f);
            int checks=0,passed=0;
            void Check(string label,bool pass)
            {
                checks++;if(pass) passed++;
                Plugin.Log.LogInfo($"[AutoTest] 图鉴阶段 {label} {(pass ? "通过" : "不通过")}");
            }
            var all=InventoryGui.DevBestiaryFiltered();
            Check("默认地区模式",!InventoryGui.DevBestiaryByStage);
            InventoryGui.DevBestiarySelect(-101); // area 1
            yield return new WaitForSeconds(.15f);
            var area=InventoryGui.DevBestiaryFiltered();
            Check("地区过滤",area.Count>0 && area.All(r=>BestiaryRoute.Of(r).Area==1));
            InventoryGui.DevBestiarySelect(-3);
            yield return new WaitForSeconds(.15f);
            Check("切到阶段全表",InventoryGui.DevBestiaryByStage && InventoryGui.DevBestiaryFiltered().Count==all.Count);
            var union=new List<string>();
            for(int s=0;s<6;s++)
            {
                InventoryGui.DevBestiarySelect(-100-s);
                yield return new WaitForSeconds(.15f);
                var rows=InventoryGui.DevBestiaryFiltered();
                Check("阶段"+s+"分类",rows.Count>0 && rows.All(r=>ProgressionBalance.BossRewards.TryGetValue(r,out var boss)
                    ? boss.Stage==s : Mathf.Clamp(World.Areas.LevelOf(EnemyDrops.SceneOf(r)),0,5)==s));
                Check("阶段"+s+"包含分类标题",InventoryGui.DevBestiaryLayout().Contains("："));
                union.AddRange(rows);
            }
            Check("阶段无重复无遗漏",union.Count==all.Count && union.Distinct().Count()==all.Count && !all.Except(union).Any());
            InventoryGui.DevBestiarySelect(-104);
            yield return new WaitForSeconds(.15f);
            Check("蜂巢骑士阶段4",InventoryGui.DevBestiaryFiltered().Contains("HiveKnight"));
            yield return new WaitForSeconds(.5f);
            InventoryGui.DevBestiarySelect(-2);
            yield return new WaitForSeconds(.15f);
            Check("切回地区保留筛选",!InventoryGui.DevBestiaryByStage && InventoryGui.DevBestiaryFiltered().SequenceEqual(area));
            yield return new WaitForSeconds(.5f);
            InventoryGui.DevBestiarySelect(-3);
            yield return new WaitForSeconds(.15f);
            Check("阶段保留筛选",InventoryGui.DevBestiaryFiltered().Contains("HiveKnight") && InventoryGui.DevBestiaryLayout().Contains("首领"));
            InventoryGui.DevBestiarySelect(-99);
            yield return new WaitForSeconds(.15f);
            Check("全部阶段含阶段标题",InventoryGui.DevBestiaryFiltered().Count==all.Count && InventoryGui.DevBestiaryLayout().Contains("阶段 0"));
            InventoryGui.DevScrollBestiary(999);
            yield return new WaitForSeconds(.15f);
            Check("滚动不改变分类",InventoryGui.DevBestiaryFiltered().Count==all.Count);
            yield return new WaitForSeconds(.5f);
            Plugin.Log.LogInfo($"[AutoTest] 图鉴阶段汇总 {passed}/{checks}");
            InventoryGui.DevBestiary(false);InventoryGui.Open=false;
        }
    }
}
