# RA2 规则、Trait 与内容图

## 内容根节点

`mods/ra2/mod.yaml` 是 RA2 Mod 的加载根。它定义：

- Westwood 包格式与 Content 文件系统
- 原版 RA2/YR MIX 包和 Mod 自带覆盖资源的挂载顺序
- 系统地图与用户地图目录
- Rules、Sequences、ModelSequences、Weapons、Voices、Notifications、TileSets
- ChromeLayout、Fluent、Hotkeys、Metrics、Fonts
- 三个程序集：`OpenRA.Mods.Common.dll`、`OpenRA.Mods.Cnc.dll`、`OpenRA.Mods.RA2.dll`
- `RectangularIsometric` 60x30 地图网格和深度缓冲
- 默认 `UnitOrderGenerator`

只有被这些清单直接或间接引用的 YAML 才进入运行规则。向目录新增文件并不会自动加载。

## Actor 与 Trait 构造

MiniYAML actor 节点通过 `Inherits` 合并父模板，Trait 名映射到程序集中的 `*Info` 类型。ObjectCreator 先构造 Info/Ruleset，再在 World 创建 Actor 时由 Info 创建运行时 Trait。常见后缀 `@name` 允许同一 Actor 挂载多个同类型 Trait；`-TraitName` 用于移除继承 Trait。

主要行为关系：

- `IIssueOrder`：声明可产生的订单和 Targeter。
- `IResolveOrder`：收到同步订单后的行为入口。
- `Activity`：Actor 的跨 tick 行为队列，如移动、进入、攻击、变形。
- `ConditionalTrait`：根据 condition 启用/禁用。
- `INotify*`：Actor 生命周期、伤害、生产、部署等事件。
- `IRender*`：不同绘制层的输出。
- `ISync`：进入确定性世界状态和同步哈希的数据。

## RA2 专用程序集

`OpenRA.Mods.RA2/` 约 70 个源码文件，职责分为：

- 活动：弹道导弹、资源超时空传送、驻军进入/退出、感染、直升机部署。
- 弹体/渲染：电弧、辐射束、Arc Laser、带颜色格子。
- 单位系统：驻军、航母/子机、导弹生成器、心灵控制、幻影坦克、感染、自动维修。
- 支援能力：天气控制、天气调色、生成导弹条件。
- UI：自定义命令栏、选择命令栏、自动维修、FPS 开关、电力计和 iOS 布局。
- 文件/工具：AudioBag 和 RA2 地图导入。

规则文件通过 Trait/Projectile/Warhead 的 YAML 名称实例化这些类型。新增 C# 类型必须同时确认 AOT 保留、程序集被 manifest 加载、YAML 引用和 lint。

## 三阵营基地链

| 阵营 | 基地车 | 基地建筑 | 关键 Trait |
|---|---|---|---|
| 盟军 | `amcv` | `gacnst` | `Mobile`、`Transforms`、`BaseBuilding` |
| 苏军 | `smcv` | `nacnst` | `Mobile`、`Transforms`、`BaseBuilding` |
| 尤里 | `pcv` | `yacnst` | `Mobile`、`Transforms`、`BaseBuilding` |

基地车展开依赖目标 footprint 可放置；基地建筑重新收起还受 `global-factundeploy`、`factundeploy` 和 force-move 规则约束。移动与展开共享订单/主循环但使用不同 Trait，所以两者同时失效时应优先检查输入和 OrderManager。

## 当前 Yuri 扩展状态

工作区含未跟踪或未提交的 Yuri infantry/vehicles/structures/naval、武器、序列、cameo、建筑 SHP 及 editor override。`mod.yaml` 已把它们加入清单，`world.yaml` 已加入 Yuri 随机阵营、子阵营和 starting units。

这组内容不能视为上游稳定基线。后续开发需要验证：

- 所有 YAML 引用的 Actor、Weapon、Sequence、Voice、Palette 和素材均存在。
- `pcv/yacnst` 的资源、footprint、生产 prerequisite 和 starting unit 链完整。
- AOT 下新 C# Trait 不会因反射裁剪缺失。
- 编辑器专用 override 不进入正常世界，正常规则也不会只在编辑器中定义。

## 规则加载风险

- 同名 Trait 的 `@instance` 后缀是身份的一部分，覆盖时必须匹配完整键。
- Rules 顺序会影响继承目标是否已可解析及后续 override 的最终值。
- ContentPackages 的先后决定同名 Westwood 资源的覆盖者；注释说明原 RA2 素材优先于 YR 共享素材，而 Mod `bits` 最后覆盖内容包。
- 地图可以携带自定义 Rules/Sequences；设备问题必须同时检查地图嵌入规则。
- `AllowUnusedFluentMessagesInExternalPackages: false` 会让本地化清理更严格，但现有 lint 中仍可能出现非阻断未使用 key 警告。
- `EnableDepthBuffer: true` 让地形、Voxel、Sprite secondary depth 和 framebuffer 尺寸共同影响渲染。

## 验证命令

- `./utility.sh --check-yaml`：加载 Mod、规则、地图和 Fluent lint。
- 引擎/项目单元测试：验证 Touch adapter、debug policy、hang state 等纯逻辑。
- iOS Release build：验证 AOT、链接与资源复制。
- 安装前后对 App bundle 的 `mod.yaml`、目标规则、DLL、shader 做哈希核对。
- 物理设备分别测试三个阵营的基地车选择、移动、展开和重新收起。
