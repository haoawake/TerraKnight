"""Packs a release from the mod's build, BepInEx 5 and the launcher:

- release/TerraKnight-v<version>-Setup.exe: the one-file installer (tools/installer). Run it: it finds Hollow Knight and
  Terraria in the Steam libraries on any drive (or lets the player pick them), puts BepInEx, the mod and the launcher
  into the game folder, points the mod at Terraria, and leaves 空洞泰拉.bat (and a desktop shortcut) to play; the
  launcher's script goes into BepInEx\TerraKnight.
- release/TerraKnight-v<version>.zip: the same files laid out like the game folder, for a manual install (unzip into
  the game folder), with 安装说明.txt and Install (English).txt.

BepInEx comes from an installed copy (--bepinex, by default the Hollow Knight folder in LocalPaths.props): its core
folder, winhttp.dll, .doorstop_version, changelog.txt, and doorstop_config.ini switched off (the launcher switches it
on for the modded start only). The build goes to a staging folder, never into the game's own plugin folder.

    python tools/make_release.py [--bepinex <Hollow Knight folder with BepInEx>] [--dll <a built TerraKnight.dll>]
"""
import argparse, os, re, shutil, subprocess, sys, zipfile
from pathlib import Path

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
REPO = 'https://github.com/haoawake/TerraKnight'

NOTES = """空洞泰拉 TerraKnight v{version}

推荐：运行 TerraKnight-v{version}-Setup.exe 一键安装（会自动在各个盘的 Steam 库里找到空洞骑士和泰拉瑞亚）。

手动安装（这个压缩包）：
1. 把压缩包里的所有东西解压到空洞骑士的游戏目录（hollow_knight.exe 所在的文件夹）。里面已经带了 BepInEx 5 框架，
   不用另外安装。
2. 电脑上要装着泰拉瑞亚（Steam 版 1.4.x）：mod 直接读本机泰拉瑞亚的贴图、音效和文字，不随 mod 打包。
   在同一个 Steam 库里会自动找到；不在同一个库（比如另一个盘）时，在 BepInEx\\config\\com.haoawake.terraknight.cfg
   的 [Paths] 下写 TerrariaDir = 泰拉瑞亚的游戏目录。
3. 双击游戏目录里的 空洞泰拉.bat 开始玩。从 Steam 直接启动是原版空洞骑士。

完整说明：{repo}
"""

NOTES_EN = """TerraKnight v{version}

Recommended: run TerraKnight-v{version}-Setup.exe to install in one click (it finds Hollow Knight and Terraria in the
Steam libraries on every drive).

Manual install (this archive):
1. Unzip everything in this archive into Hollow Knight's game folder (the folder with hollow_knight.exe). The BepInEx 5
   framework is included; there is nothing else to install.
2. Terraria (Steam, 1.4.x) must be installed on the same computer: the mod reads its textures, sounds and text from your
   Terraria, and none of them are packed with the mod. Terraria in the same Steam library is found automatically;
   otherwise (another drive, say) write TerrariaDir = <Terraria's folder> under [Paths] in
   BepInEx\\config\\com.haoawake.terraknight.cfg.
3. Double-click 空洞泰拉.bat in the game folder to play. Starting the game from Steam runs vanilla Hollow Knight.

Full documentation (English): {repo}/blob/main/README.en.md
"""

# BepInEx's own files at the game folder's root (besides its BepInEx\core folder).
BEPINEX_ROOT_FILES = ('winhttp.dll', '.doorstop_version', 'changelog.txt')


def local_hollow_knight():
    props = Path(ROOT) / 'LocalPaths.props'
    if props.is_file():
        m = re.search(r'<HollowKnightDir>([^<]+)</HollowKnightDir>', props.read_text(encoding='utf-8'))
        if m:
            return Path(m.group(1).strip())
    return None


def stage_bepinex(source: Path, stage: Path):
    core = source / 'BepInEx' / 'core'
    assert (core / 'BepInEx.Preloader.dll').is_file(), f'No BepInEx in {source}'
    shutil.copytree(core, stage / 'BepInEx' / 'core')
    for name in BEPINEX_ROOT_FILES:
        assert (source / name).is_file(), f'Missing BepInEx file {name} in {source}'
        shutil.copy2(source / name, stage / name)
    ini = (source / 'doorstop_config.ini').read_text(encoding='utf-8')
    ini = re.sub(r'(?m)^enabled\s*=.*$', 'enabled = false', ini)
    (stage / 'doorstop_config.ini').write_text(ini, encoding='utf-8', newline='\r\n')
    for d in ('plugins', 'config', 'patchers'):
        (stage / 'BepInEx' / d).mkdir(parents=True, exist_ok=True)


def zip_dir(folder: Path, zip_path: Path, skip=()):
    if zip_path.exists():
        zip_path.unlink()
    with zipfile.ZipFile(zip_path, 'w', zipfile.ZIP_DEFLATED) as z:
        for dirpath, dirs, files in os.walk(folder):
            rel_dir = os.path.relpath(dirpath, folder)
            if not files and not dirs:
                z.writestr(rel_dir.replace(os.sep, '/') + '/', '')
            for f in files:
                if f in skip:
                    continue
                full = os.path.join(dirpath, f)
                z.write(full, os.path.relpath(full, folder))


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument('--atlas', type=Path, help='Reviewed native world atlas to include in this package')
    parser.add_argument('--bepinex', type=Path, default=local_hollow_knight(), help='A Hollow Knight folder with BepInEx 5 installed')
    parser.add_argument('--dll', type=Path, help='An already built TerraKnight.dll (skips the build)')
    args = parser.parse_args()
    assert args.bepinex, 'Where is BepInEx? Pass --bepinex <Hollow Knight folder>'
    plugin = open(os.path.join(ROOT, 'src', 'TerraKnight', 'Plugin.cs'), encoding='utf-8').read()
    version = re.search(r'Version\s*=\s*"([^"]+)"', plugin).group(1)
    name = f'TerraKnight-v{version}'
    release = Path(ROOT) / 'release'
    stage = release / name
    assert stage.resolve().parent == release.resolve(), 'Invalid release staging directory'
    shutil.rmtree(stage, ignore_errors=True)
    stage.mkdir(parents=True)

    stage_bepinex(args.bepinex, stage)
    plugin_dir = stage / 'BepInEx' / 'plugins' / 'TerraKnight'
    plugin_dir.mkdir(parents=True, exist_ok=True)
    if args.dll:
        shutil.copy2(args.dll, plugin_dir / 'TerraKnight.dll')
    else:
        build_dir = os.path.join(ROOT, 'artifacts', f'release-v{version}', 'build')
        r = subprocess.run(['dotnet', 'build', os.path.join(ROOT, 'src', 'TerraKnight'), '-c', 'Release', '-p:SkipPluginInstall=true', '-o', build_dir])
        if r.returncode != 0:
            sys.exit('build failed')
        shutil.copy2(os.path.join(build_dir, 'TerraKnight.dll'), plugin_dir)
    # The game folder keeps only the launcher's .bat; its script goes with the mod's own files.
    shutil.copy2(os.path.join(ROOT, 'launcher', '空洞泰拉.bat'), stage)
    (stage / 'BepInEx' / 'TerraKnight').mkdir(parents=True, exist_ok=True)
    shutil.copy2(os.path.join(ROOT, 'launcher', 'terraknight-launch.ps1'), stage / 'BepInEx' / 'TerraKnight')
    if args.atlas:
        assert args.atlas.is_file() and args.atlas.stat().st_size > 100, 'Missing reviewed atlas'
        atlas_dir = stage / 'BepInEx' / 'TerraKnight'
        atlas_dir.mkdir(parents=True, exist_ok=True)
        shutil.copy2(args.atlas, atlas_dir / 'worldatlas.bin')

    # The installer's payload: the game folder's files, without the readmes.
    payload = Path(ROOT) / 'artifacts' / f'release-v{version}' / 'payload.zip'
    payload.parent.mkdir(parents=True, exist_ok=True)
    zip_dir(stage, payload)
    installer_out = Path(ROOT) / 'artifacts' / f'release-v{version}' / 'installer'
    r = subprocess.run(['dotnet', 'build', os.path.join(ROOT, 'tools', 'installer'), '-c', 'Release',
                        f'-p:PayloadZip={payload}', f'-p:InstallerVersion={version}', '-o', str(installer_out)])
    if r.returncode != 0:
        sys.exit('installer build failed')
    setup = release / f'{name}-Setup.exe'
    shutil.copy2(installer_out / 'TerraKnightSetup.exe', setup)
    print('wrote', setup, setup.stat().st_size, 'bytes')

    with open(stage / '安装说明.txt', 'w', encoding='utf-8-sig', newline='\r\n') as f:
        f.write(NOTES.format(version=version, repo=REPO))
    with open(stage / 'Install (English).txt', 'w', encoding='utf-8-sig', newline='\r\n') as f:
        f.write(NOTES_EN.format(version=version, repo=REPO))
    zip_path = release / (name + '.zip')
    zip_dir(stage, zip_path)
    print('wrote', zip_path)
    with zipfile.ZipFile(zip_path) as z:
        for i in z.infolist():
            print(f'  {i.filename}  {i.file_size} bytes')


if __name__ == '__main__':
    main()
