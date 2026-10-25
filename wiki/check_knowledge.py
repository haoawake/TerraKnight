"""Coverage and real Chrome responsiveness checks for the expanded local wiki."""
import html
import json
import re
import time
from pathlib import Path
from playwright.sync_api import sync_playwright

ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'artifacts/wiki-preview/terraknight-wiki'
BASE='http://127.0.0.1:8098/terraknight-wiki/'


def main():
    meta=json.loads((OUT/'build-info.json').read_text('utf-8'))
    source=ROOT/meta['release']['snapshot']
    knowledge=json.loads((source/'tools/encyclopedia_knowledge.json').read_text('utf-8'))
    details=json.loads((source/'tools/encyclopedia_details.json').read_text('utf-8'))
    texts='\n'.join(p.read_text('utf-8') for p in OUT.glob('text-*.html'))
    # Readers see the dialogue, while developer lookup keys remain in private exports.
    def readable(value):
        value=html.unescape(html.unescape(value))
        value=re.sub(r'<[^>]*>','',value)
        return re.sub(r'\s+','',value)
    rendered=readable(texts)
    missing=[r['Id'] for r in knowledge['dialogue'] if readable(r['Text']) not in rendered]
    assert not missing,missing[:30]
    assert meta['knowledge_coverage']['presentation']=='player'
    assert not list(OUT.glob('fsm-*.html'))
    assert not re.search(r'HealthManager|PlayMaker|内部\s*ID|源码',texts)
    assert sum(r['Changed'] for r in knowledge['dialogue'])==len(json.loads((source/'src/TerraKnight/Resources/terrarian-dialogue.json').read_text('utf-8')))
    assert len(list(OUT.glob('ai-*.html')))==meta['entries']['怪物']
    assert meta['knowledge_coverage']['ai_profiles']==len(knowledge['profiles'])==110
    assert all((OUT/('godhome-'+k.lower().replace('@','-')+'.html')).exists() for k in details['godhome'])
    assert '返还期望' in (OUT/'casino.html').read_text('utf-8')
    assert '36' in (OUT/'quest-dialogue.html').read_text('utf-8')
    # The highest-stakes catalog must use the actual purchase function's prices.
    guide=(OUT/'npc-1.html').read_text('utf-8')
    assert '1000 吉欧' in guide and '500 吉欧' in guide
    pages=['knowledge.html','dialogue.html','quest-dialogue.html','fishing.html','casino.html',
           'fishing-topic-1-1.html','ai-crawler.html','godhome-falseknight.html','native-shop-room_shop.html']
    metrics=[]; errors=[]; failed=[]
    with sync_playwright() as p:
        browser=p.chromium.launch(executable_path=r'C:\Program Files\Google\Chrome\Application\chrome.exe',headless=True)
        context=browser.new_context(viewport={'width':1440,'height':900})
        page=context.new_page()
        page.on('pageerror',lambda e:errors.append(str(e)))
        page.on('response',lambda r:failed.append((r.status,r.url)) if r.status>=400 else None)
        page.add_init_script("window.longTasks=[];new PerformanceObserver(l=>longTasks.push(...l.getEntries().map(e=>e.duration))).observe({type:'longtask',buffered:true})")
        cdp=context.new_cdp_session(page);cdp.send('Emulation.setCPUThrottlingRate',{'rate':4})
        for route in pages:
            start=time.perf_counter();page.goto(BASE+route,wait_until='networkidle')
            metrics.append(dict(route=route,elapsed_ms=round((time.perf_counter()-start)*1000),
                **page.evaluate('({dom_nodes:document.querySelectorAll("*").length,long_tasks:longTasks,html_bytes:performance.getEntriesByType("navigation")[0].encodedBodySize})')))
            assert page.locator('h1').count()==1,route
        page.goto(BASE+'godhome-falseknight.html',wait_until='networkidle')
        table=page.locator('[data-chunk-table]').first
        assert table.locator('tbody tr').count()==24
        first=table.locator('tbody tr').first.inner_text()
        page.locator('.pagination').first.get_by_role('button',name='下一页').click()
        page.wait_for_function('(first)=>document.querySelector("[data-chunk-table] tbody tr").innerText!==first',arg=first)
        assert table.locator('tbody tr').count()==24
        assert 'table-' in page.url
        page.reload(wait_until='networkidle')
        assert table.locator('tbody tr').first.inner_text()!=first
        # A fresh worker loads all text and performs matching away from the UI thread.
        page.goto(BASE+'search.html',wait_until='networkidle')
        page.wait_for_function('document.querySelectorAll(".entry-card").length===24')
        assert len(page.workers)==0
        start=time.perf_counter();page.locator('#browse-query').fill('十字路的水最近怪怪的')
        page.wait_for_selector('.entry-card[href$="quest-dialogue.html"]')
        search_ms=round((time.perf_counter()-start)*1000)
        assert len(page.workers)==1
        assert search_ms<8000,search_ms
        search_tasks=page.evaluate('longTasks')
        page.locator('#browse-query').fill('不存在的词条_fish_9999')
        page.wait_for_selector('.empty')
        page.locator('#browse-query').fill('')
        page.wait_for_function('document.querySelectorAll(".entry-card").length===24')
        page.goto(BASE+'casino.html',wait_until='networkidle')
        page.screenshot(path=str(ROOT/'artifacts/wiki-casino-desktop.png'),full_page=True)
        for width in (390,320):
            page.set_viewport_size({'width':width,'height':844})
            for route in pages:
                page.goto(BASE+route,wait_until='networkidle')
                assert page.evaluate('document.documentElement.scrollWidth<=innerWidth'),(width,route)
            page.goto(BASE+'knowledge.html',wait_until='networkidle')
            page.screenshot(path=str(ROOT/f'artifacts/wiki-knowledge-{width}.png'),full_page=True)
        browser.close()
    assert not errors,errors
    assert not failed,failed
    result=dict(coverage=meta['knowledge_coverage'],cpu_throttle=4,metrics=metrics,
        cold_full_text_search_ms=search_ms,search_main_thread_long_tasks=search_tasks,
        lazy_table_rows=24,mobile_widths=[390,320],errors=errors,failed_requests=failed)
    (ROOT/'artifacts/wiki-knowledge-check.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),'utf-8')
    print(json.dumps(result,ensure_ascii=False,indent=2))


if __name__=='__main__':main()
