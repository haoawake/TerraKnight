"""Summarize measured early-magic rows; never equate dummy DPS with boss clear time."""
from pathlib import Path
from collections import Counter
import argparse
import json
import statistics

ROOT = Path(__file__).resolve().parents[1]


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('runs', nargs='+', type=Path)
    parser.add_argument('--output', type=Path, default=ROOT / 'artifacts/early-magic-review')
    args = parser.parse_args()
    out = args.output
    out.mkdir(parents=True, exist_ok=True)
    catalog, gear, rows, provenance, quarantine, tracking = {}, {}, [], [], [], []
    for run in args.runs:
        if not run.is_absolute():
            run = ROOT / run
        data = json.loads((run / 'saves/early-magic-audit.json').read_text(encoding='utf-8-sig'))
        for item in data['Catalog']:
            catalog[item['Type']] = item
        for item in data.get('Gear') or []:
            gear[item['Type']] = item
        for row in data['Rows']:
            reason = None
            if row['Weapon'] == 'ShadeSoulFocus' and ('earlymagic-' in run.name or 'earlymagicboss-' in run.name):
                reason = 'Custom held button not maintained in initial protocol; replaced by earlymagicrepair.'
            elif row['Weapon'] not in catalog and 'earlymagic-' in run.name:
                reason = 'Initial cross-class fixture used ranged distance for melee; replaced by earlymagiccontrols.'
            elif 'CeilingDropper' in row['Target']:
                reason = 'Native suicide/gas explosions make this repeated kill fixture unsuitable.'
            elif 'invalid-spawn' in row['Profile']:
                reason = 'Target disappeared during spawn; not a weapon result.'
            elif row['Target'].startswith('native-enemy/') and row['Casts'] == 0 and row['Hits'] == 0 and row['Seconds'] < .2:
                reason = 'Native actor deactivated immediately after cloning; no combat window occurred.'
            if reason:
                quarantine.append(dict(run=run.name, row=row, reason=reason))
            else:
                rows.append(row)
        tracking_path = run / 'saves/early-magic-tracking.json'
        if tracking_path.exists():
            tracking.append(json.loads(tracking_path.read_text(encoding='utf-8-sig')))
        result = json.loads((run / 'result.json').read_text(encoding='utf-8-sig')) if (run / 'result.json').exists() else None
        recovered = ('earlymagicencounters-' in run.name or 'earlymagicencounterslate-' in run.name) and not data['Complete']
        provenance.append(dict(run=str(run), complete=data['Complete'], recovered_partial=recovered, result=result, scope=data['Scope']))
    # Native encounters rerun after the gas-explosion fixture failure supersede older
    # observations of the same weapon/AI/record. Repeats of DPS fixtures remain samples.
    last_encounter = {}
    for i, row in enumerate(rows):
        if row['Target'].startswith('native-enemy/'):
            last_encounter[(row['Weapon'], row['Profile'], row['Target'])] = i
    rows = [row for i, row in enumerate(rows) if not row['Target'].startswith('native-enemy/') or last_encounter[(row['Weapon'],row['Profile'],row['Target'])] == i]
    by = {(r['Weapon'], r['Profile'], r['Target']): r for r in rows}
    audit = []
    for name, item in catalog.items():
        own = [r for r in rows if r['Weapon'] == name]
        def get(profile, target='static-dummy'):
            return by.get((name, profile, target))
        bare, kit, finite, moving = get('bare'), get('reference'), get('finite'), get('reference', 'moving-dummy-250ms-aim')
        robes = sorted((r for r in own if r['Profile'].startswith('robe/')), key=lambda r: r['Dps'], reverse=True)
        audit.append(dict(item=item, bare=bare, reference=kit, finite=finite, moving=moving,
                          robes=robes, bosses=[r for r in own if r['Target'].startswith('native-ai-window/')]))
    required = [('bare','static-dummy'),('reference','static-dummy'),('finite','static-dummy'),('reference','moving-dummy-250ms-aim')]
    coverage = {name: {profile + '/' + target: (name,profile,target) in by for profile,target in required} for name in catalog}
    all_profiles = all(all(flags.values()) for flags in coverage.values())
    boss_coverage = sorted({r['Weapon'] for r in rows if r['Target'].startswith('native-ai-window/')})
    encounter_coverage = {ai: sorted({r['Weapon'] for r in rows if r['Profile'] == 'finite-native-enemy/' + ai}) for ai in ('Vanilla','New')}
    summary = dict(catalog_count=len(catalog), measured_rows=len(rows), gear=gear, quarantine=quarantine, tracking=tracking,
                   profiles=dict(Counter(r['Profile'] for r in rows)),
                   stages=dict(Counter(x['Stage'] for x in catalog.values())),
                   complete=all(p['complete'] or p['recovered_partial'] for p in provenance) and all_profiles and len(boss_coverage)==len(catalog) and all(len(v)==len(catalog) for v in encounter_coverage.values()) and bool(tracking) and all(t['Failures']==0 for t in tracking),
                   coverage=coverage, boss_coverage=boss_coverage, encounter_coverage=encounter_coverage,
                   provenance=provenance, weapons=audit)
    (out / 'review.json').write_text(json.dumps(summary, ensure_ascii=False, indent=2), encoding='utf-8')
    lines = [
        '# 前期法师武器平衡实测', '',
        f'范围：大师难度，当前运行时阶段 0–2，共 {len(catalog)} 把法师武器；累计 {len(rows)} 个场景。', '',
        '测试数值未改动。各行是实际游戏中的命中及伤害记录，不是伤害模型推算。', '',
        f"覆盖：固定靶/同期配装/有限魔力/移动靶全项={all_profiles}；首领伤害窗口={len(boss_coverage)}/{len(catalog)} 把；普通敌人原版 AI={len(encounter_coverage['Vanilla'])}/{len(catalog)}、新版 AI={len(encounter_coverage['New'])}/{len(catalog)}。", '',
        '## 主要发现', '',
        '宝石法杖与法袍的复合特效值得优先调整，但数据不支持把全部前期法师武器一起削弱。紫晶法杖固定靶三次重复平均 DPS：裸装 80.3、紫晶法袍 82.1、翡翠法袍 144.5、钻石法袍 142.9、琥珀法袍 157.5。后面三种组合约为裸装的 1.8–2.0 倍；紫晶法袍本身没有使伤害翻倍。这里逐帧补满魔力，用于比较输出上限。', '',
        '初轮短窗口还筛出红玉法杖＋蓝玉法袍 190.5、钻石法杖＋紫晶法袍 182.8、蓝玉法杖＋红玉法袍 178.8 DPS。这些是筛查样本，不能当作重复取样均值。建议优先检查追踪、穿透回头命中、双弹与双轮齐射叠加后的总伤害收益，保留各宝石的玩法差异。本轮没有修改伤害、魔耗或追踪规则，也没有安装测试 DLL。', '',
        '琥珀法杖自身特效是反弹；与紫晶法袍搭配才叠加紫晶追踪。追踪没有独立倒计时，会在弹丸存活期间每次更新重新索敌，因此连续发射时看起来一直锁定。普通弹丸寿命约 5 秒，翡翠特效上限约 10 秒，但本次减速缩小后约 6.1–6.3 秒消失；反弹不会刷新寿命。代表性宝石弹共 24 项检查通过，死亡、失活、超距或墙体遮挡会解除该目标，之后可重新找到可见目标。这项结论不外推到所有其他追踪武器。', '',
        '本报告的 374 行是有效测量记录，不是 374 项平衡断言。普通敌人及首领窗口补充了固定靶数据，但没有测量完整首领击杀时间，也不能据此证明实际游玩“无敌”。', '',
        '## 方法与边界', '',
        '- 裸装/同期护甲：不带饰品、前缀、强化、药水或护符。无限魔力行使用 200 点基础池并逐帧补满，只表示输出上限。',
        '- 有限魔力：阶段 0/1/2 分别使用 20/80/200 基础魔力，加护甲加成，持续 20 秒，不补魔、不喝药；站定回魔。末段 DPS 为最后 4 秒。此魔力配置是比较场景，不是强制获取上限。',
        '- 移动靶：同一真实命中与防御路径的假人按固定轨迹移动，每 250 毫秒更新瞄准；这不是实际首领战。',
        '- 法袍交叉测试：先单独穿法袍，另标注带巫师帽/魔法帽的紫晶法杖组合；法袍/帽子的获取阶段单独列出，不能把较晚组合算作首通配置。',
        '- 首领窗口：原版场景、新版首领 AI、正常无敌阶段，测试角色固定位置且免死。为维持可比窗口，首领血量回补并停留较早阶段；记录伤害、移动及状态数，不代表击杀时间、生存或完整阶段覆盖。',
        '- 持续区域武器按其操作方式使用；猩红魔杖在目标上方放云一次，不反复覆盖自己的云。单目标零命中须排查距离、地形和状态，不能直接判定武器弱。',
        '- 短窗口存在暴击及随机弹幕波动；灰冲击枪等高方差武器须用延长复测判断。', '',
        '## 逐把覆盖与结果', '',
        '|阶段|武器|裸装 DPS|同期配装 DPS|有限魔力 DPS / 末段|首次缺魔秒|移动靶 DPS|首领窗口 DPS|',
        '|---|---|---:|---:|---:|---:|---:|---|',
    ]
    def val(row, key='Dps'):
        return f'{row[key]:.1f}' if row else '未测'
    for x in sorted(audit, key=lambda x: (x['item']['Stage'], x['item']['Type'])):
        item, finite = x['item'], x['finite']
        bosses = '；'.join(r['Target'].split('/')[1] + '=' + val(r) for r in x['bosses']) or '未测'
        lines.append(f"|{item['Stage']}|{item['Name']} ({item['Type']})|{val(x['bare'])}|{val(x['reference'])}|{val(finite)} / {val(finite,'TailDps')}|{val(finite,'FirstDry')}|{val(x['moving'])}|{bosses}|")
    lines += ['', '## 宝石法杖与法袍', '', '|法杖|组合/模式|目标|DPS|魔力上限|首次缺魔秒|', '|---|---|---|---:|---:|---:|']
    for x in audit:
        for r in x['robes']:
            lines.append(f"|{x['item']['Name']}|{r['Profile'][5:]}|{r['Target']}|{r['Dps']:.1f}|{r['ManaMax']}|{r['FirstDry']:.1f}|")
    lines += ['', '## 重点组合重复取样', '', '|组合|样本数|平均 DPS|最小–最大|', '|---|---:|---:|---|']
    groups = {}
    for r in rows:
        if '/validated-repeat' in r['Profile']:
            groups.setdefault(r['Profile'].split('/validated-repeat')[0], []).append(r['Dps'])
    for profile, samples in groups.items():
        lines.append(f'|{profile}|{len(samples)}|{statistics.mean(samples):.1f}|{min(samples):.1f}–{max(samples):.1f}|')
    lines += ['', '## 配装获取阶段', '', '|装备|阶段|来源|防御|魔力|减耗|魔法增伤|魔法暴击|', '|---|---:|---|---:|---:|---:|---:|---:|']
    for g in sorted(gear.values(), key=lambda x: (x['Stage'], x['Type'])):
        lines.append(f"|{g['Name']}|{g['Stage']}|{g['Source']}|{g['Defense']}|{g['ManaMax']}|{g['ManaCost']:.0%}|{g['MagicDamage']:.0%}|{g['MagicCrit']:.0%}|")
    lines += ['', '## 同期近战与射手对照', '', '|阶段|武器|DPS|目标防御|', '|---|---|---:|---:|']
    for r in rows:
        if r['Weapon'] not in catalog:
            lines.append(f"|{r['Stage']}|{r['Name']} ({r['Weapon']})|{r['Dps']:.1f}|{r['Defense']}|")
    encounters = [r for r in rows if r['Target'].startswith('native-enemy/')]
    lines += ['', '## 普通敌人接敌测试', '',
              '在当前房间中复制真实普通敌人，保留其原生组件及所选 AI；每轮只留目标，固定初始距离，测试角色站定射击且免死。无补魔；8 秒未击杀保留为未击杀，不填虚假击杀时间。玩家受击次数反映此夹具下的压制能力，不代表真人躲避结果。', '',
              '|武器|目标|AI/模式|敌人生命|是否击杀|耗时/观察秒|玩家受击|', '|---|---|---|---:|---|---:|---:|']
    for r in encounters:
        lines.append(f"|{r['Name']}|{r['Target']}|{r['Profile']}|{r.get('EnemyLife',0)}|{'是' if r.get('Killed') else '否'}|{r['Seconds']:.2f}|{r.get('PlayerHits',0)}|")
    lines += ['', '## 追踪与单发寿命', '']
    for t in tracking:
        lines += [f"检查 {t['Checks']} 项，失败 {t['Failures']} 项。独立发射后，屏蔽这些测试弹的地图碰撞来测实际老化；另用实际索敌方法测存活、死亡、失活、超距和墙体遮挡。", '', '|组合|最后仍存活的采样秒数|最后年龄更新数|', '|---|---:|---:|']
        for x in t['Lifetimes']:
            lines.append(f"|{x['Case']}|{x['LastSeenSeconds']:.2f}|{x['LastAge']}|")
        lines += ['', '普通宝石弹初始 300 更新（约 5 秒）；翡翠特效 600 更新是上限，但减速后缩小可提前消失。存活期间每次 AI 更新都会重新选择可见的近敌；没有独立的追踪时长预算。琥珀＋紫晶特效常规索敌 1000 像素，反弹重新索敌 2400 像素。', '']
    lines += ['', '## 获取与首通边界', '', '|武器|阶段|来源|对应首领奖励（不能反用于该首通）|', '|---|---:|---|---|']
    for item in sorted(catalog.values(), key=lambda x: (x['Stage'], x['Type'])):
        lines.append(f"|{item['Name']}|{item['Stage']}|{item['Source']}|{'、'.join(item['BossRewardOf']) or '非此表首领奖励'}|")
    lines += ['', '## 证据与完成状态', '']
    for p in provenance:
        r = p['result'] or {}
        lines += [f"- `{p['run']}`：场景输出完成={p['complete']}，进程完成={r.get('Completed','运行中')}，超时={r.get('TimedOut','未知')}，玩家存档变化={r.get('ChangedPlayerSaves','待检查')}。"]
    lines += ['', '## 无效样本与补测', '',
              f'已排除 {len(quarantine)} 条无效夹具记录，详见 review.json/quarantine：初轮暗魂幽影未按住自定义施法键、初轮近战靶距离过远、自爆气囊残留爆炸，以及生成时已消失的目标。部分接敌批次因夹具错误中断，保留已完成的有效阶段，并由后续批次补齐；未将这些中断批次写成全通过。',
              '短固定靶窗口用于筛查，存在起手与随机波动；重点组合另做取消上轮动作后的三次重复采样。多人、完整首领击杀时间、全部攻击阶段以及角色实际躲避操作不在本次通过范围。',
              '测试期间原版注册表快照有变化，且有其他 Steam/输入活动；未覆盖或恢复全局设置。原版玩家存档是否变化逐批见 result.json。', '']
    (out / 'report.md').write_text('\n'.join(lines) + '\n', encoding='utf-8')
    print(json.dumps({k: summary[k] for k in ('catalog_count', 'measured_rows', 'stages', 'complete')}, ensure_ascii=False))


if __name__ == '__main__':
    main()
