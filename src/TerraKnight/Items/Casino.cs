using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// Personal casino ledger. A slot spin or a turn of the wheel settles and persists before its presentation starts; a
    /// pinball marble settles when it falls into its channel (its flight is the machine's real motion, CasinoPinSim), its
    /// launch saved before anything of where it goes can be seen.
    /// </summary>
    internal static partial class Casino
    {
        public const int HouseArea=25;
        public const long HousePrice=500000; // 5000 geo.
        public const long MaxBetGeo=1000000,MaxBalls=1000000000,MaxCredit=100000000000000000;
        // A stake in copper: from 100 geo (from 1 geo all in) up to anything anyone carries.
        public const long MinStake=10000,MaxStake=1000000000000000;
        public const int MaxMultiplier=1000,WheelSectors=65;
        public static readonly long[] Denominations={100,500,1000,5000,10000,100000};
        public static readonly long[] Stakes={100,500,1000,5000,10000};
        // The wheel's chips, in geo.
        public static readonly long[] Chips={100,500,1000,5000,10000,100000};
        // The slot: three reels, each its own order of the five symbols, a 3x3 window; five lines across it (the middle, top
        // and bottom rows, the two diagonals), the stake split over them. A line pays three of a symbol; the middle line also
        // its first two alike, a consolation (the user's, 2026-10-23: something back now and then, the best pair the stake
        // back). In quarters of a line's bet: 95% back in all; a third of the spins give something back, one in five more
        // than the stake.
        public static readonly int[][] Strips={new[]{0,1,2,3,4},new[]{0,1,2,4,3},new[]{0,2,1,3,4}};
        public static readonly int[][] Lines={new[]{0,0,0},new[]{-1,-1,-1},new[]{1,1,1},new[]{-1,0,1},new[]{1,0,-1}};
        public static readonly int[] TripleQuarters={30,44,66,108,187},PairQuarters={4,6,8,12,20};
        // The pinball: what each of the eleven channels gives back of a marble's value, in tenths (PinSim's paytable). Where
        // a marble falls is the machine's physics (CasinoPinSim): every marble down the funnel and off the windmills, so the
        // force of the launch hardly changes where it lands; the cup pays three times or brings two more marbles; a marble
        // settles with all that came of it. Measured with auto-fire, about 95% back (a marble at a time somewhat less),
        // one marble in five more than its value.
        public static readonly int[] ChannelTenths=PinSim.ChannelTenths;
        public const int Channels=PinSim.Channels,AllIn=6,MostLoaded=28;
        public enum Game { None,Slots,Pinball,Roulette }
        public enum Colour { Red,Black,Green }
        // A marble in the pinball: loaded (Force below 0; the all-in one paid when it is struck) or struck, flying.
        [Serializable] public sealed class Marble
        {
            public long Id;
            public int Denomination;
            public long Price;
            public uint Seed;
            public double Force=-1,Clock;
            public string Scene="";
            public float X,Y;
            public Marble Copy()=>(Marble)MemberwiseClone();
        }
        [Serializable] public sealed class Saved
        {
            public int Version=1;
            public long[] Balls=new long[6];
            public long Credit,Sequence,NextMarble=1;
            public ulong RandomState;
            public int LastGame,LastMultiplier=1,LastDenomination,LastWheel,LastColour;
            public int[] Reels=new int[3];
            public long LastStake,LastPayout;
            public bool LastWin,LastAllIn;
            public string LastMessage="欢迎光临，所有倍率均含本金。";
            public List<Marble> Machine=new List<Marble>();
        }
        static Saved state=new Saved();
        public static Saved Snapshot()
        {
            var machine=new List<Marble>(state.Machine.Count);foreach(var m in state.Machine)machine.Add(m.Copy());
            return new Saved{Balls=(long[])state.Balls.Clone(),Credit=state.Credit,Sequence=state.Sequence,NextMarble=state.NextMarble,RandomState=state.RandomState,
                LastGame=state.LastGame,LastMultiplier=state.LastMultiplier,LastDenomination=state.LastDenomination,LastWheel=state.LastWheel,LastColour=state.LastColour,
                Reels=(int[])state.Reels.Clone(),LastStake=state.LastStake,LastPayout=state.LastPayout,LastWin=state.LastWin,LastAllIn=state.LastAllIn,LastMessage=state.LastMessage,Machine=machine};
        }
        public static long Balls(int denomination)=>denomination>=0&&denomination<6?state.Balls[denomination]:0;
        public static long Credit=>state.Credit;
        public static long Sequence=>state.Sequence;
        public static Saved Last=>Snapshot();
        public static long Available=>Shop.Purse+state.Credit;
        public static string Notice { get; private set; }="";
        public static void Load(Saved saved)
        {
            dirty=false;launchUnsaved=false;Close();state=saved??new Saved();
            var balls=new long[6];if(state.Balls!=null)Array.Copy(state.Balls,balls,Math.Min(6,state.Balls.Length));
            for(int i=0;i<6;i++)balls[i]=Math.Max(0,Math.Min(MaxBalls,balls[i]));state.Balls=balls;
            state.Credit=Math.Max(0,Math.Min(MaxCredit,state.Credit));state.Sequence=Math.Max(0,Math.Min(long.MaxValue-1,state.Sequence));
            if(state.Reels==null||state.Reels.Length!=3)state.Reels=new int[3];for(int i=0;i<3;i++)state.Reels[i]=Mathf.Clamp(state.Reels[i],0,4);
            state.LastGame=Mathf.Clamp(state.LastGame,0,3);
            // The wheel's number, or the pinball's last family's winnings (tenths).
            state.LastMultiplier=Mathf.Clamp(state.LastMultiplier,1,MaxMultiplier);state.LastWheel=Mathf.Clamp(state.LastWheel,0,state.LastGame==(int)Game.Pinball?PinSim.MostTenths:WheelSectors-1);state.LastColour=Mathf.Clamp(state.LastColour,0,2);state.LastDenomination=Mathf.Clamp(state.LastDenomination,0,AllIn);
            if(state.RandomState==0){var bytes=new byte[8];using(var random=RandomNumberGenerator.Create())random.GetBytes(bytes);state.RandomState=BitConverter.ToUInt64(bytes,0)|1UL;}
            if(state.Machine==null)state.Machine=new List<Marble>();
            state.Machine.RemoveAll(m=>m==null||m.Denomination<0||m.Denomination>AllIn||m.Price<0||(m.Denomination<AllIn&&m.Price!=Denominations[m.Denomination]*100));
            foreach(var m in state.Machine){if(m.Id<=0||m.Id>=state.NextMarble)m.Id=state.NextMarble++;if(double.IsNaN(m.Force)||m.Force>1)m.Force=-1;if(m.Scene==null)m.Scene="";}
            Notice="";dirty=false;launchUnsaved=false;CasinoPinball.Clear();
            Landfall();
        }
        // Marbles still flying when the game last saved fall now, as they were flying: the same launches run again.
        static void Landfall()
        {
            var flying=state.Machine.FindAll(m=>m.Force>=0);if(flying.Count==0)return;
            var launches=new List<(double,uint,double)>();foreach(var m in flying)launches.Add((m.Force,m.Seed,m.Clock));
            var into=PinSim.Replay(launches);long back=0;int fell=0;
            for(int i=0;i<flying.Count;i++)
            {
                var m=flying[i];if(into[i]<0){m.Force=-1;continue;}
                state.Machine.Remove(m);long got=TenthsPayout(m.Price,into[i]);state.Credit=Math.Min(MaxCredit,state.Credit+got);back+=got;fell++;
            }
            if(fell>0){state.LastMessage=$"上次还在弹珠机里的 {fell} 颗弹珠已经落下，返还 {Money(back)}。";Notice=state.LastMessage;dirty=true;}
        }
        public static void Clear(){CasinoShow.Clear();Load(null);}
        static uint NextUInt()
        {
            unchecked {ulong x=state.RandomState;if(x==0)x=0x9E3779B97F4A7C15UL;x^=x>>12;x^=x<<25;x^=x>>27;state.RandomState=x;return(uint)((x*2685821657736338717UL)>>32);}
        }
        static int Next(int count)
        {
            uint bound=(uint)count,threshold=unchecked(0u-bound)%bound,r;do{r=NextUInt();}while(r<threshold);return(int)(r%bound);
        }
        public static long SlotPayout(long copper,int a,int b,int c)
        {
            if(copper<=0||a<0||a>4||b<0||b>4||c<0||c>4)return 0;
            int q=0;for(int l=0;l<Lines.Length;l++)q+=LineQuarters(l,a,b,c);
            return checked(copper*q/20);
        }
        public static Colour WheelColour(int number)=>number==0?Colour.Green:number<=32?Colour.Red:Colour.Black;
        public static long WheelPayout(long copper,Colour chosen,int number)=>number>=0&&number<WheelSectors&&WheelColour(number)==chosen
            ?checked(copper*(chosen==Colour.Green?6300:197)/100):0;
        // The symbol a reel shows at a row of the window (-1 top, 0 middle, 1 bottom) when it stops at a place on its strip.
        public static int SymbolAt(int reel,int stop,int row)=>Strips[reel][((stop+row)%5+5)%5];
        // What a line pays, in quarters of its bet: three of one symbol; on the middle line, else its first two alike.
        public static int LineQuarters(int line,int a,int b,int c)
        {
            var l=Lines[line];int x=SymbolAt(0,a,l[0]),y=SymbolAt(1,b,l[1]),z=SymbolAt(2,c,l[2]);
            return x==y&&y==z?TripleQuarters[x]:line==0&&x==y?PairQuarters[x]:0;
        }
        public static bool LineTriple(int line,int a,int b,int c){var l=Lines[line];int x=SymbolAt(0,a,l[0]);return x==SymbolAt(1,b,l[1])&&x==SymbolAt(2,c,l[2]);}
        public static long ChannelPayout(long price,int channel)=>channel<0||channel>=Channels?0:TenthsPayout(price,ChannelTenths[channel]);
        /// <summary>What a marble's family won (tenths of its worth: channels and the gold cup) in copper.</summary>
        public static long TenthsPayout(long price,int tenths)=>tenths<0||tenths>PinSim.MostTenths?0:checked(price*tenths/10);
        public static bool IsMachine(StationKind kind)=>kind==StationKind.CasinoSlots||kind==StationKind.CasinoPinball||kind==StationKind.CasinoRoulette;
        public static Game GameOf(StationKind kind)=>kind==StationKind.CasinoSlots?Game.Slots:kind==StationKind.CasinoPinball?Game.Pinball:kind==StationKind.CasinoRoulette?Game.Roulette:Game.None;
        public static string TextureOf(StationKind kind)=>kind==StationKind.CasinoSlots?"Casino_Slots":kind==StationKind.CasinoPinball?"Casino_Pinball":"Casino_Roulette";
        public static string Title(Game game)=>game==Game.Slots?"老虎机":game==Game.Pinball?"弹珠机":"红黑绿转盘";
        public static void AddItems(List<ItemDef> list)
        {
            list.Add(new ItemDef{Type=ItemType.CasinoKit,Name="赌场",Texture="PrefabHouse_25",Source=ItemSource.Shop,Value=500000,MaxStack=99,Consumable=true,
                NoMelee=true,NoUseGraphic=true,UseTime=.5f,UseAnimation=.5f,TileBoost=4,Tooltip="5000 吉欧，搭建26×12格的赌场\n内含老虎机、弹珠机、红黑绿转盘，右键家具使用\n鼠标所指格为底部中央，无安装距离限制；可覆盖旧房，物品掉出\n赌场内的机器可用镐拆下后重新摆放；弹珠和奖金属于角色，拆机器不会丢失"});
            foreach(var pair in new[]{(ItemType.CasinoSlots,StationKind.CasinoSlots),(ItemType.CasinoPinball,StationKind.CasinoPinball),(ItemType.CasinoRoulette,StationKind.CasinoRoulette)})
                list.Add(new ItemDef{Type=pair.Item1,Name=Title(GameOf(pair.Item2)),Texture=TextureOf(pair.Item2),PlacesStation=pair.Item2,Source=ItemSource.Craft,MaxStack=99,
                    Consumable=true,NoMelee=true,NoUseGraphic=true,UseTime=.25f,UseAnimation=.25f,Tooltip="赌场内的可互动家具，右键打开\n拆除后可以重新摆放，角色的弹珠与奖金独立保存"});
        }
        // All money and token mutations share one rollback boundary. No dropped currency on a full bag.
        sealed class Wallet
        {
            readonly Inventory inv;readonly ItemStack[][] arrays;readonly ItemStack mouse;readonly int spare;readonly int nativeGeo;
            public Wallet(){inv=TerraMod.Instance.Inventory;arrays=new[]{(ItemStack[])inv.Slots.Clone(),(ItemStack[])inv.Bank.Clone(),(ItemStack[])inv.Safe.Clone(),(ItemStack[])inv.Forge.Clone(),(ItemStack[])inv.Void.Clone()};mouse=inv.Mouse;spare=Shop.SpareCopper;nativeGeo=PlayerData.instance!=null?PlayerData.instance.geo:0;}
            public void Restore(){var targets=new[]{inv.Slots,inv.Bank,inv.Safe,inv.Forge,inv.Void};for(int i=0;i<targets.Length;i++)Array.Copy(arrays[i],targets[i],arrays[i].Length);inv.Mouse=mouse;Shop.SpareCopper=spare;if(PlayerData.instance!=null)PlayerData.instance.geo=nativeGeo;}
        }
        static bool Fail(string reason){Notice=reason;return false;}
        static bool Pay(long copper)
        {
            if(copper<0||Available<copper)return false;
            long credit=Math.Min(copper,state.Credit);state.Credit-=credit;return Shop.Spend(copper-credit);
        }
        static long ReceiveCapacity()
        {
            var inv=TerraMod.Instance.Inventory;long capacity=0;
            for(int i=0;i<inv.Slots.Length;i++)
            {
                if(i>=Inventory.Size&&!Inventory.IsCoinSlot(i))continue;
                var s=inv.Slots[i];if(s.IsEmpty)capacity+=int.MaxValue;else if(s.Type==ItemType.Geo)capacity+=int.MaxValue-(long)s.Count;
            }
            return capacity;
        }
        static void Deliver()
        {
            if(TerraMod.Instance.Creative)
            {
                long room=Math.Max(0,(long)int.MaxValue-(PlayerData.instance?.geo??0));
                long amount=Math.Min(state.Credit,room*100);state.Credit-=amount;Shop.Earn(amount);return;
            }
            long capacity=ReceiveCapacity();if(capacity<=0)return;
            long copper=Math.Min(state.Credit,Math.Min(capacity,1000000000L)*100L);
            // Integer-only payout: the shared currency helper converts large counts to float.
            // Never call its world-drop fallback; anything that does not fit stays in this ledger.
            long total=Shop.SpareCopper+copper;int left=TerraMod.Instance.Inventory.Pickup(ItemType.Geo,(int)(total/100));
            state.Credit-=copper-(long)left*100;Shop.SpareCopper=(int)(total%100);
        }
        internal static bool DevFailSave;
        // The pinball's own changes (marbles loaded, struck, fallen) are saved a moment later, a few at a time: a launch
        // within two seconds (before where it goes can be seen), the rest within a few seconds.
        static bool dirty,launchUnsaved;static float lastCommit=-99;
        static void SaveSoon(bool launch){dirty=true;launchUnsaved|=launch;}
        internal static void SaveIfDue(bool now=false)
        {
            if(!dirty||TerraMod.Instance==null||HeroController.SilentInstance==null)return;float t=Time.realtimeSinceStartup;
            if(!now&&t-lastCommit<(launchUnsaved?2f:6f))return;lastCommit=t;
            try{Deliver();}catch(Exception e){Plugin.Log.LogError("[赌场] 奖金入袋失败："+e);}
            if(Save.ModSave.CommitCasino()){dirty=false;launchUnsaved=false;}else Plugin.Log.LogWarning("[赌场] 弹珠机的记录暂未写入存档，稍后再试。");
        }
        static bool Change(Func<bool> action)
        {
            if(!Access())return Fail("请靠近已放置的机器，且先收好光标物品。");
            if(Busy)return Fail("这一局正在显示结果，请稍候。");
            var prior=Snapshot();var wallet=new Wallet();
            try
            {
                if(!action()){state=prior;wallet.Restore();return false;}
                Deliver();state.Sequence++;
                if(DevFailSave||!Save.ModSave.CommitCasino())
                {state=prior;wallet.Restore();return Fail("存档写入失败，本次交易已退回，未开始新一局。");}
                dirty=false;launchUnsaved=false;lastCommit=Time.realtimeSinceStartup;
                Notice=state.LastMessage;return true;
            }
            catch(Exception e){state=prior;wallet.Restore();Plugin.Log.LogError("[赌场] 交易已回滚："+e);return Fail("本次交易未完成，投入已退回。");}
        }
        public static bool ValidStake(long geo)=>geo>=100&&geo<=MaxBetGeo&&(geo>10000||Array.IndexOf(Stakes,geo)>=0);
        public static bool ValidCopper(long copper)=>copper>=100&&copper<=MaxStake;
        // A spin: the stake in copper (all one has, all in).
        public static bool PlaySlots(long copper,bool allIn=false)
        {
            if(openGame!=Game.Slots||copper<(allIn?100:MinStake)||copper>MaxStake)return Fail(allIn?"全额下注至少要有 1 吉欧。":"投入至少 100 吉欧。");
            int linesWon=0;
            bool ok=Change(()=>{
                if(Available<copper)return Fail("吉欧不足。");if(state.Credit>MaxCredit-copper*10)return Fail("赌场余额已满，背包装不下更多吉欧。");
                if(!Pay(copper))return Fail("吉欧不足。");for(int i=0;i<3;i++)state.Reels[i]=Next(5);
                long payout=SlotPayout(copper,state.Reels[0],state.Reels[1],state.Reels[2]);state.Credit+=payout;
                state.LastGame=(int)Game.Slots;state.LastStake=copper;state.LastPayout=payout;state.LastWin=payout>copper;state.LastAllIn=allIn;
                int lines=0;for(int l=0;l<Lines.Length;l++)if(LineTriple(l,state.Reels[0],state.Reels[1],state.Reels[2]))lines++;linesWon=lines;
                state.LastMessage=payout==0?"没有中奖。":lines>0?$"{lines} 条线三连，返还 {Money(payout)}。":payout==copper?$"中线两个相同，保本返还 {Money(payout)}。":$"中线两个相同，安慰奖返还 {Money(payout)}。";return true;
            });if(ok){BeginAnimation(2.6f);Achievements.AchievementTracker.SlotsRound(copper,state.LastPayout,allIn,linesWon,Lines.Length);}return ok;
        }
        // A turn of the wheel: all the chips on the table (in copper) on one colour.
        public static bool PlayWheel(long copper,Colour chosen,bool allIn=false)
        {
            if(openGame!=Game.Roulette||copper<(allIn?100:MinStake)||copper>MaxStake||(int)chosen<0||(int)chosen>2)return Fail(allIn?"全额下注至少要有 1 吉欧。":"桌上的筹码至少 100 吉欧。");
            bool ok=Change(()=>{
                if(Available<copper)return Fail("吉欧不足。");if(state.Credit>MaxCredit-copper*63)return Fail("赌场余额已满，背包装不下更多吉欧。");
                if(!Pay(copper))return false;state.LastWheel=Next(WheelSectors);long payout=WheelPayout(copper,chosen,state.LastWheel);state.Credit+=payout;
                state.LastGame=(int)Game.Roulette;state.LastColour=(int)chosen;state.LastStake=copper;state.LastPayout=payout;state.LastWin=payout>0;state.LastAllIn=allIn;
                state.LastMessage=$"落点：{ColourName(WheelColour(state.LastWheel))}；返还 {Money(payout)}（含本金）。";return true;
            });if(ok){BeginAnimation(3);Achievements.AchievementTracker.WheelRound(copper,state.LastPayout,allIn,chosen==Colour.Green);}return ok;
        }
        public static bool BuyBalls(int denomination,int count=1)
        {
            if(openGame!=Game.Pinball||denomination<0||denomination>=6||count<1||count>100)return Fail("购买数量无效。");
            return Change(()=>{long price=Denominations[denomination]*100*count;
                if(state.Balls[denomination]>MaxBalls-count)return Fail("弹珠数量已达上限，请先出售。");
                if(Available<price||!Pay(price))return Fail("吉欧不足。");state.Balls[denomination]+=count;
                state.LastMessage=$"购入 {count} 颗 {Denominations[denomination]} 吉欧弹珠，已存入弹珠箱。";return true;});
        }
        public static bool SellBalls(int denomination,long count)
        {
            if(openGame!=Game.Pinball||denomination<0||denomination>=6||count<=0)return Fail("出售数量无效。");
            return Change(()=>{if(state.Balls[denomination]<count)return Fail("持有弹珠不足。");long price=checked(Denominations[denomination]*100*count);
                if(state.Credit>MaxCredit-price)return Fail("赌场余额已满，背包装不下更多吉欧。");state.Balls[denomination]-=count;state.Credit+=price;
                state.LastMessage=$"出售 {count} 颗弹珠，兑换 {Money(price)}。";return true;});
        }

        // ---------------------------------------------------------------- the pinball's marbles
        public static List<Marble> Machine=>state.Machine;
        public static int LoadedCount{get{int n=0;foreach(var m in state.Machine)if(m.Force<0)n++;return n;}}
        public static Marble NextLoaded(){foreach(var m in state.Machine)if(m.Force<0)return m;return null;}
        public static bool HasAllInMarble(){foreach(var m in state.Machine)if(m.Denomination==AllIn)return true;return false;}
        /// <summary>A marble from the tray into the machine (the all-in one: everything one has, paid when it is struck).</summary>
        public static bool LoadMarble(int denomination)
        {
            if(openGame!=Game.Pinball||denomination<0||denomination>AllIn)return false;
            if(LoadedCount>=MostLoaded)return Fail("机器装满了。");
            if(denomination==AllIn){if(HasAllInMarble())return Fail("全额弹珠已经在机器里了。");if(Available<100)return Fail("身上没有吉欧了。");}
            else{if(state.Balls[denomination]<=0)return Fail("这种珠子没有了，在左边买。");state.Balls[denomination]--;}
            state.Machine.Add(new Marble{Id=state.NextMarble++,Denomination=denomination,Price=denomination==AllIn?0:Denominations[denomination]*100});
            SaveSoon(false);return true;
        }
        /// <summary>A loaded marble taken back out (to the tray; an all-in one that was struck and came back, its money back).</summary>
        public static bool UnloadMarble(long id)
        {
            int i=state.Machine.FindIndex(x=>x.Id==id);if(i<0||state.Machine[i].Force>=0)return false;var m=state.Machine[i];state.Machine.RemoveAt(i);
            if(m.Denomination<AllIn)state.Balls[m.Denomination]++;else if(m.Price>0)state.Credit=Math.Min(MaxCredit,state.Credit+m.Price);
            SaveSoon(false);return true;
        }
        /// <summary>The first loaded marble struck from the plunger at a force (0..1), at the machine's clock; null if none could be.</summary>
        public static Marble Launch(double force,double clock)
        {
            var m=NextLoaded();if(m==null){Fail("机器里没有珠子：点右边的珠子装填。");return null;}
            if(m.Denomination==AllIn&&m.Price==0)
            {
                // Everything one has goes into it, paid as any bet is: saved before it flies.
                bool ok=Change(()=>{long all=Available;if(all<100)return Fail("身上没有吉欧了。");if(state.Credit>MaxCredit-all*10)return Fail("赌场余额已满，背包装不下更多吉欧。");if(!Pay(all))return Fail("吉欧不足。");
                    m.Price=all;Struck(m,force,clock);state.LastMessage=$"全额 {Money(all)} 的弹珠发射了！";return true;});
                return ok?m:null;
            }
            Struck(m,force,clock);SaveSoon(true);return m;
        }
        static void Struck(Marble m,double force,double clock)
        {
            m.Force=Math.Max(0,Math.Min(1,force));m.Clock=clock;m.Seed=NextUInt();if(m.Seed==0)m.Seed=1;m.Scene=scene??"";
            var region=TerrainSystem.Find(scene);var at=region==null?Vector2.zero:new Vector2(machineBox.center.x,machineBox.y)-(Vector2)region.Root.position;m.X=at.x;m.Y=at.y;
            state.Sequence++;
        }
        /// <summary>
        /// A marble and all that came of it in (its channel, the cup, its free marbles' channels): what they won, in tenths of
        /// its worth, goes into the casino's balance (into the bag when saved).
        /// </summary>
        public static long Settle(long id,int tenths,int free,out Marble marble)
        {
            marble=null;int i=state.Machine.FindIndex(x=>x.Id==id);if(i<0||tenths<0||tenths>PinSim.MostTenths)return -1;
            marble=state.Machine[i];state.Machine.RemoveAt(i);long payout=TenthsPayout(marble.Price,tenths);
            state.Credit=Math.Min(MaxCredit,state.Credit+payout);
            state.LastGame=(int)Game.Pinball;state.LastStake=marble.Price;state.LastPayout=payout;state.LastWheel=tenths;state.LastDenomination=marble.Denomination;
            state.LastMultiplier=1;state.LastWin=payout>marble.Price;state.LastAllIn=marble.Denomination==AllIn;state.Sequence++;
            state.LastMessage=(free>0?$"弹珠连同分出的 {free} 颗共返还 {Money(payout)}":$"弹珠返还 {Money(payout)}")+$"（×{tenths/10m:0.#}）。";
            SaveSoon(false);Achievements.AchievementTracker.PinDone(marble.Price,payout,free,marble.Denomination==AllIn);return payout;
        }
        /// <summary>A marble struck too softly, back on the plunger: the next to go again.</summary>
        public static void Returned(long id)
        {
            int i=state.Machine.FindIndex(x=>x.Id==id);if(i<0)return;var m=state.Machine[i];state.Machine.RemoveAt(i);m.Force=-1;state.Machine.Insert(0,m);SaveSoon(false);
        }
        /// <summary>What the casino holds of one's winnings into the bag now (each round's save does it too).</summary>
        public static bool CollectCredit()=>Change(()=>true);
        public static string Money(long copper)=>Lang.F("{0} 吉欧",(copper/100m).ToString(copper%100==0?"0":"0.00"));
        public static string ColourName(Colour colour)=>colour==Colour.Red?"红色":colour==Colour.Black?"黑色":"绿色";
    }
}
