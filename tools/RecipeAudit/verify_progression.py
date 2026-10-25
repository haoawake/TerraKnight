"""Check source closure of the actual staged game tables and retain an auditable report."""
import argparse
import collections
import json
from pathlib import Path
from verify_vanilla_recipes import validate


def audit(data):
    items, recipes = data['items'], data['recipes']
    stages = []
    for stage in data['stages']:
        s = stage['Stage']
        sources = {t: ['basic stock'] for t, d in items.items() if d['Source'] == 'Basic' and d['Stage'] <= s}
        for shop in stage['Shops']:
            sources.setdefault(shop['Type'], []).append('shop:' + shop['Npc'])
        for record, rows in stage['Drops'].items():
            for row in rows:
                sources.setdefault(row['Type'], []).append(f"drop:{record} p={row['Chance']:.5f}")
        for category in ('Fishing', 'Crates', 'Bait', 'Quests', 'WorldSources', 'HabitatSources'):
            for t in stage.get(category, []):
                if t in items and items[t]['Stage'] <= s:
                    sources.setdefault(t, []).append(category)
        leaks = sorted(t for t in sources if t in items and items[t]['Stage'] > s)
        reach = {t for t in sources if t in items and items[t]['Stage'] <= s}
        eligible = [r for r in recipes if r['StationStage'] <= s]
        crafted = set()
        for _ in range(len(recipes)):
            old = len(reach)
            for r in eligible:
                if all(i['Type'] in reach for i in r['Ingredients']):
                    crafted.add(r['Result'])
                    reach.add(r['Result'])
            if old == len(reach):
                break
        missing = [r for r in eligible if r['Result'] not in crafted]
        blockers = collections.Counter(i['Type'] for r in missing for i in r['Ingredients'] if i['Type'] not in reach)
        stages.append(dict(stage=s, source_count=len(sources), reachable_count=len(reach),
                           craftable_results=len(crafted), eligible_recipes=len(eligible),
                           missing_results=sorted({r['Result'] for r in missing}), blockers=dict(blockers.most_common()),
                           future_source_leaks=leaks, sources=sources))
    return dict(vanilla=validate(data), accessories_checked=data['accessoriesChecked'], stages=stages,
                boss_material_checks=data.get('bossMaterialChecks', []),
                combat=data.get('combat', []))


if __name__ == '__main__':
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument('runtime', type=Path)
    ap.add_argument('--output', type=Path, required=True)
    args = ap.parse_args()
    result = audit(json.loads(args.runtime.read_text('utf-8-sig')))
    args.output.write_text(json.dumps(result, ensure_ascii=False, indent=2), 'utf-8')
    print('Vanilla mismatches:', len(result['vanilla']['failures']))
    for s in result['stages']:
        print(f"Stage {s['stage']}: craftable={s['craftable_results']}; missing={len(s['missing_results'])}; leaks={len(s['future_source_leaks'])}")
        if s['missing_results']:
            print('  Results:', ', '.join(s['missing_results']))
            print('  Blockers:', s['blockers'])
    raise SystemExit(bool(result['vanilla']['failures'] or any(s['missing_results'] or s['future_source_leaks'] for s in result['stages'])
                          or any(not x['Pass'] for x in result['boss_material_checks'])))
