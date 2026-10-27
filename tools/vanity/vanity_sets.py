"""Terraria's vanity items as the mod takes them: which are left out, and how they group into costume sets.

Read by gen_vanity.py. Data: tools/vanilla-data/vanity/vanity-runtime.json (DumpVanity.cs) and vanity-zh.json.
"""
import json
import os
import re

HERE = os.path.dirname(os.path.abspath(__file__))
DATA = os.path.join(HERE, "..", "vanilla-data", "vanity")

# No look on the player in Hallownest: music, skies, voices, the cursor, a toy and a shader.
EXCLUDED_PREFIXES = ("MusicBox", "PlayerVoice")
EXCLUDED = {
    "RainbowCursor", "UnicornonaStick", "DontStarveShaderItem", "CritterShampoo",
}

SLOTS = ["headSlot", "bodySlot", "legSlot", "handOnSlot", "handOffSlot", "backSlot", "frontSlot", "shoeSlot",
         "waistSlot", "wingSlot", "shieldSlot", "neckSlot", "faceSlot", "balloonSlot", "beardSlot"]

# Terraria's developer sets (Player.TryGettingDevArmor's 21 cases): what one case gives, the first the one the
# boss rolls for (BossDropPlan.DeveloperItems) and the rest paid with it. Names are ItemID names.
DEV_SETS = [
    ["RedsWings", "RedsHelmet", "RedsBreastplate", "RedsLeggings", "RedsYoyo"],
    ["CenxsBreastplate", "CenxsTiara", "CenxsLeggings", "CenxsWings"],
    ["CenxsDress", "CenxsTiara", "CenxsDressPants", "CenxsWings"],
    ["CrownosWings", "CrownosMask", "CrownosBreastplate", "CrownosLeggings"],
    ["WillsWings", "WillsHelmet", "WillsBreastplate", "WillsLeggings"],
    ["JimsWings", "JimsHelmet", "JimsBreastplate", "JimsLeggings"],
    ["AaronsHelmet", "AaronsBreastplate", "AaronsLeggings"],
    ["DTownsWings", "DTownsHelmet", "DTownsBreastplate", "DTownsLeggings"],
    ["BejeweledValkyrieWing", "BejeweledValkyrieHead", "BejeweledValkyrieBody", "ValkyrieYoyo"],
    ["Yoraiz0rWings", "Yoraiz0rHead", "Yoraiz0rDarkness", "Yoraiz0rShirt", "Yoraiz0rPants"],
    ["SkiphsWings", "SkiphsHelm", "SkiphsShirt", "SkiphsPants"],
    ["LokisWings", "LokisHelm", "LokisShirt", "LokisPants"],
    ["ArkhalisWings", "Arkhalis", "ArkhalisHat", "ArkhalisShirt", "ArkhalisPants"],
    ["LeinforsWings", "LeinforsHat", "LeinforsShirt", "LeinforsPants", "LeinforsAccessory"],
    ["GhostarsWings", "GhostarSkullPin", "GhostarShirt", "GhostarPants"],
    ["SafemanWings", "SafemanSunHair", "SafemanSunDress", "SafemanDressLeggings"],
    ["FoodBarbarianWings", "FoodBarbarianHelm", "FoodBarbarianArmor", "FoodBarbarianGreaves"],
    ["GroxTheGreatWings", "GroxTheGreatHelm", "GroxTheGreatArmor", "GroxTheGreatGreaves"],
    ["ChickenBonesWings", "ChickenBonesHead", "ChickenBonesBody", "ChickenBonesLegs", "ChickenBonesRobe"],
    ["KazzymodusWings", "KazzymodusHood", "KazzymodusChestpiece", "KazzymodusLeggings"],
    ["LunasWings", "LunasHead", "LunasBody", "LunasLegs", "LunasCloak"],
]

# Pieces that go together though their names or numbers part (and ones that look alike but don't).
EXTRA_SETS = [
    ["FamiliarWig", "FamiliarShirt", "FamiliarPants"],
    ["PharaohsMask", "PharaohsRobe"],
    ["Tiara", "PrincessDress"],
    ["PrincessHat", "PrincessDressNew"],
    ["WitchHat", "WitchDress", "WitchBoots"],
    ["SailorHat", "SailorShirt", "SailorPants"],
    ["WhiteLunaticHood", "WhiteLunaticRobe"],
    ["BlueLunaticHood", "BlueLunaticRobe"],
    ["TreeMask", "TreeShirt", "TreeTrunks"],
    ["SnowHat", "UglySweater"],
    ["DyeTraderTurban", "DyeTraderRobe"],
    ["ClothierJacket", "ClothierPants"],
    ["LincolnsHood", "LincolnsHoodie", "LincolnsPants"],
    ["GameMasterShirt", "GameMasterPants"],
    ["PrettyPinkRibbon", "PrettyPinkDressSkirt", "PrettyPinkDressPants"],
    ["UndertakerHat", "UndertakerCoat"],
    ["VictorianGothHat", "VictorianGothDress"],
    ["DrManFlyMask", "DrManFlyLabCoat"],
    ["CapricornMask", "CapricornChestplate", "CapricornLegs", "CapricornTail"],
    ["PrinceUniform", "PrincePants", "PrinceCape"],
    ["RoyalTiara", "RoyalDressTop", "RoyalDressBottom", "RoyalScepter", "GlassSlipper"],
    ["WilsonShirt", "WilsonPants", "WilsonBeardShort"],
    ["WillowShirt", "WillowSkirt"],
    ["MaidHead", "MaidShirt", "MaidPants"],
    ["MaidHead2", "MaidShirt2", "MaidPants2"],
    ["GolfHat", "GolfShirt", "GolfPants"],
    ["GraduationCapBlue", "GraduationGownBlue"],
    ["GraduationCapMaroon", "GraduationGownMaroon"],
    ["GraduationCapBlack", "GraduationGownBlack"],
    ["ChippysHead", "ChippysBody", "ChippysLegs", "ChippysHeadband"],
    ["DogEars", "DogTail"],
    ["FoxEars", "FoxTail"],
    ["LizardEars", "LizardTail"],
    ["BunnyEars", "BunnyTail"],
    ["BlueBikiniBody", "BlueBikiniLegs"],
    ["StarPrincessCrown", "StarPrincessDress"],
    ["TheBrideHat", "TheBrideDress"],
    ["BrideofFrankensteinMask", "BrideofFrankensteinDress"],
    ["ReaperHood", "ReaperRobe"],
    ["GhostMask", "GhostShirt"],
    ["PixieShirt", "PixiePants"],
    ["DryadCoverings", "DryadLoincloth"],
    ["TreasureHunterShirt", "TreasureHunterPants"],
    ["MermaidAdornment", "MermaidTail", "SeashellHairpin"],
    ["FishCostumeMask", "FishCostumeShirt", "FishCostumeFinskirt"],
    ["LamiaHat", "LamiaShirt", "LamiaPants"],
    ["TopHat", "TuxedoShirt", "TuxedoPants"],
    ["RuneHat", "RuneRobe"],
    ["MoonLordBody"],
    ["WhiteTuxedoShirt", "WhiteTuxedoPants"],
    ["FallenTuxedoShirt", "FallenTuxedoPants"],
    ["TheDoctorsShirt", "TheDoctorsPants"],
    ["PalworldPalMetalArmorBody", "PalworldPalMetalArmorLegs"],
]

def tokens(name):
    return re.findall(r"[A-Z][a-z0-9]*|[a-z0-9]+", name)

def load():
    runtime = json.load(open(os.path.join(DATA, "vanity-runtime.json"), encoding="utf-8"))
    zh = json.load(open(os.path.join(DATA, "vanity-zh.json"), encoding="utf-8"))
    return runtime, zh

def included(item):
    n = item["name"]
    return not (n.startswith(EXCLUDED_PREFIXES) or n.endswith("Monolith") or n in EXCLUDED)

def kind(item):
    if item.get("headSlot", -1) >= 0 and not item["accessory"]: return "H"
    if item.get("bodySlot", -1) >= 0 and not item["accessory"]: return "B"
    if item.get("legSlot", -1) >= 0 and not item["accessory"]: return "L"
    return "A"

def group(items):
    """Costume sets: the explicit lists, then runs of head, body and legs a few numbers apart sharing a first word."""
    by_name = {i["name"]: i for i in items}
    taken = {}
    sets = []
    def add(names):
        names = [n for n in names if n in by_name and n not in taken]
        if not names: return
        for n in names: taken[n] = len(sets)
        sets.append(names)
    for s in DEV_SETS: add(s)
    for s in EXTRA_SETS: add(s)
    run = []
    def flush():
        if run: add([i["name"] for i in run])
        run.clear()
    for i in sorted(items, key=lambda i: i["id"]):
        if i["name"] in taken: flush(); continue
        k = kind(i)
        if run:
            last = run[-1]
            same = tokens(last["name"])[0] == tokens(i["name"])[0]
            kinds = {kind(x) for x in run}
            if same and i["id"] - last["id"] <= 3 and k != "A" and k not in kinds:
                run.append(i)
                continue
            flush()
        run.append(i)
        if k == "A": flush()
    flush()
    return sets
