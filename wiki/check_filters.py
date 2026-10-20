"""Exercise catalog filters against the exported item stages and native enemy tiers."""
import collections
import json
import os
from pathlib import Path
import sys
from urllib.parse import parse_qs, urlsplit

ROOT = Path(__file__).resolve().parents[1]
CATALOG = Path(os.environ.get('TK_WIKI_SOURCE_ROOT', ROOT))
OUT = ROOT/'artifacts/wiki-preview/terraknight-wiki'


def main():
    from playwright.sync_api import sync_playwright
    origin = sys.argv[1].rstrip('/') if len(sys.argv) > 1 else 'http://127.0.0.1:8098'
    base = origin+'/terraknight-wiki/'
    runtime = json.loads((CATALOG/'tools/encyclopedia_runtime.json').read_text(encoding='utf-8'))['data']
    # A place left in the item enum with nothing made of it is named by its enum and gets no page.
    runtime['items'] = {t: d for t, d in runtime['items'].items() if d['Name'] != t}
    index = json.loads((OUT/'search-index.json').read_text(encoding='utf-8'))
    monsters = [e for e in index if e['kind'] == '怪物']
    sys.path.insert(0, str(ROOT/'tools'))
    from generate_encyclopedia import build
    _, roster = build(assets_only=True)
    tiers = {'enemy-'+e['key'].lower().replace('@', '-'): '首领' if e['boss'] else
             '精英' if e['kind'] == 'miniboss' or e['hp'] >= 70 else '小怪' for e in roster}
    assert all(e['category'] == tiers[e['id']] for e in monsters)
    expected_stages = {str(i): {'item-'+t.lower() for t, d in runtime['items'].items() if d['Stage'] == i}
                       for i in range(6)}
    errors = []
    with sync_playwright() as p:
        browser = p.chromium.launch(executable_path=r'C:\Program Files\Google\Chrome\Application\chrome.exe', headless=True)
        page = browser.new_page(viewport={'width': 1440, 'height': 1000})
        page.on('pageerror', lambda error: errors.append(str(error)))

        def ready(route):
            page.goto(base+route, wait_until='networkidle')
            page.wait_for_function("document.querySelector('#result-count').textContent.includes('已显示')")

        def shown():
            links = []
            nav = page.locator('.pagination').first
            if nav.is_visible():
                nav.locator('button[data-page="0"]').click()
            while True:
                links.extend(page.locator('#browse-results .entry-card').evaluate_all('(links) => links.map(a => a.getAttribute("href").split("/").pop().replace(".html", ""))'))
                next_page = nav.get_by_role('button', name='下一页')
                if not nav.is_visible() or next_page.is_disabled():
                    break
                next_page.click()
            assert len(links) == len(set(links))
            return set(links)

        for route in ('items.html', 'item-index.html', 'search.html?kind=物品'):
            ready(route)
            seen = set()
            for stage, expected in expected_stages.items():
                page.locator('#stage-filter').select_option(stage)
                assert shown() == expected, (route, stage)
                seen.update(expected)
                assert parse_qs(urlsplit(page.url).query)['stage'] == [stage]
            assert seen == {'item-'+t.lower() for t in runtime['items']}, route

        ready('items.html?stage=4&category=近战武器')
        assert shown() == {e['id'] for e in index if e['kind'] == '物品' and e['stage'] == '4' and e['category'] == '近战武器'}
        page.locator('[data-filter="stage"] a[data-value="0"]').click()
        assert page.locator('#category-filter').input_value() == '近战武器'
        assert shown() == {e['id'] for e in index if e['kind'] == '物品' and e['stage'] == '0' and e['category'] == '近战武器'}
        page.reload(wait_until='networkidle')
        assert page.locator('#stage-filter').input_value() == '0'
        assert page.locator('#category-filter').input_value() == '近战武器'

        ready('enemies.html')
        assert page.locator('#category-filter option').all_text_contents() == ['全部分类', '小怪', '精英', '首领']
        for category in ('小怪', '精英', '首领'):
            page.locator('[data-filter="category"] a[data-value="'+category+'"]').click()
            assert shown() == {e['id'] for e in monsters if e['category'] == category}
            for stage in expected_stages:
                page.locator('#stage-filter').select_option(stage)
                assert shown() == {e['id'] for e in monsters if e['category'] == category and e['stage'] == stage}, (category, stage)
            page.locator('#stage-filter').select_option('')
        page.locator('#browse-query').fill('FalseKnight')
        page.wait_for_function("location.search.includes('q=FalseKnight')")
        assert shown() == {e['id'] for e in monsters if 'falseknight' in e['id']}
        page.locator('#browse-query').fill('unfindable_filter_12987')
        page.wait_for_selector('.empty')
        assert page.locator('#category-filter').input_value() == '首领'

        ready('enemies.html?category=精英&stage=2')
        page.screenshot(path=str(ROOT/'artifacts/wiki-enemy-filters-desktop.png'), full_page=True)
        page.set_viewport_size({'width': 390, 'height': 844})
        for route in ('enemies.html?category=首领&stage=0', 'items.html?stage=0', 'item-index.html?stage=5'):
            ready(route)
            assert page.evaluate('document.documentElement.scrollWidth <= innerWidth'), route
            page.locator('[data-filter="stage"] a[data-value="1"]').click()
            assert page.locator('#stage-filter').input_value() == '1'
        ready('items.html?stage=4&category=近战武器')
        page.screenshot(path=str(ROOT/'artifacts/wiki-item-stage-filter-mobile.png'), full_page=False)
        browser.close()
    assert not errors, errors
    report = {'status': 'passed', 'enemy_categories': dict(collections.Counter(tiers.values())),
              'all_items_checked': len(runtime['items']), 'item_stages': {s: len(v) for s, v in expected_stages.items()},
              'item_catalog_routes': 3, 'enemy_category_stage_combinations': 18,
              'combined_filters': True, 'url_restore': True, 'mobile': True, 'errors': errors}
    (ROOT/'artifacts/wiki-filter-check.json').write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')
    print(json.dumps(report, ensure_ascii=False))


if __name__ == '__main__':
    main()
