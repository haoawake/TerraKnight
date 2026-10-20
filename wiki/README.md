# 泰拉圣巢wiki

## 全境互动地图

`map.html` 从游戏导出的 `worldatlas.bin` 生成，与游戏世界地图共用 `WorldMapLayout.cs` 坐标、`WorldMapLayers.cs` 图层和逐格配色。先导出地图资源，再运行常规构建：

```powershell
python wiki/prepare_world_map.py "F:\SteamLibrary\steamapps\common\Hollow Knight\BepInEx\TerraKnight\worldatlas.bin"
python wiki/build.py
```

资源生成到 `artifacts/wiki-world-map/`，构建时复制到站点的 `assets/world-map/`。无需地图服务、CDN 或在线 API。支持滚轮、拖动、双指缩放、方向键、缩放按钮、全屏，以及主世界／室内与梦境／全部图层；Boss 列表支持中英文名称与场景搜索，分享 `map.html#boss=Hornet` 可直接定位。

图集覆盖当前布局的全部 379 个房间、10,255,600 个格子。48 个 Boss 含宝藏袋／圣物区分的四个变体；常规战斗优先使用 `BossArena.cs` 的真实竞技场，避免代表场景把大黄蜂初战放到王国边缘。38 个标记定位到战斗房间，另 10 个竞技场没有图集地形，明确显示入口标记（神居捷径、黑卵神殿、斗兽场看台），并保留真实竞技场 ID。不推测竞技场地形或 Boss 实时站位。图集为原始地形，不含玩家存档改造。

`python wiki/check_world_map.py` 独立验证房间尺寸、图集覆盖、48 个 Boss 与召唤物链接、入口位置、完整站点内部链接，并输出验收记录。

仅上线地图、保留线上原百科快照时，在本机交互终端运行 `python wiki/deploy_world_map.py --self-signed`。它先从现站核实所有地图关联词条，再隐藏提示宝塔 API 密钥；密钥只保存在进程内存。地图资源和共享脚本在服务器独立暂存，验证后替换；通过修改共享脚本给既有页面增加导航及 Boss 定位入口，保留原正文。所有改动保存在独立备份目录；发布后下载全部更新文件逐一比对 SHA-256，失败时恢复备份。`--self-signed` 仅针对现有宝塔面板的自签名证书，公开站点下载仍校验证书。该脚本只为本次连接直接联网，不修改系统代理。验收结果在 `artifacts/wiki-map-deploy-result.json`，日志在 `artifacts/wiki-map-deploy.log`，两者均不记录密钥。

从项目根目录的 `泰拉圣巢本地百科全书.md` 生成静态游戏 Wiki。网页无需数据库或服务端运行时，不包含宝塔凭据。

线上地址：https://haoawake.xyz/terraknight-wiki/ 。部署目录为 `/www/wwwroot/homepage/terraknight-wiki`，与已有 `/terraknight/` 专区独立。

```powershell
python -m pip install -r wiki/requirements.txt
python wiki/build.py
python -m http.server 8098 --directory artifacts/wiki-preview
```

本地预览地址为 `http://localhost:8098/terraknight-wiki/`。可发布文件位于 `artifacts/wiki-preview/terraknight-wiki/`，上传到已有站点根目录的 `terraknight-wiki` 子目录即可。支持普通静态目录访问，不需要修改主站路由。

重新生成百科后再次运行构建命令即可更新网站。每个物品、怪物和城镇 NPC 均有独立地址；搜索覆盖正文，支持类型、物品分类和阶段筛选。收藏保存在读者自己的浏览器中。

宝塔 API 客户端 `tools/bt_wiki.py` 从标准输入或隐藏输入读取凭据，凭据不放进站点包。部署前必须检查目标目录、原有文件和 Web 配置。

验证命令（需要本机 Chrome 和 Python Playwright）：

```powershell
python wiki/check.py
python wiki/check.py https://haoawake.xyz
```

验证涵盖全量本地链接与页面、中文搜索、收藏持久化、阶段筛选、无结果状态、多阶段生命表和移动端横向溢出检查。桌面、手机和生命表截图保存在 `artifacts/wiki-*.png`。

首次部署可运行以下命令，并在隐藏提示中输入 API 密钥。客户端使用 64 KB 分块上传，将 ZIP 保存在服务器 `/www/backup/` 的独立目录，再解压新专区；目标已存在时拒绝覆盖。首页入口更新前也会创建并核对原首页备份。

```powershell
python tools/bt_wiki.py --panel https://123.207.78.205:8888 --self-signed deploy --bundle artifacts/terraknight-wiki.zip
python tools/bt_wiki.py --panel https://123.207.78.205:8888 --self-signed homepage
```

`--self-signed` 仅用于当前宝塔面板的自签名证书；正式站点 HTTPS 验证保持开启。后续替换 Wiki 内容时，先备份线上专区，再上传新生成的静态文件；不要覆盖主站或原有游戏专区。


## 游戏图标与怪物肖像

通过隔离副本的 `wikiassets` 套件导出游戏实际使用的物品图标（含动画帧数、内部物品 ID）和猎人日志肖像映射，不手工按相似名称猜图。资源保留在本机 `artifacts/`，不将原版美术提交到源码目录。

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File tools/resume-test.ps1 -Suite wikiassets -TimeoutSec 240
python wiki/prepare_illustrations.py artifacts/<隔离运行目录>/saves/wiki-assets --hollow-knight-data "F:\SteamLibrary\steamapps\common\Hollow Knight\hollow_knight_Data"
python wiki/build.py
```

生成器按当前枚举和掉落表逐项交叉检查全部物品纹理 ID 和怪物记录，不使用固定旧数量。物品动画取第一帧；怪物按 Unity 精灵的真实网格裁切，避免紧密拼接图集中的邻图混入。输出两张透明 WebP 图集与一份坐标清单，列表、搜索、配方、掉落链接和详情页共享使用。梦境/场景变体遵循游戏自身的猎人日志肖像对应关系，原版共用肖像的条目也会共用原图。


## 更新已有线上 Wiki

`tools/bt_wiki.py` 的 `update` 动作先把新版解压到独立暂存目录，核对关键页面与数据，再将旧 Wiki 完整移到服务器备份目录，最后移入新版。切换失败时尝试恢复旧目录；主站首页和游戏专区会逐项核对不变。旧备份保留，不自动删除。

```powershell
python tools/bt_wiki.py --panel https://123.207.78.205:8888 --self-signed update --bundle artifacts/terraknight-wiki.zip
python wiki/verify_release.py https://haoawake.xyz
python wiki/check.py https://haoawake.xyz
python wiki/check_illustrations.py https://haoawake.xyz
```

发布前先用 `SkipPluginInstall=true` 构建隔离测试 DLL，并保存该次构建的源码 SHA256 清单；运行 `wikiassets` 和 `healthmath`，随后导入：

```powershell
python tools/refresh_wiki_snapshots.py artifacts/<wikiassets运行目录> artifacts/<healthmath运行目录> --build-hashes artifacts/wiki-source-hashes.json
python tools/generate_encyclopedia.py
```

导入器要求两次测试完成、正式存档未变、设置已恢复，并检查源码与构建快照一致。百科生成器进一步逐条比较运行时物品阶段、配方和掉落（包括任选组、概率分子分母、数量和解锁阶段）。血池场景拆分继续使用已核验的 `healthbook` 快照，其依赖源码变动时必须重测。完整构建会将上次本地输出移到独立目录，避免已删除词条残留。静态资源、图集与搜索索引带构建版本，避免更新后沿用旧缓存。

若血量或场景识别源码变化，可将完整 `healthbook` 运行目录作为导入器的第二个参数，同时刷新公式和场景部位。物品最终名称、阶段、来源、属性及纹理由同次 `wikiassets` 的实际数据库导出，固定掉落保留难度限定；未启用的圣物明确标注，不列为可获取物品。宠物、地区鱼饵与苔藓的额外击杀抽选另列于玩法系统和对应物品来源。

`python wiki/check_drops.py` 逐行核对全部怪物掉落的物品、数量、阶段、概率/任选组及物品页反向链接。源码正在变动时先复制到独立快照目录再构建，通过 `TK_WIKI_SOURCE_ROOT` 为生成器、导入器和检查脚本指定同一目录；页面标明工作区实测快照，避免将未发布数据标作正式版本。

## 图鉴分类与阶段筛选

怪物图鉴提供小怪、精英、首领快捷分类，沿用 `EnemyDrops.IsBossRecord` / `IsEliteRecord` 的判定：首领优先，其余小首领或原始生命至少 70 的记录为精英。分类写入百科、详情页和搜索索引，可与战斗阶段和关键词组合筛选。

物品图鉴、全物品分类索引和全站搜索均覆盖全部物品的配置阶段 0–5；分类索引与物品图鉴共用筛选和分页，不再只有静态分类链接。快捷阶段入口保留已有分类与搜索条件，URL 可复制并在刷新后恢复。配置阶段是物品标签，实际获取条件以词条为准。

`python wiki/check_filters.py` 对照隔离导出逐项检查所有物品在三个入口的六个阶段、全部怪物的分类与阶段组合、分页完整性、组合条件、URL 恢复和手机布局。

## 版本更新日志

导航的“更新日志”只显示按新到旧排列的版本折叠列表，不包含介绍、Wiki 更新摘要或重复的版本索引。每个版本默认收起，点击标题展开或收起完整官方发布说明；独立版本页继续供搜索和分享。发布时间统一显示为洛杉矶时间。

`python wiki/update_releases.py` 只读获取公开 Releases 并更新本地 `wiki/releases.json` 缓存，不包含草稿；随后运行 `python wiki/build.py` 重建网站。常规构建直接读取缓存，无需联网。缓存内容计入构建版本，更新发布说明后可刷新页面缓存。

## 列表分页

物品、怪物、NPC 与搜索列表每页显示 24 条，长表格超过 24 行时也分页。合成配方仍每页加载 40 条。列表上下均提供页码、上一页、下一页和跳页；只有一页时隐藏分页栏。图鉴和搜索的页码随筛选条件写入 URL，刷新后保留；修改筛选条件回到第一页。

运行 `python wiki/check_pagination.py` 验证翻页、跳页、刷新恢复、边界页码、空结果、全部目录覆盖与桌面和手机宽度；运行 `python wiki/check_performance.py` 验证配方分块和全文索引按需加载。先按上文启动本地预览服务。
