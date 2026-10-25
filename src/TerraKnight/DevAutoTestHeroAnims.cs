using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using Newtonsoft.Json;
using TerraKnight.Player;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TerraKnight
{
    internal sealed partial class DevAutoTest
    {
        IEnumerator GuardedHeroExport()
        {
            var work=HeroAnimExport();
            try {
                while(true){bool more;try{more=work.MoveNext();}catch(Exception ex){Plugin.Log.LogError("[AutoTest] HERO_EXPORT 不通过: "+ex);break;}
                    if(!more)break;yield return work.Current;}
            }finally{(work as IDisposable)?.Dispose();}
        }
        IEnumerator GuardedHeroCheck()
        {
            var work=HeroAnimCheck();
            try {
                while(true){bool more;try{more=work.MoveNext();}catch(Exception ex){Plugin.Log.LogError("[AutoTest] HERO_ANIM 不通过: "+ex);break;}
                    if(!more)break;yield return work.Current;}
            }finally{(work as IDisposable)?.Dispose();}
        }
        IEnumerator HeroAnimCheck()
        {
            yield return GoTo("Town","left1");
            yield return new WaitForSeconds(3f);
            var hero=HeroController.instance;
            var anim=hero.GetComponent<tk2dSpriteAnimator>();
            var local=(TerraPlayerSprite)AccessTools.Field(typeof(TerraMod),"playerSprite").GetValue(mod);
            var peer=TerraPlayerSprite.Create(mod.transform);
            int checks=0, failures=0;
            void Check(string label,bool pass) { checks++; if(!pass)failures++; Plugin.Log.LogInfo($"[AutoTest] HERO_ANIM {label} {(pass?"通过":"不通过")}"); }
            var manifest=new List<object>();
            try
            {
                for(int kind=0;kind<HeroSpecialAnimations.Sheets.Length;kind++)
                    for(int frame=0;frame<HeroSpecialAnimations.Counts[kind];frame++)
                    {
                        int code=HeroSpecialAnimations.Encode(kind,frame);
                        var picture=HeroSpecialAnimations.Frame(kind,frame);
                        Check($"resource/{kind}/{frame}", picture!=null && picture.rect.width==96 && picture.rect.height==96);
                        using(var bytes=new MemoryStream()) {
                            var state=new Net.PlayerState { Scene="Town",Body=code,Leg=-1,Wing=-1,Mount=-1 };
                            var writer=new BinaryWriter(bytes); state.Write(writer); writer.Flush(); bytes.Position=0;
                            var received=Net.PlayerState.Read(new BinaryReader(bytes));
                            Check($"packet/{kind}/{frame}",received.Body==code && bytes.Position==bytes.Length);
                            peer.Pose((Vector2)hero.transform.position+Vector2.right*4,Vector2.zero,true,-1,-1,null,1,1,received.Body,default,0);
                            Check($"peer-render/{kind}/{frame}",peer.SpecialPoseActive && peer.GetComponentsInChildren<SpriteRenderer>().Count(r=>r.enabled&&r.gameObject.activeInHierarchy)==1);
                        }
                    }
                peer.gameObject.SetActive(false);
                hero.RelinquishControl(); hero.StopAnimationControl();
                foreach(var entry in HeroSpecialAnimations.Clips)
                {
                    var clip=anim.GetClipByName(entry.Key);
                    Check("native-clip/"+entry.Key,clip!=null && clip.frames.Length>0);
                    if(clip==null || clip.frames.Length==0)continue;
                    anim.Play(clip);anim.Pause();
                    foreach(int frame in new[]{0,clip.frames.Length-1}) {
                        AccessTools.Field(typeof(tk2dSpriteAnimator),"clipTime").SetValue(anim,(float)frame);
                        anim.SetFrame(frame,false);
                        int code=HeroSpecialAnimations.Code(hero);
                        int expected=HeroSpecialAnimations.Encode(entry.Value.Kind,HeroSpecialAnimations.FrameFor(entry.Value,frame,clip.frames.Length));
                        Check("native-frame/"+entry.Key+"/"+frame,code==expected);
                    }
                    manifest.Add(new { NativeClip=entry.Key, NativeFrames=clip.frames.Length, NativeFps=clip.fps,
                        NativeWrap=clip.wrapMode.ToString(),Sheet=HeroSpecialAnimations.Sheets[entry.Value.Kind],First=entry.Value.First,Last=entry.Value.Last });
                }
                foreach(var name in new[]{"Collect Magical 2","Prostrate Rise","Focus","Collect Normal 2","Map Idle","Roar Lock","DG Warp Charge"}) {
                    var clip=anim.GetClipByName(name);anim.Play(clip);anim.Pause();
                    AccessTools.Field(typeof(tk2dSpriteAnimator),"clipTime").SetValue(anim,(float)(clip.frames.Length/2));anim.SetFrame(clip.frames.Length/2,false);
                    yield return new WaitForSeconds(.15f);
                    Check("local-render/"+name,local.SpecialPoseActive && local.GetComponentsInChildren<SpriteRenderer>().Count(r=>r.enabled&&r.gameObject.activeInHierarchy)==1);
                    Shot("Town","heroanim_"+Safe(name));yield return null;
                }
                anim.Play("Idle"); anim.Pause();yield return new WaitForSeconds(.15f);
                Check("restore-normal-layers",!local.SpecialPoseActive && local.GetComponentsInChildren<SpriteRenderer>().Count(r=>r.enabled&&r.gameObject.activeInHierarchy)>1);
                peer.gameObject.SetActive(true);
                peer.Pose(hero.transform.position,Vector2.zero,true,1,-1,null,1,1,-1,default,0);
                Check("peer-restore-normal-layers",!peer.SpecialPoseActive && peer.GetComponentsInChildren<SpriteRenderer>().Count(r=>r.enabled&&r.gameObject.activeInHierarchy)>1);
                var renderGo=new GameObject("Special animation validation camera");
                var renderCam=renderGo.AddComponent<Camera>(); renderCam.enabled=false; renderCam.orthographic=true;
                renderCam.orthographicSize=32f/World.CellVisuals.PixelsPerUnit;
                renderCam.transform.position=new Vector3(0,26f/World.CellVisuals.PixelsPerUnit,-20);
                renderCam.cullingMask=1<<30;renderCam.clearFlags=CameraClearFlags.SolidColor;renderCam.backgroundColor=Color.clear;
                foreach(var tr in peer.GetComponentsInChildren<Transform>(true))tr.gameObject.layer=30;
                for(int k=0;k<HeroSpecialAnimations.Sheets.Length;k++)
                {
                    peer.Pose(Vector2.zero,Vector2.zero,true,1,-1,null,1,1,HeroSpecialAnimations.Encode(k,HeroSpecialAnimations.Counts[k]/2),default,0);
                    var active=RenderTexture.active;var target=new RenderTexture(256,256,24,RenderTextureFormat.ARGB32);var picture=new Texture2D(256,256,TextureFormat.RGBA32,false);
                    try {
                        renderCam.targetTexture=target;renderCam.Render();RenderTexture.active=target;
                        picture.ReadPixels(new Rect(0,0,256,256),0,0);picture.Apply();
                        int opaque=picture.GetPixels32().Count(p=>p.a>128);Check("gpu-render/"+k,opaque>100);
                        File.WriteAllBytes(Path.Combine(DevCopy.SaveDir,"render-"+HeroSpecialAnimations.Sheets[k]+".png"),picture.EncodeToPNG());
                    }finally{RenderTexture.active=active;renderCam.targetTexture=null;target.Release();Object.Destroy(target);Object.Destroy(picture);}
                }
                Object.Destroy(renderGo);
                peer.gameObject.SetActive(false);
                foreach(var f in hero.GetComponents<PlayMakerFSM>()) if(f.FsmName.Contains("Spell"))DumpFsm(f);
                anim.Resume();hero.RegainControl();hero.StartAnimationControl();
                yield return GoTo("Crossroads_ShamanTemple",EntryGateOf("Crossroads_ShamanTemple"));
                foreach(var f in Object.FindObjectsByType<PlayMakerFSM>(FindObjectsInactive.Include,FindObjectsSortMode.None))
                    if(f.gameObject.scene.name=="Crossroads_ShamanTemple" && f.name.Contains("Vengeful"))DumpFsm(f);
                yield return new WaitForSeconds(2f);
                var pickup=Object.FindObjectsByType<PlayMakerFSM>(FindObjectsInactive.Include,FindObjectsSortMode.None)
                    .FirstOrDefault(f=>f.gameObject.scene.name=="Crossroads_ShamanTemple" && f.FsmName=="Vengeful Spirit");
                Check("real-pickup-fsm-found",pickup!=null);
                if(pickup!=null)
                {
                    PlayerData.instance.fireballLevel=0;
                    pickup.gameObject.SetActive(true);yield return null;
                    pickup.SetState("Get");
                    float until=Time.realtimeSinceStartup+22f,pressAt=Time.realtimeSinceStartup+7f;
                    var codes=new HashSet<int>();bool sawHiddenNative=false,sawRemote=false;int holdSamples=0,holdErrors=0,duplicateSamples=0;var pictures=new HashSet<int>();
                    while(Time.realtimeSinceStartup<until)
                    {
                        foreach(var actor in Object.FindObjectsByType<HeroCutsceneDouble>(FindObjectsSortMode.None))
                        {
                            if(!actor.Visible)continue;codes.Add(actor.PoseCode);
                            // A stand-in left on where the orb was, away from the Knight lying again (Prostrate), is no second human there.
                            if(local.gameObject.activeInHierarchy && Vector2.Distance(actor.transform.position,hero.transform.position)<=3f)duplicateSamples++;
                            if(actor.GetComponent<tk2dSpriteAnimator>().CurrentClip?.name=="Collect Magical 2"){holdSamples++;if(actor.PoseCode!=HeroSpecialAnimations.Encode(0,6))holdErrors++;}
                            var raw=actor.GetComponent<Renderer>();if(raw!=null && raw.forceRenderingOff)sawHiddenNative=true;
                            if(pictures.Count<6 && pictures.Add(actor.PoseCode))Shot("Crossroads_ShamanTemple","real_absorb_"+pictures.Count);
                        }
                        if(HeroCutsceneDouble.TryNetPose(hero,out var forwarded) && forwarded.Shown)sawRemote=true;
                        if(Time.realtimeSinceStartup>pressAt){pressAt=Time.realtimeSinceStartup+1;yield return Tap(UnityEngine.InputSystem.Key.Space,.04f);yield return Tap(UnityEngine.InputSystem.Key.Enter,.04f);}
                        yield return new WaitForSecondsRealtime(.1f);
                    }
                    Check("real-pickup-terraria-body-animates",codes.Count>=3);
                    Check("real-pickup-native-double-hidden",sawHiddenNative);
                    Check("real-pickup-pose-forwarding",sawRemote);
                    Check("real-pickup-hold-still",holdSamples>=3 && holdErrors==0);
                    Check("real-pickup-only-one-human",duplicateSamples==0);
                    Check("real-pickup-native-reward-preserved",PlayerData.instance.fireballLevel>=1);
                    File.WriteAllText(Path.Combine(DevCopy.SaveDir,"real-absorption-check.json"),JsonConvert.SerializeObject(new{HoldSamples=holdSamples,HoldErrors=holdErrors,DuplicateSamples=duplicateSamples,Codes=codes,NativeDoubleHidden=sawHiddenNative,PoseForwarding=sawRemote,FireballLevel=PlayerData.instance.fireballLevel},Formatting.Indented));
                }
                var doubles=new List<object>();
                foreach(string room in new[]{"Crossroads_ShamanTemple","Ruins1_24","Mines_31","Abyss_10","Abyss_12","Abyss_21","Waterways_13","Room_Fungus_Shaman","Dream_Nailcollection"})
                {
                    var scene=UnityEngine.SceneManagement.SceneManager.GetSceneByName(room);bool added=!scene.isLoaded;
                    if(added && !Application.CanStreamedLevelBeLoaded(room)){Plugin.Log.LogWarning("[AutoTest] Missing optional scene: "+room);continue;}
                    if(added){var load=UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(room,UnityEngine.SceneManagement.LoadSceneMode.Additive);while(load!=null && !load.isDone)yield return null;}
                    foreach(var a in Object.FindObjectsByType<tk2dSpriteAnimator>(FindObjectsInactive.Include,FindObjectsSortMode.None))
                    {
                        if(a==null || a.gameObject.scene.name!=room || a.Library==null || !PathOf(a.transform).Contains("Knight"))continue;
                        var clips=a.Library.clips.Where(c=>c!=null && c.frames.Length>0).Select(c=>new{Name=c.name,Fps=c.fps,Count=c.frames.Length}).ToArray();
                        doubles.Add(new{Scene=room,Path=PathOf(a.transform),Replacement=HeroCutsceneDouble.IsDouble(a.transform),Clips=clips});
                    }
                    if(added){var unload=UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync(room);while(unload!=null && !unload.isDone)yield return null;}
                }
                File.WriteAllText(Path.Combine(DevCopy.SaveDir,"cutscene-double-audit.json"),JsonConvert.SerializeObject(doubles,Formatting.Indented));
                File.WriteAllText(Path.Combine(DevCopy.SaveDir,"hero-animation-mapping.json"),JsonConvert.SerializeObject(manifest,Formatting.Indented));
                File.WriteAllText(Path.Combine(DevCopy.SaveDir,"hero-animation-check.json"),JsonConvert.SerializeObject(new{Checks=checks,Failures=failures,Scope="Actual renderer, native clips, local/peer render, packet round trip, real Vengeful Spirit pickup and 9-scene double audit. Not a two-client session."},Formatting.Indented));
            }
            finally { anim.Resume();hero.RegainControl();hero.StartAnimationControl();Object.Destroy(peer.gameObject); }
        }

        IEnumerator HeroAnimExport()
        {
            var hero = HeroController.instance;
            var animator = hero.GetComponent<tk2dSpriteAnimator>();
            var library = animator.Library;
            string dir = Path.Combine(DevCopy.SaveDir, "hero-animation-reference");
            Directory.CreateDirectory(dir);
            var records = library.clips.Where(c => c != null).Select(c => new {
                name = c.name, fps = c.fps, wrapMode = c.wrapMode.ToString(), loopStart = c.loopStart,
                frames = c.frames.Select(f => new { sprite = f.spriteCollection.spriteDefinitions[f.spriteId].name, id = f.spriteId }).ToArray()
            }).ToArray();
            File.WriteAllText(Path.Combine(dir, "native-clips.json"), JsonConvert.SerializeObject(records, Formatting.Indented));
            var cameraGo = new GameObject("Hero animation export camera");
            Object.DontDestroyOnLoad(cameraGo);
            var camera = cameraGo.AddComponent<Camera>();
            camera.enabled = false; camera.orthographic = true; camera.orthographicSize = 2.5f;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.clear;
            camera.cullingMask = 1 << 30; camera.nearClipPlane = .01f; camera.farClipPlane = 100;
            var rt = new RenderTexture(256, 256, 24, RenderTextureFormat.ARGB32);
            var pixels = new Texture2D(256,256,TextureFormat.RGBA32,false);
            var nativeGo = new GameObject("Native hero reference"); nativeGo.layer = 30;
            Object.DontDestroyOnLoad(nativeGo);
            var native = nativeGo.AddComponent<tk2dSprite>();
            var person = TerraPlayerSprite.Create(mod.transform);
            foreach (var t in person.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 30;
            person.gameObject.SetActive(false);
            void Save(string file, Vector3 centre, float halfHeight)
            {
                var previous = RenderTexture.active;
                try {
                    camera.transform.position = new Vector3(centre.x,centre.y,-20);
                    camera.orthographicSize = halfHeight;
                    camera.targetTexture = rt;
                    camera.Render(); RenderTexture.active = rt;
                    pixels.ReadPixels(new Rect(0,0,256,256),0,0); pixels.Apply();
                    File.WriteAllBytes(file,pixels.EncodeToPNG());
                } finally { RenderTexture.active = previous; camera.targetTexture = null; }
            }
            try
            {
                foreach (var clip in library.clips.Where(c => c != null &&
                    (c.name.IndexOf("Collect",StringComparison.OrdinalIgnoreCase)>=0 || c.name.IndexOf("Get",StringComparison.OrdinalIgnoreCase)>=0
                    || c.name.IndexOf("Focus",StringComparison.OrdinalIgnoreCase)>=0 || c.name.IndexOf("Roar",StringComparison.OrdinalIgnoreCase)>=0
                    || c.name.IndexOf("Sit",StringComparison.OrdinalIgnoreCase)>=0 || c.name.IndexOf("Wake",StringComparison.OrdinalIgnoreCase)>=0
                    || c.name.IndexOf("Dream",StringComparison.OrdinalIgnoreCase)>=0)))
                {
                    string sub = Path.Combine(dir,"native-frames",Safe(clip.name)); Directory.CreateDirectory(sub);
                    for(int i=0;i<clip.frames.Length;i++) {
                        var frame=clip.frames[i]; native.SetSprite(frame.spriteCollection,frame.spriteId);
                        Save(Path.Combine(sub,i.ToString("000")+".png"),Vector3.zero,2.5f);
                        if(i%12==0) yield return null;
                    }
                }
                nativeGo.SetActive(false); person.gameObject.SetActive(true);
                for(int frame=0;frame<7;frame++) {
                    person.Pose(Vector2.zero,Vector2.zero,true,1,-1,null,1,1,frame,default,0);
                    Save(Path.Combine(dir,"terrarian-default-"+frame+".png"),new Vector3(0,24f/World.CellVisuals.PixelsPerUnit,0),28f/World.CellVisuals.PixelsPerUnit);
                }
                yield return GoTo("Crossroads_ShamanTemple",EntryGateOf("Crossroads_ShamanTemple"));
                foreach(var f in Object.FindObjectsByType<PlayMakerFSM>(FindObjectsInactive.Include,FindObjectsSortMode.None))
                    if(f.gameObject.scene.name=="Crossroads_ShamanTemple" && (f.name.Contains("Spell") || f.FsmName.Contains("Spell") || f.name.Contains("Shaman"))) DumpFsm(f);
                var doubles=new List<object>();
                person.gameObject.SetActive(false);nativeGo.SetActive(true);
                foreach(string room in new[]{"Crossroads_ShamanTemple","Ruins1_24","Mines_31","Abyss_10","Abyss_12","Abyss_21","Waterways_13","Room_Fungus_Shaman","Dream_Nailcollection"})
                {
                    var scene=UnityEngine.SceneManagement.SceneManager.GetSceneByName(room);bool added=!scene.isLoaded;
                    if(added && !Application.CanStreamedLevelBeLoaded(room)){Plugin.Log.LogWarning("[AutoTest] Missing optional scene: "+room);continue;}
                    if(added){var load=UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(room,UnityEngine.SceneManagement.LoadSceneMode.Additive);while(load!=null && !load.isDone)yield return null;}
                    foreach(var a in Object.FindObjectsByType<tk2dSpriteAnimator>(FindObjectsInactive.Include,FindObjectsSortMode.None))
                    {
                        if(a==null || a.gameObject.scene.name!=room || a==animator || a.Library==null)continue;
                        string path=PathOf(a.transform);
                        if(!path.Contains("Knight") && !path.Contains("Get Fireball"))continue;
                        var clips=a.Library.clips.Where(c=>c!=null && c.frames.Length>0).ToArray();
                        doubles.Add(new{Scene=room,Path=path,Clips=clips.Select(c=>new{Name=c.name,Fps=c.fps,Count=c.frames.Length,Wrap=c.wrapMode.ToString()}).ToArray()});
                        File.WriteAllText(Path.Combine(dir,"cutscene-doubles.json"),JsonConvert.SerializeObject(doubles,Formatting.Indented));
                        Plugin.Log.LogInfo($"[AutoTest] HERO_DOUBLE {room}/{path}: {string.Join(",",clips.Select(c=>c.name+"="+c.frames.Length))}");
                        if(clips.Length>25)continue; // A full hero library is already exported above.
                        foreach(var c in clips)
                        {
                            string sub=Path.Combine(dir,"cutscene-doubles",Safe(room),Safe(path),Safe(c.name));Directory.CreateDirectory(sub);
                            for(int i=0;i<c.frames.Length;i++){
                                var frame=c.frames[i];native.SetSprite(frame.spriteCollection,frame.spriteId);
                                Save(Path.Combine(sub,i.ToString("000")+".png"),Vector3.zero,2.5f);
                                if(i%12==0)yield return null;
                            }
                        }
                    }
                    if(added){var unload=UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync(room);while(unload!=null && !unload.isDone)yield return null;}
                }
                File.WriteAllText(Path.Combine(dir,"cutscene-doubles.json"),JsonConvert.SerializeObject(doubles,Formatting.Indented));
                Plugin.Log.LogInfo($"[AutoTest] HERO_ANIM_EXPORT {records.Length} native clips exported to {dir}");
            }
            finally {
                Object.Destroy(person.gameObject); Object.Destroy(nativeGo); Object.Destroy(cameraGo);
                Object.Destroy(pixels); rt.Release(); Object.Destroy(rt);
            }
        }
    }
}
