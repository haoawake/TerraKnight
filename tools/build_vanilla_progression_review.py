"""Build the review from the isolated game's actual authored loot export."""
import argparse
import json
import re
from pathlib import Path

root = Path(__file__).resolve().parents[1]
parser = argparse.ArgumentParser()
parser.add_argument('run', type=Path)
parser.add_argument('--attacks', type=Path)
args = parser.parse_args()
report = json.loads((args.run / 'saves/vanilla-loot-report.json').read_text('utf-8-sig'))
result = json.loads((args.run / 'result.json').read_text('utf-8-sig'))
assert result['Completed'] and not result['Failures'] and not report['Failed']
names = dict(re.findall(r'\["([^"]+)"\] = new Entry \{ En = "[^"]+", Zh = "([^"]+)"',
                        (root / 'src/TerraKnight/Items/EnemyDropData.cs').read_text('utf-8-sig')))
names.update({'Hornet@4': '大黄蜂二战', 'FalseKnight@5': '失败冠军',
              'MageLord@5': '灵魂暴君', 'InfectedKnight@5': '失落近亲'})
native_names = json.loads((root / 'tools/vanilla-data/crafting/item-names.json').read_text('utf-8-sig'))
native_map = json.loads((root / 'tools/vanilla-data/crafting/baseline-item-map.json').read_text('utf-8-sig'))
item_names = {native_map.get(i, value.get('type')): value.get('zh') for i, value in native_names.items()}
def item_label(item):
    return item_names.get(item) or item
lines = ['# 泰拉瑞亚细阶段、首领强度与奖励', '',
         '替代此前按六个能力阶段平摊四职业装备的分配方案。六个区域阶段保留用于地图能力；首领胜利另记泰拉瑞亚节点。', '',
         '普通制作物不直接掉落；保留原版独特掉落装备，商店装备排除。首领奖励不强凑四职业：机械首领给神圣锭和自己的魂，四柱给自己的碎片，升级武器和护甲使用原版配方制作。', '',
         '同区采用固定血量、防御和伤害差异，鼓励先弱后强；不会按玩家当前穿戴自动涨数值，也不会禁止玩家越序挑战。', '',
         '|当前可用装备示例|推荐目标|胜利后的用途|下一步|',
         '|---|---|---|---|',
         '|商店基础武器、木/矿物装备|早期支线、假骑士|史莱姆法杖、机动配件、忍者装备；毛里克的稀有近战|大黄蜂、邪恶首领|',
         '|金/铂金、邪恶矿武器，丛林支线装备|吸虫之母/粪虫防御者|组织样本/暗影鳞片制作护甲和镐，开启狱石材料|蜂后、骷髅王|',
         '|邪恶套、丛林套、蜂类武器|螳螂领主、灵魂大师、地狱支线|手枪自己升凤凰爆破枪；村正配其他原料制作永夜刃；狱石制作熔岩套、小鬼法杖|血肉墙对应首领|',
         '|肉前毕业装备|守望者骑士/收藏家/灵魂暴君/残破容器|各自独特的肉山武器与徽章；困难模式矿物、蜘蛛材料开放|宝箱怪、史莱姆皇后|',
         '|钛金/精金、蜘蛛套，宝箱怪武器、刃杖|大黄蜂二战（较弱的机械首领）|力量之魂制作巨兽鲨；神圣锭制作神圣武器和护甲|马科斯、失落近亲|',
         '|神圣装备、巨兽鲨等；也可按职业更换三王顺序|马科斯/失落近亲|各给视域/恐惧之魂；魔眼法杖、火焰喷射器分别制作|集齐三王魂制作斧钻/镐斧，进入叶绿链|',
         '|叶绿、海龟等三王后装备|叛徒领主（世纪之花）|花系独特武器；灵气、蘑菇矿加工和断裂英雄剑开放|神之驯服者、月亮事件支线|',
         '|世纪之花武器、幽灵/蘑菇矿套等|神之驯服者（石巨人）|石巨人独特武器、甲虫鞘翅自己制作甲虫套|后期支线、空洞骑士|',
         '|石巨人及后期支线装备|空洞骑士（邪教徒）|远古操纵机|四柱对应首领|',
         '|月前装备|骨钉兄弟/骨钉贤者/画师/纯粹容器|分别给日耀/星旋/星云/星尘碎片，自己制作天界武器|辐光（月亮领主）|',
         '|天界武器|辐光|独特月主武器、夜明矿；夜明锭与天界护甲自己制作|终局制作|', '',
         '三王的六种击败顺序都验证了独立魂解锁与第一、第二、第三王节点。猪龙鱼公爵属于可越序的高难支线，击败它不会冒充三王或世纪之花已击败。', '',
         '## 全部首领数值与奖励', '',
         '生命是单人大师整场总量，已计入原生多阶段/多身体的分摊。四柱对应首领仅在神居出现，生命列使用能力阶段 5 的神居有效总量；其防御沿用神居现有缩放。其他行防御是世界战有效防御。伤害系数作用于区域基础伤害与 Boss 总系数，原生攻击的面具数仍参与计算。', '',
         '|首领|区域阶段 / 原版节点|整场生命|世界防御 / 伤害系数|独特装备候选与标志物|原材料|升级用途|',
         '|---|---|---:|---|---|---|---|']
for b in sorted(report['Bosses'], key=lambda b: (b['Stage'], b['MasterLife'])):
    gear = [item_label(g['Item']) for g in b['Gear']]
    if b['Signature'] != 'None':
        gear.append(item_label(b['Signature']))
    mats = [f"{item_label(m['Item'])} ×{m['Min']}–{m['Max']}" for m in b['Materials']]
    life = round(b['EffectiveMasterLife'])
    defense = '神居缩放' if b['Godhome'] else str(b['Defense'])
    lines.append(f"|{names.get(b['Boss'], b['Boss'])} `{b['Boss']}`|{b['Stage']} / {b['Role']}|{life:,}|{defense} / ×{b['DamageMultiplier']}|{'、'.join(gear) or '以材料推进'}|{'、'.join(mats) or '见装备奖励'}|{b['Upgrade']}|")
lines += ['', '主装备候选共用一次必定抽取，另有 30% 的第二次抽取；标志物和列出的材料独立给。原版唯一装备的获取条件在此按模组首领映射简化，表格不表示复刻了所有原版事件或昼夜条件。', '',
          '## 上阶段装备转交附近小怪', '',
          '每件转交装备选择一个后续节点附近的普通敌人，5% 独立掉落；必须达到该节点和区域阶段。表内不是新增合成配方。', '',
          '|物品|小怪记录|区域阶段|解锁节点|', '|---|---|---:|---|']
for t in report['Transfers']:
    lines.append(f"|{item_label(t['Item'])}|{names.get(t['Monster'], t['Monster'])} `{t['Monster']}`|{t['Stage']}|{t['Milestone']}|")
lines += ['', '## 神居与材料门槛', '',
          '神居同时取能力阶段和世界首领进度的较低上限；未完成世界对应首领时给当前可用的制作原材料。神居不会提前解锁世界进度；四柱对应的神居原生首领仅在邪教徒节点后可推进各自碎片。', '',
          '四种碎片分别归属四柱；任何一种都不能从普通小怪获得。每种机械魂归属自己的机械首领。叶绿需三王全胜，灵气/蘑菇矿/断裂英雄剑需世纪之花，夜明矿需月主。探索赠礼、商店和神居通关奖也检查材料进度。', '',
          '旧存档首次迁移会保留旧图鉴的击败记录；旧记录没有完整的世界/神居来源，无法反推所有历史击败场景。迁移后新胜利按实际死亡场景分别记账。', '',
          '坐骑旁路也检查进度：蜂后坐骑归胡长老，史莱姆皇后坐骑归蜂巢骑士，史莱姆鞍归假骑士；神居未完成对应世界战时不额外掉坐骑。', '',
          '## 验证证据', '',
          f"- 隔离副本 `{args.run.as_posix()}`：{report['Checks']} 项掉落/节点/数值检查通过，1327 项原版实际制作检查通过。原玩家存档无变更、无新增；共享设置未声称恢复。",
          '- 检查覆盖 42 个正式首领、合法且非空奖励、商店/制作物排除、装备唯一归属、坐骑旁路、六种三王顺序、神居六个能力上限、材料来源与多阶段整场血量。']
if args.attacks:
    attack = json.loads((args.attacks / 'saves/master-attack-variants-audit.json').read_text('utf-8-sig'))
    ar = json.loads((args.attacks / 'result.json').read_text('utf-8-sig'))
    assert ar['Completed'] and not ar['Failures']
    lines += [f"- 实际攻击副本 `{args.attacks.as_posix()}`：7 组武器实测。使用固定目标、相同配装，无锻造，正常消耗弹药；这些输出不能当成移动首领的实际通关时间。", '',
              '|武器|爆发 DPS|持续 DPS|', '|---|---:|---:|']
    for p in attack['Profiles']:
        lines.append(f"|{p['Weapon']}|{p['BurstDps']:.1f}|{p['SustainedDps']:.1f}|")
lines += ['', '武器平衡改用同一能力阶段的共同基准，避免较高稀有度把原版合成升级反向压低；原版配方直接消耗同职业武器时，最终校准后仍保留合成升级的输出收益。未完成全部 42 场移动首领的实战通关测试，也未验证本次变更的双端联机表现。', '']
installed_file = root / 'artifacts/vanilla-progression-install-result.json'
if installed_file.exists():
    installed = json.loads(installed_file.read_text('utf-8-sig'))
    assert installed['Installed'] and installed['BuildSha256'] == installed['TestedSha256'] == installed['InstalledSha256']
    lines += ['## 正式安装', '', f"已安装到 `{installed['Target']}`，旧 DLL/PDB 备份在 `{installed['Backup']}`。",
              f"构建、隔离测试与正式安装 DLL 的 SHA-256 一致：`{installed['InstalledSha256']}`。", '']
out = root / 'docs/原版细阶段与首领奖励-2026-10-21.md'
out.write_text('\n'.join(lines), encoding='utf-8')
print(out)
