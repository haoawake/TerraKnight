"""Build the offline Chinese encyclopedia from this checkout, without running the game.

Only reads production sources and README. No game assets, saves or network needed.
Run: python tools/generate_encyclopedia.py
"""
from __future__ import annotations

import collections
import json
from pathlib import Path
import re
import hashlib
import math
import struct
import os
import posixpath

ROOT = Path(os.environ.get('TK_WIKI_SOURCE_ROOT', Path(__file__).resolve().parents[1])).resolve()
SRC = ROOT / 'src/TerraKnight'
OUT = ROOT / '泰拉圣巢本地百科全书.md'
STR = r'"(?:\\.|[^"\\])*"'


def read(rel):
    return (SRC / rel).read_text(encoding='utf-8-sig')


def clean(s):
    # Strip comments without damaging quoted strings (including URLs).
    return re.sub(STR + r'|//[^\n]*|/\*[\s\S]*?\*/',
                  lambda m: m[0] if m[0].startswith('"') else ' ', s)


def split(s):
    """Split C# arguments/initializers, retaining nested objects and strings."""
    out, start, depth, quoted, esc = [], 0, 0, False, False
    for i, c in enumerate(s):
        if quoted:
            if esc:
                esc = False
            elif c == '\\':
                esc = True
            elif c == '"':
                quoted = False
        elif c == '"':
            quoted = True
        elif c in '({[':
            depth += 1
        elif c in ')}]':
            depth -= 1
        elif c == ',' and depth == 0:
            out.append(s[start:i].strip())
            start = i + 1
    out.append(s[start:].strip())
    return [x for x in out if x]


def balanced(s, pos):
    opening = s[pos]
    closing = {'(': ')', '{': '}'}[opening]
    depth, quoted, esc = 0, False, False
    for i in range(pos, len(s)):
        c = s[i]
        if quoted:
            if esc:
                esc = False
            elif c == '\\':
                esc = True
            elif c == '"':
                quoted = False
        elif c == '"':
            quoted = True
        elif c == opening:
            depth += 1
        elif c == closing:
            depth -= 1
            if depth == 0:
                return s[pos+1:i], i+1
    raise ValueError('Unbalanced source')


def string(s):
    try:
        value = json.loads(s)
        return value if isinstance(value, str) else None
    except (ValueError, TypeError):
        return None


def text_expr(s, constants):
    if not s or s == 'null':
        return ''
    # Literal concatenation and local constant aliases only; never eval C#.
    pieces = re.split(r'\s+\+\s+', s)
    result = []
    for p in pieces:
        val = string(p.strip())
        if val is None:
            val = constants.get(p.strip())
        if val is None:
            return ''
        result.append(val)
    return ''.join(result)


def esc(s):
    return str(s).replace('|', '&#124;').replace('\r', '').replace('\n', '<br>')


def fields(s):
    out = {}
    for a in split(s):
        m = re.match(r'(\w+)\s*=\s*([\s\S]*)', a)
        if m:
            out[m[1]] = m[2].strip()
    return out


def money(value):
    price = value if value <= 100 else round(100 * (value / 100) ** .8)
    return f'{price / 100:g} 吉欧'


def shop_money(item):
    """Prices are calculated by the actual game purchase function, not resale compression."""
    if not hasattr(shop_money, 'prices'):
        shop_money.prices = json.loads((ROOT/'tools/encyclopedia_details.json').read_text(encoding='utf-8'))['prices']
    return f'{shop_money.prices[item] / 100:g} 吉欧'


def expand_tuple_loops(s):
    """Expand literal foreach tables just as the item builders do."""
    pattern = r'foreach\s*\(var\s*\(([^)]+)\)\s+in\s+new\[\]\s*\{'
    while True:
        match = re.search(pattern, s)
        if not match:
            return s
        rows, end = balanced(s, match.end()-1)
        names = [n.strip() for n in match[1].split(',')]
        pos = end
        while s[pos].isspace() or s[pos] == ')':
            pos += 1
        if s[pos] == '{':
            body, finish = balanced(s, pos)
        else:
            finish = s.index(';', pos)+1
            body = s[pos:finish]
        expanded = []
        for row in split(rows):
            if not row.startswith('('):
                raise ValueError('Unexpected foreach row: '+row)
            values = split(row[1:-1])
            assert len(names)==len(values)
            mapping = dict(zip(names, values))
            expanded.append(re.sub(STR+r'|\b\w+\b', lambda m: mapping.get(m[0],m[0]), body))
        s = s[:match.start()] + '\n'.join(expanded) + s[finish:]


def build(assets_only=False):
    files = sorted((SRC / 'Items').glob('Items*.cs'))
    files += [SRC / ('Items/' + name) for name in ('NailItem.cs', 'DreamNailItem.cs',
              'BossPotions.cs', 'RelicsGenerated.cs', 'FishingItems.cs', 'PetsData.cs', 'HkItems.cs')]
    contents = {p: expand_tuple_loops(clean(p.read_text(encoding='utf-8-sig'))) for p in files}
    enum = balanced(contents[SRC / 'Items/Items.cs'], contents[SRC / 'Items/Items.cs'].index('{', contents[SRC / 'Items/Items.cs'].index('enum ItemType')))[0]
    types = [x.strip().split('=')[0].strip() for x in split(enum)]
    types.remove('None')
    items = {t: {'name': t, 'stage': 0, 'source': 'Basic', 'value': 0, 'tip': '', 'kind': '其他', 'stats': {}, 'file': '', 'set': '', 'setbonus': ''} for t in types}
    signatures = collections.defaultdict(list)
    for p, s in contents.items():
        for m in re.finditer(r'\bItemDef\s+(\w+)\s*\(', s):
            args, end = balanced(s, m.end()-1)
            params = []
            for a in split(args):
                q = a.split('=', 1)
                name = re.search(r'(\w+)\s*$', q[0])
                if name:
                    params.append((name[1], q[1].strip() if len(q) == 2 else None))
            tail = s[end:]
            stop = tail.find(';')
            helper_body = tail[:stop] if stop >= 0 else tail
            signatures[m[1]].append((p, m.start(), params, helper_body))

    for p, s in contents.items():
        constants = {}
        # Common multi-declaration const strings: fly, dev, hover, etc.
        for m in re.finditer(r'const string\s+([^;]+);', s):
            for a in split(m[1]):
                q = a.split('=', 1)
                if len(q) == 2:
                    constants[q[0].strip()] = text_expr(q[1].strip(), constants)
        for m in re.finditer(r'\b(\w+)\s*\(\s*ItemType\.(\w+)\s*,\s*(' + STR + ')', s):
            method, t = m[1], m[2]
            if t not in items:
                continue
            args, _ = balanced(s, s.index('(', m.start()))
            args = split(args)
            candidates = signatures.get(method, [])
            local = [x for x in candidates if x[0] == p]
            previous = [x for x in local if x[1] < m.start()]
            sig = max(previous,key=lambda x:x[1]) if previous else local[0] if local else candidates[0] if candidates else None
            params = sig[2] if sig else []
            helper_body = sig[3] if sig else ''
            bind = {k: v for k, v in params if v is not None}
            positional = 0
            for arg in args:
                named = re.match(r'^(\w+)\s*:\s*([\s\S]+)', arg)
                if named:
                    bind[named[1]] = named[2]
                elif positional < len(params):
                    bind[params[positional][0]] = arg
                    positional += 1
            d = items[t]
            d.update(name=string(m[3]), file=p.relative_to(ROOT).as_posix())
            if bind.get('id', '').isdigit():
                d['texture'] = 'Item_' + bind['id']
            if method == 'Station':
                d.update(stage=int(bind['stage']), source=bind['source'].split('.')[-1], value=int(bind['value']))
            d['tip'] = text_expr(bind.get('tip') or bind.get('flavour'), constants)
            if method == 'Wing':
                d['tip'] = d['tip'] or '可飞行和缓慢坠落；飞行表现按翅膀种类决定。'
            if method == 'H':
                d['tip'] = (d['tip'] + '\n按 E 朝光标抛出').strip()
            cls = bind.get('cls') or bind.get('kind') or bind.get('cl') or ''
            if method in ('Acc', 'Acc2', 'Wing'):
                kind = '饰品'
            elif method == 'Armor':
                kind = '盔甲'
            elif method == 'Mat':
                kind = '材料'
            elif method == 'Station':
                kind = '家具与工作站'
            elif method == 'H':
                kind = '钩爪'
            elif method in ('B',):
                kind = '永久增益'
            elif method in ('P', 'P2', 'Potion'):
                kind = '药水与消耗品'
            elif method == 'Ammo':
                kind = '弹药'
            elif method in ('Gun', 'Bow') or 'Ranged' in cls:
                kind = '远程武器'
            elif method in ('Staff2', 'SentryStaff', 'Whip') or 'Summon' in cls:
                kind = '召唤武器'
            elif method in ('Staff',) or 'Magic' in cls:
                kind = '魔法武器'
            elif method in ('Pick', 'Axe', 'Hammer', 'Tool', 'Drill', 'Chainsaw', 'Hamaxe', 'Saw'):
                kind = '工具'
            else:
                kind = '近战武器' if method in ('Sword', 'Spear', 'Thrown', 'Yoyo', 'Flail') or 'Melee' in cls else '其他'
            if kind == '其他' or method in ('B','H'):
                if 'DamageClass.Magic' in helper_body or 'Staff(' in helper_body:
                    kind = '魔法武器'
                elif 'DamageClass.Summon' in helper_body or 'Staff2(' in helper_body:
                    kind = '召唤武器'
                elif 'DamageClass.Ranged' in helper_body or 'Gun(' in helper_body or 'Bow(' in helper_body:
                    kind = '远程武器'
                elif 'DamageClass.Melee' in helper_body or 'Sword(' in helper_body:
                    kind = '近战武器'
            if p.name == 'ItemsGuns.cs':
                kind = '远程武器'
            if p.name == 'ItemsR7.cs' and method == 'B':
                kind = '永久增益'
            if 'Armor(' in helper_body:
                kind = '盔甲'
            if p.name != 'ItemsR7.cs' and method == 'H':
                d['tip'] = text_expr(bind.get('tip'),constants)
            d['kind'] = kind
            for key, label in [('dmg','伤害'), ('defense','防御'), ('mana','耗魔'), ('power','工具力'), ('pick','镐力'), ('axe','斧力参数'), ('hammer','锤力'), ('crit','基础暴击加成'), ('life','恢复生命'), ('kb','击退'), ('speed','弹速参数')]:
                v = bind.get(key)
                if v and re.fullmatch(r'-?[\d.]+f?', v) and float(v.rstrip('f')) != 0:
                    d['stats'][label] = v.rstrip('f')
            if method == 'Armor' and len(args) > 5:
                d['stats']['防御'] = args[5]
            if method in ('P2','Potion') and bind.get('mana', '0') != '0':
                d['stats'].pop('耗魔', None)
                d['stats']['恢复魔力'] = bind['mana']
            ticks = bind.get('ticks','0')
            if ticks.isdigit() and int(ticks)>0:
                d['stats']['增益持续'] = f'{int(ticks)/60:g} 秒'
            if bind.get('potion')=='true':
                d['stats']['药水病'] = '会触发'
            if kind == '盔甲':
                d['set'] = bind.get('set','').removeprefix('ArmorSet.')
                d['setbonus'] = bind.get('setBonus',bind.get('set','')).removeprefix('ArmorSet.')
                if d['setbonus']=='None':
                    d['setbonus']=d['set']
                slot = bind.get('slot','').split('.')[-1]
                d['stats']['部位'] = {'Head':'头部','Body':'身体','Legs':'腿部'}.get(slot,slot)

        # Explicit ItemDef initializers, including the starting accessories and tools.
        for m in re.finditer(r'new ItemDef\s*\{', s):
            obj, _ = balanced(s, m.end()-1)
            f = fields(obj)
            t = f.get('Type', '').removeprefix('ItemType.')
            name = string(f.get('Name', ''))
            if t not in items or name is None:
                continue
            d = items[t]
            d.update(name=name, file=p.relative_to(ROOT).as_posix(), tip=text_expr(f.get('Tooltip'), constants))
            if 'Texture' in f:
                texture = text_expr(f['Texture'], constants)
                if texture:
                    d['texture'] = texture
            for key, label in [('Damage','伤害'),('Defense','防御'),('Mana','耗魔'),('PickPower','镐力'),('HealLife','恢复生命'),('HealMana','恢复魔力')]:
                if re.fullmatch(r'\d+', f.get(key, '')):
                    d['stats'][label] = f[key]
            if 'Accessory' in f:
                d['kind'] = '饰品'
            elif 'Places' in f:
                d['kind'] = '建材'
            elif 'PlacesStation' in f:
                d['kind'] = '家具与工作站'
            elif 'PickPower' in f:
                d['kind'] = '工具'
            elif 'ArmorSlot' in f:
                d['kind'] = '盔甲'
            elif 'Class' in f:
                d['kind'] = {'Melee':'近战武器','Ranged':'远程武器','Magic':'魔法武器','Summon':'召唤武器'}.get(f['Class'].split('.')[-1], '其他')
            elif 'HealLife' in f or 'HealMana' in f or 'BuffType' in f:
                d['kind'] = '药水与消耗品'
            if 'Stage' in f and f['Stage'].isdigit():
                d['stage'] = int(f['Stage'])
            if 'Source' in f:
                d['source'] = f['Source'].split('.')[-1]

    # Source/value overrides apply in exactly the same order as ItemDb.Build.
    for filename in ['ItemsGenerated.cs', 'ItemsOres.cs', 'ItemsMoney.cs', 'ItemsCraftChain.cs', 'ItemsInfo.cs', 'ItemsStations2.cs', 'ItemsArmor2.cs', 'BossPotions.cs', 'RelicsGenerated.cs', 'FishingItems.cs', 'PetsData.cs']:
        s = clean(read('Items/' + filename))
        for t, stage, source, value in re.findall(r'\(ItemType\.(\w+),\s*(\d+),\s*ItemSource\.(\w+),\s*(\d+)\)', s):
            items[t].update(stage=int(stage), source=source, value=int(value))
    items['Chain'].update(stage=0, source='Craft', value=150)
    for t in ('WoodHelmet','WoodBreastplate','WoodGreaves'):
        items[t].update(stage=0,source='Craft')

    seals = []
    seal_source = clean(read('Items/BossSummons.cs'))
    for m in re.finditer(r'\bS\(ItemType\.', seal_source):
        a, _ = balanced(seal_source, seal_source.index('(', m.start()))
        a = split(a)
        t, key = a[0].split('.')[-1], string(a[1])
        seal = dict(result=t, count=1, station='砧', stage=int(a[4]), ingredients=[(a[5].split('.')[-1],int(a[6])),(a[8].split('.')[-1],int(a[9]))])
        seal.update(key=key, dream=len(a)>12 and a[12]=='true', locality=string(a[11]), material_sources=[string(a[7]),string(a[10])])
        seals.append(seal)
        items[t].update(name=string(a[2]), stage=int(a[4]), source='Craft', kind='Boss召唤物', texture='HK_BossSeal_'+key, file='src/TerraKnight/Items/BossSummons.cs', tip='首次击败对应首领（记录 '+key+'）后，可在普通世界使用；梦境与神居中不能使用；进入完整原版竞技场重战，成功开战消耗一个，条件不符不消耗；梦境/神居战后返回出发处，现世战后留在竞技场。'+('需要梦之钉。' if seal['dream'] else '')+'材料来源：'+seal['locality']+'。')

    # ItemDb applies factories and overrides at runtime. Use its verified final
    # metadata, as we already do for expanded recipes and post-constructor drops.
    runtime = json.loads((ROOT/'tools/encyclopedia_runtime.json').read_text(encoding='utf-8'))
    for rel, digest in runtime['source_hashes'].items():
        assert hashlib.sha256((SRC/rel).read_bytes()).hexdigest() == digest, f'Refresh isolated wikiassets export: {rel}'
    assert set(items) == set(runtime['data']['items']), 'Runtime item coverage mismatch'
    relics_enabled = not re.search(r'\bEnabled\s*=\s*false', clean(read('Items/Relics.cs')))
    for t, d in items.items():
        rd = runtime['data']['items'][t]
        assert 'Name' in rd, 'Refresh wikiassets with complete item metadata'
        d.update(name=rd['Name'] if rd['Name'] != t else d['name'], stage=rd['Stage'], source=rd['Source'], texture=rd['Texture'], value=rd['Value'])
        if rd.get('Tooltip'):
            d['tip'] = rd['Tooltip']
        if t.startswith('Relic'):
            d['kind'] = '大师圣物' if relics_enabled else '未启用的大师圣物'
            if not relics_enabled:
                d['tip'] = '当前构建已关闭大师圣物。此页仅保留定义记录，不会从首领掉落，也不能在普通流程中取得。'
        elif t.startswith('Hk'):
            d['kind'] = '原版关键物品'
        elif rd.get('PlacesWall', 0) > 0:
            d['kind'] = '背景墙'
        elif d['kind'] == '其他':
            if rd.get('Accessory', 'None') != 'None':
                d['kind'] = '饰品'
            elif rd.get('PlacesStation', 'None') != 'None':
                d['kind'] = '家具与工作站'
            elif rd.get('Damage', 0) > 0:
                d['kind'] = {'Melee':'近战武器','Ranged':'远程武器','Magic':'魔法武器','Summon':'召唤武器'}.get(rd['Class'], '其他')
        for field, label in [('Damage','伤害'),('Defense','防御'),('Mana','耗魔'),('PickPower','镐力'),('HealLife','恢复生命'),('HealMana','恢复魔力')]:
            d['stats'].pop(label, None)
            if rd.get(field, 0) or (field == 'Defense' and d['kind'] == '盔甲'):
                d['stats'][label] = str(rd[field])

    ores = []
    for m in re.finditer(r'new OreInfo\(', clean(read('Items/ItemsOres.cs'))):
        s = clean(read('Items/ItemsOres.cs'))
        a, _ = balanced(s, m.end()-1)
        a = split(a)
        t, bar = a[0].split('.')[-1], a[3].split('.')[-1]
        o = dict(type=t, name=string(a[1]), bar=bar, per=int(a[4]), stage=int(a[6]), value=int(a[7]), station=string(a[8]) if len(a)>8 else '熔炉', station_stage=int(a[9]) if len(a)>9 else 0)
        ores.append(o)
        items[t].update(name=o['name'], kind='材料', texture='Item_' + a[2])
        if t != 'Hellstone':
            items[t].update(stage=o['stage'], source='Drop', value=o['value'])
    by_bar = {o['bar']: o for o in ores}

    recipes = []
    for filename in ['RecipeData.cs', 'ItemsStations.cs', 'ItemsOres.cs', 'RecipeDataChain.cs']:
        s = clean(read('Items/' + filename))
        for m in re.finditer(r'new Recipe\(ItemType\.', s):
            a, _ = balanced(s, s.index('(', m.start()))
            a = split(a)
            if len(a) < 4 or not a[1].isdigit():
                continue
            ingredients = []
            for arg in a[4:]:
                q = re.fullmatch(r'\(ItemType\.(\w+),\s*(\d+)\)', arg)
                if not q:
                    raise ValueError('Unknown recipe ingredient: ' + arg)
                ingredients.append((q[1], int(q[2])))
            recipes.append(dict(result=a[0].split('.')[-1], count=int(a[1]), station=string(a[2]), stage=int(a[3]), ingredients=ingredients))
    for o in ores:
        recipes.append(dict(result=o['bar'], count=1, station=o['station'], stage=o['station_stage'], ingredients=[(o['type'], o['per'])] + ([('Obsidian',1)] if o['type']=='Hellstone' else [])))
    recipes.extend(seals)
    made, uses = collections.defaultdict(list), collections.defaultdict(list)
    for r in recipes:
        made[r['result']].append(r)
        for t, n in r['ingredients']:
            uses[t].append((r['result'], n, r['count']))

    enemies = []
    s = clean(read('Items/EnemyDropData.cs'))
    for m in re.finditer(r'\[("[^"]+")\]\s*=\s*new Entry\s*\{', s):
        body, _ = balanced(s, m.end()-1)
        f = fields(body)
        e = dict(key=string(m[1]), name=string(f['Zh']), en=string(f['En']), stage=int(f['Stage']), kind=string(f['Kind']), hp=int(f['Hp']), scene=string(f['Scene']), boss=f['Boss']=='true', drops=[])
        e['geo'] = int(f.get('Geo','-1'))
        for q in re.finditer(r'\b([DCG])\(([^()]*)\)', f['Drops']):
            a = split(q[2])
            group = int(a.pop(0)) if q[1]=='G' else 0
            t = a[0].split('.')[-1]
            den = int(a[1]) if len(a)>1 else 1
            lo = int(a[2]) if len(a)>2 else 1
            hi = int(a[3]) if len(a)>3 else 1
            num = int(a[4]) if len(a)>4 else 1
            lock = 0 if q[1]=='C' else items[t]['stage']
            if t in by_bar:
                o = by_bar[t]
                t, lo, hi = o['type'], lo*o['per'], hi*o['per']
            e['drops'].append(dict(type=t, den=den, num=num, lo=lo, hi=hi, group=group, lock=lock))
        enemies.append(e)
    drops = collections.defaultdict(list)
    for e in enemies:
        for d in e['drops']:
            drops[d['type']].append((e, d))

    if assets_only:
        items['KnightNail']['texture'] = 'HK_Nail'
        items['DreamNail']['texture'] = 'HK_DreamNail'
        items['Geo']['texture'] = 'HK_Geo'
        missing = [t for t, d in items.items() if not d.get('texture')]
        assert not missing, 'Missing texture mapping: ' + repr(missing)
        return items, enemies

    for t in ('ChaosCylinderFull','ChaosCylinderRandom','ChaosCylinderSimple'):
        for k in ('stage','source','value'):
            items[t][k] = items['ChaosCylinder'][k]
    runtime = json.loads((ROOT/'tools/encyclopedia_runtime.json').read_text(encoding='utf-8'))
    for rel, digest in runtime['source_hashes'].items():
        assert hashlib.sha256((SRC/rel).read_bytes()).hexdigest() == digest, f'Refresh isolated wikiassets export: {rel}'
    actual = runtime['data']
    assert set(items) == set(actual['items']), 'Runtime item coverage mismatch'
    # Stages and sources are settled at run time (ProgressionBalance, material unlocks): the export is authoritative.
    restaged = 0
    for t, d in items.items():
        rd = actual['items'][t]
        # Post-initialization balance changes are authoritative for the displayed stats.
        for field, label in [('Damage', '伤害'), ('Defense', '防御'), ('Mana', '耗魔'), ('PickPower', '镐力')]:
            if rd[field] > 0:
                d['stats'][label] = str(rd[field])
            else:
                d['stats'].pop(label, None)
        if (d['stage'], d['source']) != (rd['Stage'], rd['Source']):
            restaged += 1
            d['stage'], d['source'] = rd['Stage'], rd['Source']
    def recipe_key(r):
        return (r['result'], r['count'], r['station'], r['stage'], tuple(sorted(r['ingredients'])))
    runtime_recipes = [dict(result=r['Result'], count=r['Count'], station=r['Station'], stage=r['StationStage'], ingredients=[(x['Type'],x['Count']) for x in r['Ingredients']]) for r in actual['recipes']]
    # Expanded recipes and post-constructor drop tables are authoritative runtime data.
    recipes = runtime_recipes
    made, uses = collections.defaultdict(list), collections.defaultdict(list)
    for r in recipes:
        made[r['result']].append(r)
        for t,n in r['ingredients']: uses[t].append((r['result'],n,r['count']))
    assert {e['key'] for e in enemies} == set(actual['drops']), 'Runtime enemy coverage mismatch'
    for e in enemies:
        assert all('Weight' in d and 'MeanCount' in d for d in actual['drops'][e['key']]), 'Refresh wikiassets with weighted drop metadata'
        expected = [dict(type=d['Type'], num=d['Num'], den=d['Den'], lo=d['Min'], hi=d['Max'], group=d['Group'], lock=d['LockStage'], master=d['Master'], weight=d['Weight'] if d['Weight']>0 else 1) for d in actual['drops'][e['key']]]
        e['drops'] = expected
        assert 'drop_regions' in actual, 'Refresh wikiassets with six-stage regional death-scene drops'
        merged = {}
        for stage, records in actual['drop_regions'].items():
            for home in records[e['key']]:
                for d in home['Drops']:
                    key = (home['Area'], d['Type'], d['Num'], d['Den'], d['Min'], d['Max'], d['Group'], d['LockStage'], d['Master'], round(d['Probability'], 8))
                    if key not in merged:
                        merged[key] = dict(area=home['Area'], type=d['Type'], num=d['Num'], den=d['Den'], lo=d['Min'], hi=d['Max'], group=d['Group'], lock=d['LockStage'], master=d['Master'], probability=d['Probability'], stages=[])
                    merged[key]['stages'].append(int(stage))
        e['drops'] = list(merged.values())
    drops = collections.defaultdict(list)
    for e in enemies:
        for d in e['drops']: drops[d['type']].append((e,d))
    # Boss bags are emitted by TreasureBags.Leave, outside the ordinary drop table.
    # Follow the bag -> relic -> boss/variant mapping even if relic drops are disabled.
    relic_text = read('Items/RelicsGenerated.cs')
    relic_routes = {relic: dict(key=key, condition='') for key, relic in re.findall(
        r'\["([^"]+)"\]\s*=\s*ItemType\.(Relic\w+)', relic_text)}
    for key, by, value, relic in re.findall(r'\("([^"]+)", "(scene|name)", "([^"]+)", ItemType\.(\w+)\)', relic_text):
        condition = f'场景 `{value}`' if by == 'scene' else f'首领对象名包含 `{value}`（重战形态）'
        relic_routes[relic] = dict(key=key, condition=condition)
    bag_routes = {}
    bags_by_boss = collections.defaultdict(list)
    for bag, relic, zh in re.findall(r'\(ItemType\.(Bag\w+), ItemType\.(Relic\w+), "([^"]+)"', read('Items/TreasureBagsGenerated.cs')):
        assert bag in items and relic in relic_routes, ('Unmapped treasure bag', bag, relic)
        route = dict(relic_routes[relic], bag=bag, name=zh)
        assert any(e['key'] == route['key'] for e in enemies), ('Missing bag boss', bag, route['key'])
        bag_routes[bag] = route
        bags_by_boss[route['key']].append(route)
        items[bag]['kind'] = '宝藏袋'
    assert set(bag_routes) == {t for t in items if t.startswith('Bag')}, 'Treasure bag acquisition coverage mismatch'
    relic_variants = collections.defaultdict(list)
    if relics_enabled:
        for key, by, value, relic in re.findall(r'\("([^"]+)", "(scene|name)", "([^"]+)", ItemType\.(\w+)\)', read('Items/RelicsGenerated.cs')):
            relic_variants[key].append((by, value, relic))

    # Use the game's own Mono runtime, not CoreCLR: a retained intermediate float
    # can shift the ceiling by one. The snapshot is invalidated by source hashes.
    health_snapshot = json.loads((ROOT/'tools/encyclopedia_enemy_health.json').read_text(encoding='utf-8'))
    for rel, digest in health_snapshot['source_hashes'].items():
        assert hashlib.sha256((SRC/rel).read_bytes()).hexdigest() == digest, f'Run isolated healthmath test to refresh changed health source: {rel}'
    health = health_snapshot['records']
    assert set(health) == {e['key'] for e in enemies}
    for e in enemies:
        if e['boss']:
            e['scene'] = health[e['key']].get('scene', e['scene'])
    observed = json.loads((ROOT/'tools/encyclopedia_health_observations.json').read_text(encoding='utf-8'))
    for rel, digest in observed['source_hashes'].items():
        assert hashlib.sha256((SRC/rel).read_bytes()).hexdigest() == digest, f'Health observations need refreshing after source change: {rel}'
    def actor(scene, suffix):
        group = next(s['entries'] for s in observed['scenes'] if s['scene']==scene)
        return next(e for e in group if e['path'].endswith(suffix))
    def observed_pools(scene, suffix):
        return [d['pool'] for d in actor(scene,suffix)['levels']]
    def f32(v):
        return struct.unpack('f',struct.pack('f',v))[0]
    def remaining(a, hp):
        return [math.ceil(f32(hp*f32(d['scale']))) for d in a['levels']]

    npcs = []
    # The townsfolk of Terraria's shop table and the later ones (Town/TownNpcsMore.cs: the Wizard, the Tavernkeep...).
    for rel in ('Items/TownShopData.cs', 'Town/TownNpcsMore.cs'):
        if not (SRC/rel).exists():
            continue
        s = clean(read(rel))
        for m in re.finditer(r'new NpcDef\(', s):
            a, _ = balanced(s, m.end()-1)
            a = split(a)
            n = dict(name=string(a[2]), internal=string(a[1]), stage=int(a[4]), stock=[])
            if any(x['internal']==n['internal'] for x in npcs):
                continue
            for q in re.finditer(r'new Stock\(Items.ItemType\.(\w+),\s*(\d+),\s*(null|new\[\]\s*\{[^}]*\})\)', a[5]):
                n['stock'].append((q[1], int(q[2]), list(map(int,re.findall(r'\d+', q[3]))) if q[3]!='null' else []))
            npcs.append(n)
    # NpcDef applies arrival overrides in its constructor (e.g. TaxCollector).
    # Use final runtime stages rather than the generated constructor arguments.
    npc_runtime = json.loads((ROOT/'tools/encyclopedia_details.json').read_text(encoding='utf-8'))['npcs']
    arrivals = {n['Internal']: n['Stage'] for n in npc_runtime}
    for n in npcs:
        key = n['internal']
        if key not in arrivals and key == 'TravellingMerchant':
            key = 'TravelingMerchant'
        assert key in arrivals, 'Missing final runtime NPC stage: ' + key
        n['stage'] = arrivals[key]
    extra = clean(read('Town/TownShopExtra.cs')) if (SRC/'Town/TownShopExtra.cs').exists() else ''
    for npc,t,stage in re.findall(r'\("(\w+)",\s*ItemType\.(\w+),\s*(\d+)\)',extra):
        owner = next((n for n in npcs if n['internal']==npc), None)
        if owner is not None:
            owner['stock'].append((t,int(stage),[]))
    # ProgressionShops can add stock and change its tier dynamically. Keep static
    # moon-phase conditions, but use the same six-stage runtime sources as crafting.
    if 'shop_stages' in actual:
        earliest = {}
        for stage, stocks in sorted(actual['shop_stages'].items(), key=lambda pair:int(pair[0])):
            for stock in stocks:
                npc = {'TravelingMerchant':'TravellingMerchant'}.get(stock['Npc'], stock['Npc'])
                earliest.setdefault((npc,stock['Type']),int(stage))
        assert {npc for npc,t in earliest} <= {n['internal'] for n in npcs}, 'Unmapped runtime shop NPC; do not silently omit its inventory'
        for n in npcs:
            n['stock'] = [(t,earliest[(n['internal'],t)],phases) for t,_,phases in n['stock'] if (n['internal'],t) in earliest]
            seen = {t for t,_,_ in n['stock']}
            for (npc,t),stage in earliest.items():
                if npc==n['internal'] and t not in seen:
                    n['stock'].append((t,stage,[]));seen.add(t)
    shops = collections.defaultdict(list)
    for n in npcs:
        for t, stage, phases in n['stock']:
            shops[t].append((n, max(stage,n['stage']), phases))

    bossrefs = {}
    for key, st, ref in re.findall(r'\["([^"]+)"\]\s*=\s*R\((\d+),\s*([^\n]+)\),', read('Items/BossStats.cs')):
        bossrefs[key] = (int(st), ref)
    tunes = {}
    for key, life, defense, damage, stun, note in re.findall(r'\["([^"]+)"\]\s*=\s*T\(([\d.]+)f,\s*([\d.]+)f,\s*([\d.]+)f,\s*(true|false),\s*(' + STR + r')\)', read('Items/BossBalance.cs')):
        tunes[key] = (life, defense, damage, stun=='true', string(note))
    bossnames = dict(re.findall(r'static readonly Boss (\w+) = T\("([^"]+)"', read('Items/BossStats.cs')))

    # Human-readable representative regions, following Areas.AreaName's branch order.
    def area(scene):
        groups = [('皇家水道',('GG_Pipeway','GG_Waterways','Waterways')), ('王国边缘',('GG_Lurker','Deepnest_East','Room_Wyrm')), ('愚人斗兽场',('Room_Colosseum',)), ('野兽巢穴',('Deepnest_Spider_Town','Room_spider')), ('深巢',('Deepnest',)), ('蜂巢',('Hive',)), ('远古盆地',('Abyss_17','Abyss_18','Abyss_19')), ('深渊',('Abyss_06','Abyss_08','Abyss_09','Abyss_1','Abyss_Lighthouse')), ('远古盆地',('Abyss',)), ('白色宫殿',('White_Palace',)), ('黑卵神殿',('Room_temple','Room_Final')), ('神居',('GG_',)), ('梦境',('Dream_',)), ('格林剧团',('Grimm_',)), ('水晶山峰',('Mines',)), ('安息之地',('RestingGrounds','Room_Mansion')), ('泪水之城',('Ruins',)), ('真菌荒地',('Fungus2',)), ('苍绿之径',('Fungus1',)), ('呼啸悬崖',('Cliffs',)), ('国王山道',('Tutorial',)), ('德特茅斯',('Town',)), ('遗忘十字路',('Crossroads',))]
        if scene.startswith('Fungus3'):
            # Mirror the exact fog-room list rather than assuming all Fungus3 is gardens.
            fog = read('World/Areas.cs').split('static bool IsFogCanyon',1)[-1].split(';',1)[0]
            return '雾之峡谷' if scene.startswith(tuple(string(x) for x in re.findall(STR, fog))) else '王后花园'
        for name, starts in groups:
            if scene.startswith(starts):
                return name
        return '圣巢'

    # The enemy's roster stage is a loot label; balance may override it.
    def combat_stage(e):
        if e['key'] in bossrefs:
            return bossrefs[e['key']][0]
        scene = e['scene']
        if scene in ('Dream_01_False_Knight','GG_Failed_Champion'): return 2
        if scene in ('Dream_02_Mage_Lord','GG_Soul_Tyrant'): return 3
        if scene in ('Dream_03_Infected_Knight','GG_Lost_Kin'): return 4
        if scene.startswith(('White_Palace','GG_','Dream_','Grimm_','Room_Final','Abyss_15')):
            if scene.startswith(('GG_Pipeway','GG_Waterways')): return 2
            if scene.startswith('GG_Lurker'): return 4
            return 5
        return {'真菌荒地':1,'雾之峡谷':1,'呼啸悬崖':1,'泪水之城':2,'皇家水道':2,'水晶山峰':2,'安息之地':2,'深巢':3,'野兽巢穴':3,'远古盆地':3,'王后花园':3,'王国边缘':4,'蜂巢':4,'愚人斗兽场':4,'深渊':4}.get(area(scene),0)

    special = {
        'KnightNail':'开局获得；永久保留，缺失会补回。不可合成、出售或丢弃。',
        'DreamNail':'在安息之地取得原版梦之钉后获得；永久保留，缺失会补回。不可合成、出售或丢弃。',
        'CopperShortsword':'新档开局背包自带 1 件。', 'CopperPickaxe':'新档开局背包自带 1 件。',
        'WoodenBow':'新档开局背包自带 1 件。', 'WoodenArrow':'新档开局弹药栏自带 100 支。',
        'Torch':'新档开局自带 10 支。', 'GrapplingHook':'新档开局装在钩爪装备格；按 E 使用。',
        'CloudinaBottle':'新档开局已装备；取得帝王之翼时也有对应能力物品补发。',
        'ClimbingClaws':'新档开局已装备；取得螳螂爪时也有对应能力物品补发。',
        'ShieldOfCthulhu':'取得原版蛾翼披风时发放对应能力物品。',
        'HermesBoots':'取得原版飞毛腿护符（gotCharm_37）时发放对应能力物品。',
        'Flipper':'取得原版伊思玛的眼泪时发放对应能力物品。',
        'DeadCellsRamRune':'取得原版荒芜俯冲（quakeLevel）时发放对应能力物品；猛击可砸开下砸地板。',
        'LifeCrystal':'每档在阶段 0、1 的非首领房间随机分布 24 个地面水晶；完整新增面具也会奖励水晶。',
        'Geo':'原版怪物、吉欧矿等原有吉欧来源；击杀掉钱与物品掉落表分别处理。',
        'DirtBlock':'挖掘对应土质格子获得。', 'StoneBlock':'挖掘对应石质格子获得。',
        'Wood':'怪物掉落；回收木方块也可获得。',
        'ChaosCylinderFull':'从混沌圆筒切换模式得到；不是独立刷落。',
        'ChaosCylinderRandom':'从混沌圆筒切换模式得到；不是独立刷落。',
        'ChaosCylinderSimple':'从混沌圆筒切换模式得到；不是独立刷落。',
        'ShadeCloak':'取得原版暗影披风时放进背包，可重铸。',
        'CrystalHeart':'取得原版水晶之心时放进背包，可重铸。',
        'WaterCandle':'商人常驻出售，3000 吉欧。',
        'AbigailsFlower':'墓地环境里（附近立着 7 块墓碑，或在安息之地）墓碑旁随机长出，右键采下。',
    }
    for t in ('Tombstone','GraveMarker','CrossGraveMarker','Headstone','Gravestone','Obelisk'):
        special[t] = '死亡时（被尖刺扎死也算）照原版抛出一块墓碑，滚到能立住的地方立起来，10 秒内找不到地方就变成掉落物；用镐挖下得到本物品。身上吉欧不超过 1000 时从这 6 种里随机。'
    for i in range(1, 6):
        special[f'RichGravestone{i}'] = '死亡时身上吉欧超过 1000，抛出的墓碑是金墓碑（5 种随机），立住或变成掉落物的规则同普通墓碑；用镐挖下得到本物品。'
    for t in ('ChaosCylinderFull','ChaosCylinderRandom','ChaosCylinderSimple'):
        for k in ('stage','source','value'):
            items[t][k] = items['ChaosCylinder'][k]
    for t in made:
        if items[t]['kind'] == '其他' and items[t]['source'] == 'Craft':
            items[t]['kind'] = '材料与杂项'
    for t in ('LifeCrystal','ManaCrystal'):
        items[t]['kind'] = '永久增益'
    items['Geo']['kind'] = '货币'
    items['GrapplingHook']['kind'] = '钩爪'
    for t in ('Bomb','Dynamite'):
        items[t]['kind'] = '爆炸物'
    # Set bonus texts, including multiple case labels returning the same description.
    bonus_texts = {}
    set_text = clean(read('Items/Equipment.cs')).split('public static string SetBonusText',1)[1]
    for labels, tip in re.findall(r'((?:case ArmorSet\.\w+:\s*)+)return\s+('+STR+r')', set_text):
        for key in re.findall(r'case ArmorSet\.(\w+)',labels):
            bonus_texts[key] = string(tip)

    # A place left in the item enum with nothing made of it (an item taken out, such as the God Tamer's seal: the
    # game names it by its enum and offers it nowhere) is no item.
    for t in [t for t,d in items.items() if d['name']==t and actual['items'].get(t,{}).get('Name')==t
              and t not in made and t not in uses and t not in drops and t not in shops]:
        del items[t]
    missing = [t for t,d in items.items() if d['name']==t]
    refs = set(made) | set(uses) | set(drops) | set(shops)
    assert not refs - items.keys(), 'Unregistered item references'
    assert not missing, f'Missing item names: {missing}'
    assert items['TinkerersWorkshop']['value']==100000
    assert items['CobaltHat']['kind']=='盔甲' and items['CobaltHat']['stats']['防御']==str(actual['items']['CobaltHat']['Defense'])
    assert items['LastPrism']['stats']['伤害']==str(actual['items']['LastPrism']['Damage'])
    for e in enemies:
        for stage, records in actual['drop_regions'].items():
            expected = {(home['Area'],d['Type'],d['Num'],d['Den'],d['Min'],d['Max'],d['Group'],d['LockStage'],d['Master'],round(d['Probability'],8)) for home in records[e['key']] for d in home['Drops']}
            observed_rows = {(d['area'],d['type'],d['num'],d['den'],d['lo'],d['hi'],d['group'],d['lock'],d['master'],round(d['probability'],8)) for d in e['drops'] if int(stage) in d['stages']}
            assert observed_rows == expected, ('Stage drop coverage mismatch', e['key'], stage)
    assert len(enemies) == len(re.findall(r'new Entry\s*\{', clean(read('Items/EnemyDropData.cs'))))
    assert len(recipes) == len(actual['recipes'])
    if 'shop_stages' in actual:
        known = {n['internal'] for n in npcs}
        expected = {(npc,t) for npc,t in earliest if npc in known}
        observed_stock = {(n['internal'],t) for n in npcs for t,_,_ in n['stock']}
        assert observed_stock == expected and sum(len(n['stock']) for n in npcs)==len(expected), 'Runtime shop coverage mismatch'
    else:
        assert sum(len(n['stock']) for n in npcs) == len(re.findall(r'new Stock\(', clean(read('Items/TownShopData.cs')))) + len(re.findall(r'\("(\w+)",\s*ItemType\.(\w+),\s*(\d+)\)',extra))

    def itemlink(t):
        return f'[{items[t]["name"]}](#item-{t.lower()})'
    def enemy_link(e):
        return f'[{e["name"]}](#enemy-{e["key"].lower().replace("@","-")})'
    extra_sources = collections.defaultdict(list)
    pet_types = re.findall(r'\[ItemType\.(\w+)\]\s*=\s*new PetDef', read('Items/PetsData.cs'))
    for t in pet_types:
        items[t]['kind'] = '宠物与照明宠物'
        extra_sources[t].append('有掉落表的已登记怪物击杀（包含首领）的额外抽选：本物品概率 0.01%（1/10000），×1；全部宠物共享一次抽选，一次最多给其中一种。此抽选不套用普通掉落的难度倍率或阶段门槛。')
    fishing_source = clean(read('Items/FishingData.cs'))
    extra_drop_groups = []
    for table_name, condition in [('areaBait', '普通怪 1/6、精英 1/3 触发，×1–2'), ('areaMoss', '1/12 触发，×1')]:
        start = fishing_source.index(table_name)
        body, _ = balanced(fishing_source, fishing_source.index('{', start))
        for place, contents in re.findall(r'\["([^"]+)"\]\s*=\s*new\[\]\s*\{([^}]+)\}', body):
            options = re.findall(r'ItemType\.(\w+)', contents)
            extra_drop_groups.append((place, condition, options))
            for t in options:
                extra_sources[t].append(f'额外怪物掉落：击杀发生在{place}的已登记非首领怪物，{condition}；从该地区已解锁的候选物品中等概率选一件，未解锁物品不参与。地区按击杀地点判断，不限于图鉴代表场景。')
    gold_match = re.search(r'goldBait\s*=\s*new\[\]\s*\{([^}]+)\}', fishing_source)
    gold_options = re.findall(r'ItemType\.(\w+)', gold_match[1])
    extra_drop_groups.append(('任意地区（非首领）', '1/150 触发，×1', gold_options))
    for t in gold_options:
        extra_sources[t].append('额外怪物掉落：任意地区已登记非首领怪物，1/150 触发黄金鱼饵抽选，×1；从已解锁的黄金鱼饵中等概率选择一种。')
    def chance(e, d):
        if d.get('master'):
            return f"{d['area']}；仅大师模式必掉（100%）；其他难度不掉落"
        if 'stages' in d:
            stages = sorted(d['stages'])
            label = f'{stages[0]}–{stages[-1]}' if len(stages)>1 and stages==list(range(stages[0],stages[-1]+1)) else '/'.join(map(str,stages))
            return f"{d['area']}；阶段{label}：{100*d['probability']:.4g}%" + (f"（组{d['group']}互斥）" if d['group'] else '')
        base = min(1, d['num']/d['den'])
        if not d['group']:
            return ('必掉（100%）' if base==1 else f"{100*base:.4g}%（{d['num']}/{d['den']}）")
        group = [x for x in e['drops'] if x['group']==d['group']]
        stages = sorted({max(d['lock'],x['lock']) for x in group})
        values = []
        for stage in stages:
            unlocked = [x for x in group if x['lock']<=stage]
            first = unlocked[0]
            prob = min(1,first['num']/first['den'])*d.get('weight',1)/sum(x.get('weight',1) for x in unlocked)
            values.append(f"阶段{stage}：{100*prob:.4g}%（{len(unlocked)}选1）")
        return f"组{d['group']}互斥；"+'；'.join(values)
    def amount(lo, hi):
        return str(lo) if lo == hi else f'{lo}–{hi}'
    def recipe_text(r):
        return ' + '.join(f'{itemlink(t)} ×{n}' for t,n in r['ingredients']) + f' → {itemlink(r["result"])} ×{r["count"]}；{r["station"]}' + ('' if r['station'] == '徒手' else '（站在旁边）') + f'，制作站阶段门槛 {r["stage"]}'

    def health_rows(e):
        ds = health[e['key']]['difficulties']
        one = [d['pool_life'] for d in ds]
        key = e['key']
        rows, notes = [], []
        if e['kind'] in ('invulnerable','non_enemy') or e['hp'] <= 0:
            return [('无常规可击杀血条', ['—']*5)], ['该记录是无敌对象或环境物，不把计算公式得出的 0 当作可击杀生命。']
        if key in ('FalseKnight','FalseKnight@5'):
            scene = 'Dream_01_False_Knight' if key.endswith('@5') else 'Crossroads_10_boss'
            body_name = 'False Knight Dream' if key.endswith('@5') else 'False Knight New'
            body_actor = actor(scene,body_name)
            head_actor = actor(scene,body_name+'/Head')
            def phase_pool(a, hp, phase):
                return [math.ceil(f32(hp * f32(d['scale'] * f32(1 + .2 * phase)))) for d in a['levels']]
            for i in range(3):
                rows += [(f'第 {i+1} 轮：盔甲',phase_pool(body_actor,body_actor['max_hp'],i)),
                         (f'第 {i+1} 次：露出的头',phase_pool(head_actor,40,i))]
            rows.append(('地板坠落后的最后一次露头',phase_pool(head_actor,40,3)))
            rows.append(('含最后露头的血池合计',[sum(row[1][i] for row in rows) for i in range(5)]))
            armour_life = re.search(r'const float FalseKnightArmourLife\s*=\s*([\d.]+)f', read('Items/BossPhases.cs'))
            assert armour_life, 'Review False Knight armour scaling before publishing'
            armour_note = ('失败冠军保持原先的盔甲与头部血量。' if key.endswith('@5') else f'假骑士盔甲单独调整为原来的 {armour_life[1]} 倍（包含神居假骑士），露出的头及最后露头不变。')
            notes.append('头部没有独立图鉴记录，但继承盔甲的首领身份、血量预算和挑战倍率；原始盔甲与头的预算为 10:3。'+armour_note+'后续阶段分别使用 1.2／1.4／1.6 倍。表格按场景组件的最大生命、露头 FSM 的 40 点原版生命与导出 Scale 换算，包含地板坠落后的最后露头。')
        elif key in ('MageLord','MageLord@5'):
            if key=='MageLord@5':
                rows=[('第一阶段：Dream Mage Lord',observed_pools('Dream_02_Mage_Lord','Dream Mage Lord')),('第二阶段：Dream Mage Lord Phase2',observed_pools('Dream_02_Mage_Lord','Dream Mage Lord Phase2'))]
                notes.append('梦境对象映射到灵魂大师的梦境变体，使用整场独立预算；同名第二阶段候选对象只计实际出战的一份。')
            else:
                rows=[('第一阶段',observed_pools('Ruins1_24_boss','Mage Lord')),('第二阶段',observed_pools('Ruins1_24_boss','Mage Lord Phase2'))]
            rows.append(('两阶段合计',[sum(row[1][i] for row in rows) for i in range(5)]))
        elif key=='MantisLord':
            first=observed_pools('Fungus2_15_boss','Battle Main/Mantis Lord')
            s1=observed_pools('Fungus2_15_boss','Battle Sub/Mantis Lord S1')
            s2=observed_pools('Fungus2_15_boss','Battle Sub/Mantis Lord S2')
            rows=[('第一阶段：单独出战的领主',first),('第二阶段：领主 A',s1),('第二阶段：领主 B',s2),('三位合计',[a+b+c for a,b,c in zip(first,s1,s2)])]
            notes.append('每个阶段是半份同期首领生命，后一阶段多 20%，同时上场的两位平分这一份。场景中还有互斥的候选/重打对象，不把同一领主的重复预制对象计成额外首领；正常战斗为一位后两位，共三位。')
        elif key=='BlackKnight':
            knights=[observed_pools('Ruins2_03_boss',f'Battle Control/Black Knight {i}') for i in range(1,7)]
            rows=[(f'骑士 {i}',k) for i,k in enumerate(knights,1)]
            rows.append(('完整六骑士合计',[sum(k[i] for k in knights) for i in range(5)]))
            notes.append('先一只、之后两只一波，每一波按阶段规则分到自己的一份；表中每只骑士都是实测场景对象的初始血池。用吊灯提前砸掉一只时，少打的是当时那一只的血量。')
        elif key=='Oblobble':
            one = observed_pools('Room_Colosseum_Silver','Mega Fat Bee')
            rows=[('奥波路波 A：初始血池',one),('奥波路波 B：初始血池',one),('两只初始血池合计',[x*2 for x in one])]
            extra = remaining(actor('Room_Colosseum_Silver','Mega Fat Bee'),100)
            rows.append(('存活者额外回血上限（非固定必加）',extra))
            rows.append(('含回血的总量范围',[f'{2*x:,}–{2*x+y:,}' for x,y in zip(one,extra)]))
            notes.append('斗兽场脚本对存活者的当前原版 HP 加 100，上限 300；神居改为加 200、上限 750。当前 HP 超过此前 MaxHp 时，EnemyStats 会重新缩放，因此额外恢复的泰拉血量取决于击杀顺序与当时剩余血量。总量范围是初始血池加额外回血的上限口径，不把狂暴阶段再算成一整条满血。')
            gh = observed_pools('GG_Oblobbles','Mega Fat Bee')
            rows.extend([('神居调谐：每只初始血池',gh),('神居调谐：两只初始合计',[x*2 for x in gh])])
        elif key=='NailBros':
            one = observed_pools('GG_Nailmasters','Brothers/Oro')
            mato = observed_pools('GG_Nailmasters','Brothers/Mato')
            rows=[('第一阶段：奥罗单挑',one),('第二阶段：奥罗（与马托平分这一阶段）',mato),('第二阶段：马托',mato),('两阶段合计',[a+2*b for a,b in zip(one,mato)])]
            notes.append('第一阶段奥罗单挑，拿一整阶段；第二阶段比第一阶段多 20%，奥罗再次激活后和马托平分，两位各一半（表中第二阶段两行取马托的实测血池）。该对象 hpScale.level1=0，不触发额外神居倍率。')
        elif key=='LobsterLancer':
            beast=observed_pools('Room_Colosseum_Gold','Entry Object/Lobster')
            rider=observed_pools('Room_Colosseum_Gold','Entry Object/Lancer')
            gh_beast=observed_pools('GG_God_Tamer','Entry Object/Lobster')
            gh_rider=observed_pools('GG_God_Tamer','Entry Object/Lancer')
            rows=[('斗兽场：兽体',beast),('斗兽场：骑手',rider),('两个目标全清的初始总量',[a+b for a,b in zip(beast,rider)]),('仅击败兽体使骑手投降：必打目标',beast),('神居调谐：兽体',gh_beast),('神居调谐：骑手',gh_rider),('神居调谐：两个目标初始合计',[a+b for a,b in zip(gh_beast,gh_rider)])]
            notes.append('兽体与骑手分别按导出的实际组件值列出；只击败兽体会让骑手投降，因此骑手不能计入必打血量。')
        elif key=='HollowKnight':
            a=actor('Room_Final_Boss_Core','Boss Control/Hollow Knight Boss')
            boundaries=[1000,750,400,0]
            scaled=[remaining(a,hp) for hp in boundaries]
            rows=[(f'第 {i+1} 阶段（原版阈值 {boundaries[i]}→{boundaries[i+1]}）',[x-y for x,y in zip(scaled[i],scaled[i+1])]) for i in range(3)]
            last=remaining(a,250)
            rows += [('第四阶段：Set Phase 4 补至 250',last),('普通终结路线：含最终补血总量',[x+y for x,y in zip(scaled[0],last)])]
            notes.append('这是同一对象在前三阶段共用的血条，归零后脚本补到原版 250 HP。Hornet Cancel 也有设置 250 的替代动作，不能再额外加一次；虚空之心/梦之钉路线可在大黄蜂介入时转入辐光，不要求把普通终结路线所有血量打完。首领自刺也会扣血，因此总血池不等于玩家必须输出。')
        elif key=='FinalBoss':
            a=actor('Dream_Final_Boss','Boss Control/Radiance')
            boundaries=[3000,2650,2250,2000,1300,0]
            scaled=[remaining(a,hp) for hp in boundaries]
            labels=['初始阶段','尖刺波阶段','剑雨/狂暴阶段','平台阶段','攀升与终结段（剩余血池）']
            rows=[(f'{labels[i]}（原版阈值 {boundaries[i]}→{boundaries[i+1]}）',[x-y for x,y in zip(scaled[i],scaled[i+1])]) for i in range(5)]
            rows.append(('共享血条总量（不重复加阶段）',scaled[0]))
            notes.append('阶段界限来自实测 HP 检查器：2650 / 2250 / 2000 / 1300。表内每段是同一血条在相邻阈值之间的显示差值，故精确相加等于总血条；不是每个行为阶段重新生成一只满血辐光。终结还有原版剧情动作，末段剩余血池不能直接当作必须手打的伤害。')
        else:
            rows=[('单体初始最大生命',one)]
            if health[e['key']]['boss_reference']:
                notes.append('本条按一个连续血池列生命；招式变化、硬直和行为阶段不额外重复累计一整条生命。')
        if health[e['key']]['boss_reference']:
            notes.append('图鉴配置预算（普通 / 困难 / 专家 / 大师 / 空洞）：'+' / '.join(f'{d["life"]:,}' for d in ds)+'。这是单个映射对象的一份配置预算，不保证等于整场战斗总量；取整、未命中首领映射、补血/复活都会产生差别，整场总量以上表汇总为准。')
        return rows,notes

    def print_health(rows):
        add('| 部位 / 阶段 | 普通 | 困难 | 专家 | 大师 | 空洞 |')
        add('|---|---:|---:|---:|---:|---:|')
        for name, values in rows:
            assert len(values)==5, f'Incomplete health row: {name}'
            add('| '+name+' | '+' | '.join(f'{v:,}' if isinstance(v,int) else str(v) for v in values)+' |')

    # Cross-check formula exports against separately instantiated real components.
    # For split pools, the formula-only Info has no Hm; those rows deliberately use
    # the real components instead of the Hm=null whole-budget result.
    # A boss fought in phases has half its budget in a phase (BossPhases): the body that opens alone holds that half.
    for key,scene,suffix,share in [('MantisLord','Fungus2_15_boss','Battle Main/Mantis Lord',0.5),('BlackKnight','Ruins2_03_boss','Black Knight 1',0.5),('LobsterLancer','Room_Colosseum_Gold','Entry Object/Lobster',1),('FinalBoss','Dream_Final_Boss','Boss Control/Radiance',1),('NailBros','GG_Nailmasters','Brothers/Oro',0.5)]:
        formula = [d['pool_life']*share for d in health[key]['difficulties']]
        assert all(abs(a-b) <= 1 for a,b in zip(formula, observed_pools(scene,suffix))), f'Offline/engine health mismatch: {key}'

    lines = []
    def add(s=''):
        lines.append(s)
    add('# 空洞泰拉 · 本地百科全书')
    add()
    add('本地离线查阅版，中文物品名与内部 ID 双索引。适用于本项目当前工作区源码；包含尚未提交的修改。普通流程以非创造存档为准。')
    add()
    add(f'**收录：{len(items)} 件物品、{len(recipes)} 条配方、{len(enemies)} 条怪物/场景变体记录、{len(npcs)} 位泰拉城镇 NPC、{sum(len(n["stock"]) for n in npcs)} 条商店库存。**')
    add()
    add('范围：本 Mod 已注册的全部物品、掉落、配方、城镇商店和玩法说明。物品资料依据源码；当前全部怪物血量由游戏自身的 Mono 运行时执行当前计算函数导出，另以隔离副本读取的 13 个场景、33 个相关 HealthManager 组件及血量动作核对多部位。未逐一完整实战全部怪物；原版房间/对话也不是逐格攻略。')
    add()
    add('使用：在编辑器中按 **Ctrl+F 搜中文名或内部 ID**；点击物品名跳到获取方式，点击怪物名跳到完整掉落。未另行标注的概率和数量为普通难度基础值；大师圣物仅在大师模式掉落。')
    add()
    sections = [('reading','查阅约定与重要规则'),('progress','阶段路线与解锁'),('npcs','NPC 入住与完整商店'),('enemies','各阶段怪物与完整掉落'),('index','全物品分类索引'),('items','全物品逐项图鉴'),('boss-summons','Boss 召唤物合成路径'),('recipes','完整合成配方'),('fishing','钓鱼、宝匣与地区储物箱'),('systems','生命、装备、经济与探索'),('original','原版 NPC、剧情与结局路线'),('manual','操作、联机、安装与配置参考'),('audit','数据来源与覆盖核验')]
    for anchor,title in sections:
        add(f'- [{title}](#{anchor})')

    def heading(anchor, title):
        add(); add(f'<a id="{anchor}"></a>'); add(); add('## '+title); add()
    heading('reading','查阅约定与重要规则')
    add('- **三种阶段分别看**：玩家解锁阶段是五个原版能力标志的计数；怪物掉落阶段是掉落表标签；战斗阶段按地区和首领平衡表决定。开局戴着云朵瓶、攀爬爪，不等于已取得帝王之翼或螳螂爪。')
    add('- **掉矿不掉锭**：掉落表中的可熔炼金属锭在初始化时全部换成对应矿石，数量乘以每锭耗矿数；首领也照此转换。本文已经转换，不会把狱石锭误写成直接掉落。没有对应熔矿映射的锭仍按原表掉落（如神圣锭）；月明锭按当前映射换成夜明矿。')
    add('- **独立掉落与任选组**：无组条目各自判定；同一组每次最多给一件，在当前已解锁组员中按实际权重选择。表内显示单件最终概率，已经包含组触发率和权重分摊；普通小怪饰品池基础触发率 3%，精英为 7.5%，候选较多时分别按每件 0.5%／1% 扩容，上限 12%／25%；首领重复击杀饰品池为 25%，首杀保持必出。各组分别判定，未解锁时不掉落。无敌、非敌人等记录不作正常可刷来源。')
    add('- **大师圣物**：'+('首领掉落表中的圣物仅在大师模式必掉，其他难度不掉落。' if relics_enabled else '当前构建的 Relics.Enabled 为 false，圣物定义保留，但没有注册实体，也不会从首领掉落。'))
    add('- **额外击杀掉落**：宠物、鱼饵和苔藓在普通掉落表后独立判定，详见[玩法系统](#systems)。这些额外抽选不套用普通掉落的难度倍率。')
    add('- **“配置阶段”不是硬锁**：物品图鉴中的阶段来自物品数据库标签；早期自带装备、提前到达的怪物、材料齐全且在附近工作站制作，都可能早于该标签。配方是否可做以材料和实际工作站条件为准。')
    add('- **代表场景不是全分布**：怪物条目列的是掉落表中的一个代表房间，用内部场景名精确定位；同种怪可能在其他房间出现。')
    add('- **文本效果与实际实现**：物品说明取当前中文定义；部分饰品沿用泰拉描述，但圣巢没有对应减益/钓鱼等机制时可能无实际作用，不能由说明推断 Mod 已实现泰拉全部系统。')
    add()
    add('| 难度 | 非必掉概率乘数（不超过 100%） | 堆叠物数量上限额外增量 | 普通怪血量比例（相对大师） |')
    add('|---|---:|---|---:|')
    for row in [('普通','1','0','25%'),('困难','1.1','⌊(最大−最小+1)×15%⌋','50%'),('专家','1.2','⌊(最大−最小+1)×30%⌋','75%'),('大师 / 空洞','1.3','⌊(最大−最小+1)×50%⌋','100%')]:
        add('| '+' | '.join(row)+' |')
    add()
    add('不堆叠装备不增加数量上限。首领生命使用 BossStats 与 BossBalance 单独计算，不套普通怪比例。')
    assert 'stage switch { <= 0 => .4f, 1 => .48f, 2 => .5f, 3 => .55f, 4 => .3f, _ => .16f }' in read('Items/EnemyDrops.cs')
    add('吉欧另行结算：原版死亡掉钱总额在普通／困难／专家／大师与空洞分别乘 1.5／1.875／2.25／3，取整后再应用护符加成。正式首领另有独立吉欧奖励，按当前难度整场生命预算 × 首领阶段系数计算；阶段 0–5 系数为 0.4／0.48／0.5／0.55／0.3／0.16。神居改用所在场景与玩家战斗阶段，并叠加挑战奖励倍率；因此不能把普通世界基础吉欧当成神居最终奖励。')
    add('空洞难度沿用大师的敌人技能、攻击速度、硬直、防御减伤与饰品栏规则，保留原生面具数量增伤；角色生命上限为85%，治疗药水冷却延长20%，每次环境伤害固定为当前生命上限的50%（向上取整，生命上限本身不变，防御和减伤不降低该伤害，原有免疫和护盾仍有效），敌人材料与吉欧收益额外×1.4。四锁：武器最终伤害85%、生命70%、饰品属性与技能失效（保留冲刺和护甲套装）、恢复锁在战斗中禁用自然恢复与吸血且药水治疗70%（休息时解除，蓝量不受限）。空洞与生命锁合并取70%。通关材料奖励：武器/生命/恢复各+20%，饰品+40%，全四锁×2.5，空洞全四锁×3.5；按阶段发放，不额外掉落成品装备。大师及以下只使用阶段伤害，不再叠加面具数量。血量表中的大师数值也适用于空洞（单人、对应场景和挑战等级）。')

    heading('progress','阶段路线与解锁')
    add('空洞泰拉把流程分成 0–5 共六个阶段。**解锁阶段等于已取得的不同原版移动能力数量，顺序不限**：蛾翼披风、螳螂爪、水晶之心、帝王之翼、暗影披风，每种只计算一次（Shop.UnlockedStage）。例如先拿螳螂爪就是阶段 1，随后取得帝王之翼就是阶段 2，不需要先拿披风或水晶之心。装备提供的临时移动能力不计入。阶段升高后德特茅斯搬来新的 NPC，商店上新，工作站开放新配方。')
    add()
    by_key = {e['key']: e for e in enemies}
    add('### 怎样进入下一阶段'); add()
    add('以下是常规取得顺序的路线示例，不是进入各阶段必须满足的能力顺序。'); add()
    add('| 常规路线的阶段变化 | 此能力路线的首领 | 之后取得的能力 |'); add('|---|---|---|')
    for row in [
        ('阶段 0 → 阶段 1', f'**{enemy_link(by_key["Hornet"])}**（苍绿之径，一战）', '**蛾翼披风**：击败后在苍绿之径取得'),
        ('阶段 1 → 阶段 2', '无需击败首领', '**螳螂爪**：在真菌荒地的螳螂村拾取；螳螂领主可以之后再打'),
        ('阶段 2 → 阶段 3', '无需击败首领', '**水晶之心**：在水晶山峰拾取；水晶守卫不拦路'),
        ('阶段 3 → 阶段 4', f'**{enemy_link(by_key["InfectedKnight"])}**（远古盆地）', '**帝王之翼**：击败后在战斗房间后方取得'),
        ('阶段 4 → 阶段 5', f'**{enemy_link(by_key["Hornet"])}**（王国边缘，二战）', '**暗影披风**：击败后在弃置外壳取得王者之印；持王者之印打开深渊入口，在深渊取得暗影披风'),
    ]:
        add('| '+' | '.join(row)+' |')
    add()
    add('### 各阶段一览'); add()
    def counterpart(key):
        st, ref = bossrefs[key]
        return st, bossnames.get(ref.split('.')[0], ref.split('.')[0])
    (h_stage, h_ref), (v_stage, v_ref) = counterpart('Hornet'), counterpart('InfectedKnight')
    add(f'“这一阶段的首领”按战斗阶段列出常规世界中的首领（不含神居重打）；首领的生命和掉落按它所在的阶段计算，不需要全部击败才能进入下一阶段。守着能力的首领按它开启的阶段平衡：苍绿之径的大黄蜂（相当于{h_ref}）列在阶段 {h_stage}，残破容器（相当于{v_ref}）列在阶段 {v_stage}，它们在上一阶段就可以挑战。'); add()
    add('| 阶段 | 进入方式 | 这一阶段去的地区 | 这一阶段的首领 | 新入住 NPC |')
    add('|---:|---|---|---|---|')
    routes = [
        ('开局', '国王山道、德特茅斯、遗忘十字路、苍绿之径'),
        (f'击败苍绿之径的{enemy_link(by_key["Hornet"])}，取得蛾翼披风', '真菌荒地和螳螂村、雾之峡谷、呼啸悬崖'),
        ('拾取螳螂爪（无需击败首领）', '泪水之城、皇家水道、水晶山峰、安息之地'),
        ('拾取水晶之心（无需击败首领）', '深巢、野兽巢穴、远古盆地、王后花园'),
        (f'击败{enemy_link(by_key["InfectedKnight"])}，取得帝王之翼', '王国边缘、蜂巢、愚人斗兽场、深渊'),
        (f'击败王国边缘的{enemy_link(by_key["Hornet"])}取得王者之印，再在深渊取得暗影披风', '白色宫殿、梦境、格林剧团、神居、最终战'),
    ]
    for stage,(gate,places) in enumerate(routes):
        if stage > 0:
            gate = f'取得任意 {stage} 种不同移动能力；常规路线示例：{gate}'
        bosses = [e for e in enemies if e['boss'] and e['kind'] in ('boss','dream_boss','arena_boss')
                  and not e['scene'].startswith('GG_') and combat_stage(e)==stage]
        seen_names = set()
        shown = []
        for e in bosses:
            if e['name'] not in seen_names:
                seen_names.add(e['name']); shown.append(enemy_link(e))
        names = '、'.join(n['name'] for n in npcs if n['stage']==stage) or '无新增'
        add(f'| {stage} | {gate} | {places} | {"、".join(shown) or "—"} | {names} |')
    add()
    add('地区和首领是建议路线，不是门禁：敌人的强度和掉落按它所在的地区，不按解锁阶段。提早进入后期地区时，那里的敌人照样是后期强度，掉落也是后期的东西。例如苍绿之径普通怪按地区阶段 0，而大黄蜂首领按阶段 1 平衡。失败冠军 / 灵魂暴君 / 失落近亲使用 @5 掉落变体，但战斗阶段分别为 2 / 3 / 4；这些梦境战取得梦之钉后即可挑战，不需要觉醒梦之钉。')
    add()
    add('能力对应物品：蛾翼披风 → 克苏鲁护盾；螳螂爪 → 攀爬爪；帝王之翼 → 云朵瓶；飞毛腿护符 → 赫尔墨斯靴；伊思玛的眼泪 → 脚蹼；荒芜俯冲 → 重锤符文。原版技能标志用于剧情进度和阶段计算，实际移动依赖穿戴装备。')

    heading('npcs','NPC 入住与完整商店')
    add('下列泰拉 NPC 的城镇住处在**德特茅斯**，通常达到入住阶段后在镇上生成；渔夫例外，必须先在蓝湖左岸右键叫醒，才会搬进镇上。不套用泰拉原版房屋、背包枪械、哥布林入侵或特定 Boss 的入住条件。右键近距离对话。向导可查看某材料参与的配方；查看不等于免费制作。')
    add()
    add('商店同时检查 NPC 入住阶段、商品阶段及月相；打开时刷新。月相编号 = `floor(存档游玩秒数 / 1440) % 8`，每 24 分钟游玩时间换一相，编号 0–7。下列价格为无折扣单价；打开商店时戴着优惠卡按 80% 计算。')
    for n in npcs:
        add(); add(f'### {n["name"]}（入住阶段 {n["stage"]}）'); add()
        if n['internal'] == 'Angler':
            add('入住前提：在蓝湖（`Crossroads_50`）左侧岸边找到沉睡的渔夫，靠近并右键叫醒，再返回德特茅斯。无商店；每天获得一个任务名额，最多积累三个，交鱼后立即抽取下一条任务。任务鱼与台词已替换为 36 条圣巢新任务。'); continue
        if n['internal'] == 'TravellingMerchant':
            add('旅商为来访 NPC，下表是完整候选商品池，每次来访只随机带来 4–6 种达到阶段要求的商品，不保证当天出售指定物品。')
        if not n['stock']:
            add('无商店；提供对话与材料配方查询。'); continue
        add('| 商品 | 开售阶段 | 月相编号 | 单价 |'); add('|---|---:|---|---:|')
        for t,stage,phases in n['stock']:
            add(f'| {itemlink(t)} | {max(stage,n["stage"])} | {"、".join(map(str,phases)) if phases else "不限"} | {shop_money(t)} |')

    heading('enemies','各阶段怪物与完整掉落')
    add('网页图鉴可按**小怪 / 精英 / 首领**和战斗阶段组合筛选。分类沿用游戏图鉴规则：标记为 Boss 的记录归首领；其余记录中，小首领或原始生命至少 70 的归精英，其他归小怪。特殊、无敌和环境记录仍以各词条的原始类型说明为准。')
    add()
    add('按**战斗阶段**归组；每条直接列出普通、困难、专家、大师、空洞的 **Mod 实际缩放生命**。多部位、独立血池、重复补满分别列出，再给合计；仅改变招式而共用血条的行为阶段不重复加血。无敌或环境对象标为无常规可击杀血条。')
    add()
    add('计算口径：血条使用 EnemyStats.LifeMax 的向上取整；图鉴预算使用 LifeWhole。两者可能相差 1，不能用旧平衡报告的四舍五入代替实际血条。常规世界战斗与神居重打也不能混用；正文先列常规战斗，神居专属首领直接按其场景列出。血量不是“玩家必须输出的伤害”：首领自伤、可跳过的部位、存活者回血及过量伤害都要单独考虑。')
    add()
    add('“实际”指当前版本的生命换算与血池设置，不把设计意图当成已生效实现。假骑士头部、灵魂暴君和驯兽师骑手等对象，在实测中没有命中预想的首领 Record/Ref，已按它们实际采用的换算单独写明。阶段脚本的补血/复活按读取到的 SetHP 值推算；本次未把所有战斗逐一打到结局。')
    kinds = {'normal':'普通','conditional':'条件出现','pogo':'可下劈/环境交互','unique':'一次性/特殊','invulnerable':'无敌','non_enemy':'非敌人','passive':'被动','boss':'首领','miniboss':'精英/小首领','arena':'斗兽场怪','arena_boss':'斗兽场首领','dream_boss':'梦境首领','warrior_dream':'战士之梦','godhome_boss':'神居首领'}
    summons_by_boss = {seal['key']: seal for seal in seals}
    for stage in range(6):
        group = [e for e in enemies if combat_stage(e)==stage]
        add(); add(f'### 战斗阶段 {stage} · {len(group)} 条记录'); add()
        add('、'.join(enemy_link(e) for e in group)); add()
        for e in group:
            add(f'<a id="enemy-{e["key"].lower().replace("@","-")}"></a>'); add()
            add(f'#### {e["name"]} · {e["en"]}'); add()
            add(f'- 记录：`{e["key"]}`；类型：{kinds.get(e["kind"],e["kind"])}；掉落阶段 {e["stage"]}；战斗阶段 {stage}。')
            category = '首领' if e['boss'] else '精英' if e['kind'] == 'miniboss' or e['hp'] >= 70 else '小怪'
            add(f'- 图鉴分类：{category}。')
            add(f'- 代表地点：{area(e["scene"])}，`{e["scene"]}`。')
            geo_source = read('Items/EnemyDrops.cs')
            assert '1.5f * (World.GameDifficulty.Current switch' in geo_source
            if e['geo'] >= 0:
                add('- 原版击杀吉欧（无贪婪等加成）：'+ '；'.join(f'{name} {round(e["geo"] * factor)}' for name, factor in [('普通',1.5),('困难',1.875),('专家',2.25),('大师 / 空洞',3)])+'。该数额为原版死亡掉钱通道，独立首领奖励另算。')
            else:
                add('- 原版击杀吉欧：读取敌人对象的原版钱币总额后缩放；普通 ×1.5、困难 ×1.875、专家 ×2.25、大师／空洞 ×3，四舍六入五取偶。不将未配置的原版金额当作零。')
            if e['key'] in summons_by_boss:
                add(f'- 对应召唤物：{itemlink(summons_by_boss[e["key"]]["result"])}（点击查看获取方式与合成配方）。')
            for bag in bags_by_boss[e['key']]:
                condition = f'（{bag["condition"]}）' if bag['condition'] else ''
                add(f'- 宝藏袋：专家或大师模式下击败{bag["name"]}{condition}可获得 {itemlink(bag["bag"])} ×1；右键点击打开。')
            if e['drops']:
                add('- 额外掉落：宠物抽选'+('；非首领另有按击杀地区判定的鱼饵、苔藓及黄金鱼饵抽选' if not e['boss'] else '')+'，详见[额外击杀掉落规则](#systems)。')
            add()
            rows, hp_notes = health_rows(e)
            print_health(rows)
            add()
            stats = health[e['key']]['difficulties']
            assert all('defense' in d and 'stage_stats' in d for d in stats), 'Refresh healthbook with defense and progression stats'
            if e['boss']:
                add(f'- 代表场景实际首领防御：{stats[0]["defense"]}；神居挑战另叠加对应挑战的防御倍率与固定防御。')
            add('- 全难度阶段预览：普通敌人沿用游戏的进度追赶规则；首领沿用自身战斗预算。攻击为接触伤害，未实测接触面具数的记录按 1 面具估算；具体弹幕及招式伤害不由此表推断。生命为游戏图鉴预算，多部位和补血以本页上方血池说明为准。')
            add()
            add('| 难度 | 玩家阶段 | 生命 | 攻击 | 防御 |'); add('|---|---:|---:|---:|---:|')
            difficulty_names = {'Normal':'普通','Hard':'困难','Expert':'专家','Master':'大师','Hollow':'空洞'}
            assert len(stats) == 5 and {d['difficulty'] for d in stats} == set(difficulty_names)
            for difficulty in stats:
                assert [p['stage'] for p in difficulty['stage_stats']] == list(range(6))
                for preview in difficulty['stage_stats']:
                    add(f'| {difficulty_names[difficulty["difficulty"]]} | {preview["stage"]} | {preview["life"]} | {preview["damage"]} | {preview["defense"]} |')
            add()
            for note in hp_notes:
                add('- '+note)
            if e['key'] in bossrefs:
                ref = bossrefs[e['key']][1]
                bn = bossnames.get(ref.split('.')[0], ref.split('.')[0])
                share = re.search(r'"([^"]+)"',ref)
                add(f'- 配置的数值参照：{bn}'+(f'（{share[1]}）' if share else '')+'；实际命中首领映射的部位已应用倍率，未命中的部位按上方实测说明计算。泰拉参照首领的身体部件不是本场战斗的额外部位，不额外累计。')
            if e['key'] in tunes:
                life,df,dm,stun,note=tunes[e['key']]
                armour = re.search(r'const int ArmourLifePercent\s*=\s*(\d+)', read('Items/BossBalance.cs'))
                assert armour, 'Review current boss stagger rules before publishing'
                add(f'- 首领映射命中时的配置：生命 ×{life}，防御为泰拉参照值 × min(1, {df}) × 0.5 后取整，伤害 ×{dm}。'+f'原版具有硬直机制的首领，剩余生命高于 {armour[1]}% 时允许累计硬直，降至 {armour[1]}% 或以下进入霸体；空洞难度使用相同硬直规则。')
            if e['kind'] in ('invulnerable','non_enemy','pogo','passive'):
                add('- 获取提示：这是特殊/环境记录；表内数据不代表正常流程一定可击杀或可刷。优先查物品的其他来源。')
            add()
            if not e['drops']:
                add('无配置物品掉落。'); add(); continue
            add('下表按普通难度、该记录首次击杀导出；地区和玩家阶段同时满足才进入对应掉落池。概率不是保底次数。重复击杀首领的奖励概率或数量可能降低，游戏内图鉴按实际击杀记录显示。'); add()
            add('| 掉落物 | 掉落条件 / 概率 / 互斥组 | 数量 | 玩家解锁阶段 |'); add('|---|---|---:|---:|')
            for d in e['drops']:
                add(f'| {itemlink(d["type"])} | {chance(e,d)} | {amount(d["lo"],d["hi"])} | {d["lock"]} |')
            add()
            for by, value, relic in relic_variants[e['key']]:
                condition = f'场景 `{value}`' if by == 'scene' else f'首领对象名包含 `{value}`（重战形态）'
                add(f'- 变体圣物：{condition}时，大师模式的圣物替换为 {itemlink(relic)}；其他难度不掉落。')
            add()

    heading('index','全物品分类索引')
    add('分类用于查找；有些物品同时是材料、弹药或武器，全部配方用途另在物品条目列出。')
    categories = list(dict.fromkeys(d['kind'] for d in items.values()))
    for category in categories:
        group = [t for t,d in items.items() if d['kind']==category]
        add(); add(f'### {category}（{len(group)}）'); add()
        add('、'.join(itemlink(t) for t in group)); add()


    # ------------------------------------------------------------------ fishing, crates, area chests (runtime)
    # Exported by the wikiassets run: each crate opened crate_opens times at each stage, each area fished
    # fishing_casts times (fishing power fishing_power, a big pond), each area chest opened chest_opens times.
    crate_opens = actual.get('crate_opens', 0)
    fishing_casts = actual.get('fishing_casts', 0)
    fishing_power = actual.get('fishing_power', 0)
    chest_opens = actual.get('chest_opens', 0)
    def pct(c):
        return f'{100*c:.3g}%' if c >= 0.001 else '<0.1%'
    def stage_span(stages):
        stages = sorted(stages)
        if not stages:
            return ''
        if stages == list(range(stages[0], stages[-1]+1)):
            return f'{stages[0]}' if len(stages) == 1 else f'{stages[0]}–{stages[-1]}'
        return '/'.join(map(str, stages))
    catch_table = collections.defaultdict(lambda: collections.defaultdict(dict))
    for st, by_area in actual.get('fishing_stages', {}).items():
        for place, rows in by_area.items():
            for r in rows:
                if r['Type'] in items:
                    catch_table[place][r['Type']][int(st)] = r['Chance']
    # A crate is opened from the stages it is fished up at (any area's water), its contents those stages'.
    fished_at = collections.defaultdict(set)
    for place, per in catch_table.items():
        for t, by in per.items():
            fished_at[t] |= set(by)
    crate_table = collections.defaultdict(lambda: collections.defaultdict(dict))
    for st, by_crate in actual.get('crate_stages', {}).items():
        for crate, rows in by_crate.items():
            if crate not in items:
                continue
            open_from = fished_at[crate] or set(range(items[crate]['stage'], 6))
            if int(st) not in open_from:
                continue
            for r in rows:
                if r['Type'] in items:
                    crate_table[crate][r['Type']][int(st)] = (r['Chance'], r['Min'], r['Max'])
    chest_parts = {0: '武器箱', 1: '饰品箱', 2: '补给箱'}
    chest_table = collections.defaultdict(lambda: collections.defaultdict(dict))
    for st, by_chest in actual.get('chest_stages', {}).items():
        for key, rows in by_chest.items():
            for r in rows:
                if r['Type'] in items:
                    chest_table[key][r['Type']][int(st)] = (r['Chance'], r['Min'], r['Max'])
    from_crates, from_fishing, from_chests = collections.defaultdict(list), collections.defaultdict(list), collections.defaultdict(list)
    for crate, per in crate_table.items():
        for t, by in per.items():
            from_crates[t].append((crate, sorted(by), max(c for c,_,_ in by.values()), min(lo for _,lo,_ in by.values()), max(hi for _,_,hi in by.values())))
    for place, per in catch_table.items():
        for t, by in per.items():
            from_fishing[t].append((place, sorted(by), max(by.values())))
    for key, per in chest_table.items():
        for t, by in per.items():
            from_chests[t].append((key, sorted(by), max(c for c,_,_ in by.values()), min(lo for _,lo,_ in by.values()), max(hi for _,_,hi in by.values())))
    def chest_name(key):
        place, part = key.split('|')
        return place + chest_parts.get(int(part), '储物箱')
    def stage_table(per, with_counts):
        """An item-by-stage table: how often each thing comes, at each of the six stages (blank: not then)."""
        rows = sorted(per.items(), key=lambda kv: (-max((v[0] if with_counts else v) for v in kv[1].values()), items[kv[0]]['name']))
        add('| 物品 | 阶段 0 | 阶段 1 | 阶段 2 | 阶段 3 | 阶段 4 | 阶段 5 |' + (' 数量 |' if with_counts else ''))
        add('|---|' + '---:|'*6 + ('---:|' if with_counts else ''))
        for t, by in rows:
            cells = []
            for stage in range(6):
                v = by.get(stage)
                cells.append('' if v is None else pct(v[0] if with_counts else v))
            line = f'| {itemlink(t)} | ' + ' | '.join(cells) + ' |'
            if with_counts:
                lo = min(v[1] for v in by.values()); hi = max(v[2] for v in by.values())
                line += f' {amount(lo, hi)} |'
            add(line)
        add()

    def ingredient_path(t, count, depth=0, seen=None):
        seen=set() if seen is None else set(seen)
        indent='    '*depth
        candidates=made[t]
        if t not in seen and candidates:
            r=candidates[0]
            runs=math.ceil(count/r['count'])
            add(f'{indent}- {itemlink(t)} ×{count}：在 **{r["station"]}** 制作 {runs} 次（每次产出 {r["count"]}）。')
            seen.add(t)
            for material,number in r['ingredients']: ingredient_path(material,number*runs,depth+1,seen)
        else:
            d=items[t]
            origins=[]
            if d['source']=='Basic': origins.append('德特茅斯/长椅基础制作栏')
            origins += [n['name']+'商店' for n,_,_ in shops[t]]
            eligible=[e for e,_ in drops[t] if e['kind'] not in ('non_enemy','invulnerable')]
            origins += [enemy_link(e) for e in eligible[:3]]
            if len(eligible)>3: origins.append('更多掉落见物品页')
            if from_fishing[t]: origins.append('钓鱼（' + '、'.join(place for place,_,_ in sorted(from_fishing[t], key=lambda x:-x[2])[:2]) + '）')
            if from_crates[t]: origins.append('开宝匣（' + '、'.join(itemlink(c) for c,_,_,_,_ in sorted(from_crates[t], key=lambda x:-x[2])[:2]) + '）')
            if from_chests[t]: origins.append('地区储物箱')
            add(f'{indent}- {itemlink(t)} ×{count}：'+('；'.join(origins) if origins else '具体获取条件见物品词条')+'。')

    heading('items','全物品逐项图鉴')
    unresolved = []
    for t,d in items.items():
        add(f'<a id="item-{t.lower()}"></a>'); add(); add(f'### {d["name"]}'); add()
        add(f'`{t}` · {d["kind"]} · 配置阶段 {d["stage"]}'); add()
        if d['tip']:
            add('**作用**：'+esc(d['tip'])); add()
        if d['stats']:
            add('**基础属性**：'+'；'.join(f'{k} {v}' for k,v in d['stats'].items())+'。'); add()
        if d['set'] and d['set']!='None':
            add('**盔甲套装**：`'+d['set']+'`；套装判定 `'+d['setbonus']+'`。'+esc(bonus_texts.get(d['setbonus'],''))); add()
        sources = []
        sources.extend(extra_sources[t])
        for place, sts, best in sorted(from_fishing[t], key=lambda x:-x[2]):
            sources.append(f'钓鱼：{place}的水域，阶段 {stage_span(sts)}，渔力 {fishing_power} 时每次抛竿约 {pct(best)}（统计 {fishing_casts} 次抛竿）。')
        for crate, sts, best, lo, hi in sorted(from_crates[t], key=lambda x:-x[2]):
            sources.append(f'开宝匣：{itemlink(crate)}，阶段 {stage_span(sts)}，每个最多约 {pct(best)}，×{amount(lo, hi)}（每阶段统计开 {crate_opens} 个）。')
        for key, sts, best, lo, hi in sorted(from_chests[t], key=lambda x:-x[2]):
            sources.append(f'地区储物箱：新存档放在{chest_name(key)}中，阶段 {stage_span(sts)}，最多约 {pct(best)}，×{amount(lo, hi)}。')
        if t in bag_routes:
            bag = bag_routes[t]
            boss = next(e for e in enemies if e['key'] == bag['key'])
            condition = f'（{bag["condition"]}）' if bag['condition'] else ''
            sources.append(f'专家或大师模式下击败 {enemy_link(boss)}'+(f' 的{bag["name"]}形态' if bag['condition'] else '')+f'{condition}可获得本宝藏袋 ×1；普通和困难模式不掉落。右键点击打开，获得对应首领的战利品。')
        if t.startswith('Relic') and not relics_enabled:
            sources.append('当前构建关闭了大师圣物，不能通过普通制作、商店或首领掉落取得；保留此词条用于查阅定义。')
        if t in special:
            sources.append(special[t])
        if d['source']=='Basic' and t not in ('KnightNail','DreamNail') and not (t.startswith('Relic') and not relics_enabled):
            sources.append(f'基础制作栏：德特茅斯或长椅附近可取得，单件基础价 {shop_money(t)}；若材料齐全能合成，同一物品的付费项会被合成项取代。')
        for n,stage,phases in shops[t]:
            sources.append(f'商店：{n["name"]}，阶段 {stage} 起'+('，来访时随机选入库存，不保证每次出售' if n['internal']=='TravellingMerchant' else ('，月相 '+ '、'.join(map(str,phases)) if phases else '，不限月相'))+f'；单价 {shop_money(t)}。')
        for r in made[t]:
            sources.append('合成：'+recipe_text(r)+'。')
        for e,dr in drops[t]:
            sources.append(f'掉落：{enemy_link(e)}（{area(e["scene"])}，`{e["scene"]}`；掉落标签阶段 {e["stage"]}），{chance(e,dr)}，×{amount(dr["lo"],dr["hi"])}，玩家解锁阶段 {dr["lock"]} 起。'+(' **该记录为无敌/非敌人，不作正常可刷来源。**' if e['kind'] in ('invulnerable','non_enemy') else ''))
        for e in enemies:
            for by, value, relic in relic_variants[e['key']]:
                if relic == t:
                    condition = f'场景 `{value}`' if by == 'scene' else f'首领对象名包含 `{value}`（重战形态）'
                    sources.append(f'掉落：{enemy_link(e)} 的变体，{condition}时替换基础圣物；仅大师模式必掉 ×1，其他难度不掉落。')
        if not sources:
            unresolved.append(t)
            sources.append('当前注册数据未找到普通流程的明确获取路径（无掉落、配方、城镇库存或已列特殊发放）；创造模式制作栏可查。不要套用泰拉原版来源。')
        add('**获取方式**'); add()
        for source in sources:
            add('- '+source)
        if uses[t]:
            add(); add('**作为材料**：'+'；'.join(f'{itemlink(result)} ×{count}（每次消耗本物品 {need}）' for result,need,count in uses[t])+'。')
        if made[t]:
            add(); add(f'**合成流程**（按第一条配方展开到原材料；其他配方见上方“获取方式”）：'); add()
            ingredient_path(t, made[t][0]['count'])
        if t in crate_table:
            add(); add(f'**开箱内容**（每个阶段模拟打开 {crate_opens} 个，表中为一个宝匣开出该物品的概率；只列出能钓到这种宝匣的阶段）：'); add()
            stage_table(crate_table[t], True)
        if d['file']:
            add(); add(f'定义来源：[{Path(d["file"]).name}]({d["file"]})。')
        add()

    heading('boss-summons','Boss 召唤物合成路径')
    current_version = re.search(r'const string Version = "([^"]+)"', read('Plugin.cs')).group(1)
    add(f'本栏对应当前 **{current_version}** 工作区版本，共 **{len(seals)} 种**召唤物。它们是首次击败后的重战道具，不是大师圣物，也不是召唤武器。')
    add()
    add('**通用路径**：击败首领周边小怪取得当地材料 → 在砧制作召唤物 → 首次击败对应首领并满足使用阶段 → 手持使用，传送进完整原版竞技场。')
    add()
    add('- 每个召唤物使用下表两种当地掉落材料，产出 1 个。梦境变体采用本体区域，特殊首领采用住所或入口周边；无需统一玻璃、石块底料。')
    add('- 站在砧旁边、达到配方阶段且材料齐全时制作。**能制作不等于能使用**：使用始终检查探索阶段及该首领的图鉴首杀记录。')
    add('- 普通世界中可以使用，梦境和神居中不能发起；梦境型召唤物另需梦之钉。附近已有首领、已有道具挑战或无法操作时会拒绝。')
    add('- 确认成功进入竞技场并找到活体首领后消耗一个；条件不符或进入失败不消耗。联机时队友一起传送，只扣使用者的一份。')
    add()
    add('| 召唤物 | 对应首领 | 完整材料 | 小怪来源与区域 | 阶段 | 梦之钉 |')
    add('|---|---|---|---|---:|---|')
    for seal in sorted(seals,key=lambda r:r['stage']):
        enemy=next(e for e in enemies if e['key']==seal['key'])
        ingredients=' + '.join(f'{itemlink(t)} ×{n}' for t,n in seal['ingredients'])
        sources='、'.join(enemy_link(next(e for e in enemies if e['key']==key)) for key in dict.fromkeys(seal['material_sources']))
        add(f'| {itemlink(seal["result"])} | {enemy_link(enemy)} | {ingredients} | {seal["locality"]}：{sources} | {seal["stage"]} | {"需要" if seal["dream"] else "不需要"} |')
    add()
    add('### 逐件合成路径')
    add('下面展开制作 1 个所需的原料数量。出现多种配方时展示第一条可行材料路线，其他路线见物品词条；来源链接包含具体怪物、代表场景、数量与概率。')
    for seal in sorted(seals,key=lambda r:r['stage']):
        t=seal['result']
        enemy=next(e for e in enemies if e['key']==seal['key'])
        add();add('#### '+items[t]['name']);add()
        add(f'对应 {enemy_link(enemy)}；使用阶段 {seal["stage"]}；需先有该首领的图鉴击杀记录'+('，并取得梦之钉。' if seal['dream'] else '。'))
        add()
        for material,number in seal['ingredients']: ingredient_path(material,number)
        add(f'- 最后在 **砧** 合成 {itemlink(t)} ×1。');add()

    heading('recipes','完整合成配方')
    add('全部配方保留：同一结果的不同配方分别列出。材料链接可追溯到具体刷取来源。表内“门槛”是该制作站开放的阶段；除徒手配方外都要站在所需的制作站旁边，规则见下方。')
    add()
    add('| 结果 | 产量 | 材料 | 工作站 | 门槛 |'); add('|---|---:|---|---|---:|')
    for r in recipes:
        add(f'| {itemlink(r["result"])} | {r["count"]} | '+ ' + '.join(f'{itemlink(t)} ×{n}' for t,n in r['ingredients'])+f' | {r["station"]} | {r["stage"]} |')
    add()
    add('制作判定（照泰拉瑞亚原版）：① 徒手配方在任何地方可做；② 其余配方要站在所需的制作站旁边（左右 5 格、上下 4 格），并达到配方的阶段门槛；③ 水、酸水、蜂蜜、墓地等环境配方还要处在对应环境里（墓地：安息之地，或附近立着 7 块墓碑）；④ 花吉欧做的基础物品在德特茅斯或长椅附近。需备齐材料，先扣背包，再扣人物 8 格内箱子；合成栏还受当前职业筛选影响。')
    add()
    add('实物工作站：工作台、熔炉、铁砧/铅砧、秘银砧/山铜砧、锯木机、织布机、工匠作坊、放下的玻璃瓶。秘银/山铜砧兼容普通砧；玻璃瓶提供“玻璃瓶”炼药工作站。地狱熔炉、天磨、冰雪机、远古操纵机等其他制作站同样摆在地上使用，来源详见各物品词条；恶魔祭坛和猩红祭坛固定在德特茅斯长椅的两侧。')

    heading('fishing','钓鱼、宝匣与地区储物箱')
    add(f'本页数据由游戏运行时直接模拟导出：每个地区的水域在每个阶段钓 {fishing_casts} 次（渔力 {fishing_power}、足够大的水池、不带任务鱼），每种宝匣每个阶段开 {crate_opens} 个，每种地区储物箱每个阶段生成 {chest_opens} 次。表中百分比是模拟得到的出现率，会有少量随机误差；渔力、鱼饵、宝匣药水和运气会改变实际概率。')
    add()
    add('### 各地区钓鱼渔获'); add()
    add('“酸水”为在酸池中钓鱼（需要能在酸水中钓鱼的装备）。每种宝匣的开箱内容见对应宝匣的物品词条。'); add()
    for place in sorted(catch_table, key=lambda p: (p == '酸水', p)):
        add(f'#### {place}'); add()
        stage_table(catch_table[place], False)
    add('### 钓鱼宝匣'); add()
    add('| 宝匣 | 可钓到的阶段 | 开出物品种类 | 最常见的内容 |'); add('|---|---|---:|---|')
    for crate in sorted(crate_table, key=lambda c: (items[c]['stage'], items[c]['name'])):
        per = crate_table[crate]
        top = sorted(per.items(), key=lambda kv: -max(v[0] for v in kv[1].values()))[:4]
        add(f'| {itemlink(crate)} | {stage_span(fished_at[crate]) or stage_span(range(items[crate]["stage"],6))} | {len(per)} | ' + '、'.join(itemlink(x) for x,_ in top) + ' |')
    add()
    add('### 地区储物箱'); add()
    add('新存档时每个地区按面积放置储物箱（最大的地区 5 个，其余按比例，神居不放）：一个武器箱、一个饰品箱，其余为补给箱。箱子内容在放置时按当时的阶段生成；下表为各阶段的出现率。'); add()
    places = sorted({k.split('|')[0] for k in chest_table})
    for place in places:
        add(f'#### {place}'); add()
        for part in range(3):
            key = f'{place}|{part}'
            if key in chest_table:
                add(f'**{chest_parts[part]}**'); add()
                stage_table(chest_table[key], True)

    heading('systems','生命、装备、经济与探索')
    add('### 额外击杀掉落：宠物、鱼饵与苔藓')
    add('以下抽选在怪物固定掉落表之后执行，不包含在固定表条目统计中；前提是已登记且有掉落表的怪物被正常结算击杀。无敌或非敌人记录不代表可以正常刷取。')
    add()
    add('**宠物**：包含首领的击杀均可抽选，每一种宠物概率为 0.01%（1/10000），×1。全部宠物共享一次抽选，一次最多给一种；不检查物品配置阶段，不套用普通掉落难度倍率。')
    add('、'.join(itemlink(t) for t in pet_types)); add()
    add('**鱼饵与苔藓**：首领不参与。地区取击杀发生的位置；先按下表触发，再从该组当前已解锁物品中等概率选一种，没有已解锁候选时不掉落。三个类别分别判定，均不套用普通掉落难度倍率。精英为非首领且 Kind=miniboss 或原始生命至少 70 的记录。')
    add()
    add('| 击杀地区 | 触发概率 / 数量 | 候选物品 |'); add('|---|---|---|')
    for place, condition, options in extra_drop_groups:
        add(f'| {place} | {condition} | '+ '、'.join(itemlink(t) for t in options)+' |')
    add()
    add('### 永久成长'); add()
    add('- 初始生命 100、魔力 20。生命水晶每个 +20，最多 15；允许已吃数量 = min(15, 6 + 超出初始 5 个面具的完整面具数 + 已击败的不同 Boss 记录数)。24 个地面水晶各存档位置不同，已挖状态会保存；不是固定坐标攻略。')
    add('- 生命果每个 +5，最多 20，先吃满生命水晶。魔力水晶每个 +20，最多 9。最大生命 = min(700, 100 + 水晶数×20 + 生命果数×5 + 额外完整面具数×40)，生命力 Buff 再额外增加按 20 取整的 20%；魔力 = min(400, 20 + 魔力水晶数×20 + 灵魂容器数×20 + 装备加成)。')
    add('- 骨钉按阶段基础伤害 14 / 26 / 42 / 60 / 85 / 125；骨钉匠每次升级额外 +25% 基础值，四次为 2 倍。梦之钉按阶段 40 / 75 / 120 / 150 / 200 / 300，觉醒后 ×1.5。')
    add('- 恶魔之心增加饰品栏；大师另加一栏。装备效果需按物品说明和实际装备位置生效；四套配装可切换，普通档共用背包，创造档各自独立背包。')
    add()
    add('### 全部护甲套装奖励'); add()
    add('同族不同头部/胸甲可能选择不同奖励；每件装备条目列出套装判定 ID，以下按该 ID 查询。动作触发型效果如神圣闪避、钛金碎片、幽灵治疗、甲虫与日耀护盾在 ArmorEffects 中执行。')
    add()
    add('| 套装判定 ID | 完整套装奖励说明 |'); add('|---|---|')
    for key, tip in bonus_texts.items():
        add(f'| `{key}` | {esc(tip)} |')
    add()
    add('### 怪物生命、强度与刷新'); add()
    add('- 普通怪实际最大生命 = ceil(原版生命 ×9.375 ×难度比例 ×地区生命倍率)，地区阶段 0–5 倍率为 1.05／1.10／1.18／1.25／1.65／2.8。旧地区还会按已解锁阶段追赶强度；上方血量表以导出时的进度为准。首领部位使用实际 Scale 再向上取整，多部位总量相加；图鉴配置预算另列，不能混作单体或整场总血池。')
    add('- 普通怪防御随地区阶段 0–5 为 2 / 4 / 8 / 10 / 20 / 30。首领用泰拉首领参照值，再乘独立调整；三螳螂、守望者等按参战数量分摊，多阶段生命按池计算，所以不能把原版基础生命直接当作最终总生命。')
    add('- 默认普通怪加倍；仅符合刷新条件的怪会刷新。源码计时按原版 hp：≤10 为 30 秒、≤25 为 45 秒、≤50 为 90 秒、更高为 180 秒；Unstoppable 强敌分支为 300 秒，均再受配置倍率影响。刷新前 3 秒虚影；玩家离刷新点太近会等待。首领、剧情一次性敌人、斗兽场/神居/梦境/白宫等不套普通刷新。')
    add('- 首领房间的战斗范围内不能挖、放置或炸毁地形，房间其他区域允许建造；必须进入战斗区域才可伤害首领。睡眠唤醒和开场动画有保护，召唤物不会跳过正常开战流程。')
    add()
    add('### 金钱、材料与存储'); add()
    add('- 宝藏磁铁（游戏内“宝藏磁石”）额外扩大 150 像素拾取范围，并吸引附近吉欧；与金戒指同时装备取较大的范围，不相加。幸运币、钱币戒指与贪婪戒指统一从成功命中入口结算，召唤物与哨兵命中也能触发。')
    add('- 盔甲锻造按装备自身配置阶段 0–5 限制为 +3／+6／+9／+12／+15／+18，每次成功增加 1 防御；成功率 50%，敲满基础总价约 5000–30000 吉欧，不含失败消耗。旧装备超过新上限的强化等级保留。')
    add('- 吉欧为实体物品，一叠上限 2,147,483,647；存钱罐为私人共享银行，购买与原版收费会使用其中的钱。死亡带走的钱与存钱罐保护规则见物品条目。')
    add('- 价值 V 以泰拉铜币计；V≤100 按 V，V>100 的购买价格为 round(100×(V/100)^0.8) 铜；100 铜等于 1 吉欧，可用小数吉欧。文档商店价已按此换算。')
    add('- 出售价通常为购买价格的 1/5；无价值原料及完全由无价值原料递归合成的物品不能卖钱。回购物品按卖价，关闭商店会清理当次回购列表。')
    add('- 堆叠物统一上限 9999（吉欧例外），箱子支持排序、补货，背包支持快速堆叠；制作可读附近箱子。地面普通掉落退出游戏会清除。')
    add()
    add('### 地图、晶塔、挖掘与联机'); add()
    add('- 购买区域地图后，未探索格子以 70% 透明度显示；人物周围 8 格内、视线可达的格子随移动探索并变为完全不透明，生命水晶仍需探索发现。旧地图会先自动备份再重置探索位，已买地图和地形保留。普通档晶塔需解锁，创造档全开；晶塔以长椅为锚，鹿角站只通车站。')
    add('- 镐子挖格、方块与平台建设、抓钩、绳索、照明按 Mod 规则运行。首领战保留原版地形与流程；探索时可挖通路线，但不会自动替你完成原版剧情标志。')
    add('- 默认原版过门场景切换；无缝世界为默认关闭的实验配置。局域网 IP 直连最多 16 人，详细房主/客户端操作与限制见后面的操作参考。')

    heading('original','原版 NPC、剧情与结局路线')
    def stage_npcs(stage):
        return '、'.join(n['name'] for n in npcs if n['stage']==stage) or '无新 NPC'
    add('下列为空洞骑士主线的路线提示，和前面源码定义的泰拉 NPC 入住表是两套系统。原版 NPC 的全部对话、商品价格、救援分支及每个房间事件不在本项目掉落数据库中，本文不补造这些数据。')
    add()
    add('| 原版人物/目标 | 通常位置与前置 | 关联解锁 |'); add('|---|---|---|')
    for row in [
        ('斯莱','遗忘十字路村落小屋唤醒后返回德特茅斯；格鲁兹之母一带路线','原版斯莱商店；不控制泰拉军火商入住'),
        ('制图师柯尼法 / 伊塞尔达','各区域遇到制图师；镇上地图店','区域地图购买与查图；未探索区域 70% 透明度，亲自探索后完全不透明'),
        ('骨钉匠','进入泪水之城找到工坊；按原版费用与苍白矿石升级','四次骨钉升级，对本 Mod 骨钉各加 25%'),
        ('先知','安息之地梦之钉剧情；收集精华','梦之钉、梦之门、觉醒梦之钉等奖励沿原版流程；觉醒需要 1800 精华'),
        ('大黄蜂（苍绿之径）→ 蛾翼披风','苍绿之径击败大黄蜂后取得','已取得的移动能力种类增加 1，玩家阶段随之增加 1'),
        ('螳螂爪','真菌荒地螳螂村拾取；不需要先击败螳螂领主','已取得的移动能力种类增加 1，玩家阶段随之增加 1'),
        ('水晶之心','水晶山峰拾取，不需要击败首领','已取得的移动能力种类增加 1，玩家阶段随之增加 1'),
        ('残破容器 → 帝王之翼','远古盆地击败残破容器后，在战斗房间后方取得','已取得的移动能力种类增加 1，玩家阶段随之增加 1'),
        ('大黄蜂二战 → 王者之印','王国边缘战胜后进入弃置外壳领取','开启通向深渊的原版资格；不是击败瞬间就给暗影披风'),
        ('暗影披风','持王者之印进入深渊，沿原版能力路线取得','已取得的移动能力种类增加 1，玩家阶段随之增加 1'),
        ('三个守梦人','教师档案馆 / 守望者尖塔 / 野兽巢穴，需梦之钉及各自路线','解除黑卵封印；守望者骑士、乌姆等战斗按原版路线关联'),
        ('白色夫人 / 白色宫殿 / 出生地','完成原版王后花园、觉醒梦之钉入白宫及王之魂路线','王之魂与虚空之心；用于辐光路线'),
        ('空洞骑士 / 辐光','黑卵封印解除；辐光还需虚空之心与战斗中的原版梦之钉触发','对应结局与掉落；不意味着所有 DLC 内容自动通关'),
        ('格林剧团','呼啸悬崖启动梦魇灯笼，按剧团收火流程','格林、梦魇之王或放逐分支；非主线必需'),
        ('寻神者 / 神居','皇家水道垃圾坑的原版入口流程','诸神堂、万神殿与专属首领；遵守各自原版解锁条件'),
    ]:
        add('| '+' | '.join(row)+' |')

    heading('manual','操作、联机、安装与配置参考')
    add('以下保留项目文档的安装、功能与操作正文，供离线查阅；物品来源、价格与阶段发生矛盾时，以前面直接从源码生成的表和规则为准。功能说明不能替代逐项运行验证。')
    ranges = []
    for name in ('INSTALL.md','FEATURES.md','GAMEPLAY.md','MULTIPLAYER.md','UNINSTALL.md'):
        document = ROOT/'docs'/name
        def rebase_document_link(match):
            label, url = match[1], match[2]
            if url.startswith(('#', 'http:', 'https:')):
                return match[0]
            local, sep, fragment = url.partition('#')
            target = (document.parent/local).resolve()
            if target.is_relative_to(ROOT) and target.is_file():
                return f'[{label}]({target.relative_to(ROOT).as_posix()}{sep}{fragment})'
            return match[0]
        ranges.append(re.sub(r'\[([^\]]+)\]\(([^)]+)\)', rebase_document_link, document.read_text(encoding='utf-8-sig')))
    for block in ranges:
        add()
        block = re.sub(r'(\]\()((?:\.\./|\./)[^)]+)(\))', lambda m: m[1]+posixpath.normpath('docs/'+m[2])+m[3], block)
        block = re.sub(r'\]\(#([^)]*)\)',r'](README.md#\1)',block)
        add(re.sub(r'^(#{2,4}) ', r'#\1 ', block, flags=re.M).strip())

    heading('audit','数据来源与覆盖核验')
    add('- 本次百科的全部物品阶段与来源、每条配方、全部掉落条目的物品/数量/分子分母/互斥组/解锁阶段，均与隔离游戏运行时导出逐项一致；不以正则匹配数量代替实际内容核对。')
    add(f'- 物品枚举（排除 None）：{len(items)}；中文定义覆盖：{len(items)-len(missing)}/{len(items)}。所有物品均有独立获取章节和内部 ID。')
    add(f'- 配方：{len(recipes)} 条，包含生成配方、家具配方、盔甲配方、{len(ores)} 条运行时生成的熔矿配方；材料与结果引用均已检查。')
    add(f'- 掉落：{len(enemies)} 条记录；{sum(len(e["drops"]) for e in enemies)} 条独立物品掉落；所有可熔炼锭已执行与源码相同的矿石替换。')
    add(f'- 生命：{len(enemies)} 条记录全部有五难度实际血量表（不可击杀记录明确标记）；游戏内调用 EnemyStats/BossStats/BossBalance/Areas/GameDifficulty 导出，并用 13 个场景的组件与血量动作核对拆分。公式导出与实际组件值逐难度交叉检查；该导出不包含战斗测试。')
    add(f'- 城镇 NPC：{len(npcs)} 位；库存 {sum(len(n["stock"]) for n in npcs)} 条，包含商品阶段、月相与非线性吉欧价格。')
    add(f'- 未找到普通来源：{len(unresolved)} 件；逐项明确标注，不把“已注册”误写为“普通流程可获得”。')
    if unresolved:
        add('  '+ '、'.join(itemlink(t) for t in unresolved))
    add()
    add('主要依据：')
    add('- [全部魔法武器蓝耗核查（4.1.0）](docs/magic-mana-audit-4.1.0.md)：79 件基础蓝耗、修复前后对照、持续施法与减蓝特例。')
    add('- [游戏运行时血量导出](tools/encyclopedia_enemy_health.json)，当前全部怪物记录，每条五种难度。')
    add('- [血池实测记录与原版 HP 动作](tools/encyclopedia_health_observations.json)；记录附有相关源码 SHA-256，源码变化后生成器会要求刷新实测记录，防止静默沿用旧血池。')
    if (ROOT/'武器输出平衡核验.md').is_file():
        add('- [武器输出平衡核验](武器输出平衡核验.md)：原版攻击时序、随机弹幕数量、持续命中估算和独立游戏内 DPS 实测。')
    if (ROOT/'护甲套装原版属性恢复核验.md').is_file():
        add('- [护甲套装原版属性恢复核验](护甲套装原版属性恢复核验.md)：原版单件属性、完整套装奖励、特殊能力与隔离游戏验证。')
    for rel in ['Items/Items.cs','Items/ItemsGenerated.cs','Items/ItemsOres.cs','Items/ItemsStations.cs','Items/ItemsMoney.cs','Items/ItemsCraftChain.cs','Items/ItemsInfo.cs','Items/BossSummons.cs','Items/RecipeDataChain.cs','Items/RecipeData.cs','Items/EnemyDropData.cs','Items/EnemyDrops.cs','Items/Relics.cs','Items/RelicsGenerated.cs','Items/FishingCrates.cs','Items/FishingData.cs','Items/Pets.cs','Items/PetsData.cs','Items/TownShopData.cs','Town/TownNpcs.cs','Items/Shop.cs','Items/InventoryGui.cs','Items/BossStats.cs','Items/BossBalance.cs','World/Areas.cs','World/LifeCrystals.cs','World/EnemyRespawn.cs','Player/PlayerHooks.cs']:
        add(f'- [{rel}](src/TerraKnight/{rel})')
    add()
    add('更新方法：在项目根目录运行 `python tools/generate_encyclopedia.py`。生成器只读取源码与实测快照并更新本 MD，不启动游戏、不修改存档；若相关源码改变，应先用隔离副本 healthmath / healthbook 测试刷新血量/血池快照。')

    result = '\n'.join(lines) + '\n'
    anchors = re.findall(r'<a id="([^"]+)"',result)
    links = re.findall(r'\]\(#([^)]+)\)',result)
    assert len(anchors)==len(set(anchors)), 'Duplicate anchors'
    assert not set(links)-set(anchors), 'Broken internal links: '+repr(set(links)-set(anchors))
    assert all(result.count(f'<a id="item-{t.lower()}">')==1 for t in items)
    assert all(result.count(f'<a id="enemy-{e["key"].lower().replace("@","-")}">')==1 for e in enemies)
    OUT.write_text(result, encoding='utf-8')
    print(json.dumps(dict(output=str(OUT), items=len(items), recipes=len(recipes), enemies=len(enemies), drop_rows=sum(len(e['drops']) for e in enemies), npcs=len(npcs), stock_rows=sum(len(n['stock']) for n in npcs), unresolved=unresolved, categories=dict(collections.Counter(d['kind'] for d in items.values())), other_items=[t for t,d in items.items() if d['kind']=='其他'], armor_bonuses=len(bonus_texts), bytes=len(result.encode('utf-8')), internal_links=len(links)),ensure_ascii=False,indent=2))


if __name__ == '__main__':
    build()
