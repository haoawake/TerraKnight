$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$stub = @'
using System;
using System.Collections.Generic;
using System.IO;
namespace UnityEngine { public struct Vector2 {public static Vector2 zero=>default;} }
namespace TerraKnight {
 public class TerraMod {public static TerraMod Instance;public HashSet<string> GrantedAbilities=new HashSet<string>();}
}
namespace TerraKnight.Items {
 public enum ItemType {LifeCrystal}
 public struct ItemStack {public int Count;public static ItemStack Of(ItemType t,int n)=>new ItemStack{Count=n};}
 public static class Bestiary {public static Dictionary<string,int> Kills=new Dictionary<string,int>();public static int KillsOf(string r)=>Kills.TryGetValue(r,out int n)?n:0;}
 public static class EnemyDrops {public static int Paid;public static bool IsBoss(string r)=>r=="Boss"||r=="Boss@4"||r=="OtherBoss";public static void Spill(ItemStack s,UnityEngine.Vector2 p,bool d,bool perPlayer){Paid+=s.Count;}}
}
namespace TerraKnight.Net {
 public enum Msg {BossLifeReward}
 public class Packet {readonly MemoryStream stream=new MemoryStream();public BinaryWriter W;public Packet(){W=new BinaryWriter(stream);}public byte[] Done(){W.Flush();return stream.ToArray();}}
 public static class NetSession {public static bool Active;public static int Sent;public static Packet Mine(Msg m)=>new Packet();public static void Send(byte[] p){Sent++;}}
 public static class Enemies {public static bool Remote;public static bool RemoteOwned(string s)=>Remote;}
 public static class BossLifeScenarios {
  static int checks;
  static void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
  static TerraKnight.TerraMod NewPlayer(){var m=new TerraKnight.TerraMod();TerraKnight.TerraMod.Instance=m;Items.Bestiary.Kills.Clear();Items.EnemyDrops.Paid=0;NetSession.Active=false;Enemies.Remote=false;BossLifeRewards.Reset();return m;}
  static void Award(string id,string record,int from=255){using(var ms=new MemoryStream()){var w=new BinaryWriter(ms);w.Write((byte)2);w.Write(id);w.Write("arena");w.Write(record);w.Flush();ms.Position=0;BossLifeRewards.Receive(from,new BinaryReader(ms));}}
  public static string Run(){
   var ledger=new BossLifeRewardLedger();string id=Guid.NewGuid().ToString("N");
   Check(!ledger.Register(id,1,"arena","Boss",false),"non-owner registered a boss reward");
   Check(ledger.Register(id,1,"arena","Boss",true),"owner registration rejected");
   Check(!ledger.Register(id,1,"arena","Boss",true),"duplicate registration accepted");
   Check(!ledger.Complete(id,2,"arena","Boss"),"another player released owner's reward");
   Check(!ledger.Complete(id,1,"elsewhere","Boss"),"wrong arena released reward");
   Check(!ledger.Complete(id,1,"arena","OtherBoss"),"wrong boss released reward");
   Check(ledger.Complete(id,1,"arena","Boss"),"authorized release after ownership handoff failed");
   Check(!ledger.Complete(id,1,"arena","Boss"),"duplicate release paid twice");
   Check(!ledger.Register(id,1,"arena","Boss",true),"completed event was replayed");
   Check(!ledger.Register("bad-token",1,"arena","Boss",true),"invalid event token accepted");
   NewPlayer();BossLifeRewards.InitializeClaims(TerraKnight.TerraMod.Instance);
   string local=BossLifeRewards.Begin("Boss","arena");Items.Bestiary.Kills["Boss"]=1;
   Check(Items.EnemyDrops.Paid==0,"lethal hit paid before death animation");
   BossLifeRewards.Complete(local,"Boss","arena",default,false);
   Check(Items.EnemyDrops.Paid==1,"new player's first boss did not drop one crystal");
   BossLifeRewards.Complete(local,"Boss","arena",default,false);
   BossLifeRewards.Complete(BossLifeRewards.Begin("Boss","arena"),"Boss","arena",default,false);
   Check(Items.EnemyDrops.Paid==1,"repeat kill or repeated callback rewarded again");
   BossLifeRewards.Complete(BossLifeRewards.Begin("Boss@4","arena"),"Boss@4","arena",default,false);
   Check(Items.EnemyDrops.Paid==2,"separate Hornet-style encounter variant lost its first reward");
   var saved=new HashSet<string>(TerraKnight.TerraMod.Instance.GrantedAbilities);NewPlayer();TerraKnight.TerraMod.Instance.GrantedAbilities.UnionWith(saved);
   Award(Guid.NewGuid().ToString("N"),"Boss");Check(Items.EnemyDrops.Paid==0,"saved claim was not honored after reload");
   NewPlayer();Items.Bestiary.Kills["Boss"]=1;BossLifeRewards.InitializeClaims(TerraKnight.TerraMod.Instance);
   Award(Guid.NewGuid().ToString("N"),"Boss");Check(Items.EnemyDrops.Paid==0,"legacy already-killed boss received a retroactive reward");
   var veteran=TerraKnight.TerraMod.Instance;
   NewPlayer();BossLifeRewards.InitializeClaims(TerraKnight.TerraMod.Instance);var newcomer=TerraKnight.TerraMod.Instance;
   id=Guid.NewGuid().ToString("N");TerraKnight.TerraMod.Instance=veteran;Award(id,"Boss");
   Check(Items.EnemyDrops.Paid==0,"veteran received repeat multiplayer reward");
   BossLifeRewards.Reset();TerraKnight.TerraMod.Instance=newcomer;Award(id,"Boss");
   Check(Items.EnemyDrops.Paid==1,"newcomer blocked by veteran's prior victory");
   Award(id,"Boss");Award(Guid.NewGuid().ToString("N"),"Boss");
   Check(Items.EnemyDrops.Paid==1,"newcomer received duplicate or repeat rewards");
   NewPlayer();BossLifeRewards.InitializeClaims(TerraKnight.TerraMod.Instance);
   Award(Guid.NewGuid().ToString("N"),"Boss",1);Check(Items.EnemyDrops.Paid==0,"client-forged award accepted");
   NetSession.Active=true;Enemies.Remote=true;Check(BossLifeRewards.Begin("Boss","arena")==null,"puppet registered another reward");
   Enemies.Remote=false;int sent=NetSession.Sent;id=BossLifeRewards.Begin("Boss","arena");
   Check(NetSession.Sent==sent+1&&Items.EnemyDrops.Paid==0,"network registration paid before approval");
   BossLifeRewards.Complete(id,"Boss","arena",default,false);
   Check(NetSession.Sent==sent+2&&Items.EnemyDrops.Paid==0,"release paid locally before server award");
   Award(id,"Boss");Check(Items.EnemyDrops.Paid==1,"owner missed server broadcast award");
   return "PASS "+checks+" first-kill/ownership/deduplication checks; no live game or socket test.";
  }
 }
}
'@
$source = Join-Path ([IO.Path]::GetTempPath()) ('boss-life-rewards-' + [guid]::NewGuid().ToString('N') + '.cs')
try {
    [IO.File]::WriteAllText($source, $stub)
    Add-Type -Path @((Join-Path $root 'src/TerraKnight/Net/BossLifeRewards.cs'), $source)
    [TerraKnight.Net.BossLifeScenarios]::Run()
} finally { Remove-Item -LiteralPath $source -ErrorAction SilentlyContinue }
