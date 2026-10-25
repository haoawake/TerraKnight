"""TerraKnight's fishing (Terraria 1.4.5's, from fishing.json) for Hallownest: the items it lacks (poles, bait, the
accessories, fish, the Angler's 41, junk, crates, moss), the catch rules as Terraria orders them, each area's fishing
conditions, the quest fish catchable here and where, and every crate's contents shared out anew.

Writes (with --write):
  Items/Items.cs           the new ItemType names, after the last
  Items/FishingItems.cs    their ItemDefs and stage/source/value rows
  Items/FishingData.cs     rules, area conditions, quests, crates
  Resources/lang.json      the renamed items' names and tooltips, the new strings (10 languages)
usage: python -I gen_fishing.py [--write]
"""
import io, json, os, re, sys, collections
sys.stdout.reconfigure(encoding='utf-8')
HERE = os.path.dirname(os.path.abspath(__file__))
SCR = os.path.dirname(HERE)
sys.path.insert(0, os.path.join(SCR, 'gen2'))
sys.path.insert(0, os.path.join(SCR, 'port'))
PROJ = os.environ.get('TK_PROJ', r'E:\皓子不困的妙妙工具\github项目管理\projects\强兼\src\TerraKnight')
WRITE = '--write' in sys.argv
TS = os.path.join(SCR, 'terraria-src')


def read(p): return io.open(p, encoding='utf-8-sig').read()


J = json.loads(read(os.path.join(HERE, 'fishing.json')))
import dropdata as DD   # TID / TNAME / kind_of

# ---------------------------------------------------------------- Terraria's text
def loc(culture):
    out = {}
    for part in ('', '.Items', '.Game', '.Town', '.Legacy', '.NPCs'):
        p = os.path.join(TS, f'Terraria.Localization.Content.{culture}{part}.json')
        if os.path.exists(p):
            for k, v in json.loads(re.sub(r',(\s*[}\]])', r'\1', read(p))).items():
                out.setdefault(k, {}).update(v)
    return out


ZH, EN = loc('zh-Hans'), loc('en-US')


def resolve(text, table):
    if text is None: return None
    def rep(m):
        a, b = m.group(1).split('.', 1)
        return resolve(table.get(a, {}).get(b, m.group(0)), table)
    return re.sub(r'\{\$([\w.]+)\}', rep, text)


def zh_name(tid): return ZH['ItemName'].get(DD.TNAME[tid])
def zh_tip(tid): return resolve(ZH['ItemTooltip'].get(DD.TNAME[tid]), ZH)
def en_name(tid): return EN['ItemName'].get(DD.TNAME[tid])


# ---------------------------------------------------------------- Terraria's stats (DumpItems.exe)
STATS = {}
with io.open(os.path.join(HERE, 'itemstats.tsv'), encoding='utf-8') as f:
    head = f.readline().rstrip('\n').split('\t')
    for line in f:
        p = line.rstrip('\n').split('\t')
        if len(p) == len(head): STATS[int(p[0])] = dict(zip(head, p))


def st(tid, k, cast=int):
    v = STATS[tid][k]
    if cast is bool: return v == 'True'
    return cast(float(v)) if cast is int else cast(v)


# ---------------------------------------------------------------- the mod as it is
ITEMS_CS = os.path.join(PROJ, 'Items', 'Items.cs')
_enum_body = re.search(r'internal enum ItemType\s*\{(.*?)\n    \}', read(ITEMS_CS), re.S).group(1)
_fish_tail = re.search(r'\n\s*// Fishing \(gen_fishing\.py\)[^\n]*\n[^\n]*', _enum_body)
ENUM = [x.split('=')[0].strip() for x in re.sub(r'//[^\n]*', '', _enum_body if not _fish_tail else _enum_body.replace(_fish_tail.group(0), '')).replace('\n', ' ').split(',') if x.strip()]
HAVE = set(ENUM)
RECIPE_MATS = set()
for fn in os.listdir(os.path.join(PROJ, 'Items')):
    if fn.startswith('Recipe') and fn.endswith('.cs'):
        for m in re.finditer(r'\(ItemType\.(\w+), \d+\)', read(os.path.join(PROJ, 'Items', fn))): RECIPE_MATS.add(m.group(1))

# ---------------------------------------------------------------- what is added
SKIP = {'TruffleWorm', 'ZephyrFish', 'ScalyTruffle', 'CombatBook', 'DemonConch', 'BottomlessLavaBucket', 'LavaAbsorbantSponge', 'DreadoftheRedSea',
        'LadyOfTheLake', 'DeadCellsBarnacleSummonItem', 'ThisIsCanonNow', 'PaintingItsScragglinTime'}
ACID_BAITS = {4845, 4847, 4849}
# Renamed for Hallownest (lava is acid here, the biome crates its areas): zh name, zh tooltip (None: Terraria's), en name.
RENAME = {
    4881: ('酸水钩', '可在酸水中钓鱼', 'Acidproof Fishing Hook', 'Allows fishing in acid'),
    5064: ('酸水钓具包', '钓鱼线永远不会断，降低鱼饵消耗几率，渔力增加10\n可在酸水中钓鱼', 'Acidproof Tackle Bag',
           'Fishing line will never break, decreases chance of bait consumption, increases fishing power by 10\nAllows fishing in acid'),
    2422: ('酸线钓钩', '可在酸水中钓鱼', 'Acidline Fishing Hook', 'Allows fishing in acid'),
    4845: ('酸液蝴蝶', '可在酸水中钓鱼', 'Acid Butterfly', 'Allows fishing in acid'),
    4847: ('酸液萤火虫', '可在酸水中钓鱼', 'Acidfly', 'Allows fishing in acid'),
    4849: ('酸液蜗牛', '可在酸水中钓鱼', 'Acid Snail', 'Allows fishing in acid'),
    4354: ('酸液苔藓', '材料', 'Acid Moss', 'Material'),
    5141: ('酸液苔藓钓鱼浮标', None, 'Acid Moss Fishing Bobber', None),
    4325: ('鱼饵投掷者', '', None, None),
    3205: ('泪城匣', '右键单击打开', 'City Crate', None), 3984: ('泪城秘匣', '右键单击打开', 'City Coffer', None),
    5002: ('雾谷匣', '右键单击打开', 'Fog Canyon Crate', None), 5003: ('雾谷秘匣', '右键单击打开', 'Fog Canyon Coffer', None),
    3203: ('水道匣', '右键单击打开', 'Waterways Crate', None), 3982: ('水道秘匣', '右键单击打开', 'Waterways Coffer', None),
    3204: ('深巢匣', '右键单击打开', 'Deepnest Crate', None), 3983: ('深巢秘匣', '右键单击打开', 'Deepnest Coffer', None),
    3207: ('白宫匣', '右键单击打开', 'White Palace Crate', None), 3986: ('白宫秘匣', '右键单击打开', 'White Palace Coffer', None),
    3206: ('神居匣', '右键单击打开', 'Godhome Crate', None), 3985: ('神居秘匣', '右键单击打开', 'Godhome Coffer', None),
    3208: ('苍绿匣', '右键单击打开', 'Greenpath Crate', None), 3987: ('苍绿秘匣', '右键单击打开', 'Greenpath Coffer', None),
    4405: ('安息匣', '右键单击打开', 'Resting Grounds Crate', None), 4406: ('安息秘匣', '右键单击打开', 'Resting Grounds Coffer', None),
    4407: ('边缘匣', '右键单击打开', "Kingdom's Edge Crate", None), 4408: ('边缘秘匣', '右键单击打开', "Kingdom's Edge Coffer", None),
    4877: ('酸蚀匣', '右键单击打开', 'Acid Crate', None), 4878: ('酸蚀秘匣', '右键单击打开', 'Acid Coffer', None),
}
MOSS = [4354, 4377, 4378, 4389, 5127, 5128]

pole_by = {p['id']: p for p in J['poles']}
bait_by = {b['id']: b for b in J['baits']}
acc_ids = [a['id'] for a in J['accessories'] if a['id'] not in (3120, 4404)]
crate_ids = [n['id'] for c in J['crates'] for n in c['names']]
quest_ids = [q['id'] for q in J['angler']['quests']]
catch_ids = sorted({i for r in J['catch_rules'] for i in r['items']})

NEW = collections.OrderedDict()   # internal -> spec


def add(tid, kind, **kw):
    t = DD.TNAME[tid]
    if t in SKIP: return
    if t in HAVE and kind not in ('existing',): return
    NEW.setdefault(t, dict(id=tid, kind=kind, **kw))


for tid in pole_by: add(tid, 'pole')
for tid in bait_by:
    if tid not in (2436, 2437, 2438): add(tid, 'bait')
for tid in acc_ids: add(tid, 'acc')
for tid in (2436, 2437, 2438): add(tid, 'bait')
for tid in quest_ids: add(tid, 'quest')
for tid in crate_ids: add(tid, 'crate')
for tid in catch_ids:
    t = DD.TNAME.get(tid)
    if t is None or t in NEW or t in HAVE: continue
    if tid == 2320: add(tid, 'hammer')
    elif tid == 3196: add(tid, 'bomb')
    elif tid == 4410: add(tid, 'crate')
    elif st(tid, 'healLife') > 0: add(tid, 'heal')
    else: add(tid, 'mat')
for tid in MOSS: add(tid, 'mat')
add(4411, 'mat')   # the Shucked Oyster

# ---------------------------------------------------------------- Hallownest's waters as Terraria's
def area_name(scene):
    """Areas.AreaName."""
    S = lambda *p: any(scene.startswith(x) for x in p)
    if S('GG_Pipeway', 'GG_Waterways', 'Room_GG_Shortcut', 'Waterways'): return '皇家水道'
    if S('GG_Lurker', 'Deepnest_East', 'Room_Wyrm'): return '王国边缘'
    if S('Room_Colosseum'): return '愚人斗兽场'
    if S('Deepnest_Spider_Town', 'Room_spider'): return '野兽巢穴'
    if S('Deepnest', 'Room_Mask_Maker'): return '深巢'
    if S('Hive'): return '蜂巢'
    if S('Abyss_17', 'Abyss_18', 'Abyss_19'): return '远古盆地'
    if S('Abyss_06', 'Abyss_08', 'Abyss_09', 'Abyss_1', 'Abyss_Lighthouse'): return '深渊'
    if S('Abyss'): return '远古盆地'
    if S('White_Palace'): return '白色宫殿'
    if S('Room_temple', 'Room_Final'): return '黑卵神殿'
    if S('GG_'): return '神居'
    if S('Dream_'): return '梦境'
    if S('Grimm_'): return '格林剧团'
    if S('Mines'): return '水晶山峰'
    if S('RestingGrounds', 'Room_Mansion'): return '安息之地'
    if S('Ruins'): return '泪水之城'
    if S('Fungus2', 'Room_Fungus_Shaman'): return '真菌荒地'
    if S('Fungus3'):
        fog = S('Fungus3_01', 'Fungus3_02', 'Fungus3_03', 'Fungus3_24', 'Fungus3_25', 'Fungus3_26', 'Fungus3_27', 'Fungus3_28', 'Fungus3_30',
                'Fungus3_35', 'Fungus3_44', 'Fungus3_47', 'Fungus3_archive')
        return '雾之峡谷' if fog else '王后花园'
    if S('Room_Queen'): return '王后花园'
    if S('Fungus1', 'Room_nailmaster_02', 'Room_Slug_Shrine'): return '苍绿之径'
    if S('Cliffs', 'Room_nailmaster'): return '呼啸悬崖'
    if S('Tutorial'): return '国王山道'
    if S('Town', 'Room_Town', 'Room_shop', 'Room_mapper', 'Room_Bretta', 'Room_Ouiji', 'Room_Sly'): return '德特茅斯'
    if S('Crossroads', 'Room_Mender_House', 'Room_Charm_Shop', 'Room_ruinhouse'): return '遗忘十字路'
    return '圣巢'


# Each area: its stage (Areas.LevelOf), Terraria's height level (0 space .. 4 underworld) and the zones the player stands
# in: corrupt crimson hallow hallow50 (Godhome: half its checks) jungle snow desert glowshroom dungeon beach; ocean (its big
# pools are the sea's); rain (the City of Tears').
AREAS = collections.OrderedDict([
    ('国王山道', (0, 1, '')), ('德特茅斯', (0, 1, '')), ('遗忘十字路', (0, 1, '')),
    ('苍绿之径', (0, 1, 'jungle')),
    ('真菌荒地', (1, 2, 'glowshroom')),
    ('雾之峡谷', (1, 1, 'beach ocean')),
    ('呼啸悬崖', (1, 0, 'snow')),
    ('泪水之城', (2, 2, 'dungeon rain')),
    ('皇家水道', (2, 2, 'corrupt')),
    ('水晶山峰', (2, 3, 'hallow')),
    ('安息之地', (2, 1, 'snow')),
    ('深巢', (3, 3, 'crimson')), ('野兽巢穴', (3, 3, 'crimson')),
    ('远古盆地', (3, 3, '')),
    ('王后花园', (3, 3, 'jungle')),
    ('王国边缘', (4, 2, 'desert')), ('愚人斗兽场', (4, 2, 'desert')),
    ('蜂巢', (4, 3, 'jungle')),
    ('深渊', (4, 4, '')),
    ('白色宫殿', (5, 1, 'hallow')), ('神居', (5, 0, 'hallow50')),
    ('黑卵神殿', (0, 2, '')), ('梦境', (5, 0, '')), ('格林剧团', (5, 1, '')),
])

# The pools found (liquidscan.txt: scene, kind, x, y, WxH cells): their areas, acid or water, the biggest.
POOLS = collections.defaultdict(lambda: {'water': 0, 'acid': 0})
for line in io.open(os.path.join(HERE, 'liquidscan.txt'), encoding='utf-8'):
    p = line.rstrip('\n').split('\t')
    if len(p) < 5: continue
    w, h = (int(x) for x in p[4].split('x'))
    kind = 'acid' if p[1] == 'acid' else 'water'
    # A swim region is the top of its water: four cells; the water goes on down (taken as twice as deep).
    tiles = w * h if kind == 'acid' else w * max(h, 8)
    a = area_name(p[0])
    POOLS[a][kind] = max(POOLS[a][kind], tiles)

# ---------------------------------------------------------------- the rules
RAR = {'Any': 0, 'Common': 1, 'Uncommon': 2, 'Rare': 3, 'VeryRare': 4, 'Legendary': 5, 'BombRarityOfNotLegendaryAndNotVeryRareAndUncommon': 6, 'UncommonOrCommon': 7}
CONDS = ['HardMode', 'EarlyMode', 'InLava', 'InHoney', 'Junk', 'Crate', 'AnyEnemies', 'CanFishInLava', 'Dungeon', 'Beach', 'Hallow', 'GlowingMushrooms',
         'TrueDesert', 'TrueSnow', 'Remix', 'Height0', 'Height1', 'Height1And2', 'HeightAbove1', 'HeightAboveAnd1', 'HeightUnder2', 'HeightAbove2', 'Height3',
         'Corruption', 'Crimson', 'Jungle', 'Snow', 'Desert', 'RolledHallowDesert', 'Ocean', 'UnderRockLayer', 'BloodMoon', 'DidNotUseCombatBook', 'Water1000']
BIT = {c: i for i, c in enumerate(CONDS)}


def mod_item(tid):
    t = DD.TNAME.get(tid)
    if t is None: return None
    return t if (t in HAVE or t in NEW) and t not in SKIP else None


RULES = []
for r in J['catch_rules']:
    names = [c['name'] for c in r['conditions']]
    if 'Remix' in names or 'QuestFishRemix' in names or 'AnyEnemies' in names or 'BloodMoon' in names: continue
    quest = None
    mask = 0
    ok = True
    for c in r['conditions']:
        if c['name'] == 'QuestFish': quest = c['item']
        elif c['name'] in BIT: mask |= 1 << BIT[c['name']]
        else: raise SystemExit('condition ' + c['name'])
    items = []
    if r['kind'] != 'stopper':
        items = [mod_item(i) for i in r['items']]
        items = [i for i in items if i]
        if not items: continue   # nothing of it here: the next rules decide
    if quest is not None and not mod_item(quest): continue
    RULES.append(dict(index=r['index'], group=r['group'], stopper=r['kind'] == 'stopper', rarity=RAR[r['rarity']], num=r['chance'][0], den=r['chance'][1],
                      mask=mask, quest=DD.TNAME[quest] if quest else None, items=items))
LAVA_STOP = max(i for i, r in enumerate(RULES) if r['stopper'] and r['group'] == 'LavaDrops')

# ---------------------------------------------------------------- what each area's water can give (for stages, hints and quests)
def contexts():
    """Every condition set an area's pools give: (area, stage, acid, flags, quest-capable)."""
    for a, (stage, height, zones) in AREAS.items():
        pools = POOLS.get(a)
        if not pools: continue
        zs = set(zones.split())
        for liquid in ('water', 'acid'):
            tiles = pools[liquid]
            if tiles <= 0: continue
            for hard in (False, True):
                for hallow_roll in ((True, False) if 'hallow50' in zs else (('hallow' in zs),)):
                    f = set()
                    f.add('HardMode' if hard else 'EarlyMode')
                    if liquid == 'acid': f |= {'InLava', 'CanFishInLava'}
                    if 'dungeon' in zs: f.add('Dungeon')
                    if 'beach' in zs: f.add('Beach')
                    if hallow_roll: f.add('Hallow')
                    if 'glowshroom' in zs: f.add('GlowingMushrooms')
                    if 'desert' in zs: f |= {'TrueDesert', 'Desert'}
                    if 'snow' in zs: f |= {'TrueSnow', 'Snow'}
                    if 'corrupt' in zs: f.add('Corruption')
                    if 'crimson' in zs: f.add('Crimson')
                    if 'jungle' in zs: f.add('Jungle')
                    if 'desert' in zs and hallow_roll: f.add('RolledHallowDesert')
                    f.add(f'Height{height}')
                    if height in (1, 2): f.add('Height1And2')
                    if height > 1: f.add('HeightAbove1')
                    if height >= 1: f.add('HeightAboveAnd1')
                    if height < 2: f.add('HeightUnder2')
                    if height > 2: f.add('HeightAbove2')
                    if 'ocean' in zs and height <= 1 and tiles > 1000: f.add('Ocean')
                    if tiles > 1000: f.add('Water1000')
                    f.add('DidNotUseCombatBook')
                    # Acid wants the acid gear (the acid bait drops from the Waterways on): stage 2 at the earliest.
                    yield a, max(stage, 4 if hard else 0, 2 if liquid == 'acid' else 0), liquid, f


def reachable(flags, quest=None):
    """The items some roll of the dice gives under these conditions (the rarity flags, the junk and crate rolls all ways)."""
    out = set()
    for combo in range(1 << 7):
        rar = {i + 1 for i in range(5) if combo >> i & 1}
        junk, crate = bool(combo & 32), bool(combo & 64)
        f = set(flags)
        if junk: f.add('Junk')
        if crate: f.add('Crate')
        acid = 'InLava' in f and 'CanFishInLava' in f
        i = 0
        while i < len(RULES):
            r = RULES[i]
            # Acid (with the gear): the area's water rules, Terraria's lava catches first, the sure ones at half (Fishing.cs).
            if acid and i == LAVA_STOP:
                f.discard('InLava')
                i += 1
                continue
            need = {c for c in CONDS if r['mask'] >> BIT[c] & 1}
            if not need <= f or (r['quest'] and r['quest'] != quest):
                i += 1
                continue
            ro = r['rarity']
            hit = ro == 0 or (ro in rar if ro <= 5 else (2 in rar and 4 not in rar and 5 not in rar) if ro == 6 else (1 in rar or 2 in rar))
            if not hit:
                i += 1
                continue
            if r['stopper']: break
            out.update(r['items'])
            if r['num'] >= r['den'] and not (acid and i < LAVA_STOP): break
            i += 1
    return out


CATCH_STAGE = {}
CATCH_AREAS = collections.defaultdict(set)
for a, stage, liquid, f in contexts():
    for t in reachable(f):
        CATCH_STAGE[t] = min(CATCH_STAGE.get(t, 9), stage)
        CATCH_AREAS[t].add(a)
QUEST_AREAS = {}
for q in quest_ids:
    t = DD.TNAME[q]
    if t in SKIP: continue
    where = set()
    best = 9
    for a, stage, liquid, f in contexts():
        if t in reachable(f, quest=t):
            where.add(a)
            best = min(best, stage)
    if where: QUEST_AREAS[t] = (best, [a for a in AREAS if a in where])

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
