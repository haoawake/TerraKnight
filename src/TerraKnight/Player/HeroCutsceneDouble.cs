using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace TerraKnight.Player
{
    /// <summary>The pickup's independent Knight (e.g. Knight Get Fireball), not HeroController's renderer.</summary>
    [HarmonyPatch]
    internal sealed class HeroCutsceneDouble : MonoBehaviour
    {
        static readonly HashSet<HeroCutsceneDouble> live = new HashSet<HeroCutsceneDouble>();
        tk2dSpriteAnimator animator;
        tk2dBaseSprite sourceSprite;
        Renderer original;
        SpriteRenderer replacement;
        float footOffset=-.8f, faceSign=1f;
        int code=-1;
        public bool Visible => replacement!=null && replacement.enabled && gameObject.activeInHierarchy;
        public int PoseCode => code;

        public static bool IsDouble(Transform t)
        {
            for(var p=t;p!=null;p=p.parent)
                if(p.name.StartsWith("Knight Get ",System.StringComparison.Ordinal) || p.name.StartsWith("Knight Dummy",System.StringComparison.Ordinal))return true;
            return false;
        }
        [HarmonyPostfix, HarmonyPatch(typeof(tk2dSpriteAnimator),"OnEnable")]
        static void Enabled(tk2dSpriteAnimator __instance)
        {
            if(!IsDouble(__instance.transform) || __instance.GetComponent<HeroCutsceneDouble>()!=null)return;
            __instance.gameObject.AddComponent<HeroCutsceneDouble>();
        }
        void Awake()
        {
            animator=GetComponent<tk2dSpriteAnimator>(); sourceSprite=GetComponent<tk2dBaseSprite>();original=GetComponent<Renderer>();
            var hero=HeroController.SilentInstance;
            if(hero!=null){var box=hero.GetComponent<BoxCollider2D>();if(box!=null)footOffset=box.bounds.min.y-hero.transform.position.y;
                if(Mathf.Abs(hero.transform.lossyScale.x)>.01f)faceSign=(hero.cState.facingRight?1:-1)/Mathf.Sign(hero.transform.lossyScale.x);}
            replacement=new GameObject("Terrarian cutscene replacement").AddComponent<SpriteRenderer>();
            replacement.transform.SetParent(transform,false);replacement.enabled=false;
            if(original!=null){replacement.sortingLayerID=original.sortingLayerID;replacement.sortingOrder=original.sortingOrder;original.forceRenderingOff=true;}
        }
        void OnEnable()=>live.Add(this);
        void OnDisable(){live.Remove(this);if(replacement!=null)replacement.enabled=false;}
        void LateUpdate()
        {
            if(original==null || animator==null)return;
            original.forceRenderingOff=true;
            var clip=animator.CurrentClip;
            int kind=3,frame=0;
            if(clip!=null && HeroSpecialAnimations.Clips.TryGetValue(clip.name,out var range))
            {kind=range.Kind;frame=HeroSpecialAnimations.FrameFor(range,HeroSpecialAnimations.NativeFrame(range,animator,clip),clip.frames.Length);}
            else if(clip!=null && name.StartsWith("Knight Dummy",System.StringComparison.Ordinal))
            {
                var r=clip.name=="Fall"?new HeroSpecialAnimations.Range(0,10,10)
                    :clip.name=="Shake"||clip.name=="Shaking"?new HeroSpecialAnimations.Range(2,3,5)
                    :clip.name=="Rise"?new HeroSpecialAnimations.Range(3,3,7):new HeroSpecialAnimations.Range(1,0,9);
                kind=r.Kind;frame=HeroSpecialAnimations.FrameFor(r,animator.CurrentFrame,clip.frames.Length);
            }
            else if(clip!=null && clip.name.IndexOf("Get Fireball",System.StringComparison.OrdinalIgnoreCase)>=0)
            {kind=0;frame=Mathf.RoundToInt(Mathf.Clamp01(animator.CurrentFrame/(float)Mathf.Max(1,clip.frames.Length-1))*11);}
            code=HeroSpecialAnimations.Encode(kind,frame);
            replacement.sprite=HeroSpecialAnimations.Frame(kind,frame);
            replacement.transform.position=new Vector3(transform.position.x,transform.position.y+footOffset,transform.position.z-.0001f);
            replacement.transform.rotation=Quaternion.identity;
            var scale=transform.lossyScale;
            replacement.transform.localScale=new Vector3(Mathf.Abs(scale.x)>.001f?1f/scale.x:1,Mathf.Abs(scale.y)>.001f?1f/scale.y:1,Mathf.Abs(scale.z)>.001f?1f/scale.z:1);
            replacement.flipX=transform.lossyScale.x*faceSign<0;
            replacement.color=new Color(.86f,.86f,.9f,sourceSprite!=null?sourceSprite.color.a:1f);
            replacement.enabled=original.enabled;
        }
        /// <summary>Dev: the shown double's pose code, clip and feet (the animreal recordings).</summary>
        internal static bool DevShown(out int pose,out string clip,out Vector3 feet)
        {
            pose=-1;clip="-";feet=Vector3.zero;
            foreach(var actor in live)
            {
                if(actor==null || !actor.Visible)continue;
                pose=actor.code;feet=actor.replacement.transform.position;
                var c=actor.animator!=null?actor.animator.CurrentClip:null;
                clip=actor.name+":"+(c!=null?c.name+"#"+actor.animator.CurrentFrame:"-");
                return true;
            }
            return false;
        }
        public static bool TryNetPose(HeroController hero,out Net.PoseRecord pose)
        {
            pose=default;
            if(hero==null)return false;
            foreach(var actor in live)
            {
                if(actor==null || !actor.Visible || actor.gameObject.scene.name!=GameManager.UnsafeInstance?.sceneName || actor.replacement.color.a<=.01f)continue;
                pose=new Net.PoseRecord {Shown=true,Feet=actor.replacement.transform.position,Facing=actor.replacement.flipX?-1:1,
                    Opacity=actor.replacement.color.a,BodyOverride=actor.code,LegOverride=0,Swing=-1,ItemScale=1,Mount=-1,Wing=-1,
                    Box=hero.GetComponent<BoxCollider2D>().bounds};
                return true;
            }
            return false;
        }
    }
}
