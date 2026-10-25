"""Compare every item and every difficulty/stage enemy row with fresh runtime data."""
import hashlib
import html
import json
import os
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SOURCE = Path(os.environ.get('TK_WIKI_SOURCE_ROOT', ROOT/json.loads((ROOT/'wiki-source.json').read_text('utf-8')).get('snapshot','.')))
OUT = ROOT/'artifacts/wiki-preview/terraknight-wiki'

def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))

def plain(value):
    return html.unescape(re.sub('<[^>]*>', '', value))

def compact(value):
    return re.sub(r'\s+', '', plain(value))

def main():
    runtime = read(SOURCE/'tools/encyclopedia_runtime.json')
    for rel, digest in runtime['source_hashes'].items():
        assert hashlib.sha256((SOURCE/'src/TerraKnight'/rel).read_bytes()).hexdigest() == digest, rel
    data = runtime['data']
    health = read(SOURCE/'tools/encyclopedia_enemy_health.json')['records']
    index = read(OUT/'search-index.json')
    expected = {f'item-{t.lower()}' for t,d in data['items'].items() if d['Name'] != t}
    assert {e['id'] for e in index if e['kind']=='物品'} == expected
    assert {e['id'] for e in index if e['kind']=='怪物'} == {'enemy-'+k.lower().replace('@','-') for k in data['drops']}
    assert set(health) == set(data['drops'])
    items = accessories = rows_checked = 0
    unresolved_accessories = []
    for key, item in data['items'].items():
        if item['Name'] == key:
            continue
        page = (OUT/f'item-{key.lower()}.html').read_text(encoding='utf-8')
        text = compact(page)
        assert compact(item['Name']) in text, key
        assert f'所需阶段{item["Stage"]}' in text, key
        tooltip = item.get('Tooltip') or ''
        if tooltip and not key.startswith('Relic'):
            assert compact(html.escape(tooltip)) in text, (key, 'tooltip')
        stats = next((compact(p) for p in re.findall(r'<p>.*?</p>', page, re.S) if '基础属性' in p), '')
        for field,label in [('Damage','伤害'),('Defense','防御'),('Mana','耗魔'),('PickPower','镐力'),('HealLife','恢复生命'),('HealMana','恢复魔力')]:
            if item.get(field, 0):
                assert re.search(label+str(item[field])+r'(?:；|。|$)',stats), (key,field,item[field],stats)
        if item.get('Accessory','None') != 'None':
            accessories += 1
            if '当前注册数据未找到普通流程的明确获取路径' in text:
                unresolved_accessories.append(key)
        items += 1
    labels = {'Normal':'普通','Hard':'困难','Expert':'专家','Master':'大师','Hollow':'空洞'}
    for key, record in health.items():
        page = (OUT/('enemy-'+key.lower().replace('@','-')+'.html')).read_text(encoding='utf-8')
        table = next(t for t in re.findall(r'<table>.*?</table>',page,re.S) if '接触伤害参考' in plain(t.split('</thead>')[0]))
        actual = [[compact(c) for c in re.findall(r'<td[^>]*>(.*?)</td>',row,re.S)] for row in re.findall(r'<tr>(.*?)</tr>',table.split('<tbody>')[1],re.S)]
        expected_rows=[]
        for d in record['difficulties']:
            p=next(p for p in d['stage_stats'] if p['stage']==record['stage'])
            expected_rows.append([labels[d['difficulty']],f"{p['life']:,}",str(p['damage']),str(p['defense'])])
        assert actual == expected_rows, (key,actual,expected_rows)
        rows_checked += len(actual)
        if any(len({(s['life'],s['damage'],s['defense']) for s in d['stage_stats']})>1 for d in record['difficulties']):
            table=next(t for t in re.findall(r'<table>.*?</table>',page,re.S) if '能力阶段' in plain(t.split('</thead>')[0]))
            actual=[[compact(c) for c in re.findall(r'<td[^>]*>(.*?)</td>',row,re.S)] for row in re.findall(r'<tr>(.*?)</tr>',table.split('<tbody>')[1],re.S)]
            expected_rows=[[str(p['stage']),labels[d['difficulty']],f"{p['life']:,}",str(p['damage']),str(p['defense'])] for d in record['difficulties'] for p in d['stage_stats']]
            assert actual==expected_rows,key
            rows_checked+=len(actual)
    shop_pairs = set()
    npc_names = {}
    for rel in ['Items/TownShopData.cs','Town/TownNpcsMore.cs']:
        text = (SOURCE/'src/TerraKnight'/rel).read_text(encoding='utf-8-sig')
        npc_names.update(re.findall(r'new NpcDef\(\d+,\s*"(\w+)",\s*"([^"]+)"', text))
    for stocks in data['shop_stages'].values():
        for stock in stocks:
            npc = {'TravelingMerchant':'TravellingMerchant'}.get(stock['Npc'], stock['Npc'])
            shop_pairs.add((npc, stock['Type']))
    for npc, item in shop_pairs:
        entry = next(e for e in index if e['kind']=='NPC' and e['title']==npc_names[npc])
        npc_page = (OUT/(entry['id']+'.html')).read_text(encoding='utf-8')
        assert f'href="/terraknight-wiki/item-{item.lower()}.html"' in npc_page, (npc,item)
        item_page = (OUT/f'item-{item.lower()}.html').read_text(encoding='utf-8')
        assert '商店：' in plain(item_page), (npc,item)
        if npc == 'TravellingMerchant':
            assert '不保证每次出售' in item_page, item
    final_npcs = read(SOURCE/'tools/encyclopedia_details.json')['npcs']
    for npc in final_npcs:
        entry = next(e for e in index if e['kind']=='NPC' and e['title']==npc['Zh'])
        assert entry['stage'] == str(npc['Stage']), ('NPC arrival override', npc['Internal'], entry['stage'], npc['Stage'])
    report = dict(status='passed',items=items,accessories=accessories,enemies=len(health),difficulty_stage_stat_rows=rows_checked,
                  runtime_npc_arrival_stages=len(final_npcs),
                  shop_inventory_pairs=len(shop_pairs),
                  unresolved_accessory_sources=unresolved_accessories,
                  note='Unknown acquisition paths are explicitly marked on item pages; simulated fishing/chest/crate frequencies are labeled as samples, not exact probabilities.')
    (ROOT/'artifacts/wiki-current-catalog-audit.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps(report,ensure_ascii=False))

if __name__ == '__main__':
    main()
