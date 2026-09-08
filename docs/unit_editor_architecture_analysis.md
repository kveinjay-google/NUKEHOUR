# 单位与阵营编辑器 — 项目架构分析（Phase 1）

> 日期：2026-07-31  
> 仓库：`ra2-mac`（红色警戒2：阿富汗危机 / OpenRA RA2）  
> 阶段目标：**只分析、不编码**。为后续「数据驱动编辑器」定边界，避免破坏现有游戏。

---

## 0. 关键结论（先读这段）

| 误解 | 事实 |
|------|------|
| 「单位写在代码里，要抽到 JSON」 | **单位/建筑/阵营已经是数据驱动**：MiniYAML（`mods/ra2/rules/*.yaml`） |
| 「要改引擎核心才能做编辑器」 | **不必改引擎核心**；用「编辑器源数据 → 生成/合并 MiniYAML 覆盖层」即可 |
| 「用 JSON 替换全部 YAML」 | **高风险**；会丢掉 `Inherits`、`-Trait` 移除、武器引用、Fluent、lint 管线 |

**推荐架构（与现有 `ai_profiles.py` 同构）：**

```
编辑器 UI（Mac / Python）
   │  读写 JSON（人类友好的编辑源）
   │  Validator 校验
   ▼
codegen / merge
   │  产出 MiniYAML delta（仅改动的字段）
   ▼
mods/ra2/rules/editor-overrides.yaml   （或 SupportDir 用户包）
mods/ra2/weapons/editor-overrides.yaml
   │  在 mod.yaml 中排在 Rules/Weapons 列表末尾
   ▼
OpenRA MiniYaml 合并 → 游戏运行（引擎 C# 特质不变）
```

**现成先例：** [`ai_profiles.py`](../ai_profiles.py) + [`launcher_ai_profiles.json`](../launcher_ai_profiles.json) → 生成 [`mods/ra2/rules/ai-profiles.yaml`](../mods/ra2/rules/ai-profiles.yaml)，启动器保存后自动写入，游戏直接读 YAML。

---

## 1. 游戏引擎

| 项 | 内容 |
|----|------|
| 引擎 | **OpenRA** |
| 版本 | **`release-20250330`**（`mod.config` → `ENGINE_VERSION`；`engine/VERSION`） |
| 形态 | OpenRA Mod SDK：`engine/`（可重新 fetch）+ `mods/ra2` + 可选 C# 工程 |
| 本地引擎补丁 | [`engine-patches/`](../engine-patches/)（LaunchInto、i18n、高架桥等） |
| 模组程序集 | `OpenRA.Mods.Common` / `OpenRA.Mods.Cnc` / `OpenRA.Mods.RA2` |
| 启动 | `launcher.py` / `launch-game.sh` → `OpenRA.dll`，`Game.Mod=ra2`，`Engine.ModSearchPaths=<repo>/mods` |

C# 代码实现的是 **Trait 行为**（移动、生产、武器结算、体素渲染等），**不是**单位数值表。数值与科技树在 YAML。

---

## 2. 项目目录结构

```
ra2-mac/
├── engine/                 # OpenRA 引擎（gitignore，由 fetch-engine.sh 拉取）
├── engine-patches/         # 本地引擎补丁
├── mods/
│   ├── ra2/                # ★ 玩法模组（数据主体）
│   └── ra2-content/        # 正版素材安装器（非玩法）
├── OpenRA.Mods.RA2/        # RA2 专用 C# Trait
├── launcher.py             # Mac 启动器（Tk）
├── ai_profiles.py          # ★ AI 人格：JSON → YAML 的成功范式
├── ra2_files.py            # 存档/回放/素材路径
├── packaging/              # 打包
└── docs/                   # 文档（本文件）
```

### `mods/ra2/` 数据分区

| 路径 | 职责 |
|------|------|
| `mod.yaml` | 清单：Rules / Weapons / Sequences / Fluent / FileSystem |
| `rules/` | Actor、World、Player、AI（单位/建筑/阵营入口） |
| `weapons/` | 武器（伤害、射程、射速、Versus） |
| `sequences/` | SHP 序列；`voxels.yaml` = 体素模型表 |
| `fluent/` (+ `zh-CN/`) | 显示名、描述 |
| `bits/` | 覆盖素材（cameo、建筑 shp 等，挂载优先级最高） |
| `chrome/` | UI 布局 |
| `maps/` | 内置地图 |
| `audio/` | 语音/提示音 |

用户数据目录（macOS）：`~/Library/Application Support/OpenRA/`  
（`settings.yaml`、`Content/ra2/`、`Saves/`、`Replays/`、`maps/`）

---

## 3. 当前单位系统

### 定义方式

每个可生产单位是一个 **Actor**（MiniYAML 节点），通过 `Inherits:` 继承抽象模板（`^Infantry` / `^Vehicle` / `^Aircraft` / `^Ship`），再覆盖具体 Trait。

### 关键文件

| 类型 | 文件 |
|------|------|
| 模板 | `rules/defaults.yaml`（`^Infantry`、`^Vehicle`、`^Aircraft`、`^Ship`…） |
| 盟军步兵 | `rules/allied-infantry.yaml` |
| 苏军步兵 | `rules/soviet-infantry.yaml` |
| 盟军载具 | `rules/allied-vehicles.yaml` |
| 苏军载具 | `rules/soviet-vehicles.yaml` |
| 空军 | `rules/aircraft.yaml` |
| 海军 | `rules/allied-naval.yaml`、`soviet-naval.yaml` |

### 编辑器关心的 Trait（数值层）

| Trait | 字段 | 含义 |
|-------|------|------|
| `Valued` | `Cost` | 造价 |
| `Health` | `HP` | 生命 |
| `Mobile` / `Aircraft` | `Speed` | 速度 |
| `Armor` | `Type` | 护甲类型 |
| `Armament` | `Weapon` | 武器名 → `weapons/*.yaml` |
| `Buildable` | `Queue`, `Prerequisites`, `BuildPaletteOrder` | 生产队列与科技树 |
| `Tooltip` | `Name` | Fluent 键 |

示例（灰熊坦克 `mtnk`）：`Cost: 700`、`HP: 300`、`Speed: 105`、`Weapon: 105mm`、`Prerequisites: ~gaweap`。

复杂行为（幻影、超时空矿车、感染、驻军等）依赖 **C# Trait + 条件表达式**，不宜第一期用扁平 JSON 完整表达；编辑器第一期应只暴露「安全可改字段」。

---

## 4. 当前建筑系统

| 文件 | 内容 |
|------|------|
| `rules/allied-structures.yaml` | 盟军基地建筑 |
| `rules/soviet-structures.yaml` | 苏军基地建筑 |
| `rules/tech-structures.yaml` | 科技中立建筑 |
| `rules/civilian-structures.yaml` | 平民建筑 |

模式：`Inherits: ^BaseBuilding` + `Building.Footprint/Dimensions` + `Buildable.Queue`（`Building` / `Support`）+ `Production`（建造厂）+ `ProvidesPrerequisite`（解锁科技名）。

生产队列定义在 `rules/player.yaml`：`ClassicProductionQueue@Building|Support|Infantry|Vehicle|Aircraft|Ship`。

**科技树不是独立表**，而是：

1. 建筑 `ProvidesPrerequisite: structures.allies`（可带 `Factions:`）  
2. 单位/建筑 `Buildable.Prerequisites: ~structures.allies, ~gaweap`  

前缀语义（OpenRA）：

- `~foo`：需要且缺省时从面板隐藏  
- `!foo` / `~!foo`：取反  

---

## 5. 当前阵营系统

定义在 `rules/world.yaml` → `^BaseWorld`：

| InternalName | Side |
|--------------|------|
| america, germany, england, france, korea | Allies |
| cuba, libya, iraq, russia, china | Soviets |
| Random / randomallies / randomsoviets | 随机 |

显示名：Fluent（`fluent/rules.ftl` / `zh-CN`）。

开局单位：`StartingUnits@…`（同文件），按 `Factions:` 绑定。  
**已知缺口：** `china` 在阵营列表中，但部分苏军 StartingUnits 的 `Factions:` 未包含 china——编辑器若动阵营需一并修。

大厅科技等级：`player.yaml` → `ProvidesTechPrerequisite@…` → `techlevel.*`。

**今日没有「第三可玩阵营」（尤里）完整科技树**；自定义阵营属于后续 Phase，需新建 Faction + Prerequisite 链 + 起始单位 + 建筑/单位归属。

---

## 6. 模型与资源管理

| 层 | 机制 |
|----|------|
| 步兵/部分建筑 | SHP + `sequences/*.yaml` |
| 坦克/舰船 | VXL + `sequences/voxels.yaml` + `RenderVoxels` |
| 图标 | `*icon.shp`（cameo.mix / `bits/cameos/`） |
| 正版素材 | SupportDir `Content/ra2/*.mix` |
| 模组覆盖 | `mods/ra2/bits/**`（FileSystem **最后挂载，优先**） |

编辑器原则（用户要求）：

- **不复制模型**，只引用资源路径 / 序列名  
- 新单位需：Actor YAML + Sequence/Voxel 引用 + Cameo + Fluent  
- 3D 预览（Phase 4）可先对接现有 `assetbrowser` / 体素管线，缺格式时再做转换接口  

---

## 7. 数值配置方式

| 数值 | 位置 |
|------|------|
| 造价 / HP / 速度 / 护甲 | Actor YAML |
| 伤害 / 射程 / 射速 / Versus | `weapons/*.yaml` |
| 建造时间 | `Buildable.BuildDuration`（-1 则由造价推导）+ Modifier |
| 精英武器 | 独立武器（如 `105mmE`）+ 条件 `Armament@elite` |
| 显示名 | Fluent，不是 YAML 内嵌中文（多数） |

`Armament.Weapon: 105mm` 必须在 Weapons 清单中存在，否则 lint/加载失败。

---

## 8. 存档与持久化

| 类型 | 路径 / 机制 |
|------|-------------|
| 游戏存档 | `~/Library/Application Support/OpenRA/Saves/ra2/{version}/`（`.orasav`） |
| 回放 | `…/Replays/ra2/{version}/` |
| 用户地图 | `…/maps/ra2/{version}/` |
| 引擎设置 | `…/settings.yaml` |
| 启动器配置 | 仓库内 `launcher_skirmish.json`、`launcher_ai_profiles.json` |

**今天没有「用户 Rules 覆盖」挂载点。**  
Rules 只加载 `mod.yaml` 列出的文件。要做安全编辑：

1. 生成 `editor-overrides.yaml` 并加入 `mod.yaml` Rules **末尾**；或  
2. 挂载 `~^SupportDir|…/editor-overrides.yaml`（推荐，避免脏 git 树）

地图可放用户目录；**平衡数据目前只能走 mod 清单。**

---

## 9. 启动器与现有编辑入口

[`launcher.py`](../launcher.py)：

- 工具页已有：**地图编辑器**（`Game.LaunchInto=editor`）、**素材浏览器**（`assetbrowser`）  
- AI 页：编辑 JSON → `ai_profiles.regenerate()` → 写 YAML  
- **尚无**「单位/阵营编辑器」入口  

引擎内置 Map Editor ≠ 单位平衡器；单位编辑器应是 **独立模块**（建议先做启动器侧 Python UI + codegen，再考虑游戏内 Chrome）。

---

## 10. 需要重构什么？哪些迁到外部配置？

### 10.1 不需要重构（保持不动）

- OpenRA 引擎 Trait 管线  
- `^Infantry` / `^Vehicle` 等完整继承图  
- 序列帧索引、体素光照、Chrome  
- 复杂专用 Trait（幻影、感染、超时空矿车…）的 C# 实现  

### 10.2 应新增的「外部编辑源」（不替换现有 YAML）

建议目录（与用户 Phase 2 对齐，但语义是 **编辑源 + 生成物**）：

```
data/                          # 编辑器源数据（JSON，可版本管理）
  factions/                    # 阵营元数据（侧边、颜色、起始单位类）
  units/                       # 单位可编辑字段（delta 或完整安全子集）
  buildings/                   # 建筑可编辑字段
  weapons/                     # 武器可编辑字段（可选一期）
  schema/                      # JSON Schema + Validator

mods/ra2/rules/
  editor-overrides.yaml        # ★ 生成物：MiniYAML delta，给引擎读

Mods/                          # 用户导出包（后续 Phase）
  MyFaction/
    mod_version
    faction.json
    units/ …
```

**关键：游戏运行时仍读 MiniYAML；JSON 是编辑器与版本控制的源，不是第二套运行时。**

若坚持「游戏直接读 JSON」，则必须新增 C# 加载器——**违反「不改核心」原则且收益低**，不推荐。

### 10.3 轻量重构（允许、可控）

| 项 | 说明 |
|----|------|
| `mod.yaml` | Rules/Weapons/Fluent **末尾**增加 overrides 条目 |
| 启动器 | 增加「单位编辑器」入口；保存时 codegen + 可选校验 |
| 解析器 | Python MiniYAML 子集解析（只读 base，算有效字段） |
| Validator | 独立模块；失败阻止写入 overrides |
| china 起始单位 | 修 `StartingUnits` Factions 列表（顺带正确性） |

### 10.4 禁止的破坏性做法

- 把全部 `rules/*.yaml` 一次性改写成 JSON  
- 在引擎里 hardcode 单位表  
- 编辑器直接改 `defaults.yaml` 巨型继承树而不做备份/diff  
- 未校验就覆盖生产用 YAML  

---

## 11. 与用户阶段计划的对齐（修正版路线）

用户总目标不变；落地顺序建议如下（**不跳阶段**）：

### Phase 1（本阶段）✅

架构分析文档（本文件）。

### Phase 2 — 数据驱动「可编辑层」（原用户「第二阶段」的正确实现）

1. 定义编辑器 JSON Schema（Faction / Unit / Building / Weapon 安全字段）  
2. 从现有 YAML **导入快照**（只读解析 → `data/`），保证 100% 对照现网  
3. Validator  
4. Codegen → `editor-overrides.yaml`  
5. `mod.yaml` 挂载  
6. 验收：改灰熊 HP 1000→2000 → 进游戏生效；未改单位行为不变  

> 注意：用户文中的 `data/factions|units|buildings` **保留**，但作为编辑源；  
> 「让游戏读取这些数据」= 读取 **由其生成的 MiniYAML**，而不是让引擎原生吃 JSON。

### Phase 3 — 编辑器 UI

Mac 风格（可先 Tk/SwiftUI/WebView，建议先复用启动器 Tk 或独立 `editor/` 包），左阵营 / 中列表 / 右属性。

### Phase 4 — 模型浏览

引用 sequences/voxels；旋转缩放；无则转换接口。

### Phase 5+ — 新阵营、Mod 包、测试、手册

按用户原文 Phase 6–10 推进。

---

## 12. 风险与约束

| 风险 | 缓解 |
|------|------|
| MiniYAML 继承难完整展开 | 一期只编辑「叶子 Actor 上已显式写出的字段」+ 常用覆盖；深度继承用引擎 lint 验收 |
| 改武器影响多单位 | UI 标明共享武器；支持「克隆武器再改」 |
| 自定义阵营缺美术/语音 | 先允许复用现有序列；新美术走 bits 引用 |
| overrides 与手改 YAML 冲突 | overrides 文件专属编辑器写入；禁止手改；git 分离 |
| 联机不同步 | 覆盖层必须进入 mod 版本哈希；文档标明「改数据需双方一致」 |

---

## 13. Phase 1 交付清单

- [x] 引擎与目录结构说明  
- [x] 单位 / 建筑 / 阵营数据流说明  
- [x] 模型与数值配置说明  
- [x] 存档与覆盖层缺口说明  
- [x] **明确：无需把单位从 C# 抽出（本就不在 C#）**  
- [x] **明确：外部配置 = JSON 编辑源 + MiniYAML 生成物**  
- [x] **明确：禁止破坏性替换现有 rules**  
- [x] 推荐复用 `ai_profiles.py` 模式  

---

## 14. 下一阶段入口（待你确认后开始编码）

**Phase 2 第一步建议任务：**

1. 新增 `data/schema/`（Unit/Building/Faction 安全字段 Schema）  
2. 实现 `tools/rules_inventory.py`：扫描 `mod.yaml` Rules，导出只读清单  
3. 实现 `tools/editor_codegen.py`：JSON delta → MiniYAML  
4. 挂载空的 `editor-overrides.yaml`，跑 `make test` / skirmish，确认零行为变化  

完成后再做「改一个坦克血量」的端到端验收。

---

*文档结束。未经确认进入 Phase 2 前，不修改游戏 Rules / 引擎代码。*
