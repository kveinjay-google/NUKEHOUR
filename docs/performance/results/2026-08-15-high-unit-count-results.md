# OpenRA iPad 高单位数量性能基准结果

日期：2026-08-15

设备：iPad Air 11-inch (M4)，真机，横屏 2360×1640

构建：Release / ios-arm64 / 未附加调试器

采样：每场景预热 5 秒，正式采样 30 秒

## 结论

本轮性能下降的首要瓶颈是 iOS 构建对 Mod 程序集启用了全量 Mono 解释器，而不是 GPU 填充率、AI、暂停状态、镜头内单位绘制或热降频。生成 AOT 对象注册表并关闭全量解释器后，1000 个移动单位的 FPS 中位数由 7.02 提升至 51.60（7.35 倍），World Tick p95 由 104.32 ms 降至 9.75 ms（10.70 倍）。

最终生成注册表版本复测 1000 个移动单位时：FPS p50 51.71、FPS p5 49.53、World Tick p95 9.99 ms、Render Prepare p95 1.87 ms、Present p95 0.083 ms，温度状态为 Nominal。剩余约 52 FPS 上限更符合帧调度/渲染节奏限制，而不是单位模拟已经超出单帧预算。

## 解释器与全 AOT 对比

下表的 FPS 为 p50；World 为 World Tick p95，单位 ms。

| 单位 | 状态 | 解释器 FPS | AOT FPS | 倍率 | 解释器 World | AOT World |
|---:|---|---:|---:|---:|---:|---:|
| 100 | 静止 | 52.00 | 60.01 | 1.15× | 7.58 | 2.60 |
| 100 | 移动 | 51.76 | 60.04 | 1.16× | 12.13 | 3.53 |
| 100 | 交战 | 52.09 | 60.04 | 1.15× | 7.63 | 2.82 |
| 300 | 静止 | 41.37 | 60.02 | 1.45× | 15.09 | 3.79 |
| 300 | 移动 | 20.70 | 60.04 | 2.90× | 29.27 | 5.02 |
| 300 | 交战 | 30.29 | 60.04 | 1.98× | 18.44 | 3.66 |
| 600 | 静止 | 16.56 | 54.74 | 3.30× | 31.85 | 5.22 |
| 600 | 移动 | 11.01 | 53.21 | 4.83× | 60.92 | 7.45 |
| 600 | 交战 | 15.34 | 54.99 | 3.59× | 33.12 | 5.26 |
| 1000 | 静止 | 10.21 | 51.81 | 5.07× | 53.78 | 6.90 |
| 1000 | 移动 | 7.02 | 51.60 | 7.35× | 104.32 | 9.75 |
| 1000 | 交战 | 10.17 | 51.81 | 5.09× | 53.50 | 7.12 |

原始样本保存在同目录的 `baseline-interpreter-primary` 和 `aot-primary` 文件夹中。每个 `summary.json` 包含完整逐样本数据、CPU、内存、分配、GC、温度和前 20 个分类热点。

## A/B 定位结果

| 实验 | 结果 | 判断 |
|---|---|---|
| 暂停模拟 | 1000 静止单位的 World Tick p95 降至约 0 ms，但 FPS 仍约 51.8 | 模拟已不是 AOT 版本的帧率上限 |
| 镜头移到空白区 | 1000 静止单位 FPS 51.78 → 51.76，基本不变 | 可见单位绘制不是当前上限 |
| 内部世界分辨率 50% | 1000 静止/移动 FPS 与全分辨率基本不变 | GPU 填充率不是当前上限 |
| 禁用 AI | 固定测试场景本身无 AI，解释器版本仍显著下降 | AI 不是解释器基线的主因 |
| 静止/移动/交战 | 解释器版本移动最差；AOT 后三者均接近 52–60 FPS | 热点位于被解释执行的 Actor/Activity/Trait 路径 |
| 温度状态 | 最终注册表复测为 Nominal | 提升不是热降频假象 |

## 最终 1000 移动单位指标

- FPS：p5 49.53，p50 51.71
- Frame Time：p50 16.39 ms，p95 17.07 ms
- World Tick p95：9.99 ms
- Actor Tick p95：4.06 ms
- Trait Tick p95：6.18 ms
- Render Prepare p95：1.87 ms
- Present p95：0.083 ms
- CPU：p50 7.99%，p95 8.43%
- 分配速率：86.63 MB/s
- 30 秒采样期 GC：Gen0 5292 次，Gen1 3 次，Gen2 3 次
- 峰值内存：Resident 1.26 GiB，Managed 525 MiB
- Thermal：Nominal

## 分类热点

最终注册表版本的累计耗时前列：

1. Actor Activity / Move：2264 ms
2. Trait / RenderSprites：936 ms
3. Trait / AttackTurreted：836 ms
4. Renderable Generation：466 ms
5. Trait / WithDecoration：210 ms
6. Trait / Armament：185 ms
7. Trait / Shroud：180 ms
8. Draw Submission：164 ms

这些数据已把问题定位到解释器开销并验证了 P0 修复。当前不继续盲目并行化 `World.Tick`，也不在缺乏新证据时重写渲染器。分配速率和 Gen0 次数仍偏高，适合作为后续独立 P1 工作，届时应再次用相同场景证明改动收益并验证同步、存档和回放确定性。

## 构建属性结论

- `UseInterpreter`：未启用
- `MtouchInterpreter`：空（已移除 Mod 全量解释器）
- `MtouchUseLlvm`：未显式启用
- `MtouchDebug`：未显式设置（Release 构建）
- `TrimMode`：未显式设置；本轮不依赖裁剪获得性能收益，AOT 注册表为后续裁剪提供直接构造路径
- 生成的 AOT 对象注册表包含 1186 个直接构造委托，并由覆盖测试检查四个程序集中的可构造类型。

## 已知限制

本轮基准使用确定性本地压力场景，没有加入 AI 玩家，因此 AI 降频不是本次 P0 修复范围。CPU 为进程 CPU 百分比，内存同时报告 resident 与 managed；GPU 时间目前只能由 Render Prepare、Draw Submission 和 Present 的 CPU 侧计时间接判断。后续若优化剩余 52 FPS 帧节奏，应使用 Instruments 的 Metal System Trace/Time Profiler 补充系统级证据。
