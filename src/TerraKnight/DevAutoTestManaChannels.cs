using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight.Items
{
    internal sealed partial class TerraProjectiles
    {
        internal Dictionary<string,float> DevManaArc(bool slow)
        {
            var mod=TerraMod.Instance;
            mod.Stats.Mana=slow?0:mod.Stats.ManaMax;
            mod.CheckMana(ItemDb.Get(ItemType.ArcSurge));
            var before=new HashSet<Proj>(arcs.Keys);
            ShootArcSurge(HeroController.instance.transform.position+Vector3.right*6f,Vector2.right,100,0,0);
            var p=arcs.Keys.First(q=>!before.Contains(q));
            var data=new Dictionary<string,float>{{"VisualLife",arcs[p].Life},{"ProjectileLife",p.TimeLeft}};
            p.Updates=1;data["CanDamageFirst"]=p.Spec.CanDamage(p)?1:0;
            p.Updates=2;data["CanDamageLater"]=p.Spec.CanDamage(p)?1:0;
            DevClear();
            return data;
        }
        // Run the actual AI methods on an unregistered holdout so each sample is
        // exactly one Terraria tick, independent of rendering rate. Child shots
        // are cleared before the live loop resumes; this is not a combat-DPS test.
        internal Dictionary<string,float> DevManaChannel(ItemType item, bool slow)
        {
            var mod=TerraMod.Instance;
            var kind=ItemDb.Get(item).Shoot;
            var spec=specs[kind];
            var p=new Proj {Kind=kind, Spec=spec, Scale=1f, Damage=100, Vel=Vector2.right,
                Pos=HeroController.instance.transform.position, TimeLeft=3600, Ai0=item==ItemType.LastPrism?1f:0f};
            void Mana()
            {
                mod.Stats.Mana=slow?0:mod.Stats.ManaMax;
            }
            Mana(); mod.CheckMana(ItemDb.Get(item));
            void Tick()
            {
                Mana();
                if(item==ItemType.LastPrism)StepLastPrism(p);
                else if(item==ItemType.LaserMachinegun)StepLaserMachinegun(p);
                else StepMedusaHead(p);
            }
            var result=new Dictionary<string,float>();
            if(item==ItemType.MedusaHead)
            {
                int before=DevMedusaTargetChecks;
                p.Ai0=60f;
                Tick();result["AfterOneTick"]=p.Ai0;
                for(int i=1;i<60;i++)Tick();
                result["AfterSixtyTicks"]=p.Ai0;
                result["TargetChecksIn60Ticks"]=DevMedusaTargetChecks-before;
            }
            else
            {
                for(int i=0;i<180;i++)Tick();
                result["ChargeAfter180Ticks"]=p.Ai0;
                if(item==ItemType.LastPrism)
                {
                    var beam=new Proj {Kind=ProjKind.LastPrismBeam, Spec=specs[ProjKind.LastPrismBeam],Parent=p};
                    StepPrismBeam(beam);
                    result["HitInterval"]=beam.OwnerCooldown;
                    result["UsesSharedImmunity"]=beam.Cooldown==0&&beam.Spec.LocalCooldown==0?1:0;
                    result["PhysicalScale"]=beam.Scale;
                    result["SlowVisualFlag"]=beam.LocalAi0;
                }
                else
                {
                    int shots=DevMachinegunLasers, frames=0, sounds=0;
                    for(int i=0;i<240;i++)
                    {
                        int frame=p.Frame, sound=p.SoundDelay;
                        Tick();
                        if(p.Frame!=frame)frames++;
                        if(p.SoundDelay>sound)sounds++;
                    }
                    result["LasersIn240Ticks"]=DevMachinegunLasers-shots;
                    result["AnimationAdvances"]=frames;
                    result["SoundCycles"]=sounds;
                }
            }
            result["Alive"]=p.Dead?0:1;
            DevClear();
            return result;
        }
    }
}

namespace TerraKnight
{
    internal sealed partial class DevAutoTest
    {
        IEnumerator ManaChannelsTest()
        {
            if(!DevCopy.Verified)throw new InvalidOperationException("Isolated saves required");
            bool oldNoFallDamage=mod.DevNoFallDamage;
            mod.DevNoFallDamage=true;
            yield return GoTo("Town","left1");
            mod.ApplyCreative(false);
            PlayerData.instance.isInvincible=true;
            var inv=mod.Inventory;
            var slots=(ItemStack[])inv.Slots.Clone();
            var acc=(ItemStack[])inv.Accessories.Clone();
            bool infinite=inv.Infinite;
            var results=new List<object>();
            int checks=0,failures=0;
            void Check(string name,bool pass,string detail)
            {
                checks++;if(!pass)failures++;
                results.Add(new {Name=name,Pass=pass,Detail=detail});
                Plugin.Log.LogInfo($"[AutoTest] 缺魔持续施法 {name} {(pass?"通过":"不通过")} {detail}");
            }
            try
            {
                Wear();inv.Infinite=false;
                HeroController.instance.transform.position=new Vector2(60f,40f);
                yield return null;
                mod.DevAim=new Vector2(80f,40f);
                mod.DevHold=true;
                foreach(var item in new[]{ItemType.LastPrism,ItemType.LaserMachinegun,ItemType.MedusaHead})
                {
                    inv.Slots[2]=ItemStack.Of(item);inv.Selected=2;
                    foreach(bool slow in new[]{false,true})
                    {
                        var data=TerraProjectiles.Instance.DevManaChannel(item,slow);
                        string label=item+"/"+slow;
                        string detail=JsonConvert.SerializeObject(data);
                        Check(label+"持续存在",data["Alive"]==1,detail);
                        if(item==ItemType.MedusaHead)
                        {
                            Check(label+"凝视计时不放慢",data["AfterOneTick"]==59&&data["AfterSixtyTicks"]==0,detail);
                            Check(label+"原版整数间隔检查",data["TargetChecksIn60Ticks"]==(slow?2:4),detail);
                        }
                        else
                        {
                            Check(label+"蓄力按真实帧推进",data["ChargeAfter180Ticks"]==(item==ItemType.LastPrism?181:180),detail);
                            if(item==ItemType.LastPrism)
                            {
                                Check(label+"命中间隔",data["HitInterval"]==(slow?12:5)&&data["UsesSharedImmunity"]==1,detail);
                                Check(label+"视觉状态与碰撞",data["SlowVisualFlag"]==(slow?1:0)&&Mathf.Abs(data["PhysicalScale"]-1.4f)<.001f,detail);
                            }
                            else
                            {
                                Check(label+"双束发射频率",data["LasersIn240Ticks"]==(slow?32:80),detail);
                                Check(label+"枪身动画",data["AnimationAdvances"]==(slow?80:240),detail);
                                Check(label+"开火音效频率",data["SoundCycles"]==(slow?16:40),detail);
                            }
                        }
                    }
                }
                inv.Slots[2]=ItemStack.Of(ItemType.ArcSurge);
                foreach(bool slow in new[]{false,true})
                {
                    var data=TerraProjectiles.Instance.DevManaArc(slow);
                    string detail=JsonConvert.SerializeObject(data);
                    Check("ArcSurge/"+slow+"特效寿命",data["VisualLife"]==(slow?48:16)&&data["ProjectileLife"]==(slow?50:18),detail);
                    Check("ArcSurge/"+slow+"特效延长不重复伤害",data["CanDamageFirst"]==1&&data["CanDamageLater"]==0,detail);
                }
                var magic=ItemDb.All.Where(d=>d.Class==DamageClass.Magic&&d.IsWeapon).Select(d=>new {Type=d.Type.ToString(),d.Channel,Shoot=d.Shoot.ToString(),d.Mana}).ToArray();
                File.WriteAllText(Path.Combine(DevCopy.SaveDir,"mana-channels.json"),JsonConvert.SerializeObject(new {Checks=checks,Failures=failures,Results=results,MagicItems=magic},Formatting.Indented));
                Plugin.Log.LogInfo($"[AutoTest] 缺魔持续施法合计 {checks-failures}/{checks} {(failures==0?"通过":"不通过")}");
            }
            finally
            {
                mod.DevHold=false;mod.DevAim=null;
                TerraProjectiles.Instance?.DevClear();
                Array.Copy(slots,inv.Slots,slots.Length);Array.Copy(acc,inv.Accessories,acc.Length);
                inv.Infinite=infinite;mod.Stats.Mana=mod.Stats.ManaMax;
                mod.DevNoFallDamage=oldNoFallDamage;
            }
        }
    }
}
