using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using TerraKnight.Items;
using TerraKnight.Player;
using UnityEngine;

namespace TerraKnight
{
    internal sealed partial class DevAutoTest
    {
        IEnumerator MinionGrowthTest()
        {
            if (!DevCopy.Verified) throw new InvalidOperationException("Isolated saves required");
            int stageWas = Shop.DevStage, checks = 0, failures = 0;
            var rows = new List<object>();
            void Check(string name, bool pass)
            {
                checks++; if (!pass) failures++;
                Plugin.Log.LogInfo("[AutoTest] 召唤成长/" + name + (pass ? " 通过" : " 不通过"));
            }
            yield return GoTo("Town", "left1");
            mod.ApplyCreative(false);
            PlayerData.instance.isInvincible = true; mod.DevNoFallDamage = true;
            var minions = Minions.Instance;
            Equip(new[] {ItemType.SpiderMask,ItemType.SpiderBreastplate,ItemType.SpiderGreaves},
                new[] {ItemType.PygmyNecklace,ItemType.NecromanticScroll,ItemType.PapyrusScarab,ItemType.SummonerEmblem});
            yield return null;
            Check("测试配装7召唤栏", Equipment.MaxMinions == 7);
            Vector2 At() => HeroController.instance.transform.position + Vector3.up;
            void Reset() { minions.Clear(); PlayerBuffs.Clear(); SummonTags.Clear(); }
            try
            {
                foreach (int stage in new[] {0,1,2,3,4,5})
                {
                    Reset(); Shop.DevStage = stage;
                    for (int n = 1; n <= 7; n++)
                    {
                        Check("阿比盖尔施放/"+stage+"/"+n, minions.Summon(ItemDb.Get(ItemType.AbigailsFlower),At()));
                        yield return new WaitForSeconds(.12f);
                        int baseDamage = minions.DevDamage(Minions.Kind.Abigail);
                        int damage = minions.DevContactDamage(Minions.Kind.Abigail);
                        int expected = (int)(baseDamage*(1f+(n-1)*(stage>=4?1.3f:.55f)));
                        Check("阿比盖尔层数和单本体/"+stage+"/"+n,
                            minions.DevCount(Minions.Kind.AbigailCounter)==n && minions.DevCount(Minions.Kind.Abigail)==1 && Mathf.Abs(minions.SlotsUsed-n)<.01f);
                        Check("阿比盖尔命中伤害/"+stage+"/"+n,baseDamage>0 && damage==expected);
                        rows.Add(new {Kind="Abigail",Stage=stage,Count=n,BaseDamage=baseDamage,ContactDamage=damage});
                    }
                }
                // Existing summons must grow on crossing the hardmode boundary, without recasting.
                Shop.DevStage=3;
                int before=minions.DevContactDamage(Minions.Kind.Abigail), serial=minions.DevSerial(Minions.Kind.Abigail);
                Shop.DevStage=4; yield return new WaitForSeconds(.1f);
                Check("已有阿比盖尔跨阶段即时强化",minions.DevContactDamage(Minions.Kind.Abigail)>before && minions.DevSerial(Minions.Kind.Abigail)==serial);
                Reset();
                int prior=0;
                for(int n=1;n<=7;n++)
                {
                    minions.Summon(ItemDb.Get(ItemType.DesertTigerStaff),At());
                    yield return new WaitForSeconds(.12f);
                    var kind=n<=3?Minions.Kind.Tiger1:n<=6?Minions.Kind.Tiger2:Minions.Kind.Tiger3;
                    int baseDamage=minions.DevDamage(kind), damage=minions.DevContactDamage(kind);
                    Check("沙漠虎宝石共享栏/"+n,minions.DevCount(Minions.Kind.TigerGem)==n && Mathf.Abs(minions.SlotsUsed-n)<.01f);
                    Check("沙漠虎形态/"+n,minions.DevCount(kind)==1 && minions.DevCount(Minions.Kind.Tiger1)+minions.DevCount(Minions.Kind.Tiger2)+minions.DevCount(Minions.Kind.Tiger3)==1);
                    Check("沙漠虎普通攻击逐次强化/"+n,damage==(int)(baseDamage*(1f+(n-1)*.4f)) && damage>prior);
                    prior=damage;
                    rows.Add(new {Kind=kind.ToString(),Stage=4,Count=n,BaseDamage=baseDamage,ContactDamage=damage});
                }
                serial=minions.DevSerial(Minions.Kind.Tiger3);
                minions.Summon(ItemDb.Get(ItemType.DesertTigerStaff),At());
                yield return new WaitForSeconds(.12f);
                Check("满栏重召不清空虎本体或攻击状态",minions.DevSerial(Minions.Kind.Tiger3)==serial && minions.DevCount(Minions.Kind.TigerGem)==7 && minions.DevContactDamage(Minions.Kind.Tiger3)==prior);
                Equip(Array.Empty<ItemType>(),Array.Empty<ItemType>());
                yield return new WaitForSeconds(.2f);
                Check("卸下装备后降回一栏小虎",minions.DevCount(Minions.Kind.TigerGem)==1 && minions.DevCount(Minions.Kind.Tiger1)==1 && minions.DevContactDamage(Minions.Kind.Tiger1)==minions.DevDamage(Minions.Kind.Tiger1));
                PlayerBuffs.Remove(PlayerBuffs.StormTiger); yield return new WaitForSeconds(.2f);
                Check("取消增益清除宝石和虎",minions.DevCount(Minions.Kind.TigerGem)==0 && minions.DevCount(Minions.Kind.Tiger1)==0);

                Equip(new[] {ItemType.SpiderMask,ItemType.SpiderBreastplate,ItemType.SpiderGreaves},
                    new[] {ItemType.PygmyNecklace,ItemType.NecromanticScroll,ItemType.PapyrusScarab,ItemType.SummonerEmblem});
                yield return null;
                Reset();
                for(int n=0;n<8;n++) { minions.Summon(ItemDb.Get(ItemType.StardustDragonStaff),At()); yield return new WaitForSeconds(.06f); }
                Check("满栏替换保留星尘龙链条",minions.DevCount(Minions.Kind.DragonHead)==1 && minions.DevCount(Minions.Kind.DragonTail)==1
                    && minions.DevCount(Minions.Kind.DragonBody1)==7 && minions.DevCount(Minions.Kind.DragonBody2)==7 && Mathf.Abs(minions.SlotsUsed-7)<.01f);
                Reset();
                for(int n=0;n<8;n++) { minions.Summon(ItemDb.Get(ItemType.OpticStaff),At()); yield return new WaitForSeconds(.06f); }
                Check("满栏替换保留双子成对",minions.DevCount(Minions.Kind.Retanimini)==7 && minions.DevCount(Minions.Kind.Spazmamini)==7 && Mathf.Abs(minions.SlotsUsed-7)<.01f);

                // Actual collision path, with a stationary native enemy and no whip tags.
                yield return GoTo("Crossroads_01",EntryGateOf("Crossroads_01"));
                var target=UnityEngine.Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None)
                    .FirstOrDefault(h=>!h.isDead && h.gameObject.activeInHierarchy && h.gameObject.scene.name=="Crossroads_01" && !EnemyStats.Of(h).Boss);
                Check("命中测试有原生敌人",target!=null);
                if(target!=null)
                {
                    foreach(var fsm in target.GetComponents<PlayMakerFSM>())fsm.enabled=false;
                    target.IsInvincible=false;
                    var body=target.GetComponent<Rigidbody2D>();
                    Equip(new[] {ItemType.SpiderMask,ItemType.SpiderBreastplate,ItemType.SpiderGreaves},
                        new[] {ItemType.PygmyNecklace,ItemType.NecromanticScroll,ItemType.PapyrusScarab,ItemType.SummonerEmblem});
                    yield return null;
                    foreach(var staff in new[]{ItemType.AbigailsFlower,ItemType.DesertTigerStaff})
                    {
                        Reset();
                        var kind=staff==ItemType.AbigailsFlower?Minions.Kind.Abigail:Minions.Kind.Tiger1;
                        var at=(Vector2)HeroController.instance.transform.position+Vector2.right*1.8f;
                        for(int n=0;n<3;n++)minions.Summon(ItemDb.Get(staff),at);
                        yield return new WaitForSeconds(.15f);
                        minions.SetTarget(target); CadenceTrace.Target=target; CadenceTrace.Hits.Clear();
                        int expected=minions.DevContactDamage(kind);
                        for(float end=Time.time+6f;Time.time<end;)
                        {
                            target.hp=100000; Pin(target,body,at);
                            if(CadenceTrace.Hits.Any(h=>h.Damage==expected))break;
                            yield return null;
                        }
                        Check("实际命中使用成长伤害/"+staff,CadenceTrace.Hits.Any(h=>h.Damage==expected));
                    }
                }
            }
            finally { Shop.DevStage=stageWas; CadenceTrace.Target=null; minions.Clear(); PlayerBuffs.Clear(); }
            File.WriteAllText(Path.Combine(DevCopy.SaveDir,"minion-growth.json"),JsonConvert.SerializeObject(new {Checks=checks,Failures=failures,Rows=rows},Formatting.Indented));
            Plugin.Log.LogInfo($"[AutoTest] 召唤成长合计 {checks-failures}/{checks} {(failures==0?"通过":"不通过")}");
        }
    }
}
