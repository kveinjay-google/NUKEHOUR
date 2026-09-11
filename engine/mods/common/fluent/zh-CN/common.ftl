## Buttons
button-cancel = 取消
button-retry = 重试
button-back = 返回
button-continue = 继续
button-quit = 退出

## Server Orders
notification-custom-rules = 此地图包含自定义规则，游戏体验可能有所变化。
notification-map-bots-disabled = 此地图已禁用电脑玩家。
notification-two-humans-required = 此服务器需要至少两名人类玩家才能开始比赛。
notification-unknown-server-command = 未知的服务器命令：{ $command }。
notification-admin-start-game = 只有主机才能开始游戏。
notification-no-start-until-required-slots-full = 必需的位置未满，无法开始游戏。
notification-no-start-without-players = 没有玩家，无法开始游戏。
notification-insufficient-enabled-spawn-points = 启用的出生点不足，无法开始游戏。
notification-malformed-command = { $command } 命令格式错误。
notification-state-unchanged-ready = 标记为就绪后无法更改状态。
notification-invalid-faction-selected = 无效的阵营选择：{ $faction }。
notification-state-unchanged-game-started = 游戏开始后无法更改状态（{ $command }）。
notification-requires-host = 只有主机才能执行此操作。
notification-invalid-bot-slot = 无法向已有玩家的位置添加电脑。
notification-invalid-bot-type = 无效的电脑类型。
notification-admin-change-map = 只有主机才能更换地图。
notification-player-disconnected = { $player } 已断开连接。
notification-team-player-disconnected = { $player }（队伍 { $team }）已断开连接。
notification-observer-disconnected = { $player }（观战者）已断开连接。
notification-unknown-map = 服务器上未找到该地图。
notification-searching-map = 正在资源中心搜索地图……
notification-admin-change-configuration = 只有主机才能更改配置。
notification-changed-map = { $player } 将地图更换为 { $map }。
notification-option-changed = { $player } 将 { $name } 更改为 { $value }。
notification-you-were-kicked = 你已被踢出服务器。
notification-admin-kicked = { $admin } 将 { $player } 踢出了服务器。
notification-kicked = { $player } 已被踢出服务器。
notification-temp-ban = { $admin } 对 { $player } 实施了临时封禁。
notification-admin-transfer-admin = 只有管理员才能转让管理员权限。
notification-empty-slot = 该位置无人。
notification-nick-changed = { $player } 现已更名为 { $name }。
notification-player-dropped = 一名玩家因超时被移除。
notification-connection-problems = { $player } 遇到连接问题。
notification-timeout-dropped = { $player } 因超时已被移除。
notification-timeout-dropped-in =
    { $timeout ->
       *[other] { $player } 将在 { $timeout } 秒后被移除。
    }
notification-error-game-started = 游戏已经开始。
notification-requires-password = 服务器需要密码。
notification-incorrect-password = 密码错误。
notification-incompatible-mod = 服务器正在运行不兼容的模组。
notification-incompatible-version = 服务器正在运行不兼容的版本。
notification-incompatible-protocol = 服务器正在运行不兼容的协议。
notification-engine-mismatch = 你的 NUKE HOUR 版本与此服务器不兼容。
notification-runtime-contract-missing = 此服务器需要更新的 NUKE HOUR 多人联机协议。
notification-runtime-contract-mismatch = 你导入的游戏资源与本局不兼容。
notification-map-mismatch = 所选地图与本局不匹配。
notification-you-were-banned = 你已被服务器封禁。
notification-you-were-temp-banned = 你已被服务器临时封禁。
notification-game-full = 游戏已满员。
notification-new-admin = { $player } 现在是管理员。
notification-option-locked = { $option } 无法更改。
notification-invalid-configuration-command = 无效的配置命令。
notification-admin-option = 只有主机才能设置该选项。
notification-error-number-teams = 无法解析队伍数量：{ $raw }。
notification-admin-kick = 只有主机才能踢出玩家。
notification-kick-self = 主机不能踢出自己。
notification-kick-none = 该位置无人。
notification-no-kick-game-started = 游戏开始后只能踢出观战者和已战败的玩家。
notification-admin-clear-spawn = 只有管理员才能清除出生点。
notification-spawn-occupied = 你不能与其他玩家占用同一出生点。
notification-spawn-locked = 该出生点已锁定给其他玩家位置。
notification-admin-lobby-info = 只有主机才能设置大厅信息。
notification-invalid-lobby-info = 发送了无效的大厅信息。
notification-player-color-terrain = 颜色已调整以减少与地形的相似度。
notification-player-color-player = 颜色已调整以减少与其他玩家的相似度。
notification-invalid-player-color = 无法确定有效的玩家颜色，已随机选择。
notification-invalid-error-code = 解析错误消息失败。
notification-master-server-connected = 主服务器通信已建立。
notification-master-server-error = 主服务器通信失败。
notification-game-offline = 游戏未在线广播。
notification-no-port-forward = 服务器端口无法从互联网访问。
notification-blacklisted-server-name = 服务器名称包含违禁词。
notification-requires-authentication = 服务器要求玩家拥有 OpenRA 论坛账号。
notification-no-permission-to-join = 你没有加入此服务器的权限。
notification-slot-closed = 你的位置已被主机关闭。
notification-map-change-no-slot = 所选地图的玩家位置不足，你已被移出大厅。
notification-map-not-ready = 所选地图仍在下载或校验中。

## LobbySettingsNotification
notification-lobby-option = { $name }：{ $value }。

## ServerOrders, UnitOrders
notification-joined = { $player } 加入了游戏。
notification-lobby-disconnected = { $player } 已离开。

## UnitOrders
notification-game-has-started = 游戏已开始。
notification-game-saved = 游戏已保存。
notification-game-paused = 游戏已被 { $player } 暂停。
notification-game-unpaused = 游戏已被 { $player } 恢复。

## Server
notification-game-started = 游戏开始。

## PlayerMessageTracker
notification-chat-temp-disabled =
    { $remaining ->
       *[other] 聊天已禁用，请在 { $remaining } 秒后重试。
    }

## VoteKickTracker
notification-unable-to-start-a-vote = 无法发起投票。
notification-insufficient-votes-to-kick = 踢出玩家 { $kickee } 的票数不足。
notification-kick-already-voted = 你已经投过票了。
notification-vote-kick-started = 玩家 { $kicker } 发起了踢出 { $kickee } 的投票。
notification-vote-kick-in-progress = { $percentage }% 的玩家已投票踢出 { $kickee }。
notification-vote-kick-ended = 踢出玩家 { $kickee } 的投票未通过。

## ActorEditLogic
label-duplicate-actor-id = 单位 ID 重复
label-actor-id = 输入单位 ID
label-actor-owner = 所属方

## ActorSelectorLogic
label-actor-type = 类型：{ $actorType }

## CommonSelectorLogic
options-common-selector =
    .search-results = 搜索结果
    .all = 全部
    .multiple = 多个
    .none = 无

## SaveMapLogic
label-unpacked-map = 未打包

dialog-save-map-failed =
    .title = 地图保存失败
    .prompt = 详见 debug.log。
    .confirm = 确定

dialog-overwrite-map-failed =
    .title = 警告
    .prompt = 保存将覆盖
    已存在的地图。
    .confirm = 保存

dialog-overwrite-map-outside-edit =
    .title = 警告
    .prompt = 该地图已在编辑器之外被修改，
    保存可能会覆盖已有进度。
    .confirm = 保存

notification-save-current-map = 已保存当前地图。

## GameInfoLogic
menu-game-info =
    .objectives = 目标
    .briefing = 简报
    .options = 选项
    .debug = 调试
    .chat = 聊天

## GameInfoObjectivesLogic, GameInfoStatsLogic
label-mission-in-progress = 进行中
label-mission-accomplished = 已完成
label-mission-failed = 已失败

## GameInfoStatsLogic
label-client-state-disconnected = 离线
label-mute-player = 屏蔽此玩家
label-unmute-player = 取消屏蔽此玩家
button-kick-player = 踢出此玩家
button-vote-kick-player = 投票踢出此玩家

dialog-kick =
    .title = 踢出 { $player }？
    .prompt = 该玩家将无法重新加入游戏。
    .confirm = 踢出

dialog-vote-kick =
    .title = 投票踢出 { $player }？
    .prompt = 该玩家将无法重新加入游戏。
    .prompt-break-bots =
    { $bots ->
        [one] 踢出游戏管理员将同时踢出 1 个电脑。
       *[other] 踢出游戏管理员将同时踢出 { $bots } 个电脑。
    }
    .vote-start = 发起投票
    .vote-for = 赞成
    .vote-against = 反对
    .vote-cancel = 弃权

notification-vote-kick-disabled = 此服务器已禁用投票踢人。

## GameTimerLogic
label-paused = 已暂停
label-max-speed = 最高速度
label-replay-speed = { $percentage }% 速度
label-replay-complete = 已完成 { $percentage }%

## LobbyLogic, InGameChatLogic
label-chat-disabled = 聊天已禁用
label-chat-availability =
    { $seconds ->
       *[other] 聊天将在 { $seconds } 秒后可用……
    }

## LobbyLogic, ServerListLogic
label-bot-player = AI 玩家

## IngameMenuLogic
menu-ingame =
    .leave = 离开
    .abort = 放弃任务
    .restart = 重新开始
    .surrender = 投降
    .load-game = 载入游戏
    .save-game = 保存游戏
    .music = 音乐
    .settings = 设置
    .return-to-map = 返回地图
    .resume = 继续
    .save-map = 保存地图
    .exit-map = 退出地图编辑器

dialog-leave-mission =
    .title = 离开任务
    .prompt = 离开本局并返回菜单？
    .confirm = 离开
    .cancel = 留下

dialog-restart-mission =
    .title = 重新开始
    .prompt = 确定要重新开始吗？
    .confirm = 重新开始
    .cancel = 留下

dialog-surrender =
    .title = 投降
    .prompt = 确定要投降吗？
    .confirm = 投降
    .cancel = 留下

dialog-error-max-player =
    .title = 错误：玩家数量超限
    .prompt = 定义的玩家过多（{ $players }/{ $max }）。
    .confirm = 返回

dialog-exit-map-editor =
    .title = 退出地图编辑器
    .prompt-unsaved = 退出并丢失所有未保存的更改？
    .prompt-deleted = 该地图可能已在编辑器之外被删除
    .confirm-anyway = 仍然退出
    .confirm = 退出

dialog-play-map-warning =
    .title = 警告
    .prompt = 该地图可能已被删除，或存在
    导致无法加载的错误。
    .cancel = 确定

dialog-exit-to-map-editor =
    .title = 离开任务
    .prompt = 离开本局并返回编辑器？
    .confirm = 返回编辑器
    .cancel = 留下

## IngamePowerBarLogic
## IngamePowerCounterLogic
label-power-usage = 电力使用：{ $usage }/{ $capacity }
label-infinite-power = 无限

## IngameSiloBarLogic
## IngameCashCounterLogic
label-silo-usage = 储存容量：{ $usage }/{ $capacity }

## ObserverShroudSelectorLogic
options-shroud-selector =
    .all-players = 所有玩家
    .disable-shroud = 关闭迷雾
    .other = 其他

## ObserverStatsLogic
options-observer-stats =
    .none = 信息：无
    .basic = 基本
    .economy = 经济
    .production = 生产
    .support-powers = 支援技能
    .combat = 战斗
    .army = 军队
    .earnings-graph = 收入（图表）
    .army-graph = 军队（图表）

## WorldTooltipLogic
label-unrevealed-terrain = 未探索区域

## DownloadPackageLogic
label-downloading = 正在下载 { $title }
label-fetching-mirror-list = 正在获取镜像列表……
label-downloading-from = 正在从 { $host } 下载 { $received } { $suffix }
label-downloading-from-progress = 正在从 { $host } 下载 { $received } / { $total } { $suffix }（{ $progress }%）
label-unknown-host = 未知主机
label-download-failed = 下载失败
label-verifying-archive = 正在校验压缩包……
label-archive-validation-failed = 压缩包校验失败
label-extracting-archive = 正在解压……
label-extracting-archive-entry = 正在解压 { $entry }
label-archive-extraction-failed = 压缩包解压失败
label-mirror-selection-failed = 在线镜像不可用，请从原版光盘安装。

## InstallFromSourceLogic
label-detecting-sources = 正在检测驱动器
label-checking-sources = 正在检查来源
label-searching-source-for = 正在搜索 { $title }
label-content-package-installation = 选择要安装的内容包：
label-game-sources = 游戏来源
label-digital-installs = 数字版安装
label-game-content-not-found = 未找到游戏内容
label-alternative-content-sources = 请插入或安装以下内容来源之一：
label-installing-content = 正在安装内容
label-copying-filename = 正在复制 { $filename }
label-copying-filename-progress = 正在复制 { $filename }（{ $progress }%）
label-installation-failed = 安装失败
label-check-install-log = 详见 logs 目录下的 install.log。
label-extracting-filename = 正在解压 { $filename }
label-extracting-filename-progress = 正在解压 { $filename }（{ $progress }%）

## ModContentLogic
button-manual-install = 手动安装

## KickClientLogic
dialog-kick-client =
    .prompt = 踢出 { $player }？

## KickSpectatorsLogic
dialog-kick-spectators =
    .prompt =
    { $count ->
        [one] 确定要踢出 1 名观战者吗？
       *[other] 确定要踢出 { $count } 名观战者吗？
    }

## LobbyLogic
options-slot-admin =
    .add-bots = 添加
    .remove-bots = 移除
    .configure-bots = 配置电脑
    .teams-count = { $count } 支队伍
    .humans-vs-bots = 玩家对电脑
    .free-for-all = 各自为战
    .configure-teams = 配置队伍

## LobbyLogic, InGameChatLogic
button-general-chat = 全体
button-team-chat = 队伍

## LobbyOptionsLogic, MissionBrowserLogic
label-not-available = 不可用

## LobbyUtils
options-lobby-slot =
    .slot = 位置
    .open = 开放
    .closed = 关闭
    .bots = 电脑
    .bots-disabled = 已禁用电脑

## MapPreviewLogic
label-connecting = 正在连接……
label-downloading-map = 正在下载 { $size } kB
label-downloading-map-progress = 正在下载 { $size } kB（{ $progress }%）
button-retry-install = 重试安装
button-retry-search = 重试搜索
## also MapChooserLogic
label-created-by = 作者：{ $author }

## SpawnSelectorTooltipLogic
label-disabled-spawn = 已禁用的出生点
label-available-spawn = 可用的出生点

## DisplaySettingsLogic
options-camera =
    .close = 近
    .medium = 中
    .far = 远
    .furthest = 最远

options-display-mode =
    .windowed = 窗口化
    .legacy-fullscreen = 全屏（旧式）
    .fullscreen = 全屏

label-video-display-index = 显示器 { $number }

options-status-bars =
    .standard = 标准
    .show-on-damage = 受伤时显示
    .always-show = 始终显示

options-target-lines =
    .automatic = 自动
    .manual = 手动
    .disabled = 禁用

checkbox-frame-limiter = 启用帧率限制（{ $fps } FPS）

## HotkeysSettingsLogic
label-original-notice = 默认为「{ $key }」
label-duplicate-notice = 该键已在 { $context } 上下文中用于「{ $key }」
hotkey-context-any = 任意

## InputSettingsLogic
options-mouse-scroll-type =
    .disabled = 禁用
    .standard = 标准
    .inverted = 反转
    .joystick = 摇杆

label-touch-joystick-size-container = 虚拟摇杆尺寸
options-touch-joystick-size =
    .small = 小（112 pt）
    .medium = 中（128 pt）
    .large = 大（144 pt）

## InputSettingsLogic, IntroductionPromptLogic
options-control-scheme =
    .classic = 经典模式（左键下令）
    .modern = 新模式（右键下令）

## SettingsLogic
dialog-settings-save =
    .title = 需要重启
    .prompt = 部分更改将在
    重启游戏后生效。
    .cancel = 继续

dialog-settings-restart =
    .title = 立即重启？
    .prompt = 部分更改将在重启游戏后
    生效。现在重启吗？
    .confirm = 立即重启
    .cancel = 稍后重启

dialog-settings-reset =
    .title = 重置{ $panel }
    .prompt = 确定要重置
    此面板中的所有设置吗？
    .confirm = 重置
    .cancel = 取消

## AssetBrowserLogic
label-all-packages = 所有包
label-length-in-seconds = { $length } 秒

## ConnectionLogic
label-connecting-to-endpoint = 正在连接 { $endpoint }……
label-could-not-connect-to-target = 无法连接到 { $target }
label-unknown-error = 未知错误
label-password-required = 需要密码
label-connection-failed = 连接失败
notification-mod-switch-failed = 模组切换失败。

## GameSaveBrowserLogic
dialog-rename-save =
    .title = 重命名存档
    .prompt = 输入新文件名：
    .confirm = 重命名

dialog-delete-save =
    .title = 删除所选存档？
    .prompt = 删除「{ $save }」。
    .confirm = 删除

dialog-delete-all-saves =
    .title = 删除全部存档？
    .prompt =
    { $count ->
       *[other] 删除 { $count } 个存档。
    }
    .confirm = 全部删除

notification-save-deletion-failed = 删除存档「{ $savePath }」失败，详见日志。

dialog-overwrite-save =
    .title = 覆盖存档？
    .prompt = 覆盖 { $file }？
    .confirm = 覆盖

## MainMenuLogic
label-loading-news = 正在加载新闻
label-news-retrieval-failed = 获取新闻失败：{ $message }
label-news-parsing-failed = 解析新闻失败：{ $message }
label-author-datetime = { $author } 发布于 { $datetime }

## MapChooserLogic
label-all-maps = 所有地图
label-no-matches = 无匹配
label-player-count =
    { $players ->
       *[other] { $players } 名玩家
    }
label-map-size-huge = （巨大）
label-map-size-large = （大）
label-map-size-medium = （中）
label-map-size-small = （小）
label-map-searching-count =
    { $count ->
       *[other] 正在 OpenRA 资源中心搜索 { $count } 张地图……
    }
label-map-unavailable-count =
    { $count ->
       *[other] { $count } 张地图未在 OpenRA 资源中心找到
    }

notification-map-deletion-failed = 删除地图「{ $map }」失败，详见 debug.log。

dialog-delete-map =
    .title = 删除地图
    .prompt = 删除地图「{ $title }」？
    .confirm = 删除

dialog-delete-all-maps =
    .title = 删除地图
    .prompt = 删除本页所有地图？
    .confirm = 删除

options-order-maps =
    .player-count = 玩家
    .title = 标题
    .date = 日期
    .size = 大小

## MissionBrowserLogic
dialog-no-video =
    .title = 未安装视频
    .prompt = 可在模组选择器的
    「内容管理」菜单中安装游戏视频。
    .cancel = 返回

dialog-cant-play-video =
    .title = 无法播放视频
    .prompt = 视频播放时出现问题。
    .cancel = 返回

## MusicPlayerLogic
label-sound-muted = 音频已在设置中静音。
label-no-song-playing = 没有正在播放的曲目

## MuteHotkeyLogic
label-audio-muted = 音频已静音。
label-audio-unmuted = 音频已取消静音。

## PlayerProfileLogic
label-loading-player-profile = 正在加载玩家档案……
label-loading-player-profile-failed = 玩家档案加载失败。

## ProductionTooltipLogic, EncyclopediaLogic
label-requires = 需要{ $prerequisites }。

## ReplayBrowserLogic
label-duration = 时长：{ $time }

options-replay-type =
    .singleplayer = 单人游戏
    .multiplayer = 多人游戏

options-winstate =
    .victory = 胜利
    .defeat = 失败

options-replay-date =
    .today = 今天
    .last-week = 最近 7 天
    .last-fortnight = 最近 14 天
    .last-month = 最近 30 天

options-replay-duration =
    .very-short = 5 分钟以内
    .short = 短（10 分钟）
    .medium = 中（30 分钟）
    .long = 长（60 分钟以上）

dialog-rename-replay =
    .title = 重命名回放
    .prompt = 输入新文件名：
    .confirm = 重命名

dialog-delete-replay =
    .title = 删除所选回放？
    .prompt = 删除回放 { $replay }？
    .confirm = 删除

dialog-delete-all-replays =
    .title = 删除全部所选回放？
    .prompt =
    { $count ->
       *[other] 删除 { $count } 个回放。
    }
    .confirm = 全部删除

notification-replay-deletion-failed = 删除回放文件「{ $file }」失败，详见 debug.log。

## ReplayUtils
-incompatible-replay-recorded = 录制时使用的是

dialog-incompatible-replay =
    .title = 回放不兼容
    .prompt = 无法读取回放元数据。
    .confirm = 确定
    .prompt-unknown-version = { -incompatible-replay-recorded }未知版本。
    .prompt-unknown-mod = { -incompatible-replay-recorded }未知模组。
    .prompt-unavailable-mod = { -incompatible-replay-recorded }不可用的模组：{ $mod }。
    .prompt-incompatible-version = { -incompatible-replay-recorded }不兼容的版本：
    { $version }。
    .prompt-unavailable-map = { -incompatible-replay-recorded }不可用的地图：
    { $map }。

# SelectUnitsByTypeHotkeyLogic
nothing-selected = 未选中任何单位。

## SelectUnitsByTypeHotkeyLogic, SelectAllUnitsHotkeyLogic
selected-units-across-screen =
    { $units ->
       *[other] 已选中屏幕内 { $units } 个单位。
    }

selected-units-across-map =
    { $units ->
       *[other] 已选中全图 { $units } 个单位。
    }

## ServerCreationLogic
label-internet-server-nat-A = 互联网服务器（UPnP/NAT-PMP
label-internet-server-nat-B-enabled = 已启用
label-internet-server-nat-B-not-supported = 不支持
label-internet-server-nat-B-disabled = 已禁用
label-internet-server-nat-C = ）：

label-local-server = 本地服务器：

dialog-server-creation-failed =
    .prompt = 无法监听端口 { $port }。
    .prompt-port-used = 请检查端口是否已被占用。
    .prompt-error = 错误：「{ $message }」（{ $code }）。
    .title = 服务器创建失败
    .cancel = 返回

## ServerListLogic
label-players-online-count =
    { $players ->
       *[other] { $players } 名玩家在线
    }

label-search-status-failed = 查询服务器列表失败。
label-search-status-no-games = 未找到游戏，请尝试更改筛选条件。
label-no-server-selected = 未选择服务器
label-online-room-status-no-rooms = 当前没有可加入的公网房间。
label-online-room-error-unavailable = 在线大厅暂时不可用，局域网联机仍可正常使用。
label-online-room-error-disappeared = 这个房间已经不存在。
label-online-room-error-rate-limited = 房间查询过于频繁，请稍后重试。
label-online-room-error-invalid-response = 在线大厅返回了无效的房间信息。
label-online-room-error-invalid-code = 请输入有效的六位房间代码。
label-online-room-error-handshake = 握手协议不兼容
label-online-room-error-orders = 游戏指令协议不兼容
label-online-room-error-engine = 引擎版本不兼容
label-online-room-error-mod = 游戏模块不兼容
label-online-room-error-version = 游戏核心不兼容
label-online-room-error-resources = 导入资源不兼容

label-map-status-searching = 搜索中……
label-map-classification-unknown = 未知地图

label-players-count =
    { $players ->
        [0] 无玩家
        [one] 1 名玩家
       *[other] { $players } 名玩家
    }

label-bots-count =
    { $bots ->
        [0] 无电脑
        [one] 1 个电脑
       *[other] { $bots } 个电脑
    }

## ServerListLogic, ReplayBrowserLogic, ObserverShroudSelectorLogic
label-players = 玩家

## ServerListLogic, GameInfoStatsLogic
label-spectators = 观战者
label-spectators-count =
    { $spectators ->
        [0] 无观战者
        [one] 1 名观战者
       *[other] { $spectators } 名观战者
    }

## ServerlistLogic, GameInfoStatsLogic, ObserverShroudSelectorLogic, SpawnSelectorTooltipLogic, ReplayBrowserLogic
label-team-name = 队伍 { $team }
label-no-team = 无队伍

label-playing = 进行中
label-waiting = 等待中

label-other-players-count =
    { $players ->
        [one] 另 1 名玩家
       *[other] 另 { $players } 名玩家
    }

label-in-progress-for =
    { $minutes ->
        [0] 已进行不到 1 分钟。
       *[other] 已进行 { $minutes } 分钟。
    }

label-password-protected = 需要密码
label-waiting-for-players = 等待玩家
label-server-shutting-down = 服务器正在关闭
label-unknown-server-state = 服务器状态未知

## Game
notification-saved-screenshot = 已保存截图 { $filename }

## ChatCommands
notification-invalid-command = { $name } 不是有效命令。

## DebugVisualizationCommands
description-combat-geometry = 切换战斗几何覆盖层。
description-render-geometry = 切换渲染几何覆盖层。
description-screen-map-overlay = 切换屏幕地图覆盖层。
description-depth-buffer = 切换深度缓冲覆盖层。
description-actor-tags-overlay = 切换单位标签覆盖层。

## DevCommands
notification-cheats-disabled = 作弊已禁用。
notification-invalid-cash-amount = 无效的资金数量。
description-toggle-visibility = 切换可见性检查与小地图。
description-give-cash = 给予默认或指定数量的资金。
description-give-cash-all = 给予所有玩家与 AI 默认或指定数量的资金。
description-instant-building = 切换瞬间建造。
description-build-anywhere = 切换任意地点建造。
description-unlimited-power = 切换无限电力。
description-enable-tech = 切换全科技建造。
description-fast-charge = 切换支援技能快速充能。
description-dev-cheat-all = 开启全部作弊并附赠资金。
description-dev-crash = 使游戏崩溃。
description-levelup-actor = 为选中单位增加指定等级。
description-player-experience = 为选中单位的所有者增加指定玩家经验。
description-power-outage = 使选中单位的所有者停电 5 秒。
description-kill-selected-actors = 杀死选中单位。
description-dispose-selected-actors = 移除选中单位。

## HelpCommands
notification-available-commands = 可用命令如下：
description-no-description = 暂无描述。
description-help-description = 提供各种命令的实用信息。

## PlayerCommands
description-pause-description = 暂停或恢复游戏。
description-surrender-description = 自毁一切并输掉游戏。

## DeveloperMode
notification-cheat-used = 使用作弊：{ $player } 使用了 { $cheat }{ $suffix }。

## CustomTerrainDebugOverlay
description-custom-terrain-debug-overlay = 切换自定义地形调试覆盖层。

## CellTriggerOverlay
description-cell-triggers-overlay = 切换脚本触发器覆盖层。

## ExitsDebugOverlay
description-exits-overlay = 显示工厂出口。

## HierarchicalPathFinderOverlay
description-hpf-debug-overlay = 切换分层寻路覆盖层。

## PathFinderOverlay
description-path-debug-overlay = 切换寻路可视化。

## TerrainGeometryOverlay
description-terrain-geometry-overlay = 切换地形几何覆盖层。

## MapOptions, MissionBrowserLogic
options-game-speed =
    .slowest = 最慢
    .slower = 较慢
    .normal = 普通
    .fast = 快
    .faster = 较快
    .fastest = 最快

## TimeLimitManager
options-time-limit =
    .no-limit = 无限制
    .options =
        { $minutes ->
           *[other] { $minutes } 分钟
        }

notification-time-limit-expired = 时间限制已到。

## EditorActorBrush
notification-added-actor = 已添加 { $name }（{ $id }）

## EditorCopyPasteBrush
notification-copied-tiles =
    { $amount ->
       *[other] 已复制 { $amount } 个地块
    }

## EditorDefaultBrush
notification-selected-area = 已选中区域 { $x },{ $y }（{ $width },{ $height }）
notification-removed-area = 已移除区域 { $x },{ $y }（{ $width },{ $height }）
notification-selected-actor = 已选中单位 { $id }
notification-cleared-selection = 已清除选择
notification-removed-actor = 已移除 { $name }（{ $id }）
notification-removed-resource = 已移除 { $type }
notification-moved-actor = 已将 { $id } 从 { $x1 },{ $y1 } 移动到 { $x2 },{ $y2 }

## EditorResourceBrush
notification-added-resource =
    { $amount ->
       *[other] 已添加 { $amount } 格 { $type }
    }

## EditorTileBrush
notification-added-tile = 已添加地块 { $id }
notification-filled-tile = 已填充地块 { $id }

## EditorMarkerLayerBrush
notification-added-marker-tiles =
    { $amount ->
       *[other] 已添加 { $amount } 个 { $type } 类型标记地块
    }
notification-removed-marker-tiles =
    { $amount ->
       *[other] 已移除 { $amount } 个标记地块
    }
notification-cleared-selected-marker-tiles = 已清除 { $amount } 个 { $type } 类型标记地块
notification-cleared-all-marker-tiles = 已清除 { $amount } 个标记地块

## EditorActionManager
notification-opened = 已打开

## MapOverlaysLogic
mirror-mode =
    .none = 无
    .flip = 翻转
    .rotate = 旋转

## ActorEditLogic
notification-edited-actor = 已编辑 { $name }（{ $id }）
notification-edited-actor-id = 已编辑 { $name }（{ $old-id }-> { $new-id }）

## ConquestVictoryConditions, StrategicVictoryConditions
notification-player-is-victorious = { $player } 获得胜利。
notification-player-is-defeated = { $player } 已被击败。

## OrderManager
notification-desync-compare-logs = 第 { $frame } 帧失去同步。
    请与其他玩家对比 syncreport.log。

## SupportPowerTimerWidget
support-power-timer = { $player } 的{ $support-power }：{ $time }

## WidgetUtils
label-win-state-won = 胜
label-win-state-lost = 负

## Player
enumerated-bot-name =
    { $name } { $number ->
       *[zero] {""}
        [other] { $number }
    }
