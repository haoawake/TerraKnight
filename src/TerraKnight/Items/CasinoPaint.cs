using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
namespace TerraKnight.Items
{
    // The live IMGUI and the offline visual checks consume these same draw operations.
    internal static class CasinoPaint
    {
        internal sealed class Command {public Rect Rect,Uv;public Texture2D Texture;public Color Color;public string Text;public GUIStyle Style;public float Angle;}
        public static bool Capture;
        static bool captureEnabled=true;
        public static Vector2 Mouse;
        public static Vector2? Click;
        static readonly List<Command> commands=new List<Command>();
        public static bool Enabled {get=>Capture?captureEnabled:GUI.enabled;set{if(Capture)captureEnabled=value;else GUI.enabled=value;}}
        public static void Begin(Vector2? click=null){commands.Clear();Capture=true;captureEnabled=true;Click=click;Mouse=click??new Vector2(-999,-999);}
        public static void End(){Capture=false;Click=null;}
        public static void Image(Texture2D t,Rect r,Color color,Rect? uv=null,float angle=0)
        {
            if(t==null)return;var coords=uv??new Rect(0,0,1,1);
            if(Capture){commands.Add(new Command{Texture=t,Rect=r,Uv=coords,Color=color,Angle=angle});return;}
            var before=GUI.matrix;var previous=GUI.color;GUI.color=color;if(angle!=0)GUIUtility.RotateAroundPivot(angle,r.center);GUI.DrawTextureWithTexCoords(r,t,coords,true);GUI.color=previous;GUI.matrix=before;
        }
        public static void Fill(Rect r,Color c)=>Image(Texture2D.whiteTexture,r,c);
        public static void Text(Rect r,string value,GUIStyle style,Color color)
        {
            if(Capture){commands.Add(new Command{Rect=r,Text=value,Style=style,Color=color});return;}
            var old=GUI.color;GUI.color=color;GUI.Label(r,value,style);GUI.color=old;
        }
        public static void Fit(Texture2D t,Rect r,Color? tint=null)
        {if(t==null)return;float scale=Mathf.Min(r.width/t.width,r.height/t.height);var actual=new Rect(r.center.x-t.width*scale/2,r.center.y-t.height*scale/2,t.width*scale,t.height*scale);Image(t,actual,tint??Color.white);}
        // A pixel 9-slice: corners of cap texels drawn k units a texel, the edges and middle stretched (the pixel art's own frame).
        public static void Pixel9(Texture2D t,Rect r,int cap,float k,Color tint)
        {
            if(t==null)return;float c=Mathf.Min(cap*k,Mathf.Min(r.width,r.height)/2);float u=(float)cap/t.width,v=(float)cap/t.height;
            float[] xs={r.x,r.x+c,r.xMax-c,r.xMax},ys={r.y,r.y+c,r.yMax-c,r.yMax};float[] us={0,u,1-u,1},vs={1,1-v,v,0};
            for(int y=0;y<3;y++)for(int x=0;x<3;x++)if(xs[x+1]>xs[x]&&ys[y+1]>ys[y])Image(t,new Rect(xs[x],ys[y],xs[x+1]-xs[x],ys[y+1]-ys[y]),tint,new Rect(us[x],vs[y+1],us[x+1]-us[x],vs[y]-vs[y+1]));
        }
        public static bool Hit(Rect r)
        {
            if(!Enabled)return false;if(Capture)return Click.HasValue&&r.Contains(Click.Value);
            return GUI.Button(r,GUIContent.none,GUIStyle.none);
        }
        public static void Save(string path,int width=1120,int height=760)
        {
            var root=new GameObject("Casino shared-layout capture");root.layer=30;var canvas=root.AddComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;var rtRoot=root.GetComponent<RectTransform>();rtRoot.pivot=new Vector2(0,1);rtRoot.sizeDelta=new Vector2(width,height);rtRoot.position=Vector3.zero;
            var cg=new GameObject("Casino capture camera");var cam=cg.AddComponent<Camera>();cam.enabled=false;cam.orthographic=true;cam.orthographicSize=height/2f;cam.aspect=(float)width/height;cam.transform.position=new Vector3(width/2f,-height/2f,-10);cam.nearClipPlane=.01f;cam.farClipPlane=100;cam.cullingMask=1<<30;cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=Color.black;canvas.worldCamera=cam;
            var target=new RenderTexture(width,height,24);cam.targetTexture=target;
            try
            {
                foreach(var command in commands)
                {
                    var go=new GameObject(command.Text==null?"painted surface":"casino label");go.layer=30;go.transform.SetParent(root.transform,false);var rect=go.AddComponent<RectTransform>();rect.anchorMin=rect.anchorMax=new Vector2(0,1);rect.pivot=new Vector2(.5f,.5f);rect.anchoredPosition=new Vector2(command.Rect.center.x,-command.Rect.center.y);rect.sizeDelta=command.Rect.size;rect.localRotation=Quaternion.Euler(0,0,-command.Angle);
                    if(command.Text!=null)
                    {
                        var text=go.AddComponent<Text>();text.font=command.Style.font;text.fontSize=command.Style.fontSize;text.fontStyle=command.Style.fontStyle;text.alignment=command.Style.alignment;text.color=command.Color;text.supportRichText=false;text.horizontalOverflow=command.Style.wordWrap?HorizontalWrapMode.Wrap:HorizontalWrapMode.Overflow;text.verticalOverflow=VerticalWrapMode.Overflow;text.raycastTarget=false;
                        text.font.RequestCharactersInTexture(command.Text,text.fontSize,text.fontStyle);text.text=command.Text;
                    }
                    else{var image=go.AddComponent<RawImage>();image.texture=command.Texture;image.uvRect=command.Uv;image.color=command.Color;image.raycastTarget=false;}
                }
                Canvas.ForceUpdateCanvases();Canvas.ForceUpdateCanvases();cam.Render();var before=RenderTexture.active;RenderTexture.active=target;var picture=new Texture2D(width,height,TextureFormat.RGBA32,false);picture.ReadPixels(new Rect(0,0,width,height),0,0);picture.Apply();System.IO.File.WriteAllBytes(path,picture.EncodeToPNG());Object.Destroy(picture);RenderTexture.active=before;
            }
            finally{Object.Destroy(root);Object.Destroy(cg);target.Release();Object.Destroy(target);}
        }
    }
}
