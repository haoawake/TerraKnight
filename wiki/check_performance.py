"""Verify bounded catalog DOM and demand-loaded recipe payloads in a real browser."""
import json
import sys
from pathlib import Path
from playwright.sync_api import sync_playwright

ROOT = Path(__file__).resolve().parents[1]


def main():
    origin = sys.argv[1].rstrip('/') if len(sys.argv) > 1 else 'http://127.0.0.1:8098'
    base = origin + '/terraknight-wiki/'
    requests, errors = [], []
    with sync_playwright() as p:
        browser = p.chromium.launch(executable_path=r'C:\Program Files\Google\Chrome\Application\chrome.exe', headless=True)
        page = browser.new_page(viewport={'width': 1440, 'height': 900})
        page.on('request', lambda r: requests.append(r.url.split('?')[0]))
        page.on('pageerror', lambda e: errors.append(str(e)))
        page.goto(base + 'recipes.html', wait_until='networkidle')
        page.wait_for_function("document.querySelector('#recipe-count').textContent.includes('找到')")
        assert page.locator('#recipe-results tbody tr').count() == 40
        chunks = {u for u in requests if '/data/recipes/' in u and not u.endswith('/index.json')}
        assert chunks == {base + 'data/recipes/0.json'}, chunks
        first = page.locator('#recipe-results tbody tr').first.inner_text()
        page.locator('.pagination').first.get_by_role('button', name='下一页').click()
        page.wait_for_function("document.querySelector('#recipe-page').textContent.includes('第 2 /')")
        assert page.locator('#recipe-results tbody tr').count() == 40
        assert page.locator('#recipe-results tbody tr').first.inner_text() != first
        assert base + 'data/recipes/1.json' in requests
        page.locator('#recipe-query').fill('天顶剑')
        page.wait_for_function("document.querySelector('#recipe-results tbody').textContent.includes('天顶剑')")
        assert all('天顶剑' in s for s in page.locator('#recipe-results tbody tr').all_text_contents())
        page.locator('.pagination').first.wait_for(state='hidden')
        page.reload(wait_until='networkidle')
        assert page.locator('#recipe-query').input_value() == '天顶剑'
        page.locator('#recipe-query').fill('')
        page.locator('#recipe-stage').select_option('0')
        page.wait_for_function("!document.querySelector('#recipe-results tbody').hasAttribute('aria-busy') && document.querySelector('#recipe-results tbody tr td:last-child').textContent === '0'")
        assert all(s == '0' for s in page.locator('#recipe-results tbody tr td:last-child').all_text_contents())
        page.set_viewport_size({'width': 390, 'height': 844})
        assert page.evaluate('document.documentElement.scrollWidth <= innerWidth')
        page.screenshot(path=str(ROOT / 'artifacts/wiki-recipes-mobile.png'), full_page=True)
        requests.clear()
        page.goto(base + 'items.html', wait_until='networkidle')
        page.wait_for_function("document.querySelector('#result-count').textContent.includes('已显示')")
        assert page.locator('.entry-card').count() == 24
        assert not any(u.endswith('search-index.json') or u.endswith('items-index.json') for u in requests)
        page.locator('#browse-query').fill('Zenith')
        page.wait_for_function("performance.getEntriesByType('resource').some(r => r.name.includes('items-index.json')) && document.querySelector('#result-count').textContent.includes('已显示')")
        page.wait_for_selector('.entry-card[href$="item-zenith.html"]')
        assert any(u.endswith('items-index.json') for u in requests)
        page.set_viewport_size({'width': 1440, 'height': 900})
        page.goto(base + 'recipes.html', wait_until='networkidle')
        page.screenshot(path=str(ROOT / 'artifacts/wiki-recipes-desktop.png'), full_page=True)
        browser.close()
    assert not errors, errors
    print(json.dumps({'recipe_page_size': 40, 'initial_recipe_chunks': 1, 'initial_item_cards': 24,
                      'full_search_loaded_on_demand': True, 'pagination_search_stage_mobile': 'passed'}))


if __name__ == '__main__':
    main()
