"""Check live exported False Knight parts against the pre-change runtime snapshot."""
import argparse
import json
import math
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]

def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('run', type=Path)
    args = parser.parse_args()
    before = read(ROOT/'artifacts/wiki-sync-source-20261022/tools/encyclopedia_health_observations.json')['scenes']
    after = read(args.run/'saves/encyclopedia-health-parts.json')
    checks = []
    for scene, body, ratio in [('Crossroads_10_boss','False Knight New',1.5),('Dream_01_False_Knight','False Knight Dream',1.)]:
        old_scene = next(s for s in before if s['scene']==scene)
        new_scene = next(s for s in after if s['scene']==scene)
        for part, expected in [(body,ratio),(body+'/Head',1.)]:
            old = next(e for e in old_scene['entries'] if e['path'].endswith(part))
            new = next(e for e in new_scene['entries'] if e['path'].endswith(part))
            assert old['max_hp']==new['max_hp'], (scene,part,'native hp')
            for old_level,new_level in zip(old['levels'],new['levels']):
                assert old_level['difficulty']==new_level['difficulty']
                actual = new_level['scale']/old_level['scale']
                assert math.isclose(actual,expected,rel_tol=2e-6), (scene,part,actual,expected)
                checks.append(dict(scene=scene,part=part,difficulty=new_level['difficulty'],before=old_level['pool'],after=new_level['pool'],scale_ratio=actual))
    old_math = read(ROOT/'artifacts/wiki-sync-source-20261022/tools/encyclopedia_enemy_health.json')['records']
    new_math = read(args.run/'saves/encyclopedia-enemy-health.json')
    assert old_math.keys()==new_math.keys()
    assert [key for key in old_math if old_math[key]!=new_math[key]]==['FalseKnight'], 'Unrelated enemy stats changed'
    assert old_math['FalseKnight@5']==new_math['FalseKnight@5'], 'Failed Champion stats changed'
    for old,new in zip(old_math['FalseKnight']['difficulties'],new_math['FalseKnight']['difficulties']):
        assert new['life']>old['life']
        assert old['raw_budget']==new['raw_budget'], 'Base budget was redistributed'
        assert old['godhome']==new['godhome'], 'Godhome base budget was redistributed'
        assert old['defense']==new['defense']
        for a,b in zip(old['stage_stats'],new['stage_stats']):
            assert a['damage']==b['damage'] and a['defense']==b['defense']
    result=dict(status='passed',part_difficulty_checks=len(checks),checks=checks,
                failed_champion_unchanged=True,false_knight_attack_defense_unchanged=True,other_enemy_records_unchanged=len(old_math)-1)
    (ROOT/'artifacts/false-knight-armour-runtime-check.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps({k:v for k,v in result.items() if k!='checks'}))

if __name__=='__main__':
    main()
