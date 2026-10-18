"""Audit every published drop row and reverse item link against the isolated export."""
import html
import json
import os
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
CATALOG = Path(os.environ.get('TK_WIKI_SOURCE_ROOT', ROOT))
OUT = ROOT/'artifacts/wiki-preview/terraknight-wiki'


def plain(value):
    return html.unescape(re.sub(r'<[^>]+>', '', value)).strip()


def main():
    runtime = json.loads((CATALOG/'tools/encyclopedia_runtime.json').read_text(encoding='utf-8'))['data']
    checked = 0
    for key in runtime['drops']:
        merged = {}
        for stage, records in runtime['drop_stages'].items():
            for drop in records[key]:
                signature = tuple(drop[k] for k in ('Type', 'Num', 'Den', 'Min', 'Max', 'Group', 'LockStage', 'Master')) + (round(drop['Probability'], 8),)
                if signature not in merged:
                    merged[signature] = dict(drop, Stages=[])
                merged[signature]['Stages'].append(int(stage))
        drops = list(merged.values())
        enemy_file = 'enemy-'+key.lower().replace('@', '-')+'.html'
        page = (OUT/enemy_file).read_text(encoding='utf-8')
        tables = [table for table in re.findall(r'<table>.*?</table>', page, re.S)
                  if '掉落物' in table.split('</thead>')[0]]
        assert bool(tables) == bool(drops), key
        if not drops:
            continue
        rows = re.findall(r'<tr>(.*?)</tr>', tables[0].split('<tbody>')[1], re.S)
        assert len(rows) == len(drops), (key, len(rows), len(drops))
        for row, drop in zip(rows, drops):
            cells = re.findall(r'<td[^>]*>(.*?)</td>', row, re.S)
            target = '/terraknight-wiki/item-'+drop['Type'].lower()+'.html'
            assert f'href="{target}"' in cells[0], (key, drop)
            count = str(drop['Min']) if drop['Min'] == drop['Max'] else f"{drop['Min']}–{drop['Max']}"
            assert plain(cells[2]) == count, (key, drop)
            assert plain(cells[3]) == str(drop['LockStage']), (key, drop)
            chance = plain(cells[1])
            if drop['Master']:
                assert '仅大师模式' in chance and '其他难度不掉落' in chance
            else:
                stages = sorted(drop['Stages'])
                label = f'{stages[0]}–{stages[-1]}' if len(stages)>1 and stages==list(range(stages[0],stages[-1]+1)) else '/'.join(map(str,stages))
                expected = f"阶段{label}：{100*drop['Probability']:.4g}%" + (f"（组{drop['Group']}互斥）" if drop['Group'] else '')
                assert chance == expected, (key, drop, chance, expected)
            item_page = (OUT/('item-'+drop['Type'].lower()+'.html')).read_text(encoding='utf-8')
            assert f'href="/terraknight-wiki/{enemy_file}"' in item_page, (key, drop['Type'])
            checked += 1
    pets = re.findall(r'\[ItemType\.(\w+)\]\s*=\s*new PetDef',
                      (CATALOG/'src/TerraKnight/Items/PetsData.cs').read_text(encoding='utf-8'))
    systems = (OUT/'systems.html').read_text(encoding='utf-8')
    for item in pets:
        assert f'href="/terraknight-wiki/item-{item.lower()}.html"' in systems
        item_page = (OUT/f'item-{item.lower()}.html').read_text(encoding='utf-8')
        assert '1/10000' in item_page and '×1' in item_page
    relic_source = (CATALOG/'src/TerraKnight/Items/Relics.cs').read_text(encoding='utf-8')
    if re.search(r'\bEnabled\s*=\s*false', relic_source):
        assert not any(d['Master'] for rows in runtime['drops'].values() for d in rows)
        for path in OUT.glob('item-relic*.html'):
            assert '当前构建已关闭大师圣物' in path.read_text(encoding='utf-8'), path.name
    report = {'enemy_tables': len(runtime['drops']), 'drop_rows': checked,
              'reverse_item_links': checked, 'pet_drop_sources': len(pets), 'status': 'passed'}
    (ROOT/'artifacts/wiki-drop-audit.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
    print(json.dumps(report))


if __name__ == '__main__':
    main()
