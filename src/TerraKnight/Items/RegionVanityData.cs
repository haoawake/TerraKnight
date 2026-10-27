// <generated> by tools/vanity/import_regional_vanity.py from output/regional-vanity-native: edit there. </generated>
namespace TerraKnight.Items
{
    internal static partial class ItemDb
    {
        /// <summary>Each area's costume piece (World.AreaBlocks.All's index) and the vanilla piece it recolours (a class of
        /// its own: ready whenever Build asks, whatever order ItemDb's fields start in).</summary>
        static (int Area, ItemType Made, ItemType From)[] regionVanity => RegionVanityTable.Rows;

        static class RegionVanityTable
        {
            internal static readonly (int Area, ItemType Made, ItemType From)[] Rows =
            {
                (0, ItemType.RegionVanityKingsPassHead, ItemType.TimelessTravelerHood),
                (0, ItemType.RegionVanityKingsPassBody, ItemType.TimelessTravelerRobe),
                (0, ItemType.RegionVanityKingsPassLegs, ItemType.TimelessTravelerBottom),
                (1, ItemType.RegionVanityDirtmouthHead, ItemType.GangstaHat),
                (1, ItemType.RegionVanityDirtmouthBody, ItemType.ClothierJacket),
                (1, ItemType.RegionVanityDirtmouthLegs, ItemType.ClothierPants),
                (2, ItemType.RegionVanityHowlingCliffsHead, ItemType.ParkaHood),
                (2, ItemType.RegionVanityHowlingCliffsBody, ItemType.ParkaCoat),
                (2, ItemType.RegionVanityHowlingCliffsLegs, ItemType.ParkaPants),
                (3, ItemType.RegionVanityCrossroadsHead, ItemType.SailorHat),
                (3, ItemType.RegionVanityCrossroadsBody, ItemType.SailorShirt),
                (3, ItemType.RegionVanityCrossroadsLegs, ItemType.SailorPants),
                (4, ItemType.RegionVanityGreenpathHead, ItemType.HerosHat),
                (4, ItemType.RegionVanityGreenpathBody, ItemType.HerosShirt),
                (4, ItemType.RegionVanityGreenpathLegs, ItemType.HerosPants),
                (5, ItemType.RegionVanityFungalWastesHead, ItemType.MushroomHat),
                (5, ItemType.RegionVanityFungalWastesBody, ItemType.MushroomVest),
                (5, ItemType.RegionVanityFungalWastesLegs, ItemType.MushroomPants),
                (6, ItemType.RegionVanityFogCanyonHead, ItemType.FloretProtectorHelmet),
                (6, ItemType.RegionVanityFogCanyonBody, ItemType.FloretProtectorChestplate),
                (6, ItemType.RegionVanityFogCanyonLegs, ItemType.FloretProtectorLegs),
                (7, ItemType.RegionVanityQueensGardensHead, ItemType.FlowerBoyHat),
                (7, ItemType.RegionVanityQueensGardensBody, ItemType.FlowerBoyShirt),
                (7, ItemType.RegionVanityQueensGardensLegs, ItemType.FlowerBoyPants),
                (8, ItemType.RegionVanityCityOfTearsHead, ItemType.TaxCollectorHat),
                (8, ItemType.RegionVanityCityOfTearsBody, ItemType.TaxCollectorSuit),
                (8, ItemType.RegionVanityCityOfTearsLegs, ItemType.TaxCollectorPants),
                (9, ItemType.RegionVanityRoyalWaterwaysHead, ItemType.PlumbersHat),
                (9, ItemType.RegionVanityRoyalWaterwaysBody, ItemType.PlumbersShirt),
                (9, ItemType.RegionVanityRoyalWaterwaysLegs, ItemType.PlumbersPants),
                (10, ItemType.RegionVanityCrystalPeakHead, ItemType.EngineeringHelmet),
                (10, ItemType.RegionVanityCrystalPeakBody, ItemType.CyborgShirt),
                (10, ItemType.RegionVanityCrystalPeakLegs, ItemType.CyborgPants),
                (11, ItemType.RegionVanityRestingGroundsHead, ItemType.FuneralHat),
                (11, ItemType.RegionVanityRestingGroundsBody, ItemType.FuneralCoat),
                (11, ItemType.RegionVanityRestingGroundsLegs, ItemType.FuneralPants),
                (12, ItemType.RegionVanityDeepnestHead, ItemType.PlaguebringerHelmet),
                (12, ItemType.RegionVanityDeepnestBody, ItemType.PlaguebringerChestplate),
                (12, ItemType.RegionVanityDeepnestLegs, ItemType.PlaguebringerGreaves),
                (13, ItemType.RegionVanityBeastsDenHead, ItemType.WolfMask),
                (13, ItemType.RegionVanityBeastsDenBody, ItemType.WolfShirt),
                (13, ItemType.RegionVanityBeastsDenLegs, ItemType.WolfPants),
                (14, ItemType.RegionVanityKingdomsEdgeHead, ItemType.CowboyHat),
                (14, ItemType.RegionVanityKingdomsEdgeBody, ItemType.CowboyJacket),
                (14, ItemType.RegionVanityKingdomsEdgeLegs, ItemType.CowboyPants),
                (15, ItemType.RegionVanityHiveHead, ItemType.BeeHat),
                (15, ItemType.RegionVanityHiveBody, ItemType.BeeShirt),
                (15, ItemType.RegionVanityHiveLegs, ItemType.BeePants),
                (16, ItemType.RegionVanityAncientBasinHead, ItemType.AncientArmorHat),
                (16, ItemType.RegionVanityAncientBasinBody, ItemType.AncientArmorShirt),
                (16, ItemType.RegionVanityAncientBasinLegs, ItemType.AncientArmorPants),
                (17, ItemType.RegionVanityAbyssHead, ItemType.SuperHeroMask),
                (17, ItemType.RegionVanityAbyssBody, ItemType.SuperHeroCostume),
                (17, ItemType.RegionVanityAbyssLegs, ItemType.SuperHeroTights),
                (18, ItemType.RegionVanityWhitePalaceHead, ItemType.GoldCrown),
                (18, ItemType.RegionVanityWhitePalaceBody, ItemType.PrinceUniform),
                (18, ItemType.RegionVanityWhitePalaceLegs, ItemType.PrincePants),
                (19, ItemType.RegionVanityBlackEggTempleHead, ItemType.ReaperHood),
                (19, ItemType.RegionVanityBlackEggTempleBody, ItemType.FallenTuxedoShirt),
                (19, ItemType.RegionVanityBlackEggTempleLegs, ItemType.FallenTuxedoPants),
                (20, ItemType.RegionVanityColosseumHead, ItemType.FoodBarbarianHelm),
                (20, ItemType.RegionVanityColosseumBody, ItemType.FoodBarbarianArmor),
                (20, ItemType.RegionVanityColosseumLegs, ItemType.FoodBarbarianGreaves),
                (21, ItemType.RegionVanityGrimmTroupeHead, ItemType.VampireMask),
                (21, ItemType.RegionVanityGrimmTroupeBody, ItemType.VampireShirt),
                (21, ItemType.RegionVanityGrimmTroupeLegs, ItemType.VampirePants),
                (22, ItemType.RegionVanityDreamHead, ItemType.PedguinHat),
                (22, ItemType.RegionVanityDreamBody, ItemType.PedguinShirt),
                (22, ItemType.RegionVanityDreamLegs, ItemType.PedguinPants),
                (23, ItemType.RegionVanityGodhomeHead, ItemType.RoyalTiara),
                (23, ItemType.RegionVanityGodhomeBody, ItemType.RoyalDressTop),
                (23, ItemType.RegionVanityGodhomeLegs, ItemType.RoyalDressBottom),
            };
        }
    }
}
