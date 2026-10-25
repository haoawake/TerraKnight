"""Validate final novice evidence and generate its single Chinese report."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
import sys

p = argparse.ArgumentParser(description=__doc__)
p.add_argument('--acquisition', required=True, type=Path)
p.add_argument('--audit', required=True, type=Path)
p.add_argument('--retake', required=True, type=Path, action='append')
p.add_argument('--controller-build', type=Path, default=Path('artifacts/novice-validated-build'))
p.add_argument('--controller-source', type=Path, default=Path('artifacts/novice-validated-source'))
a = p.parse_args()
root = Path(__file__).resolve().parents[1]
main = Path('artifacts/resume-noviceall-20261017-151535')
read = lambda f: json.loads(f.read_text(encoding='utf-8-sig'))
sha = lambda f: hashlib.sha256(f.read_bytes()).hexdigest()
for run in (a.acquisition, a.audit, *a.retake):
    r = read(run/'result.json')
    assert r['Completed'] and not r['TimedOut'] and not r['Failures'], run
    assert r['RestoredSettings'] and not r['ChangedPlayerSaves'] and not r['NewPlayerSaves'], run
    log = (run/'Player.log').read_bytes()
    finish = log.rfind(b'[AutoTest]')
    exceptions = [line for line in log[:finish].splitlines() if b'Exception:' in line]
    assert not exceptions, (run, exceptions)
assert any(any('Phase2' in name for name in row.get('PhasesSeen',[]))
    for attempt in a.retake for row in read(attempt/'saves/novice-bosses.json')['Results'] if row['Boss']=='MageLord'), 'Soul Master phase two has no combat coverage'
build = Path('artifacts/novice-validated-build')
source = Path('artifacts/novice-validated-source')
assert read(a.audit/'saves/loot-balance-runtime.json')['pluginSha256'].lower() == sha(build/'TerraKnight.dll'), 'audit plugin hash differs'
for f, expected in read(build/'source-hashes.json').items():
    assert sha(source/f) == expected, f
for f, expected in read(a.controller_build/'source-hashes.json').items():
    assert sha(a.controller_source/f) == expected, f
    if not Path(f).name.startswith('DevAutoTest'):
        assert (source/f).read_bytes() == (a.controller_source/f).read_bytes(), ('production drift', f)
installed = Path('F:/SteamLibrary/steamapps/common/Hollow Knight/BepInEx/plugins/TerraKnight/TerraKnight.dll')
assert sha(installed) == sha(a.controller_build/'TerraKnight.dll'), 'installed DLL differs'
for f in ('Items/WeaponBalance.cs', 'Town/TownShopExtra.cs'):
    assert (root/'src/TerraKnight'/f).read_bytes() == (source/'src/TerraKnight'/f).read_bytes(), f

def run(*args):
    subprocess.run([sys.executable, *map(str, args)], cwd=root, check=True)

run('tools/RecipeAudit/verify_vanilla_recipes.py', a.audit/'saves/loot-balance-runtime.json',
    '--output', a.audit/'vanilla-recipe-comparison.json')
retake_args=[v for attempt in a.retake for v in ('--retake',attempt)]
run('tools/validate_novice_evidence.py', main, '--acquisition', a.acquisition,
    '--audit', a.audit, *retake_args)
run('tools/build_novice_balance_report.py', main, '--acquisition', a.acquisition,
    '--audit', a.audit, *retake_args,
    '--before-audit', 'artifacts/resume-noviceaudit-20261017-161815')
summary = read(main/'novice-evidence-audit.json')
provenance = dict(Main=str(main), Acquisition=str(a.acquisition), Audit=str(a.audit), Retakes=list(map(str,a.retake)),
    PluginSha256=sha(build/'TerraKnight.dll'), SourceManifest=str(build/'source-hashes.json'),
    ControllerPluginSha256=sha(a.controller_build/'TerraKnight.dll'), ControllerManifest=str(a.controller_build/'source-hashes.json'),
    Summary=summary, RecipeComparison=read(a.audit/'vanilla-recipe-comparison.json'))
(build/'validated-evidence.json').write_text(json.dumps(provenance, ensure_ascii=False, indent=2), encoding='utf-8')
print('Novice report and provenance validated.')
