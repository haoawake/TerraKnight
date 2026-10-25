using System.Collections.Generic;
using TerraKnight.UI;
using UnityEngine;

namespace TerraKnight.Items
{
    internal static partial class InventoryGui
    {
        static int trackingScroll;
        static GUIStyle trackingText, trackingTitle;
        static ItemType trackingGuideItem;
        internal static bool DevTrackingActive=>RecipeTracking.Active;
        internal static ItemType DevTrackingType=>RecipeTracking.Current?.Type??ItemType.None;
        internal static int DevTrackingDepth=>RecipeTracking.Path.Count;
        internal static void DevTrackAction(int action)=>TrackAction(action);
        internal static int DevFocusGuideResult(ItemType type)
        {
            RefreshRecipes();int i=recipes.FindIndex(r=>r.Result==type);
            if(i>=0){focus=i;focusY=0f;grid=false;}
            return i;
        }

        static void StartRecipeTracking(Recipe recipe)
        {
            RecipeTracking.Start(recipe);trackingScroll=0;
            trackingGuideItem=Town.TownNpcs.GuideItem.Type;
            grid=false;bestiaryOpen=statsOpen=hkOpen=blacklistOpen=sellMode=false;
            Sound("Menu_Tick");
        }
        static void ClearRecipeTracking() { RecipeTracking.Clear();trackingScroll=0; }
        static void TrackIngredient(int index,bool source)
        {
            var node=RecipeTracking.Current;var r=node?.Recipe;
            if(r==null||index<0||index>=r.Ingredients.Length)return;
            var ingredient=r.Ingredients[index];
            if(!RecipeTracking.SourcesReady&&(source||RecipeTracking.For(ingredient.Type).Count==0))return;
            if(source||RecipeTracking.For(ingredient.Type).Count==0&&RecipeTracking.Farmable(ingredient.Type))OpenTrackedBook(ingredient.Type);
            else { RecipeTracking.Enter(ingredient.Type,RecipeTracking.Needed(node,ingredient.Count));trackingScroll=0;Sound("Menu_Tick"); }
        }
        static void OpenTrackedBook(ItemType type)
        {
            if(!RecipeTracking.Farmable(type))return;
            Open=true;EndBookSearch();ResetBookSearchFilters();SetBookSearch(ItemDb.Get(type).Name);
            bestiaryChosen=null;bestiaryOpen=true;statsOpen=hkOpen=blacklistOpen=sellMode=false;grid=false;
            Sound("Menu_Tick");
        }
        static void TrackAction(int action)
        {
            if(!RecipeTracking.Active)return;
            if(action==0)ClearRecipeTracking();
            else if(action==1)RecipeTracking.Back();
            else if(action==2)RecipeTracking.Jump(0);
            else if(action==3)RecipeTracking.Variant(-1);
            else if(action==4)RecipeTracking.Variant(1);
            else if(action==5)trackingScroll=Mathf.Max(0,trackingScroll-3);
            else if(action==6)trackingScroll+=3;
            else if(action==7)OpenTrackedBook(RecipeTracking.Current.Type);
            else if(action==8){bestiaryOpen=false;EndBookSearch();}
            if(action<5)trackingScroll=0;
            Sound("Menu_Tick");
        }
        static void TrackingButton(Rect rect,string label,int action,Vector2 mouse,float ui)
        {
            bool hover=rect.Contains(mouse);
            Fill(rect,hover?new Color(.3f,.4f,.65f,.98f):new Color(.12f,.17f,.32f,.98f));
            Rim(rect,new Color(.45f,.55f,.8f),ui);
            Outlined(rect,label,trackingText,hover?new Color(1f,.86f,.3f):Color.white);
            Add(rect,Kind.RecipeTraceAction,action);
        }
        static void TrackingStyles(float ui)
        {
            if(trackingText==null){trackingText=new GUIStyle(GUI.skin.label){fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleLeft,wordWrap=true};trackingTitle=new GUIStyle(trackingText);}
            trackingText.fontSize=Mathf.RoundToInt(19f*ui);trackingTitle.fontSize=Mathf.RoundToInt(25f*ui);
        }
        static void DrawTrackingReturn(Rect panel,float ui,Vector2 mouse)
        {
            if(!RecipeTracking.Active)return;
            TrackingStyles(ui);
            var rect=new Rect(panel.x,panel.y-36f*ui,250f*ui,30f*ui);
            panels.Add(rect);TrackingButton(rect,Lang.T("← 返回材料追踪"),8,mouse,ui);
        }
        static void DrawRecipeTracking(Rect view,float ui,Vector2 mouse,ref ItemStack hovered,ref string hoveredExtra,ref Kind hoveredKind)
        {
            if(!RecipeTracking.Active||bestiaryOpen||statsOpen||hkOpen||blacklistOpen)return;
            if(Town.TownNpcs.GuideCraftOpen&&Town.TownNpcs.GuideItem.Type!=trackingGuideItem){ClearRecipeTracking();return;}
            TrackingStyles(ui);
            float w=Mathf.Min(900f*ui,view.width-40f*ui),h=Mathf.Min(690f*ui,view.height-140f*ui);
            var panel=new Rect(view.xMax-w-20f*ui,view.y+100f*ui,w,h);
            panels.Add(panel);Add(panel,Kind.RecipeTracePanel,0);
            Fill(panel,new Color(.07f,.1f,.23f,.98f));Rim(panel,new Color(.45f,.55f,.85f),2f*ui);
            if(panel.Contains(mouse)){hovered=default;hoveredExtra=null;}
            float x=panel.x+20f*ui,y=panel.y+12f*ui,iw=panel.width-40f*ui;
            var node=RecipeTracking.Current;
            Outlined(new Rect(x,y,iw-55f*ui,35f*ui),Lang.F("材料追踪：{0} ×{1}",ItemDb.Get(node.Type).Name,node.Required),trackingTitle,Color.white);
            TrackingButton(new Rect(panel.xMax-45f*ui,y,30f*ui,30f*ui),"×",0,mouse,ui);y+=43f*ui;
            var names=new List<string>();int from=Mathf.Max(0,RecipeTracking.Path.Count-4);
            if(from>0)names.Add("…");for(int i=from;i<RecipeTracking.Path.Count;i++)names.Add(ItemDb.Get(RecipeTracking.Path[i].Type).Name);
            Outlined(new Rect(x,y,iw,34f*ui),string.Join(" → ",names),trackingText,new Color(.7f,.8f,1f));y+=40f*ui;
            TrackingButton(new Rect(x,y,145f*ui,32f*ui),Lang.T("← 上一级"),1,mouse,ui);
            TrackingButton(new Rect(x+155f*ui,y,145f*ui,32f*ui),Lang.T("返回目标"),2,mouse,ui);
            if(RecipeTracking.Farmable(node.Type))TrackingButton(new Rect(x+310f*ui,y,190f*ui,32f*ui),Lang.T("查图鉴来源"),7,mouse,ui);
            y+=46f*ui;
            var r=node.Recipe;
            if(r==null){
                if(!RecipeTracking.SourcesReady){Outlined(new Rect(x,y,iw,80f*ui),Lang.T("正在检索掉落来源…"),trackingText,Color.white);return;}
                string source=Town.TownNpcs.RecipeSources(node.Type);
                if(string.IsNullOrEmpty(source))source=ItemDb.Get(node.Type).Source==ItemSource.Shop?Lang.T("此物品通过商店获取。"):
                    Lang.T("暂无合成配方或怪物掉落记录，请查看物品说明及商店、采集等获取途径。");
                Outlined(new Rect(x,y,iw,130f*ui),source,trackingText,new Color(1f,.86f,.5f));y+=140f*ui;
                Outlined(new Rect(x,y,iw,panel.yMax-y-20f*ui),Lang.T(ItemDb.Get(node.Type).Tooltip??""),trackingText,Color.white);return;
            }
            TrackingButton(new Rect(x,y,50f*ui,32f*ui),"‹",3,mouse,ui);
            Outlined(new Rect(x+65f*ui,y,iw-130f*ui,32f*ui),Lang.F("配方 {0}/{1} · 每次产出 {2}",node.Variant+1,node.Options.Count,r.Count),trackingText,Color.white);
            TrackingButton(new Rect(panel.xMax-70f*ui,y,50f*ui,32f*ui),"›",4,mouse,ui);y+=40f*ui;
            string station=string.IsNullOrEmpty(r.Station)?Lang.T("徒手"):Lang.T(r.Station);
            if(r.Environment!=CraftEnvironment.None)station+=" · "+Lang.T(CraftEnvironments.Description(r.Environment));
            Outlined(new Rect(x,y,iw,42f*ui),Lang.F("制作站：{0}",station),trackingText,new Color(.8f,.9f,1f));y+=48f*ui;
            float rowH=55f*ui;
            int visible=Mathf.Max(1,Mathf.FloorToInt((panel.yMax-y-100f*ui)/rowH));
            trackingScroll=Mathf.Clamp(trackingScroll,0,Mathf.Max(0,r.Ingredients.Length-visible));
            var near=World.Chests.ForCrafting(HeroController.SilentInstance);
            for(int i=trackingScroll;i<r.Ingredients.Length&&i<trackingScroll+visible;i++){
                var g=r.Ingredients[i];int needed=RecipeTracking.Needed(node,g.Count),have=Recipes.Count(inv,g.Type,near);
                if(Town.TownNpcs.GuideItem.Type==g.Type)have+=Town.TownNpcs.GuideItem.Count;
                bool craft=RecipeTracking.For(g.Type).Count>0,drop=RecipeTracking.Farmable(g.Type);
                var rowRect=new Rect(x,y,iw,rowH-5f*ui);
                Fill(rowRect,rowRect.Contains(mouse)?new Color(.25f,.3f,.48f):new Color(.12f,.16f,.28f));
                Add(rowRect,Kind.RecipeTraceIngredient,i);
                var icon=new Rect(x+4f*ui,y+3f*ui,40f*ui,40f*ui);DrawIcon(icon,ItemStack.Of(g.Type),false);
                Outlined(new Rect(x+52f*ui,y,iw-245f*ui,45f*ui),Lang.F("{0}  已有 {1} / 需要 {2}",ItemDb.Get(g.Type).Name,have,needed),trackingText,have>=needed?new Color(.6f,1f,.65f):Color.white);
                string action=craft?Lang.T("看下位"):!RecipeTracking.SourcesReady?Lang.T("正在检索掉落来源…"):drop?Lang.T("查图鉴"):Lang.T("获取说明");
                Outlined(new Rect(panel.xMax-195f*ui,y,170f*ui,45f*ui),action,trackingText,new Color(.8f,.85f,1f));
                if(craft&&drop){var lookup=new Rect(panel.xMax-110f*ui,y+8f*ui,80f*ui,30f*ui);Fill(lookup,new Color(.2f,.3f,.45f));Outlined(lookup,Lang.T("图鉴"),trackingText,Color.white);Add(lookup,Kind.RecipeTraceSource,i);}
                if(rowRect.Contains(mouse)){hovered=ItemStack.Of(g.Type);hoveredKind=Kind.RecipeTraceIngredient;hoveredExtra=Lang.T("点击继续追踪；有图鉴按钮时可直接查掉落来源。");}
                y+=rowH;
            }
            float footer=panel.yMax-90f*ui;
            if(r.Ingredients.Length>visible){TrackingButton(new Rect(x,footer,90f*ui,30f*ui),"↑",5,mouse,ui);TrackingButton(new Rect(x+100f*ui,footer,90f*ui,30f*ui),"↓",6,mouse,ui);}
            Outlined(new Rect(x,footer+35f*ui,iw,48f*ui),Lang.T("已有量包含背包、向导槽和附近箱子。点击材料逐层查看；图鉴只显示已解锁的掉落资料。"),trackingText,new Color(.65f,.75f,.9f));
        }
    }
}
