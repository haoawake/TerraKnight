"""Check complete image coverage and verify the actual wiki presentation."""
import collections
import json
import sys
from pathlib import Path
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
BASE = '/terraknight-wiki/'


def main():
    folder = ROOT / 'artifacts/wiki-preview/terraknight-wiki/assets/illustrations'
    manifest = json.loads((folder / 'manifest.json').read_text(encoding='utf-8'))
    index = json.loads((folder.parents[1] / 'search-index.json').read_text(encoding='utf-8'))
    expected = {entry['id'] for entry in index if entry['kind'] in ('物品', '怪物')}
    assert set(manifest) == expected
    counts = collections.Counter(entry['kind'] for entry in manifest.values())
    assert counts == {'item': sum(e['kind']=='物品' for e in index), 'enemy': sum(e['kind']=='怪物' for e in index)}
    sheets = {name: Image.open(folder / name).convert('RGBA') for name in ('items.webp', 'enemies.webp')}
    for key, entry in manifest.items():
        sheet = sheets[entry['sheet']]
        x, y, cell = entry['x'], entry['y'], entry['cell']
        assert x >= 0 and y >= 0 and x + cell <= sheet.width and y + cell <= sheet.height, key
        assert sheet.crop((x, y, x + cell, y + cell)).getbbox(), key
    from playwright.sync_api import sync_playwright
    origin = sys.argv[1] if len(sys.argv) > 1 else 'http://127.0.0.1:8098'
    errors, failed = [], []
    with sync_playwright() as p:
        browser = p.chromium.launch(executable_path=r'C:\Program Files\Google\Chrome\Application\chrome.exe', headless=True)
        page = browser.new_page(viewport={'width': 1440, 'height': 1000})
        page.on('pageerror', lambda error: errors.append(str(error)))
        page.on('response', lambda response: failed.append(f'{response.status} {response.url}') if response.status >= 400 else None)
        for route, picture in [('item-zenith.html', 'item-zenith'), ('enemy-finalboss.html', 'enemy-finalboss'), ('enemy-falseknight.html', 'enemy-falseknight')]:
            page.goto(origin + BASE + route, wait_until='networkidle')
            page.wait_for_selector('.illustrated-title [data-illustration="' + picture + '"]')
            assert page.locator('.prose a .wiki-picture').count() > 0
        page.screenshot(path=str(ROOT / 'artifacts/wiki-illustrated-enemy.png'), full_page=True)
        for route in ('items.html', 'enemies.html', 'search.html?q=铜'):
            page.goto(origin + BASE + route, wait_until='networkidle')
            page.wait_for_selector('.entry-card .wiki-picture')
            assert page.evaluate('''expected => [...document.querySelectorAll('.entry-card')].every(link => {
                const id = link.getAttribute('href').split('/').pop().replace('.html','');
                return !expected.includes(id) || link.querySelector('[data-illustration="'+id+'"]');
            })''', list(expected)), route
        page.goto(origin + BASE + 'items.html', wait_until='networkidle')
        page.screenshot(path=str(ROOT / 'artifacts/wiki-illustrated-items.png'), full_page=True)
        page.get_by_role('button', name='切换深色模式').click()
        assert page.locator('html').get_attribute('data-theme') == 'dark'
        page.set_viewport_size({'width': 390, 'height': 844})
        for route in ('enemy-finalboss.html', 'items.html', 'recipes.html'):
            page.goto(origin + BASE + route, wait_until='networkidle')
            page.wait_for_selector('.wiki-picture')
            assert page.evaluate('document.documentElement.scrollWidth <= innerWidth'), route
        page.goto(origin + BASE + 'enemy-finalboss.html', wait_until='networkidle')
        page.screenshot(path=str(ROOT / 'artifacts/wiki-illustrated-mobile.png'), full_page=True)
        browser.close()
    assert not errors, errors
    assert not failed, failed
    print(json.dumps({'origin': origin, 'coverage': dict(counts), 'blank_images': 0, 'cards': True,
                      'search': True, 'detail': True, 'inline_links': True, 'mobile': True, 'errors': errors}))


if __name__ == '__main__':
    main()
