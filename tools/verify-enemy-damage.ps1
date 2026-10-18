# Runs the actual stage budgets and TerraStats damage path outside Unity.
# Only unrelated game services and Unity's managed math helpers are stubbed.
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$sources = @(
    'src/TerraKnight/World/Areas.cs',
    'src/TerraKnight/World/GameDifficulty.cs',
    'src/TerraKnight/Player/TerraStats.cs'
) | ForEach-Object { Join-Path $root $_ }
$stubs = @'
using System;
namespace UnityEngine {
    public struct Color { public Color(float r,float g,float b){} public static Color white => new Color(); }
    public static class Mathf {
        public static int Max(int a,int b)=>Math.Max(a,b);
        public static float Max(float a,float b)=>Math.Max(a,b);
        public static int Min(int a,int b)=>Math.Min(a,b);
        public static float Min(float a,float b)=>Math.Min(a,b);
        public static int Clamp(int x,int a,int b)=>Max(a,Min(x,b));
        public static int RoundToInt(float x)=>(int)Math.Round(x,MidpointRounding.ToEven);
        public static int FloorToInt(float x)=>(int)Math.Floor(x);
        public static float InverseLerp(float a,float b,float x)=>Max(0,Min(1,(x-a)/(b-a)));
        public static float Lerp(float a,float b,float x)=>a+(b-a)*Max(0,Min(1,x));
    }
}
public class PlayerData { public int maxHealthBase=5,heartPieces,MPReserveMax; }
namespace TeamCherry.Localization { public static class Language {
    public static bool Has(string k,string s)=>false;
    public static string Get(string k,string s)=>k;
} }
namespace TerraKnight { public static class Lang { public static bool IsSource=>true; public static string T(string s)=>s; } }
namespace TerraKnight.World { public static class AreaBlocks { public static string WallSheetOfScene(string s)=>null; } }
namespace TerraKnight.Items {
    public enum PermanentBoost { AegisCrystal,ArcaneCrystal,ArtisanLoaf }
    public static class Shop { public static int Stage; public static int UnlockedStage()=>Stage; }
    public static class EnemyDrops { public static float TypicalHp(int s)=>10; }
    public static class EnemyDebuffs { public static int SoulDrainers=>0; }
    public static class ArmorEffects {
        public static int RegenBonus=>0;
        public static int NebulaManaLevel=>0;
        public static int ModifyHurt(int x)=>x;
    }
}
public static class EnemyDamageVerification {
    static int checks;
    static void Check(bool ok,string label) { checks++; if(!ok)throw new Exception(label); }
    public static string Run() {
        var old=new[]{20,30,42,58,76,100};
        var armor=new[]{10,20,30,45,65,90};
        var report=new System.Text.StringBuilder();
        for(int stage=0;stage<=5;stage++) {
            int budget=TerraKnight.World.Areas.MasterDamagePerMask(stage);
            Check(TerraKnight.World.Areas.MasterHazardDamagePerMask(stage)==old[stage],"hazard budget changed");
            Check(budget>old[stage],"enemy budget did not increase");
            foreach(var difficulty in new[]{TerraKnight.World.DifficultyLevel.Normal,TerraKnight.World.DifficultyLevel.Hard,TerraKnight.World.DifficultyLevel.Expert,TerraKnight.World.DifficultyLevel.Master,TerraKnight.World.DifficultyLevel.Hollow}) {
                TerraKnight.World.GameDifficulty.Current=difficulty;
                float share=TerraKnight.World.GameDifficulty.EnemyFactor;
                foreach(float category in new[]{1f,1.5f,2f})
                foreach(int masks in new[]{1,2,4,8}) {
                    int raw=(int)Math.Round(budget*category);
                    var naked=new TerraKnight.Player.TerraStats{Life=10000};
                    var geared=new TerraKnight.Player.TerraStats{Life=10000,Defense=armor[stage]};
                    int damage=geared.TakeMasks(masks,raw,share);
                    int bare=naked.TakeMasks(masks,raw,share);
                    float coefficient=difficulty>=TerraKnight.World.DifficultyLevel.Master?1f:difficulty==TerraKnight.World.DifficultyLevel.Expert?.75f:.5f;
                    int prior=Math.Max(1,(int)Math.Round((masks*(int)Math.Round(old[stage]*category)-armor[stage]/2)*share));
                    int expected=Math.Max(1,(int)Math.Max(1,(int)Math.Round(raw*share*(difficulty==TerraKnight.World.DifficultyLevel.Hollow?masks:1))-armor[stage]*coefficient));
                    Check(damage==expected,"vanilla defense/difficulty ordering mismatch");
                    Check(damage<bare,"armor no longer reduces damage");
                    Check(geared.Life==10000-damage,"life deduction incorrect");
                    if(category==1 && masks==1) report.AppendLine($"stage={stage} difficulty={difficulty} defense={armor[stage]} before={prior} after={damage}");
                }
            }
            TerraKnight.Items.Shop.Stage=stage;
            Check(TerraKnight.World.Areas.StatLevel(0)==stage,"earlier enemies do not catch up");
            Check(TerraKnight.World.Areas.CatchUp(0)==(stage>0?.9f:1f),"catch-up modifier changed");
        }
        var shielded=new TerraKnight.Player.TerraStats{Life=100,Shield=20,Defense=20,Endurance=.17f};
        TerraKnight.World.GameDifficulty.Current=TerraKnight.World.DifficultyLevel.Master;
        Check(shielded.TakeMasks(1,68)==39,"endurance incorrect");
        Check(shielded.Life==81 && shielded.Shield==0,"shield incorrect");
        foreach(var difficulty in new[]{TerraKnight.World.DifficultyLevel.Normal,TerraKnight.World.DifficultyLevel.Hard,TerraKnight.World.DifficultyLevel.Expert,TerraKnight.World.DifficultyLevel.Master,TerraKnight.World.DifficultyLevel.Hollow}) {
            TerraKnight.World.GameDifficulty.Current=difficulty;
            double effectiveness=difficulty>=TerraKnight.World.DifficultyLevel.Master?1d:difficulty==TerraKnight.World.DifficultyLevel.Expert?.75d:.5d;
            foreach(int defense in new[]{0,1,3,46,74,500}) foreach(float endurance in new[]{0f,.17f,.5f}) {
                var player=new TerraKnight.Player.TerraStats{Life=10000,Defense=defense,Endurance=endurance};
                int expected=Math.Max(1,(int)((1f-endurance)*Math.Max(1d,100-defense*effectiveness)));
                Check(player.TakeDirect(100)==expected,"direct damage or fractional defense/endurance mismatch");
            }
        }
        TerraKnight.World.GameDifficulty.Current=TerraKnight.World.DifficultyLevel.Master;
        Check(new TerraKnight.Player.TerraStats{Life=1000,Defense=46}.TakeMasks(1,317)==271,"46 defense boss example");
        Check(new TerraKnight.Player.TerraStats{Life=1000,Defense=74}.TakeMasks(1,317)==243,"full warding boss example");
        Check(TerraKnight.Player.TerraStats.NativeHitDamage(0,458)==0,"zero native hit causes damage");
        Check(new TerraKnight.Player.TerraStats{Life=1000,Defense=150}.TakeMasks(2,458)==308,"Nightmare King still doubles mask damage");
        Check(new TerraKnight.Player.TerraStats{Life=1000,Defense=150}.TakeMasks(1,458)==308,"single-mask hit changed stage/source budget");
        foreach(var level in new[]{TerraKnight.World.DifficultyLevel.Normal,TerraKnight.World.DifficultyLevel.Hard,TerraKnight.World.DifficultyLevel.Expert,TerraKnight.World.DifficultyLevel.Master,TerraKnight.World.DifficultyLevel.Hollow})
            Check(TerraKnight.World.GameDifficulty.Parse((int)level)==level,"difficulty save ID round-trip");
        Check(TerraKnight.World.GameDifficulty.Parse(0)==TerraKnight.World.DifficultyLevel.Master && TerraKnight.World.GameDifficulty.Parse(6)==TerraKnight.World.DifficultyLevel.Master,"legacy/unknown save changed difficulty");
        Check(TerraKnight.World.GameDifficulty.NameOf(TerraKnight.World.DifficultyLevel.Hollow)=="空洞","Hollow label");
        Check(TerraKnight.World.GameDifficulty.IconOf(TerraKnight.World.DifficultyLevel.Hollow)==TerraKnight.World.GameDifficulty.IconOf(TerraKnight.World.DifficultyLevel.Master),"Hollow icon missing");
        TerraKnight.World.GameDifficulty.Current=TerraKnight.World.DifficultyLevel.Hollow;
        Check(TerraKnight.World.GameDifficulty.MasterRules && TerraKnight.World.GameDifficulty.EnemyFactor==1 && TerraKnight.World.GameDifficulty.DefenseEffectiveness==1,"Hollow does not inherit Master rules");
        Check(new TerraKnight.Player.TerraStats{Life=2000,Defense=150}.TakeMasks(2,458)==766,"Hollow failed to restore double-mask damage");
        Check(new TerraKnight.Player.TerraStats{Life=2000,Defense=150}.TakeMasks(1,458)==308,"Hollow single mask differs from Master");
        Check(new TerraKnight.Player.TerraStats{Life=2000,Defense=150}.TakeDirect(458)==308,"Hollow changed direct damage");
        TerraKnight.World.GameDifficulty.Current=TerraKnight.World.DifficultyLevel.Master;
        var tank=new TerraKnight.Player.TerraStats{Life=100,Defense=1000};
        Check(tank.TakeMasks(1,32,.25f)==1,"minimum damage incorrect");
        Check(tank.TakeDirect(20)==1,"direct damage changed");
        report.AppendLine($"PASS {checks} checks; actual Areas/GameDifficulty/TerraStats sources, neutral armor effects.");
        return report.ToString();
    }
}
'@
$tempSource = Join-Path ([IO.Path]::GetTempPath()) ('enemy-damage-' + [guid]::NewGuid().ToString('N') + '.cs')
try {
    [IO.File]::WriteAllText($tempSource, $stubs)
    Add-Type -Path ($sources + $tempSource) -CompilerOptions '/nowarn:0649' -WarningAction SilentlyContinue
    [EnemyDamageVerification]::Run()
} finally {
    Remove-Item -LiteralPath $tempSource -Force -ErrorAction SilentlyContinue
}
