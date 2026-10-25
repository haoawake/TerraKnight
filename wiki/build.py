"""Build a complete static wiki from the reviewed local encyclopedia."""
from __future__ import annotations
import collections
import hashlib
import html
import json
import re
import shutil
import zipfile
import time
import os
from datetime import datetime
from zoneinfo import ZoneInfo
from pathlib import Path
from urllib.parse import quote
import markdown
from world_map_page import content as world_map_content
from knowledge import load as load_knowledge
from player_content import apply as player_content, player_release_notes

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'artifacts/wiki-preview/terraknight-wiki'
BASE = '/terraknight-wiki/'
_active = json.loads((ROOT/'wiki-source.json').read_text('utf-8')) if (ROOT/'wiki-source.json').exists() else {}
_snapshot = ROOT / _active.get('snapshot', '.')
CATALOG_ROOT = Path(os.environ.get('TK_WIKI_SOURCE_ROOT', _snapshot if _snapshot.is_dir() else ROOT)).resolve()
SOURCE = CATALOG_ROOT / '泰拉圣巢本地百科全书.md'
VERSION = json.loads((CATALOG_ROOT/'wiki-source.json').read_text(encoding='utf-8')) if (CATALOG_ROOT/'wiki-source.json').exists() else {}
TITLE = '空洞泰拉wiki'
RELEASES_FILE = ROOT / 'wiki/releases.json'
BUILD = hashlib.sha256(SOURCE.read_bytes() + RELEASES_FILE.read_bytes() + json.dumps(VERSION, sort_keys=True).encode('utf-8') + Path(__file__).read_bytes() + (ROOT/'wiki/knowledge.py').read_bytes() + b''.join(p.read_bytes() for p in sorted((CATALOG_ROOT/'tools').glob('encyclopedia_knowledge*.json'))) + b''.join(p.read_bytes() for p in sorted((ROOT/'wiki/assets').glob('*')) if p.is_file()) + (ROOT/'wiki/world_map_page.py').read_bytes() + (ROOT/'artifacts/wiki-world-map/map.json').read_bytes() + (ROOT/'artifacts/wiki-illustrations/manifest.json').read_bytes()).hexdigest()[:12]
BUILD = hashlib.sha256((BUILD + (ROOT/'wiki/player_content.py').read_text('utf-8')).encode('utf-8')).hexdigest()[:12]
NAV = [('knowledge', '全部资料'), ('map', '世界地图'), ('progress', '阶段与解锁'), ('items', '物品图鉴'), ('enemies', '怪物图鉴'),
       ('npcs', 'NPC 与商店'), ('recipes', '合成配方'), ('fishing', '钓鱼与宝匣'), ('casino', '赌博概率'), ('dialogue', '全部台词'), ('enemy-ai', '攻击与打法'), ('boss-summons', 'Boss 召唤物'), ('systems', '玩法系统'), ('changelog', '更新日志')]


def plain(value):
    value = re.sub(r'<[^>]+>', '', value)
    value = re.sub(r'\[([^\]]+)\]\([^)]+\)', r'\1', value)
    return re.sub(r'[`*#|_]', '', value).strip()


table_serial = 0


def render(value):
    result = markdown.markdown(value, extensions=['tables', 'fenced_code', 'toc'],
                               extension_configs={'toc': {'permalink': False}})
    def bounded(match):
        global table_serial
        table = match[0]
        tbody = re.search(r'<tbody>(.*?)</tbody>', table, re.S)
        if not tbody:
            return '<div class="table-scroll" tabindex="0">'+table+'</div>'
        rows = re.findall(r'<tr>.*?</tr>', tbody[1], re.S)
        if len(rows) <= 48 or 'data-preserve-table' in table:
            return '<div class="table-scroll" tabindex="0">'+table+'</div>'
        table_serial += 1
        directory = OUT/f'data/tables/{table_serial}'
        directory.mkdir(parents=True, exist_ok=True)
        for offset in range(0, len(rows), 24):
            (directory/f'{offset//24}.json').write_text(json.dumps(rows[offset:offset+24],ensure_ascii=False,separators=(',',':')),'utf-8')
        table=table[:tbody.start(1)]+''.join(rows[:24])+table[tbody.end(1):]
        return f'<div class="table-scroll" tabindex="0" data-chunk-table="{table_serial}" data-total="{len(rows)}">{table}</div><noscript>完整表格可下载页脚百科；启用脚本可翻页。</noscript>'
    # Recipes have their own 40-row chunker, below.
    if value.startswith('<!-- recipe-table -->'):
        return re.sub(r'(<table>.*?</table>)', r'<div class="table-scroll" tabindex="0">\1</div>',result,flags=re.S)
    return re.sub(r'<table>.*?</table>', bounded, result, flags=re.S)


def shell(title, content, active='', description='', toc=''):
    nav = ''.join(f'<a class="{"active" if active == key else ""}" href="{BASE}{key}.html">{label}</a>' for key, label in NAV)
    safe_title = html.escape(title)
    desc = html.escape(description or '空洞泰拉游戏百科：全物品获取、怪物实际血量、合成配方、NPC 商店与阶段解锁。', quote=True)
    version_notice = (f'{html.escape(VERSION.get("version", ""))} 本地更新版 · 游戏资料已同步'
                      if VERSION.get('kind') == 'workspace-snapshot' else
                      f'已发布版本 <a href="https://github.com/haoawake/TerraKnight/releases/tag/{html.escape(VERSION.get("tag", "v" + VERSION.get("version", "")))}">{html.escape(VERSION.get("version", ""))}</a> · 物品、配方、掉落与游戏图标已同步')
    notice_html = '' if active == 'changelog' else f'<div class="version-notice">{version_notice}</div>'
    return f'''<!doctype html>
<html lang="zh-CN" data-wiki-build="{BUILD}"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>{safe_title}{' · '+TITLE if title != TITLE else ''}</title><meta name="description" content="{desc}">
<meta name="theme-color" content="#10201e"><link rel="icon" href="{BASE}assets/crest.svg" type="image/svg+xml">
<link rel="stylesheet" href="{BASE}assets/wiki.css?v={BUILD}"><script src="{BASE}assets/wiki.js?v={BUILD}" defer></script></head>
<body><a class="skip" href="#main">跳到正文</a><header class="topbar"><a class="brand" href="{BASE}"><img src="{BASE}assets/crest.svg" width="34" height="34" alt=""><span>空洞泰拉<span class="brand-tag">WIKI</span></span></a>
<nav aria-label="主导航">{nav}</nav><a class="search-link" href="{BASE}search.html">搜索百科 <kbd>/</kbd></a></header>
<main id="main">{notice_html}{content}</main><footer><a href="{BASE}">空洞泰拉wiki</a><span>当前版本游戏资料 · <a href="{BASE}audit.html">数据来源</a> · <a href="{BASE}downloads/encyclopedia.md">下载离线百科</a></span><a href="#main">回到顶部 ↑</a></footer>
<div class="toast" role="status" aria-live="polite"></div></body></html>'''


def main():
    if OUT.exists():
        assert OUT.resolve() == (ROOT / 'artifacts/wiki-preview/terraknight-wiki').resolve()
        OUT.rename(OUT.parent / ('previous-wiki-' + str(time.time_ns())))
    OUT.mkdir(parents=True, exist_ok=True)
    (OUT / 'assets').mkdir(exist_ok=True)
    (OUT / 'downloads').mkdir(exist_ok=True)
    for asset in (ROOT / 'wiki/assets').iterdir():
        shutil.copyfile(asset, OUT / 'assets' / asset.name)
    atlas = ROOT / 'artifacts/wiki-world-map'
    if not (atlas / 'map.json').is_file():
        raise FileNotFoundError('Run wiki/prepare_world_map.py with the game worldatlas.bin first')
    shutil.copytree(atlas, OUT / 'assets/world-map', dirs_exist_ok=True)
    (OUT / 'map.html').write_text(shell('圣巢全境地图', world_map_content(BASE, BUILD), 'map'), encoding='utf-8')
    map_data = json.loads((atlas / 'map.json').read_text(encoding='utf-8'))
    map_links = collections.defaultdict(list)
    for boss in map_data['bosses']:
        map_links[boss['url']].append(boss)
    illustrations = ROOT / 'artifacts/wiki-illustrations'
    if illustrations.is_dir():
        shutil.copytree(illustrations, OUT / 'assets/illustrations', dirs_exist_ok=True)
        packed = {}
        for key,row in json.loads((illustrations/'manifest.json').read_text('utf-8')).items():
            if row['kind'] not in packed:
                packed[row['kind']] = {k:row[k] for k in ['sheet','cell','sheet_width','sheet_height']} | {'entries':[]}
            packed[row['kind']]['entries'].append([key,row['x'],row['y']])
        (OUT/'assets/illustrations/manifest-lite.json').write_text(json.dumps(packed,ensure_ascii=False,separators=(',',':')),'utf-8')
    source = SOURCE.read_text(encoding='utf-8')
    matches = list(re.finditer(r'<a id="([^"]+)"></a>\s*\n', source))
    sections = {}
    entries = []
    current = ''
    for i, match in enumerate(matches):
        key = match[1]
        body = source[match.end():matches[i + 1].start() if i + 1 < len(matches) else len(source)].strip()
        heading = re.search(r'^#{2,4} (.+)', body, re.M)
        if not heading:
            raise ValueError('No title for ' + key)
        title = heading[1]
        body = body[heading.end():].strip()
        body = re.sub(r'\n### 战斗阶段 \d+ · \d+ 条记录\s*$', '', body)
        if not key.startswith(('item-', 'enemy-')):
            current = key
            sections[key] = {'id': key, 'title': title, 'body': body}
            continue
        kind = '物品' if key.startswith('item-') else '怪物'
        category_match = re.search(r'^`[^`]+` · (.+?) · 配置阶段 (\d+)', body, re.M)
        stage_match = re.search(r'战斗阶段 (\d+)', body)
        category = category_match[1] if category_match else kind
        stage = category_match[2] if category_match else stage_match[1] if stage_match else ''
        if kind == '怪物':
            enemy_category = re.search(r'^- 图鉴分类：(小怪|精英|首领)。', body, re.M)
            if not enemy_category:
                raise ValueError('Regenerate the encyclopedia with enemy categories: ' + key)
            category = enemy_category[1]
        entries.append({'id': key, 'title': title, 'body': body, 'kind': kind,
                        'category': category, 'stage': stage, 'parent': current})
    # Town residents get their own pages, preserving every shop table.
    npcs = sections['npcs']
    npc_parts = list(re.finditer(r'^### (.+)$', npcs['body'], re.M))
    npc_intro = npcs['body'][:npc_parts[0].start()]
    for i, part in enumerate(npc_parts):
        end = npc_parts[i + 1].start() if i + 1 < len(npc_parts) else len(npcs['body'])
        title = re.sub(r'（入住阶段 \d+）', '', part[1])
        stage = re.search(r'入住阶段 (\d+)', part[1])
        entries.append({'id': f'npc-{i+1}', 'title': title, 'body': npcs['body'][part.end():end].strip(),
                        'kind': 'NPC', 'category': '城镇居民', 'stage': stage[1] if stage else '', 'parent': 'npcs'})
    base_sections = set(sections)
    coverage = load_knowledge(CATALOG_ROOT, sections, entries)
    coverage.update(player_content(CATALOG_ROOT, sections, entries))
    knowledge_sections = [s for key,s in sections.items() if key not in base_sections]
    npc_intro = '[圣巢本地商店与服务](#native-shops) · [全部新版台词](#dialogue)\n\n城镇居民按能力阶段与条件入住。商品会随阶段、月相和首领进度变化，具体购买条件请查看居民词条。月相按游戏内时间轮换，共八个阶段。'
    releases = json.loads(RELEASES_FILE.read_text(encoding='utf-8'))
    assert releases and len({r['tag'] for r in releases}) == len(releases)
    releases.sort(key=lambda r: r['published_at'], reverse=True)
    log = []
    inline_notes = []
    for release in releases:
        release['body'] = player_release_notes(release['body'])
        tag = release['tag']
        assert re.fullmatch(r'v\d+\.\d+\.\d+', tag), tag
        assert release['url'] == 'https://github.com/haoawake/TerraKnight/releases/tag/' + tag
        key = 'release-' + tag.replace('.', '-')
        date = datetime.fromisoformat(release['published_at'].replace('Z', '+00:00')).astimezone(ZoneInfo('America/Los_Angeles')).strftime('%Y-%m-%d %H:%M')
        status = '预发布' if release['prerelease'] else '正式发布'
        notes = re.sub(r'^(#{1,6}) ', lambda m: '#' * min(6, len(m[1]) + 1) + ' ', release['body'], flags=re.M)
        sections[key] = {'id': key, 'title': f'{tag} 更新日志',
                         'body': f'**{status}** · {date}（洛杉矶时间）\n\n[返回版本总览](#changelog) · [GitHub 原发布页]({release["url"]})\n\n---\n\n' + notes}
        inline_body = re.sub(r'^(#{1,6}) ', lambda m: '#' * min(6, len(m[1]) + 2) + ' ', release['body'], flags=re.M)
        anchor = 'changelog-' + tag.replace('.', '-')
        rendered_notes = re.sub(r'\bid="([^"]+)"', lambda m: f'id="{anchor}-{m[1]}"', render(inline_body))
        inline_notes.extend(['', f'<details class="release-note" id="{anchor}"><summary><strong>{tag} 更新日志</strong><span class="release-meta">{date} · {status}</span></summary><div class="release-note-body"><p><a href="{release["url"]}">GitHub 原发布页</a></p>{rendered_notes}</div></details>', ''])
    log.extend(inline_notes)
    sections['changelog'] = {'id': 'changelog', 'title': '版本更新日志', 'body': '\n'.join(log)}
    targets = {k: BASE + k + '.html' for k in sections}
    targets['index'] = BASE + 'item-index.html'
    if 'boss-summons' not in sections:
        summons = [e for e in entries if e['category'] == 'Boss召唤物']
        sections['boss-summons'] = {'id': 'boss-summons', 'title': 'Boss 召唤物',
            'body': '点击召唤物查看获取方式、使用条件与合成配方。\n\n' + '\n'.join(
                f'- [{e["title"]}](#{e["id"]})' for e in summons)}
        targets['boss-summons'] = BASE + 'boss-summons.html'
    targets.update({e['id']: BASE + e['id'] + '.html' for e in entries})
    references = set()

    def rewrite(body):
        def link(match):
            label, url = match[1], match[2]
            if url.startswith('#'):
                key = url[1:]
                if key not in targets:
                    raise ValueError('Unknown reference: ' + key)
                return f'[{label}]({targets[key]})'
            if re.match(r'https?://', url):
                return match[0]
            rel = url.split('#')[0]
            path = (CATALOG_ROOT / rel).resolve()
            # Embedded excerpts can retain a parent-relative link from their original
            # document folder. Resolve an existing root source without leaving the snapshot.
            if rel.startswith('../') and not path.is_relative_to(CATALOG_ROOT):
                local = re.sub(r'^(?:\.\./)+', '', rel)
                candidate = (CATALOG_ROOT / local).resolve()
                if candidate.is_relative_to(CATALOG_ROOT) and candidate.is_file():
                    rel, path = local, candidate
            if not path.is_relative_to(CATALOG_ROOT) or not path.is_file():
                # Older README excerpts have a few corrupted legacy document paths.
                if '阶段' in label:
                    return f'[{label}]({BASE}progress.html)'
                if '怪物' in label:
                    return f'[{label}]({BASE}enemies.html)'
                raise ValueError(f'Missing source link: {label}: {rel}')
            references.add(rel)
            return f'[{label}]({BASE}sources/{quote(rel)}{".txt" if path.suffix == ".cs" else ""})'
        return re.sub(r'\[([^\]]+)\]\(([^)]+)\)', link, body)

    def write(name, content):
        (OUT / name).write_text(content, encoding='utf-8')

    def cards(rows):
        return '<div class="entry-grid">' + ''.join(
            f'<a class="entry-card" href="{targets[e["id"]]}"><span class="entry-type">{html.escape(e["category"])} · 阶段 {e["stage"] or "—"}</span><h3>{html.escape(e["title"])}</h3><span class="entry-action">查看词条 ↗</span></a>' for e in rows) + '</div>'

    counts = collections.Counter(e['kind'] for e in entries)
    # Detailed pages are real HTML documents, readable and linkable without JavaScript.
    for e in entries:
        parent = {'物品': 'items', '怪物': 'enemies', 'NPC': 'npcs'}[e['kind']]
        body = render(rewrite(e['body']))
        for boss in map_links.get(e['id'] + '.html', []):
            body = f'<p class="map-entry-link"><a href="{BASE}map.html#boss={quote(boss["id"])}">在世界地图定位：{html.escape(boss["name"])} →</a></p>' + body
        doc = f'''<div class="article-wrap"><div class="breadcrumbs"><a href="{BASE}">首页</a> / <a href="{BASE}{parent}.html">{e['kind']}图鉴</a> / 词条</div>
<div class="article-heading"><div><span class="eyebrow">{html.escape(e['category'])} / 阶段 {e['stage'] or '—'}</span><h1>{html.escape(e['title'])}</h1></div><button class="bookmark" data-id="{e['id']}" aria-pressed="false">收藏词条 ☆</button></div>
<div class="article-layout"><article class="prose">{body}</article><aside class="article-aside"><div class="aside-title">查阅提示</div><p>蓝绿色文字可跳转至相关物品、怪物和配方。</p><p>血量表依次为普通、困难、专家、大师、空洞；分阶段和多部位以词条说明为准。</p><a href="{BASE}reading.html">查阅说明 →</a><a href="{BASE}search.html">搜索更多词条 →</a></aside></div></div>'''
        write(e['id'] + '.html', shell(e['title'], doc, parent, plain(e['body'])[:140]))

    for key, section in sections.items():
        body = section['body']
        if key in ('items', 'index', 'enemies', 'npcs'):
            kind = {'items': '物品', 'index': '物品', 'enemies': '怪物', 'npcs': 'NPC'}[key]
            subset = [e for e in entries if e['kind'] == kind]
            intro = npc_intro if key == 'npcs' else '全部物品均可按配置阶段和分类组合筛选，也可输入名称或获取方式。配置阶段是物品标签，实际获取条件请查看词条。' if kind == '物品' else body
            intro_html = render(rewrite(intro))
            if kind == '怪物':
                notes = re.split(r'^### 战斗阶段 ', body, maxsplit=1, flags=re.M)[0].strip()
                intro_html = '<p>按小怪、精英、首领与战斗阶段组合筛选，也可搜索名称或掉落物。</p><details><summary>分类与血量说明</summary>' + render(rewrite(notes)) + '</details>'
            listing = cards(subset[:24])
            categories = ['小怪', '精英', '首领'] if kind == '怪物' else sorted(set(e['category'] for e in subset))
            shortcuts = ''
            if kind in ('物品', '怪物'):
                shortcuts = '<nav class="filter-shortcuts" aria-label="阶段快捷筛选" data-filter="stage"><span>阶段</span>' + ''.join(
                    f'<a class="filter-link" data-value="{value}" href="?stage={value}"'+(' aria-current="true"' if value == '' else '')+f'>{label}</a>'
                    for value, label in [('', '全部')] + [(str(i), f'阶段 {i}') for i in range(6)]) + '</nav>'
            if kind == '怪物':
                shortcuts = '<nav class="filter-shortcuts" aria-label="怪物分类" data-filter="category"><span>怪物</span>' + ''.join(
                    f'<a class="filter-link" data-value="{value}" href="?category={quote(value)}"'+(' aria-current="true"' if value == '' else '')+f'>{label}</a>'
                    for value, label in [('', f'全部 {len(subset)}')] + [(c, f'{c} {sum(e["category"] == c for e in subset)}') for c in categories]) + '</nav>' + shortcuts
            # Bound the initial DOM; the full catalog remains available through filtering.
            filters = shortcuts + f'<div class="browse-controls"><label class="field">检索词条<input id="browse-query" type="search" placeholder="输入名称或获取方式"></label><label class="field">阶段<select id="stage-filter"><option value="">全部阶段</option>{"".join(f"<option>{i}</option>" for i in range(6))}</select></label><label class="field">分类<select id="category-filter"><option value="">全部分类</option>{"".join(f"<option>{html.escape(c)}</option>" for c in categories)}</select></label></div><p id="result-count" class="muted" aria-live="polite">共 {len(subset)} 条记录</p>'
            content = f'<div class="page-wrap"><span class="eyebrow">THE FIELD GUIDE</span><h1>{section["title"]}</h1><div class="prose intro">{intro_html}</div><section data-browser="{kind}">{filters}<div id="browse-results">{listing}</div></section></div>'
        elif key == 'recipes':
            rendered = render('<!-- recipe-table -->\n'+rewrite(body))
            table = re.search(r'<div class="table-scroll"[^>]*>(<table>.*?</table>)</div>', rendered, re.S)
            assert table, 'Recipe table missing'
            head = re.search(r'<thead>.*?</thead>', table[1], re.S)[0]
            rows = re.findall(r'<tr>.*?</tr>', re.search(r'<tbody>(.*?)</tbody>', table[1], re.S)[1], re.S)
            size = 40
            (OUT / 'data/recipes').mkdir(parents=True, exist_ok=True)
            recipe_index = []
            for offset in range(0, len(rows), size):
                chunk = rows[offset:offset + size]
                write(f'data/recipes/{offset // size}.json', json.dumps(chunk, ensure_ascii=False, separators=(',', ':')))
                for i, row in enumerate(chunk):
                    cells = re.findall(r'<td[^>]*>(.*?)</td>', row, re.S)
                    recipe_index.append({'chunk': offset // size, 'row': i, 'text': html.unescape(plain(row)), 'stage': plain(cells[-1])})
            write('data/recipes/index.json', json.dumps(recipe_index, ensure_ascii=False, separators=(',', ':')))
            controls = '<section data-recipes><div class="browse-controls"><label class="field grow">搜索配方<input id="recipe-query" type="search" placeholder="结果、材料或制作站"></label><label class="field">门槛<select id="recipe-stage"><option value="">全部阶段</option>' + ''.join(f'<option>{i}</option>' for i in range(6)) + '</select></label></div><p id="recipe-count" aria-live="polite"></p><div id="recipe-results" class="table-scroll" tabindex="0"><table>' + head + '<tbody>' + ''.join(rows[:size]) + '</tbody></table></div><p id="recipe-page" class="muted" aria-live="polite"></p><noscript>启用 JavaScript 可翻页和搜索；也可下载页脚的完整百科。</noscript></section>'
            rendered = rendered[:table.start()] + controls + rendered[table.end():]
            content = f'<div class="article-wrap"><h1>{section["title"]}</h1><article class="prose wide">{rendered}</article></div>'
        elif key == 'changelog':
            content = f'<div class="article-wrap"><article class="prose wide">{render(rewrite(body))}</article></div>'
        else:
            content = f'<div class="article-wrap"><div class="breadcrumbs"><a href="{BASE}">首页</a> / 百科指南</div><span class="eyebrow">HALLownest FIELD NOTES</span><h1>{section["title"]}</h1><article class="prose wide">{render(rewrite(body))}</article></div>'
        active = 'items' if key == 'index' else 'changelog' if key.startswith('release-') else key
        write(('item-index' if key == 'index' else key) + '.html', shell(section['title'], content, active))

    search_content = f'''<div class="page-wrap"><span class="eyebrow">FIND YOUR NEXT DISCOVERY</span><h1>搜索百科</h1><p class="lead">从一个名字，找到它的获取方式、掉落、配方和故事。</p><section data-browser="all"><div class="browse-controls"><label class="field grow">关键词<input id="browse-query" type="search" placeholder="试试：天顶剑、假骑士、矿石、Zenith" autofocus></label><label class="field">类型<select id="kind-filter"><option value="">全部类型</option><option>物品</option><option>怪物</option><option>NPC</option><option>指南</option></select></label><label class="field">阶段<select id="stage-filter"><option value="">全部阶段</option>{''.join(f'<option>{i}</option>' for i in range(6))}</select></label><label class="field check"><input id="saved-filter" type="checkbox">仅看收藏</label></div><p id="result-count" class="muted" aria-live="polite">正在载入搜索索引…</p><div id="browse-results"></div><noscript>搜索需要启用 JavaScript。也可以从主导航浏览全部静态词条。</noscript></section></div>'''
    write('search.html', shell('搜索百科', search_content))
    recipe_count = len(re.findall(r'^\| \[', sections['recipes']['body'], re.M))
    stats = ''.join(f'<div><strong>{number:,}</strong><span>{label}</span></div>' for number, label in [(counts['物品'], '物品与材料'), (counts['怪物'], '怪物 / 场景变体'), (recipe_count, '合成配方'), (counts['NPC'], '城镇 NPC')])
    portals = [('items', '01', '每一件装备，都有来处。', '武器、材料、饰品与护甲。查掉落、查商店，也查它能合成什么。'), ('enemies', '02', '知晓对手，再踏入战场。', '五种难度的实际血量，多部位、分阶段血池与完整掉落。'), ('progress', '03', '下一步，向圣巢更深处。', '从开局到终局：能力获取、地区路线与 NPC 入住条件。')]
    portal_html = ''.join(f'<a class="portal" href="{BASE}{key}.html"><span class="portal-num">{num} / FIELD GUIDE</span><h2>{line}</h2><p>{desc}</p><span class="portal-arrow">↗</span></a>' for key,num,line,desc in portals)
    home = f'''<section class="hero"><div class="hero-copy"><span class="eyebrow"><span class="live-dot"></span> TERRAKNIGHT · 泰拉瑞亚 × 空洞骑士</span><h1>一段冒险，<br>一整座<span>圣巢的知识。</span></h1><p>欢迎来到空洞泰拉wiki。<br>装备从哪里来，下一站去哪里，在这里找到答案。</p><form action="{BASE}search.html" class="hero-search"><label class="sr-only" for="home-query">搜索百科</label><span aria-hidden="true">⌕</span><input id="home-query" name="q" type="search" placeholder="搜索物品、怪物、材料、NPC…"><button type="submit">探索百科 →</button></form><div class="quick-links">热门查阅 <a href="{BASE}item-zenith.html">天顶剑</a><a href="{BASE}enemy-falseknight.html">假骑士</a><a href="{BASE}progress.html">阶段解锁</a><a href="{BASE}recipes.html">合成配方</a><a href="{BASE}boss-summons.html">Boss 召唤物</a></div></div><div class="hero-art" aria-hidden="true"><div class="orbit orbit-one"></div><div class="orbit orbit-two"></div><img src="{BASE}assets/crest.svg" alt=""><span class="art-note">在地表之下，继续探索。<br>THE KINGDOM BENEATH</span></div></section>
<section class="stats" aria-label="收录统计">{stats}<a href="{BASE}audit.html">收录范围<br>与数据来源 ↗</a></section><section class="home-section"><div class="section-heading"><div><span class="eyebrow">YOUR ADVENTURE STARTS HERE</span><h2>选择你的探索方向</h2></div><a href="{BASE}index.html#all-guides">全部指南 ↓</a></div><div class="portals">{portal_html}</div></section>
<section class="home-section split" id="all-guides"><div><span class="eyebrow">FROM FIRST STEPS TO THE FINAL LIGHT</span><h2>每一次解锁，<br>都是新的起点。</h2><p class="muted">六个成长阶段，串起能力、探索与城镇生活。<br>路线是建议，探索由你决定。</p><a class="button" href="{BASE}progress.html">查看完整成长路线 →</a></div><div class="stage-list">{''.join(f'<a href="{BASE}progress.html"><span>0{i}</span><div><strong>{name}</strong><small>{detail}</small></div><span>↗</span></a>' for i,name,detail in [(0,'初入圣巢','开局即为阶段 0'),(1,'蛾翼披风','击败苍绿之径的大黄蜂后取得'),(2,'螳螂爪','在螳螂村拾取，无需击败首领'),(3,'水晶之心','在水晶山峰拾取，无需击败首领'),(4,'帝王之翼','击败远古盆地的残破容器后取得'),(5,'暗影披风','击败王国边缘的大黄蜂得王者之印，再进深渊')])}</div></section>
<section class="home-section guide-strip">{''.join(f'<a href="{BASE}{key}.html"><span>{name}</span><small>{desc}</small><b>↗</b></a>' for key,name,desc in [('npcs','NPC 与商店','入住、库存、月相与价格'),('systems','玩法系统','生命、装备、经济与探索'),('original','剧情与结局','原版 NPC 与终局路线'),('manual','安装与操作','上手、联机与配置参考')])}</section><div class="data-note">资料按当前游戏版本整理。血量已核对五种难度；场景核对范围与特殊机制请阅读 <a href="{BASE}audit.html">数据说明</a>。</div>'''
    # Place an obvious map entry on the home page as well as the shared navigation.
    home = home.replace('<section class="stats"', f'<section class="home-section"><a class="button" href="{BASE}map.html">探索圣巢全境地图 · {len(map_data["rooms"])} 个房间与 {len(map_data["bosses"])} 个 Boss →</a></section><section class="stats"', 1)
    write('index.html', shell(TITLE, home))
    index = []
    for e in entries + [dict(s, kind='指南', category='版本更新' if s['id'] == 'changelog' or s['id'].startswith('release-') else '玩法指南', stage='') for s in sections.values()]:
        text = plain(e['body'])
        index.append({k: e[k] for k in ('id', 'title', 'kind', 'category', 'stage')} | {'url': targets[e['id']], 'text': text})
    index.append({'id': 'map', 'title': '圣巢全境地图', 'kind': '指南', 'category': '玩法指南', 'stage': '', 'url': BASE + 'map.html', 'text': '完整逐格地图；缩放、图层切换、Boss 搜索、战斗房间与入口位置。'})
    write('search-index.json', json.dumps(index, ensure_ascii=False, separators=(',', ':')))
    compact = [dict(e, text=e['text'][:120]) for e in index]
    write('data/browse-index.json', json.dumps(compact, ensure_ascii=False, separators=(',', ':')))
    for name, kind in [('items', '物品'), ('enemies', '怪物'), ('npcs', 'NPC')]:
        write(f'data/{name}-index.json', json.dumps([e for e in index if e['kind'] == kind], ensure_ascii=False, separators=(',', ':')))
        write(f'data/{name}-browse.json', json.dumps([e for e in compact if e['kind'] == kind], ensure_ascii=False, separators=(',', ':')))
    combined = '# 空洞泰拉玩家百科\n\n' + '\n\n'.join(
        '<a id="'+e['id']+'"></a>\n\n## '+e['title']+'\n\n'+e['body'] for e in [*sections.values(),*entries])
    (OUT/'downloads/encyclopedia.md').write_text(combined,'utf-8')
    for rel in sorted(references):
        src = CATALOG_ROOT / rel
        dest = OUT / 'sources' / (rel + ('.txt' if src.suffix == '.cs' else ''))
        dest.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(src, dest)
    write('404.html', shell('没有找到这个词条', f'<div class="page-wrap"><h1>这条路暂时没有词条。</h1><p>可以返回首页，或搜索你想找的内容。</p><a class="button" href="{BASE}search.html">搜索百科 →</a></div>'))
    summary = {'name': TITLE, 'base': BASE, 'entries': dict(counts), 'guides': len(sections),
               'recipes': recipe_count, 'source_sha256': hashlib.sha256(SOURCE.read_bytes()).hexdigest(), 'pages': len(list(OUT.glob('*.html'))), 'release': VERSION, 'boss_summons': sum(e['category']=='Boss召唤物' for e in entries), 'release_notes': len(releases)}
    summary['knowledge_coverage'] = coverage
    summary['world_map'] = map_data['source'] | {'bosses': len(map_data['bosses']), 'entrance_markers': sum(b['precision']=='entrance' for b in map_data['bosses'])}
    write('build-info.json', json.dumps(summary, ensure_ascii=False, indent=2))
    bundle = ROOT / 'artifacts/terraknight-wiki.zip'
    with zipfile.ZipFile(bundle, 'w', zipfile.ZIP_DEFLATED) as archive:
        for path in sorted(OUT.rglob('*')):
            if path.is_file():
                archive.write(path, path.relative_to(OUT.parent))
    print(json.dumps(summary | {'bundle_bytes': bundle.stat().st_size}, ensure_ascii=False, indent=2))


if __name__ == '__main__':
    main()
