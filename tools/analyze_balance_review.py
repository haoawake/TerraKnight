"""Summarize a normal-acquisition, isolated runtime balance export."""
import collections
import json
import pathlib
import sys

run = pathlib.Path(sys.argv[1])
data = json.loads((run / 'saves/balance-review.json').read_text(encoding='utf-8-sig'))
assert data['FreeMaking'] is False, 'Creative/free-making samples are not normal acquisition evidence'
items = {row['Item']: row for row in data['Items']}
over = []
for crate in data['Crates']:
    for name, count in crate['Counts'].items():
        item = items[name]
        if item['Stage'] > crate['Stage']:
            over.append(dict(Stage=crate['Stage'], Crate=crate['Crate'], Item=name,
                             ItemStage=item['Stage'], Count=count))

stages = []
for stage in range(6):
    own = [r for r in data['World'] if r['Stage'] == stage and not (r['Scene'] or '').startswith('GG_')]
    god = next(r for r in data['Godhome'] if r['Stage'] == stage and r['Key'] == 'BigFly' and r['Profile'] == 'attuned')
    stages.append(dict(Stage=stage, WorldBossDefense=dict(collections.Counter(r['Defense'] for r in own)),
                       GruzGodhome=god))

summary = dict(
    Source=str(run), CrateSamples=sum(r['Samples'] for r in data['Crates']),
    FishingChecks=sum(r['Checks'] for r in data['Catches']), OverStage=over,
    Stages=stages,
    ExampleCrates=[r for r in data['Crates'] if r['Crate'] in ('CrateCrossroads', 'CofferCrossroads', 'WoodenCrate', 'IronCrate', 'GoldenCrate')],
    ExampleCatches=[r for r in data['Catches'] if r['Stage'] == 0 and r['Area'] == '苍绿之径'],
    GodhomeExamples=[r for r in data['Godhome'] if r['Stage'] == 5 and r['Key'] in ('FalseKnight','BigFly','FinalBoss')],
)
(run / 'balance-summary.json').write_text(json.dumps(summary, ensure_ascii=False, indent=2), encoding='utf-8')
print(json.dumps({k:v for k,v in summary.items() if k not in ('ExampleCrates','ExampleCatches','GodhomeExamples')}, ensure_ascii=False, indent=2))
print('CRATES', [(r['Stage'],r['Crate'],round(r['Geo'],2),r['Gear'],r['Crystals']) for r in summary['ExampleCrates']])
print('CATCHES', [(r['Power'],r['Buff'],r['Crates']/r['Checks']) for r in summary['ExampleCatches']])
