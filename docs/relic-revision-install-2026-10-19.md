# 三件修订圣物接入游戏

已替换螳螂领主、战斗姐妹和加利安的完整物品贴图，并接入独立的 50×50 雕像与原版 48×64 底座。名称、物品 ID 与存档映射保持不变，其余 45 件沿用现有游戏资源。

放置时采用原生像素比例，保留整体浮动、方向翻转、金光和场景生命周期。独立底座只绘制底部区域，透明上部不重复绘制。既有摆放对象在游戏重新加载后使用新图。

Release 构建 0 错误。隔离存档 relics 套件 22/22 通过，实际摆放的三件均为本次修订对象；检查纹理规格、摆放占格、雕像比例、金光增亮、持续浮动、隐藏、重载和挖除。游戏渲染截图已人工检查。

证据：artifacts/resume-relics-20261019-144729/result.json 与 screenshots/Town_relics_camera.png。原玩家存档新增 0、修改 0，设置已恢复。退出阶段存在原游戏 MusicRegion.FadeOut 的空引用，发生在套件完成之后。

安装结果：artifacts/relic-revision-install-result.json。源图、安装前 DLL/PDB 备份：artifacts/relic-install-before-20261019。安装文件与本次构建 DLL 字节一致。
