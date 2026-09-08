# 架构总索引

## 子系统文档

| 主题 | 研究记录 | 主要入口 |
|---|---|---|
| 启动/设置/iOS | [bootstrap-platform-ios.md](bootstrap-platform-ios.md) | `AppDelegate.FinishedLaunching`、`Game.InitializeAndRun` |
| 主循环/网络/卡死 | [game-loop-network-hangs.md](game-loop-network-hangs.md) | `Game.Loop`、`OrderManager`、`NetworkConnection` |
| 渲染/网格 | [rendering-terrain-overlays.md](rendering-terrain-overlays.md) | `Game.RenderTick`、`WorldRenderer`、`TerrainSpriteLayer` |
| 触摸/命令/UI | [input-touch-orders-ui.md](input-touch-orders-ui.md) | `Sdl2Input`、`TouchGestureAdapter`、`UnitOrderGenerator` |
| RA2 规则/内容 | [ra2-rules-content.md](ra2-rules-content.md) | `mods/ra2/mod.yaml`、Actor/Trait YAML |
| 构建/设备 | [build-deploy-device-testing.md](build-deploy-device-testing.md) | `OpenRA.iOS.csproj`、`devicectl` |

## 常见任务的源码入口

| 任务 | 首查文件/符号 |
|---|---|
| iPad 启动失败 | `ios/OpenRA.iOS/AppDelegate.cs`；`Game.Initialize`；`graphics.log` |
| 触摸无响应 | `Sdl2Input.PumpInput`；`TouchGestureAdapter`；`Widget.HandleInput` |
| 选中/移动失败 | 世界交互控制器；`UnitOrderGenerator.OrderForUnit`；`Mobile.ResolveOrder` |
| 基地车展开失败 | `CommandBarLogic.PerformDeployOrderOnSelection`；`Transforms`；MCV YAML |
| 整个游戏冻结 | `HangDiagnostics`；`Game.InnerLogicTick`；`OrderManager.TickImmediate` |
| 地形/迷雾纹路 | `TerrainSpriteLayer`；`ShroudRenderer`；`combined.frag`；`Sprite` UV |
| 侧边栏/菜单布局 | RA2 Chrome YAML；CustomCommandBarWidget；Settings ChromeLogic |
| 新增单位 | `mod.yaml` 清单；Actor YAML；Sequence；Weapon；Fluent；资源 |
| 尤里阵营 | Yuri rules/sequences/weapons；world faction/starting units；editor overrides |
| 安装包未更新 | csproj BundleResource；App bundle 哈希；签名时间；设备覆盖安装 |

## 五条关键调用链

### 启动

`UIKit -> AppDelegate -> Platform override -> Game.InitializeAndRun -> Settings -> Renderer/Sound -> InstalledMods -> InitializeMod -> Run`

### 逻辑帧

`Loop -> LogicTick -> InnerLogicTick -> TickImmediate -> Connection.Receive -> TryTick -> ProcessOrders -> World.Tick -> TickRender`

### 渲染帧

`RenderTick -> PrepareRenderables -> Terrain -> Actors -> Shroud -> Overlays -> Annotations -> UI -> PumpInput/Present`

### 触摸订单

`SDL_FINGER* -> TouchGestureAdapter -> MouseInput -> Ui/Widget -> World interaction -> UnitOrderGenerator -> World.IssueOrder -> OrderManager -> local Server -> ResolveOrder`

### 物理设备部署

`source/config -> dotnet ios-arm64 build -> BundleResource/AOT/link -> codesign -> bundle hash audit -> devicectl install -> terminate/launch -> container logs/screenshots`

## 当前问题诊断状态

### 网格纹路

已确认：

- 不是右下角 PerfText。
- 不是 `TerrainGeometryOverlay` 单独造成，因为纹路覆盖未探索黑区，而该 Trait 明确跳过未探索格。
- 删除 Trait 后设备纹路不变；该错误修改已撤回。
- 地形和 shroud 都经 `TerrainSpriteLayer` 的每格 quad 绘制。

当前最强假设：iOS GLES 下的 sprite-sheet UV 精度、纹理边缘或 world framebuffer 二次缩放产生瓦片采样缝。仍需单变量 A/B 包在物理 iPad 上验证后才能定案。

### 卡死

已确认：

- OpenRA 主循环把逻辑、渲染和输入泵放在同一线程，所以同步阻塞会形成“画面定住、按钮无响应、进程不退出”。
- 本地遭遇战仍经真实 loopback TCP Server。
- 一次真实卡死最后停在 `TickImmediate` 边界，连接状态为 Connected。
- 后台 socket read 不直接运行在主线程。

当前范围：主线程同步 `NetworkStream.Write` 或即时订单处理。需要更细 breadcrumb 才能精准区分。

## 风险清单

| 等级 | 风险 | 控制措施 |
|---|---|---|
| 高 | 工作树、App bundle、设备安装版本不一致 | 每次安装前记录目标文件哈希与签名时间 |
| 高 | 主线程网络写/订单回调卡死整个 UIKit | 细化插桩；避免无限同步 IO；记录 server/client 双侧状态 |
| 高 | GLES shader/UV 与桌面行为不同 | 物理设备单变量 A/B；shader highp 与 UV inset 测试 |
| 高 | 大量脏工作区改动被混入提交 | 精确暂存；提交前展示完整 staged list |
| 中 | `Platform.IsIOS` 与 `OperatingSystem.IsIOS()` 分裂 | 新平台策略明确选择并测试两者 |
| 中 | 覆盖安装保留旧设置/内容 | 区分 Bundle 与 Documents；避免盲目清容器 |
| 中 | 反射/AOT 裁剪导致类型缺失 | 保持 linker 配置；lint + Release AOT build + 设备加载 |
| 中 | Chrome 放大视觉但未扩大命中区 | 同时验证 Bounds、滚动、遮罩与真实 tap |
| 中 | Yuri 扩展依赖不完整 | 单独维护内容清单，lint 所有 Actor/Weapon/Sequence |
| 低 | `TryTick` 等待网络帧导致整 UI 冻结 | 它会返回并继续 render，现有源码反对此归因 |

## 证据等级

- A：物理 iPad 单变量复现并有日志/截图/包哈希。
- B：测试与源码调用链共同证明。
- C：源码强推断，尚未设备验证。
- D：外观或时间相关性假设。

任何修复在对外声称完成前，核心结论必须达到 A；纯逻辑工具可以用 B。
