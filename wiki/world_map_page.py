"""Markup for the static, dependency-free world atlas."""


def content(base, build):
    return f'''<link rel="stylesheet" href="{base}assets/world-map.css?v={build}">
<section class="atlas-page"><div class="atlas-heading"><div><span class="eyebrow">HALLOWNEST ATLAS</span><h1>圣巢全境地图</h1><p>逐格探索地形，寻找下一场战斗。</p></div><button id="map-fullscreen" class="button secondary">全屏地图</button></div>
<div class="atlas-app" id="atlas-app"><aside class="atlas-sidebar" aria-label="地图设置">
<label class="field">地图图层<select id="map-layer"><option value="world">主世界</option><option value="deep">室内与梦境</option><option value="all">全部图层</option></select></label>
<div class="atlas-options"><label><input type="checkbox" id="map-bosses" checked> Boss 标记</label><label><input type="checkbox" id="map-labels" checked> 地区名称</label><label><input type="checkbox" id="map-objects" checked> 场景设施</label></div>
<label class="field">寻找 Boss<input id="map-search" type="search" placeholder="名称、英文名或房间…"></label>
<p id="map-count" class="muted" role="status">正在载入地图…</p><div id="map-boss-list" class="atlas-boss-list"></div>
<details class="atlas-source"><summary>地图说明与数据来源</summary><p id="map-source"></p><p>实心圆为战斗房间，菱形为竞技场入口。标记定位到房间，不代表 Boss 在房间中的实时站位。神居与部分终局竞技场未被游戏地图图集收录，通过入口查看对应战斗。</p><p>室内与梦境沿用游戏地图布局；房间之间的空白不代表可行走道路。滚轮缩放、拖动平移，触屏可双指缩放。聚焦地图后可用方向键、+、−、0 操作。</p></details>
</aside><div class="atlas-stage"><canvas id="world-map" tabindex="0" aria-label="圣巢地图；方向键平移，加减键缩放，零键重置。Boss 也可通过左侧列表选择。"></canvas>
<div class="atlas-toolbar" aria-label="缩放控制"><button id="map-zoom-in" aria-label="放大">+</button><button id="map-zoom-out" aria-label="缩小">−</button><button id="map-reset">全图</button><button id="map-exit-fullscreen">退出全屏</button></div>
<div id="map-position" class="atlas-position" aria-live="off"></div><div id="map-detail" class="atlas-detail" hidden></div><div id="map-loading" class="atlas-loading" role="status">正在载入逐格地形…</div></div></div>
<noscript><p>互动地图需要 JavaScript。你仍可浏览<a href="{base}enemies.html?category=首领">Boss 图鉴</a>和<a href="{base}boss-summons.html">召唤物列表</a>。</p></noscript></section>
<script src="{base}assets/world-map.js?v={build}" defer></script>'''
