using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using Newtonsoft.Json;
using TerraKnight.Items;
using TerraKnight.Net;
using TerraKnight.Player;
using TerraKnight.Terraria;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    internal sealed partial class DevAutoTest
    {
        IEnumerator SkatesTest()
        {
            if(!DevCopy.Verified)throw new InvalidOperationException("Private copy required");
            int checks=0,failures=0,oldStage=Shop.DevStage;
            var rows=new List<object>();
            void Check(string name,bool pass,string detail="") {
                checks++;if(!pass)failures++;rows.Add(new{name,pass,detail});
                Plugin.Log.LogInfo($"[AutoTest] 溜冰鞋 {name}：{detail} {(pass?"通过":"不通过")}");
            }
            yield return GoTo("Town","left1");
            mod.ApplyCreative(false);Shop.DevStage=5;mod.DevNoFallDamage=true;
            PlayerData.instance.isInvincible=true;
            var hero=HeroController.instance;var rb=hero.GetComponent<Rigidbody2D>();var box=hero.GetComponent<BoxCollider2D>();
            var hook=GrapplingHook.Instance;var remote=new RemotePlayer {Slot=237,Name="Skates test"};
            GameObject ledge=null;
            IEnumerator Reset()
            {
                remote.Destroy();hook.ReleaseAll();Mounts.Dismount();TerraMovement.DevMove=0;TerraMovement.DevJump=false;
                hero.transform.position=Clear(hero,new Vector2(112f,12f));rb.position=hero.transform.position;rb.linearVelocity=Vector2.zero;
                yield return new WaitForSeconds(.8f);
            }
            SpriteRenderer[] ShoeRenderers(Transform root,int shoe)=>root.GetComponentsInChildren<SpriteRenderer>(true)
                .Where(r=>r.enabled&&r.gameObject.activeInHierarchy&&r.sprite!=null&&r.sprite.texture==TerrariaAssets.Texture("Acc_Shoes_"+shoe)).ToArray();
            try {
                for(int x=200;x<=300;x++)for(int y=20;y<=54;y++)TerraWorld.Set(x,y,y==20?CellKind.Stone:CellKind.Empty);
                Equip(Array.Empty<ItemType>(),Array.Empty<ItemType>());
                mod.Inventory.Slots[0]=default;mod.Inventory.Selected=0;mod.Inventory.Override=-1;
                yield return Reset();
                TerraCamera.DevLook=new Vector2(112f,12f);
                var rider=mod.GetComponentsInChildren<TerraPlayerSprite>(true).First(p=>p.transform.parent==mod.transform);
                var baseSize=box.size;var baseOffset=box.offset;
                TerraMovement.Apply(hero);float baseJump=hero.JUMP_SPEED;
                foreach(int id in new[]{57,58,59,60}) {
                    yield return Reset();var mount=Mounts.Get(id);
                    Check("实际穿戴/"+id,Mounts.TryMount(mount,hero));
                    yield return new WaitForSeconds(.2f);TerraMovement.Apply(hero);
                    Check("原版脚部穿戴贴图/"+id,ShoeRenderers(rider.transform,id-30).Length==1);
                    Check("不再把物品图标画在脚下/"+id,!Effects.FxBatch.Instance.GetComponentsInChildren<SpriteRenderer>().Any(r=>r.enabled&&r.gameObject.activeInHierarchy&&r.sprite==TerraMod.ItemSprite(mount.Item)));
                    Check("正常碰撞体与跳跃加成/"+id,box.size==baseSize&&box.offset==baseOffset&&Mathf.Abs(hero.JUMP_SPEED-baseJump-TerraMovement.PxT)<.01f
                        &&hero.JUMP_STEPS==Mathf.RoundToInt(17f/60f/Time.fixedDeltaTime));
                    yield return new WaitForEndOfFrame();RenderMountPhoto(id,"_skates_standing");
                    TerraMovement.DevMove=1;var frames=new HashSet<int>();
                    for(float t=0;t<.65f;t+=Time.deltaTime){frames.Add(rider.LegFrameNow);yield return null;}
                    Check("正常腿部滑行动画/"+id,frames.Count>1&&frames.Any(f=>f>=7),string.Join(",",frames));
                    float coastBefore=rb.linearVelocity.x/TerraMovement.PxT;TerraMovement.DevMove=0;
                    yield return new WaitForSeconds(.3f);
                    float coastAfter=rb.linearVelocity.x/TerraMovement.PxT;
                    Check("松开方向继续滑行/"+id,coastBefore>2.5f&&coastAfter>coastBefore*.75f,$"{coastBefore:F2} -> {coastAfter:F2}");
                    Check("滑行时双脚保持站立帧/"+id,rider.LegFrameNow==0);
                    rb.linearVelocity=new Vector2(.5f*TerraMovement.PxT,0);
                    yield return new WaitForSeconds(.4f);
                    Check("低速滑行逐渐停下/"+id,Mathf.Abs(rb.linearVelocity.x/TerraMovement.PxT)<.12f);
                    var state=new PlayerState {Scene="Town",Feet=NetScenes.ToLocal("Town",mod.NetPose.Feet+Vector2.right*2),Shown=true,FacingRight=false,
                        Grounded=true,Mount=id,Opacity=1,ItemScale=1,Swing=-1,Body=-1,Leg=0,Eq=new TerraPlayerSprite.Equips {Shoe=6,Stealth=1}};
                    using(var memory=new MemoryStream()) {
                        using(var writer=new BinaryWriter(memory,System.Text.Encoding.UTF8,true))state.Write(writer);
                        memory.Position=0;state=PlayerState.Read(new BinaryReader(memory));
                    }
                    remote.Receive(state);remote.Pose();
                    var root=GameObject.Find("Terra Net Player 237");
                    Check("远端显示同款原版溜冰鞋/"+id,root!=null&&ShoeRenderers(root.transform,id-30).Length==1);
                    yield return new WaitForEndOfFrame();RenderMountPhoto(id,"_skates_remote");remote.Destroy();
                    ledge=new GameObject("Skates hook ledge") {layer=ChunkColliders.TerrainLayer};
                    ledge.transform.position=box.bounds.center+Vector3.up*3f;
                    ledge.AddComponent<BoxCollider2D>().size=new Vector2(2f,.35f);Physics2D.SyncTransforms();
                    float hookStartY=hero.transform.position.y;
                    hook.Throw(hero,ItemType.GrapplingHook,ledge.transform.position);
                    float until=Time.time+1.5f;while(!GrapplingHook.IsLatched&&Time.time<until)yield return null;
                    yield return new WaitForSeconds(.2f);
                    Check("穿戴中抓钩命中并牵引，不强制脱鞋/"+id,GrapplingHook.IsLatched&&Mounts.Current==mount&&hero.transform.position.y>hookStartY+.3f,
                        hook.DevState+" mount="+Mounts.Current?.Native);
                    Mounts.Dismount();
                    Check("抓钩悬挂时可以重新穿鞋/"+id,Mounts.TryMount(mount,hero));
                    yield return new WaitForSeconds(.15f);
                    Check("悬挂后继续保持穿戴/"+id,GrapplingHook.IsLatched&&Mounts.Current==mount);
                    hook.DevJumpRelease(hero);yield return new WaitForSeconds(.1f);
                    Check("抓钩跳脱后保留溜冰鞋/"+id,!GrapplingHook.IsLatched&&Mounts.Current==mount);
                    UnityEngine.Object.Destroy(ledge);ledge=null;
                    Mounts.Dismount();yield return new WaitForSeconds(.1f);
                    Check("脱鞋后恢复原穿戴层/"+id,ShoeRenderers(rider.transform,id-30).Length==0);
                }
                yield return Reset();Equip(Array.Empty<ItemType>(),new[]{ItemType.CloudinaBottle,ItemType.AngelWings});
                yield return new WaitForSeconds(.3f);Mounts.TryMount(Mounts.Get(57),hero);
                Check("保留翅膀和额外跳跃选项",TerraMovement.HasWings&&TerraMovement.JumpCloud);
                TerraMovement.DevGroundJump(hero);yield return new WaitForSeconds(.25f);
                int extraBefore=TerraMovement.DevExtraJumps;
                AccessTools.Method(typeof(HeroController),"DoDoubleJump").Invoke(hero,null);yield return null;
                Check("实际额外跳跃不解除穿戴",TerraMovement.DevExtraJumps>extraBefore&&Mounts.Current==Mounts.Get(57));
                float wingBefore=TerraMovement.DevWingTime;
                TerraMovement.DevJump=true;yield return new WaitForSeconds(.6f);TerraMovement.DevJump=false;
                Check("穿戴中实际使用翅膀飞行",Mounts.Current==Mounts.Get(57)&&wingBefore>TerraMovement.DevWingTime&&TerraMovement.DevWingTime>0,
                    $"飞行时间 {wingBefore:F1} -> {TerraMovement.DevWingTime:F1}");
                yield return Reset();Equip(Array.Empty<ItemType>(),new[]{ItemType.RocketBoots});yield return new WaitForSeconds(.3f);
                Mounts.TryMount(Mounts.Get(57),hero);hero.transform.position+=Vector3.up*4;rb.position=hero.transform.position;rb.linearVelocity=Vector2.zero;
                yield return new WaitForSeconds(.15f);TerraMovement.DevJump=true;yield return new WaitForSeconds(.45f);TerraMovement.DevJump=false;
                int rocketFuel=(int)AccessTools.Field(typeof(TerraMovement),"rocketFuel").GetValue(null);
                float rocketPulse=(float)AccessTools.Field(typeof(TerraMovement),"pulseLeft").GetValue(null);
                Check("原版规则：穿溜冰鞋时火箭靴不额外提供喷气",Mounts.Current==Mounts.Get(57)&&rocketFuel==0&&rocketPulse==0,
                    $"剩余喷气次数 {rocketFuel}，喷气持续 {rocketPulse:F1}，y速度 {rb.linearVelocity.y:F2}");
                yield return Reset();Equip(Array.Empty<ItemType>(),Array.Empty<ItemType>());yield return new WaitForSeconds(.15f);
                Check("原版同样允许抓钩的迅猛龙对照骑乘",Mounts.TryMount(Mounts.Get(54),hero));
                ledge=new GameObject("Raptor hook ledge") {layer=ChunkColliders.TerrainLayer};
                ledge.transform.position=box.bounds.center+Vector3.up*3;
                ledge.AddComponent<BoxCollider2D>().size=new Vector2(2,.35f);Physics2D.SyncTransforms();
                hook.Throw(hero,ItemType.GrapplingHook,ledge.transform.position);
                float raptorUntil=Time.time+1.5f;while(!GrapplingHook.IsLatched&&Time.time<raptorUntil)yield return null;
                yield return new WaitForSeconds(.2f);
                Check("迅猛龙遵守相同原版抓钩白名单",GrapplingHook.IsLatched&&Mounts.Current==Mounts.Get(54));
                UnityEngine.Object.Destroy(ledge);ledge=null;
                yield return Reset();
                Check("普通坐骑对照骑乘",Mounts.TryMount(Mounts.Get(1),hero));
                hook.Throw(hero,ItemType.GrapplingHook,box.bounds.center+Vector3.up*3);
                Check("普通坐骑使用抓钩仍按原版解除",!Mounts.Riding);hook.ReleaseAll();
            } finally {
                if(ledge!=null)UnityEngine.Object.Destroy(ledge);remote.Destroy();hook.ReleaseAll();Mounts.Dismount();
                TerraMovement.DevMove=0;TerraMovement.DevJump=false;TerraCamera.DevLook=null;Shop.DevStage=oldStage;
            }
            File.WriteAllText(Path.Combine(DevCopy.SaveDir,"skates-result.json"),JsonConvert.SerializeObject(new{checks,failures,rows},Formatting.Indented));
            Plugin.Log.LogInfo($"[AutoTest] 溜冰鞋 合计 {checks-failures}/{checks} {(failures==0?"通过":"不通过")}");
        }
    }
}
