# RA2 触屏命令反馈指针设计

**Status:** 已确认，直接实施
**Date:** 2026-08-26

## 1. 问题与目标

触屏玩家点击地面移动或点击目标攻击后，当前只看到一个低透明度白色圆环或目标闪烁，无法立即区分命令类型。桌面端虽然已有 `move` / `attack` 鼠标光标动画，但 iOS 没有可在点击前悬停的指针，因此不能把持续鼠标光标直接照搬到触屏交互。

本设计在命令发出后，于命令目标点播放一次原作风格的动态确认指针：

- 移动：绿色收缩再展开。
- 攻击和强制攻击：红色攻击符号。
- 两种反馈均播放 400ms 后自动消失。
- 一次多单位命令只显示一个反馈。
- 未映射的命令继续使用现有白色圆环或目标闪烁，不改变旧行为。

## 2. 方案选择

采用 `OrderEffects` 的 order-aware 一次性 `SpriteAnnotation`：

1. `WorldInteractionControllerWidget` 已把本次输入的第一个有效命令、命令字符串和视觉目标交给 `INotifyOrderIssued`。
2. `OrderEffects` 根据命令字符串选择反馈序列，在 `Target.CenterPosition` 创建一次性动画。
3. `SpriteAnnotation` 使用 `Animation.PlayThen`，播完自动从 World 和 ScreenMap 移除。

不采用以下方案：

- **全局强制软件光标。** 它能绕过 iOS SDL 的空硬件光标，但会让菜单和普通触摸持续显示默认鼠标箭头，超出“点击后的命令确认”目标。
- **新增 PNG 指针。** 项目已有玩家导入的正版 `mouse.shp` / `mousepal.pal`，重画会重复资产并增加许可、生成器和打包负担。
- **RA2 专用重复 trait。** 第二个 `INotifyOrderIssued` 会与现有 `OrderEffects` 叠加或重复旧回退；扩展现有通用 trait 更小且保持其他 mod 默认不变。

## 3. 动画与映射

新增普通序列图像 `order-feedback`，只引用玩家已导入的零售素材：

```yaml
order-feedback:
	move:
		Filename: conquer|mouse.shp
		Start: 31
		Length: 10
		Tick: 40
		ZOffset: 2047
	attack:
		Filename: conquer|mouse.shp
		Start: 58
		Length: 5
		Tick: 80
		ZOffset: 2047
```

时序：

- 移动：10 帧 × 40ms = 400ms。
- 攻击：5 帧 × 80ms = 400ms。
- 40ms / 80ms 都是引擎 40ms 动画 tick 的整数倍，不产生交替长短帧。
- 不添加 Offset、Scale、AlphaFade 或 ZRamp；SHP 帧本身已经围绕热点对齐。

命令映射：

| Order string | Sequence |
| --- | --- |
| `Move` | `move` |
| `Attack` | `attack` |
| `ForceAttack` | `attack` |
| `AttackMove` | 现有回退（原作另有专用 `attackmove` cursor，不在本次冒充红色攻击指针） |
| `AssaultMove` | 现有回退 |

目标类型 `Terrain`、`Actor`、`FrozenActor` 均使用一次性动态指针。无效目标或未知命令不选择新序列，继续进入当前 `OrderEffects` 的白环 / 白闪回退。

## 4. 引擎接口与兼容性

`OrderEffectsInfo` 新增三个可选字段：

- `OrderFeedbackImage`
- `OrderFeedbackSequences`（order string → sequence）
- `OrderFeedbackPalette`

只有 `OrderFeedbackImage`、palette 和相应字典项都配置时才启用新反馈。没有配置这些字段的其他 mod 完整保留旧行为。序列字典使用 `SequenceReference` lint，palette 使用 `PaletteReference` lint。

`OrderEffectsInfo` 提供一个纯查询 seam，根据 order string 与 `TargetType` 返回序列或 null，供运行时和 NUnit 共同调用，避免仅靠字符串源码测试。

## 5. 资产与发布边界

- 只在 YAML 中引用 `conquer|mouse.shp` 和现有 `mouse` palette。
- 不复制、提取或提交 `.shp`、`.pal`、`.mix`、临时 PNG / GIF。
- `moveflsh` 白环保留，供未知命令回退。
- 修改后的 `world.yaml` 与 `sequences/misc.yaml` 只更新 public-content manifest 的 size / SHA-256；来源分类与 `public:false` 保持不变。
- Engine 修改必须同时写入下一号 `engine-patches`，并验证从 pinned release 顺序重放后与物化文件一致。

## 6. 验收

自动验收：

- 真实 YAML 解析得到精确映射、图像、palette、帧号、长度、Tick 与 ZOffset。
- 三种有效 target type 均返回对应序列；无效 target 和未知命令返回 null。
- 旧白环配置仍存在。
- YAML lint、缺失 sprite 检查、Common / RA2 Release build、patch replay 与 diff-check 通过。

运行验收：

- 空地移动显示一个绿色 400ms 指针。
- 攻击单位、强制攻击地面显示一个红色 400ms 指针。
- 冻结目标反馈锚定目标点。
- 多选单位一次命令只显示一个反馈。
- 反馈播完自动消失，不残留全局鼠标箭头。
