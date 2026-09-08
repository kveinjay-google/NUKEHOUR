# iOS 内核黑匣子与自动诊断中心设计

## 目标

为 iOS 开发版建立低开销、持续运行、异常退出后仍可恢复的内核飞行记录器。发生卡死、黑屏、输入失效或异常退出时，报告必须指出最后完成的步骤、仍未退出的最深调用作用域及其父调用链，并在 iPad 连接开发 Mac 后自动进入项目诊断收件箱。

## 当前问题

现有 `HangDiagnostics` 只有一个全局阶段字符串和 64 条面包屑。`logic.network-receive.enter` 包裹整个 `OrderManager.TickImmediate()`，其中可以同步执行网络发送、回放写盘、`StartGame`、地图和 Trait 初始化、GC 与音频启动，因此阶段名称不能代表真实停点。普通日志缺少统一事件序号、线程、帧和单调时钟，且异步刷新可能丢失最后数秒。旧卡死报告还会在下次启动时被覆盖。

## 总体架构

系统分为五个独立组件：

1. `DiagnosticFlightRecorder`：预分配、固定容量、线程安全的结构化事件环形缓冲区。
2. `DiagnosticWatchdog`：分别检查主循环、客户端网络、本地服务器和音频回调心跳，冻结时生成一致性快照。
3. `DiagnosticPackageBuilder`：后台生成 `.oradiag` 目录包和可读摘要。
4. `IosMetricKitSubscriber`：接收 Apple 的 hang/crash 调用栈并写入待处理报告。
5. `openra-diagnostic-bridge`：Mac 端自动发现连接设备、提取报告、符号化并写入 `.diagnostics/inbox/`。

这些组件不得依赖诊断 UI。UI 只是查看、标记和手动导出的入口，诊断核心在 UI 不响应时仍必须工作。

## 事件模型

每条事件使用固定大小数据结构，包含：

- 单调时间戳和全局递增序号；
- 托管线程 ID、子系统和整数 Event ID；
- `Begin`、`Progress`、`End` 或 `Instant` 阶段；
- `spanId` 与 `parentSpanId`；
- 逻辑帧、渲染帧和网络帧；
- 两个整数参数槽，用于 Actor ID、Trait 类型 ID、数据包类型、队列深度、资源大小或进度。

事件热路径不得格式化字符串或执行文件 I/O。Event ID 到名称、源码位置及参数语义的映射随构建版本保存。每个受监控线程还维护固定深度的活动作用域栈，因此冻结报告可以重建类似：

`StartGame > LoadComplete > PathFinder.WorldLoaded > BuildDomains(6/9)`。

## 持久化与恢复

飞行记录器使用预分配文件保存最近事件，采用双头部、序列号和校验值避免写入中断造成整体损坏。应用正常退出时写入 clean-shutdown 标记；下次启动发现未正常退出的会话时，先封存旧证据，再建立新会话，绝不覆盖旧报告。

默认资源限制：

- 环形记录文件不超过 8 MB；
- 最多保留 10 个诊断包；
- 单包默认上限 20 MB；
- 性能指标每 500 ms 采样；
- 最近画面每 10 秒异步更新一张低分辨率 PNG；
- 达到限额时删除最旧的已完成报告，不删除活动会话。

## 精细探针

### 开始游戏和世界加载

记录旧世界释放、`BeforeGameStart`、Map/Rules/Sequence 加载、World 和 WorldRenderer 构造、每个 `IWorldLoaded` 与 `IPostWorldLoaded` Trait、GC、调色板刷新、音乐解码和 `AfterGameStart`。长步骤必须发送进度事件。

### 网络和本地服务器

把即时订单发送与接收拆开；记录同步 socket 写开始/结束、字节数与耗时，后台 read 的包长度和序号、队列深度、ACK/订单/即时订单类型、回放写入以及服务器最后处理的事件和发送队列。

### 模拟

`World.Tick` 内维护当前 Actor、Trait、Activity、Effect 和 frame-end action 游标。正常项目只更新原子游标，超过慢步骤阈值或触发冻结时才展开为完整事件，避免每帧过量写入。

### 渲染、输入和音频

渲染记录各 pass、renderer/provider、widget、输入泵和 Present。输入记录触点匿名 ID、坐标变换、手势状态、命中的 widget/actor、是否消费及生成的命令，但不记录聊天正文。音频记录播放/停止/解码、活动播放数、PCM 字节数和锁等待；实时回调只更新原子心跳、缓冲长度和 underrun 计数，禁止分配、锁和文件 I/O。

## 卡死判定与现场包

前台运行时按子系统独立判断：2.5 秒无进度进入 suspected，8 秒确认冻结；声明了进度预算的加载作用域只要持续更新 Progress 就不误报。确认后看门狗直接从环形缓冲区、活动作用域和原子游标构造快照，不调用已经卡住的主线程。

`.oradiag` 包含：

- `trace.bin`、`summary.json` 和 `timeline.txt`；
- 当前最深作用域、最后完成的兄弟步骤和各线程心跳；
- CPU、resident/managed memory、GC、FPS、P95 帧耗时和 thermal/memory-warning；
- 网络、服务器、音频和模拟游标快照；
- 各日志尾部、异常日志、回放尾段；
- 最近成功保存的画面；
- Git revision、构建时间、Bundle 版本、设备/iOS 概要和关键程序集/规则资源哈希；
- MetricKit payload（如果已经交付）。

## MetricKit 与调用栈

iOS 14 及以上注册 `MXMetricManagerSubscriber`，保存 `MXHangDiagnostic.callStackTree`、hang duration 和 crash diagnostics。MetricKit 可能延迟到下次启动交付，因此它不能替代实时飞行记录器。开发构建同时用 `os_signpost` 标记核心作用域，便于 Instruments 现场抓取原生栈。每个 Release 构建保留对应 dSYM，Mac 接收桥负责符号化。

## 自动传送

Codex 任务不是常驻上传服务器，所以采用本地开发桥：

- USB 连接时使用 `xcrun devicectl` 从应用 Documents 自动提取 Pending 报告；
- 可选 Bonjour 局域网传送使用一次性配对令牌；
- 报告落入仓库 `.diagnostics/inbox/<report-id>/`；
- 生成 `summary.md`、符号化堆栈和机器可读索引；
- Mac 端监视器可由用户登录后的 LaunchAgent 持续运行，但安装 LaunchAgent 必须作为明确、可移除的开发工具步骤执行。

应用端不默认上传互联网。未来若需要多设备远程测试，可增加用户明确启用的 HTTPS/Sentry 传输器。

## 诊断中心 UI

iOS 开发版提供触摸尺寸的诊断中心：

- 当前状态和各子系统心跳；
- “标记问题”，保存前 30 秒并继续记录后 15 秒；
- “保存现场”，立即封存当前状态和最近画面；
- 最近报告列表、大小和命中作用域；
- 手动分享或删除。

关闭 UI 不影响持续录制，UI 操作不得在主线程压缩或哈希大型文件。

## 隐私与安全

默认移除聊天正文、认证数据、完整 IP、设备唯一标识及容器外路径。触点使用会话内匿名 ID。诊断包清单列出包含文件；自动提取只允许已配对的本机开发设备和指定 Bundle ID。

## 验收标准

自动测试覆盖环形轮转、并发写入、父子作用域、崩溃恢复、损坏头部恢复、报告轮转、脱敏和 MetricKit 序列化。设备故障注入覆盖：

1. `IWorldLoaded` Trait 死循环；
2. 同步 socket 写阻塞；
3. 音频混音锁等待；
4. 渲染 Present 阻塞；
5. frame-end action 卡死。

每份报告必须在一个最深作用域内命中故障，并显示父调用链和最后完成步骤。常驻模式零托管热路径分配、记录内存不超过 8 MB、平均 CPU 增量不超过 1%；增强录制模式 CPU 增量不超过 3%。
