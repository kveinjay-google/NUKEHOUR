# OpenRA iPad 高单位数量性能审计

日期：2026-08-14
目标设备：iPad Air 11-inch (M4), iPad16,8
目标构建：Release / ios-arm64 / 无调试器

## 已确认事实

`dotnet msbuild` 的最终属性为：

- `UseInterpreter`：未设置；
- `MtouchInterpreter`：`OpenRA.Mods.RA2,OpenRA.Mods.Common,OpenRA.Mods.Cnc`；
- `MtouchUseLlvm`：未设置；
- `MtouchDebug`：未设置；
- `TrimMode`：未设置；
- 项目显式设置 `MtouchLink=None` 和 `EnableAssemblyILStripping=false`。

因此并非全程序解释执行，但三个 Mod 程序集仍由解释器执行。RA2 单位的绝大多数 Trait、AI、寻路、索敌和渲染 Trait 位于这些程序集，单位数量增加时可能形成线性放大的 P0 瓶颈。它必须通过同一真机场景的解释器/AOT A/B 数据确认。

iOS OpenGL ES 路径的 `OpenGL.CheckGLError()` 在没有调试消息回调时调用 `glGetError()`。该调用仍位于 draw、blend、clear、buffer 和 texture 等热路径。临时的逐帧 `glCheckFramebufferStatus` 已从 `DrawElements` 移除；剩余 framebuffer 完整性检查只发生在 framebuffer 创建时，属于正确的初始化校验。

现有 `Benchmark` 每个逻辑 tick 保存 `PerfHistory.LastValue`，游戏结束才输出逐指标 CSV。它没有固定 30 秒窗口、帧分位数、分配率、GC 次数、热状态、场景元数据或热点排名，不能作为本阶段的验收工具。

现有可复用设施包括：

- `PerfSample` / `PerfHistory`：已有 tick、render、render_prepare、render_world、render_widgets、render_flip、tick_actors、bot_tick 等阶段；
- `RuntimeResourceSampler`：已有进程 CPU、resident memory 和 managed memory；
- `DiagnosticTrace`：适合卡死时间线，不适合逐 Actor 性能统计；
- `IOS_AUTOTEST` 启动链：可自动进入固定地图；
- `Benchmark` 启动参数：可扩展为自动化基准而不引入生产 UI。

## 基准解释规则

FPS 输出 p5/p50，frame time 输出 p50/p95。低 FPS 尾部对应 p5，而不是 p95。每个场景预热 5 秒、采样 30 秒，记录 build、设备、单位数、状态、镜头、暂停、内部渲染比例、AI 和解释器/AOT 模式。

分类依据：暂停恢复指向模拟；空镜头或 50% 内部分辨率恢复指向渲染；关闭 AI 恢复指向 AI；高 allocations/GC 与长帧对齐指向 GC；present 高而 CPU 不高指向 GPU/同步；随 thermal state 恶化持续下降指向热降频；同场景 AOT 显著恢复指向解释器。

## 当前结论与限制

目前只能确认两个 P0 候选：Mod 解释器和 Release 热路径 `glGetError`。尚无 100/300/600/1000 单位的真机数据，因此不对索敌、寻路、AI、World.Tick 或渲染器作主观优化，也不并行化 World.Tick。

后续所有性能结论必须引用同一场景的基线与复测 JSON/CSV。函数级采样器不可用时，报告只称为“已插桩阶段/Trait 前 20”，不冒充完整函数调用栈。
