"""Stage and inspect an unpublished candidate without touching release/ or the installation."""
from pathlib import Path
import hashlib
import json
import re
import shutil
import subprocess
import zipfile
import argparse
from make_release import stage_bepinex, zip_dir, local_hollow_knight, NOTES, NOTES_EN, REPO

ROOT = Path(__file__).resolve().parents[1]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', type=Path, default=ROOT / 'artifacts/prerelease-package')
    parser.add_argument('--build', type=Path, default=ROOT / 'artifacts/prerelease-build/TerraKnight.dll')
    args = parser.parse_args()
    base = args.output.resolve()
    stage = base / 'stage'
    if stage.exists():
        raise SystemExit('Candidate stage already exists; preserve it and choose a fresh location.')
    stage.mkdir(parents=True)
    build = args.build.resolve()
    source = (ROOT / 'src/TerraKnight/Plugin.cs').read_text(encoding='utf-8')
    version = re.search(r'const string Version = "([^"]+)"', source)[1]
    changelog = re.search(r'^## (\d+\.\d+\.\d+)', (ROOT / 'docs/CHANGELOG.md').read_text(encoding='utf-8'), re.M)[1]
    stage_bepinex(local_hollow_knight(), stage)
    plugin = stage / 'BepInEx/plugins/TerraKnight'
    plugin.mkdir(parents=True, exist_ok=True)
    shutil.copy2(build, plugin / 'TerraKnight.dll')
    shutil.copy2(ROOT / 'launcher/空洞泰拉.bat', stage)
    scripts = stage / 'BepInEx/TerraKnight'
    scripts.mkdir(parents=True)
    shutil.copy2(ROOT / 'launcher/terraknight-launch.ps1', scripts)
    (stage / '安装说明.txt').write_text(NOTES.format(version=version, repo=REPO), encoding='utf-8-sig')
    (stage / 'Install (English).txt').write_text(NOTES_EN.format(version=version, repo=REPO), encoding='utf-8-sig')
    target = base / 'candidate.zip'
    zip_dir(stage, target)
    sha = lambda p: hashlib.sha256(p.read_bytes()).hexdigest()
    failures = []
    with zipfile.ZipFile(target) as archive:
        assert archive.testzip() is None
        dll = archive.read('BepInEx/plugins/TerraKnight/TerraKnight.dll')
        assert hashlib.sha256(dll).hexdigest() == sha(build)
        assert re.search(r'^enabled\s*=\s*false\s*$', archive.read('doorstop_config.ini').decode('utf-8-sig'), re.M)
        names = archive.namelist()
        assert not any(re.search(r'(^|/)(user\d|TerraKnight_slot|Player\.log|LogOutput\.log)', n) for n in names)
        assert not any(n.endswith(('.pdb', '.cfg')) for n in names)
    if version != changelog:
        failures.append(f'Plugin version {version} differs from latest changelog {changelog}')
    manifest = {str(p.relative_to(ROOT)).replace('\\','/'): sha(p) for p in sorted((ROOT/'src/TerraKnight').rglob('*'))
                if p.is_file() and not {'bin','obj'}.intersection(p.parts) and p.suffix in {'.cs','.csproj','.png','.json','.wav','.shader'}}
    (base/'source-manifest.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding='utf-8')
    dirty = bool(subprocess.check_output(['git','status','--porcelain'], cwd=ROOT, text=True).strip())
    result = dict(Version=version, ChangelogVersion=changelog, SourceHead=subprocess.check_output(['git','rev-parse','HEAD'], cwd=ROOT, text=True).strip(), WorkingTreeDirty=dirty,
                  SourceTreeHash=hashlib.sha256(json.dumps(manifest, sort_keys=True).encode()).hexdigest(),
                  BuildHash=sha(build), ZipHash=sha(target), Entries=len(names), ZipIntegrity=True, BuildParity=True,
                  VanillaDefault=True, NoSavesOrDevConfig=True, ReleaseBlockers=failures, Published=False)
    (base / 'result.json').write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding='utf-8')
    print(json.dumps(result, ensure_ascii=False, indent=2))


if __name__ == '__main__':
    main()
