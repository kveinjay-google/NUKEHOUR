# RA2 触屏命令反馈指针实施计划

> **Execution mode:** 用户已确认推荐方案；连续执行，不再请求选项。严格 RED → GREEN，完成后独立规格审查与质量审查。

**Goal:** 在移动、攻击与强制攻击命令发出后，于目标点播放一次原作绿色 / 红色动态指针，同时保留未知命令（包括有独立 cursor 语义的攻击移动）的旧白色回退。

**Architecture:** 扩展通用 `OrderEffectsInfo`，用可选 order→sequence 字典选择一次性 `SpriteAnnotation`；RA2 world 配置映射原作 `mouse.shp` 序列。未配置映射的 mod 与未知命令保持旧行为。

**Technology:** C#/.NET, NUnit, OpenRA MiniYaml, RA2 sprite sequences, engine patch series.

## Task 1: 行为与规则 RED

**Files**

- Create: `engine/OpenRA.Test/OrderFeedbackEffectsTest.cs`

1. 用真实 `mods/ra2/rules/world.yaml` 解析 `OrderEffectsInfo`。
2. 断言 `Move` / `Attack` / `ForceAttack` 在 `Terrain`、`Actor`、`FrozenActor` 上返回预期序列。
3. 断言未知命令、无效目标返回 null。
4. 解析 `mods/ra2/sequences/misc.yaml`，锁定两段动画的 filename、Start、Length、Tick、ZOffset，并锁定旧 `moveflsh` 仍存在。
5. 运行 focused NUnit，确认只因缺少新 API / 配置 / 序列而失败。

## Task 2: 最小生产实现

**Files**

- Modify: `engine/OpenRA.Mods.Common/Traits/World/OrderEffects.cs`
- Modify: `mods/ra2/rules/world.yaml`
- Modify: `mods/ra2/sequences/misc.yaml`

1. 给 `OrderEffectsInfo` 添加可选 image、sequence dictionary、palette 字段及 lint attributes。
2. 添加纯映射查询；只允许 Actor / FrozenActor / Terrain。
3. 在旧 target switch 之前创建一次性 `SpriteAnnotation`，成功后返回 true；无映射时落回原分支。
4. 配置 RA2 精确命令映射、原作序列和 400ms 时序。
5. focused NUnit 转 GREEN，再跑相邻 Order / interaction 测试。

## Task 3: 持久化与包清单

**Files**

- Create: `engine-patches/0011-order-feedback-effects.patch`
- Modify: `engine-patches/README.md`
- Modify: `packaging/public-content-manifest.json`

1. 从官方 pinned release 依次应用 0001–0010，生成只包含 OrderEffects 与新测试的 0011 patch。
2. 顺序重放 0001–0011，逐字节验证目标 engine 文件与工作区一致。
3. 更新两个 RA2 YAML 的实际 size / SHA-256，分类与 `public:false` 不变。
4. 禁止添加任何零售 SHP / PAL / MIX 或临时预览。

## Task 4: 完整验证与审查

1. Focused NUnit 与相邻交互测试。
2. `./utility.sh --check-yaml`。
3. `./utility.sh --check-missing-sprites`（使用本机已导入正版内容）。
4. Common 与 RA2 Release build。
5. public bundle focused audits、manifest size / SHA 核对、`git diff --check`。
6. 检查 staged 文件清单只含本功能与规格 / 计划，不含现有 untracked 私有文件。
7. 独立规格审查通过后，再做独立代码质量与资产 / 许可审查；修复所有 Critical / Important。
8. 提交实现；不 push、不创建远端、不安装设备（除非用户另行要求）。
