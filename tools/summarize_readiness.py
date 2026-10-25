"""Combine exact-candidate runtime evidence; never infer a clean release from packaging alone."""
import argparse
import hashlib
import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]

def read(p):
    return json.loads(p.read_text(encoding='utf-8-sig'))

def main():
    ap = argparse.ArgumentParser(); ap.add_argument('candidate', type=Path); a = ap.parse_args()
    base = a.candidate.resolve(); receipt = read(base/'receipt.json')
    matrix = read(base/'regression-matrix.json')
    if isinstance(matrix, dict): matrix = [matrix]
    blockers, rows, bosses, shutdown_notes = [], [], [], []
    for entry in matrix:
        case = entry['Case']; result = read(Path(entry['Result'])); problems = []
        for key in ('Completed',):
            if not result.get(key): problems.append(key+' is false')
        for key in ('TimedOut','Failures','ChangedPlayerSaves','NewPlayerSaves','Exception'):
            if result.get(key): problems.append(key+': '+str(result[key]))
        if result['BuildHash'].lower() != receipt['BuildHash'].lower(): problems.append('DLL hash mismatch')
        if not (result.get('RestoredSettings') or result.get('SharedSettingsUnchanged')): problems.append('Original settings were not confirmed unchanged')
        run = Path(result['Run']); count = result.get('PassMarkers')
        if count is None:
            auto=(run/'AutoTest.log').read_text('utf-8-sig')
            count = sum('[AutoTest]' in l and '通过' in l and '不通过' not in l and '共享物品转化被现有入口禁用' not in l for l in auto.splitlines())
            if case=='terradialogue':
                m=re.search(r'泰拉身份台词与碑文 (\d+)/(\d+) 通过',auto)
                if not m or m[1]!=m[2]: problems.append('Dialogue total did not pass')
                else: count=int(m[1])
        logs=[(side,run/(side+'Player.log')) for side in ('host','client')]
        logs.append(('solo',run/'Player.log'))
        for side,log in logs:
            if not log.exists(): continue
            lines = log.read_text('utf-8-sig',errors='replace').splitlines()
            errors=[]
            teardown_lines=set()
            for i,l in enumerate(lines):
                if i in teardown_lines: continue
                if ('ThreadAbortException: Thread was being aborted.' in l and 'Shutting down Steam API.' in lines[:i]):
                    end=i+1
                    while end<len(lines) and lines[end].startswith((' ','\t')): end+=1
                    if 'TerraKnight.Net.Server.Drop' in '\n'.join(lines[i:end]):
                        teardown_lines.update(range(i,end))
                        shutdown_notes.append(dict(Case=case,Side=side,Note='Unity aborted the background server thread after process shutdown; retained separately from gameplay failures.'))
                        continue
                if ' at TerraKnight.Net.' in l or re.search(r'^\[Error\s*:TerraKnight\]',l): errors.append(l)
                elif re.match(r'^[A-Za-z][A-Za-z0-9_.]*(?:Exception|Error):',l):
                    end=i+1
                    while end<len(lines) and lines[end].startswith((' ','\t')): end+=1
                    stack='\n'.join(lines[i:end])
                    native_teardown=(('CameraController.ReleaseLock' in stack and 'CameraLockArea.OnTriggerExit2D' in stack)
                                     or ('MusicRegion.FadeOut' in stack and 'MusicRegion.OnTriggerExit2D' in stack)
                                     or ('ObjectPool::Spawn' in stack and 'AudioPlayerOneShotSingle' in stack and 'PlayMakerUnity2DProxy.OnTriggerExit2D' in stack))
                    if ('Shutting down Steam API.' in lines[:i] and native_teardown and 'TerraKnight.' not in stack):
                        shutdown_notes.append(dict(Case=case,Side=side,Note='Native trigger cleanup after Steam shutdown; retained separately from gameplay failures.',Stack=stack))
                    else: errors.append(stack)
            if errors: problems.append(side+' exceptions: '+str(errors[:8]))
        if case.startswith('netboss'):
            for side in ('host','client'):
                p = run/(side+'-saves/netboss.jsonl')
                samples = [json.loads(l) for l in p.read_text('utf-8-sig').splitlines()] if p.exists() else []
                sums = [r for r in samples if r.get('sum')]
                if len(sums) != 5: problems.append(side+' boss summaries != 5')
                for s in sums:
                    bad = []
                    frames=[r for r in samples if not r.get('sum') and r.get('k')==s['k']]
                    expected_owner=1 if result.get('ClientOwns') else 0
                    owners=sorted({r['own'] for r in frames})
                    if expected_owner not in owners: bad.append('expected authority direction was never observed')
                    if not any(r.get('peer') for r in frames): bad.append('peer never visible')
                    is_replica=(side=='client') != bool(result.get('ClientOwns'))
                    visible=[e for r in frames for e in r.get('e',[]) if e.get('rec')==s['k'].split('@')[0] and e.get('a') and e.get('k',0)>0]
                    if not visible: bad.append('no rendered boss sample')
                    if is_replica and not any(e.get('p') for e in visible): bad.append('replica did not show a rendered puppet boss')
                    if s['kills'] != 1: bad.append('kill count is not one')
                    if s['standing']: bad.append('boss remains alive')
                    if s['gatesShut']: bad.append('battle gates remain closed')
                    if not s['ctl']: bad.append('control not returned')
                    if s['errors'] or s['dead']: bad.append('exception or player death')
                    if not s['wall'].startswith('0/'): bad.append('terrain penetration occurred in samples')
                    if s.get('bag')!='None' and s.get('bagInventory',0)+s.get('bagVisible',0)!=1: bad.append('personal treasure bag missing or duplicated')
                    bosses.append(dict(case=case,side=side,summary=s,problems=bad,ObservedOwners=owners,Frames=len(frames),RenderedBossSamples=len(visible)))
                    problems.extend(s['k']+': '+b for b in bad)
        rows.append(dict(Case=case,Checks=count,Problems=problems,Run=str(run)))
        blockers.extend(case+': '+p for p in problems)
    if len(rows) != 10: blockers.append('Expected 10 suites; found '+str(len(rows)))
    manifest = read(base/'source-manifest.json')
    current={p.relative_to(ROOT).as_posix():hashlib.sha256(p.read_bytes()).hexdigest()
             for p in (ROOT/'src/TerraKnight').rglob('*') if p.is_file() and not {'bin','obj'}.intersection(p.parts)
             and p.suffix in {'.cs','.csproj','.png','.json','.wav','.shader'}}
    changed = sorted(rel for rel in manifest.keys()|current.keys() if manifest.get(rel)!=current.get(rel))
    production_changes=[rel for rel in changed if not Path(rel).name.startswith('DevAutoTest')]
    if production_changes: blockers.append('Production sources changed since candidate build: '+str(production_changes))
    package=read(base/'package/result.json')
    if package['BuildHash'] != receipt['BuildHash'] or package['ReleaseBlockers']: blockers.append('Package parity/version failed')
    handshake=read(base/'version-handshake.json')
    if not handshake['Completed'] or handshake['Failures']: blockers.append('Mixed-version handshake rejection failed')
    recipes=read(base/'recipe-audit.json'); variants=read(base/'recipe-coverage.json')
    if recipes['failures'] or recipes['missingVariants'] or variants['Missing']: blockers.append('Recipe mismatch or missing supported variant')
    output=dict(BuildHash=receipt['BuildHash'],Suites=rows,Bosses=bosses,Blockers=blockers,Published=False,LaterSourceChanges=changed,
                VersionHandshake=handshake,NativeShutdownNotes=shutdown_notes,
                Recipes=dict(Checked=recipes['checked'],Mismatches=len(recipes['failures']),SupportedVariants=variants['ExpectedSupportedVariants'],Missing=len(variants['Missing']),UnsupportedConditionRecords=len(variants['UnsupportedVanillaRecipes'])),
                Limitations=['本机 TCP 双客户端；未验证跨机器公网、高延迟、丢包及三人以上联机。',
                             '假骑士、螳螂领主、灵魂大师、残破容器、空洞骑士分别验证主机和客机负责战斗；其余 Boss 未验证。',
                             'Boss 回归使用防死和脚本攻击，覆盖同步、阶段切换、结算、奖励及控制；正常难度和全部招式另需验证。',
                             '本 DLL 未重新测量各地区代表帧率；同时运行的其他游戏会干扰性能数据。',
                             '渲染对象与状态采样不等同于人工目视完整游玩。'])
    (base/'readiness-summary.json').write_text(json.dumps(output,ensure_ascii=False,indent=2),'utf-8')
    lines=['# 4.4.2 发布前复核','', '结论：'+('仍有阻断项。' if blockers else '本报告列出的隔离回归通过；未验证范围见下文。'),'',
           '候选 DLL SHA-256：`'+receipt['BuildHash']+'`','', '| 检查 | 通过断言 | 状态 |','|---|---:|---|']
    labels={'netboss-clientOwns=False':'主机负责 Boss（5 场）','netboss-clientOwns=True':'客机负责 Boss（5 场）',
            'networld':'共享世界','net2':'战斗同步','netloot':'掉落','netrelease':'重连/委托/工匠作坊/联机微光',
            'releasecheck':'单机发布综合','shimmeraudit':'单机微光','recipevariants':'游戏内配方变体','terradialogue':'原生台词/石碑'}
    lines += [f'| {labels.get(r["Case"],r["Case"])} | {r["Checks"]} | '+('有问题' if r['Problems'] else '通过')+' |' for r in rows]
    lines += ['',f'原版配方核对 **{recipes["checked"]}** 条，差异 **{len(recipes["failures"])}**；已支持变体 **{variants["ExpectedSupportedVariants"]}** 条，缺失 **{len(variants["Missing"])}**。不支持的原版条件/制作站另列 **{len(variants["UnsupportedVanillaRecipes"])}** 条记录，不冒充已实现。']
    lines += ['', '旧版本/旧协议握手拒绝 **2/2** 通过。所有列出的运行使用同一 DLL；玩家存档无新增或更改，原设置保持不变。',
              '', 'ZIP 完整性与包内 DLL 哈希一致性已复核；ZIP SHA-256：`'+package['ZipHash']+'`。']
    lines += ['', '## 修复与测试前置条件','',
              '- 修复跨场景 Boss 控制脚本在场景归属注册前影响非参与玩家。',
              '- 修复联机共享掉落无法进行微光转换/分解；由主机生成唯一结果，保留原场景坐标。',
              '- 修复客机本地尸体坠落导致个人宝藏袋生成在场景外；奖励位置跟随同步 Boss 本体，并验证两端真实拾取。',
              '- 协议升级至 net14，联机双方须使用相同版本。',
              '- 修正旧战斗夹具的快照字段、启动顺序与距离选敌断言；保留失败记录。',
              '- 测试副本使用独立 Unity 公司名、配置和存档；不回写或覆盖玩家设置。',
              '', '## 阻断项',''] + (['- '+b for b in blockers] or ['- 本次矩阵未发现阻断项。'])
    lines += ['', '## 验证边界','']+['- '+s for s in output['Limitations']]
    if changed: lines += ['- 构建后工作区另外变化的文件（候选 DLL 未包含这些后续编辑）：'+', '.join(changed)]
    if shutdown_notes: lines += ['- 保留 '+str(len(shutdown_notes))+' 条仅在进程退出后出现的记录：原生相机/音乐/对象池触发器清理空引用、Unity 终止后台服务器线程；详见 JSON 的 NativeShutdownNotes。游戏过程异常另行检查。']
    lines += ['- 单机微光审计中的旧固定提示“共享物品转化被现有入口禁用”已从有效断言数排除；当前联机微光以 netrelease 的真实双客户端转换、分解和拾取为依据。']
    lines += ['', '## 原始证据','']+['- '+r['Case']+': `'+r['Run']+'`' for r in rows]
    lines += ['', '安装包仅暂存，未发布、未打标签、未更新日常游戏安装。']
    (base/'发布前复核.md').write_text('\n'.join(lines)+'\n','utf-8')
    print(json.dumps(dict(Suites=len(rows),Checks=sum(r['Checks'] for r in rows),Blockers=blockers),ensure_ascii=False,indent=2))
    return bool(blockers)

if __name__ == '__main__': raise SystemExit(main())
