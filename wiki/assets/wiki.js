(() => {
  'use strict';
  function openReleaseFromHash() {
    let id;
    try { id = decodeURIComponent(location.hash.slice(1)); } catch { return; }
    const release = document.getElementById(id);
    if (release?.matches('details.release-note')) release.open = true;
  }
  openReleaseFromHash();
  window.addEventListener('hashchange', openReleaseFromHash);
  document.querySelectorAll('a[href^="#changelog-"]').forEach(link => link.addEventListener('click', () => {
    const release = document.getElementById(link.hash.slice(1));
    if (release?.matches('details.release-note')) release.open = true;
  }));
  const base = '/terraknight-wiki/';
  const version = '?v=' + (document.documentElement.dataset.wikiBuild || 'dev');
  let theme = 'light';
  try { if (localStorage.getItem('terraknight-wiki-theme') === 'dark') theme = 'dark'; } catch {}
  const themeButton = document.createElement('button');
  themeButton.className = 'theme-toggle';
  themeButton.type = 'button';
  function applyTheme() {
    document.documentElement.dataset.theme = theme;
    themeButton.setAttribute('aria-label', theme === 'dark' ? '切换浅色模式' : '切换深色模式');
    themeButton.setAttribute('aria-pressed', String(theme === 'dark'));
    themeButton.textContent = theme === 'dark' ? '☀ 浅色' : '☾ 深色';
    themeButton.title = themeButton.getAttribute('aria-label');
  }
  themeButton.addEventListener('click', () => {
    theme = theme === 'dark' ? 'light' : 'dark';
    applyTheme();
    try { localStorage.setItem('terraknight-wiki-theme', theme); } catch {}
  });
  document.querySelector('.topbar')?.append(themeButton);
  applyTheme();
  const escape = value => String(value).replace(/[&<>"']/g, c => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
  let illustrations = null;
  let illustrationRequest = null;
  function picture(id, label, size) {
    const image = illustrations?.[id];
    if (!image) return '';
    const scale = size / image.cell;
    const style = `width:${size}px;height:${size}px;background-image:url(${base}assets/illustrations/${image.sheet}${version});background-size:${image.sheet_width * scale}px ${image.sheet_height * scale}px;background-position:${-image.x * scale}px ${-image.y * scale}px`;
    return `<span class="wiki-picture ${image.kind}" role="img" aria-label="${escape(label)}" data-illustration="${escape(id)}" style="${style}"></span>`;
  }
  function decorate(root = document) {
    if (!illustrations) {
      if (!illustrationRequest && (root.querySelector('a[href*="/item-"],a[href*="/enemy-"]') || /\/(item-|enemy-)/.test(location.pathname))) {
        illustrationRequest = fetch(base + 'assets/illustrations/manifest-lite.json' + version).then(response => {
          if (!response.ok) throw new Error('illustrations'); return response.json();
        }).then(groups => {
          illustrations = {};
          for (const [kind, group] of Object.entries(groups)) {
            const {entries,...sheet} = group;
            for (const [id,x,y] of entries) illustrations[id] = {...sheet,kind,x,y};
          }
          decorate();
        }).catch(() => { illustrationRequest = null; });
      }
      return;
    }
    root.querySelectorAll('a[href]').forEach(link => {
      if (link.querySelector('.wiki-picture')) return;
      const path = link.getAttribute('href').split('?')[0];
      const match = path.match(/\/(item-[\w-]+|enemy-[\w-]+)\.html$/);
      if (!match || !illustrations[match[1]]) return;
      const label = link.querySelector('h3')?.textContent || link.textContent;
      link.insertAdjacentHTML('afterbegin', picture(match[1], label, link.classList.contains('entry-card') ? 64 : 24));
    });
    const key = location.pathname.split('/').pop().replace(/\.html$/, '');
    const heading = document.querySelector('.article-heading h1');
    if (heading && illustrations[key] && !heading.querySelector('.wiki-picture')) {
      heading.insertAdjacentHTML('afterbegin', picture(key, heading.textContent, illustrations[key].kind === 'enemy' ? 112 : 80));
      heading.classList.add('illustrated-title');
    }
  }
  decorate();
  function saved() { try { return JSON.parse(localStorage.getItem('terraknight-wiki-saved') || '[]'); } catch { return []; } }
  const bookmark = document.querySelector('.bookmark');
  if (bookmark) {
    const refresh = () => { const active = saved().includes(bookmark.dataset.id); bookmark.setAttribute('aria-pressed', String(active)); bookmark.textContent = active ? '已收藏 ★' : '收藏词条 ☆'; };
    refresh();
    bookmark.addEventListener('click', () => {
      const items = new Set(saved()); const id = bookmark.dataset.id;
      if (items.has(id)) items.delete(id); else items.add(id);
      try { localStorage.setItem('terraknight-wiki-saved', JSON.stringify([...items])); refresh(); }
      catch { const toast = document.querySelector('.toast'); toast.textContent = '浏览器未允许保存收藏'; toast.classList.add('show'); setTimeout(() => toast.classList.remove('show'), 2500); }
    });
  }
  document.addEventListener('keydown', event => {
    if (event.key === '/' && !event.ctrlKey && !event.metaKey && !event.altKey && !/INPUT|TEXTAREA|SELECT/.test(document.activeElement.tagName)) {
      event.preventDefault(); const input = document.querySelector('input[type=search]');
      if (input) input.focus(); else location.href = base + 'search.html';
    }
  });
  function pageNumber(value) {
    const n = Number(value);
    return Number.isSafeInteger(n) && n > 0 ? n - 1 : 0;
  }
  function pagination(root, change) {
    const bars = ['上方', '下方'].map((position, i) => {
      const nav = document.createElement('nav');
      nav.className = 'pagination';
      nav.setAttribute('aria-label', position + '分页');
      if (i) root.after(nav); else root.before(nav);
      nav.addEventListener('click', event => {
        const button = event.target.closest('button[data-page]');
        if (!button || button.disabled) return;
        change(Number(button.dataset.page));
        if (i) bars[0].scrollIntoView({block: 'start'});
      });
      nav.addEventListener('submit', event => {
        event.preventDefault();
        const input = nav.querySelector('input');
        if (input.checkValidity()) change(Number(input.value) - 1);
        if (i) bars[0].scrollIntoView({block: 'start'});
      });
      return nav;
    });
    return (page, pages, busy = false) => bars.forEach(nav => {
      nav.hidden = pages <= 1;
      const button = (target, label, disabled = false, current = false) => `<button type="button" data-page="${target}" ${disabled || busy ? 'disabled' : ''} ${current ? 'aria-current="page"' : ''}>${label}</button>`;
      const numbers = [...new Set([0, page - 1, page, page + 1, pages - 1])].filter(n => n >= 0 && n < pages).sort((a, b) => a - b);
      nav.innerHTML = button(page - 1, '上一页', page === 0) + '<div class="page-numbers">' + numbers.map((n, i) => (i && n > numbers[i - 1] + 1 ? '<span aria-hidden="true">…</span>' : '') + button(n, n + 1, false, n === page)).join('') + '</div>' + button(page + 1, '下一页', page + 1 >= pages) + `<form class="page-jump"><label>跳至 <input type="number" min="1" max="${pages}" value="${page + 1}" aria-label="跳转页码" required ${busy ? 'disabled' : ''}> / ${pages} 页</label><button ${busy ? 'disabled' : ''}>前往</button></form>`;
    });
  }
  document.querySelectorAll('.prose .table-scroll').forEach(root => {
    if (root.closest('[data-recipes]')) return;
    if (root.dataset.chunkTable) {
      const total = Number(root.dataset.total), pages = Math.ceil(total / 24);
      const body = root.querySelector('tbody'), cache = new Map([[0, [...body.children].map(r => r.outerHTML)]]);
      const summary = document.createElement('p'); summary.className = 'muted'; summary.setAttribute('aria-live', 'polite'); root.before(summary);
      const param = 'table-' + root.dataset.chunkTable;
      let revision = 0;
      const update = pagination(root, draw);
      async function draw(page) {
        page = Math.max(0, Math.min(page, pages - 1));
        const current = ++revision; update(page, pages, true); body.setAttribute('aria-busy', 'true');
        try {
          let rows = cache.get(page);
          if (!rows) {
            const response = await fetch(`${base}data/tables/${root.dataset.chunkTable}/${page}.json${version}`);
            if (!response.ok) throw new Error('table page'); rows = await response.json(); cache.set(page, rows);
            if (cache.size > 5) cache.delete(cache.keys().next().value);
          }
          if (current !== revision) return;
          body.innerHTML = rows.join(''); decorate(body);
          summary.textContent = `共 ${total} 条 · 第 ${page + 1} / ${pages} 页 · 每页 24 条`;
          const state = new URLSearchParams(location.search);
          if (page) state.set(param, String(page + 1)); else state.delete(param);
          history.replaceState(null, '', location.pathname + (state.size ? '?' + state : '') + location.hash);
        } catch { if (current === revision) summary.textContent = '表格加载失败，请重试翻页。'; }
        finally { if (current === revision) { update(page, pages); body.removeAttribute('aria-busy'); } }
      }
      draw(pageNumber(new URLSearchParams(location.search).get(param)));
      return;
    }
    const rows = [...root.querySelectorAll('tbody > tr')];
    if (rows.length <= 24) return;
    const size = 24, pages = Math.ceil(rows.length / size);
    const summary = document.createElement('p');
    summary.className = 'muted';
    summary.setAttribute('aria-live', 'polite');
    root.before(summary);
    const updatePages = pagination(root, draw);
    function draw(page) {
      rows.forEach((row, i) => { row.hidden = i < page * size || i >= (page + 1) * size; });
      summary.textContent = `共 ${rows.length} 条 · 第 ${page + 1} / ${pages} 页`;
      updatePages(page, pages);
    }
    draw(0);
  });
  const recipeBrowser = document.querySelector('[data-recipes]');
  if (recipeBrowser) {
    const query = document.querySelector('#recipe-query');
    const stage = document.querySelector('#recipe-stage');
    const count = document.querySelector('#recipe-count');
    const label = document.querySelector('#recipe-page');
    const tableBody = document.querySelector('#recipe-results tbody');
    const state = new URLSearchParams(location.search);
    query.value = state.get('q') || '';
    stage.value = state.get('stage') || '';
    let page = pageNumber(state.get('page')), index = [], filtered = [], revision = 0;
    const updatePages = pagination(document.querySelector('#recipe-results'), target => { page = target; draw(); });
    const chunks = new Map();
    function chunk(id) {
      if (!chunks.has(id)) chunks.set(id, fetch(`${base}data/recipes/${id}.json${version}`).then(r => {
        if (!r.ok) throw new Error('recipe page'); return r.json();
      }).catch(error => { chunks.delete(id); throw error; }));
      return chunks.get(id);
    }
    async function draw() {
      const current = ++revision;
      const pages = Math.max(1, Math.ceil(filtered.length / 40));
      page = Math.min(page, pages - 1);
      updatePages(page, pages, true);
      const rows = filtered.slice(page * 40, (page + 1) * 40);
      count.textContent = '正在加载配方…';
      tableBody.setAttribute('aria-busy', 'true');
      const params = new URLSearchParams();
      if (query.value.trim()) params.set('q', query.value.trim());
      if (stage.value) params.set('stage', stage.value);
      if (page) params.set('page', String(page + 1));
      history.replaceState(null, '', location.pathname + (params.size ? '?' + params : ''));
      try {
        const ids = [...new Set(rows.map(r => r.chunk))];
        const data = new Map(await Promise.all(ids.map(async id => [id, await chunk(id)])));
        if (current !== revision) return;
        tableBody.innerHTML = rows.length ? rows.map(r => data.get(r.chunk)[r.row]).join('') : '<tr><td colspan="5">没有找到匹配配方。</td></tr>';
        count.textContent = `找到 ${filtered.length} 条配方 · 每页最多 40 条`;
        label.textContent = `第 ${page + 1} / ${pages} 页`;
        updatePages(page, pages);
        decorate(tableBody);
      } catch {
        if (current === revision) {
          count.textContent = '配方加载失败，请重试翻页或搜索。';
          updatePages(page, pages);
        }
      } finally { if (current === revision) tableBody.removeAttribute('aria-busy'); }
    }
    function filter(reset = true) {
      if (reset) page = 0;
      const terms = query.value.trim().toLocaleLowerCase().split(/\s+/).filter(Boolean);
      filtered = index.filter(r => (!stage.value || r.stage === stage.value) && terms.every(t => r.search.includes(t)));
      draw();
    }
    let timer;
    query.addEventListener('input', () => { clearTimeout(timer); timer = setTimeout(filter, 150); });
    stage.addEventListener('change', () => filter());
    fetch(base + 'data/recipes/index.json' + version).then(r => {
      if (!r.ok) throw new Error('recipe index'); return r.json();
    }).then(data => { index = data.map(r => ({...r, search: r.text.toLocaleLowerCase()})); filter(false); })
      .catch(() => { count.textContent = '配方索引加载失败，请刷新重试。'; });
  }
  const browser = document.querySelector('[data-browser]');
  if (!browser) return;
  const input = document.querySelector('#browse-query');
  const stage = document.querySelector('#stage-filter');
  const category = document.querySelector('#category-filter');
  const kind = document.querySelector('#kind-filter');
  const onlySaved = document.querySelector('#saved-filter');
  const results = document.querySelector('#browse-results');
  const count = document.querySelector('#result-count');
  let records = [], filtered = [], fullLoaded = false, fullRequest = null;
  const pageSize = 24;
  const catalogIndex = {'物品': 'items', '怪物': 'enemies', 'NPC': 'npcs'}[browser.dataset.browser];
  const searchable = data => data.map(record => ({...record, search: `${record.title} ${record.id} ${record.category} ${record.text}`.toLocaleLowerCase()}));
  const params = new URLSearchParams(location.search);
  let page = pageNumber(params.get('page'));
  const updatePages = pagination(results, target => { page = target; draw(); });
  input.value = params.get('q') || '';
  if (stage) stage.value = params.get('stage') || '';
  if (category) category.value = params.get('category') || '';
  if (kind) kind.value = params.get('kind') || '';
  if (onlySaved) onlySaved.checked = params.get('saved') === '1';
  function draw() {
    const pages = Math.max(1, Math.ceil(filtered.length / pageSize));
    page = Math.min(page, pages - 1);
    const state = new URLSearchParams(location.search);
    if (page) state.set('page', String(page + 1)); else state.delete('page');
    history.replaceState(null, '', location.pathname + (state.size ? '?' + state : '') + location.hash);
    const query = input.value.trim().toLocaleLowerCase();
    count.textContent = `找到 ${filtered.length.toLocaleString()} 条记录 · 已显示 ${filtered.length ? page * pageSize + 1 : 0}–${Math.min((page + 1) * pageSize, filtered.length)} 条 · 第 ${page + 1} / ${pages} 页`;
    results.innerHTML = filtered.length ? '<div class="entry-grid">' + filtered.slice(page * pageSize, (page + 1) * pageSize).map(record => {
      let snippet = '';
      if (query) {
        const pos = record.text.toLocaleLowerCase().indexOf(query);
        const start = Math.max(0, pos - 28);
        snippet = (start ? '…' : '') + record.text.slice(start, start + 115) + '…';
      }
      return `<a class="entry-card" href="${escape(record.url)}"><span class="entry-type">${escape(record.category)}${record.stage ? ' · 阶段 ' + escape(record.stage) : ''}</span><h3>${escape(record.title)}</h3>${snippet ? `<p class="entry-snippet">${escape(snippet)}</p>` : ''}<span class="entry-action">查看词条 ↗</span></a>`;
    }).join('') + '</div>' : '<div class="empty">没有找到匹配词条。试试更短的关键词，或清除筛选条件。</div>';
    updatePages(page, pages);
    decorate(results);
  }
  let searchWorker = null, searchRevision = 0;
  function filter(reset = true, workerRows = null) {
    if (reset) page = 0;
    const query = input.value.trim().toLocaleLowerCase();
    if (query && workerRows === null && typeof Worker !== 'undefined') {
      const revision = ++searchRevision;
      if (!searchWorker) {
        searchWorker = new Worker(base + 'assets/search-worker.js' + version);
        searchWorker.onmessage = ({data}) => {
          if (data.revision !== searchRevision) return;
          if (data.error) { count.textContent = '搜索内容加载失败，请重新输入重试。'; return; }
          filter(false, data.rows);
        };
        searchWorker.onerror = () => { count.textContent = '搜索线程加载失败，请刷新重试。'; };
      }
      count.textContent = '正在检索全部文字…';
      searchWorker.postMessage({revision, query, url:location.origin + base + (catalogIndex ? `data/${catalogIndex}-index.json` : 'search-index.json') + version,
        kind:kind?.value || (browser.dataset.browser !== 'all' ? browser.dataset.browser : ''), stage:stage?.value || '', category:category?.value || '', onlySaved:onlySaved?.checked || false, saved:saved()});
      return;
    }
    if (!query) ++searchRevision;
    if (query && workerRows === null && !fullLoaded) {
      count.textContent = '正在载入搜索内容…';
      if (!fullRequest) fullRequest = fetch(base + (catalogIndex ? `data/${catalogIndex}-index.json` : 'search-index.json') + version)
        .then(r => { if (!r.ok) throw new Error('search'); return r.json(); })
        .then(data => { records = searchable(data); fullLoaded = true; })
        .catch(error => { fullRequest = null; throw error; });
      fullRequest.then(() => filter(false)).catch(() => { count.textContent = '搜索内容加载失败，请重试输入。'; });
      return;
    }
    const tokens = query.split(/\s+/).filter(Boolean);
    const favorites = new Set(saved());
    filtered = workerRows || records.filter(record => {
      if (browser.dataset.browser !== 'all' && record.kind !== browser.dataset.browser) return false;
      if (stage && stage.value && record.stage !== stage.value) return false;
      if (category && category.value && record.category !== category.value) return false;
      if (kind && kind.value && record.kind !== kind.value) return false;
      if (onlySaved && onlySaved.checked && !favorites.has(record.id)) return false;
      return tokens.every(token => record.search.includes(token));
    });
    if (query) filtered.sort((a,b) => {
      const score = r => r.title.toLocaleLowerCase() === query ? 0 : r.title.toLocaleLowerCase().includes(query) ? 1 : r.id.includes(query) ? 2 : 3;
      return score(a) - score(b);
    });
    const state = new URLSearchParams();
    if (query) state.set('q', input.value.trim());
    if (stage && stage.value) state.set('stage', stage.value);
    if (category && category.value) state.set('category', category.value);
    if (kind && kind.value) state.set('kind', kind.value);
    if (onlySaved && onlySaved.checked) state.set('saved', '1');
    history.replaceState(null, '', location.pathname + (state.size ? '?' + state : ''));
    browser.querySelectorAll('[data-filter]').forEach(group => {
      const control = group.dataset.filter === 'stage' ? stage : category;
      group.querySelectorAll('a[data-value]').forEach(link => {
        if (control && link.dataset.value === control.value) link.setAttribute('aria-current', 'true');
        else link.removeAttribute('aria-current');
        const target = new URLSearchParams(state);
        if (link.dataset.value) target.set(group.dataset.filter, link.dataset.value);
        else target.delete(group.dataset.filter);
        link.href = location.pathname + (target.size ? '?' + target : '');
      });
    });
    draw();
  }
  let timer;
  input.addEventListener('input', () => { clearTimeout(timer); timer = setTimeout(filter, 120); });
  [stage, category, kind, onlySaved].filter(Boolean).forEach(element => element.addEventListener('change', filter));
  browser.querySelectorAll('[data-filter] a[data-value]').forEach(link => link.addEventListener('click', event => {
    event.preventDefault();
    const control = link.closest('[data-filter]').dataset.filter === 'stage' ? stage : category;
    if (control) { control.value = link.dataset.value; filter(); }
  }));
  fetch(base + (catalogIndex ? `data/${catalogIndex}-browse.json` : 'data/browse-index.json') + version).then(response => { if (!response.ok) throw new Error('index'); return response.json(); })
    .then(data => { if (!fullLoaded) records = searchable(data); filter(false); })
    .catch(() => { count.textContent = '搜索索引暂时不可用。请刷新重试；下方静态词条仍可直接阅读。'; });
})();
