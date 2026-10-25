"""Audit recorded ownership and combat coverage; a completed fixture is not a win."""
import argparse
import collections
import json
from pathlib import Path
import re

p = argparse.ArgumentParser(description=__doc__)
p.add_argument('run', type=Path)
p.add_argument('--audit', type=Path)
p.add_argument('--acquisition', type=Path)
p.add_argument('--retake', type=Path, action='append', default=[])
p.add_argument('--allow-running', action='store_true')
a = p.parse_args()
read = lambda f: json.loads(f.read_text(encoding='utf-8-sig'))
root = Path(__file__).resolve().parents[1]
enum = (root/'artifacts/novice-audit-source/src/TerraKnight/Items/Items.cs').read_text(encoding='utf-8-sig')
enum = enum.split('enum ItemType', 1)[1].split('{', 1)[1].split('}', 1)[0]
enum = re.sub(r'//[^\n]*|/\*.*?\*/', '', enum, flags=re.S)
types = [t.strip() for t in enum.split(',') if t.strip()]
assert all(re.fullmatch(r'\w+', t) for t in types)
name = lambda t: types[t] if isinstance(t, int) else str(t)
data = read((a.acquisition or a.run)/'saves/novice-acquisition.json')
if a.acquisition:
    previous = read(a.run/'saves/novice-acquisition.json')
    key = lambda r:(r['Difficulty'],r['Class'],r['Stage'],r['Seed'])
    old = {key(r):r for r in previous['Rows']}
    unchanged = [r for r in data['Rows'] if r['Class'] != 0]
    assert len(unchanged) == 576
    assert all(r == old[key(r)] for r in unchanged), 'non-melee acquisition changed; old battles cannot be reused'
assert data['Failures'] == 0 and len(data['Rows']) == 768
keys = {(r['Difficulty'], r['Class'], r['Stage'], r['Seed']) for r in data['Rows']}
assert len(keys) == 768
for r in data['Rows']:
    assert r['Earned'] - r['Spent'] == r['Cash'] >= 0
    assert all(v >= 0 for v in r['Stock'].values())
    gear = r['ArmorNames'] + [r['WeaponInfo']['Type']] + [name(t) for t in r['Accessories']]
    assert all(r['Stock'].get(t, 0) > 0 for t in gear if t != 'None'), (r['Difficulty'], r['Class'], r['Stage'], r['Seed'], gear)
    assert len(r['Accessories']) == len(set(r['Accessories']))
    assert len(r['Accessories']) <= (6 if r['Difficulty'] == 'Master' else 5)
    assert all(r['Stock'].get(t, 0) > 0 for t in r['BuffItems'] + r['SummonAlternatives'])
b = a.run/'saves/novice-bosses.json'
results = read(b)['Results'] if b.exists() else []
if b.exists():
    battles = read(b)
    assert battles['RealAI'] and not battles['Immortal'] and not battles['DirectDamage']
retaken_keys = set()
for retake in a.retake:
    rs = read(retake/'result.json')
    assert rs['Completed'] and not rs['TimedOut'] and not rs['Failures'] and rs['RestoredSettings']
    assert not rs['ChangedPlayerSaves'] and not rs['NewPlayerSaves']
    replacement = read(retake/'saves/novice-bosses.json')
    assert replacement['RealAI'] and not replacement['Immortal'] and not replacement['DirectDamage']
    key = lambda r:(r['Difficulty'],r['Class'],r['GearStage'],r['Boss'])
    updated = {key(r):r for r in replacement['Results']}
    retaken_keys.update(updated)
    results = [updated.pop(key(r),r) for r in results] + list(updated.values())
stations = {'SharpeningStation':159, 'AmmoBox':93, 'CrystalBall':29, 'BewitchingTable':150, 'SliceOfCake':192, 'WarTable':348}
missing = []
for r in results:
    for station in r['BuffStations']:
        if station in stations and stations[station] not in r['StartBuffIds']:
            missing.append([r['Difficulty'], r['Class'], r['GearStage'], station])
if not a.allow_running:
    status = read(a.run/'result.json')
    assert status['Completed'] and not status['TimedOut'] and not status['Failures']
    assert status['RestoredSettings'] and not status['ChangedPlayerSaves'] and not status['NewPlayerSaves']
    assert len(results) == 48
    if a.acquisition:
        status = read(a.acquisition/'result.json')
        assert status['Completed'] and not status['TimedOut'] and not status['Failures'] and status['RestoredSettings']
        assert not status['ChangedPlayerSaves'] and not status['NewPlayerSaves']
        assert all((r['Difficulty'],r['Class'],r['GearStage'],r['Boss']) in retaken_keys for r in results if r['Class']==0)
    profiles={(r['Difficulty'],r['Class'],r['Stage']):r for r in data['Rows'] if r['Seed']==0}
    for r in results:
        profile=profiles[(r['Difficulty'],r['Class'],r['GearStage'])]
        assert r['Weapon']==profile['WeaponInfo']['Type'] and r['Forge']==profile['WeaponInfo']['Forge']
        assert r['Armor']==profile['Armor'] and r['Accessories']==profile['Accessories']
if a.audit:
    audit = read(a.audit/'saves/novice-audit.json')
    assert audit['Failures'] == 0
    by_type = {r['Type']:r for r in audit['Items']}
    early = collections.Counter()
    for r in data['Rows']:
        for action in r['Actions']:
            if action.startswith('craft:'):
                t = action[6:].split('*')[0]
                if by_type[t]['Stage'] > r['Stage']:
                    early[(r['Stage'], t, by_type[t]['Stage'])] += 1
else:
    early = {}
combat_seen=lambda r:bool(r['PhasesSeen']) if 'PhasesSeen' in r else r['Seen']
summary = dict(AcquisitionChecks=data['Checks'], OwnershipRows=768,
    NonMeleeAcquisitionUnchanged=576 if a.acquisition else None,RetakenFights=len(retaken_keys),
    CompletedFights=len(results), ConfirmedKills=sum(r['Won'] for r in results),
    Unready=[[r['Difficulty'],r['Class'],r['GearStage']] for r in results if not r['Awake'] or not combat_seen(r) and not r.get('WaveSeen')],
    NotReachedBoss=[[r['Difficulty'],r['Class'],r['GearStage']] for r in results if not combat_seen(r)],
    WaveOnly=[[r['Difficulty'],r['Class'],r['GearStage']] for r in results if r.get('WaveSeen') and not combat_seen(r)],
    MissingStationBuffs=missing,
    CraftsBeforeMetadataStage=[dict(Stage=k[0],Type=k[1],MetadataStage=k[2],Samples=v) for k,v in early.items()])
(a.run/'novice-evidence-audit.json').write_text(json.dumps(summary, ensure_ascii=False, indent=2), encoding='utf-8')
print(json.dumps(summary, ensure_ascii=False))
