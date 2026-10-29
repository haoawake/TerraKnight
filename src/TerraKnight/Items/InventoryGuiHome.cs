using System.Collections.Generic;
using TerraKnight.World;
using TerraKnight.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TerraKnight.Items
{
    internal static partial class InventoryGui
    {
        static bool homePanel,homeEditing,homeRenaming;
        static string homeEditKey,homeNameDraft="";
        static int homeStoragePage;
        static readonly ImeTextEntry homeNameInput=new ImeTextEntry(c=>{if(homeRenaming&&!char.IsControl(c)&&homeNameDraft.Length<24)homeNameDraft+=c;});
        static bool HomeTyping => homeRenaming || homeItemSearchTyping || Time.frameCount-homeRenameEnded<=1;
        static int homeRenameEnded=-20;
        public static bool HomePanelOpen => homePanel;
        public static void OpenHomeSettings(string key)
        {
            if(Stations.Home(key)==null)return;
            EndHomeSearch();
            Open=true;Town.TownNpcs.Close(false);EndRename(false);EndBookSearch();
            homePanel=homeEditing=true;homeEditKey=key;homeRenaming=false;homeNameInput.End();
            homeNameDraft=Stations.Home(key).Name??"";
            Sound("Menu_Open");
        }
        static void CloseHomePanel()
        { homePanel=homeEditing=homeRenaming=false;homeEditKey=null;homeNameInput.End();EndHomeSearch(); }
        static void ToggleHomePanel()
        {
            homePanel=!homePanel;homeEditing=false;homeRenaming=false;homeNameInput.End();
            if(!homePanel)Stations.LeaveHome();
            else
            {
                Town.TownNpcs.Close(false);hkOpen=statsOpen=blacklistOpen=sellMode=bestiaryOpen=grid=false;
                // Straight into the home opened last, or the only one: no colour to pick each time the bag opens.
                var homes=Stations.Homes();
                string key=Stations.Home(Stations.LastHome)!=null?Stations.LastHome:homes.Count==1?homes[0].Key:null;
                if(key!=null&&Stations.SelectHome(key)){homeStoragePage=0;homeItemSearch="";}
            }
            Sound("Menu_Tick");
        }
        /// <summary>A home right-clicked in the world: the bag opened on its storage.</summary>
        public static void OpenHome(string key)
        {
            if(Stations.Home(key)==null)return;
            EndHomeSearch();
            Open=true;Town.TownNpcs.Close(false);EndRename(false);EndBookSearch();
            hkOpen=statsOpen=blacklistOpen=sellMode=bestiaryOpen=grid=false;
            homePanel=true;homeEditing=homeRenaming=false;homeEditKey=null;homeNameInput.End();
            if(Stations.SelectHome(key)){homeStoragePage=0;homeItemSearch="";}
            Sound("Menu_Open");
        }
        static void ClickHomeColor(int color,bool right)
        {
            if(homeEditing)
            {
                var home=Stations.Home(homeEditKey);
                if(home!=null&&Stations.ChangeHome(home.Key,color,home.Name))Sound("Menu_Tick");
                return;
            }
            foreach(var home in Stations.Homes())if(home.Color==color)
            {
                if(right){OpenHomeSettings(home.Key);return;}
                if(Stations.SelectHome(home.Key)) {homeStoragePage=0;homeItemSearch="";EndHomeSearch();Sound("Menu_Open");}
                return;
            }
        }
        static void ActHomeButton(int index)
        {
            if(index==0){CloseHomePanel();Stations.LeaveHome();return;}
            if(index==1){homeEditing=false;homeRenaming=false;homeNameInput.End();return;}
            if(index==2)
            {
                var home=Stations.Home(homeEditKey);if(home==null)return;
                homeNameDraft=home.Name??"";homeRenaming=true;homeNameInput.Begin();return;
            }
            if(index==3){SaveHomeName();return;}
            if(index==4){homeStoragePage=Mathf.Max(0,homeStoragePage-1);return;}
            if(index==5){homeStoragePage++;return;}
            if(index==6&&Stations.HomeActive){OpenHomeSettings(Stations.SelectedHome);return;}
            // One click: the bag quick-stacked onto every home's storages, all colours.
            if(index==7){Sound(Chests.QuickStackHomes(inv)>0?"Grab":"Menu_Tick");return;}
            if(index==8){homeItemSearch="";homeStoragePage=0;return;}
            if(index==9){Sound(Chests.PutHome()?"Grab":"Menu_Tick");return;}
            if(index==10){if(Chests.SortHome()){homeStoragePage=0;Sound("Grab");}return;}
            if(index==11){Sound(Chests.DepositAllHome()?"Grab":"Menu_Tick");return;}
        }
        static void SaveHomeName()
        {
            var home=Stations.Home(homeEditKey);
            if(home!=null)Stations.ChangeHome(home.Key,home.Color,homeNameDraft);
            homeNameInput.End();homeRenaming=false;homeRenameEnded=Time.frameCount;
        }
        static void TickHomeRename()
        {
            if(!Open){CloseHomePanel();return;}
            TickHomeSearch();
            if(!homeRenaming)return;
            if(Stations.Home(homeEditKey)==null){CloseHomePanel();return;}
            homeNameInput.Tick();var kb=Keyboard.current;
            if(kb==null||homeNameInput.EditingComposition)return;
            homeNameDraft=homeNameInput.TakeBackspaces(homeNameDraft);
            if(kb.enterKey.wasPressedThisFrame||kb.numpadEnterKey.wasPressedThisFrame)SaveHomeName();
            else if(kb.escapeKey.wasPressedThisFrame){homeRenaming=false;homeNameInput.End();homeRenameEnded=Time.frameCount;}
        }
        static void HomeButton(Rect r,string title,int action,Vector2 mouse,string tip=null)
        {
            Add(r,Kind.HomeAction,action);
            JournalNine(r,"UI/PanelBackground",8,6f*GameView.Scale,r.Contains(mouse)?new Color(.23f,.29f,.46f):new Color(.13f,.17f,.3f));
            JournalNine(r,"UI/PanelBorder",8,6f*GameView.Scale,new Color(.65f,.71f,.88f,.7f));
            Outlined(r,Lang.T(title),HomeTextStyle(true),Color.white);
            if(tip!=null&&r.Contains(mouse))DrawLines(mouse,new List<string>{Lang.T(tip)},Color.white);
        }
        internal static void DevHomeSelect(string key) { Open=true;homePanel=true;homeEditing=false;Stations.SelectHome(key); }
        internal static bool DevHomeClick(string kind,int index,bool right=false)
        {
            foreach(var h in hits)if(h.Kind.ToString()==kind&&h.Index==index)
            {Act(h,!right,right,!right,right);return true;}return false;
        }
    }
}
