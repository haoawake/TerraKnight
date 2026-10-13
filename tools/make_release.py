"""Packs a release: release/TerraKnight-v<version>.zip, laid out like the game folder so it unzips
straight into it: BepInEx/plugins/TerraKnight/TerraKnight.dll, the launcher (泰拉圣巢.bat and
terraknight-launch.ps1) and 安装说明.txt. The version is Plugin.Version. The build goes to the
staging folder, never into the game's own plugin folder (so it is safe while the game is running).

    python tools/make_release.py
"""
import os, re, shutil, subprocess, sys, zipfile

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
REPO = 'https://github.com/haoawake/TerraKnight'

NOTES = """泰拉圣巢 TerraKnight v{version}

1. 先给空洞骑士装好 BepInEx 5.4.23（win x64）：https://github.com/BepInEx/BepInEx/releases
   把 BepInEx_win_x64_5.4.23.x.zip 解压到空洞骑士的游戏目录（hollow_knight.exe 所在的文件夹），
   从 Steam 启动一次游戏、到主菜单后退出，让 BepInEx 建好它的文件夹。
2. 把这个压缩包里的所有东西解压到同一个游戏目录（会放进 BepInEx\\plugins\\TerraKnight\\，
   以及游戏目录里的 泰拉圣巢.bat 和 terraknight-launch.ps1）。
3. 电脑上要装着泰拉瑞亚（Steam 版 1.4.x）：mod 直接读本机泰拉瑞亚的贴图、音效和文字，不随 mod 打包。
   在同一个 Steam 库里会自动找到；找不到时在 BepInEx\\config\\com.haoawake.terraknight.cfg 里填 TerrariaDir。
4. 双击游戏目录里的 泰拉圣巢.bat 开始玩。从 Steam 直接启动是原版空洞骑士。

完整说明：{repo}
"""


def main():
    plugin = open(os.path.join(ROOT, 'src', 'TerraKnight', 'Plugin.cs'), encoding='utf-8').read()
    version = re.search(r'Version\s*=\s*"([^"]+)"', plugin).group(1)
    name = f'TerraKnight-v{version}'
    stage = os.path.join(ROOT, 'release', name)
    shutil.rmtree(stage, ignore_errors=True)
    plugin_dir = os.path.join(stage, 'BepInEx', 'plugins', 'TerraKnight')
    os.makedirs(plugin_dir)
    r = subprocess.run(['dotnet', 'build', os.path.join(ROOT, 'src', 'TerraKnight'), '-c', 'Release', f'-p:PluginInstallDir={plugin_dir}'])
    if r.returncode != 0:
        sys.exit('build failed')
    for f in os.listdir(plugin_dir):
        if not f.endswith('.dll'):
            os.remove(os.path.join(plugin_dir, f))
    for f in ('泰拉圣巢.bat', 'terraknight-launch.ps1'):
        shutil.copy2(os.path.join(ROOT, 'launcher', f), stage)
    with open(os.path.join(stage, '安装说明.txt'), 'w', encoding='utf-8-sig', newline='\r\n') as f:
        f.write(NOTES.format(version=version, repo=REPO))
    zip_path = os.path.join(ROOT, 'release', name + '.zip')
    if os.path.exists(zip_path):
        os.remove(zip_path)
    with zipfile.ZipFile(zip_path, 'w', zipfile.ZIP_DEFLATED) as z:
        for dirpath, _, files in os.walk(stage):
            for f in files:
                full = os.path.join(dirpath, f)
                z.write(full, os.path.relpath(full, stage))
    print('wrote', zip_path)
    with zipfile.ZipFile(zip_path) as z:
        for i in z.infolist():
            print(f'  {i.filename}  {i.file_size} bytes')


if __name__ == '__main__':
    main()
