"""Update asset metadata and preview; raster export remains in the pack's existing PS exporter."""
import json
import shutil
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1] / '黑吼四职业武器素材包'
BACKUP = ROOT / '历史版本_改型前'
BACKUP.mkdir(exist_ok=True)

def read(name):
    return json.loads((ROOT / name).read_text(encoding='utf-8-sig'))

def write(name, value):
    (ROOT / name).write_text(json.dumps(value, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')

for name in ['素材与提示词.json', '导入配置.json', '升级名称映射.json', 'README.md', '给Claude的特效接入说明.md', '预览模板.html', '动画与操作预览.html', '四职业武器预览.png']:
    if not (BACKUP / name).exists():
        shutil.copy2(ROOT / name, BACKUP / name)

changes = [
    ('abyss_deathbell', 'abyss_shriek_yoyo', '深渊悠悠球', [32, 32], 'exec-424af5d5-c963-4710-985c-205b65138e75.png'),
    ('void_pipe_organ', 'void_shriek_sniper', '虚空狙击枪', [76, 28], 'exec-83fc985f-903c-4ea4-9c02-7fc414de8922.png'),
    ('abyss_shriek_focus', 'abyss_shriek_staff', '深渊尖啸法杖', [40, 64], 'exec-204c03c2-7de1-4ce1-b499-4a9749a2342b.png'),
]
gen = Path('C:/Users/luo/.codex/generated_images/01a158bf-d9a4-7890-90cf-916c4dfc1ba8')
specs = read('素材与提示词.json')
prompts = read('改型提示词.json')
for old, new, name, size, filename in changes:
    for folder in ['武器贴图', '物品图标_36px']:
        src = ROOT / folder / (old + '.png')
        if src.exists():
            dest = BACKUP / folder
            dest.mkdir(exist_ok=True)
            shutil.move(str(src), str(dest / src.name))
    shutil.copy2(gen / filename, ROOT / '生成原稿' / (new + '.png'))
    spec = next(s for s in specs if s['id'] in [old, new])
    spec.update(id=new, name=name, size=size, refs=[str(ROOT / '生成原稿' / (old + '.png'))], replaces=old,
                prompt=next(p['prompt'] for p in prompts if p['id'] == new), source=str(gen / filename))
write('素材与提示词.json', specs)

config = read('导入配置.json')
config['schema'] = 2
config['weapons'][:3] = [
    dict(id='abyss_shriek_yoyo', name='深渊悠悠球', **{'class': 'melee'}, texture='武器贴图/abyss_shriek_yoyo.png', size=[32,32], pivot=[16,16], stringAttach=[16,16],
         render='Yo-yo head rotates around central axle. Draw thin procedural string from hand to center; no chain, no rotation sprite sheet.',
         mechanic='Terraria yo-yo: travel toward cursor, hover within tether range while held, return on release. Soul pulse on hit with per-target cooldown; final damage and timings belong to gameplay integration.'),
    dict(id='void_shriek_sniper', name='虚空狙击枪', **{'class': 'ranged'}, texture='武器贴图/void_shriek_sniper.png', size=[76,28], grip=[21,20], muzzle=[73,13], scope=[31,6], restForward=[1,0],
         render='Single long rifle barrel, mounted scope and shoulder stock. Rotate around grip, slight shot recoil and recovery; no multi-barrel organ animation.',
         mechanic='Sniper category: a deliberate single aimed shot. Bullet consumption, scope controls, penetration and stats are gameplay decisions, not implemented in this artwork pack.'),
    dict(id='abyss_shriek_staff', name='深渊尖啸法杖', **{'class': 'magic'}, texture='武器贴图/abyss_shriek_staff.png', size=[40,64], grip=[15,43], castOrigin=[28,13], restForward=[0.48,-0.88],
         castAnimation='abyss_triad_cast', render='Physical held staff; point crown toward cursor. Existing three-soul animation is separate spell-body art, never the held-item sprite.',
         mechanic='Retain cursor-position three-pulse spell with range and solid-wall checks. Staff animation is transform based; particles and sound waves belong to Claude.'),
]
config['existingPart'] = dict(id='yoyo_string', render='Thin procedural string attached to central axle; do not use chain links.', legacyReference='原版参考/Chain.png', legacyUsage='Old flail reference only, not used by the revised yo-yo.')
config['claudeEffects'] = ['yo-yo hit soul pulse with per-target cooldown', 'single sniper shot trail and impact; avoid shotgun fan waves', 'mage cursor-position 3 local pulses from staff cast', 'minion directional cone shout', 'particles, glow, fading trails, impact flash']
config['delivery'] = '27 production PNGs; 3 redesigned weapons, summon mask and spell/minion sheets retained. No gameplay registration. Historical assets are isolated in 历史版本_改型前.'
config['upgradeFrom'] = dict(abyss_shriek_yoyo='wailing_deathbell', void_shriek_sniper='ghost_pipe_organ', abyss_shriek_staff='howling_wraiths_focus', dirge_mask='elegy_mask')
config['animations'][-1]['usage'] = 'Legacy optional resonance-shell body, not a default projectile of the new sniper rifle.'
write('导入配置.json', config)
mapping = read('升级名称映射.json')
lookup = {old: new for old,new,*_ in changes}
lookup.update({'深渊丧钟':'深渊悠悠球','虚空风琴':'虚空狙击枪','深渊尖啸':'深渊尖啸法杖'})
write('升级名称映射.json', [[a, lookup.get(b,b)] for a,b in mapping])

html = (BACKUP / '预览模板.html').read_text(encoding='utf-8-sig')
html = html.replace('链钟收放', '悠悠球悬停与收线').replace('深渊丧钟 · 投掷／回收', '深渊悠悠球 · 悬停／旋转／收线').replace('钟头旋转，原版链节重复排列。交点标记为持握端；碰撞魂啸未加入。', '圆盘围绕中轴旋转，以细绳连接持握端；展示伸出、悬停与收回，命中魂啸交给 Claude。')
html = html.replace('虚空风琴 · 瞄准／后坐','虚空狙击枪 · 瞄准／后坐').replace('三枪口随武器方向转动，右键共鸣弹主体展示；左键声波及爆发特效未加入。','单长枪管、瞄准镜、抵肩枪托；展示瞄准与单发后坐，弹道和命中特效交给 Claude。')
html = html.replace('深渊尖啸 · 显形／尖叫／回收','深渊尖啸法杖 · 持握／施法').replace('8 帧主体围绕施法点播放，可整体旋转。示意落点不在角色头顶绑定；三次伤害声浪由接入方完成。','持握法杖指向光标，原有 8 帧三魂动画在落点独立播放；三次伤害声浪由接入方完成。')
start = html.index('const defs=')
end = html.index('\ndocument.querySelector', start)
html = html[:start] + "const defs=[['abyss_shriek_yoyo','深渊悠悠球','战士 · 悠悠球 · 32×32',32],['void_shriek_sniper','虚空狙击枪','射手 · 狙击枪 · 76×28',76],['abyss_shriek_staff','深渊尖啸法杖','法师 · 法杖 · 40×64',40],['dirge_mask','冥歌面具','召唤师 · 36×40',36]];" + html[end:]
html = html.replace('height:210px','height:280px')
start = html.index('const [b,o,t,m]=cs')
end = html.index('const tf=',start)
motion = '''const [b,o,t,m]=cs,dx=Math.cos(a),dy=Math.sin(a),cycle=time%3,reach=cycle<.4?cycle/.4:cycle<2.4?1:Math.max(0,(3-cycle)/.6),hx=170,hy=140,bx=hx+dx*(28+reach*130),by=hy+dy*(28+reach*130)+Math.sin(time*5)*reach*5;
b.strokeStyle=light?'#56516e':'#c3bed6';b.lineWidth=1.5;b.beginPath();b.moveTo(hx,hy);b.quadraticCurveTo((hx+bx)/2,(hy+by)/2+12*reach,bx,by);b.stroke();sprite(b,'abyss_shriek_yoyo',bx,by,time*12,3,[16,16]);dot(b,hx,hy);
const recoil=Math.max(0,1-(time%1.8)/.16)*9;sprite(o,'void_shriek_sniper',210-dx*recoil,140-dy*recoil,a,3,[21,20]);dot(o,210,140);
sprite(t,'abyss_shriek_staff',95,205,a+Math.atan2(.88,.48),2,[15,43]);dot(t,95,205);
'''
html = html[:start] + motion + html[end:]
html = html.replace("sprite(t,'abyss_triad_cast',250,140,a+Math.PI/2,3", "sprite(t,'abyss_triad_cast',310,120,a+Math.PI/2,2")
html = html.replace('dot(t,250,140)', 'dot(t,310,120)')
(ROOT / '预览模板.html').write_text(html, encoding='utf-8')

readme = '''# 黑吼四职业武器：改型版

本轮按最新要求重绘三个武器主体，使用内置 imagegen；提示词保存在 `改型提示词.json`，原稿位于 `生成原稿/`。

| 职业 | 名称 | 透明贴图 | 尺寸 |
|---|---|---|---|
| 战士 | 深渊悠悠球 | 武器贴图/abyss_shriek_yoyo.png | 32×32 |
| 射手 | 虚空狙击枪 | 武器贴图/void_shriek_sniper.png | 76×28 |
| 法师 | 深渊尖啸法杖 | 武器贴图/abyss_shriek_staff.png | 40×64 |
| 召唤师 | 冥歌面具 | 武器贴图/dirge_mask.png | 36×40 |

悠悠球细绳与旋转、狙击枪瞄准后坐、法杖持握施法均在 `动画与操作预览.html` 中展示。可调整全方向瞄准、暂停以及深浅背景。

`物品图标_36px/` 是物品栏图标；`动画图集/abyss_triad_cast.png` 仍为 64×64、竖排 8 帧的三魂法术主体，独立于新法杖；`动画图集/dirge_soul_minion.png` 仍为 40×40、竖排 8 帧的召唤物主体。共 27 张导出 PNG，二值透明、最多 16 色，Point/Nearest 采样。

黑色主体是不透明的，银蓝轮廓和白色尖叫五官保持系列一致。原链钟、风琴和法术物品图标及旧配置归档于 `历史版本_改型前/`；它们不再是当前有效武器。`原版参考/Chain.png` 只是历史链条参考，新悠悠球使用程序细绳。共鸣弹主体仅保留作可选素材，不再指定为狙击枪默认弹幕。

导入锚点、朝向和动画配置见 `导入配置.json`；说明见 `给Claude的特效接入说明.md`。握持点为贴图建议，接入角色后仍需实机校准。

运行 `导出素材.ps1` 重导出 PNG，运行 `生成预览.ps1` 重建离线预览。`素材验证.json`、`黑色主体验证.json` 与 `预览验证.json` 记录素材和浏览器检查。

本轮交付贴图、展示动画与接入资料；未注册游戏物品、实现伤害/扣弹/联机。粒子、声浪、拖尾、光晕与命中特效由 Claude 补充。总览与网页截图仅为展示图，不能作为游戏贴图导入。
'''
(ROOT / 'README.md').write_text(readme, encoding='utf-8')
handoff = '''# 黑吼改型版：给 Claude 的特效接入说明

项目绝对路径：`E:\\皓子不困的妙妙工具\\github项目管理\\projects\\强兼`

素材包：项目根目录 `黑吼四职业武器素材包`。先读 `README.md`、`导入配置.json`，打开 `动画与操作预览.html`。当前有效形态是：**悠悠球、狙击枪、法杖、召唤面具**。历史素材在 `历史版本_改型前/`，不可按旧链钟/三管风琴配置接入。

| 职业 | 名称与相对路径 | 处理方式 |
|---|---|---|
| 战士 | 深渊悠悠球，武器贴图/abyss_shriek_yoyo.png，32×32 | 中轴在 (16,16)，细绳连接手与中轴；按泰拉瑞亚悠悠球方式伸出、悬停、收线，圆盘独立旋转。命中魂啸须设同目标触发冷却。不要使用旧链节。 |
| 射手 | 虚空狙击枪，武器贴图/void_shriek_sniper.png，76×28 | 默认朝右，单长枪管、瞄准镜、枪托。握点、枪口见配置；程序瞄准、后坐与恢复。补单发狙击弹道、魂啸命中，具体射速、穿透、瞄准镜交互与耗弹另做平衡。 |
| 法师 | 深渊尖啸法杖，武器贴图/abyss_shriek_staff.png，40×64 | 这是持握物品。杖头指向光标，施法落点独立于玩家，在距离限制及实心墙检查后生成三魂法术。 |
| 召唤师 | 冥歌面具，武器贴图/dirge_mask.png，36×40 | 保留原有召唤形态与小冥魂动画；先靠近、蓄势、锁定方向尖叫，再恢复，多个仆从错开攻击。 |

三魂法术主体：`动画图集/abyss_triad_cast.png`，64×512，每帧64×64，共8帧：0–3展开、4–5尖叫、6–7回收。原图局部朝上，可以整体旋转；法术在光标落点释放，不能强制只朝上。三次伤害由逻辑调度，不等于贴图帧数。

小冥魂主体：`动画图集/dirge_soul_minion.png`，40×320，每帧40×40，共8帧：0–1待机、2–3蓄势、4–5尖叫、6–7恢复。嘴部点 (29,20)，默认朝右，尖叫期间锁定方向。

`动画单帧/` 有16张独立帧；`物品图标_36px/` 有四件当前物品栏图标。`弹幕贴图/void_resonance_shell.png` 为旧共鸣弹可选素材，不能据此恢复原三管风琴的扇形攻击。

表现：不透明黑色虚空主体、银蓝轮廓、少量紫色刻纹、亮白五官；Point/Nearest 采样。不要把黑色当透明。补粒子、声浪、拖尾、命中、光晕和消散；不把特效烘焙回持握武器贴图。

当前仅素材与浏览器展示动画，尚无游戏物品注册、掉落/配方、伤害、弹药、魔力、AI 或网络实现。参数和握点是接入建议，需要游戏内校准；保留现有无关改动。
'''
(ROOT / '给Claude的特效接入说明.md').write_text(handoff, encoding='utf-8')

# The preview can be checked directly from disk, matching the user's open local-file tab.
check = (ROOT / '检查预览.cjs').read_text(encoding='utf-8-sig')
check = check.replace("'http://127.0.0.1:8899/动画与操作预览.html'", "require('url').pathToFileURL(path.join(__dirname,'动画与操作预览.html')).href")
(ROOT / '检查预览.cjs').write_text(check, encoding='utf-8')
print('Updated revised weapon metadata, handoff and preview template.')
