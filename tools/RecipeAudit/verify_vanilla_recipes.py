"""Validate every vanilla-item recipe in a fresh game runtime export against Terraria data."""
import argparse
import collections
import json
import re
from pathlib import Path
from audit_crafting import TILES, expanded_ingredients

ROOT = Path(__file__).resolve().parents[2]
# Mod-only items may deliberately borrow a vanilla sprite. Containers.cs uses
# Item_3093 (Herb Bag) for AutoSellBag; that does not make its custom recipe vanilla.
# Liquid-adaptation items deliberately borrow vanilla icons but have mod recipes.
MOD_ONLY_TYPES = {'AutoSellBag', 'BottomlessWaterBucket', 'BottomlessAcidBucket', 'BottomlessShimmerBucket'}
TILES.update({'烹饪锅': 96, '篝火': 215, '酒桶': 94, '重型组装机': 283, '玻璃窑': 302,
              '蜂蜜分配机': 308, '生命木织机': 304, '天磨': 305})


def validate(mod):
    vanilla = json.loads((ROOT / 'tools/vanilla-data/crafting/vanilla-runtime.json').read_text('utf-8-sig'))
    assert vanilla['setupComplete']
    ids = {}
    items = mod['items']
    if isinstance(items, list):
        assert all(isinstance(d, dict) and 'Type' in d for d in items), 'invalid item export'
        assert len({d['Type'] for d in items}) == len(items), 'duplicate exported item types'
        entries = ((d['Type'], d) for d in items)
    else:
        assert isinstance(items, dict), 'invalid item catalogue'
        entries = items.items()
    for t, d in entries:
        if t in MOD_ONLY_TYPES:
            continue
        m = re.fullmatch(r'Item_(\d+)', d.get('Texture') or '')
        if m:
            ids[t] = int(m[1])
    by_result = collections.defaultdict(list)
    for r in vanilla['recipes']:
        by_result[r['result']].append(r)

    def matches(v, r):
        if v['count'] != r['Count'] or TILES.get(r['Station']) != (13 if v['tile'] == 355 else v['tile']):
            return False
        environment = next((e for flag, e in [('needWater', 'Water'), ('needLava', 'Lava'),
                           ('needHoney', 'Honey'), ('needGraveyardBiome', 'Graveyard')]
                            if flag in v['conditions']), 'None')
        if r.get('Environment', 'None') != environment:
            return False
        actual = collections.Counter()
        for ing in r['Ingredients']:
            if ing['Type'] not in ids:
                return False
            actual[ids[ing['Type']]] += ing['Count']

        def assign(slots, remaining):
            if not slots:
                return not any(remaining.values())
            choices, count = slots[0]
            for item in choices:
                if remaining[item] >= count:
                    rest = remaining.copy()
                    rest[item] -= count
                    if assign(slots[1:], rest):
                        return True
            return False
        return assign(expanded_ingredients(v, vanilla['groups']), actual)

    failures, custom, checked = [], [], 0
    for r in mod['recipes']:
        if r['Result'] not in ids:
            custom.append(r['Result'])
            continue
        checked += 1
        if not any(matches(v, r) for v in by_result[ids[r['Result']]]):
            failures.append(r)
    from recipe_variant_coverage import coverage
    variants = coverage(mod)
    return dict(checked=checked, custom=custom, failures=failures,
                supportedVariants=variants['ExpectedSupportedVariants'], missingVariants=variants['Missing'],
                unsupportedVariantRecords=variants['UnsupportedVanillaRecipes'])


if __name__ == '__main__':
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument('runtime', type=Path)
    ap.add_argument('--output', type=Path, required=True)
    args = ap.parse_args()
    result = validate(json.loads(args.runtime.read_text('utf-8-sig')))
    args.output.write_text(json.dumps(result, ensure_ascii=False, indent=2), 'utf-8')
    print(f"Checked={result['checked']}; custom={len(result['custom'])}; mismatches={len(result['failures'])}; missing supported variants={len(result['missingVariants'])}")
    for r in result['failures']:
        print(r['Result'], r['Count'], r['Station'], r['Ingredients'])
    raise SystemExit(bool(result['failures'] or result['missingVariants']))
