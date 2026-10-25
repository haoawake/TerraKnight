"""Validate every local link, then exercise desktop/mobile search and browsing."""
import json
from html.parser import HTMLParser
from pathlib import Path
from urllib.parse import unquote, urlsplit
from table_data import expand_tables

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'artifacts/wiki-preview'
BASE = '/terraknight-wiki/'


class Links(HTMLParser):
    def __init__(self):
        super().__init__()
        self.links = []
        self.ids = set()

    def handle_starttag(self, tag, attrs):
        attrs = dict(attrs)
        if attrs.get('id'):
            self.ids.add(attrs['id'])
        for name in ('href', 'src'):
            if attrs.get(name):
                self.links.append(attrs[name])


def static_check():
    pages = {}
    for path in (OUT / 'terraknight-wiki').glob('*.html'):
        parser = Links()
        parser.feed(expand_tables(path.read_text(encoding='utf-8'), OUT/'terraknight-wiki'))
        pages[path.resolve()] = parser
    checked = 0
    for path, parser in pages.items():
        for link in parser.links:
            url = urlsplit(link)
            if url.scheme or url.netloc:
                continue
            dest = OUT / unquote(url.path).lstrip('/') if url.path.startswith('/') else path.parent / unquote(url.path) if url.path else path
            if dest.is_dir():
                dest = dest / 'index.html'
            assert dest.is_file(), (path.name, link)
            if url.fragment and dest.resolve() in pages:
                if dest.name == 'map.html' and url.fragment.startswith('boss='):
                    map_data = json.loads((OUT / 'terraknight-wiki/assets/world-map/map.json').read_text(encoding='utf-8'))
                    assert unquote(url.fragment[5:]) in {b['id'] for b in map_data['bosses']}, (path.name, link, 'boss')
                else:
                    assert unquote(url.fragment) in pages[dest.resolve()].ids, (path.name, link, 'fragment')
            checked += 1
    index = json.loads((OUT / 'terraknight-wiki/search-index.json').read_text(encoding='utf-8'))
    import sys
    import os
    meta=json.loads((OUT/'terraknight-wiki/build-info.json').read_text('utf-8'))
    os.environ['TK_WIKI_SOURCE_ROOT']=str(ROOT/meta['release']['snapshot'])
    sys.path.insert(0, str(ROOT / 'tools'))
    from generate_encyclopedia import build
    items, enemies = build(assets_only=True)
    # A place left in the item enum with nothing made of it keeps its enum name and gets no page.
    assert {e['id'] for e in index if e['kind']=='物品'} == {'item-'+t.lower() for t, d in items.items() if d['name'] != t}
    assert {e['id'] for e in index if e['kind']=='怪物'} == {'enemy-'+e['key'].lower().replace('@','-') for e in enemies}
    npc_pages = len(list((OUT / 'terraknight-wiki').glob('npc-*.html')))
    assert sum(e['kind'] == 'NPC' for e in index) == npc_pages >= 11
    assert len({e['id'] for e in index}) == len(index)
    print(json.dumps({'pages': len(pages), 'local_links': checked, 'indexed_entries': len(index)}))


def browser_check(origin='http://127.0.0.1:8098'):
    from playwright.sync_api import sync_playwright
    errors, failed = [], []
    with sync_playwright() as p:
        browser = p.chromium.launch(executable_path=r'C:\Program Files\Google\Chrome\Application\chrome.exe', headless=True)
        page = browser.new_page(viewport={'width': 1440, 'height': 1050}, device_scale_factor=1)
        page.on('pageerror', lambda error: errors.append(str(error)))
        page.on('response', lambda response: failed.append(f'{response.status} {response.url}') if response.status >= 400 else None)
        page.goto(origin + BASE, wait_until='networkidle')
        assert page.title() == '空洞泰拉wiki'
        page.screenshot(path=str(ROOT / 'artifacts/wiki-desktop.png'), full_page=True)
        page.locator('#home-query').fill('天顶剑')
        page.get_by_role('button', name='探索百科').click()
        page.wait_for_function("document.querySelector('#result-count').textContent.includes('已显示')")
        page.locator('.entry-card').first.click()
        assert page.url.endswith('/item-zenith.html')
        page.get_by_role('button', name='收藏词条').click()
        assert page.locator('.bookmark').get_attribute('aria-pressed') == 'true'
        page.reload(wait_until='networkidle')
        assert page.locator('.bookmark').get_attribute('aria-pressed') == 'true'
        page.goto(origin + BASE + 'search.html?saved=1', wait_until='networkidle')
        assert page.locator('.entry-card').count() == 1
        assert '天顶剑' in page.locator('.entry-card').inner_text()
        page.goto(origin + BASE + 'enemies.html?stage=0', wait_until='networkidle')
        assert page.locator('.entry-card').count() > 0
        assert all('阶段 0' in text for text in page.locator('.entry-type').all_text_contents())
        page.goto(origin + BASE + 'search.html?q=unfindable_987654', wait_until='networkidle')
        assert page.locator('.empty').is_visible()
        page.goto(origin + BASE + 'enemy-falseknight.html', wait_until='networkidle')
        assert page.locator('article table').first.locator('tbody tr').count() > 0
        assert '整场总血量' in page.locator('article').inner_text()
        page.screenshot(path=str(ROOT / 'artifacts/wiki-health.png'), full_page=True)
        assert '互斥' in page.locator('article').inner_text()
        assert '玩家解锁阶段' in page.locator('article').inner_text()
        page.goto(origin + BASE + 'item-bosssigilfalseknight.html', wait_until='networkidle')
        page.wait_for_selector('[data-illustration="item-bosssigilfalseknight"]')
        assert '首次击败' in page.locator('article').inner_text()
        assert page.locator('article a[href$="item-ironore.html"]').count() > 0
        assert page.locator('article a[href$="item-gel.html"]').count() > 0
        page.screenshot(path=str(ROOT / 'artifacts/wiki-boss-seal.png'), full_page=True)
        page.set_viewport_size({'width': 390, 'height': 844})
        for route in ('', 'enemy-falseknight.html', 'item-bosssigilfalseknight.html', 'items.html', 'recipes.html'):
            page.goto(origin + BASE + route, wait_until='networkidle')
            assert page.evaluate('document.documentElement.scrollWidth <= innerWidth'), route
        page.goto(origin + BASE, wait_until='networkidle')
        page.screenshot(path=str(ROOT / 'artifacts/wiki-mobile.png'), full_page=True)
        browser.close()
    assert not errors, errors
    assert not failed, failed
    print(json.dumps({'browser': 'passed', 'desktop': True, 'mobile': True, 'search': True,
                      'bookmarks': True, 'stage_filter': True, 'health_table': True, 'errors': errors}))


if __name__ == '__main__':
    import sys
    static_check()
    browser_check(sys.argv[1] if len(sys.argv) > 1 else 'http://127.0.0.1:8098')
