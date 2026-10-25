"""Merge per-room benchmark reruns without promoting invalid samples to performance evidence."""
import argparse
import json
from pathlib import Path


def valid(row):
    return (row['Arrived'] and row['Control'] and row['Samples'] > 120 and row['RenderLuma'] > .005
            and (row['Phase'] != 'move' or row['Travel'] > 1))


def summarize(inputs):
    selected, rejected, metadata, errors = {}, [], [], []
    for path in inputs:
        data = json.loads(path.read_text(encoding='utf-8-sig'))
        ai = data.get('EnemyAI', 'Vanilla')
        metadata.append({k: v for k, v in data.items() if k not in ('Rows', 'Errors')})
        errors.extend({'File': str(path), 'Message': e} for e in data['Errors'])
        for row in data['Rows']:
            row = dict(row, AI=ai, Evidence=str(path))
            key = ai, row['Scene'], row['Phase'], str(path)
            if row['Phase'] != 'load' and not valid(row):
                rejected.append(row)
                continue
            selected[key] = row
    areas = list(dict.fromkeys(row['Area'] for row in selected.values()))
    summary = []
    for area in areas:
        row = {'Area': area}
        for ai in ('Vanilla', 'New'):
            samples = [r for r in selected.values() if r['Area'] == area and r['AI'] == ai and r['Phase'] != 'load']
            phases = {p: [r for r in samples if r['Phase'] == p] for p in ('idle', 'move')}
            row[ai] = None if not samples else {
                'Rooms': len({r['Scene'] for r in samples}),
                'IdleMinFps': min((r['Fps'] for r in phases['idle']), default=0),
                'MoveMinFps': min((r['Fps'] for r in phases['move']), default=0),
                'WorstP95Ms': max(r['P95Ms'] for r in samples),
                'WorstP99Ms': max(r['P99Ms'] for r in samples),
                'WorstMs': max(r['WorstMs'] for r in samples),
                'WorstScene': max(samples, key=lambda r: r['P95Ms'])['Scene'],
                'Slow50': sum(r['Slow50'] for r in samples),
                'Samples': sum(r['Samples'] for r in samples),
                'ModMeanMaxMs': max(r['ModMeanMs'] for r in samples),
                'GC0': sum(r['GC0'] for r in samples),
                'IdleCovered': bool(phases['idle']), 'MoveCovered': bool(phases['move'])}
        summary.append(row)
    completed_room_phases = {(r['AI'],r['Scene'],r['Phase']) for r in selected.values()}
    unresolved_rooms = [r for r in rejected if (r['AI'], r['Scene'], r['Phase']) not in completed_room_phases]
    unresolved = [r for r in unresolved_rooms if not any(v['AI']==r['AI'] and v['Area']==r['Area'] and v['Phase']==r['Phase'] for v in selected.values())]
    return dict(Sources=[str(p) for p in inputs], Metadata=metadata, Regions=summary,
                SelectedRows=list(selected.values()), RejectedSamples=rejected,
                UnresolvedInvalidSamples=unresolved, UnresolvedRoomSamples=unresolved_rooms, LoggedErrors=errors)


def main():
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument('--input', action='append', type=Path, required=True)
    ap.add_argument('--output', type=Path, required=True)
    ap.add_argument('--plot', action='store_true')
    args = ap.parse_args()
    result = summarize(args.input)
    args.output.mkdir(parents=True, exist_ok=True)
    (args.output/'region-summary.json').write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding='utf-8')
    rows = ['# 地区性能检查', '', '各数字取该地区有效房间样本的较差值。失败的跨场景、失去控制或未实际移动的采样不进入统计。', '',
            '| 地区 | 原版AI站立/移动FPS | 新版AI站立/移动FPS | 原版/新版P95毫秒 | >50ms帧 |',
            '|---|---:|---:|---:|---:|']
    for region in result['Regions']:
        v, n = region['Vanilla'], region['New']
        fps = lambda p: f"{p['IdleMinFps']:.0f} / {p['MoveMinFps']:.0f}" if p else '未测'
        p95 = lambda p: f"{p['WorstP95Ms']:.2f}" if p else '—'
        slow = sum(p['Slow50'] for p in (v,n) if p)
        rows.append(f"| {region['Area']} | {fps(v)} | {fps(n)} | {p95(v)} / {p95(n)} | {slow} |")
    rows += ['', '测试边界：RTX 5060 Laptop GPU、Ryzen 9 8940HX；1920×1080、关闭垂直同步、不锁帧、独立后台副本。逐帧强制绘制主相机，诊断时排除全屏 Masker Blackout 遮罩；未覆盖最终窗口呈现、所有UI、所有房间和完整Boss战。',
             '两种AI的代表房间和采样位置不完全一致，表格用于逐地区性能检查，不作为严格同场景同位置的AI性能对比。加载阶段单列，未混入站立/移动统计。', '',
             f"仍无同地区有效替代采样的项数：{len(result['UnresolvedInvalidSamples'])}。原房间未完成的采样：" + ', '.join(r['AI']+'/'+r['Scene']+'/'+r['Phase'] for r in result['UnresolvedRoomSamples']) + '。']
    (args.output/'regions.md').write_text('\n'.join(rows)+'\n', encoding='utf-8')
    if args.plot:
        import matplotlib
        matplotlib.use('Agg')
        import matplotlib.pyplot as plt
        from matplotlib.font_manager import FontProperties
        font = FontProperties(fname='C:/Windows/Fonts/msyh.ttc')
        regions = sorted(result['Regions'], key=lambda r: max((r[a]['WorstP95Ms'] for a in ('Vanilla','New') if r[a]), default=0), reverse=True)
        fig, ax = plt.subplots(figsize=(11, 12))
        y = list(range(len(regions)))
        for ai, offset, color, label in [('Vanilla',-.18,'#3b82b1','原版AI'),('New',.18,'#d98c33','新版AI')]:
            ax.barh([v+offset for v in y], [r[ai]['WorstP95Ms'] if r[ai] else 0 for r in regions], height=.32, color=color, label=label)
        ax.set_yticks(y, [r['Area'] for r in regions], fontproperties=font)
        ax.invert_yaxis(); ax.set_xlabel('P95 frame time (ms), lower is better')
        ax.axvline(1000/144, color='#777777', linestyle='--', linewidth=1, label='144 FPS budget (6.94 ms)')
        ax.axvline(1000/60, color='#b83a3a', linestyle=':', linewidth=1, label='60 FPS budget (16.67 ms)')
        ax.set_xlim(0,18); ax.grid(axis='x', alpha=.2); ax.set_axisbelow(True)
        ax.set_title('24地区性能检查：最差房间的P95帧耗时', fontproperties=font, fontsize=16, pad=20)
        ax.legend(prop=font, loc='lower right')
        fig.text(.17,.02,'1080p forced offscreen main-camera render; uncapped. Representative rooms, not full boss fights.',fontsize=8)
        fig.tight_layout(rect=(0,.04,1,1)); fig.savefig(args.output/'regional-performance.png',dpi=150); plt.close(fig)
    all_samples=[r for r in result['SelectedRows'] if r['Phase']!='load']
    print(json.dumps({'regions':len(result['Regions']), 'samples':sum(r['Samples'] for r in all_samples),
                     'slow50':sum(r['Slow50'] for r in all_samples), 'unresolved':len(result['UnresolvedInvalidSamples'])}))


if __name__ == '__main__':
    main()
