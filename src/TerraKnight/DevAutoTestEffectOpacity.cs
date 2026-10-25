using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using BepInEx.Configuration;
using Newtonsoft.Json;
using TerraKnight.Effects;
using TerraKnight.Items;
using TerraKnight.UI;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    internal sealed partial class DevAutoTest
    {
        IEnumerator GuardedEffectOpacity()
        {
            var stack=new Stack<IEnumerator>(); stack.Push(EffectOpacityTest());
            try { while(stack.Count>0) {
                var next=stack.Peek(); bool more;
                try { more=next.MoveNext(); } catch(Exception e) { Plugin.Log.LogError("[AutoTest] EFFECT_OPACITY 不通过 "+e); break; }
                if(!more) { (next as IDisposable)?.Dispose(); stack.Pop(); continue; }
                if(next.Current is IEnumerator nested) stack.Push(nested); else yield return next.Current;
            }} finally { while(stack.Count>0) (stack.Pop() as IDisposable)?.Dispose(); }
        }

        IEnumerator EffectOpacityTest()
        {
            if(!DevCopy.Verified) throw new InvalidOperationException("Isolated save required");
            yield return GoTo("Town","left1");
            yield return new WaitForSecondsRealtime(2f);
            var hero=HeroController.instance; PlayerData.instance.isInvincible=true;
            float saved=WeaponEffects.Opacity;
            bool drive=TerraCursor.DevDrives, held=TerraCursor.DevHeld;
            var pointer=TerraCursor.DevAt;
            var fx=FxBatch.Instance;
            bool scope=fx.WeaponEffect;
            int checks=0,failures=0;
            var details=new List<object>();
            void Check(string what,bool pass) { checks++;if(!pass)failures++;Plugin.Log.LogInfo($"[AutoTest] EFFECT_OPACITY {what} {(pass?"通过":"不通过")}"); }
            bool Near(float a,float b)=>Mathf.Abs(a-b)<.015f;
            var native=new GameObject("Opacity native renderer probe");
            var sr=native.AddComponent<SpriteRenderer>(); sr.sprite=FxBatch.Whole("Projectile_1");
            sr.color=new Color(.8f,.6f,.4f,.8f); NativeWeaponEffects.Track(sr);
            try {
                IngameOptions.DevPage(3); IngameOptions.Show();
                yield return new WaitForSecondsRealtime(.5f);
                var bar=IngameOptions.DevEffectSlider;
                Check("slider-visible",bar.width>20f && IngameOptions.DevPanel.Contains(bar.center));
                TerraCursor.DevDrives=true; TerraCursor.DevAt=new Vector2(bar.x+bar.width*.35f,bar.center.y); TerraCursor.DevClick();
                yield return null; yield return null; yield return null;
                Check("slider-click-35",Near(WeaponEffects.Opacity,.35f));
                var reload=new ConfigFile(WeaponEffects.DevConfigPath,false);
                Check("config-reload",Near(reload.Bind("Video","WeaponEffectsOpacity",1f).Value,.35f));
                Shot("Town","effect_opacity_settings");
                IngameOptions.Hide(); TerraCursor.DevDrives=false; TerraCursor.DevAt=null;
                foreach(float opacity in new[]{1f,.35f,0f}) {
                    WeaponEffects.Set(opacity);
                    fx.WeaponEffect=true;
                    int before=fx.DevDrawCount;
                    fx.Draw(sr.sprite,TerraParticles.ToPx(hero.transform.position+Vector3.up*2f),new Color(.8f,.6f,.4f,1f),0f,1f);
                    Check("alpha/"+opacity,opacity==0f?fx.DevDrawCount==before:Near(fx.DevLastAlpha.a,opacity)&&Near(fx.DevLastAlpha.r,.8f));
                    before=fx.DevDrawCount;
                    fx.Draw(sr.sprite,TerraParticles.ToPx(hero.transform.position+Vector3.up*2f),new Color(.8f,.6f,.4f,0f),0f,1f);
                    Check("additive/"+opacity,opacity==0f?fx.DevDrawCount==before:Near(fx.DevLastAdditive.r,.8f*opacity));
                    fx.WeaponEffect=false;
                    fx.Draw(sr.sprite,Vector2.zero,Color.white,0f,1f);
                    Check("non-effect-unaffected/"+opacity,Near(fx.DevLastAlpha.a,1f));
                    NativeWeaponEffects.DevApply();
                    Check("native-fade/"+opacity,opacity==0f?sr.forceRenderingOff:!sr.forceRenderingOff&&Near(sr.color.a,.8f*opacity));
                    NativeWeaponEffects.DevRestore();
                    Check("native-restore/"+opacity,!sr.forceRenderingOff&&Near(sr.color.a,.8f));
                    for(int i=0;i<10;i++) { NativeWeaponEffects.DevApply();NativeWeaponEffects.DevRestore(); }
                    Check("native-no-cumulative-fade/"+opacity,Near(sr.color.a,.8f)&&!sr.forceRenderingOff);
                    details.Add(new{Opacity=opacity,Normal=WeaponEffects.Straight(Color.white).a,Additive=WeaponEffects.Additive(Color.white).r});
                }
                WeaponEffects.Set(.35f);
                IngameOptions.DevPage(3); IngameOptions.Show();yield return new WaitForSecondsRealtime(.1f);
                Check("reopen-retains-setting",Near(WeaponEffects.Opacity,.35f));IngameOptions.Hide();
                WeaponEffects.Set(-1f);Check("lower-bound",WeaponEffects.Opacity==0f);
                WeaponEffects.Set(2f);Check("upper-bound",WeaponEffects.Opacity==1f);
                WeaponEffects.Set(float.NaN);Check("invalid-value",WeaponEffects.Opacity==1f);
                var dummy=TargetDummy.DevHm;
                Check("real-dummy-found",dummy!=null);
                if(dummy!=null) foreach(float opacity in new[]{1f,0f}) {
                    WeaponEffects.Set(opacity);
                    int before=TargetDummy.DevHits;
                    var c=(Vector2)dummy.GetComponent<Collider2D>().bounds.center;
                    TerraProjectiles.Instance.Shoot(ProjKind.Bullet,c-Vector2.right,Vector2.right*8f,100,0f,0f);
                    yield return new WaitForSecondsRealtime(.4f);
                    Check("real-hit-at/"+opacity,TargetDummy.DevHits>before);
                }
                foreach(float opacity in new[]{1f,.35f,0f}) {
                    WeaponEffects.Set(opacity);TerraProjectiles.Instance.DevClear();
                    Vector2 c=hero.GetComponent<BoxCollider2D>().bounds.center;
                    for(int k=0;k<8;k++) {
                        TerraProjectiles.Instance.Shoot(ProjKind.NebulaBlazeBig,c+new Vector2(-3+k*.8f,2.5f),Vector2.up*.1f,100,0f,0f);
                        int d=TerraParticles.Instance.NewDust(TerraParticles.ToPx(c+new Vector2(-2+k*.6f,1.5f)),8,8,6);
                        if(d>=0)TerraParticles.Instance[d].NoGravity=true;
                    }
                    yield return new WaitForSecondsRealtime(.08f);
                    Shot("Town","effect_opacity_"+Mathf.RoundToInt(opacity*100f));
                }
            } finally {
                IngameOptions.Hide();NativeWeaponEffects.DevRestore();
                WeaponEffects.Set(saved);fx.WeaponEffect=scope;
                TerraCursor.DevDrives=drive;TerraCursor.DevHeld=held;TerraCursor.DevAt=pointer;
                UnityEngine.Object.Destroy(native);
            }
            File.WriteAllText(Path.Combine(DevCopy.SaveDir,"effect-opacity.json"),JsonConvert.SerializeObject(new{Checks=checks,Failures=failures,Details=details,Scope="UI slider/config, actual alpha/additive draws, native renderer restoration, real projectile hits at zero opacity. Not a two-client test."},Formatting.Indented));
            Plugin.Log.LogInfo($"[AutoTest] EFFECT_OPACITY {checks} checks {failures} failures {(failures==0?"通过":"不通过")}");
        }
    }
}
