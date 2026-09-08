# RA2 Mac/iOS 代码研究记录

本目录用于维护可由源码验证的项目架构知识。研究基线为分支 `engine-20250330`，起始提交 `3138e92`。工作区含大量尚未提交的用户扩展，因此记录必须区分：上游 OpenRA 引擎、RA2 Mod、iOS 移植、桌面启动/打包工具，以及当前开发中的未提交内容。

## 研究原则

- 每项结论指向具体文件与符号，不用目录名代替证据。
- 明确区分“已从源码确认”“已在 iPad 实测”“日志支持的推断”和“待验证假设”。
- 涉及资源加载时同时检查源码树、构建产物、已安装 App 和 Documents 支持目录。
- 涉及画面问题时按地形、迷雾、注释层、调试几何、Widget 五层分别排查。
- 涉及卡死时记录逻辑帧、网络帧、渲染帧、生命周期和最后一个非阻塞边界。
- 不覆盖或混入工作区中既有的用户修改。

## 文档索引

- [源码清单](source-inventory.md)
- [启动、平台与 iOS 生命周期](bootstrap-platform-ios.md)
- [游戏循环、网络与卡死诊断](game-loop-network-hangs.md)
- [渲染、地形、迷雾与覆盖层](rendering-terrain-overlays.md)
- [输入、触摸、命令与 UI](input-touch-orders-ui.md)
- [RA2 规则与内容图](ra2-rules-content.md)
- [构建、签名、安装与设备测试](build-deploy-device-testing.md)
- [架构总索引](architecture-index.md)
- [后续开发检查表](development-checklist.md)

## 当前已纠正的认识

- 右下角 FPS/Tick/Render 文本由用户主动启用，应保留；它与网格是否存在没有因果关系。
- 截图中的菱形线条外观与 `TerrainGeometryOverlay` 高度相似，但从规则中移除 Trait 后设备仍显示，证明仅凭外观不能完成归因。
- 在确认实际加载的 Mod 来源、安装包内容和绘制调用前，不再把该网格归结为单一 Trait。

## 验证基线（2026-08-13）

- `./utility.sh --check-yaml`：通过所有 RA2 规则与地图检查；存在既有未使用 Fluent key 和 Bot 名称缺失警告。
- `OpenRA.Test`：172/172 通过。
- 签名 `ios-arm64` Release build：0 error、8 warnings。
- 重新构建前的 App 曾通过 `codesign --verify --deep --strict`；当前新构建返回 `CSSMERR_TP_NOT_TRUSTED`，安装前必须重新验证证书信任状态，不能据此声称当前包已通过签名门禁。
- 网格与卡死均已形成更准确的诊断范围，但尚未声称修复；后续必须按专项 A/B 和插桩方案在物理 iPad 验证。
