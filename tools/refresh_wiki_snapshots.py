"""Import successful isolated exports, rejecting source changes since the plugin build."""
import argparse
import hashlib
import json
import os
from pathlib import Path

ROOT = Path(os.environ.get('TK_WIKI_SOURCE_ROOT', Path(__file__).resolve().parents[1])).resolve()
SRC = ROOT/'src/TerraKnight'

def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))

def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('assets_run', type=Path)
    p.add_argument('health_run', type=Path)
    p.add_argument('--build-hashes', type=Path, required=True)
    p.add_argument('--progression-run', type=Path)
    args = p.parse_args()
    hashes = read(args.build_hashes)
    for run in (args.assets_run,args.health_run,*([args.progression_run] if args.progression_run else [])):
        result = read(run/'result.json')
        assert result['Completed'] and not result['TimedOut'] and not result['Failures']
        isolated_copy = result.get('IndependentInstallation') is True and result.get('SharedSettingsRestored') is False
        assert (result.get('RestoredSettings') is True or isolated_copy), 'Neither restored shared settings nor an independent installation'
        assert not result['ChangedPlayerSaves'] and not result['NewPlayerSaves']
    runtime = read(ROOT/'tools/encyclopedia_runtime.json')
    health = read(ROOT/'tools/encyclopedia_enemy_health.json')
    for rel in hashes:
        assert hashlib.sha256((SRC/rel).read_bytes()).hexdigest()==hashes[rel], 'Sources changed since build: '+rel
    runtime['source_hashes'] = hashes
    runtime['data'] = read(args.assets_run/'saves/wiki-assets/runtime-data.json')
    if args.progression_run:
        audit = read(args.progression_run/'saves/progression-audit.json')
        runtime['data']['shop_stages'] = {str(s['Stage']):s['Shops'] for s in audit['stages']}
        assert set(runtime['data']['shop_stages']) == set(map(str, range(6)))
    health['source_hashes'] = {rel:hashes[rel] for rel in health['source_hashes']}
    health['records'] = read(args.health_run/'saves/encyclopedia-enemy-health.json')
    for name,data in [('encyclopedia_runtime.json',runtime),('encyclopedia_enemy_health.json',health)]:
        (ROOT/'tools'/name).write_text(json.dumps(data,ensure_ascii=False,indent=2),encoding='utf-8')
    parts = args.health_run/'saves/encyclopedia-health-parts.json'
    if parts.is_file():
        observed = read(ROOT/'tools/encyclopedia_health_observations.json')
        deps = set(observed['source_hashes']) | {'Items/EnemyDrops.cs', 'Items/EnemyDropData.cs'}
        observed['source_hashes'] = {rel:hashes[rel] for rel in sorted(deps)}
        observed['scenes'] = read(parts)
        (ROOT/'tools/encyclopedia_health_observations.json').write_text(json.dumps(observed,ensure_ascii=False,indent=2),encoding='utf-8')
    print('Imported verified isolated exports; sources match the built plugin.')

if __name__=='__main__':
    main()
