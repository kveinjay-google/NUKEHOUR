# iOS 左下战斗按钮常显文字设计

## 目标

让玩家无需试按即可理解左下角五个战斗按钮，同时保持已经确认的 56pt 触控面积、圆弧排列、安全区避让和三阵营皮肤。

## 已确认方案

采用方案 A：在每个按钮内部下缘常显短标签。五个按钮继续按照 `STOP / DEPLOY / SELECT_TYPE / FORCE_ATTACK / RETURN_BASE` 排列，使用专门为 56pt iOS 按钮设计的本地化键：

- `button-ios-viewport-action-stop.label`
- `button-ios-viewport-action-deploy.label`
- `button-ios-viewport-action-select-type.label`
- `button-ios-viewport-action-force-attack.label`
- `button-ios-viewport-action-return-base.label`

英文显示为“Stop / Deploy / Type / Force / Base”，中文显示为“停止 / 部署 / 同类 / 强攻 / 基地”。专用短标签避免既有 `Same Type / Force Atk` 在 56pt 按钮内溢出。最后一个必须写“基地”，因为实际行为是选择、轮换并定位基地，而不是命令单位返回基地。

## 视觉与布局

- 每个按钮仍为 56 个物理点，标签不能扩大按钮、动作容器或命中范围。
- 图标外壳保持原尺寸；生成器把语义图形上移，在按钮下缘预留稳定的深色标签区。
- 标签区高度为 14 个物理点，左右及下缘各保留 6 个物理点内边距，使底签完整收在按钮外框内。
- iPhone 紧凑横屏使用 `IosTouchLabel`（20 个逻辑像素）的粗体标签；iPad 使用现有 10pt `TinyBold`。五个中英文短标签必须在两类字体下完整落入标签区。
- 文字使用近白色、高对比描边，并保持稳定；按下、激活和禁用状态仍由整个阵营按钮图形表达。
- 深色标签区由阵营图集直接绘制；运行时只叠加 `LabelWidget`，并忽略鼠标悬停，事件始终落到原按钮。不得使用会取得鼠标焦点的 `ColorBlockWidget` 作为标签区。

## 响应式与本地化

运行时根据 `IosScreenSnapshot` 把物理点转换为逻辑像素，并在每次既有动作布局刷新时同步更新标签位置和字体。标签文本只引用 Fluent，不烘焙进三阵营 PNG。

## 非目标

- 不改变按钮命令语义、顺序、圆弧角度、触控直径或摇杆布局。
- 不新增长按行为或独立标签触点。
- 本轮不实现首次教学层；常显标签本身应独立解决可发现性。

## 验收

- iPhone 844×390、932×430 与 iPad 1133×744、1180×820、1366×1024 均保持原动作按钮安全区和互不重叠门禁。
- 标签区完整处于每个按钮内，物理尺寸在所有设备一致。
- 五个按钮分别引用正确 Fluent 标签；中英文键均存在。
- 三阵营、七种按钮状态的生成图集均保留清晰、稳定的空标签区，生成两次字节一致。
- 动作按钮子控件顺序严格为 `Image@ICON` 后接 `Label@LABEL`；标签不能遮断按钮 hover/press/click。
- focused NUnit、触控资产 Python 测试、RA2 YAML 检查和相关 Release build 通过。
