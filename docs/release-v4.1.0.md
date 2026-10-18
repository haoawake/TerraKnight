## 4.1.0 — 修复与平衡补丁

- 下调残破容器血量：普通／困难／专家／大师（空洞）分别为 40000／51000／56000／71400，失落近亲保持独立数值。
- 核查全部 79 件魔法武器蓝耗，移除阶段平衡对蓝耗的覆盖，修复其中 74 件偏低问题；水矢恢复 10、月曜 9、终极棱镜每次扣费 12。修复持续施法不读取重铸减蓝词缀的问题，保留连发周期与持续光束各自的扣费时机。
- Wiki 合成配方改为每页 40 条、按需加载；图鉴首次显示 60 条，全文搜索内容在输入关键词后再加载。
- 新增“空洞”难度：暂时沿用大师的数值、掉落、减伤和饰品栏规则，仅此难度恢复原版面具数量增伤；大师及以下不再叠加面具数量。已有存档难度保持不变。
- 盔甲锻造上限随装备阶段变为 +3、+6、+9、+12、+15、+18，每级 +1 防御；单件敲满总价仍约 5000–30000 吉欧，不含失败消耗，成功率仍为 50%。旧装备超出新上限的等级保留。
- 稀有特殊装备增加第二种普通怪物获取来源，优先分散到另一地区；每次仍只掉一件。基础掉率至少普通怪 1%、精英 2.5%，已有较高概率再提高约 25%。
- 所有普通饰品统一提高掉率：普通怪共享饰品池 2%→3%，精英 5%→7.5%，候选较多时增加有限的池预算；首领重复击杀饰品池 20%→25%。闪亮红气球等探索饰品同样生效。
- 修复幸运币在召唤物、部分哨兵及其他命中入口不触发的问题；钱币戒指和贪婪戒指同步生效，铜币零钱增加可见入账提示，避免同一击重复抽选。
- 宝藏磁铁（游戏内名“宝藏磁石”）现在也能吸引吉欧。额外范围为 150 像素，与金戒指同时装备时使用较大范围，不重复相加。
- 掉落物黑名单、怪物图鉴、圣巢背包和快捷商店四个按钮移到弹药栏右侧，尺寸保持不变。
- 梦魇之王格林在死亡演出完成后、离开梦境前直接发放战利品，不再等待场景卸载。
- 修复箱子重命名和图鉴搜索的中文输入：显式启用输入法、显示组合文字、定位候选框，处理候选词 Enter/Esc/退格与保存/退出冲突，恢复焦点及键盘设备切换后的输入。

验证：Release 构建与对应离线逻辑回归；本次未进行实机游玩和完整联机测试。安装时退出游戏，将压缩包解压覆盖到游戏目录，再通过原来的“空洞泰拉.bat”启动。

---

## 4.1.0 — Fixes and balance patch

- Reduced Broken Vessel health to 40000/51000/56000/71400 on Normal/Hard/Expert/Master (Hollow). Lost Kin retains its separate budget.
- Audited all 79 magic weapons and restored Terraria mana defaults, fixing 74 reduced costs. Water Bolt costs 10, Lunar Flare 9 and Last Prism 12 per payment. Held spells now honor mana prefix modifiers, preserving burst and channel payment timing.
- Wiki recipes now load on demand in pages of 40. Catalogs initially render 60 entries, with full search data loaded only when a query is entered.
- Added Hollow difficulty: currently inherits Master's stats, loot, defense rules and accessory slots, but restores original mask-count damage multipliers. Master and lower difficulties keep normalized single-hit damage. Existing saves retain their difficulty.
- Armor forging caps now scale by equipment stage: +3, +6, +9, +12, +15 and +18, with +1 defense per level. Full-upgrade prices remain about 5,000–30,000 Geo per piece, excluding failures. The 50% success rate and legacy over-cap levels remain.
- Specialty equipment gains a second ordinary-monster source, preferably in another eligible region. It still drops one item at a time, with modest independent chance increases.
- Increased general accessory pool rates: ordinary enemies 2% to 3%, elites 5% to 7.5%, and repeat-boss pools 20% to 25%, with bounded compensation for large pools. Includes exploration items such as the Shiny Red Balloon.
- Fixed Lucky Coin effects missing from minion, sentry and other hit paths; Coin Ring and Greedy Ring inherit the fix. Copper change now has a visible payout notice, without duplicate rolls.
- Treasure Magnet now attracts Geo too, adding 150 pixels of range. Combined with Gold Ring, the larger range applies instead of adding both bonuses.
- Moved the blacklist, Bestiary, Hallownest bag and quick-shop buttons beside the ammo column without changing their size.
- Nightmare King Grimm now pays loot after his death sequence, before leaving the dream.
- Fixed Chinese IME support in chest renaming and Bestiary search, including composition, candidate positioning, Enter/Esc/Backspace conflicts, focus recovery and keyboard replacement.

Validation: Release build and relevant offline logic checks. No gameplay or full multiplayer testing was performed for this patch. Exit the game, extract the archive over the game folder, then launch through the existing TerraKnight BAT.