
# ---------------------------------------------------------------- the quest fish kept (catchable somewhere here)
for q in quest_ids:
    t = DD.TNAME[q]
    if t in NEW and t not in QUEST_AREAS: del NEW[t]

# ---------------------------------------------------------------- where the bait and moss drop (drops3.py adds them: fishing_drops.json)
BAIT_AREAS = {
    'Worm': ['国王山道', '遗忘十字路'], 'Snail': ['遗忘十字路'], 'Grasshopper': ['遗忘十字路'], 'Firefly': ['遗忘十字路', '安息之地'],
    'MonarchButterfly': ['苍绿之径'], 'SulphurButterfly': ['苍绿之径'], 'ZebraSwallowtailButterfly': ['苍绿之径'], 'UlyssesButterfly': ['苍绿之径'],
    'JuliaButterfly': ['苍绿之径'], 'LadyBug': ['苍绿之径'], 'Grubby': ['苍绿之径', '王后花园'],
    'GlowingSnail': ['真菌荒地'], 'Sluggy': ['真菌荒地'],
    'WaterStrider': ['雾之峡谷'], 'BlackDragonfly': ['雾之峡谷'], 'BlueDragonfly': ['雾之峡谷'], 'GreenDragonfly': ['雾之峡谷'],
    'OrangeDragonfly': ['雾之峡谷'], 'RedDragonfly': ['雾之峡谷'], 'YellowDragonfly': ['雾之峡谷'], 'LightningBug': ['雾之峡谷', '水晶山峰'],
    'RedAdmiralButterfly': ['王后花园'], 'PurpleEmperorButterfly': ['王后花园'], 'TreeNymphButterfly': ['王后花园'], 'Buggy': ['王后花园', '深巢'],
    'Stinkbug': ['泪水之城', '皇家水道'], 'Maggot': ['皇家水道', '泪水之城'], 'EnchantedNightcrawler': ['泪水之城'],
    'BlackScorpion': ['深巢'], 'Scorpion': ['王国边缘'],
    'HellButterfly': ['皇家水道'], 'Lavafly': ['王国边缘'], 'MagmaSnail': ['深渊', '远古盆地'],
}
GOLD_BAITS = ['GoldButterfly', 'GoldGrasshopper', 'GoldWorm', 'GoldDragonfly', 'GoldLadyBug', 'GoldWaterStrider']
MOSS_AREAS = {'LavaMoss': ['苍绿之径', '皇家水道'], 'KryptonMoss': ['苍绿之径'], 'XenonMoss': ['雾之峡谷'], 'ArgonMoss': ['水晶山峰'],
              'VioletMoss': ['深巢'], 'RainbowMoss': ['白色宫殿']}
AREA_STAGE = {a: v[0] for a, v in AREAS.items()}

# ---------------------------------------------------------------- stages
POLE_STAGE = {'WoodFishingPole': 0, 'ReinforcedFishingPole': 0, 'FisherofSouls': 1, 'Fleshcatcher': 1, 'BloodFishingRod': 2, 'FiberglassFishingPole': 2,
              'ScarabFishingRod': 4, 'MechanicsRod': 2, 'SittingDucksFishingRod': 2, 'HotlineFishingHook': 4, 'GoldenFishingRod': 0}
CRAFTED = {'WoodFishingPole', 'ReinforcedFishingPole', 'FisherofSouls', 'Fleshcatcher', 'AnglerTackleBag', 'LavaproofTackleBag'} | {DD.TNAME[i] for i in range(5140, 5147)}
ACC_STAGE = {'LavaFishingHook': 2, 'LavaproofTackleBag': 2}
MOSS_STAGE = {m: min(AREA_STAGE[a] for a in areas) for m, areas in MOSS_AREAS.items()}
BOBBER_MOSS = {5141: 'LavaMoss', 5142: 'KryptonMoss', 5143: 'XenonMoss', 5144: 'ArgonMoss', 5145: 'VioletMoss', 5146: 'RainbowMoss'}


def stage_of(t, spec):
    k = spec['kind']
    tid = spec['id']
    if t in CATCH_STAGE: return CATCH_STAGE[t]
    if k == 'pole': return POLE_STAGE.get(t, 0)
    if k == 'quest': return QUEST_AREAS[t][0]
    if t in BAIT_AREAS: return min(AREA_STAGE[a] for a in BAIT_AREAS[t])
    if t in GOLD_BAITS: return 1
    if t in ('ApprenticeBait',): return 0
    if t in ('JourneymanBait',): return 0
    if t in ('MasterBait',): return 1
    if t in MOSS_STAGE: return MOSS_STAGE[t]
    if tid in BOBBER_MOSS: return MOSS_STAGE[BOBBER_MOSS[tid]]
    if k == 'acc': return ACC_STAGE.get(t, 0)
    if t == 'ShuckedOyster': return CATCH_STAGE.get('Oyster', 4)
    return 0


# ---------------------------------------------------------------- crates: the areas they hold, shared out anew
def entry_items():
    """EnemyDropData.cs: each entry's scene and the items it drops (rows and sure groups)."""
    src = read(os.path.join(PROJ, 'Items', 'EnemyDropData.cs'))
    for m in re.finditer(r'Scene = "([^"]*)", Drops = new\[\] \{(.*?)\} \},', src):
        yield m.group(1), re.findall(r'ItemType\.(\w+)', m.group(2))


AREA_EQUIP = collections.defaultdict(set)
for scene, items in entry_items():
    a = area_name(scene)
    for t in items:
        if t in DD.STAGE and DD.SOURCE.get(t) == 'Drop' and DD.kind_of(t) in ('weapon', 'accessory', 'armor'): AREA_EQUIP[a].add(t)
CRATE_AREAS = {
    'DungeonFishingCrate': ['泪水之城'], 'OceanCrate': ['雾之峡谷'], 'CorruptFishingCrate': ['皇家水道'], 'CrimsonFishingCrate': ['深巢', '野兽巢穴'],
    'HallowedFishingCrate': ['白色宫殿', '水晶山峰'], 'FloatingIslandFishingCrate': ['神居', '呼啸悬崖'], 'JungleFishingCrate': ['苍绿之径', '王后花园'],
    'FrozenCrate': ['安息之地'], 'OasisCrate': ['王国边缘', '愚人斗兽场'], 'LavaCrate': [],
}
CRATE_SPECIAL = {'JungleFishingCrate': 'FiberglassFishingPole', 'OasisCrate': 'ScarabFishingRod', 'CorruptFishingCrate': 'BloodFishingRod',
                 'LavaCrate': 'LavaFishingHook'}
vanilla_primary = {}
for c in J['crates']:
    prim = []
    for e in c.get('loop_primary', []):
        for g in e['give']:
            for i in g['items']:
                t = mod_item(i)
                if t and t not in prim and DD.kind_of(t) in ('weapon', 'accessory', 'armor') if t in DD.MOD_TID else t: prim.append(t)
    for n in c['names']: vanilla_primary[n['internal']] = prim
TIER_WINDOW = {'WoodenCrate': (0, 1, 4), 'IronCrate': (0, 2, 3), 'GoldenCrate': (1, 3, 2),
               'WoodenCrateHard': (2, 4, 4), 'IronCrateHard': (3, 5, 3), 'GoldenCrateHard': (4, 5, 2)}
ALL_EQUIP = sorted({t for s in AREA_EQUIP.values() for t in s})
CRATES = collections.OrderedDict()
for c in J['crates']:
    for n in c['names']:
        t = n['internal']
        hard = t.endswith('Hard')
        base = t[:-4] if hard else t
        if t in TIER_WINDOW:
            lo, hi, den = TIER_WINDOW[t]
            pool = sorted(x for x in ALL_EQUIP if lo <= DD.STAGE[x] <= hi)
            CRATES[t] = dict(kind='tier', level=['WoodenCrate', 'IronCrate', 'GoldenCrate'].index(base), hard=hard, pool=pool, den=den, special=None, lo=lo, hi=hi)
        else:
            pool = sorted({x for a in CRATE_AREAS[base] for x in AREA_EQUIP.get(a, ())} | set(vanilla_primary.get(t, [])) - {CRATE_SPECIAL.get(base)})
            pool = [x for x in pool if x in DD.STAGE or x in NEW]
            CRATES[t] = dict(kind='acid' if base == 'LavaCrate' else 'biome', level=3, hard=hard, pool=pool, den=1, special=CRATE_SPECIAL.get(base), lo=0, hi=5)
CRATES['Oyster'] = dict(kind='oyster', level=0, hard=False, pool=[], den=1, special=None, lo=0, hi=5)


def tlist(names): return [mod_item(DD.TID[n]) if n in DD.TID else None for n in names]


ORES_PRE = [t for t in ('CopperOre', 'TinOre', 'IronOre', 'LeadOre', 'SilverOre', 'TungstenOre', 'GoldOre', 'PlatinumOre') if t in HAVE]
ORES_HM = [t for t in ('CobaltOre', 'PalladiumOre', 'MythrilOre', 'OrichalcumOre', 'AdamantiteOre', 'TitaniumOre') if t in HAVE]
BARS_PRE = [t for t in ('IronBar', 'SilverBar', 'GoldBar', 'LeadBar', 'TungstenBar', 'PlatinumBar') if t in HAVE]
BARS_LOW = [t for t in ('CopperBar', 'TinBar', 'IronBar', 'LeadBar') if t in HAVE]
BARS_HM = [t for t in ('CobaltBar', 'MythrilBar', 'AdamantiteBar', 'PalladiumBar', 'OrichalcumBar', 'TitaniumBar') if t in HAVE]
POTIONS_BIOME = [t for t in ('ObsidianSkinPotion', 'SpelunkerPotion', 'HunterPotion', 'GravitationPotion', 'MiningPotion', 'HeartreachPotion') if t in HAVE]
POTIONS_TIER = [t for t in ('ObsidianSkinPotion', 'SwiftnessPotion', 'IronskinPotion', 'NightOwlPotion', 'ShinePotion', 'HunterPotion', 'GillsPotion',
                            'MiningPotion', 'HeartreachPotion', 'TrapsightPotion') if t in HAVE]

# ---------------------------------------------------------------- C#
def cs(s): return '"' + s.replace('\\', '\\\\').replace('"', '\\"').replace('\n', '\\n') + '"'


def name_of(tid):
    if tid in RENAME: return RENAME[tid][0]
    return zh_name(tid)


def tip_of(t, spec):
    tid = spec['id']
    if spec['kind'] == 'quest':
        return '抓捕位置：' + '、'.join(QUEST_AREAS[t][1])
    if tid in RENAME and RENAME[tid][1] is not None: return RENAME[tid][1]
    v = zh_tip(tid) or ''
    v = v.strip()
    if spec['kind'] == 'mat' and t in RECIPE_MATS and '材料' not in v: v = ('材料\n' + v).strip()
    return v


defs, rows = [], []
for t, spec in NEW.items():
    tid = spec['id']
    k = spec['kind']
    rare = st(tid, 'rare')
    value = max(0, st(tid, 'value'))
    name, tip = name_of(tid), tip_of(t, spec)
    if k == 'pole':
        p = pole_by[tid]
        acid = 'true' if p.get('canFishInLava') else 'false'
        defs.append(f'list.Add(Pole(ItemType.{t}, {cs(name)}, {tid}, {p["fishingPole"]}, {p["shootSpeed"]}f, {p["bobberProjectile"]}, {rare}, {cs(tip)}, {acid}));')
    elif k == 'bait':
        b = bait_by[tid]
        acid = 'true' if tid in ACID_BAITS else 'false'
        mat = 'true' if t in RECIPE_MATS else 'false'
        defs.append(f'list.Add(Bait(ItemType.{t}, {cs(name)}, {tid}, {b["bait"]}, {rare}, {cs(tip)}, {acid}, {mat}));')
    elif k == 'acc':
        bon = []
        if tid in (2373, 3721, 5064): bon.append('FishingLine = true')
        if tid in (2375, 3721, 5064): bon.append('TackleBox = true')
        if tid in (2374, 3721, 5064) or 5139 <= tid <= 5146: bon.append('FishingSkill = 10')
        if tid in (4881, 5064): bon.append('AcidFishing = true')
        if 5139 <= tid <= 5146: bon.append(f'BobberKind = {986 + tid - 5139}')
        defs.append(f'list.Add(Acc(ItemType.{t}, {cs(name)}, {tid}, 0, 0, {rare}, {cs(tip)}, bonus: new Bonuses {{ {", ".join(bon)} }}));')
    elif k == 'quest':
        defs.append(f'list.Add(Quest(ItemType.{t}, {cs(name)}, {tid}, {cs(tip)}));')
    elif k == 'crate':
        defs.append(f'list.Add(Crate(ItemType.{t}, {cs(name)}, {tid}, {rare}, {cs(tip)}));')
    elif k == 'hammer':
        defs.append(f'list.Add(new ItemDef {{ Type = ItemType.{t}, Name = {cs(name)}, Texture = "Item_{tid}", Class = DamageClass.Melee, Damage = {st(tid, "damage")}, '
                    f'Knockback = {st(tid, "knockBack", float)}f, HammerPower = {st(tid, "hammer")}, UseAnimation = {st(tid, "useAnimation")} * Tick, UseTime = {st(tid, "useTime")} * Tick, '
                    f'Rare = {rare}, Tooltip = {cs(tip)} }});')
    elif k == 'bomb':
        defs.append(f'list.Add(new ItemDef {{ Type = ItemType.{t}, Name = {cs(name)}, Texture = "Item_{tid}", UseAnimation = {st(tid, "useAnimation")} * Tick, '
                    f'UseTime = {st(tid, "useTime")} * Tick, NoMelee = true, NoUseGraphic = true, Shoot = ProjKind.Bomb, ShootSpeed = {st(tid, "shootSpeed", float)}f, '
                    f'Consumable = true, MaxStack = 9999, Rare = {rare}, Tooltip = {cs(tip)}, AutoReuse = false }});')
    elif k == 'heal':
        defs.append(f'list.Add(new ItemDef {{ Type = ItemType.{t}, Name = {cs(name)}, Texture = "Item_{tid}", HealLife = {st(tid, "healLife")}, PotionSick = true, Consumable = true, '
                    f'MaxStack = 9999, UseAnimation = 17 * Tick, UseTime = 17 * Tick, UseSound = "Item_2", Rare = {rare}, Tooltip = {cs(tip)} }});')
    else:
        mat = 'true' if t in RECIPE_MATS else 'false'
        defs.append(f'list.Add(Fish(ItemType.{t}, {cs(name)}, {tid}, {rare}, {cs(tip)}, {mat}));')
    src = 'Craft' if t in CRAFTED else 'Drop'
    rows.append(f'yield return (ItemType.{t}, {stage_of(t, spec)}, ItemSource.{src}, {value});')

# Existing items caught: their rows stay; the generated source keeps them as they are.
items_cs = ['// <auto-generated> scratchpad/fishing/gen_fishing.py: Terraria 1.4.5\'s fishing items (fishing.json, Item.SetDefaults via DumpItems). </auto-generated>',
            'using System.Collections.Generic;', '', 'namespace TerraKnight.Items', '{', '    internal static partial class ItemDb', '    {',
            '        static void AddFishing(List<ItemDef> list)', '        {']
items_cs += ['            ' + d for d in defs]
items_cs += ['        }', '', '        static IEnumerable<(ItemType, int, ItemSource, int)> FishingSources()', '        {']
items_cs += ['            ' + r for r in rows]
items_cs += ['        }', '    }', '}', '']


def arr(xs): return 'new[] { ' + ', '.join(f'ItemType.{x}' for x in xs) + ' }' if xs else 'new ItemType[0]'


data = ['// <auto-generated> scratchpad/fishing/gen_fishing.py: Terraria 1.4.5\'s catch rules (GameContentFishDropPopulator), Hallownest\'s waters, the quests, the crates. </auto-generated>',
        'using System.Collections.Generic;', '', 'namespace TerraKnight.Items', '{', '    internal static partial class Fishing', '    {']
data.append('        /// <summary>The catch rules in Terraria\'s order (the remix seed\'s, the Blood Moon\'s and what Hallownest lacks left out).</summary>')
data.append('        static readonly Rule[] rules =')
data.append('        {')
for r in RULES:
    q = f'ItemType.{r["quest"]}' if r['quest'] else 'ItemType.None'
    data.append(f'            new Rule({r["index"]}, {"true" if r["group"] == "LavaDrops" else "false"}, {"true" if r["stopper"] else "false"}, {r["rarity"]}, {r["num"]}, {r["den"]}, 0x{r["mask"]:X}UL, {q}, {arr(r["items"])}),')
data.append('        };')
data.append('')
data.append('        /// <summary>Each area\'s fishing: its stage, Terraria\'s height level and the zones it stands for (Zone flags).</summary>')
data.append('        static readonly Dictionary<string, (int Stage, int Height, Zone Zones)> areas = new Dictionary<string, (int, int, Zone)>')
data.append('        {')
for a, (stage, height, zones) in AREAS.items():
    z = ' | '.join('Zone.' + {'corrupt': 'Corrupt', 'crimson': 'Crimson', 'hallow': 'Hallow', 'hallow50': 'HalfHallow', 'jungle': 'Jungle', 'snow': 'Snow',
                              'desert': 'Desert', 'glowshroom': 'Glowshroom', 'dungeon': 'Dungeon', 'beach': 'Beach', 'ocean': 'Ocean', 'rain': 'Rain'}[x]
                   for x in zones.split()) or 'Zone.None'
    data.append(f'            [{cs(a)}] = ({stage}, {height}, {z}),')
data.append('        };')
data.append('')
data.append('        /// <summary>The Angler\'s quest fish catchable in Hallownest: the stage it first can be, and where (area keys).</summary>')
data.append('        static readonly (ItemType Fish, int Stage, string[] Where)[] quests =')
data.append('        {')
for t, (s, where) in QUEST_AREAS.items():
    data.append(f'            (ItemType.{t}, {s}, new[] {{ {", ".join(cs(a) for a in where)} }}),')
data.append('        };')
data.append('')
data.append('        /// <summary>The crates (and the Oyster): what each holds, shared out among Hallownest\'s areas.</summary>')
data.append('        static readonly Dictionary<ItemType, CrateDef> crates = new Dictionary<ItemType, CrateDef>')
data.append('        {')
for t, c in CRATES.items():
    if t not in NEW and t not in HAVE: continue
    sp = f'ItemType.{c["special"]}' if c['special'] else 'ItemType.None'
    data.append(f'            [ItemType.{t}] = new CrateDef(CrateKind.{c["kind"].capitalize()}, {c["level"]}, {"true" if c["hard"] else "false"}, {c["den"]}, {c["lo"]}, {c["hi"]}, {sp}, {arr(c["pool"])}),')
data.append('        };')
data.append('')
for nm, xs in (('oresPre', ORES_PRE), ('oresHard', ORES_HM), ('barsPre', BARS_PRE), ('barsLow', BARS_LOW), ('barsHard', BARS_HM),
               ('potionsBiome', POTIONS_BIOME), ('potionsTier', POTIONS_TIER)):
    data.append(f'        static readonly ItemType[] {nm} = {arr(xs)};')
data += ['    }', '}', '']

# ---------------------------------------------------------------- the enum
enum_line = '        // Fishing (gen_fishing.py)\n        ' + ', '.join(NEW.keys()) + ','

# ---------------------------------------------------------------- report
print('new items', len(NEW), collections.Counter(v['kind'] for v in NEW.values()))
print('rules', len(RULES), 'quests', len(QUEST_AREAS))
for t, c in CRATES.items(): print(' crate', t, c['kind'], c['level'], 'hard' if c['hard'] else '', 'pool', len(c['pool']), (c['pool'][:6]), c['special'])
print('ores', ORES_PRE, ORES_HM, 'bars', BARS_PRE, BARS_HM)

if WRITE:
    src = read(ITEMS_CS)
    raw = io.open(ITEMS_CS, encoding='utf-8', newline='').read()
    nl = '\r\n' if '\r\n' in raw else '\n'
    old = re.search(r'\n        // Fishing \(gen_fishing\.py\)\r?\n[^\n]*', raw)
    if old: raw = raw.replace(old.group(0), '')
    anchor = 'RelicWhiteDefender, RelicWingedNosk, RelicZote,'
    assert raw.count(anchor) == 1
    raw = raw.replace(anchor, anchor + nl + enum_line.replace('\n', nl))
    io.open(ITEMS_CS, 'w', encoding='utf-8', newline='').write(raw)
    for fn, lines in (('FishingItems.cs', items_cs), ('FishingData.cs', data)):
        io.open(os.path.join(PROJ, 'Items', fn), 'w', encoding='utf-8', newline='').write('\r\n'.join(lines))
    json.dump({'baits': BAIT_AREAS, 'gold': GOLD_BAITS, 'moss': MOSS_AREAS}, io.open(os.path.join(HERE, 'fishing_drops.json'), 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
    print('written')
