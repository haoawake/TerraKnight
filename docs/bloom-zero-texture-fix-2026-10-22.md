# Bloom 零尺寸纹理与日志刷屏修复

## 已确认的问题

用户截图显示 `ArgumentException: RenderTextureDesc width must be greater than zero`，调用栈指向 `UnityStandardAssets.ImageEffects.BloomOptimized.OnRenderImage`。没有取得该玩家的原始 Player.log，截图中的 3,763,652 次、10.1 GB 和触发时机未独立核实；也不能据此断定它与三螳螂联机故障同源。

检查本机游戏 `Assembly-CSharp-firstpass.dll` 的实际实现发现：Bloom 在低质量模式下用 `source.width / 4`、`source.height / 4` 申请临时渲染纹理，高质量模式除以 2。这里没有最小尺寸检查，极小输入会得到零尺寸。当前模组此前没有保护此调用。

## 修复

`Effects/BloomGuard.cs` 为该方法增加 Harmony 前置检查。输入小于当前降采样倍数时，直接 Blit 输入到输出并跳过本帧 Bloom；尺寸恢复后自动执行原版 Bloom。不会永久关闭泛光，不屏蔽异常日志，不修改分辨率、其他渲染纹理申请或联机协议。没有输入纹理时跳过该调用。

## 实测

构建 `artifacts/bloom-guard-build`：0 个错误。

真实游戏进程中的 `bloomguard` 测试使用游戏自身 Bloom shader，并调用实际被补丁覆盖的 OnRenderImage。低/高质量各测试 1×1、1×32、32×1、3×3、4×4、64×32；小尺寸每组连续调用 100 次。39 项检查通过，700 次保护分支、5 次正常原版分支，捕获渲染错误 0 次。GPU 输出像素检查确认画面保留；恢复正常尺寸后不再走保护分支，组件仍启用。

证据：`artifacts/single-copy-bloomguard-20261022-031034/AutoTest.log` 与 `result.json`。Completed=true、TimedOut=false、Failures=[]，玩家存档无修改或新增。外围脚本 `artifacts/run-bloom-guard.ps1` 另外备份并恢复公共配置和注册表，输出 `SharedConfigRestored=True`；内部通用测试脚本的 result.json 仍保留其自身没有恢复公共配置的说明。

测试覆盖了会产生零尺寸的输入条件和恢复流程，没有复现原玩家的窗口/显示设备操作，也没有人工验收完整场景的泛光视觉效果。没有通过制造数百万条异常来复现 10 GB 日志。

## 本机安装

已在游戏关闭时备份并替换本机正式安装的 DLL、PDB。安装后 DLL 与已测试构建 SHA256 一致：`9A3CBDB62A89EFE506D7E8D818913C5ABB29EF7EA04F9B8D0036767D1B5996CE`。

原插件备份：`artifacts/before-bloom-guard-install-20261022-031144`。

该更新不会清理已经生成的大日志。旧日志若需要调查，应先保留有效样本；确认不再需要后，在游戏关闭时可以删除 Player.log 释放空间。
