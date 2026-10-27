using System;
using System.Collections.Generic;

namespace TerraKnight.Items
{
    /// <summary>Player-facing explanations of the authoritative progression rules; never changes progress.</summary>
    internal static class ProgressionInfo
    {
        internal static string WorldName => StepName(VanillaBossProgression.Current);
        internal static string BossName(string key) => Bestiary.NameOf(key);
        internal static string StepName(VanillaBossProgression.Step step)
        {
            switch (step)
            {
                case VanillaBossProgression.Step.Slime: return Lang.T("初战告捷");
                case VanillaBossProgression.Step.Eye: return Lang.T("苍绿试炼已过");
                case VanillaBossProgression.Step.Evil: return Lang.T("水道强敌已克");
                case VanillaBossProgression.Step.Dungeon: return Lang.T("深处试炼已过");
                case VanillaBossProgression.Step.Hardmode: return Lang.T("王国强敌已克");
                case VanillaBossProgression.Step.MechOne: return Lang.T("三重试炼 · 其一");
                case VanillaBossProgression.Step.MechTwo: return Lang.T("三重试炼 · 其二");
                case VanillaBossProgression.Step.MechAll: return Lang.T("三重试炼已竟");
                case VanillaBossProgression.Step.Plantera: return Lang.T("叛徒领主已败");
                case VanillaBossProgression.Step.Golem: return Lang.T("神之驯服者已败");
                case VanillaBossProgression.Step.Cultist: return Lang.T("空洞骑士已败");
                case VanillaBossProgression.Step.Pillars: return Lang.T("神居试炼已进");
                case VanillaBossProgression.Step.MoonLord: return Lang.T("辐光已败");
                default: return Lang.T("初探圣巢");
            }
        }

        static string Beat(params string[] keys)
        {
            var names = new List<string>();
            foreach (var key in keys) names.Add(BossName(key));
            return Lang.F(keys.Length > 1 ? "击败任意一位：{0}" : "击败 {0}", string.Join(" / ", names));
        }

        internal static string Missing(ItemType type, int stage, int sourceStage = 0)
        {
            var def = ItemDb.Get(type);
            var reasons = new List<string>();
            int need = Math.Max(def.Stage, sourceStage);
            if (need > stage) reasons.Add(Lang.F("探索阶段 {0}", need));
            if (!VanillaBossProgression.Allowed(type)) reasons.Add(MaterialGate(type));
            if (!VanillaBossProgression.AllowedEquipment(type, def))
            {
                string boss = null;
                foreach (var p in VanillaBossProgression.All)
                    if (p.Value.Signature == type || Array.IndexOf(p.Value.Gear, type) >= 0)
                    { boss = Beat(p.Key); break; }
                reasons.Add(boss ?? (def.Stage >= 5 ? Beat("TraitorLord") : Beat("BlackKnight", "JarCollector", "MageLord@5", "InfectedKnight")));
            }
            return string.Join(Lang.T("；"), reasons);
        }

        static string MaterialGate(ItemType t)
        {
            switch (t)
            {
                case ItemType.Hellstone: case ItemType.HellstoneBar: case ItemType.Meteorite: case ItemType.MeteoriteBar:
                    return Beat("DungDefender", "FlukeMother") + Lang.T("，或推进后续战况");
                case ItemType.Bone: return Beat("MantisLord");
                case ItemType.ShadowScale: return Beat("DungDefender");
                case ItemType.TissueSample: return Beat("FlukeMother");
                case ItemType.BeeWax: case ItemType.MountHoneyedGoggles: return Beat("GhostHu");
                case ItemType.SoulofMight: return Beat("Hornet@4");
                case ItemType.SoulofSight: return Beat("GhostMarkoth");
                case ItemType.SoulofFright: return Beat("InfectedKnight@5");
                case ItemType.HallowedBar: return Beat("Hornet@4", "GhostMarkoth", "InfectedKnight@5");
                case ItemType.ChlorophyteOre: case ItemType.ChlorophyteBar:
                    return Lang.F("全部击败：{0}、{1}、{2}", BossName("Hornet@4"), BossName("GhostMarkoth"), BossName("InfectedKnight@5"));
                case ItemType.Ectoplasm: case ItemType.SpectreBar: case ItemType.ShroomiteBar: case ItemType.Autohammer:
                case ItemType.BrokenHeroSword: case ItemType.MountReindeerBells: case ItemType.MountWitchBroom:
                case ItemType.MountSpookyWoodMountItem: case ItemType.MountSantankMountItem: return Beat("TraitorLord");
                case ItemType.BeetleHusk: case ItemType.MountCosmicCarKey: case ItemType.MountBrainScrambler: return Beat("LobsterLancer");
                case ItemType.LunarCraftingStation: return Beat("HollowKnight");
                case ItemType.FragmentSolar: return Beat("NailBros");
                case ItemType.FragmentVortex: return Beat("Nailsage");
                case ItemType.FragmentNebula: return Beat("Paintmaster");
                case ItemType.FragmentStardust: return Beat("HollowKnightPrime");
                case ItemType.LunarOre: case ItemType.LunarBar: return Beat("FinalBoss");
                case ItemType.MountShrimpyTruffle: return Beat("WhiteDefender");
                case ItemType.MountQueenSlimeMountSaddle: return Beat("HiveKnight");
                default: return Beat("BlackKnight", "JarCollector", "MageLord@5", "InfectedKnight");
            }
        }
    }
}
