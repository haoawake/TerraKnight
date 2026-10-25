"""Build an auditable restoration report from the tested delivery snapshot."""
import argparse
import json
from pathlib import Path

p=argparse.ArgumentParser(description=__doc__)
p.add_argument('runs',type=Path)
p.add_argument('--source-root',type=Path,required=True)
a=p.parse_args()
read=lambda f:json.loads(f.read_text(encoding='utf-8-sig'))
runs=read(a.runs)
if isinstance(runs,dict): runs=[runs]
indexed={r['Suite']:Path(r['Run']) for r in runs}
for run in indexed.values():
    result=read(run/'result.json')
    assert result['Completed'] and not result['TimedOut'] and not result['Failures']
    assert not result['ChangedPlayerSaves'] and not result['NewPlayerSaves']
audit=read(indexed['armorrestore']/'saves/armor-restore.json')
assert audit['Failures']==0
lines=['# 护甲、套装与饰品原版属性恢复核验', '',
       '按本机 Terraria 1.4.5.8 的原始物品定义和套装回调核验。护甲、套装和饰品保留原版属性；武器按攻击频率、弹幕数和阶段继续做输出重平衡。', '',
       '- 删除阶段统一防御、职业伤害预算、暴击清零、仆从数量覆盖和套装防御清零。',
       '- 保留单件原始属性和完整套装说明。原版自带的代价也保留，例如幽灵兜帽的魔法伤害降低。',
       '- 补齐当前原版死灵套的远程暴击 +10%、熔岩套的火块和着火免疫、暗影套的跑速上限 ×1.15 与加速/减速 ×1.75。',
       '- 星星斗篷恢复每次受伤触发 3 星，单星普通/专家/大师为 75/150/225；取消额外的阶段伤害预算和 1 秒冷却。',
       '- 蜂窝恢复原版随机蜂数、13/18 基础伤害及难度/强蜂修正；取消额外阶段伤害预算和 1 秒冷却。',
       '- 保留原版配方、当前获取阶段、饰品共池 N 选 1 和不可堆叠物品每次只掉 1 件。', '',
       '## 数据与运行验证', '',
       f'隔离游戏验证 {audit["ArmorCount"]} 件护甲、{audit["SetCount"]} 个套装判定、{audit["Combinations"]} 个整套搭配，共 {audit["Checks"]} 项检查，零失败。', '',
       '原版对照来自直接调用 Terraria 程序的 `GrantArmorBenefits` 和 `ArmorSetBonuses.Benefits`，独立于 mod 的初始化数据。原版同一套回调的金属/木材变体展开为本 mod 的套装 ID。', '',
       '游戏内检查包括神圣命中闪避、钯金命中再生、钛金碎片、山铜花瓣、寒霜减益、幽灵治疗/怒气弹、星云强化物及三级加成、星尘守卫、叶绿水晶、甲虫三层减伤、日耀三盾/冲刺、星旋隐身、水晶刺客冲刺和合法场地下的禁戒沙暴。计时机制按真实 60 Hz Tick 推进；这些检查不等同于每套装备在每个场景的人工观感验收。', '',
       '|检查套件|结果|证据|','|---|---|---|']
for suite,run in indexed.items():
    lines.append(f'|`{suite}`|完成、零失败、未超时；玩家存档无变化|`{run.relative_to(Path.cwd())}/result.json`|')
lines+=['', '## 当前单件护甲', '',
        '防御为单件防御；仆从列为装备额外增加数，基础仆从数另有 1。下面的作用说明来自验证时实际运行的数据。', '',
        '|装备 / ID|阶段|防御|仆从 +|原版单件作用|','|---|---:|---:|---:|---|']
for row in audit['Items']:
    tip=(row['Tooltip'] or '无额外单件属性').replace('\n','；').replace('|',' / ')
    lines.append(f'|{row["Name"]} / `{row["Type"]}`|{row["Stage"]}|{row["Defense"]}|{row["Bonus"]["MaxMinions"]}|{tip}|')
out=Path('护甲套装原版属性恢复核验.md')
out.write_text('\n'.join(lines)+'\n',encoding='utf-8')
(a.source_root/out.name).write_bytes(out.read_bytes())
print(f'{audit["ArmorCount"]} armour entries and verified delivery gates -> {out}')
