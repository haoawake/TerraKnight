using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using TerraKnight.Items;
using TerraKnight.Player;
using TerraKnight.World;
using TerraKnight.Terraria;
using UnityEngine;
namespace TerraKnight
{
    internal sealed partial class DevAutoTest
    {
        void RenderMountPhoto(int id)
        {
            // A dedicated preview camera excludes HK's screen fade and scene borders while
            // rendering the actual live mount/player SpriteRenderers and their actual poses.
            var renderers=new List<SpriteRenderer>();
            if(Effects.FxBatch.Instance!=null)renderers.AddRange(Effects.FxBatch.Instance.GetComponentsInChildren<SpriteRenderer>());
            foreach(var player in UnityEngine.Object.FindObjectsByType<TerraPlayerSprite>(FindObjectsSortMode.None))renderers.AddRange(player.GetComponentsInChildren<SpriteRenderer>());
            var layers=renderers.Select(r=>r.gameObject.layer).ToArray();
            var go=new GameObject("Mount verification camera");var cam=go.AddComponent<Camera>();
            cam.orthographic=true;cam.orthographicSize=2.5f;cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.16f,.18f,.22f);
            cam.cullingMask=1<<31;cam.nearClipPlane=.01f;cam.farClipPlane=100f;
            var hero=HeroController.instance;cam.transform.position=new Vector3(hero.transform.position.x,hero.transform.position.y+.25f,-10f);
            var rt=new RenderTexture(640,360,24);var active=RenderTexture.active;var tex=new Texture2D(640,360,TextureFormat.RGB24,false);
            try {
                foreach(var r in renderers)r.gameObject.layer=31;
                cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;
                tex.ReadPixels(new Rect(0,0,640,360),0,0);tex.Apply();
                File.WriteAllBytes(Path.Combine(outDir,"render_mount_"+id+".png"),tex.EncodeToPNG());
            } finally {
                for(int i=0;i<renderers.Count;i++)if(renderers[i]!=null)renderers[i].gameObject.layer=layers[i];
                cam.targetTexture=null;RenderTexture.active=active;UnityEngine.Object.Destroy(tex);rt.Release();UnityEngine.Object.Destroy(rt);UnityEngine.Object.Destroy(go);
            }
        }
        IEnumerator MountTest()
        {
            if(!DevCopy.Verified)throw new InvalidOperationException("Isolated saves required");
            int checks=0,failures=0,oldStage=Shop.DevStage;
            var results=new List<object>();
            void Check(string name,bool pass){checks++;if(!pass)failures++;Plugin.Log.LogInfo("[AutoTest] 坐骑/"+name+(pass?" 通过":" 不通过"));}
            Check("38种，钻头排除",Mounts.All.Length==38 && Mounts.Get(8)==null);
            foreach(var m in Mounts.All)
            {
                var d=ItemDb.Get(m.Item);
                Check("注册及装备栏/"+m.Native,d.IsMount && d.MountId==m.Native && d.Stage==m.Stage && Inventory.FitsMisc(d,3) && !Inventory.FitsMisc(d,2) && d.MaxStack==1);
                bool assets=true;
                foreach(var l in m.Layers) {
                    var t=TerrariaAssets.Texture(l.Texture);assets&=t!=null && t.height>=m.Frames;
                    if(t!=null && t.height%m.Frames!=0)Plugin.Log.LogInfo("[AutoTest] 坐骑贴图余量/"+m.Native+"/"+l.Texture+" "+t.width+"x"+t.height+" frames="+m.Frames);
                }
                Check("原版图像/"+m.Native,assets && TerraMod.ItemSprite(m.Item)!=null);
                foreach(var key in m.Sources) {
                    var e=EnemyDrops.EntryFor(key);
                    Check("掉落源存在/"+m.Native+"/"+key,e.Hp>0);
                    var high=EnemyDrops.At(key,5,false,true,true).Where(r=>r.Type==m.Item).ToArray();
                    Check("来源独立概率/"+m.Native+"/"+key,high.Length==1 && high[0].Group==0 && high[0].Num==1 && high[0].Den==m.Den && high[0].Min==1 && high[0].Max==1);
                    if(m.Stage>0 && !ProgressionBalance.FormalBoss(key))Check("阶段前不掉落/"+m.Native+"/"+key,!EnemyDrops.At(key,m.Stage-1,false,true,true).Any(r=>r.Type==m.Item));
                }
                Check("无限飞行获取阶段/"+m.Native,!m.Infinite || m.Stage>=4);
                using(var memory=new MemoryStream()) {
                    var s=new Net.PlayerState {Scene="Town",Mount=m.Native,Opacity=1f,ItemScale=1f};
                    using(var w=new BinaryWriter(memory,System.Text.Encoding.UTF8,true))s.Write(w);
                    memory.Position=0;var read=Net.PlayerState.Read(new BinaryReader(memory));
                    Check("网络包往返/"+m.Native,read.Mount==m.Native && memory.Position==memory.Length);
                }
            }
            yield return GoTo("Town","left1");
            mod.ApplyCreative(false);mod.Inventory.Infinite=false;Shop.DevStage=5;PlayerData.instance.isInvincible=true;mod.DevNoFallDamage=true;
            Equip(Array.Empty<ItemType>(),Array.Empty<ItemType>());
            yield return new WaitForSeconds(.5f);
            var hero=HeroController.instance;var rb=hero.GetComponent<Rigidbody2D>();var box=hero.GetComponent<BoxCollider2D>();
            hero.transform.position=Clear(hero,new Vector2(110f,13f));rb.position=hero.transform.position;rb.linearVelocity=Vector2.zero;
            TerraCamera.DevLook=new Vector2(110f,14f);
            yield return new WaitForSeconds(1f);
            var start=hero.transform.position;var size=box.size;var offset=box.offset;
            Plugin.Log.LogInfo("[AutoTest] mount runway="+start+" ground="+hero.cState.onGround+" control="+GameHooks.PlayerHasControl());
            try {
                foreach(var m in Mounts.All) {
                    Mounts.Dismount();hero.transform.position=start;rb.position=start;rb.linearVelocity=Vector2.zero;
                    yield return new WaitForSeconds(.12f);
                    bool mounted=Mounts.TryMount(m,hero);
                    Check("实际骑乘/"+m.Native,mounted);
                    if(mounted) {
                        Check("碰撞高度/"+m.Native,Mathf.Abs(box.size.y-size.y-Mathf.Max(0,m.Lift)/32f)<.001f);
                        TerraMovement.DevMove=1;
                        yield return new WaitForSeconds(.2f);
                        Check("实际移动/"+m.Native,rb.linearVelocity.x>.05f);
                        TerraMovement.DevMove=0;
                        Shot("Town","mount_"+m.Native);
                        RenderMountPhoto(m.Native);
                        yield return null;
                    }
                    results.Add(new {m.Native,m.Name,m.Stage,Mounted=mounted,X=hero.transform.position.x,Y=hero.transform.position.y,Fuel=Mounts.Fuel});
                    Mounts.Dismount();
                    Check("下车恢复碰撞/"+m.Native,box.size==size && box.offset==offset);
                }
                // Verify short flight exhaustion and unlimited flight in actual physics frames.
                foreach(int id in new[]{5,7}) {
                    Mounts.Dismount();hero.transform.position=start+Vector3.up*3f;rb.position=hero.transform.position;rb.linearVelocity=Vector2.zero;
                    yield return new WaitForSeconds(.12f);
                    bool mounted=Mounts.TryMount(Mounts.Get(id),hero);
                    Check("空中骑乘/"+id,mounted);
                    if(id==5)Check("空中重召不能刷飞行时间",mounted && Mounts.Fuel==0);
                    Mounts.Dismount();hero.transform.position=start;rb.position=start;rb.linearVelocity=Vector2.zero;
                    yield return new WaitForSeconds(.5f);
                    mounted=Mounts.TryMount(Mounts.Get(id),hero);
                    Check("地面召唤补满飞行/"+id,mounted && Mounts.Fuel==Mounts.Get(id).Flight);
                    float beforeY=hero.transform.position.y,maxY=beforeY,minFuel=Mounts.Fuel;bool sawEmpty=false;
                    TerraMovement.DevJump=true;
                    float until=Time.time+(id==5?6.5f:1.2f);
                    while(Time.time<until) {
                        maxY=Mathf.Max(maxY,hero.transform.position.y);minFuel=Mathf.Min(minFuel,Mounts.Fuel);
                        if(Mounts.Fuel<=0 && !hero.cState.onGround)sawEmpty=true;
                        yield return null;
                    }
                    Plugin.Log.LogInfo("[AutoTest] mount flight id="+id+" mounted="+Mounts.Riding+" y="+beforeY+".."+maxY+" fuel="+minFuel+".."+Mounts.Fuel+" scene="+GameManager.instance.sceneName);
                    Check("实际飞起/"+id,maxY>beforeY+.5f);
                    Check("飞行持续/"+id,Mounts.Riding && (id==5?sawEmpty:Mounts.Fuel==Mounts.Get(id).Flight));
                    TerraMovement.DevJump=false;Mounts.Dismount();
                }
                hero.transform.position=start;rb.position=start;rb.linearVelocity=Vector2.zero;yield return new WaitForSeconds(.5f);
                mod.Inventory.Misc[3]=ItemStack.Of(Mounts.Get(3).Item,1);
                yield return Tap(TerraKeys.Get(TerraKeys.Action.Mount));
                Check("R键上坐骑",Mounts.Current==Mounts.Get(3));
                yield return Tap(TerraKeys.Get(TerraKeys.Action.Mount));
                Check("R键下坐骑",!Mounts.Riding);
                Mounts.TryMount(Mounts.Get(3),hero);Mounts.Tick(false,hero);
                Check("退出游戏解除骑乘",!Mounts.Riding && box.size==size);
                Shop.DevStage=0;
                Check("提前取得后仍禁止骑乘",!Mounts.TryMount(Mounts.Get(7),hero));Shop.DevStage=5;
                Check("下车恢复重力",Mathf.Abs(rb.gravityScale-.79f)<.01f);
                // Save all new IDs and the existing misc mount slot through the real mod serializer.
                for(int i=0;i<Mounts.All.Length;i++)mod.Inventory.Slots[i]=ItemStack.Of(Mounts.All[i].Item,1);
                mod.Inventory.Misc[3]=ItemStack.Of(Mounts.Get(3).Item,1);
                Save.ModSave.Write(slot,mod);
                for(int i=0;i<Mounts.All.Length;i++)mod.Inventory.Slots[i]=default;
                mod.Inventory.Misc[3]=default;
                Save.ModSave.Read(slot,mod);
                Check("38件物品和坐骑栏存读档",Mounts.All.Select((m,i)=>mod.Inventory.Slots[i].Type==m.Item).All(v=>v) && mod.Inventory.Misc[3].Type==Mounts.Get(3).Item);
            } finally {Mounts.Dismount();TerraMovement.DevMove=0;TerraMovement.DevJump=false;Shop.DevStage=oldStage;TerraCamera.DevLook=null;}
            yield return MountCombatTest(Check);
            File.WriteAllText(Path.Combine(DevCopy.SaveDir,"mounts.json"),JsonConvert.SerializeObject(new{Checks=checks,Failures=failures,Rows=results},Formatting.Indented));
            Plugin.Log.LogInfo("[AutoTest] 坐骑合计 "+(checks-failures)+"/"+checks+(failures==0?" 通过":" 不通过"));
        }
        IEnumerator MountCombatTest(Action<string,bool> check)
        {
            Shop.DevStage=5;
            yield return GoTo("Crossroads_03","left1");
            var hero=HeroController.instance;var body=hero.GetComponent<Rigidbody2D>();var hb=hero.GetComponent<BoxCollider2D>();
            var seed=UnityEngine.Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None)
                .FirstOrDefault(h=>!h.isDead && h.gameObject.scene.name=="Crossroads_03" && !h.IsInvincible && h.GetComponent<Collider2D>()!=null && ProgressionLoot.Eligible(EnemyDrops.EntryFor(UI.EnemyHover.RecordOf(h))));
            check("攻击测试的原生敌人存在",seed!=null);
            if(seed==null)yield break;
            foreach(var danger in UnityEngine.Object.FindObjectsByType<DamageHero>(FindObjectsSortMode.None))danger.enabled=false;
            var targetObject=UnityEngine.Object.Instantiate(seed.gameObject);
            foreach(var danger in targetObject.GetComponentsInChildren<DamageHero>())danger.enabled=false;
            var target=targetObject.GetComponent<HealthManager>();var targetBody=target.GetComponent<Rigidbody2D>();var tc=target.GetComponent<Collider2D>();
            foreach(var fsm in target.GetComponents<PlayMakerFSM>())fsm.enabled=false;
            var heroAt=hero.transform.position;
            heroAt=Clear(hero,(Vector2)heroAt+Vector2.up*3f);
            foreach(int id in new[]{3,50,10,9,44,46}) {
                Mounts.Dismount();body.linearVelocity=Vector2.zero;
                hero.transform.position=Clear(hero,heroAt);body.position=hero.transform.position;
                target.hp=20000;target.isDead=false;
                var point=(Vector2)hb.bounds.center+Vector2.right*3f;
                Vector2 off=(Vector2)tc.bounds.center-(Vector2)target.transform.position;
                Pin(target,targetBody,point-off);
                yield return new WaitForSeconds(.8f);
                bool mounted=Mounts.TryMount(Mounts.Get(id),hero);check("战斗骑乘/"+id,mounted);
                if(!mounted)continue;
                int life=EnemyStats.Life(target,EnemyStats.Of(target)),made=Mounts.DevShots,hits=Mounts.DevHits;
                if(id==3 || id==50 || id==10) {
                    // The original enemy collider intersects the mount's actual contact region.
                    Vector2 at=id==10 ? (Vector2)hb.bounds.center : new Vector2(hb.bounds.center.x,hb.bounds.min.y+.05f);
                    Pin(target,targetBody,at-off);Physics2D.SyncTransforms();
                    body.linearVelocity=id==10 ? Vector2.right*20f : Vector2.down*10f;
                    bool grounded=hero.cState.onGround;hero.cState.onGround=false;
                    Mounts.FixedStep(hero,body,1.2f);hero.cState.onGround=grounded;
                    check("踩踏冲撞实际伤害/"+id,Mounts.DevHits>hits && EnemyStats.Life(target,EnemyStats.Of(target))<life);
                } else {
                    float until=Time.time+2f;
                    while(Time.time<until){Pin(target,targetBody,(Vector2)hb.bounds.center+Vector2.right*3f-off);yield return null;}
                    check("自动发射/"+id,Mounts.DevShots>made);
                    check("射击实际命中/"+id,EnemyStats.Life(target,EnemyStats.Of(target))<life);
                }
                Plugin.Log.LogInfo("[AutoTest] mount combat "+id+" life="+life+" -> "+EnemyStats.Life(target,EnemyStats.Of(target))+" shots="+(Mounts.DevShots-made)+" hits="+(Mounts.DevHits-hits));
                Mounts.Dismount();
            }
            Mounts.Dismount();UnityEngine.Object.Destroy(targetObject);
        }
    }
}
