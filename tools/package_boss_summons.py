"""Package the game-exported seals after inspecting the isolated runtime results."""
from pathlib import Path
import argparse
import json
import hashlib
import shutil
import zipfile
from PIL import Image

ROOT=Path(__file__).resolve().parents[1]
p=argparse.ArgumentParser()
p.add_argument('run',type=Path)
a=p.parse_args()
run=a.run.resolve()
raw=run/'saves/boss-seals'
result=json.loads((run/'result.json').read_text(encoding='utf-8-sig'))
tests=json.loads((raw/'results.json').read_text(encoding='utf-8-sig'))
assert result['Completed'] and result['RestoredSettings'] and not result['TimedOut']
assert not result['Failures'] and not result['ChangedPlayerSaves'] and not result['NewPlayerSaves']
assert tests['failures']==0
profiles=json.loads((raw/'manifest.json').read_text(encoding='utf-8-sig'))
assert len(profiles)==40
dest=ROOT/'空洞骑士Boss召唤物素材包'
dest.mkdir(exist_ok=True)
meta=[]
for row in profiles:
    src=raw/(row['type']+'.png')
    with Image.open(src) as im:
        assert im.size==(48,48) and im.mode=='RGBA'
        lo,hi=im.getchannel('A').getextrema()
        assert lo==0 and hi==255
    filename=f"{row['index']:02d}_{row['name']}.png"
    shutil.copy2(src,dest/filename)
    meta.append(dict(row,file=filename,width=48,height=48,sha256=hashlib.sha256(src.read_bytes()).hexdigest()))
assert len({r['sha256'] for r in meta})==40
shutil.copy2(raw/'contact-sheet.png',dest/'总览.png')
shutil.copy2(ROOT/'Boss召唤物机制与制作清单.md',dest/'机制与制作清单.md')
shutil.copy2(ROOT/'Boss召唤物测试报告.md',dest/'测试报告.md')
doc=dest/'机制与制作清单.md'
doc.write_text(doc.read_text(encoding='utf-8').replace('](Boss召唤物测试报告.md)','](测试报告.md)'),encoding='utf-8')
(dest/'manifest.json').write_text(json.dumps(meta,ensure_ascii=False,indent=2),encoding='utf-8')
(dest/'README.md').write_text('''# 空洞骑士 Boss 召唤物素材包

40 张 48×48 透明 PNG，按清单顺序从左到右、从上到下对应总览。由游戏内原版猎人日志肖像与程序绘制的封印框构成，没有使用大师圣物雕像替代召唤图标。

金色代表现世，梦紫色代表需梦钉的战斗；底部符文是项目进度，重战变体另有侧边符号。PNG 是游戏运行时图标的原样导出，文件名即道具中文名。

游戏已经由源代码接入本系列，运行时从玩家自己的游戏资源生成图标，不依赖这个 PNG 文件夹。此文件夹适合浏览、美术复核和后续界面素材使用；不是独立可安装的 Mod。

机制、40 个配方和覆盖边界见《机制与制作清单.md》。manifest.json 记录物品键、Boss、竞技场、原肖像名称和文件哈希。首批不含尚无完整入口的八个精英/独立变体，不以素材数量代替功能数量。
''',encoding='utf-8')
summary=dict(checks=len(tests['checks']),failures=tests['failures'],icons=40,tested_entries=['FalseKnight','FalseKnight@5','HollowKnightPrime'],player_saves_unchanged=True,settings_restored=True,evidence=str(run),
    limitations=['Only representative arena entries tested, not all 40 complete kills.','Hidden-window ScreenCapture images were black: icon textures inspected, inventory layout not visually confirmed.'])
(dest/'validation.json').write_text(json.dumps(summary,ensure_ascii=False,indent=2),encoding='utf-8')
for name in ['manifest.json','validation.json','总览.png']:
    shutil.copy2(dest/name,ROOT/'artifacts/boss-summons'/name)
archive=ROOT/'artifacts/空洞骑士Boss召唤物素材包.zip'
with zipfile.ZipFile(archive,'w',zipfile.ZIP_DEFLATED) as z:
    for file in sorted(dest.iterdir()):
        if file.is_file():z.write(file,dest.name+'/'+file.name)
with zipfile.ZipFile(archive) as z:
    assert z.testzip() is None
    assert len([n for n in z.namelist() if n.endswith('.png')])==41
print(json.dumps(dict(package=str(archive),files=len(list(dest.iterdir())),**summary),ensure_ascii=False,indent=2))
