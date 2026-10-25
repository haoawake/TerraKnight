using System.Collections.Generic;
using UnityEngine;

namespace TerraKnight.Effects
{
    /// <summary>Fades registered native nail/ability visuals only during rendering, restoring their
    /// original animated colours afterwards. No alpha accumulation, pooled-object changes or collider changes.</summary>
    internal static class NativeWeaponEffects
    {
        sealed class Entry
        {
            public Renderer Renderer;
            public SpriteRenderer Sprite;
            public tk2dBaseSprite Tk;
            public Color Color;
            public bool Off, Changed, ColorSaved, BlockSaved;
            public int Id;
            public readonly MaterialPropertyBlock Original = new MaterialPropertyBlock();
            public readonly MaterialPropertyBlock Faded = new MaterialPropertyBlock();
        }
        static readonly List<Entry> entries = new List<Entry>();
        static readonly HashSet<int> ids = new HashSet<int>();
        static bool installed;
        static Camera rendering;
        public static int DevTracked => entries.Count;
        internal static void DevApply() => Before(GameCameras.instance.mainCamera);
        internal static void DevRestore() => Restore();
        public static void Track(GameObject root)
        {
            if(root == null) return;
            foreach(var r in root.GetComponentsInChildren<Renderer>(true)) Track(r);
        }
        public static void Track(Renderer renderer)
        {
            if(renderer == null || !ids.Add(renderer.GetInstanceID())) return;
            entries.Add(new Entry { Id=renderer.GetInstanceID(), Renderer=renderer, Sprite=renderer as SpriteRenderer, Tk=renderer.GetComponent<tk2dBaseSprite>() });
            if(installed) return;
            installed=true; Camera.onPreCull+=Before; Camera.onPostRender+=After;
        }
        static void Before(Camera camera)
        {
            if(camera == null || GameCameras.instance == null || camera != GameCameras.instance.mainCamera) return;
            Restore(); rendering=camera;
            float opacity=WeaponEffects.Opacity;
            if(opacity>=1f) return;
            for(int i=entries.Count-1;i>=0;i--)
            {
                var e=entries[i]; var r=e.Renderer;
                if(r==null) { ids.Remove(e.Id); entries.RemoveAt(i); continue; }
                if(!r.enabled || !r.gameObject.activeInHierarchy) continue;
                e.Off=r.forceRenderingOff; e.Changed=true;
                if(opacity<=0f) { r.forceRenderingOff=true; continue; }
                if(e.Sprite!=null) { e.ColorSaved=true; e.Color=e.Sprite.color; e.Sprite.color=WeaponEffects.Straight(e.Color); }
                else if(e.Tk!=null) { e.ColorSaved=true; e.Color=e.Tk.color; e.Tk.color=WeaponEffects.Straight(e.Color); }
                else
                {
                    r.GetPropertyBlock(e.Original); r.GetPropertyBlock(e.Faded);
                    var mat=r.sharedMaterial;
                    string key=mat!=null&&mat.HasProperty("_TintColor")?"_TintColor":mat!=null&&mat.HasProperty("_Color")?"_Color":null;
                    if(key!=null) {
                        int property=Shader.PropertyToID(key);
                        var color=e.Original.HasColor(property)?e.Original.GetColor(property):mat.GetColor(property);
                        e.BlockSaved=true; e.Faded.SetColor(property,WeaponEffects.Straight(color)); r.SetPropertyBlock(e.Faded);
                    }
                }
            }
        }
        static void After(Camera camera) { if(camera==rendering) Restore(); }
        static void Restore()
        {
            foreach(var e in entries)
            {
                if(!e.Changed) continue;
                e.Changed=false; var r=e.Renderer; if(r==null)continue;
                r.forceRenderingOff=e.Off;
                // At zero only forceRenderingOff was modified; colours and property blocks were untouched.
                if(e.Sprite!=null) { if(e.ColorSaved) e.Sprite.color=e.Color; }
                else if(e.Tk!=null) { if(e.ColorSaved) e.Tk.color=e.Color; }
                else if(e.BlockSaved) r.SetPropertyBlock(e.Original);
                e.ColorSaved=e.BlockSaved=false;
            }
            rendering=null;
        }
    }
}
