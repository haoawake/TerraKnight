"""Terraria's vanity items into TerraKnight: their definitions, who drops them, and the look tables the player's drawing reads.

Run: python -I tools/vanity/gen_vanity.py   (from anywhere; paths are the repo's)
Writes:
  src/TerraKnight/Items/ItemsVanity.cs        the ItemDefs and their stage/source/value
  src/TerraKnight/Items/VanityDropsData.cs    carriers (record -> lead piece, 1 in N), set mates, developer sets
  src/TerraKnight/Player/VanityLooksData.cs   ArmorIDs.*.Sets the look code needs
and appends the new ItemType names to Items.cs once (append-only).
"""
import json
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import vanity_sets as vs  # noqa: E402

REPO = os.path.normpath(os.path.join(HERE, "..", ".."))
SRC = os.path.join(REPO, "src", "TerraKnight")

SPAWNED = {"Flukefly", "HatcherBaby", "BabyHatcher", "JellyCrawler"}
ELITES = {"ZombieGuard", "BigBee", "MushroomBrawler", "RoyalGuard", "FatFluke", "MawlekTurret", "FlameBearerLarge",
          "FlameBearerMed", "GiantHopper", "GorgeousHusk", "GreatShieldZombie", "HeavyMantis", "SlashSpider", "PaleLurker"}

# The bosses' masks (Terraria's bags give one 1 time in 7), by the vanilla boss each HK boss stands for
# (VanillaBossProgression), and the Old One's Army's in the Colosseum.
MASKS = {
    "KingSlimeMask": ["FalseKnight"], "EyeMask": ["Hornet"], "EaterMask": ["DungDefender"], "BrainMask": ["FlukeMother"],
    "BeeMask": ["GhostHu"], "SkeletronMask": ["MantisLord"], "DeerclopsMask": ["MegaBeamMiner"],
    "FleshMask": ["BlackKnight", "JarCollector", "MageLord@5", "InfectedKnight"], "QueenSlimeMask": ["HiveKnight", "Oblobble"],
    "DestroyerMask": ["Hornet@4"], "TwinMask": ["GhostMarkoth"], "SkeletronPrimeMask": ["InfectedKnight@5"],
    "PlanteraMask": ["TraitorLord"], "GolemMask": ["LobsterLancer"], "JackOLanternMask": ["Grimm"],
    "DukeFishronMask": ["WhiteDefender"], "FairyQueenMask": ["NightmareGrimm"], "BossMaskCultist": ["HollowKnight"],
    "BossMaskMoonlord": ["FinalBoss"], "BossMaskOgre": ["ColWorm"], "BossMaskDarkMage": ["ElectricMage"],
    "BossMaskBetsy": ["ColFlyingSentry"],
}
MASK_DEN = 7

# Costumes the user can tell a boss or an elite by (its look, its place), the set by its first piece.
HAND = {
    "GiantBow": ["BigFly"], "ButcherMask": ["Mawlek"], "GoldCrown": ["BigBuzzer"], "TreeMask": ["MegaMossCharger"],
    "RuneHat": ["GhostAladar"], "GhostMask": ["GhostNoEyes"], "WizardsHat": ["MageKnight"], "WhiteLunaticHood": ["MageLord"],
    "BlueLunaticHood": ["MageLord@5"], "RoninHat": ["GhostXero"], "FishCostumeMask": ["MegaJellyfish"],
    "HerosHat": ["FalseKnight@5"], "SpaceCreatureMask": ["MimicSpider"], "ReaperHood": ["GhostGalien"],
    "FlowerBoyHat": ["GhostMarmu"], "PrinceUniform": ["GreyPrince"], "TimelessTravelerHood": ["NailBros"],
    "PeddlersHat": ["Nailsage"], "DyeTraderTurban": ["Paintmaster"], "HallowedCrown": ["HollowKnightPrime"],
    "ClownHat": ["Zote"], "Kimono": ["GhostHu"], "PlaguebringerHelmet": ["GhostMarkoth"],
    "TopHat": ["GorgeousHusk"], "RoyalTiara": ["RoyalGuard"], "BeeHat": ["BigBee"], "MushroomHat": ["MushroomBrawler"],
    "KarateTortoiseMask": ["HeavyMantis"], "SWATHelmet": ["GreatShieldZombie"], "CowboyHat": ["ZombieGuard"],
    "SailorHat": ["FatFluke"], "WitchHat": ["FlameBearerLarge"], "ScarecrowHat": ["FlameBearerMed"],
    "WolfMask": ["GiantHopper"], "VampireMask": ["SlashSpider"], "UndertakerHat": ["PaleLurker"],
    "PumpkinMask": ["MawlekTurret"], "CapricornMask": ["ColMiner"], "DeadCellsBeheadedHead": ["ColShield"],
    "PedguinHat": ["ColHopper"], "HeroicisHead": ["ColMosquito"], "ChippysHead": ["ColRoller"], "LincolnsHood": ["Blobble"],
}
DEN_BOSS, DEN_ELITE, DEN_ARENA, DEN_SET, DEN_SINGLE = 8, 12, 20, 40, 30

# Where a costume belongs, by a word of its pieces' names: the areas of the enemies that wear it.
THEMES = [
    (["Fish", "Mermaid", "Sailor", "Pirate", "Buccaneer", "Bikini", "Swimsuit", "Swimshorts", "Seashell", "Umbrella"], ["water", "fog"]),
    (["Mushroom"], ["fungal"]),
    (["Bee"], ["hive"]),
    (["Ghost", "Reaper", "Vampire", "Undertaker", "Funeral", "Skull", "Plaguebringer", "Bride", "Victorian", "Butcher", "Man", "Eyebrella"], ["rest", "deep", "abyss"]),
    (["Robot", "Cyborg", "Martian", "Space", "TVHead", "Engineering", "Steampunk", "Hi", "Welding", "Palworld", "Goggles"], ["crystal"]),
    (["Tuxedo", "Top", "White", "Fallen", "Royal", "Prince", "Tiara", "Crown", "Maid", "Graduation", "Chef", "Nurse",
      "Wizards", "Fedora", "Gangsta", "Balla", "The", "Tax", "Clothier", "Peddlers", "Dye", "Plumbers", "Game", "Pretty",
      "Lincolns", "Super", "Heroicis", "Golf", "Wilson", "Willow", "Diamond", "Platinum", "Gold", "Star", "Lilac"], ["city"]),
    (["Tree", "Flower", "Garland", "Dryad", "Fox", "Wolf", "Cat", "Bunny", "Lizard", "Panda", "Dog", "Leprechaun", "Pixie",
      "Floret", "Jungle", "Unicorn", "Rabbit", "Summer", "Heart", "Vulkelf", "Goblorc"], ["green", "fog"]),
    (["Mummy", "Pharaohs", "Ancient", "Lamia", "Cowboy", "Archaeologists", "Fez", "Ronin", "Treasure", "Rock"], ["edge", "deep"]),
    (["Santa", "Elf", "Parka", "Snow", "Reindeer", "Ugly", "Mrs", "Winter", "Beanie", "Tam"], ["crystal", "cliffs"]),
    (["Pumpkin", "Scarecrow", "Witch", "Creeper", "Jack", "Karate", "Demon", "Devil", "Hunter", "Crimson", "Mysterious", "Red"], ["deep", "fungal", "green"]),
    (["Lunatic", "Rune", "Moon", "Sun", "Capricorn", "Timeless", "Chippys", "Pedguin", "Aviator", "Badgers", "Jims", "Dizzy", "Angel", "Ginger"], ["abyss", "palace", "edge"]),
    (["Party", "Clown", "Mime", "Guy", "Familiar", "Heros", "Robe", "Sunglasses", "Fish", "Eye", "Mushroom", "Giant", "Kimono", "Gi", "Tuxedo"], ["cross", "city"]),
]


def area(scene):
    pre = [("Deepnest_East", "edge"), ("Deepnest", "deep"), ("Crossroads", "cross"), ("Town", "cross"), ("Tutorial", "cross"),
           ("Cliffs", "cliffs"), ("Fungus1", "green"), ("Fungus2", "fungal"), ("Fungus3", "fog"), ("Ruins", "city"),
           ("Waterways", "water"), ("GG_Pipeway", "water"), ("GG_Waterways", "water"), ("Mines", "crystal"),
           ("RestingGrounds", "rest"), ("Hive", "hive"), ("Abyss", "abyss"), ("White_Palace", "palace"),
           ("Room_Colosseum", "colo"), ("GG_", "godhome"), ("Dream", "godhome")]
    for p, a in pre:
        if scene.startswith(p):
            return a
    return "cross"


AREA_TAGS = {"国王山道": "cross", "遗忘十字路": "cross", "苍绿之径": "green", "雾之峡谷": "fog", "真菌荒地": "fungal",
             "泪水之城": "city", "水晶山峰": "crystal", "安息之地": "rest", "皇家水道": "water", "呼啸悬崖": "cliffs",
             "远古盆地": "abyss", "王国边缘": "edge", "愚人斗兽场": "colo", "蜂巢": "hive", "深巢": "deep", "王后花园": "green",
             "深渊": "abyss", "白色宫殿": "palace"}


def route_areas():
    """The monster book's area of each record (BestiaryRouteData: the area it is first met in)."""
    src = open(os.path.join(SRC, "Items", "BestiaryRouteData.cs"), encoding="utf-8").read()
    names = re.findall(r'"([^"]+)"', re.search(r'areaNames =\s*\{(.*?)\};', src, re.S).group(1))
    groups = re.findall(r'new\[\] \{([^}]*)\}', re.search(r'areaRecords =\s*\{(.*?)\n        \};', src, re.S).group(1))
    out = {}
    for name, g in zip(names, groups):
        for r in re.findall(r'"([^"]+)"', g):
            out[r] = AREA_TAGS.get(name, "godhome")
    return out


def roster():
    src = open(os.path.join(SRC, "Items", "EnemyDropData.cs"), encoding="utf-8").read()
    rows = re.findall(r'\["([^"]+)"\] = new Entry \{ En = "[^"]*", Zh = "([^"]*)", Stage = (\d+), Boss = (true|false), Kind = "([^"]*)", Hp = (-?\d+), Geo = -?\d+, Scene = "([^"]*)"', src)
    routes = route_areas()
    return {k: {"zh": zh, "stage": int(st), "boss": b == "true", "kind": kind, "hp": int(hp), "scene": sc, "area": routes.get(k, area(sc))}
            for k, zh, st, b, kind, hp, sc in rows}


def enum_names():
    src = open(os.path.join(SRC, "Items", "Items.cs"), encoding="utf-8").read()
    m = re.search(r'internal enum ItemType\s*\{(.*?)\n    \}', src, re.S)
    body = re.sub(r'//[^\n]*', '', m.group(1))
    return [x.strip().split('=')[0].strip() for x in body.replace('\n', ' ').split(',') if x.strip()], src


def cs(s):
    return '"' + s.replace('\\', '\\\\').replace('"', '\\"').replace('\n', '\\n') + '"'


def main():
    runtime, zh = vs.load()
    items = [i for i in runtime["items"] if vs.included(i)]
    by_name = {i["name"]: i for i in items}
    names, items_src = enum_names()
    known = set(names)
    new = [i for i in items if i["name"] not in known]
    ros = roster()

    def tip(name, depth=0):
        t = zh["ItemTooltip"].get(name)
        if not t or depth > 3:
            return None
        t = re.sub(r'\{\$CommonItemTooltip\.(\w+)\}', lambda m: zh["CommonItemTooltip"].get(m.group(1), ""), t)
        t = re.sub(r'\{\$ItemTooltip\.(\w+)\}', lambda m: tip(m.group(1), depth + 1) or "", t)
        lines = [l for l in t.split("\n") if l.strip() and "{" not in l]
        return "\n".join(lines) or None

    # --- the sets and who drops them ---
    sets = vs.group(items)
    dev_names = {n for s in vs.DEV_SETS for n in s}
    lead_of = {}
    for s in sets:
        for n in s:
            lead_of[n] = s[0]
    carriers = {}  # lead -> [(record, den)]
    for mask, recs in MASKS.items():
        carriers[mask] = [(r, DEN_ARENA if ros.get(r, {}).get("kind") == "arena" else MASK_DEN) for r in recs]
    for lead, recs in HAND.items():
        if lead not in by_name and lead not in known:
            raise SystemExit("hand lead missing: " + lead)
        dens = []
        for r in recs:
            e = ros[r]
            den = DEN_BOSS if e["boss"] else DEN_ARENA if e["kind"] == "arena" else DEN_ELITE
            dens.append((r, den))
        carriers[lead_of.get(lead, lead)] = dens
    commons = {k: e for k, e in ros.items() if not e["boss"] and e["hp"] > 0 and "@" not in k and k not in SPAWNED and k not in ELITES
               and e["kind"] in ("normal", "conditional", "pogo", "unique", "quest")}
    load = {k: 0 for k in commons}
    for lead, cs_ in carriers.items():
        for r, _ in cs_:
            if r in load:
                load[r] += 1

    def target_stage(s):
        rare = max(by_name[n]["rare"] for n in s if n in by_name)
        return {0: 0, 1: 1, 2: 1, 3: 2, 4: 3, 5: 3}.get(rare, 4)

    for s in sets:
        lead = s[0]
        if lead in carriers or lead in dev_names or lead in known:
            continue
        words = set()
        for n in s:
            words.update(vs.tokens(n))
        areas = []
        for keys, ar in THEMES:
            if words & set(keys):
                areas = ar
                break
        want = target_stage(s)
        cands = [k for k, e in commons.items() if (not areas or e["area"] in areas)]
        if not cands:
            cands = list(commons)
        def score(k):
            e = commons[k]
            return (load[k] >= 2, load[k], abs(e["stage"] - want), k)
        pick = min(cands, key=score)
        load[pick] += 1
        carriers[lead] = [(pick, DEN_SET if len(s) > 1 else DEN_SINGLE)]

    # A piece's stage: its set's first carrier's (the least); the developer sets' a hardmode boss's.
    stage = {}
    for s in sets:
        lead = s[0]
        if lead in dev_names:
            st = 3
        elif lead in carriers:
            st = min(ros[r]["stage"] for r, _ in carriers[lead])
        else:
            continue
        for n in s:
            stage[n] = st
    missing = [i["name"] for i in new if i["name"] not in stage]
    if missing:
        raise SystemExit("no source: " + ", ".join(missing))

    # --- ItemType names (append-only) ---
    marker = "// Append-only: Terraria's vanity items"
    if marker not in items_src:
        to_add = [i["name"] for i in sorted(new, key=lambda i: i["id"])]
        lines, line = [], "        "
        for n in to_add:
            if len(line) + len(n) + 2 > 130:
                lines.append(line.rstrip())
                line = "        "
            line += n + ", "
        lines.append(line.rstrip())
        block = "        " + marker + " (Items/ItemsVanity.cs, tools/vanity/gen_vanity.py, 2026-10-25).\n" + "\n".join(lines) + "\n"
        anchor = "        BejeweledStaff,\n"
        assert anchor in items_src
        items_src = items_src.replace(anchor, anchor + block, 1)
        open(os.path.join(SRC, "Items", "Items.cs"), "w", encoding="utf-8").write(items_src)

    # --- ItemsVanity.cs ---
    acc_fields = [("handOnSlot", "handOn"), ("handOffSlot", "handOff"), ("backSlot", "back"), ("frontSlot", "front"),
                  ("shoeSlot", "shoe"), ("waistSlot", "waist"), ("shieldSlot", "shield"), ("neckSlot", "neck"),
                  ("faceSlot", "face"), ("balloonSlot", "balloon"), ("beardSlot", "beard")]
    out = []
    out.append("// <generated> by tools/vanity/gen_vanity.py from tools/vanilla-data/vanity: edit the tables there. </generated>")
    out.append("using System.Collections.Generic;\n\nnamespace TerraKnight.Items\n{\n    internal static partial class ItemDb\n    {")
    out.append("        /// <summary>Terraria's vanity items (Item.vanity; Item.SetDefaults' slots, rarity and value; zh-Hans names and tooltips).</summary>")
    out.append("        static void AddVanity(List<ItemDef> list)\n        {")
    for i in sorted(new, key=lambda i: i["id"]):
        n = i["name"]
        args = [f"ItemType.{n}", cs(zh["ItemName"].get(n, n)), str(i["id"]), str(i["rare"])]
        t = tip(n)
        opt = []
        if t:
            opt.append("tip: " + cs(t))
        if not i["accessory"] and i.get("headSlot", -1) >= 0:
            opt.append(f"slot: ArmorSlot.Head, sheet: {i['headSlot']}")
        elif not i["accessory"] and i.get("bodySlot", -1) >= 0:
            opt.append(f"slot: ArmorSlot.Body, sheet: {i['bodySlot']}")
        elif not i["accessory"] and i.get("legSlot", -1) >= 0:
            opt.append(f"slot: ArmorSlot.Legs, sheet: {i['legSlot']}")
        else:
            opt.append("acc: true")
            for f, a in acc_fields:
                if i.get(f, -1) > 0:
                    opt.append(f"{a}: {i[f]}")
        out.append(f"            list.Add(Vanity({', '.join(args + opt)}));")
    out.append("        }\n")
    out.append("        static ItemDef Vanity(ItemType t, string name, int id, int rare, string tip = null, ArmorSlot slot = ArmorSlot.None, int sheet = 0, bool acc = false,")
    out.append("            int handOn = 0, int handOff = 0, int back = 0, int front = 0, int shoe = 0, int waist = 0, int shield = 0, int neck = 0, int face = 0, int balloon = 0, int beard = 0) => new ItemDef")
    out.append("        {")
    out.append("            Type = t, Name = name, Texture = \"Item_\" + id, Rare = rare, Tooltip = tip, Vanity = true, Armor = slot, ArmorSheet = sheet,")
    out.append("            Accessory = acc ? AccessoryEffect.Passive : AccessoryEffect.None, HandOn = handOn, HandOff = handOff, Back = back, Front = front, Shoe = shoe,")
    out.append("            Waist = waist, Shield = shield, Neck = neck, Face = face, Balloon = balloon, Beard = beard,")
    out.append("        };\n")
    out.append("        /// <summary>Their stage (the least of the enemies that drop them: VanityDrops), all dropped, and Item.value.</summary>")
    out.append("        static IEnumerable<(ItemType, int, ItemSource, int)> VanitySources()\n        {")
    for i in sorted(new, key=lambda i: i["id"]):
        out.append(f"            yield return (ItemType.{i['name']}, {stage[i['name']]}, ItemSource.Drop, {i['value']});")
    out.append("        }\n    }\n}")
    open(os.path.join(SRC, "Items", "ItemsVanity.cs"), "w", encoding="utf-8").write("\n".join(out) + "\n")

    # --- VanityDropsData.cs ---
    exists = known | {i["name"] for i in new}
    out = ["// <generated> by tools/vanity/gen_vanity.py: edit the tables there. </generated>",
           "using System.Collections.Generic;\n\nnamespace TerraKnight.Items\n{\n    internal static partial class VanityDrops\n    {",
           "        /// <summary>Who drops a costume: the enemy's record, the set's first piece, one chance in N (the rest of the set comes with it).</summary>",
           "        static readonly (string Record, ItemType Lead, int Den)[] rows =\n        {"]
    for lead in sorted(carriers, key=lambda n: by_name[n]["id"] if n in by_name else 0):
        for r, den in carriers[lead]:
            out.append(f"            ({cs(r)}, ItemType.{lead}, {den}),  // {ros[r]['zh']}")
    out.append("        };\n")
    out.append("        /// <summary>The rest of a set, paid with its first piece; a developer set's with the piece the boss rolled.</summary>")
    out.append("        static readonly Dictionary<ItemType, ItemType[]> mates = new Dictionary<ItemType, ItemType[]>\n        {")
    for s in sets:
        if len(s) > 1 and s[0] not in dev_names:
            out.append(f"            [ItemType.{s[0]}] = new[] {{ {', '.join('ItemType.' + n for n in s[1:])} }},")
    for s in vs.DEV_SETS:
        rest = [n for n in s[1:] if n in exists]
        out.append(f"            [ItemType.{s[0]}] = new[] {{ {', '.join('ItemType.' + n for n in rest)} }},")
    out.append("        };\n")
    out.append("        /// <summary>Terraria's developer sets (Player.TryGettingDevArmor's 21): the piece a hardmode boss rolls for each (BossDropPlan.DeveloperItems).</summary>")
    out.append("        internal static readonly ItemType[] DeveloperReps =\n        {")
    out.append("            " + ", ".join("ItemType." + s[0] for s in vs.DEV_SETS) + ",")
    out.append("        };\n    }\n}")
    open(os.path.join(SRC, "Items", "VanityDropsData.cs"), "w", encoding="utf-8").write("\n".join(out) + "\n")

    # --- VanityLooksData.cs ---
    arm = runtime["armor"]
    def ints(lst):
        return ", ".join(str(x) for x in lst)
    def mapping(d):
        return ", ".join(f"[{k}] = {v}" for k, v in sorted(((int(k), v) for k, v in d["values"].items())))
    out = ["// <generated> by tools/vanity/gen_vanity.py from Terraria's ArmorIDs.*.Sets (vanity-runtime.json). </generated>",
           "using System.Collections.Generic;\n\nnamespace TerraKnight.Player\n{\n    internal static partial class VanityLooks\n    {"]
    def hs(name, lst, doc):
        out.append(f"        /// <summary>{doc}</summary>")
        out.append(f"        static readonly HashSet<int> {name} = new HashSet<int> {{ {ints(lst)} }};")
    def dm(name, d, doc):
        out.append(f"        /// <summary>{doc}</summary>")
        out.append(f"        static readonly Dictionary<int, int> {name} = new Dictionary<int, int> {{ {mapping(d)} }};")
    H, B, Bk, F, W, Fc, Bd, L, Sh, Hn = (arm["Head"], arm["Body"], arm["Back"], arm["Front"], arm["Waist"], arm["Face"], arm["Beard"],
                                        arm["Legs"], arm["Shoe"], arm["HandOn"])
    dm("HeadFrontToBack", H["FrontToBackID"], "Head.Sets.FrontToBackID: a head drawn again behind the body (DrawPlayer_01_3_BackHead).")
    hs("HeadUseSkinColor", H["UseSkinColor"], "Head.Sets.UseSkinColor: drawn in the skin's colour.")
    hs("HeadHidesHead", H["HidesHead"], "Head.Sets.HidesHead: the head's skin not drawn under it.")
    hs("HeadPreventFaceFlower", H["PreventFaceFlowerDraw"], "Head.Sets.PreventFaceFlowerDraw.")
    hs("HeadPreventFaceMask", H["PreventFaceMaskDraw"], "Head.Sets.PreventFaceMaskDraw.")
    hs("HeadFaceMaskUnder", H["DrawFaceMaskUnderHeadLayer"], "Head.Sets.DrawFaceMaskUnderHeadLayer.")
    hs("HeadPreventBeard", H["PreventBeardDraw"], "Head.Sets.PreventBeardDraw.")
    hs("HeadAltFaceHead", H["UseAltFaceHeadDraw"], "Head.Sets.UseAltFaceHeadDraw: the face-head piece swapped for its alternative (Face.Sets.AltFaceHead).")
    dm("BodyCapeBack", B["IncludedCapeBack"], "Body.Sets.IncludedCapeBack: a body's own cape (a back sheet), a man's.")
    dm("BodyCapeBackFemale", B["IncludedCapeBackFemale"], "Body.Sets.IncludedCapeBackFemale: a woman's.")
    dm("BodyCapeFront", B["IncludedCapeFront"], "Body.Sets.IncludedCapeFront: its front (a front sheet).")
    out.append("        /// <summary>Body.Sets.IncludeCapeFrontAndBack: both halves, when no other cape is worn.</summary>")
    pairs = ", ".join(f"[{k}] = ({v['frontCape']}, {v['backCape']})" for k, v in sorted(B["IncludeCapeFrontAndBack"].items(), key=lambda kv: int(kv[0])))
    out.append(f"        static readonly Dictionary<int, (int Front, int Back)> BodyCapeBoth = new Dictionary<int, (int, int)> {{ {pairs} }};")
    hs("BodyShowsShouldersJumping", B["showsShouldersWhileJumping"], "Body.Sets.showsShouldersWhileJumping: its shoulders drawn on the jump frame.")
    hs("BodyShouldersBack", B["shouldersAreAlwaysInTheBack"], "Body.Sets.shouldersAreAlwaysInTheBack.")
    hs("BodyNoHands", B["DisableHandOnAndOffAccDraw"], "Body.Sets.DisableHandOnAndOffAccDraw.")
    hs("BodyNoBelt", B["DisableBeltAccDraw"], "Body.Sets.DisableBeltAccDraw.")
    hs("BackBackpack", Bk["DrawInBackpackLayer"], "Back.Sets.DrawInBackpackLayer.")
    hs("BackTail", Bk["DrawInTailLayer"], "Back.Sets.DrawInTailLayer.")
    hs("FrontHidesShoulders", F["HidesCompositeShoulders"], "Front.Sets.HidesCompositeShoulders.")
    hs("WaistBelt", W["IsABelt"], "Waist.Sets.IsABelt.")
    hs("FacePreventHair", Fc["PreventHairDraw"], "Face.Sets.PreventHairDraw.")
    hs("FaceOverrideHelmet", Fc["OverrideHelmet"], "Face.Sets.OverrideHelmet: the head piece hidden under it.")
    hs("FaceUnderHair", Fc["DrawInFaceUnderHairLayer"], "Face.Sets.DrawInFaceUnderHairLayer.")
    hs("FaceMaskLayer", Fc["DrawInFaceMaskLayer"], "Face.Sets.DrawInFaceMaskLayer.")
    hs("FaceFlowerLayer", Fc["DrawInFaceFlowerLayer"], "Face.Sets.DrawInFaceFlowerLayer.")
    hs("FaceHeadLayer", Fc["DrawInFaceHeadLayer"], "Face.Sets.DrawInFaceHeadLayer.")
    dm("FaceAltHead", Fc["AltFaceHead"], "Face.Sets.AltFaceHead.")
    hs("BeardHairColor", Bd["UseHairColor"], "Beard.Sets.UseHairColor.")
    dm("ShoeFemale", Sh["MaleToFemaleID"], "Shoe.Sets.MaleToFemaleID.")
    out.append("    }\n}")
    open(os.path.join(SRC, "Player", "VanityLooksData.cs"), "w", encoding="utf-8").write("\n".join(out) + "\n")

    # A report of who drops what.
    rep = open(os.path.join(HERE, "vanity-drops.txt"), "w", encoding="utf-8")
    for lead in sorted(carriers, key=lambda n: by_name[n]["id"] if n in by_name else 0):
        s = next((x for x in sets if x[0] == lead), [lead])
        who = "、".join(f"{ros[r]['zh']}({r}) 1/{d}" for r, d in carriers[lead])
        print(f"{'+'.join(zh['ItemName'].get(n, n) for n in s)} ← {who}", file=rep)
    print(f"new items {len(new)}, sets {len(sets)}, carried {len(carriers)}, commons used {sum(1 for v in load.values() if v)}/{len(load)}")


if __name__ == "__main__":
    main()
