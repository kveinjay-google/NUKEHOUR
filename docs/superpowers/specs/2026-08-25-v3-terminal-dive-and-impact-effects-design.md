# V3 末端俯冲与 V3 / 基洛夫爆炸效果设计

**Status:** 浏览器视觉方案 B 已确认；等待书面规格确认
**Date:** 2026-08-25
**Visual direction:** 抬升 → 巡航 → 末端锁定俯冲；白热核心 → 主火球 → 冲击环 → 残烟 / 水花

## 1. 问题与决定

V3 当前配置启用 `LazyCurve: true`，整段飞行由一条二次曲线插值完成。它没有独立的抬升、巡航和锁定阶段，所以火箭看起来像横向漂浮，最后再突然落下。普通 V3 与普通基洛夫炸弹又共同使用单个较小的 `medium_clsn` 动画，没有局部核心、冲击环、残烟或震屏；纯视觉 warhead 还会受默认 `ImpactActors: true` 过滤。

本设计采用浏览器对比中的方案 B：

1. V3 使用专属、可配置的终端俯冲模式；不改变无畏舰和尤里潜艇导弹的旧轨迹。
2. V3 在发射时冻结目标坐标，先抬升并朝目标巡航，进入终端范围后先对准，再加速俯冲。
3. V3 和基洛夫的伤害、扩散、护甲倍率与射速保持不变；只重做视觉 warhead。
4. 爆炸每个落点最多同时存在四个 sprite 层，并使用轻量震屏，避免 iPad 上的过绘与帧时尖峰。
5. 发布包只携带 clean-room 程序生成的原创叠层 PNG；正版 RA2 主火球、声音和水花仍由玩家导入，不复制进 App、IPA 或安装包。

## 2. 范围

### 包含

- V3 普通与精英火箭的飞行轨迹。
- V3 普通 / 精英在地面、地面单位、水面和水面单位上的命中视觉。
- 基洛夫普通 / 精英炸弹在上述四类落点上的命中视觉。
- 原创局部核心、冲击环和烟尘 / 碎屑图集的可重复生成、许可来源和公开包审计。
- 轨迹确定性、规则解析、新增资产公开门禁以及桌面运行烟测。

### 不包含

- 不让 V3 追踪发射后继续移动的单位。
- 不改变 V3、基洛夫或无畏舰的伤害平衡、射程、射速和目标筛选。
- 不重写通用 projectile 框架，不改变 `GravityBomb` 的下落物理。
- 不加入核爆式全屏闪光、触觉反馈或第五个独立粒子层。
- 不把浏览器 Canvas 动画直接当游戏资产或运行时验收证据。
- 本设计阶段不安装设备；实机安装与验收必须另行明确执行。

## 3. V3 轨迹架构

### 3.1 兼容性边界

在 `BallisticMissileInfo` 新增三个可选字段：

```yaml
TerminalDive: false
TerminalTurnSpeed: 0
MaxHitSpeed: 0
```

- `TerminalDive: false` 完整保留旧路径。
- `TerminalTurnSpeed: 0` 使用有效值 `TurnSpeed`；只有该有效值也为 0 时才拒绝终端俯冲配置。
- `MaxHitSpeed: 0` 仅对旧路径保持原来的无上限语义。
- V3 是第一项、也是本轮唯一启用 `TerminalDive` 的 actor。
- `bmisl` 和 `dmisl` 不写新字段；其逐 tick 状态、轨迹和命中规则不得变化。

终端俯冲的计算提取为不依赖 World、Renderer 或随机数的纯策略。运行时 Activity 只负责把策略结果应用到 `BallisticMissile`，以便精确执行测试。

### 3.2 V3 参数

V3 使用以下解析值：

```yaml
BallisticMissile:
    CreateAngle: 64
    PrepareTick: 25
    LaunchAngle: 160
    Speed: 250
    BeginCruiseAltitude: 3c0
    BeginHitRange: 4c0
    TurnSpeed: 25
    TerminalDive: true
    TerminalTurnSpeed: 48
    HitAcceleration: 20
    MaxHitSpeed: 350
```

删除 V3 的 `LazyCurve: true`，使用默认 `false`。

这些值表达的视觉节奏是：

- 普通调姿每 tick 最多约 8.79°。
- 末端锁定每 tick 最多约 16.88°。
- 巡航速度为 250 WDist/tick；末端加速但封顶 350。
- 终端每 tick 的真实位移不超过 350 WDist；最后一步按剩余距离缩短并精确落点。
- 5c 射程允许抬升后直接进入终端阶段；10c 和 18c 显示完整或接近完整的巡航阶段。

### 3.3 确定性状态机

状态只允许按以下方向推进：

```text
Prepare → Launch → Cruise → Acquire → Hit → Done
                   └────────→ Acquire
```

#### Prepare

- 终端俯冲模式按总进度计算 Pitch，而不是重复累加截断后的整数增量。
- 第 25 个准备 tick 必须精确得到 `LaunchAngle: 160`；不能停在当前算法产生的 139。
- `PrepareTick <= 0` 在规则加载时拒绝。
- 旧模式继续使用旧准备行为，避免暗改其他导弹。

#### Launch

- 先计算到目标的水平剩余距离。
- 若距离已经小于或等于 `BeginHitRange`，直接进入 `Acquire`，不再强求达到 3c 巡航高度。
- 否则按发射角移动；达到 `BeginCruiseAltitude` 后进入 `Cruise`。
- 上述检查和新排序仅在 `TerminalDive` 模式启用。

#### Cruise

- 每 tick 的顺序固定为：检查终端距离 → 调整 Pitch / Yaw → 移动。
- Pitch 用 `TurnSpeed` 向水平角靠拢；Yaw 沿最短角方向朝冻结目标靠拢。
- 终端距离成立时本 tick 不再沿旧方向前进一步，直接进入 `Acquire`。

#### Acquire

- 这是明确可见的“末端锁定”阶段。
- 火箭保持当前位置，仅用 `TerminalTurnSpeed` 沿最短角方向把 Pitch / Yaw 对准 `targetPos - currentPos`。
- Pitch 与 Yaw 都精确对准后进入 `Hit`；锁定期间不加速，因此不会一边背向目标移动一边转弯。
- 对合法 WAngle，锁定 tick 数必须有上界；以 48 的转速最多 11 tick，V3 的 5c / 10c / 18c 基准不得超过 8 tick。

#### Hit

- 目标始终是发射时保存的 `targetPos`。
- 每 tick 先将速度增加 `HitAcceleration`，并钳制到 `MaxHitSpeed`。
- Hit 的运动方向始终是当前点到冻结目标的单位方向；Pitch / Yaw 与该方向完全一致，不再沿旧朝向积分。
- 本 tick 位移长度为 `min(speed, remainingDistance)`。当剩余距离小于或等于速度时，最后一步精确落到 `targetPos`，真实位移仍不超过 `MaxHitSpeed`。
- 候选位置越过目标法平面或比当前点更远都属于策略错误，测试必须失败；运行时保护不得从更远位置直接瞬移到目标。
- 向量缩放、距离和目标法平面判断只使用整数 / long 运算，不使用浮点累计。
- 最终必须先 `SetPosition(targetPos)`，让 `airborne` 条件撤销，再触发 Kill。这样普通 / 精英分别稳定选择 `V3Weapon` / `V3WeaponE`，不会误走空中 `UnitExplode`。

### 3.4 配置校验

当 `TerminalDive: true` 时：

- `PrepareTick > 0`
- 有效终端转速（`TerminalTurnSpeed != 0 ? TerminalTurnSpeed : TurnSpeed`）大于 0
- `MaxHitSpeed > 0`
- 所有轨迹计算只使用 `int`、`long`、`WAngle`、`WPos` 和 `WVec`，不得引入随机数或浮点累计。

## 4. 爆炸效果架构

### 4.1 为什么必须拆 warhead

`CreateEffect` 中的 `Explosions: a, b, c` 会随机选一个序列，并不会把三者叠加。每个视觉层必须有独立的 warhead ID，并通过 `Delay` 错开。

所有纯视觉 `CreateEffect` 统一使用：

```yaml
ImpactActors: false
ForceDisplayAtGroundLevel: true
```

这使效果由落点地形决定，不会被重叠但无效的 actor 静默过滤，也不会悬浮在 actor 中心高度。

### 4.2 四层上限与时序

地面命中最多四层：

1. `t=0`：原创局部白热核心。
2. `t=0`：玩家导入的主火球。
3. `Delay=1`：原创冲击环。
4. `Delay=3`：原创烟尘与碎屑合成层。

水面命中最多四层：

1. `t=0`：原创局部核心。
2. `t=0`：玩家导入的主火球。
3. `Delay=1`：原创冲击环。
4. `Delay=1`：玩家导入的水花。

每个地形只播放一次声音：陆地声音绑定主火球，水面声音绑定水花。震屏不计入 sprite 层。

### 4.3 效果矩阵

| 武器 | 主火球（玩家正版导入） | 原创叠层规格 | 水花 | 震屏 Duration / Intensity |
|---|---|---|---|---|
| V3 普通 | `large_clsn` | small core / ring / debris | `large_watersplash` | 5 / 1 |
| V3 精英 | `terrorist_explosion` | large core / ring / debris | `huge_watersplash` | 8 / 2 |
| 基洛夫普通 | `verylarge_clsn` | large core / ring / debris | `huge_watersplash` | 6 / 2 |
| 基洛夫精英 | `kirovtesla` | tesla core / ring + large debris | `huge_watersplash` | 8 / 3 |

震屏使用现有 `ScreenShaker` 的距离衰减，强度明显低于当前核爆的 20 / 5。普通爆炸不使用 `FlashEffectWarhead`，因为当前只有无空间衰减的 Nuke 全屏闪光。

### 4.4 YAML 同构

四种武器使用相同的 warhead ID 结构，普通 / 精英通过同名 ID 覆盖序列和震屏参数：

```yaml
Warhead@20Core: CreateEffect
    Image: nc-impact
    Explosions: nc_core_small
    ImpactActors: false
    ForceDisplayAtGroundLevel: true
    ValidTargets: Ground, Water, Air
Warhead@21MainLand: CreateEffect
    Explosions: large_clsn
    ImpactSounds: gexp14a.wav
    ImpactActors: false
    ForceDisplayAtGroundLevel: true
    ValidTargets: Ground, Air
Warhead@22MainWater: CreateEffect
    Explosions: large_clsn
    ImpactActors: false
    ForceDisplayAtGroundLevel: true
    ValidTargets: Water
Warhead@23Ring: CreateEffect
    Delay: 1
    Image: nc-impact
    Explosions: nc_ring_small
    ImpactActors: false
    ForceDisplayAtGroundLevel: true
    ValidTargets: Ground, Water, Air
Warhead@24PlumeLand: CreateEffect
    Delay: 3
    Image: nc-impact
    Explosions: nc_debris_small
    ImpactActors: false
    ForceDisplayAtGroundLevel: true
    ValidTargets: Ground, Air
Warhead@25SplashWater: CreateEffect
    Delay: 1
    Explosions: large_watersplash
    ImpactSounds: gexpwasa.wav
    ImpactActors: false
    ForceDisplayAtGroundLevel: true
    ValidTargets: Water
Warhead@26Shake: ShakeScreen
    Duration: 5
    Intensity: 1
    Multiplier: 1,1
```

`Warhead@1Dam` 及其 `Damage`、`Spread`、`Falloff`、`Versus`、`DamageTypes` 不变。

### 4.5 无畏舰继承隔离

`DredWeapon` / `DredWeaponE` 当前继承 V3 武器。若直接向 `V3Weapon` 添加视觉层，无畏舰会意外获得本轮新效果。

实施时提取只包含当前伤害解析值的 `^V3FamilyDamage`：

- `V3Weapon` / `V3WeaponE` 继承伤害模板并添加本设计的新视觉层。
- `DredWeapon` / `DredWeaponE` 继承同一伤害模板，但显式保留当前单层视觉。
- 测试比较变更前后的 resolved damage，确保 V3 与 Dred 的伤害、扩散和护甲倍率逐字段一致。

## 5. 原创资产与发布合规

新增一个确定性生成器，只使用数学圆、程序噪声、粒子和项目自定 RGBA 颜色，不读取任何外部 raster、MIX、SHP、调色板或正版帧：

```text
packaging/generate_projectile_impact_assets.py
mods/ra2/bits/animations/nc-impact-*.png
```

生成资产要求：

- RGBA、2 次幂纹理；帧外 gutter 完全透明。
- PNG 内嵌 `FrameSize` 与 `FrameAmount`，供现有 `PngSheetLoader` 切帧。
- 两次隔离生成逐字节相同。
- 单帧只绘制本层内容；不包含文字、商标、角色或对正版动画的描摹。
- `mods/ra2/sequences/misc.yaml` 在 `nc-impact` image 下声明 small / large / tesla 的 core、ring、debris 序列。
- 生成器、资产和 provenance / 许可说明一并进入审查。

`ios/OpenRA.iOS/OpenRA.iOS.csproj` 的 `PublicClean` ItemGroup 必须增加只匹配这些原创输出的精确白名单，例如：

```xml
<BundleResource Include="../../mods/ra2/bits/animations/nc-impact-*.png">
  <Link>mods/ra2/bits/animations/%(Filename)%(Extension)</Link>
</BundleResource>
```

不得放宽为 `bits/**/*`。DevelopmentRetail 继续由现有 canonical RA2 资源通配符包含这些文件。

`packaging/public-content-manifest.json` 把新生成 PNG 与必要的原创声明登记为：

```json
{
  "category": "project-original",
  "license": "Project original",
  "public": true
}
```

并记录实际 SHA-256。四个会被修改的现有 YAML 条目只更新 size / SHA-256 库存，继续保持 `unknown-or-derivative`、`Unreviewed`、`public:false`，除非另一个独立来源与许可证审查证明可公开；本任务不得为了让审计通过而提升分类。

这意味着当前仓库的完整 `PublicClean` App / IPA 仍会被这些混合来源 YAML 的 `not-public` 门禁阻止。本任务只证明新增 PNG 自身可公开、白名单精确且不携带零售资源；完整公开安装包通过属于现有“纯引擎公开发行”工作的后续阻塞项，不能在本轮宣称完成。

不得把用户导入后的 `.mix`、`.shp`、`.aud` 或其他零售资源复制进公开 App / IPA，也不得放宽 deny-by-default 审计来掩盖未知文件。

## 6. 预期改动边界

生产文件预计限于：

- `OpenRA.Mods.RA2/Traits/BallisticMissile.cs`
- `OpenRA.Mods.RA2/Activities/BallisticMissileFly.cs`
- `mods/ra2/rules/soviet-vehicles.yaml`
- `mods/ra2/weapons/explosions.yaml`
- `mods/ra2/weapons/misc.yaml`
- `mods/ra2/sequences/misc.yaml`
- `packaging/generate_projectile_impact_assets.py`
- `packaging/public-content-manifest.json`
- `ios/OpenRA.iOS/OpenRA.iOS.csproj`（仅新增 `nc-impact-*.png` 的 PublicClean 精确白名单）
- 新生成的 `mods/ra2/bits/animations/nc-impact-*.png`
- 聚焦测试文件与必要的测试项目引用

这些目标文件当前包含用户已有的 staged / unstaged 改动。实施必须做小块补丁并保留所有无关差异，不得 reset、stash 或覆盖整段文件。

## 7. 测试与门禁

### 7.1 轨迹纯测试

使用实际 `SpawnOffset: 0,640,300` 后的起点，对名义射程 5c、10c、18c 分别执行逐 tick 模拟。每组必须满足：

- 160 个飞行 tick 内结束，不含 25 tick 准备期。
- 状态只向前推进；5c 可以跳过 `Cruise`。
- 最终位置严格等于 `targetPos`。
- 目标轴投影不越过目标；进入终端接近后没有目标另一侧样本。
- Acquire 的 Pitch / Yaw 最短角变化不超过 48；Hit 位移不超过 350，且朝向与位移方向一致。
- 同输入执行两次，位置、角度、速度和状态序列逐 tick 完全一致。

几何矩阵还包括：

- Yaw 0、256、512、768 与跨零点 1010 → 10。
- 目标同高、+1c、-1c。
- `TerminalTurnSpeed=0` 时回退到有效 `TurnSpeed`；两者都为 0 时在规则加载阶段失败。
- `PrepareTick=25` 的末帧严格等于 `LaunchAngle=160`。

### 7.2 Activity / World 集成测试

以下断言必须使用真实 Activity / World / Actor 夹具，不能由纯策略或源码字符串测试替代：

- 目标发射后移动或死亡，V3 仍命中发射时冻结坐标。
- 最终 `SetPosition(targetPos)` 后 `airborne=false`，随后才执行 Kill。
- 普通 / 精英分别触发 `V3Weapon` / `V3WeaponE`，而不是 `UnitExplode`。
- `bmisl` / `dmisl` 未启用新模式时逐 tick 状态、位置和朝向基线不变。
- 5c、10c、18c 的实际 actor 均在超时前从 World 正常移除，不留下 orphan missile。

### 7.3 规则与视觉契约

- 比较 V3、V3E、Dred、DredE、BlimpBomb、BlimpBombE 变更前后的伤害解析快照。
- 四种目标武器分别具备 core、main-land、main-water、ring、plume、splash、shake。
- 每个 `Explosions` 只包含一个序列名。
- 所有纯视觉 CreateEffect 都是 `ImpactActors:false`。
- 地面 / 水面各恰好一个声音；普通 / 精英都保留水面层。
- 单落点最多四个 sprite，不能因继承重复叠加。

### 7.4 资产与公开包

- 生成器无外部 raster 输入，AST / source contract 阻止文件读取型素材依赖。
- 两次隔离生成 byte-identical。
- 校验 PNG 尺寸、RGBA、POT、FrameSize / FrameAmount、帧数、非空 alpha 与透明 gutter。
- 源资产与 DevelopmentRetail App 中的原创 PNG 逐字节一致。
- 使用最小 PublicClean fixture 验证精确白名单与 manifest 分类；完整 PublicClean App / IPA 在混合来源 YAML 完成独立许可审查前应保持失败，不能标记为通过。
- 公开内容 manifest 的 category、license、public 和 SHA-256 精确匹配。
- App / IPA 内不存在被导入的 MIX、SHP、AUD、VXL、VQA 等零售文件。

### 7.5 运行烟测

不以浏览器动画代替运行测试。桌面或模拟器固定测试图分别验证四种武器命中：

1. 空地；
2. 地面 actor；
3. 水面；
4. 水面 actor。

截图 / 帧日志断言爆点居中、层序正确、声音一次、地面无水花、水面无尘柱。额外执行多枚基洛夫连续投弹，记录 SpriteEffect 峰值与帧时，确认四层上限没有明显尖峰。Fog / shroud 中 SpriteEffect 隐藏是既有规则，验收场景必须处于可见区域。

建议验证顺序：

1. 聚焦 NUnit；
2. 资产生成器单测；
3. `./utility.sh --check-yaml`；
4. RA2 / Common Release build；
5. 桌面或模拟器视觉烟测；
6. 新原创资产的最小 PublicClean fixture audit；
7. 完整 Public App / IPA audit 作为已知阻塞门禁运行并保存报告，不把预期 `not-public` 误报成通过。

## 8. 验收标准

- 5c、10c、18c 都能稳定完成方案 B；无漂浮式整段抛物线、无越点、无最后 1.5c 瞬移。
- 火箭的模型朝向与当前运动方向一致；进入 Acquire 后可清楚看到对准，随后在 Hit 加速俯冲。
- V3 与基洛夫普通 / 精英在地面和水面均有清晰、分层的命中反馈。
- 命中视觉不会因无效 actor 重叠而静默消失。
- 伤害和平衡字段不变；Dred 与其他弹道导弹不受影响。
- 四层上限通过桌面 / 模拟器的 SpriteEffect 与帧时预算；iPad Mini 连续轰炸实机验收延期，本轮不声明已排除设备性能风险。
- 新增叠层资产本身通过项目原创、哈希和精确 PublicClean 白名单审计，不含任何玩家导入的公司资源。
- 完整公开安装包仍受现有混合来源 YAML 的许可证门禁阻塞；本轮不能通过重新分类绕过。
