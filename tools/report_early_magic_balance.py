"""Compare pre/post runtime measurements without interpreting dummy DPS as boss clears."""
import argparse
import json
import statistics
from pathlib import Path


def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


def repeats(rows):
    groups = {}
    for row in rows:
        if '/validated-repeat' in row['Profile']:
            groups.setdefault(row['Profile'].split('/validated-repeat')[0], []).append(row['Dps'])
    return {key: dict(count=len(vals), mean=statistics.mean(vals), minimum=min(vals), maximum=max(vals))
            for key, vals in groups.items()}


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('balance', type=Path)
    parser.add_argument('encounters', type=Path)
    parser.add_argument('--output', type=Path, default=Path('artifacts/early-magic-balance-review'))
    args = parser.parse_args()
    baseline = Path('artifacts/single-copy-earlymagiccontrols-20261026-003149')
    before = repeats(read(baseline / 'saves/early-magic-audit.json')['Rows'])
    audit = read(args.balance / 'saves/early-magic-audit.json')
    encounters = read(args.encounters / 'saves/early-magic-audit.json')
    after = repeats(audit['Rows'])
    tracking = read(args.balance / 'saves/early-magic-tracking.json')
    single = read(args.balance / 'saves/early-magic-single-hit.json')
    results = [read(run / 'result.json') for run in (args.balance, args.encounters)]
    valid = [r for r in encounters['Rows'] if 'invalid-spawn' not in r['Profile'] and
             not (r['Casts'] == 0 and r['Hits'] == 0 and r['Seconds'] < .2)]
    covered = {ai: sorted({r['Weapon'] for r in valid if r['Profile'].endswith('/' + ai)}) for ai in ('Vanilla', 'New')}
    complete = (audit['Complete'] and encounters['Complete'] and tracking['Failures'] == 0 and single['Failures'] == 0
                and all(r['Completed'] and not r['TimedOut'] and not r['Failures'] and
                        not r['ChangedPlayerSaves'] and not r['NewPlayerSaves'] for r in results)
                and all(len(v) == 10 for v in covered.values()) and len(valid) == 40
                and len(audit['Rows']) == 92 and len(after) == 5 and all(x['count'] == 3 for x in after.values())
                and len([r for r in audit['Rows'] if r['Profile'].startswith('robe/') and 'repeat' not in r['Profile']]) == 49)
    install_path = args.output / 'install.json'
    install = read(install_path) if install_path.exists() else None
    if install:
        assert all(r['BuildHash'] == install['BuildHash'] for r in results), 'Installed candidate differs from tests'
    summary = dict(complete=complete, before=before, after=after, measurements=len(audit['Rows']), installation=install,
                   encounters=valid, encounter_coverage=covered, tracking=tracking, single_hit=single,
                   results=results)
    args.output.mkdir(parents=True, exist_ok=True)
    (args.output / 'review.json').write_text(json.dumps(summary, ensure_ascii=False, indent=2), encoding='utf-8')
    lines = ['# 前期宝石法杖平衡调整与复测', '',
             '日期：2026-10-26；大师难度。此前 29 把武器审计保留为调整前证据，本轮只改动七种宝石弹的命中规则及复合特效折扣。', '',
             '## 改动', '',
             '- 每发早期宝石弹对每名敌人只命中一次，穿透与反弹仍可命中其他敌人；持续追踪和反弹重新索敌都跳过已经命中的敌人。每发弹丸独立记录，新弹仍能命中原目标。',
             '- 蓝玉双弹、琥珀反弹、红玉双轮齐射的伤害折扣由取最小值改为叠乘。例如双弹＋双轮齐射由每发 50% 改为 60%×50%=30%。',
             '- 保留宝石特效、同色法袍加成、法袍属性及现有弹丸寿命。璀璨宝石法杖和禁戒宝石弹不采用早期弹的单目标命中限制。', '',
             '## 紫晶法杖三次重复取样', '',
             '相同固定靶与防御，逐帧补满魔力，比较输出上限；不代表实际首领击杀时间。', '',
             '|组合|调整前平均 DPS|调整后平均 DPS|调整后范围|样本数（前/后）|',
             '|---|---:|---:|---|---|']
    labels = {'bare': '裸装', 'robe/AmethystRobe': '紫晶法袍', 'robe/EmeraldRobe': '翡翠法袍',
              'robe/DiamondRobe': '钻石法袍', 'robe/AmberRobe': '琥珀法袍'}
    for profile, label in labels.items():
        a, b = before[profile], after[profile]
        lines.append(f"|{label}|{a['mean']:.1f}|{b['mean']:.1f}|{b['minimum']:.1f}–{b['maximum']:.1f}|{a['count']}/{b['count']}|")
    lines += ['', '## 七种法杖与法袍交叉结果', '',
              '每种法杖测裸装、同期配装、有限魔力、移动靶；另测 7×7 法袍组合。有限魔力按前轮场景配置，不是角色魔力获取上限。短窗口有随机波动。', '',
              '|法杖|组合/模式|目标|DPS|', '|---|---|---|---:|']
    for row in audit['Rows']:
        if 'repeat' not in row['Profile']:
            lines.append(f"|{row['Name']}|{row['Profile']}|{row['Target']}|{row['Dps']:.1f}|")
    lines += ['', '## 运行时验证', '',
              f"- 有效输出测量 {len(audit['Rows'])} 条；普通敌人接敌记录 {len(valid)} 条，原版/新版 AI 各覆盖 {len(covered['Vanilla'])}/{len(covered['New'])} 把前期武器。",
              f"- 单发寿命与目标有效性：{tracking['Checks']} 项，失败 {tracking['Failures']}。",
              f"- 同一敌人只命中一次、命中后跳过索敌、新弹可再命中：{single['Checks']} 项，失败 {single['Failures']}。",
              f"- 完成所需覆盖及隔离验证：{complete}。各测试 DLL 哈希、超时和玩家存档检查见 review.json/results。", '',
              '普通敌人测试角色站定且免死，固定初始距离，无补魔；这只衡量夹具下的命中及压制，不证明真人生存或完整首领击杀结果。未测多人。', '',
              '|武器|目标|AI|是否击杀|观察/耗时秒|玩家受击|', '|---|---|---|---|---:|---:|']
    for row in valid:
        lines.append(f"|{row['Name']}|{row['Target']}|{row['Profile'].split('/')[-1]}|{'是' if row['Killed'] else '否'}|{row['Seconds']:.2f}|{row['PlayerHits']}|")
    lines += ['', '## 证据目录', '', f'- 调整前重复采样：`{baseline.resolve()}`',
              f'- 调整后输出、寿命及命中检查：`{args.balance.resolve()}`',
              f'- 调整后接敌：`{args.encounters.resolve()}`', '']
    if install:
        lines += ['## 本地安装', '',
                  f"已安装至 `{install['PluginDirectory']}`，DLL SHA256 为 `{install['BuildHash']}`，与两轮隔离测试一致。旧 DLL/PDB 保存在 `{install['Backup']}`。重启游戏后生效；未终止用户游戏进程。", '']
    (args.output / 'report.md').write_text('\n'.join(lines), encoding='utf-8')
    print(json.dumps({k: summary[k] for k in ('complete', 'measurements', 'before', 'after')}, ensure_ascii=False))
    if not complete:
        raise SystemExit('Incomplete verification; inspect review.json')


if __name__ == '__main__':
    main()
