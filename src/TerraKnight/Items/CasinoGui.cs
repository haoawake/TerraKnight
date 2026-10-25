using System;
using TerraKnight.Town;
using TerraKnight.World;
using UnityEngine;
namespace TerraKnight.Items
{
    // The casino's window: which machine is open, the stake, the choice, the keypad; the machine itself is drawn full screen
    // (CasinoScenes.cs). While it is open the Knight stands still (the maps' hold, UI.MapHold).
    internal static partial class Casino
    {
        static Game openGame;static StationKind machineKind;static Rect machineBox;static string scene;
        static float animationStart,animationDuration;static long animationMoney;static long[] animationBalls;
        internal static int DevDrawFrames;
        static long stake=100;static int denomination=-1;static Colour choice;
        // The round whose end the window still has to show.
        static long pendingShow=-1;
        static bool holding;
        enum Pad { None,Amount }
        static Pad pad;static string padDigits="",padError="";
        static readonly Color Gold=new Color(1,.84f,.51f),Muted=new Color(.69f,.76f,.80f),Green=new Color(.39f,.95f,.66f);
        public static bool IsOpen=>openGame!=Game.None;
        public static bool Busy=>IsOpen&&Time.realtimeSinceStartup<animationStart+animationDuration;
        public static float AnimationProgress=>animationDuration<=0?1:Mathf.Clamp01((Time.realtimeSinceStartup-animationStart)/animationDuration);
        public static bool OpenFor(StationKind kind,Rect box)
        {
            if(!IsMachine(kind)||TerraMod.Instance==null||!TerraMod.Instance.Inventory.Mouse.IsEmpty)return false;
            machineKind=kind;machineBox=box;var cell=CellGrid.WorldToCell(box.center);scene=TerraWorld.RegionAt(cell.x,cell.y)?.SceneName;openGame=GameOf(kind);animationDuration=0;pad=Pad.None;Notice="";
            if(denomination<0)denomination=0;pendingShow=-1;SceneOpened();
            if(!Access()){Close();return false;}TownNpcs.Close(true);InventoryGui.Open=false;Chests.Close(false);UI.FullMap.Close();
            var hero=HeroController.SilentInstance;if(hero!=null&&!holding){UI.MapHold.Take(hero);holding=true;}
            TerraMod.Instance?.PlaySound(kind==StationKind.CasinoSlots?"tk_casino_open_slots":kind==StationKind.CasinoRoulette?"tk_casino_open_wheel":"tk_casino_open_pinball",.9f);
            return true;
        }
        static bool Access()
        {
            var hero=HeroController.SilentInstance;if(!IsOpen||hero==null||hero.cState.dead||TerraMod.Instance==null||!TerraMod.Instance.Inventory.Mouse.IsEmpty||!Net.WorldSync.CanInteract)return false;
            if(!Stations.DevReach(hero).Overlaps(machineBox))return false;var cell=CellGrid.WorldToCell(machineBox.center);if(TerraWorld.RegionAt(cell.x,cell.y)?.SceneName!=scene)return false;
            foreach(var s in Stations.Standing(k=>k==machineKind))if((s.Box.center-machineBox.center).sqrMagnitude<.0001f)return true;return false;
        }
        public static void Close()
        {
            bool was=IsOpen;openGame=Game.None;animationDuration=0;pad=Pad.None;if(was)SceneClosed();
            if(holding){holding=false;UI.MapHold.Give(HeroController.SilentInstance);}
        }
        public static void Escape(){if(pad!=Pad.None)pad=Pad.None;else Close();}
        static void BeginAnimation(float seconds)
        {
            animationStart=Time.realtimeSinceStartup;animationDuration=seconds;CasinoShow.Start(machineKind,scene,machineBox,Snapshot());animationMoney=Available+(openGame==Game.Pinball?0:state.LastStake-state.LastPayout);animationBalls=(long[])state.Balls.Clone();
            pendingShow=state.Sequence;
        }
        public static void Tick(){CasinoShow.Tick();SaveIfDue();if(IsOpen&&(!Access()||GameManager.instance==null||GameManager.instance.isPaused))Close();}
        static void OpenPad(Pad goal){pad=goal;padDigits="";padError="";}
        internal static void DevCapture(string path){CasinoPaint.Begin();try{DrawScene();CasinoPaint.Save(path,Screen.width,Screen.height);}finally{CasinoPaint.End();}}
        internal static void DevUiClick(Vector2 point){CasinoPaint.Begin(point);try{DrawScene();}finally{CasinoPaint.End();}}
        internal static long DevStake=>stake;
        // The open pinball's next marble struck, as its handle would.
        internal static bool DevStrike(double force)=>pinMachine!=null&&CasinoPinball.Strike(pinMachine,force);
        internal static bool DevKeypadOpen=>pad!=Pad.None;
        internal static string DevPadDigits=>padDigits;
    }
}
