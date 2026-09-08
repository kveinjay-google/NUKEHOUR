
## ingame-debug.yaml
label-debug-panel-title = 调试选项
checkbox-debug-panel-instant-build = 瞬间建造
checkbox-debug-panel-enable-tech = 全科技解锁
checkbox-debug-panel-build-anywhere = 任意地点建造
checkbox-debug-panel-unlimited-power = 无限电力
checkbox-debug-panel-instant-charge = 瞬间充能
checkbox-debug-panel-disable-visibility-checks = 禁用可见性检查
button-debug-panel-give-cash = 获得 $20,000
button-debug-panel-grow-resources = 资源生长
button-debug-panel-give-exploration = 揭开战争迷雾
button-debug-panel-reset-exploration = 重置战争迷雾
label-debug-panel-visualizations-title = 可视化
checkbox-debug-panel-show-unit-paths = 显示单位路径
checkbox-debug-panel-show-customterrain-overlay = 显示自定义地形
checkbox-debug-panel-show-actor-tags = 显示单位标签
checkbox-debug-panel-show-depth-preview = 显示深度数据
checkbox-debug-panel-show-combatoverlay = 显示战斗几何
checkbox-debug-panel-show-geometry = 显示渲染几何
checkbox-debug-panel-show-terrain-overlay = 显示地形几何
checkbox-debug-panel-show-screenmap = 显示屏幕地图

## ingame-observer.yaml
button-observer-widgets-options = 选项 (Esc)
button-replay-player-pause-tooltip = 暂停
button-replay-player-play-tooltip = 播放

button-replay-player-slow =
    .tooltip = 慢速
    .label = 50%

button-replay-player-regular =
    .tooltip = 常速
    .label = 100%

button-replay-player-fast =
    .tooltip = 快速
    .label = 200%

button-replay-player-maximum =
    .tooltip = 最高速
    .label = MAX

label-basic-stats-player-header = 玩家
label-basic-stats-cash-header = 资金
label-basic-stats-power-header = 电力
label-basic-stats-kills-header = 击杀
label-basic-stats-deaths-header = 损失
label-basic-stats-assets-destroyed-header = 摧毁
label-basic-stats-assets-lost-header = 损失
label-basic-stats-experience-header = 得分
label-basic-stats-actions-min-header = APM
label-economy-stats-player-header = 玩家
label-economy-stats-cash-header = 资金
label-economy-stats-income-header = 收入
label-economy-stats-assets-header = 资产
label-economy-stats-earned-header = 已赚取
label-economy-stats-spent-header = 已花费
label-economy-stats-harvesters-header = 采矿车
label-production-stats-player-header = 玩家
label-production-stats-header = 生产
label-support-powers-player-header = 玩家
label-support-powers-header = 支援技能
label-army-player-header = 玩家
label-army-header = 军队
label-combat-stats-player-header = 玩家
label-combat-stats-assets-destroyed-header = 摧毁
label-combat-stats-assets-lost-header = 损失
label-combat-stats-units-killed-header = 击杀单位
label-combat-stats-units-dead-header = 损失单位
label-combat-stats-buildings-killed-header = 摧毁建筑
label-combat-stats-buildings-dead-header = 损失建筑
label-combat-stats-army-value-header = 军力价值
label-combat-stats-vision-header = 视野

## ingame-player.yaml
supportpowers-support-powers-palette =
    .ready = 就绪
    .hold = 暂停

button-command-bar-production-x5 =
    .short = ×5
    .tooltip = 五倍建造
    .tooltipdesc = 切换每次建造五个。开启后，每次点击会加入五个单位或普通建筑；唯一建筑仍遵守建造上限。

button-command-bar-attack-move =
    .label = 攻移
    .tooltip = 攻击移动
    .tooltipdesc = 选中单位将移动至目标地点，
    并攻击途中遭遇的所有敌人。

    瞄准时按住 <(Ctrl)> 可下达强攻移动命令，
    攻击途中遭遇的所有单位与建筑。

    左键点击图标，然后右键点击目标地点。

button-command-bar-force-move =
    .label = 强移
    .tooltip = 强制移动
    .tooltipdesc = 选中单位将移动至目标地点
     - 抑制目标的默认行为
     - 载具将尝试碾压目标地点的敌人

    左键点击图标，然后右键点击目标。
    指挥单位时按住 <(Alt)> 可临时启用。

button-command-bar-force-attack =
    .label = 强攻
    .tooltip = 强制攻击
    .tooltipdesc = 选中单位将攻击指定的单位或地点，
    忽略其对该目标的默认行为。

    左键点击图标，然后右键点击目标。
    指挥单位时按住 <(Ctrl)> 可临时启用。

button-command-bar-guard =
    .label = 警戒
    .tooltip = 警戒
    .tooltipdesc = 选中单位将跟随目标单位。

    左键点击图标，然后右键点击目标单位。

button-command-bar-deploy =
    .label = 部署
    .tooltip = 部署
    .tooltipdesc = 选中单位将执行其默认的部署行为
     - 基地车将展开为建造厂
     - 建造厂将重新收起为基地车
     - 运输单位将卸下乘客
     - 自爆卡车与 MAD 坦克将自爆
     - 飞机将返回基地

    对选中单位立即生效。

button-command-bar-scatter =
    .label = 散开
    .tooltip = 散开
    .tooltipdesc = 选中单位将停止当前行动
    并移动至附近位置。

    对选中单位立即生效。

button-command-bar-stop =
    .label = 停止
    .tooltip = 停止
    .tooltipdesc = 选中单位将停止当前行动。
    选中建筑将重置集结点。

    对选中目标立即生效。

button-ios-viewport-action-stop =
    .label = 停止
button-ios-viewport-action-deploy =
    .label = 部署
button-ios-viewport-action-select-type =
    .label = 同类
button-ios-viewport-action-force-attack =
    .label = 强攻
button-ios-viewport-action-return-base =
    .label = 基地

button-command-bar-queue-orders =
    .label = 路点
    .tooltip = 路径点模式
    .tooltipdesc = 使用路径点模式为选中单位
    下达多个连续命令，单位将按顺序
    依次执行这些命令。

    左键点击图标，然后在游戏世界中下达命令。
    指挥单位时按住 <(Shift)> 可临时启用。

button-stance-bar-attackanything =
    .label = 主动
    .tooltip = 主动攻击状态
    .tooltipdesc = 将选中单位设为主动攻击状态：
     - 单位会主动攻击视野内的敌方单位与建筑
     - 单位会在战场上追击攻击者

button-stance-bar-defend =
    .label = 防御
    .tooltip = 防御状态
    .tooltipdesc = 将选中单位设为防御状态：
     - 单位会主动攻击视野内的敌方单位
     - 单位不会移动或追击敌人

button-stance-bar-returnfire =
    .label = 还击
    .tooltip = 还击状态
    .tooltipdesc = 将选中单位设为还击状态：
     - 单位只会反击攻击自己的敌人
     - 单位不会移动或追击敌人

button-stance-bar-holdfire =
    .label = 停火
    .tooltip = 停火状态
    .tooltipdesc = 将选中单位设为停火状态：
     - 单位不会向敌人开火
     - 单位不会移动或追击敌人

button-top-buttons-fps-tooltip = 显示/隐藏帧数
button-top-buttons-debug-tooltip = 调试菜单
button-top-buttons-floating-controls-tooltip = 显示/隐藏触控组件
button-top-buttons-options-tooltip = 选项
button-top-buttons-repair-tooltip = 手动维修（点击建筑）
button-top-buttons-auto-repair-tooltip = 自动修理
button-top-buttons-power-tooltip = 自动修理
button-top-buttons-beacon-tooltip = 放置信标
button-top-buttons-sell-tooltip = 出售
labelwithtooltip-player-widgets-cash = <0>
labelwithtooltip-player-widgets-power = <0>

productionpalette-sidebar-production-palette =
    .ready = 就绪
    .hold = 暂停

button-production-types-building-tooltip = 建筑
button-production-types-support-tooltip = 支援
button-production-types-infantry-tooltip = 步兵
button-production-types-vehicle-tooltip = 载具
button-production-types-aircraft-tooltip = 飞机
button-production-types-naval-tooltip = 海军
button-production-types-scroll-up-tooltip = 向上滚动
button-production-types-scroll-down-tooltip = 向下滚动
dropdownbutton-hpf-overlay-locomotor = 选择移动方式
dropdownbutton-hpf-overlay-check = 选择阻挡类型

## mainmenu-prerelease-notification.yaml
label-mainmenu-prerelease-notification-prompt-title = NUKE HOUR 开发者预览版
label-mainmenu-prerelease-notification-prompt-text-a = 这是 NUKE HOUR 的 pre-alpha 预览构建，
label-mainmenu-prerelease-notification-prompt-text-b = 供社区关注开发进度，也作为模组开发者的示例。
label-mainmenu-prerelease-notification-prompt-text-c = 许多功能缺失或不完整，性能尚未优化，
label-mainmenu-prerelease-notification-prompt-text-d = 平衡性将在未来的 beta 版本中处理。
button-mainmenu-prerelease-notification-continue = 我知道了

## mainmenu.yaml
label-menu-subtitle = NUKE HOUR


## settings.yaml tabs
button-settings-title = 游戏设置
button-settings-tab-display = 显示
button-settings-tab-audio = 音频
button-settings-tab-input = 输入
button-settings-tab-touch = 触控操作
button-settings-tab-hotkeys = 热键
button-settings-tab-advanced = 高级
## settings-advanced.yaml
label-forum-account-section-header = 论坛账号

## ingame-player.yaml selection bar
button-selection-bar-group-01 =
    .label = 1
button-selection-bar-group-02 =
    .label = 2
button-selection-bar-group-03 =
    .label = 3
button-selection-bar-group-04 =
    .label = 4
button-selection-bar-group-05 =
    .label = 5
button-selection-bar-group-06 =
    .label = 6
button-selection-bar-group-07 =
    .label = 7
button-selection-bar-group-08 =
    .label = 8
button-selection-bar-group-09 =
    .label = 9
button-selection-bar-group-10 =
    .label = 0
button-selection-bar-group =
    .tooltip = 快捷编组
    .tooltipdesc = 左键点击选择该编组。
    双击或 Alt+点击 将视角跳转到编组。

    Ctrl+点击：将当前选中单位编入该组。
    Shift+点击：将该组并入当前选择。
    Ctrl+Shift+点击：把当前选择加入该组。
button-selection-bar-select-all =
    .label = 展开
    .tooltip = 展开选择全部作战单位
    .tooltipdesc = 第一次点击选择屏幕内的作战单位。
    再次点击将选择范围扩展到整张地图。
button-selection-bar-select-by-type =
    .label = 同类
    .tooltip = 选择同类型单位
    .tooltipdesc = 将选择扩展为与当前选中单位相同类型的其他单位。
    第一次点击覆盖屏幕范围，再次点击覆盖全图。

button-selection-bar-cycle-base =
    .label = 基地
    .tooltip = 跳转至基地
    .tooltipdesc = 循环选择并跳转到己方基地建筑。
button-selection-bar-to-selection =
    .label = 选中
    .tooltip = 跳转至所选目标
    .tooltipdesc = 将视角移动到当前选中的单位或建筑。
button-selection-bar-to-last-event =
    .label = 事件
    .tooltip = 跳转至最近的雷达事件
    .tooltipdesc = 将视角移动到最近一次雷达提示位置。
button-selection-bar-cycle-harvesters =
    .label = 矿车
    .tooltip = 循环选择采矿车
    .tooltipdesc = 循环选择并跳转到己方采矿车。
button-selection-bar-remove-from-group =
    .label = 移出
    .tooltip = 移出编队
    .tooltipdesc = 将当前选中单位从所属编组中移除。
button-selection-bar-sell =
    .label = 出售
    .tooltip = 出售模式
    .tooltipdesc = 进入出售模式，左键点击建筑以出售。
button-selection-bar-repair =
    .label = 维修
    .tooltip = 维修模式
    .tooltipdesc = 进入维修模式，左键点击建筑以维修。
button-selection-bar-beacon =
    .label = 信标
    .tooltip = 放置信标
    .tooltipdesc = 在地图上放置信标标记。

button-command-bar-edit =
    .label = 编辑
    .tooltip = 编辑快捷栏
    .tooltipdesc = 进入编辑模式：拖拽调整顺序，点击或右键显示/隐藏指令。
    再次点击保存并退出。
button-command-bar-collapse =
    .label = ▾
    .tooltip = 收起快捷栏
    .tooltipdesc = 收起为原版风格的紧凑小条。
button-command-bar-expand =
    .label = ▴
    .tooltip = 展开快捷栏
    .tooltipdesc = 展开为带文字标签的完整快捷栏。
label-command-bar-edit-hint = 拖拽排序 · 点击切换显示 · 右键也可隐藏/显示
