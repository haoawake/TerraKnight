'use strict';
let records, request;
self.onmessage = async ({data}) => {
  try {
    if (!request) request = fetch(data.url).then(r => {
      if (!r.ok) throw new Error('search index'); return r.json();
    }).then(rows => records = rows.map(r => ({...r, search: `${r.title} ${r.id} ${r.category} ${r.text}`.toLocaleLowerCase()})))
      .catch(error => { request = null; throw error; });
    await request;
    const tokens = data.query.toLocaleLowerCase().split(/\s+/).filter(Boolean), saved = new Set(data.saved);
    const rows = records.filter(r => (!data.kind || r.kind === data.kind) && (!data.stage || r.stage === data.stage)
      && (!data.category || r.category === data.category) && (!data.onlySaved || saved.has(r.id))
      && tokens.every(t => r.search.includes(t)));
    const score = r => r.title.toLocaleLowerCase() === data.query ? 0 : r.title.toLocaleLowerCase().includes(data.query) ? 1 : r.id.includes(data.query) ? 2 : 3;
    rows.sort((a,b) => score(a) - score(b));
    self.postMessage({revision:data.revision, rows:rows.map(({search,...r}) => {
      const pos = r.text.toLocaleLowerCase().indexOf(data.query), start = Math.max(0,pos-28);
      return {...r, text:(start ? '…' : '') + r.text.slice(start,start+180)};
    })});
  } catch (error) { self.postMessage({revision:data.revision, error:String(error)}); }
};
