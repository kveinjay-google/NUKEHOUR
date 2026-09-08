# iOS 三键四分之一圆弧与快捷栏迁移设计

**日期：** 2026-08-26

**状态：** 已批准（A1）

**适用范围：** iPhone 与 iPad 横屏触控布局；桌面布局不变

## 目标

把左下角虚拟摇杆重新贴近安全区左下角，只在摇杆的 12 点到 3 点方向保留三个高频目标命令：部署、同类、强攻。将停止与返回基地迁移到屏幕底部快捷命令栏，并保证快捷栏收起和展开时都能直接使用。

本设计必须同时解决当前五键环造成的两个问题：

1. 左上方两个按钮扩大了控制簇负向 X 范围，使摇杆被迫向画面中心移动约 76pt。
2. 布局只接收快捷栏顶部的单一 Y 值，把居中的快捷栏误当成全宽障碍，使摇杆不必要地整体上移。

## 用户已批准的产品选择

- 采用 A 布局：单半径、等角度的四分之一圆弧。
- 左下浮动按钮只保留 `DEPLOY`、`SELECT_TYPE`、`FORCE_ATTACK`。
- `STOP` 与“基地”移入快捷命令栏。
- 采用 A1：`STOP` 与 `CYCLE_BASE` 在触控快捷栏收起和展开时都可见。
- 三个浮动按钮继续显示常驻文字标签：部署、同类、强攻。
- 不缩小 56pt 命中框，不删除三阵营现有图集中的停止与基地素材。

## 左下控制簇几何

### 角度与顺序

以摇杆中心为圆心，以屏幕向右为 0°、向上为 -90°：

| 顺序 | 命令 | 角度 |
| --- | --- | --- |
| 1 | `DEPLOY` | -90°（12 点） |
| 2 | `SELECT_TYPE` | -45°（1:30） |
| 3 | `FORCE_ATTACK` | 0°（3 点） |

三个按钮必须位于同一半径，不使用双半径或折线布局。

### 尺寸与半径

- 按钮命中框：56×56pt。
- 摇杆尺寸继续支持 112、128、144pt 三档。
- 摇杆初始触摸命中区与可见底座一致，使用内切圆；取得触摸焦点后继续允许拇指拖出圆外。这样既保留紧凑半径，也不会让 45° 按钮与摇杆方形外接框争抢输入。
- 圆弧半径：`joystickDiameter / 2 + 48pt`。
- 因此三档半径分别为 104、112、120pt。
- 该公式在最大 144pt 摇杆下仍保证按钮与摇杆至少约 8pt 间距，并保证相邻按钮远大于 8pt 间距。

### 安全区锚定

在没有真实障碍碰撞时：

- 摇杆左边缘 = `SafeBounds.Left + 12pt`。
- 摇杆底边缘 = `SafeBounds.Bottom - 12pt`。
- 刘海位于左侧或右侧时均使用实时 Safe Area 重新计算。

### 快捷栏避让

布局必须接收快捷栏的真实二维矩形，而不是单一 `bottomObstacleTop`。

1. 将快捷栏真实矩形向四边外扩 8pt。
2. 分别检查 `JoystickBounds` 和三个 `ActionBounds`；禁止使用它们的整体 Union 代表实体占用，因为扇区内部空白不是障碍。
3. 先判每个实体矩形的 X 区间是否与扩展快捷栏重叠，再只对真正二维相交的矩形求位移。
4. 避让位移为 `max(entity.Bottom - expandedBar.Top)`，并把摇杆和三个按钮整体向上移动该最小值。
5. 不允许因快捷栏位于屏幕中部而把左下控制簇当成与全宽障碍相交。
6. 避让后所有命中框仍必须位于 Safe Area 加 12pt 边距内。

## 浮动动作迁移

### 删除的运行时控件

从 `IOS_VIEWPORT_ACTIONS` 的活动控件树中删除：

- `Button@STOP`
- `Button@RETURN_BASE`

`IosViewportActionsLogic` 的动作数组必须严格为：

```text
DEPLOY, SELECT_TYPE, FORCE_ATTACK
```

移除浮动 `RETURN_BASE` 的专用绑定、禁用谓词和重复选择实现。现有三阵营 actions atlas 仍保留五个图形区域，避免破坏素材合同和兼容性；运行时活动控件合同与完整图集合同必须在审计中分开验证。

### 快捷栏按钮复用

不新增按钮、图标或命令类型，复用现有：

- `STOP`：继续由 `CommandBarLogic` 发出 Stop 命令。
- `CYCLE_BASE`：继续由 `SelectionCommandBarLogic` 选择基地并居中视口。

触控布局中：

- 从 `TouchSuppressedIds` 移除 `STOP` 与 `CYCLE_BASE`。
- `DEPLOY`、`SELECT_BY_TYPE`、`FORCE_ATTACK` 仍保持 suppression，防止与左下三个浮动按钮重复。
- `STOP` 与 `CYCLE_BASE` 加入触控 compact 顺序，并紧邻排列在既有 compact 命令之后、展开切换按钮之前。
- 两者在 compact 与 expanded 状态都是触控布局必备项，不允许编辑模式把唯一触控入口永久移除。
- 编辑模式点击或把这两个槽位拖到隐藏行时必须当场拒绝删除；不能依赖重启后的 Normalize 才恢复。
- 两个按钮在 compact 与 expanded 状态的实际触控矩形均不得小于 44×44 物理 pt，并且必须完全位于实时 Safe Area。
- compact 状态继续显示短标签：`STOP` 使用 Stop/停止，`CYCLE_BASE` 使用 Base/基地；图标在上、标签在下，不能退化成无文字图标。
- 桌面可用集合、默认可见集合、compact 顺序与编辑行为完全不改变。

### 基地命令行为

快捷栏 `CYCLE_BASE` 必须保留原浮动“基地”按钮的用户可见行为：

1. 每次无状态地按 Primary Base 优先、其余 ActorID 的稳定顺序生成候选列表。
2. 当前选中某个候选时取下一个；未选中候选时取列表首项；末尾回绕首项。
3. 没有 `BaseBuilding` 时回退到第一个可选的己方建筑。
4. 成功选择后居中视口并播放现有点击反馈。

不创建第二个 `RETURN_BASE` 快捷栏槽位。

## 偏好迁移

快捷栏布局版本从 3 升至 4。

- 旧触控配置（版本小于 4）升级时，各追加一次 `STOP`、`CYCLE_BASE`，不得重复。
- 新安装的触控默认配置必须直接包含两者；不能依赖一个不会运行的旧版本迁移分支。
- A1 将两者定义为触控必备入口，因此加载 v4 自定义配置时也要补齐缺失项。
- 桌面配置不补齐这两个触控必备项，也不改变 `CYCLE_BASE` 的桌面默认可见性。
- 保存后的版本必须为 4。

## 响应式设备矩阵

自动几何测试至少覆盖：

- iPhone 844×390 native points：renderer 1558×720，Safe Area 47/0/47/21pt。
- iPhone 932×430 native points：renderer 1560×720，Safe Area 59/0/59/21pt。
- iPad mini：renderer/native 1133×744，Safe Area 0/0/0/20pt。
- iPad Air：renderer/native 1180×820，Safe Area 0/0/0/20pt。
- iPad Pro：renderer/native 1366×1024，Safe Area 0/0/0/20pt。
- iPhone 另加一组左右不对称 Safe Area 交换用例，证明刘海换边会重算锚点；对称 47/47、59/59 仅作为真实设备基线，不冒充不同几何用例。
- 每台设备的 112、128、144pt 摇杆档位。
- 快捷栏 compact、expanded、隐藏三种状态。

每个组合必须断言：

- 三个按钮顺序、角度误差不超过 1°。
- 三个按钮同半径，半径符合 `D/2+48pt`。
- 摇杆可见/圆形命中区和所有按钮命中框位于安全区内。
- 按钮之间、按钮方形命中框与摇杆圆形命中区之间至少 8pt。
- 未碰撞时摇杆严格保持 12pt 左/下锚点。
- 碰撞时只发生最小向上位移。
- 控制簇不与外扩 8pt 的真实快捷栏矩形相交。

## 测试与审计合同

### C# 行为测试

- 纯几何设备矩阵和真实矩形避障。
- 摇杆初始触摸只接受圆形底座内的点，获得焦点后的拖动不受该圆限制。
- 活动动作数严格为 3，顺序严格为 Deploy / Select Type / Force Attack。
- 三个运行时 Label 继续应用响应式字体与 bounds。
- 触控可用集合包含 STOP、CYCLE_BASE，仍排除三个浮动重复项。
- compact 和 expanded 都包含 STOP、CYCLE_BASE。
- 两个必备项在编辑模式点击、右键或拖出活动行后仍当场保留。
- 两个必备项在两种快捷栏状态均具有至少 44×44 物理 pt 命中框、完整 Safe Area containment 和可读短标签。
- v1/v2/v3/v4、新安装、重复项、桌面配置迁移矩阵。
- `CYCLE_BASE` 主基地优先、稳定循环、无基地回退。

### YAML、素材与 bundle 审计

- `ingame.yaml` 的活动浮动按钮严格为三项。
- 三阵营 action atlas 仍保留五个素材区域及原有 1x/2x 像素合同。
- bundle 审计分别维护“图集完整五项”和“运行时活动三项”两个常量，不能因为删除控件而停止检查未使用的兼容素材。
- canonical source 与 `.app` 内 `ingame.yaml`、managed assemblies、touch assets 必须一致。
- 更新 `packaging/public-content-manifest.json` 中受影响 tracked 内容的 SHA-256 和 size；不新增零售资源。

### 引擎可重放交付

所有 `engine/` 变更必须：

- 精确 force-add 已批准的物化引擎文件。
- 先以独立 `0013-post-0012-overlay-sync.patch` 捕获当前已提交、但 0001–0012 尚未重放的既有 engine overlay 漂移；该补丁不得混入 A1 新行为。
- 再生成 `0014-ios-three-action-quarter-ring.patch`，只包含 A1 新行为及其测试。
- 最后以独立 `0015-ios-lobby-scroll-layout-polish.patch` 捕获与 A1 六文件不相交的既有 lobby、地图预览、聊天与滚动布局 engine 补集；外层 mod 的 `mods/ra2/chrome.yaml` 不得进入 engine patch。
- 更新 `engine-patches/README.md`。
- 从官方 `release-20250330` ZIP 应用 0001–0015 后逐字节复现全部 tracked 物化引擎目标。

## 真机验收

在自动测试和 Release 签名构建通过后，覆盖安装到已连接的真机，不先卸载，保持：

- Bundle ID `com.openra.ipad.personal`
- Team/Application Identifier `267KQ4A3N4`
- `Documents/OpenRA` 和现有设置/存档不被删除

至少完成：

1. `codesign --verify --deep --strict`。
2. bundle source/hash/managed audit。
3. 覆盖安装与启动成功 JSON。
4. 进程持续存活检查。
5. 新运行期无新增 exception 日志。
6. 真机目视检查左下摇杆、三键圆弧、compact/expanded 快捷栏中的停止与基地。

## 非目标

- 不改变桌面鼠标/快捷栏布局。
- 不重新绘制三阵营触控图集。
- 不删除 atlas 中停止、基地素材。
- 不改变 Deploy、Select Type、Force Attack 的命令语义。
- 不在本任务中重新设计右侧 HUD、生产栏或支持能力栏。
