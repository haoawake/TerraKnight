"""Exercise catalog, recipe and long-table pagination in Chrome."""
import json
from pathlib import Path
from playwright.sync_api import sync_playwright

ROOT = Path(__file__).resolve().parents[1]
BASE = 'http://127.0.0.1:8098/terraknight-wiki/'


def main():
    errors = []
    with sync_playwright() as p:
        browser = p.chromium.launch(executable_path=r'C:\Program Files\Google\Chrome\Application\chrome.exe', headless=True)
        page = browser.new_page(viewport={'width': 1440, 'height': 1000})
        page.on('pageerror', lambda error: errors.append(str(error)))

        def ready(route):
            page.goto(BASE + route, wait_until='networkidle')
            page.wait_for_function("document.querySelector('#result-count').textContent.includes('已显示')")

        for route in ('items.html', 'enemies.html', 'search.html'):
            ready(route)
            nav = page.locator('.pagination').first
            assert page.locator('.entry-card').count() == 24
            first = page.locator('.entry-card').first.get_attribute('href')
            assert nav.get_by_role('button', name='上一页').is_disabled()
            page.locator('.pagination').last.get_by_role('button', name='下一页').click()
            assert 'page=2' in page.url
            assert page.locator('.entry-card').first.get_attribute('href') != first
            page.reload(wait_until='networkidle')
            assert nav.locator('[aria-current="page"]').inner_text() == '2'
            nav.get_by_role('spinbutton').fill('3')
            nav.get_by_role('button', name='前往').click()
            assert nav.locator('[aria-current="page"]').inner_text() == '3'
            page.locator('#stage-filter').select_option('0')
            assert 'page=' not in page.url
            assert all('阶段 0' in text for text in page.locator('.entry-type').all_text_contents())
        ready('items.html?page=999999')
        assert page.locator('.pagination').first.get_by_role('button', name='下一页').is_disabled()
        assert 0 < page.locator('.entry-card').count() <= 24
        ready('items.html?q=Zenith&page=2')
        assert page.locator('.entry-card').count() == 1
        assert not page.locator('.pagination').first.is_visible()
        ready('search.html?q=unfindable_987654')
        assert page.locator('.empty').is_visible()
        assert not page.locator('.pagination').first.is_visible()
        for value in ('NaN', 'Infinity', '-3', '1.5'):
            ready('items.html?page=' + value)
            assert page.locator('.pagination').first.locator('[aria-current="page"]').inner_text() == '1'

        for route, catalog in (('items.html', 'items'), ('enemies.html', 'enemies'), ('npcs.html', 'npcs')):
            ready(route)
            seen = []
            nav = page.locator('.pagination').first
            while True:
                seen.extend(page.locator('.entry-card').evaluate_all('(links) => links.map(a => a.getAttribute("href"))'))
                if not nav.is_visible() or nav.get_by_role('button', name='下一页').is_disabled():
                    break
                nav.get_by_role('button', name='下一页').click()
            expected = json.loads((ROOT / f'artifacts/wiki-preview/terraknight-wiki/data/{catalog}-browse.json').read_text(encoding='utf-8'))
            assert len(seen) == len(set(seen)) == len(expected), route
            assert set(seen) == {record['url'] for record in expected}, route

        page.goto(BASE + 'recipes.html?page=2', wait_until='networkidle')
        page.wait_for_function("document.querySelector('#recipe-page').textContent.includes('第 2 /')")
        assert page.locator('#recipe-results tbody tr').count() == 40
        page.locator('.pagination').last.get_by_role('button', name='下一页').click()
        page.wait_for_function("document.querySelector('#recipe-page').textContent.includes('第 3 /')")
        page.locator('#recipe-query').fill('天顶剑')
        page.wait_for_function("document.querySelector('#recipe-results tbody').textContent.includes('天顶剑')")
        page.locator('.pagination').first.wait_for(state='hidden')

        # A generated shop/guide table must replace its visible rows on each page.
        out = ROOT / 'artifacts/wiki-preview/terraknight-wiki'
        import re
        candidates = [f for f in out.glob('*.html') if f.name != 'recipes.html'
                      and any(len(re.findall('<tr>', body)) > 24 for body in re.findall(r'<tbody>(.*?)</tbody>', f.read_text(encoding='utf-8'), re.S))]
        assert candidates
        page.goto(BASE + candidates[0].name, wait_until='networkidle')
        nav = page.locator('.pagination').first
        root = nav.locator('xpath=following-sibling::div[contains(@class,"table-scroll")][1]')
        assert root.locator('tbody tr:visible').count() == 24
        first = root.locator('tbody tr:visible').first.inner_text()
        nav.get_by_role('button', name='下一页').click()
        assert root.locator('tbody tr:visible').first.inner_text() != first
        assert root.locator('tbody tr:visible').count() <= 24

        for width in (1440, 390):
            page.set_viewport_size({'width': width, 'height': 900})
            for route in ('items.html?page=5', 'enemies.html', 'search.html', 'recipes.html?page=3', candidates[0].name):
                page.goto(BASE + route, wait_until='networkidle')
                assert page.evaluate('document.documentElement.scrollWidth <= innerWidth'), (width, route)
            ready('items.html?page=5')
            page.screenshot(path=str(ROOT / f'artifacts/wiki-pagination-{width}.png'), full_page=True)
        browser.close()
    assert not errors, errors
    print(json.dumps({'catalog_page_size': 24, 'recipe_page_size': 40, 'long_table_page_size': 24,
                      'navigation_reload_jump_filter_empty_mobile': 'passed', 'all_catalog_records': 'passed', 'errors': errors}))


if __name__ == '__main__':
    main()
