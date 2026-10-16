using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TerraKnight.Items;
using TerraKnight.Player;
using UnityEngine;

namespace TerraKnight
{
    internal sealed partial class DevAutoTest
    {
        IEnumerator LootCombatTest(Action<string,bool,string> check,List<object> report,int firstStage=0,int firstClass=0,HashSet<ItemType>[] available=null)
        {
            yield return GoTo("Crossroads_01",EntryGateOf("Crossroads_01"));
            var hero=HeroController.instance;
            PlayerData.instance.isInvincible=true;
            mod.DevNoFallDamage=true;
            hero.transform.position=Clear(hero,hero.transform.position);
            yield return new WaitForSeconds(.6f);
            Vector3 heroAt=hero.transform.position;
            var heroBody=hero.GetComponent<Rigidbody2D>();
            var target=UnityEngine.Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None)
                .FirstOrDefault(h=>!h.isDead && h.gameObject.activeInHierarchy && h.gameObject.scene.name=="Crossroads_01" && !EnemyStats.Of(h).Boss);
            check("实战有目标",target!=null,"");
            if(target==null) yield break;
            var info=EnemyStats.Of(target);
            int originalHp=target.hp, originalMaxHp=info.MaxHp;
            // Homing weapons must not choose a different wandering enemy and make this target's
            // damage falsely appear to be zero. This is a single-target fixture, not a live room fight.
            var others=UnityEngine.Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None)
                .Where(h=>h!=target && h.gameObject!=target.gameObject && h.gameObject.activeSelf
                    && h.gameObject.scene.name=="Crossroads_01" && !h.transform.IsChildOf(target.transform) && !target.transform.IsChildOf(h.transform))
                .Select(h=>h.gameObject).Distinct().ToArray();
            foreach(var go in others) go.SetActive(false);
            foreach(var fsm in target.GetComponents<PlayMakerFSM>()) fsm.enabled=false;
            target.IsInvincible=false;
            var body=target.GetComponent<Rigidbody2D>();
            var col=hero.GetComponent<BoxCollider2D>();
            var targetCol=target.GetComponent<Collider2D>();
            Vector2 offset=targetCol!=null ? (Vector2)targetCol.bounds.center-(Vector2)target.transform.position : Vector2.zero;
            var inv=mod.Inventory;
            InventoryGui.Open=false;
            inv.Mouse=default;inv.Override=-1;inv.UseMouse=false;
            for(int s=firstStage;s<6;s++)
            {
                Shop.DevStage=s;
                for(int cls=firstClass;cls<4;cls++)
                {
                    hero.transform.position=heroAt;
                    if(heroBody!=null) heroBody.linearVelocity=Vector2.zero;
                    Minions.Instance.Clear();PlayerBuffs.Clear();SummonTags.Clear();EnemyDebuffs.Clear();
                    TerraProjectiles.Instance.DevClear();Whips.Instance.Clear();
                    for(int i=0;i<inv.Slots.Length;i++) inv.Slots[i]=default;
                    bool Usable(ItemType t) => ItemDb.Get(t).Stage<=s && (available==null || available[s].Contains(t));
                    var p=ProgressionBalance.ArmorPaths.Where(p=>p.Stage<=s && ProgressionBalance.ClassIndex(p.Class)==cls && p.Items.All(Usable))
                        .OrderByDescending(p=>p.Stage).First();
                    var acc=ProgressionBalance.AccessoryPaths[s][cls].Where(Usable).ToArray();
                    if(cls==3 && s>=1 && s<=3) acc=acc.Concat(new[] {ItemType.PygmyNecklace}).ToArray();
                    Equip(p.Items,acc);
                    Equipment.Update(inv);
                    mod.Stats.ManaCrystals=new[] {1,2,3,5,7,9}[s];
                    mod.Stats.UpdateMaximums(PlayerData.instance,Equipment.Current.ManaMax);
                    mod.Stats.Mana=mod.Stats.ManaMax;
                    ItemType weapon=ProgressionBalance.Core.Take(s+1).Reverse().SelectMany(row=>row[cls])
                        .First(t=>Usable(t) && (cls!=3 || ItemDb.Get(t).IsSummon && !ItemDb.Get(t).Sentry));
                    inv.Slots[0]=ItemStack.Of(weapon);inv.Selected=0;
                    inv.Mouse=default;inv.Override=-1;inv.UseMouse=false;
                    inv.Slots[Inventory.Size-1]=ItemStack.Of(ItemType.WoodenArrow,999);
                    inv.Slots[Inventory.Size-2]=ItemStack.Of(ItemType.MusketBall,999);
                    target.hp=50000;EnemyStats.Of(target);
                    target.IsInvincible=false;
                    Vector2 centre=col.bounds.center;
                    Vector2 aim=centre+Vector2.right*(cls==0 ? 1f : 3f);
                    if(cls==2 && s==5) aim+=Vector2.up*1.5f; // Large homing discs need clear flight space above the floor.
                    Pin(target,body,aim-offset);
                    mod.DevAim=aim;
                    mod.DevHold=ItemDb.Get(weapon).Channel ? true : (bool?)null;
                    yield return null;
                    if(cls==3)
                    {
                        for(int n=0;n<Equipment.MaxMinions;n++)
                        {
                            while(!mod.DevReady) {Pin(target,body,aim-offset);yield return null;}
                            mod.DevUseSelected(centre+Vector2.right*1.5f);
                            yield return null;
                        }
                        mod.DevAimMinions(weapon,aim);
                        var whip=ProgressionBalance.Core.Take(s+1).Reverse().SelectMany(row=>row[cls]).FirstOrDefault(t=>Usable(t) && ItemDb.Get(t).IsWhip);
                        if(whip!=ItemType.None) {inv.Slots[0]=ItemStack.Of(whip);weapon=whip;aim=centre+Vector2.right*1.2f;}
                    }
                    Pin(target,body,aim-offset);
                    int before=EnemyStats.Life(target,info),mana=mod.Stats.Mana;
                    int casts=1,created=TerraProjectiles.DevMade(ItemDb.Get(weapon).Shoot);
                    mod.DevUseSelected(aim);
                    float start=Time.time;
                    float nextDiagnostic=start+1;
                    for(float until=Time.time+8;Time.time<until;)
                    {
                        if(target==null || target.isDead) break;
                        // Keep both ends stationary: recoil/contact over previous cases must not
                        // move the shooter behind terrain while the aim stays at the old position.
                        hero.transform.position=heroAt;
                        if(heroBody!=null) heroBody.linearVelocity=Vector2.zero;
                        Pin(target,body,aim-offset);
                        mod.DevAim=aim;
                        if(mod.DevReady) {mod.DevUseSelected(aim);casts++;}
                        if(cls==2 && s==5 && Time.time>=nextDiagnostic)
                        {
                            nextDiagnostic+=1;
                            Plugin.Log.LogInfo($"[AutoTest] 台风诊断 主角={hero.transform.position} 中心={col.bounds.center} 目标={targetCol.bounds} 瞄准={aim} {TerraProjectiles.Instance.DevDescribe(ItemDb.Get(weapon).Shoot)}");
                        }
                        yield return null;
                    }
                    int after=target!=null && !target.isDead ? EnemyStats.Life(target,info) : 0;
                    float seconds=Time.time-start;
                    int damage=before-after;
                    float dps=damage/Mathf.Max(.1f,seconds);
                    check($"实际命中 阶段{s}职业{cls}",damage>0,$"{weapon} 伤害={damage}，DPS={dps:F1}，魔力={mana}→{mod.Stats.Mana}，使用{casts}次，实际物品{inv.Current.Type}，生成{TerraProjectiles.DevMade(ItemDb.Get(weapon).Shoot)-created}");
                    report.Add(new {Stage=s,Class=cls,Weapon=weapon.ToString(),Damage=damage,Dps=dps,Seconds=seconds,ManaBefore=mana,ManaAfter=mod.Stats.Mana,Equipment.Current.Defense,MaxMinions=Equipment.MaxMinions,GroundTargetPinned=true});
                    mod.DevHold=null;mod.DevAim=null;
                }
                yield return null;
            }
            Minions.Instance.Clear();SummonTags.Clear();TerraProjectiles.Instance.DevClear();Whips.Instance.Clear();
            mod.DevHold=null;mod.DevAim=null;mod.DevNoFallDamage=false;
            foreach(var go in others) if(go!=null) go.SetActive(true);
            if (target!=null) { target.hp=originalHp; info.MaxHp=originalMaxHp; info.Carry=0; }
        }
    }
}
