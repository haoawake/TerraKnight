"""Summarize an exported normal-mode equipment source closure without changing game data."""
import argparse
import collections
import json
from pathlib import Path


def analyze(data):
    items = data['Items']
    previous = set()
    states = []
    families = collections.defaultdict(lambda: collections.defaultdict(list))
    for item, row in items.items():
        if row['IsArmor'] and row['Set'] != 'None':
            families[row['Set']][row['ArmorSlot']].append(item)
    last_sets = set()
    for state in data['Snapshots']:
        stage = state['Stage']
        reachable = set(state['Reachable'])
        fresh = reachable - previous
        classes = {}
        for cls in ('Melee', 'Ranged', 'Magic', 'Summon'):
            weapons = [t for t in reachable if items[t]['Balanced'] and items[t]['Class'] == cls]
            ranked = sorted(weapons, key=lambda t: items[t]['ModelDps'][stage], reverse=True)
            new = [t for t in ranked if t in fresh]
            no_ammo = [t for t in weapons if items[t]['UseAmmo'] != 'None'
                       and not any(items[a]['Ammo'] == items[t]['UseAmmo'] for a in reachable)]
            classes[cls] = dict(weapons=len(weapons), new=len(new), candidates=new,
                                model_top=ranked[:5], no_reachable_ammo=no_ammo)
        sets = {name for name, parts in families.items()
                if all(any(t in reachable for t in parts[slot]) for slot in ('Head', 'Body', 'Legs'))}
        states.append(dict(stage=stage, checkpoint=state['Label'], milestone=state['Milestone'],
                           classes=classes, new_armor_sets=sorted(sets-last_sets)))
        previous = reachable
        last_sets = sets
    exits = {s['Stage']: s for s in data['Snapshots']}
    checks = []
    for stage, state in exits.items():
        targets = [('weapon', t) for group in data['Core'][stage] for t in group]
        targets += [('accessory', t) for group in data['Accessories'][stage] for t in group]
        targets += [('armor', t) for path in data['ArmorPaths'] if path['Stage'] == stage for t in path['Items']]
        checks += [dict(stage=stage, kind=kind, item=t, reachable=t in state['Reachable']) for kind, t in targets]
    equivalent = all(all(items[a][key] == items[b][key] for key in ('Defense', 'Bonus', 'Set', 'SetBonus'))
                     for a, b in [('AncientCobaltHelmet', 'JungleHat'),
                                  ('AncientCobaltBreastplate', 'JungleShirt'),
                                  ('AncientCobaltLeggings', 'JunglePants')])
    return dict(target_count=len(checks), missing_targets=[c for c in checks if not c['reachable']],
                checks=checks, ancient_cobalt_equivalent_to_jungle=equivalent, states=states,
                limits=['Existential closure, not a full fresh-save playthrough.',
                        'NPC availability uses the stage stock table; travel/quests/crates are conditional or sampled.',
                        'All bosses of earlier bands are assumed cleared; same-step bosses are grouped.',
                        'No quantities, shop affordability, travel time, or drop-farming duration are proven by closure.',
                        'Weapon output is a source model; it does not prove practical combat performance.',
                        'Deliberate cross-stage exploration and earlier recipe access are not defects.'])


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('runtime', type=Path)
    parser.add_argument('output', type=Path)
    args = parser.parse_args()
    result = analyze(json.loads(args.runtime.read_text(encoding='utf-8-sig')))
    args.output.write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding='utf-8')
    print(f"Growth targets: {result['target_count']}; missing: {len(result['missing_targets'])}")
    print('Ancient cobalt equals jungle:', result['ancient_cobalt_equivalent_to_jungle'])
    for state in result['states']:
        if state['checkpoint'] == 'entry':
            print(state['stage'], {cls: row['new'] for cls, row in state['classes'].items()})
