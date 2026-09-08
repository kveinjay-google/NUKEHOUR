# iOS High-Unit-Count Performance Design

## Goal

建立可重复、可自动化、低扰动的 iPad Release 性能基准，区分解释器、模拟、渲染、GC、同步和热降频，并只优化数据排名前三的热点。

## Architecture

扩展现有 `Benchmark` 为一次有明确元数据、预热期和 30 秒采样期的 session。session 从现有 `PerfHistory`、资源采样器和一个只在 benchmark 模式开启的分类 profiler 读取数据，输出原始 CSV、`summary.json` 和 top-20。正常游戏路径只保留一个 disabled 分支，不进行逐 Actor/Trait 计时或磁盘写入。

固定场景控制器通过 iOS 启动环境变量自动进入 Official Tournament Map A，按稳定 ActorID 顺序创建 100/300/600/1000 个相同单位，并切换 idle、move、combat、camera-visible、camera-empty、paused、AI 和 50% internal-resolution 条件。每个结果带完整场景键，Mac 端脚本按矩阵逐次启动、等待报告、拉取并汇总。

AOT 改造是独立 A/B 阶段。构建期生成 registry，覆盖 manifest、YAML、widget logic、TraitInfo、projectile、warhead、loader 和带 `UseCtor` 的动态构造类型。iOS `ObjectCreator` 优先直接委托，桌面继续原反射；完整性测试扫描 RA2 内容，缺一个名称就失败。只有 registry 版本能启动并通过规则/地图测试后才移除 Mod interpreter。

## Measurements

每个样本记录 monotonic timestamp、logic/render frame、FPS、frame/tick/actor/trait/render-prepare/world/widgets/present ms、allocation bytes、GC generation counts、CPU、resident/managed memory、thermal state、actor/effect/renderable/batch 数。聚合输出 FPS p5/p50、耗时 p50/p95、allocations MB/s、GC delta 和各分类总耗时/调用数/最大值。

Profiler 分类至少覆盖 Actor Activity、每种 ITick Trait、pathfinding、target search、AI、shroud/fog、effects/projectiles、renderable generation、VXL、sprite batching、draw submission 和 present。Trait 名称在 session 开始前缓存；benchmark 未启用时不创建字典、不读时钟。

## Benchmark Purity

Release benchmark 模式关闭逐帧 debug log、HangDiagnostics 细粒度 stage、`glGetError` 和任何临时 framebuffer/viewport 诊断，但保留启动失败、一次性 framebuffer 完整性检查和 watchdog heartbeat。报告落盘在采样结束后执行，采样期间不逐帧格式化字符串或 flush 文件。

## Determinism and Safety

不并行化 World.Tick。任何后续并行候选必须读取不可变快照，并按 ActorID 固定顺序提交结果。固定场景使用相同地图、出生区域、actor 类型、随机种子、镜头矩形和命令 tick；失败、内存警告、后台切换或 thermal state 变化会写入结果并标记该 run 无效，而不是静默混入比较。

## Acceptance

基准矩阵每个场景可自动运行 30 秒并生成 schema 校验通过的报告；正常 Release 的额外 CPU 小于 1%，benchmark profiler 模式额外 CPU 小于 5%；AOT registry 完整性测试覆盖全部 RA2 动态类型；每项优化都附相同场景的前后数据，且不得只以平均 FPS 或主观手感验收。
