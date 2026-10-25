"""Publish only a completed isolated weapon output audit."""
import argparse
import json
from pathlib import Path

p = argparse.ArgumentParser()
p.add_argument('run', type=Path)
p.add_argument('--integration-run', type=Path)
p.add_argument('--output', type=Path, default=Path('武器输出平衡核验.md'))
a = p.parse_args()
read = lambda f: json.loads(f.read_text(encoding='utf-8-sig'))
result = read(a.run/'result.json')
assert result['Completed'] and not result['Failures'] and not result['TimedOut']
assert not result['ChangedPlayerSaves'] and not result['NewPlayerSaves']
data = read(a.run/'saves/weapon-balance.json')
assert not data['Failures']
weapons = {r['Type']: r for r in data['Weapons']}
assert all('\ufffd' not in r['Name'] for r in weapons.values()), 'Corrupted runtime names must not be published'
integration = None
if a.integration_run:
    r=read(a.integration_run/'result.json')
    assert r['Completed'] and not r['Failures'] and not r['TimedOut'] and not r['ChangedPlayerSaves'] and not r['NewPlayerSaves']
    integration=read(a.integration_run/'saves/weapon-balance.json')
    assert not integration['Failures']
    assert {w['Type']:(w['Stage'],w['Damage']) for w in integration['Weapons']} == {t:(w['Stage'],w['Damage']) for t,w in weapons.items()}
lines = ['# 武器输出平衡核验', '',
         '原版参考从本机 Terraria 的 Item.SetDefaults 读取，保存在 `tools/vanilla-data/combat/vanilla-runtime.json`。武器按原版单发伤害、攻击周期、随机弹幕数量期望和持续命中机制适配到六阶段。', '',
         '- 原版稀有度只用于威力参考分组，不用它决定解锁：最终阶段由现有掉落路线和原版配方材料、工作站校验决定。',
         '- 快速、多弹幕武器分摊总输出，连发按完整周期计算。哨兵、仆从与持续区域单独计入自动攻击，不按施法速度计算。',
         '- 保留同阶段原版武器的输出差别，以及吸血、穿透、散布等定位；吸血鬼飞刀保留吸血。饰品属性未在此次调整中削弱。',
         '- 静态输出模型不是每一场战斗的精确模拟。散布、目标体型、地形、蓄力、追踪、无敌时间及弹药会改变实际输出。下面将数据检查和实际命中分开列出。', '',
         f'数据检查覆盖 **{len(weapons)}** 种武器；独立游戏内实测 **{len(data["Live"])}** 个案例；检查 **{data["Checks"]}** 项、失败 **{data["Failures"]}** 项。正式玩家存档无新增或改动。', '',
         '## 吸血鬼飞刀与泰拉之刃', '',
         '同阶段、普通难度、不佩戴护甲或饰品、固定单目标；分别在近距离和中距离持续攻击约 12 秒。距离单位为空洞骑士世界单位。', '',
         '|武器|单发面板|距离|使用次数|生成主弹幕|实际 DPS|',
         '|---|---:|---:|---:|---:|---:|']
for r in (integration or data)['Live']:
    if r['Type'] in ('VampireKnives','TerraBlade') and r['Seconds'] > 11:
        lines.append(f'|{weapons[r["Type"]]["Name"]}|{r["Damage"]}|{r["Distance"]:g}|{r["Casts"]}|{r["Made"] if r["Type"] == "VampireKnives" else "剑气由 SwordFx 发射"}|{r["Dps"]:.1f}|')
lines += ['', '飞刀一次发射 4–8 把，期望 4.9375 把。泰拉之刃的挥斩与剑气均走真实命中逻辑，实际输出由敌人损失的泰拉血量计算，未用面板乘攻速替代。', '',
          '## 游戏内实测', '',
          '魔力每帧补满、弹药无限供应，目的是测攻击输出；不表示正常游玩时可无限续航。召唤物数量列为本次实际装备上限，裸装为 1；完整配装还能叠加仆从和鞭子。弩车案例使用抬高的目标，以避开向下盲区和地面遮挡。', '',
          '|阶段|武器|距离|秒数|单发面板|仆从上限|实际 DPS|', '|---:|---|---:|---:|---:|---:|---:|']
for r in data['Live']:
    lines.append(f'|{r["Stage"]}|{weapons[r["Type"]]["Name"]}|{r["Distance"]:g}|{r["Seconds"]:.1f}|{r["Damage"]}|{r["Minions"]}|{r["Dps"]:.1f}|')
if integration:
    lines += ['', '## 安装版合并回归', '',
              f'安装版同时保留并行完成的掉落物和摆放物显示修复。护甲、套装和饰品恢复原版属性；451 种武器的阶段和伤害与完整武器实测逐项一致。安装版另测 {len(integration["Live"])} 组关键案例，{integration["Checks"]} 项检查、零失败。上方飞刀和泰拉之刃对比使用此安装版记录。', '',
              '|武器|距离|秒数|实际 DPS|', '|---|---:|---:|---:|']
    for r in integration['Live']:
        lines.append(f'|{weapons[r["Type"]]["Name"]}|{r["Distance"]:g}|{r["Seconds"]:.1f}|{r["Dps"]:.1f}|')
lines += ['', '## 所有武器数据', '',
          '基准数量、重复命中和伤害单位是模型期望。持续攻击的每秒命中估计包括独立冷却、仆从预算数量等，其使用周期不代表实际命中周期；这些估计不能替代上面的实测结果。弹药、暴击、饰品、敌人防御另行作用。', '',
          '|阶段|武器 / ID|原版伤害|现伤害|模型周期帧数|基准数量|重复命中|每秒命中估计|计算依据|',
          '|---:|---|---:|---:|---:|---:|---:|---:|---|']
for r in sorted(weapons.values(), key=lambda r: (r['Stage'],r['Type'])):
    lines.append(f'|{r["Stage"]}|{r["Name"]} / {r["Type"]}|{r["VanillaDamage"]}|{r["Damage"]}|{r["Cycle"]*60:.1f}|{r["Volley"]:.3f}|{r.get("Repeat",1):.2f}|{r["Hits"]:.2f}|{r["Basis"]}|')
lines += ['', f'实测证据：`{a.run.as_posix()}/saves/weapon-balance.json`。', '']
a.output.write_text('\n'.join(lines), encoding='utf-8')
print(f'{len(weapons)} weapons, {len(data["Live"])} live cases -> {a.output}')
