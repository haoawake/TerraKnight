using System;
using System.Collections.Generic;
using TerraKnight.Items;
using TerraKnight.Net;
using UnityEngine;

namespace TerraKnight.World
{
    internal sealed partial class Stations
    {
        public const int HomeColorCount = 64;
        public const int HomeRadius = 10;
        public static string SelectedHome { get; private set; }
        /// <summary>The home opened last (kept when the bag closes): the home button goes straight back into it.</summary>
        public static string LastHome { get; private set; }
        static Sprite HomeSprite;
        public sealed class HomeInfo
        {
            public string Key, Scene, Name;
            public Vector2 Local;
            public int Color, Order;
            public string Title => string.IsNullOrEmpty(Name) ? Lang.F("家园 {0}", Order) : Name;
        }
        static void AddHomeSpec() => specs[StationKind.HomeEverywhere] = new Spec { W = 3, H = 3, Heights = new[] {16,16,16}, Item = ItemType.HomeEverywhere, Counts = Array.Empty<string>() };
        static string HomeKey(Station s) => WorldSync.Key("s", s.Scene, s.Local);
        public static Color HomeColor(int index)
        {
            index = Mathf.Clamp(index,0,63);
            // Four rings of sixteen hues: exactly 64 stable entries, independent of locale or saves.
            return Color.HSVToRGB((index%16)/16f, index/16%2 == 0 ? .85f : .48f, index/32 == 0 ? 1f : .66f);
        }
        public static List<HomeInfo> Homes()
        {
            RepairHomeColors();
            var result = new List<HomeInfo>();
            foreach (var s in stations) if (s.Kind == StationKind.HomeEverywhere)
                result.Add(new HomeInfo { Key=HomeKey(s), Scene=s.Scene, Local=s.Local, Color=s.HomeColor, Name=s.HomeName, Order=s.HomeOrder });
            result.Sort((a,b) => a.Order.CompareTo(b.Order));
            return result;
        }
        public static HomeInfo Home(string key)
        {
            if (key == null) return null;
            foreach(var s in stations)if(s.Kind==StationKind.HomeEverywhere&&HomeKey(s)==key)
                return new HomeInfo {Key=key,Scene=s.Scene,Local=s.Local,Color=s.HomeColor,Name=s.HomeName,Order=s.HomeOrder};
            return null;
        }
        public static bool HomeActive => SelectedHome != null && Home(SelectedHome) != null;
        public static bool HasHomes { get { foreach(var s in stations) if(s.Kind==StationKind.HomeEverywhere)return true; return false; } }
        public static bool SelectHome(string key)
        {
            if (Home(key)==null || !WorldSync.CanInteract) return false;
            Chests.Close(false); SelectedHome=LastHome=key;
            InventoryGui.Open=true;
            return true;
        }
        public static void LeaveHome() { Chests.CloseHome(); SelectedHome=null; }
        public static int FreeHomeColor(string except=null)
        {
            var used=new bool[64];
            foreach(var s in stations) if(s.Kind==StationKind.HomeEverywhere && HomeKey(s)!=except && s.HomeColor>=0 && s.HomeColor<64)used[s.HomeColor]=true;
            for(int i=0;i<64;i++)if(!used[i])return i;
            return -1;
        }
        static void RepairHomeColors()
        {
            if(WorldSync.Guest)return;
            int order=0;foreach(var s in stations)if(s.Kind==StationKind.HomeEverywhere)order=Math.Max(order,s.HomeOrder);
            var used=new HashSet<int>();
            foreach(var s in stations)if(s.Kind==StationKind.HomeEverywhere)
            {
                if(s.HomeOrder<=0)s.HomeOrder=++order;
                if(s.HomeColor<0||s.HomeColor>=64||!used.Add(s.HomeColor))
                {
                    int c=0;while(c<64&&used.Contains(c))c++;
                    s.HomeColor=c<64?c:-1;if(c<64)used.Add(c);
                }
            }
        }
        public static bool ColorAvailable(int color,string except)
        {
            if(color<0||color>=64)return false;
            foreach(var s in stations)if(s.Kind==StationKind.HomeEverywhere && HomeKey(s)!=except && s.HomeColor==color)return false;
            return true;
        }
        public static bool ChangeHome(string key,int color,string name)
        {
            if(!WorldSync.CanInteract || Home(key)==null || !ColorAvailable(color,key))return false;
            name=(name??"").Trim();if(name.Length>24)name=name.Substring(0,24);
            if(WorldSync.Guest) { WorldSync.Submit(new WorldSync.Action { Op="homeStyle",Key=key,Kind=color,Name=name });return true; }
            return ApplyHomeStyle(key,color,name);
        }
        internal static bool ApplyHomeStyle(string key,int color,string name)
        {
            if(!ColorAvailable(color,key))return false;
            foreach(var s in stations)if(s.Kind==StationKind.HomeEverywhere&&HomeKey(s)==key)
            { s.HomeColor=color;s.HomeName=(name??"").Trim();if(s.HomeName.Length>24)s.HomeName=s.HomeName.Substring(0,24);return true; }
            return false;
        }
        public static Rect HomeBounds(HomeInfo home)
            => HomeBounds(home.Local);
        static Rect HomeBounds(Vector2 local)
        {
            var centre=local+Vector2.up*(1.5f*CellGrid.CellSize);
            float half=(HomeRadius+.5f)*CellGrid.CellSize;
            return new Rect(centre.x-half,centre.y-half,half*2,half*2);
        }
        internal static bool AnyHomeContains(string scene, Rect localBox)
        {
            foreach(var home in stations)
                if(home.Kind==StationKind.HomeEverywhere && home.Scene==scene && HomeBounds(home.Local).Overlaps(localBox))return true;
            return false;
        }
        static void DrawHomeConnection(Station s)
        {
            // The Piggy Bank is no home's storage (the user's 10-30 "让“哪都是家”去掉储钱罐的识别"); the other banks are.
            bool connectable=(s.Spec.Counts!=null&&s.Spec.Counts.Length>0) || s.Kind==StationKind.Safe
                || s.Kind==StationKind.DefendersForge || s.Kind==StationKind.VoidVault;
            var box=new Rect(s.Local.x-s.Spec.W*CellGrid.CellSize*.5f,s.Local.y,s.Spec.W*CellGrid.CellSize,s.Spec.H*CellGrid.CellSize);
            HomeConnectionHighlight.Draw(ref s.HomeHighlight,s.Renderer,connectable&&AnyHomeContains(s.Scene,box));
        }
        public static bool HomeContains(string key,string scene,Rect localBox)
        {
            var home=Home(key);
            return home!=null&&scene==home.Scene&&HomeBounds(home).Overlaps(localBox);
        }
        public static void HomeStations(string key,ICollection<string> into)
        {
            var home=Home(key);if(home==null)return;
            foreach(var s in stations)
                if(HomeContains(key,s.Scene,new Rect(s.Local.x-s.Spec.W*CellGrid.CellSize*.5f,s.Local.y,s.Spec.W*CellGrid.CellSize,s.Spec.H*CellGrid.CellSize)))
                    foreach(var name in s.Spec.Counts)if(!into.Contains(name))into.Add(name);
        }
        internal static IEnumerable<(int Kind,ItemType Item)> HomeBanks(string key)
        {
            foreach(var s in stations)
                if((s.Kind==StationKind.Safe||s.Kind==StationKind.DefendersForge||s.Kind==StationKind.VoidVault)
                    &&HomeContains(key,s.Scene,new Rect(s.Local.x-s.Spec.W*CellGrid.CellSize*.5f,s.Local.y,s.Spec.W*CellGrid.CellSize,s.Spec.H*CellGrid.CellSize)))
                    yield return(s.Kind==StationKind.PiggyBank?0:s.Kind==StationKind.Safe?1:s.Kind==StationKind.DefendersForge?2:3,s.Spec.Item);
        }
        static void DrawHomeAura(Station s)
        {
            if(s.Kind!=StationKind.HomeEverywhere)return;
            if(s.Aura!=null)
            {
                UnityEngine.Object.Destroy(s.Aura.sharedMaterial);UnityEngine.Object.Destroy(s.Aura.gameObject);s.Aura=null;
            }
            if(s.HomeBoundary==null)s.HomeBoundary=SoftHomeBoundary.Create(s.Renderer);
            s.HomeBoundary.Tint=HomeColor(s.HomeColor);
        }
        public static List<string> SelectedHomeStations()
        { var names=new List<string>();HomeStations(SelectedHome,names);return names; }
        /// <summary>Dev: every crafting station's footprint (cells across and up) and the recipe names it counts as.</summary>
        internal static IEnumerable<(StationKind Kind,int W,int H,string[] Counts)> DevCraftingSpecs()
        {
            foreach(var kv in specs)
                if(kv.Key!=StationKind.HomeEverywhere&&kv.Value.Counts!=null&&kv.Value.Counts.Length>0)yield return(kv.Key,kv.Value.W,kv.Value.H,kv.Value.Counts);
        }
    }

    internal sealed partial class Chests
    {
        static void DrawHomeConnection(Chest c)
        {
            var box=new Rect(c.Local.x-WidthOf(c.Style)/2,c.Local.y,WidthOf(c.Style),Height);
            HomeConnectionHighlight.Draw(ref c.HomeHighlight,c.Renderer,!c.Bank&&Stations.AnyHomeContains(c.Scene,box));
        }
        public sealed class HomeStorage
        {
            public string Key, Title;
            public ItemStack[] Items;
            public int Bank=-1;
            public ItemType With;
        }
        public static List<HomeStorage> HomeStorages(string key)
        {
            var list=new List<HomeStorage>();
            if(Stations.Home(key)==null)return list;
            foreach(var c in chests)
                if(!c.Bank&&HomeContainsChest(key,c))list.Add(new HomeStorage {Key=Net.WorldSync.Key("c",c.Scene,c.Local),Title=string.IsNullOrEmpty(c.Name)?ItemDb.Get(RegionalChests.ItemOf(c.Style)).Name:c.Name,Items=c.Items});
            var used=new HashSet<int>();
            foreach(var bank in Stations.HomeBanks(key))if(used.Add(bank.Kind))
                list.Add(new HomeStorage {Key="bank:"+bank.Kind,Title=ItemDb.Get(bank.Item).Name,Items=TerraMod.Instance.Inventory.BankOf(bank.Kind),Bank=bank.Kind,With=bank.Item});
            return list;
        }
        static bool HomeContainsChest(string key,Chest c) => Stations.HomeContains(key,c.Scene,new Rect(c.Local.x-WidthOf(c.Style)/2,c.Local.y,WidthOf(c.Style),Height));
        public static bool HomeCanUseChest(string home,string chest)
        { var c=chests.Find(x=>Net.WorldSync.Key("c",x.Scene,x.Local)==chest);return c!=null&&HomeContainsChest(home,c); }
        public static bool HomeOpenStorage(int index)
        {
            var list=HomeStorages(Stations.SelectedHome);if(index<0||index>=list.Count)return false;
            var storage=list[index];Close(false);
            if(storage.Bank>=0)
            {
                var home=Stations.Home(Stations.SelectedHome);if(home==null)return false;
                Open(new Chest(storage.Items){Bank=true,BankKind=storage.Bank,OpenedWith=storage.With,Remote=true,Scene=home.Scene,Local=home.Local});
            }
            else
            {
                var c=chests.Find(x=>Net.WorldSync.Key("c",x.Scene,x.Local)==storage.Key);if(c==null||!CraftsFrom(c))return false;
                c.Remote=true;Open(c);
            }
            return true;
        }
        public static bool HomeRemoteOpen => open!=null&&open.Remote;
        public static void CloseHome() { if(HomeRemoteOpen)Close(false); }
        public static void HomeCrafting(List<ItemStack[]> into)
        {
            foreach(var storage in HomeStorages(Stations.SelectedHome))
            {
                if(storage.Bank<0) {var c=chests.Find(x=>x.Items==storage.Items);if(c==null||!CraftsFrom(c))continue;}
                if(!into.Contains(storage.Items))into.Add(storage.Items);
            }
        }
    }
}
