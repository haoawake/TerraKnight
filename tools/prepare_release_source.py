"""Package reviewed source changes for checksum-verified publication by GitHub Actions."""
import hashlib
import json
import subprocess
import zipfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def main():
    changed = subprocess.check_output(['git', 'diff', '--name-only', '-z'], cwd=ROOT).decode('utf-8').split('\0')
    files = {p for p in changed if p and p != 'wiki-source.json' and not p.startswith('.github/')}
    # Include new production sources and the Wiki pipeline; unrelated local reports are excluded.
    for prefix in ('src/TerraKnight', 'wiki', 'tools/vanilla-data/combat'):
        for p in (ROOT / prefix).rglob('*'):
            if p.is_file() and not any(part in ('bin', 'obj', '__pycache__') for part in p.parts):
                if prefix != 'src/TerraKnight' or p.suffix == '.cs':
                    files.add(p.relative_to(ROOT).as_posix())
    for pattern in ('tools/verify-*.ps1',):
        files.update(p.relative_to(ROOT).as_posix() for p in ROOT.glob(pattern))
    files.update(['tools/generate_encyclopedia.py', 'tools/refresh_wiki_snapshots.py',
                  'tools/encyclopedia_enemy_health.json', 'tools/encyclopedia_health_observations.json',
                  'tools/audit_magic_mana.py', 'tools/prepare_release_source.py', 'tools/export-wiki-isolated.ps1',
                  'tools/test-single-drop-copy.ps1', 'tools/bt_wiki.py', 'tools/bt_release_asset.py',
                  'tools/generate_weapon_baseline.py', 'docs/magic-mana-audit-4.1.0.md',
                  'docs/release-v4.1.0.md', '泰拉圣巢本地百科全书.md'])
    # Preserve any additional source documents linked from the generated Wiki.
    source_root = ROOT / 'artifacts/wiki-preview/terraknight-wiki/sources'
    for p in source_root.rglob('*'):
        if p.is_file():
            name = p.relative_to(source_root).as_posix()
            if name.endswith('.cs.txt'):
                name = name[:-4]
            files.add(name)
    files = sorted(files)
    for name in files:
        path = ROOT / name
        assert path.is_file() and path.resolve().is_relative_to(ROOT.resolve()), name
        assert not name.startswith(('artifacts/', 'release/', '.git/')) and path.suffix not in ('.dll', '.exe', '.pdb'), name
    manifest = {name: hashlib.sha256((ROOT / name).read_bytes()).hexdigest() for name in files}
    target = ROOT / 'release/TerraKnight-v4.1.0-source.zip'
    with zipfile.ZipFile(target, 'w', zipfile.ZIP_DEFLATED) as z:
        z.writestr('__manifest__.json', json.dumps(manifest, ensure_ascii=False))
        for name in files:
            z.write(ROOT / name, name)
    print(json.dumps({'files': len(files), 'bytes': target.stat().st_size, 'sha256': hashlib.sha256(target.read_bytes()).hexdigest()}))


if __name__ == '__main__':
    main()
