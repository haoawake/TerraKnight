"""Audit exported sources without inventing seeds for recipe cycles."""
import argparse
import collections
import json
from pathlib import Path
from audit_crafting_continuity import factory, continuity


def read(path):
    return json.loads(Path(path).read_text('utf-8-sig'))


def audit(run):
    data = read(run / 'saves/progression-audit.json')
    loot = read(run / 'saves/loot-source-audit.json')
    assert loot.get('FreeMaking') is False, 'Free-making export cannot prove normal acquisition'
    items = data['items']
    recipe_count = len(data['recipes'])
    # TerraMod.ResetPlayer's fixed inventory, equipment and ammo; possession is
    # a real seed, not a source inferred from an item's Basic/Craft tag.
    data['starterSources'] = ['CopperShortsword','CopperPickaxe','WoodenBow','KnightNail',
                             'Torch','WoodenArrow','GrapplingHook','ClimbingClaws','CloudinaBottle']
    # Shimmer transforms require a real source for From. Keep native late locks;
    # the Crossroads spring's physical reachability is a separate world claim.
    for pair in loot.get('Transforms', []):
        data['recipes'].append(dict(Result=pair['To'], Count=1, Station='',
            StationStage=max(items[pair['From']]['Stage'], items[pair['To']]['Stage'], 5 if pair['Late'] else 0),
            Environment='None', Ingredients=[dict(Type=pair['From'], Count=1)]))
    for a,b in [('FlaironFlail','Flairon'),('ChaosCylinder','ChaosCylinderFull'),
                ('ChaosCylinderFull','ChaosCylinderRandom'),('ChaosCylinderRandom','ChaosCylinderSimple'),
                ('ChaosCylinderSimple','ChaosCylinder')]:
        data['recipes'].append(dict(Result=b, Count=1, Station='', StationStage=items[b]['Stage'],
            Environment='None', Ingredients=[dict(Type=a, Count=1)]))
    secondary = collections.defaultdict(list)
    for row in loot['Secondary']:
        secondary[row['Stage']].append(row)
    stages = []
    earliest = {}
    for stage in data['stages']:
        s = stage['Stage']
        # Use all actual habitat rows, rather than only a record's default scene.
        for row in loot['Drops']:
            if row['Stage'] != s or row['Godhome'] or row['Difficulty'] != 'Normal' or row['Master']:
                continue
            if row['ItemChance'] > 0:
                stage['Drops'].setdefault(row['Record'], []).append(dict(Type=row['Item'], Chance=row['ItemChance']))
        stage['Crates'] = list(set(stage['Crates']) | {t for row in secondary[s] for t in row['Counts']})
        # These are separate real producers, absent from ordinary enemy tables.
        stage['WorldSources'] = list(set(stage['WorldSources']) |
            {r['Item'] for r in loot.get('PetEggs', []) if r['Stage'] <= s} |
            {r['Item'] for r in loot['Drops'] if r['Stage'] == s and not r['Godhome']
             and r['Difficulty'] == 'Master' and r['Master'] and r['ItemChance'] > 0})
        result = factory(data, stage)
        for t in result['witnesses']:
            earliest.setdefault(t, s)
        missing_accessories = [t for t, d in items.items() if d['IsAccessory'] and not d['Placeholder']
                               and d['Stage'] <= s and t not in result['witnesses']]
        blocked_real = [r for r in result['blocked'] if r['recipe'] < recipe_count]
        eligible_real = sum(r['StationStage'] <= s for r in data['recipes'][:recipe_count])
        stages.append(dict(Stage=s, Reachable=result['reachable'], Recipes=eligible_real,
                           CraftableRecipes=eligible_real-len(blocked_real), BlockedRecipes=blocked_real,
                           MissingAccessories=missing_accessories))
    accessories = []
    best = {}
    stage_leaks = []
    blocked = []
    for row in loot['Drops']:
        if row['Blocked']:
            blocked.append(row)
        d = items[row['Item']]
        if d['Stage'] > row['Stage'] and not row['Master']:
            stage_leaks.append(row)
        if row['Difficulty'] != 'Normal' or row['First'] or row['Godhome'] or row['Master'] or row['ItemChance'] <= 0:
            continue
        k = (row['Item'], row['Stage'])
        if k not in best or row['ItemChance'] > best[k]['ItemChance']:
            best[k] = row
    slow = []
    for (t, s), row in best.items():
        d = items[t]
        if s != d['Stage'] or not (d['IsAccessory'] or d['Material']):
            continue
        threshold = .05 if row['Boss'] else .005
        p = row['ItemChance']
        if p < threshold:
            slow.append(dict(Item=t, Name=d['Name'], Stage=s, Source=row['Record'], Boss=row['Boss'],
                             Chance=p, MeanKills=1/p, QuantityMean=(row['Min']+row['Max'])/2))
    for t, d in items.items():
        if not d['IsAccessory'] or d['Placeholder']:
            continue
        routes = best.get((t, d['Stage']))
        accessories.append(dict(Item=t, Name=d['Name'], Stage=d['Stage'], Source=d['Source'],
                                Earliest=earliest.get(t), BestDrop=routes,
                                Recipes=[r for r in data['recipes'] if r['Result'] == t]))
    orphan = [dict(Item=t, Name=d['Name'], Stage=d['Stage'], Source=d['Source'], Material=d['Material'],
                   Weapon=d['IsWeapon'], Armor=d['IsArmor'], Accessory=d['IsAccessory'])
              for t, d in items.items() if not d['Placeholder'] and t != 'None' and t not in earliest]
    secondary_leaks = [dict(Kind=r['Kind'], Source=r['Source'], Stage=r['Stage'], Item=t, ItemStage=items[t]['Stage'])
                       for r in loot['Secondary'] for t in r['Counts'] if items[t]['Stage'] > r['Stage']]
    unknown_walls = [r for r in orphan if loot.get('Kinds', {}).get(r['Item'], {}).get('PlacesWall', 0) > 0]
    unresolved_functional = [r for r in orphan if r['Item'] != 'DreamNail'
                             and (r['Accessory'] or r['Material'] or r['Armor'] or r['Weapon'])]
    secondary_power_leaks = [r for r in secondary_leaks if items[r['Item']]['IsWeapon']
                             or items[r['Item']]['IsArmor'] or items[r['Item']]['IsAccessory']]
    declared_banners = {r['Item'] for r in loot.get('Banners', [])}
    categories = collections.Counter()
    statuses = []
    for t, d in items.items():
        if d['Placeholder'] or t == 'None':
            status = 'placeholder-or-none'
        elif t in earliest:
            status = 'source-or-craft-closure'
        elif t in declared_banners:
            status = 'declared-kill-banner'
        elif t == 'DreamNail' or loot.get('Kinds', {}).get(t, {}).get('HkItem'):
            status = 'native-Hollow-Knight-item'
        elif loot.get('Kinds', {}).get(t, {}).get('PlacesWall', 0) > 0:
            status = 'wall-with-unresolved-source'
        else:
            status = 'unresolved-in-this-export'
        categories[status] += 1
        statuses.append(dict(Item=t, Stage=d['Stage'], Source=d['Source'], Status=status, Earliest=earliest.get(t)))
    return dict(Items=len(items), Recipes=recipe_count, DropRows=len(loot['Drops']),
                MilestoneMode=loot['MilestoneMode'], Stages=stages, Accessories=accessories,
                Orphans=orphan, SlowSources=sorted(slow, key=lambda r:r['Chance']),
                StageLeaks=stage_leaks, BlockedDrops=blocked, SecondaryLeaks=secondary_leaks,
                SecondaryEquipmentLeaks=secondary_power_leaks, UnknownWalls=unknown_walls,
                UnresolvedFunctional=unresolved_functional, SourceCategories=dict(categories),
                AllItemStatuses=statuses, Continuity=continuity(data))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('run', type=Path)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    result = audit(args.run)
    args.output.write_text(json.dumps(result, ensure_ascii=False, indent=2), 'utf-8')
    print('items/recipes/drops', result['Items'], result['Recipes'], result['DropRows'])
    for s in result['Stages']:
        print('stage', s['Stage'], 'blocked recipes', len(s['BlockedRecipes']), 'missing accessories', len(s['MissingAccessories']))
    print('leaks/blocked/secondary leaks/orphans', len(result['StageLeaks']), len(result['BlockedDrops']), len(result['SecondaryLeaks']), len(result['Orphans']))
    print('missing accessories', [(r['Item'],r['Stage']) for r in result['Accessories'] if r['Earliest'] is None or r['Earliest'] > r['Stage']])
    print('slow sources', [(r['Item'],r['Stage'],round(r['Chance'],6),r['Source']) for r in result['SlowSources'][:40]])
