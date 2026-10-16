using System.Collections.Generic;

namespace TerraKnight.Items
{
    internal static partial class ProgressionBalance
    {
        static readonly Dictionary<ItemType,ItemType[]> Upgrades = new Dictionary<ItemType,ItemType[]>
        {
            [ItemType.FeralClaws]=new[] {ItemType.PowerGlove,ItemType.MechanicalGlove,ItemType.FireGauntlet,ItemType.BerserkerGlove},
            [ItemType.PowerGlove]=new[] {ItemType.MechanicalGlove,ItemType.FireGauntlet,ItemType.BerserkerGlove},
            [ItemType.MechanicalGlove]=new[] {ItemType.FireGauntlet},
            [ItemType.MagicQuiver]=new[] {ItemType.MoltenQuiver,ItemType.StalkersQuiver},
            [ItemType.RifleScope]=new[] {ItemType.SniperScope,ItemType.ReconScope},
            [ItemType.SniperScope]=new[] {ItemType.ReconScope},
            [ItemType.BandofStarpower]=new[] {ItemType.ManaRegenerationBand,ItemType.MagicCuffs,ItemType.CelestialCuffs},
            [ItemType.MagicCuffs]=new[] {ItemType.CelestialCuffs},
            [ItemType.CelestialMagnet]=new[] {ItemType.CelestialCuffs,ItemType.CelestialEmblem,ItemType.ArcaneFlower},
            [ItemType.ManaFlower]=new[] {ItemType.ArcaneFlower,ItemType.ManaCloak},
            [ItemType.StarCloak]=new[] {ItemType.StarVeil,ItemType.BeeCloak,ItemType.ManaCloak},
            [ItemType.CrossNecklace]=new[] {ItemType.StarVeil},
            [ItemType.NecromanticScroll]=new[] {ItemType.PapyrusScarab},
            [ItemType.HerculesBeetle]=new[] {ItemType.PapyrusScarab},
            [ItemType.SnakeBand]=new[] {ItemType.OuroborosRing},
            [ItemType.MobiusStrip]=new[] {ItemType.OuroborosRing},
            [ItemType.SilverBracer]=new[] {ItemType.TwilightGrasp},
            [ItemType.WickedArmlet]=new[] {ItemType.TwilightGrasp,ItemType.ArmletOfRuin},
            [ItemType.FrozenTurtleShell]=new[] {ItemType.FrozenShield},
            [ItemType.PaladinsShield]=new[] {ItemType.FrozenShield,ItemType.HeroShield},
            [ItemType.SpectreBoots]=new[] {ItemType.LightningBoots,ItemType.FrostsparkBoots,ItemType.TerrasparkBoots},
            [ItemType.LightningBoots]=new[] {ItemType.FrostsparkBoots,ItemType.TerrasparkBoots},
            [ItemType.FrostsparkBoots]=new[] {ItemType.TerrasparkBoots},
            [ItemType.ObsidianShield]=new[] {ItemType.AnkhShield},
            [ItemType.CobaltShield]=new[] {ItemType.ObsidianShield,ItemType.AnkhShield},
        };
        internal static bool Superseded(ItemType type,IReadOnlyList<ItemStack> worn)
        {
            if(!Upgrades.TryGetValue(type,out var higher)) return false;
            foreach(var stack in worn)
                if(!stack.IsEmpty)
                    foreach(var t in higher) if(stack.Type==t) return true;
            return false;
        }
    }
}
