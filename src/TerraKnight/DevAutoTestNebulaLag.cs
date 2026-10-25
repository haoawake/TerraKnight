using System;
using System.Collections;
using System.Collections.Generic;
using TerraKnight.Items;
using TerraKnight.Effects;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight.Items
{
    internal sealed partial class TerraProjectiles
    {
        internal bool DevNebulaLifetimeHealthy;
        internal string DevNebulaLifetime(float angle)
        {
            var p=Spawn(ProjKind.NebulaArcanum,new Vector2(1000,1000),new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*4.000001f,0,0,0);
            p.Collide=false;int resets=0;float last=0;int ticks=0;
            while(!p.Dead&&ticks++<1100){Step(p,true);if(p.Ai0<last)resets++;last=p.Ai0;}
            DevNebulaLifetimeHealthy=p.Dead&&ticks<=701;
            return $"angle={angle:F3} ticks={ticks} dead={p.Dead} resets={resets} ai={p.Ai0}";
        }
    }
}
namespace TerraKnight
{
    internal sealed partial class DevAutoTest
    {
        IEnumerator NebulaLagTest()
        {
            if(!DevCopy.Verified)throw new InvalidOperationException("Isolated save required");
            yield return GoTo("Town","left1");yield return new WaitForSecondsRealtime(1f);
            var tp=TerraProjectiles.Instance;PlayerData.instance.isInvincible=true;
            int failed=0,referenceStuck=0;
            for(int i=0;i<3600;i++) {
                float a=i*.001745329f;
                var v=new Vector2(Mathf.Cos(a),Mathf.Sin(a))*4.000001f;float age=0;
                for(int tick=0;tick<1100&&age<600;tick++) {
                    age++;
                    if(v.magnitude>4f) {if(age>=30)v*=.98f;if(v.magnitude<4.1f){v=v.normalized*4;age=0;}}
                }
                if(age<600)referenceStuck++;
            }
            Plugin.Log.LogInfo($"[AutoTest] NEBULA_LAG legacy-float-probe stuck={referenceStuck}/3600");
            for(int i=0;i<24;i++){Plugin.Log.LogInfo("[AutoTest] NEBULA_LAG lifetime "+tp.DevNebulaLifetime((i+.37f)*Mathf.PI/12));if(!tp.DevNebulaLifetimeHealthy)failed++;tp.DevClear();yield return null;}
            Plugin.Log.LogInfo($"[AutoTest] NEBULA_LAG bounded-lifetime {(failed==0?"通过":"不通过")} failures={failed}");
            var particles=TerraParticles.Instance;particles.Clear();
            for(int i=0;i<400;i++)particles.NewDust(Vector2.zero,1,1,86);
            Plugin.Log.LogInfo("[AutoTest] NEBULA_LAG particle-slot-benchmark "+particles.DevSaturationBenchmark(4000));
            long before=particles.DevDustSlotChecks;
            var dustWatch=System.Diagnostics.Stopwatch.StartNew();
            for(int i=0;i<4000;i++)particles.NewDust(Vector2.zero,1,1,86);
            long checks=particles.DevDustSlotChecks-before;
            Plugin.Log.LogInfo($"[AutoTest] NEBULA_LAG saturated-particles {(checks<=400?"通过":"不通过")} slot-checks={checks} requests=4000 time={dustWatch.Elapsed.TotalMilliseconds:F2}ms");
            particles.Clear();
            var fx=FxBatch.Instance;var cam=GameCameras.instance.mainCamera;
            var center=cam.ViewportToWorldPoint(new Vector3(.5f,.5f,Mathf.Abs(cam.transform.position.z)));
            int draw=fx.DevDrawCount;
            fx.Draw(FxBatch.Whole("Projectile_617"),TerraParticles.ToPx(center),Color.white,0f,1f);
            bool visible=fx.DevDrawCount>draw;draw=fx.DevDrawCount;
            fx.Draw(FxBatch.Whole("Projectile_617"),TerraParticles.ToPx((Vector2)center+Vector2.right*1000),Color.white,0f,1f);
            Plugin.Log.LogInfo($"[AutoTest] NEBULA_LAG render-culling {(visible&&fx.DevDrawCount==draw?"通过":"不通过")}");
            draw=fx.DevDrawCount;
            fx.Draw(FxBatch.Frame("Projectile_617",0,0,32,1,0,.5f),TerraParticles.ToPx((Vector2)center-Vector2.right*140),Color.white,0f,160f);
            Plugin.Log.LogInfo($"[AutoTest] NEBULA_LAG offscreen-pivot-visible-tail {(fx.DevDrawCount>draw?"通过":"不通过")}");
            var dummy=TargetDummy.DevHm;
            if(dummy!=null) {
                int hits=TargetDummy.DevHits;
                var target=(Vector2)dummy.GetComponent<Collider2D>().bounds.center;
                tp.Shoot(ProjKind.NebulaArcanum,target,Vector2.right*.01f,100,0,0);
                yield return new WaitForSecondsRealtime(.4f);
                Plugin.Log.LogInfo($"[AutoTest] NEBULA_LAG damage-still-applies {(TargetDummy.DevHits>hits?"通过":"不通过")} hits={TargetDummy.DevHits-hits}");
                tp.DevClear();yield return null;
            } else Plugin.Log.LogError("[AutoTest] NEBULA_LAG damage-target 不通过");
            InventoryGui.Open=false;
            IEnumerator Measure(string label,float seconds,bool fire)
            {
                var frames=new List<float>();float until=Time.realtimeSinceStartup+seconds,next=0;
                int max=0,gc=GC.CollectionCount(0);
                while(Time.realtimeSinceStartup<until){
                    if(fire&&Time.realtimeSinceStartup>=next){next=Time.realtimeSinceStartup+.25f;
                        tp.Shoot(ProjKind.NebulaArcanum,(Vector2)HeroController.instance.transform.position+new Vector2(4,5),new Vector2(4.000001f,0),0,0,0);}
                    max=Math.Max(max,tp.Count);yield return null;frames.Add(Time.unscaledDeltaTime);
                }
                Plugin.Log.LogInfo($"[AutoTest] NEBULA_LAG {label} active={tp.Count} max={max} main={tp.DevCount(ProjKind.NebulaArcanum)} sub={tp.DevCount(ProjKind.NebulaArcanumSub)} shards={tp.DevCount(ProjKind.NebulaArcanumShard)} fx-pool={FxBatch.Instance.DevPoolCount} GC={GC.CollectionCount(0)-gc} {FrameStats(frames)}");
            }
            yield return Measure("idle",2,false);
            yield return Measure("fire-early",15,true);
            yield return Measure("fire-late",15,true);
            yield return Measure("cleanup",15,false);
            Plugin.Log.LogInfo($"[AutoTest] NEBULA_LAG cleanup-complete {(tp.Count==0?"通过":"不通过")} remaining={tp.Count}");
            int drawBefore=fx.DevDrawCount;
            for(int i=0;i<300;i++)fx.Draw(FxBatch.Whole("Projectile_617"),TerraParticles.ToPx(center),Color.white,0f,1f);
            bool burst=fx.DevDrawCount-drawBefore>=300;
            yield return null;int peakPool=fx.DevPoolCount;
            yield return Measure("pool-recovery",14,false);
            Plugin.Log.LogInfo($"[AutoTest] NEBULA_LAG pool-recovery {(burst&&fx.DevPoolCount<peakPool?"通过":"不通过")} peak={peakPool} remaining={fx.DevPoolCount}");
            Shot("Town","nebula_after_cleanup");yield return new WaitForSecondsRealtime(.3f);
            tp.DevClear();
        }
    }
}
