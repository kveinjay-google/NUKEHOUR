"""Bilingual strings and language policy for the NUKE HOUR macOS launcher."""

from __future__ import annotations

import locale
import os
import re
import subprocess

PREFERENCES = (("System", "System"), ("简体中文", "zh-CN"), ("English", "en"))
LANGUAGES = PREFERENCES

EN: dict[str, str] = {
    '使用与发行声明': 'Use and Distribution Notice',
    '请在继续前阅读以下重要信息': 'Please read this important information before continuing',
    '官方网站': 'Official Website',
    '继续': 'Continue',
    '同意并继续': 'Agree and Continue',
    '''NUKE HOUR 是完全独立开发、完全免费且开源的软件项目。

EA 未认可且不支持本产品。本项目与 EA、Apple 或 Google 不存在隶属、赞助、授权或支持关系。

本软件不包含任何第三方零售游戏文件。用户必须从自己合法购买并拥有的正版游戏副本中手动导入兼容文件。界面中出现的游戏或软件名称仅用于说明可兼容导入文件的来源，不代表关联、授权、认可或支持。

本软件完全免费开源。从任何地方付费购买本软件都属于受骗上当；请向作者举报销售者，并向销售渠道申请退款。

请只从 NUKE HOUR 官方网站下载官方授权版本。不要从官方网站以外的任何地方下载安装，以防软件被篡改或植入恶意代码。''':
        '''NUKE HOUR is a completely independent, free, and open-source software project.

EA has not endorsed and does not support this product. This project is not affiliated with, sponsored by, authorized by, or supported by EA, Apple, or Google.

This software contains no third-party retail game files. Users must manually import compatible files from a legally purchased and owned copy of the original game. Game or software names shown in the interface are plain-text references used only to identify compatible import sources; they do not imply affiliation, authorization, endorsement, or support.

This software is entirely free and open source. If anyone charges you for this software, you have been deceived. Please report the seller to the author and request a refund from the marketplace or payment provider.

Download only the official authorized version from the NUKE HOUR website. Do not install copies obtained elsewhere, because they may have been modified or contain malicious code.''',
    '中': 'Medium',
    '低': 'Low',
    '快': 'Fast',
    '慢': 'Slow',
    '无': 'None',
    '粉': 'Pink',
    '紫': 'Purple',
    '近': 'Close',
    '远': 'Far',
    '青': 'Teal',
    ' 秒': ' sec',
    '其他': 'Other',
    '删除': 'Delete',
    '刷新': 'Refresh',
    '双倍': 'Double',
    '反向': 'Inverted',
    '古巴': 'Cuba',
    '回车': 'Enter',
    '复制': 'Duplicate',
    '德国': 'Germany',
    '新建': 'New',
    '显示': 'Show',
    '最快': 'Fastest',
    '最慢': 'Slowest',
    '标准': 'Standard',
    '法国': 'France',
    '禁用': 'Disabled',
    '空格': 'Space',
    '端口': 'Port',
    '网络': 'Network',
    '美国': 'America',
    '翠绿': 'Green',
    '聊天': 'Chat',
    '自动': 'Automatic',
    '苏俄': 'Russia',
    '英国': 'Britain',
    '观看': 'Watch',
    '视频': 'Video',
    '载入': 'Load',
    '退格': 'Backspace',
    '通用': 'General',
    '速度': 'Speed',
    '重置': 'Reset',
    '金黄': 'Gold',
    '阵营': 'Faction',
    '随机': 'Random',
    '静音': 'Mute',
    '韩国': 'Korea',
    '音量': 'Volume',
    '鼠标': 'Mouse',
    ' 副本': ' Copy',
    '1 队': 'Team 1',
    '2 队': 'Team 2',
    '3 队': 'Team 3',
    '4 队': 'Team 4',
    '不可用': 'Unavailable',
    '仅步兵': 'Infantry Only',
    '伊拉克': 'Iraq',
    '利比亚': 'Libya',
    '房间名': 'Room Name',
    '指挥官': 'Commander',
    '熔岩橙': 'Orange',
    '状态栏': 'Status Bars',
    '状态：': 'Status:',
    '盟军蓝': 'Allied Blue',
    '窗口化': 'Windowed',
    '苏联红': 'Soviet Red',
    '遭遇战': 'Skirmish',
    '重绑定': 'Rebind',
    '← 返回': '← Back',
    '个人资料': 'Profile',
    '任务战役': 'Campaign',
    '你的阵营': 'Your Faction',
    '保存失败': 'Save failed',
    '偏好颜色': 'Preferred Color',
    '光标大小': 'Cursor Size',
    '全部重置': 'Reset All',
    '内容管理': 'Content',
    '初始资金': 'Starting Cash',
    '删除失败': 'Delete failed',
    '制作名单': 'Credits',
    '加入房间': 'Join Room',
    '单位指令': 'Unit Orders',
    '启动失败': 'Launch failed',
    '回放控制': 'Replay Controls',
    '回放管理': 'Replays',
    '地图管理': 'Maps',
    '垂直同步': 'VSync',
    '多人联机': 'Multiplayer',
    '存档管理': 'Saves',
    '导入失败': 'Import failed',
    '导入成功': 'Import successful',
    '导入游戏目录…': 'Import Game Folder…',
    '选择 MIX / 地图…': 'Choose MIX / Maps…',
    '基础游戏': 'Base Game',
    '音乐': 'Music',
    '尤里的复仇': "Yuri's Revenge",
    '地图': 'Maps',
    '选择游戏目录或已挂载光盘': 'Choose a game folder or mounted disc',
    '选择零售内容文件': 'Choose retail content files',
    '零售内容与地图': 'Retail Content and Maps',
    '基础内容已就绪，现在可以开始游戏。': 'Base content is ready. You can now play.',
    '仍缺少核心文件': 'Core Files Still Missing',
    '需要导入游戏内容': 'Game Content Required',
    '常用尺寸': 'Common Sizes',
    '开始游戏': 'Play',
    '性能调试': 'Performance Debug',
    '总是显示': 'Always Show',
    '战场操作': 'Battlefield Controls',
    '战场镜头': 'Battlefield Camera',
    '房间地址': 'Room Address',
    '所有文件': 'All Files',
    '指令切换': 'Order Toggle',
    '按键冲突': 'Key Conflict',
    '操作失败': 'Operation failed',
    '操作方案': 'Control Scheme',
    '鼠标操作模式': 'Mouse Control Mode',
    '经典：左键选取单位，左键下达移动/攻击，右键取消　｜　新模式：左键选取，右键下达指令（当前默认）':
        'Classic: Left select & order (move/attack), Right cancel  |  Modern: Left select, Right order (default)',
    '新模式（左键选单位，右键移动/攻击）': 'Modern (Left select, Right move/attack)',
    '经典模式（左键选单位与下令，右键取消）': 'Classic (Left select & order, Right cancel)',
    '支援技能': 'Support Powers',
    '无法开局': 'Unable to start',
    '显示设置': 'Display',
    '更多功能': 'More',
    '未能获取': 'Unavailable',
    '检测中…': 'Checking…',
    '检测连接': 'Test Connection',
    '游戏速度': 'Game Speed',
    '热键设置': 'Hotkeys',
    '玩家名称': 'Player Name',
    '生产建造': 'Production',
    '生产槽位': 'Production Tabs',
    '电脑对手': 'AI Opponents',
    '矿场上限': 'Harvester Limit',
    '矿车上限': 'Ore Truck Limit',
    '确认删除': 'Confirm Delete',
    '科技等级': 'Tech Level',
    '窗口尺寸': 'Window Size',
    '端口无效': 'Invalid port',
    '简体中文': '简体中文',
    '经典全屏': 'Legacy Fullscreen',
    '编队控制': 'Squad Control',
    '缩放速度': 'Zoom Speed',
    '缺少地址': 'Address required',
    '缺少文件': 'Missing Files',
    '观战视角': 'Spectator View',
    '视频模式': 'Video Mode',
    '视频音量': 'Video Volume',
    '资金音效': 'Cash Tick',
    '载入存档': 'Load Save',
    '输入设置': 'Input',
    '进攻风格': 'Attack Style',
    '选择地图': 'Select Map',
    '部队姿态': 'Unit Stance',
    '随机盟军': 'Random Allied',
    '随机苏军': 'Random Soviet',
    '音乐控制': 'Music Controls',
    '音乐音量': 'Music Volume',
    '音效音量': 'Sound Volume',
    '音频设置': 'Audio',
    '高级设置': 'Advanced',
    'AI 数量': 'AI Count',
    'AI 配置': 'AI Profiles',
    'UI 缩放': 'UI Scale',
    '中（默认）': 'Medium (Default)',
    '停止服务器': 'Stop Server',
    '全屏显示器': 'Fullscreen Display',
    '单人遭遇战': 'Singleplayer Skirmish',
    '原生分辨率': 'Native Resolution',
    '受损时显示': 'Show When Damaged',
    '启动服务器': 'Start Server',
    '地图编辑器': 'Map Editor',
    '导入地图…': 'Import Map…',
    '小键盘回车': 'Numpad Enter',
    '开发者选项': 'Developer Options',
    '战场与规则': 'Battlefield & Rules',
    '无玩家信息': 'No player info',
    '显示器 2': 'Display 2',
    '显示器 3': 'Display 3',
    '显示器 4': 'Display 4',
    '标准 AI': 'Standard AI',
    '游戏运行中': 'Game Running',
    '目标指示线': 'Target Lines',
    '素材浏览器': 'Asset Browser',
    '经济与生产': 'Economy & Production',
    '缩放修饰键': 'Zoom Modifier',
    '请输入地址': 'Enter address',
    '进入主菜单': 'Open Main Menu',
    '退出启动器': 'Quit Launcher',
    '遭遇战开局': 'Skirmish',
    '配置名称：': 'Profile Name:',
    '镜头与视野': 'Camera & Viewport',
    '需启动引擎': 'Requires Engine',
    '音乐播放器': 'Music Player',
    '一般无需修改': 'Usually leave as-is',
    '仅进入主菜单': 'Main Menu Only',
    '停工现金门槛': 'Idle Cash Threshold',
    '启动器内功能': 'Launcher Features',
    '地图平移速度': 'Map Pan Speed',
    '屏幕边缘滚动': 'Edge Scroll',
    '建筑生产间隔': 'Building Production Interval',
    '循环播放音乐': 'Loop Music',
    '恢复出厂预设': 'Restore Factory Presets',
    '成队最小间隔': 'Squad Min Interval',
    '打开内容目录': 'Open Content Folder',
    '打开回放目录': 'Open Replays Folder',
    '打开地图目录': 'Open Maps Folder',
    '打开存档目录': 'Open Saves Folder',
    '打开完整设置': 'Open Full Settings',
    '显示性能图表': 'Show Performance Graph',
    '显示性能文本': 'Show Performance Text',
    '本机 IP：': 'Local IP:',
    '标准（默认）': 'Standard (Default)',
    '格式 宽,高': 'Format: width,height',
    '模拟性能日志': 'Simulate Perf Log',
    '没有可用地图': 'No maps available',
    '尚无可用地图': 'No maps imported',
    '当前没有可用地图。请先在“地图管理”或“内容管理”中导入合法地图。':
        'No maps are currently available. Import a legal map from Maps or Content first.',
    '游戏事件通知': 'Game Event Notifications',
    '玩家信息未知': 'Player info unknown',
    '界面反馈通知': 'UI Feedback Notifications',
    '空闲检查间隔': 'Idle Check Interval',
    '红色警戒 2': 'Red Alert 2',
    '自动（推荐）': 'Auto (Recommended)',
    '自定义 AI': 'Custom AI',
    '规模随机加成': 'Size Random Bonus',
    '进攻小队规模': 'Attack Squad Size',
    '进攻尝试间隔': 'Attack Attempt Interval',
    '选择地图文件': 'Select Map File',
    '随机播放音乐': 'Shuffle Music',
    '鼠标滚动方向': 'Mouse Scroll',
    '  一键套用：': '  Quick Apply:',
    'AI 对手配置': 'AI Opponent Profiles',
    'UI 滚动速度': 'UI Scroll Speed',
    '保存配置并生效': 'Save & Apply',
    '分矿扩张基地数': 'Expansion Base Count',
    '加 入 游 戏': 'J O I N  G A M E',
    '回放中隐藏聊天': 'Hide Chat in Replay',
    '小键盘{_i}': 'Numpad {_i}',
    '开 始 游 戏': 'S T A R T  G A M E',
    '摇杆式（推荐）': 'Joystick (Recommended)',
    '标准（经典右键甩视角）': 'Standard (classic right-hold pan)',
    '摇杆式（泰星风格）': 'Joystick (Tiberian Sun style)',
    '经典：左键选取/下令，右键取消；按住右键滑动可快速甩视角　｜　新模式：左键选取，右键下达指令':
        'Classic: left select/order, right cancel; hold right and swipe to pan quickly  |  Modern: left select, right order',
    '无限制（默认）': 'Unlimited (Default)',
    '正在启动游戏…': 'Starting game…',
    '游戏已启动': 'Game started',
    '已返回启动器': 'Returned to launcher',
    '游戏内设置面板': 'In-Game Settings',
    '缺少 .NET': 'Missing .NET',
    '5000（默认）': '5000 (Default)',
    '○ 服务器未启动': '○ Server not running',
    '新建 AI 配置': 'New AI Profile',
    '暂停菜单背景动画': 'Pause Menu Background Animation',
    '禁用系统硬件光标': 'Disable Hardware Cursor',
    '红色警戒2 房间': 'Red Alert 2 Room',
    '经典（左键命令）': 'Classic (Left-Click Orders)',
    '自动（主显示器）': 'Auto (Primary Display)',
    'OpenGL 渲染': 'OpenGL Renderer',
    '游戏地图': 'Game Map',
    '回放中启用调试命令': 'Enable Debug Commands in Replay',
    '多人联机（局域网）': 'Multiplayer (LAN)',
    '引擎与模组制作人员': 'Engine & Mod Credits',
    '手动（按住快捷键）': 'Manual (Hold Hotkey)',
    '无边框全屏（推荐）': 'Borderless Fullscreen (Recommended)',
    '现代 OpenGL': 'Modern OpenGL',
    '配置名称与风格模板': 'Profile Name & Style Template',
    'AI 机器人调试信息': 'AI Bot Debug Info',
    'Lua 脚本调试信息': 'Lua Script Debug Info',
    '✓ 可以连接到该房间': '✓ Can connect to this room',
    '启动失败：{exc}': 'Launch failed: {exc}',
    '启用游戏内开发者设置': 'Enable In-Game Developer Settings',
    '完整，游戏可正常运行': 'Complete — game can run normally',
    '已切到正在运行的游戏': 'Switched to the running game',
    '资金增减时播放点钞声': 'Play cash tick on funds change',
    '锁定鼠标于游戏窗口内': 'Lock Mouse to Game Window',
    '已保存配置（★为预设）': 'Profile saved (★ = preset)',
    '开设房间（本机做主机）': 'Host Room (This Mac as Server)',
    '本模组尚未包含战役任务': 'This mod does not include campaign missions yet',
    '载入或清理已保存的对局': 'Load or clean up saved games',
    '限制帧率为游戏逻辑速度': 'Cap Frame Rate to Game Logic Speed',
    '查看游戏内容资产安装状态': 'View game content asset install status',
    '请先退出游戏再修改热键。': 'Please quit the game before editing hotkeys.',
    '读取回放失败：{exc}': 'Failed to read replay: {exc}',
    '读取地图失败：{exc}': 'Failed to read map: {exc}',
    '读取存档失败：{exc}': 'Failed to read save: {exc}',
    '嵌入式 OpenGL ES': 'Embedded OpenGL ES',
    '引擎功能（点击后启动游戏）': 'Engine Features (launches the game)',
    '显示指令反馈与快捷聊天提示': 'Show order feedback and quick-chat tips',
    '游戏已启动，窗口即将关闭…': 'Game started — this window will close…',
    '现代（左键选取/右键命令）': 'Modern (Left Select / Right Order)',
    '观看、整理已保存的比赛回放': 'Watch and manage saved match replays',
    '光标有残影或闪烁时可尝试开启': 'Try enabling if the cursor ghosts or flickers',
    '把存储值转换成下拉框显示值。': 'Convert stored values to dropdown display values.',
    '禁用 OpenGL 调试回调': 'Disable OpenGL Debug Callbacks',
    '选「否」则切到已有游戏窗口。': 'Choose No to switch to the existing game window.',
    'Esc 取消 · 退格清除绑定': 'Esc cancel · Backspace clear binding',
    '启动失败：未找到 dotnet': 'Launch failed: dotnet not found',
    '备用鼠标平移（中键/右键拖拽）': 'Alternate Mouse Pan (Middle/Right Drag)',
    '显示探测到敌军、建筑丢失等提示': 'Show tips for enemy detection, building loss, etc.',
    '检测到游戏已经在运行。\n\n': 'A game is already running.\n\n',
    '短局模式（消灭全部敌人即获胜）': 'Short Game (win by eliminating all enemies)',
    "作者 {m['author']}": "Author {m['author']}",
    '出厂预设已恢复默认数值并重新生成': 'Factory presets restored to defaults and regenerated',
    '按玩家关系显示颜色（敌红/友黄）': 'Color by player relation (enemy red / ally yellow)',
    '浏览官方地图，导入或删除玩家地图': 'Browse official maps; import or delete player maps',
    '联机开房时自动在路由器上打开端口': 'Auto-open router ports when hosting multiplayer',
    "{m['players']} 玩家": "{m['players']} players",
    '创建或编辑遭遇战地图（引擎内置工具）': 'Create or edit skirmish maps (built-in engine tool)',
    '在游戏内高级设置页显示隐藏的开发者选项': 'Show hidden developer options on the in-game Advanced page',
    'NUKE HOUR · macOS': 'NUKE HOUR · macOS',
    '浏览单位、动画与音频素材（引擎内置工具）': 'Browse units, animations, and audio assets (built-in engine tool)',
    '输出 perf.log（仅排查卡顿需要）': 'Write perf.log (only needed when diagnosing stutter)',
    '选「是」将强制退出旧进程并重新启动；\n': 'Choose Yes to force-quit the old process and restart;\n',
    '未找到游戏启动脚本：\n{GAME_SH}': 'Game launch script not found:\n{GAME_SH}',
    '玩家地图（{len(custom)} 张）': 'Player maps ({len(custom)})',
    '端口需为 1024-65535 之间的数字': 'Port must be a number between 1024 and 65535',
    '出厂预设不可删除，可修改数值后用「复制」另存': 'Factory presets cannot be deleted; tweak values and Duplicate to save a copy',
    '启用 NAT-PMP / UPnP 端口映射': 'Enable NAT-PMP / UPnP Port Mapping',
    '降低画面流畅度，但可减少 CPU 与电量消耗': 'Lowers smoothness but reduces CPU and battery use',
    '为「{label_text}」按下新的按键组合': 'Press a new key combination for “{label_text}”',
    '在页面内放置可滚动的内容框架，返回内容父容器。': 'Place a scrollable content frame in the page; return the content parent.',
    '已创建空白配置，调整数值后点「保存配置并生效」': 'Blank profile created — adjust values, then click Save & Apply',
    'NUKE HOUR · 启动中枢': 'NUKE HOUR · Launcher',
    '✗ 连不上，请确认 IP/端口与房主服务器已启动': '✗ Cannot connect — check IP/port and that the host server is running',
    '未找到 dotnet，请先安装 .NET 运行时': 'dotnet not found — please install the .NET runtime first',
    '选择模式后点击下方按钮启动，或从左侧进入详细设置': 'Pick a mode and start below, or open detailed settings on the left',
    "已删除「{p['name']}」并重新生成游戏人格": "Deleted “{p['name']}” and regenerated game personalities",
    '模板只填入数值，点「保存配置并生效」后才会写入游戏': 'Templates only fill values; click Save & Apply to write them to the game',
    'NUKE HOUR GAME.app': 'NUKE HOUR GAME.app',
    '官方地图（{len(official)} 张，只读）': 'Official maps ({len(official)}, read-only)',
    '提示：「更多功能」页可直达回放、存档、地图等管理面板': 'Tip: the More page opens replay, save, map, and other managers',
    '解析 fluent 文件中的热键中文描述（英文兜底）。': 'Parse Chinese hotkey descriptions from fluent files (English fallback).',
    '解析引擎/模组的热键定义 yaml，返回有序定义列表。': 'Parse engine/mod hotkey YAML and return an ordered definition list.',
    '常见路径：/usr/local/share/dotnet': 'Common path: /usr/local/share/dotnet',
    '游戏引擎与 Red Alert 2 模组的贡献者': 'Contributors to the game engine and Red Alert 2 mod',
    "{m['name']}（{m['players']} 人）": "{m['name']} ({m['players']} players)",
    '热键请使用左侧「热键设置」页；此处可打开游戏内完整设置面板': 'Use Hotkeys on the left for key bindings; here you can open the full in-game settings panel',
    "确定删除「{rec['name']}」吗？此操作不可恢复。": "Delete “{rec['name']}”? This cannot be undone.",
    "{r['name']}（无法读取：{r['error']}）": "{r['name']} (unreadable: {r['error']})",
    '暂无回放。对局结束后在游戏内勾选「保存回放」，即可在此观看。': 'No replays yet. After a match, enable Save Replay in-game to watch it here.',
    '同一局域网（家中 Wi-Fi / 公司内网）即可对战，无需联网': 'Play on the same LAN (home Wi-Fi / office network) — no internet required',
    '收听游戏原声（AUD 音频封包于 MIX 内，需引擎解码播放）': 'Listen to the game soundtrack (AUD audio inside MIX packs; engine decodes playback)',
    '暂无存档。遭遇战对局中按 Esc → 保存游戏，即可在此继续。': 'No saves yet. In a skirmish, press Esc → Save Game, then continue here.',
    '已导入到：\n{dst}\n\n重启游戏后即可在地图列表中使用。': 'Imported to:\n{dst}\n\nRestart the game to use it in the map list.',
    "已把「{preset['name']}」数值填入编辑器，保存后生效": "Filled “{preset['name']}” values into the editor — save to apply",
    '暂无玩家地图。点「导入地图…」选择 .oramap 文件即可添加。': 'No player maps yet. Click Import Map… and choose a .oramap file.',
    '请输入房间地址，如 192.168.1.8 或 127.0.0.1': 'Enter a room address, e.g. 192.168.1.8 or 127.0.0.1',
    '读取 settings.yaml 的 Keys 段（用户自定义绑定）。': 'Read the Keys section of settings.yaml (user custom bindings).',
    '输入房主告诉你的 IP（如 192.168.1.8），端口默认 1234': 'Enter the host’s IP (e.g. 192.168.1.8); default port is 1234',
    '「启动器内功能」无需启动游戏即可使用；「引擎功能」将启动游戏并直达对应面板': 'Launcher Features work without starting the game; Engine Features launch the game and open the matching panel',
    '游戏内容资产（原版 MIX 封包）的安装状态；核心文件缺失时游戏将无法启动': 'Install status of game content assets (original MIX packs); missing core files prevent launch',
    "'K' / 'K Ctrl' -> ('K', ['Ctrl']) 规范形。": "'K' / 'K Ctrl' -> ('K', ['Ctrl']) canonical form.",
    '未找到 dotnet。请安装 .NET 6/8 Runtime 后重试。\n': 'dotnet not found. Install the .NET 6/8 Runtime and try again.\n',
    '浏览官方地图、导入或删除玩家地图；编辑地图请使用「更多功能 → 地图编辑器」': 'Browse official maps; import or delete player maps. To edit maps, use More → Map Editor',
    "该按键已绑定：{'、'.join(conflicts)}\n\n仍要绑定吗？": "This key is already bound: {'、'.join(conflicts)}\n\nBind anyway?",
    '读取游戏 settings.yaml 的简单两层结构，与默认值合并。': 'Read the game settings.yaml simple two-level structure and merge with defaults.',
    '载入或清理已保存的对局；「载入」将直接启动游戏并继续该存档，无需经过游戏内菜单': 'Load or clean up saved games; Load starts the game and continues that save without the in-game menu',
    '当前地图 {seats} 人席位（含你），最多可设 {max_ai} 个电脑对手': 'This map has {seats} seats (including you); up to {max_ai} AI opponents',
    '观看或清理已保存的比赛回放；「观看」将直接启动游戏进入该回放，无需经过游戏内菜单': 'Watch or clean up saved match replays; Watch launches the game into that replay without the in-game menu',
    '启动后把本机 IP 告诉朋友；你自己也用「加入房间」填 127.0.0.1 进入。': 'After starting, share your local IP with friends; join yourself via Join Room with 127.0.0.1.',
    '选好地图、规则与电脑对手后直接开局，跳过游戏内大厅；AI 人格来自「AI 配置」页': 'Pick map, rules, and AI opponents, then start — skips the in-game lobby; AI personalities come from AI Profiles',
    '点击「重绑定」后按下新的按键组合；Esc 取消，退格清除绑定。修改立即写入游戏配置。': 'Click Rebind, then press a new key combination; Esc cancels, Backspace clears. Changes write to game settings immediately.',
    "已生效（共 {n} 个人格）：开局在电脑槽位 AI 下拉选择「{p['name']}」": "Applied ({n} personalities): in skirmish, pick “{p['name']}” from the AI dropdown",
    '把非默认绑定写回 settings.yaml 的 Keys 段（与引擎序列化格式一致）。': 'Write non-default bindings back to the Keys section of settings.yaml (engine serialization format).',
    'NUKE HOUR {self._engine_version()} · RA2': 'NUKE HOUR {self._engine_version()} · RA2',
    '调整电脑的经济、生产与进攻参数并保存为配置；开局时在电脑槽位的 AI 人格下拉框中按名字选用': 'Tune AI economy, production, and attack settings and save as a profile; pick it by name in the skirmish AI personality dropdown',
    "● 服务器运行中（PID {st['pid']}）· 端口 {st.get('port')} ": "● Server running (PID {st['pid']}) · Port {st.get('port')} ",
    "{'可连接' if ok else '暂时不可连接'}\n房间名：{st.get('name')}": "{'Connected' if ok else 'Temporarily unreachable'}\nRoom: {st.get('name')}",
    "目录：{st['dir']}\n共 {st['count']} 个文件 · {st['total_size']}": "Folder: {st['dir']}\n{st['count']} files · {st['total_size']}",
    "{p['name']}({'AI' if p['is_bot'] else '玩家'}·{p['faction']})": "{p['name']}({'AI' if p['is_bot'] else 'Player'}·{p['faction']})",
    "{r.get('start', '时间未知')} · {pv} · {ra2_files.fmt_size(r['size'])}": "{r.get('start', 'Unknown time')} · {pv} · {ra2_files.fmt_size(r['size'])}",
    '界面语言': 'Language',
    '语言（重启后生效）': 'Language (applies after restart)',

    '切换后启动器立即刷新；游戏需重新启动后生效': 'Launcher refreshes immediately; restart the game to apply in-game',
    'NUKE HOUR {ver} · RA2': 'NUKE HOUR {ver} · RA2',

    '凑齐几人就进攻': 'Attack after N units',
    '出兵人数浮动': 'Attack size variance',
    '越小越早进攻；拉满会囤兵很久才冲': 'Lower = earlier attacks; maxing hoards forever',
    '越大越晚凑齐部队；狂暴建议 0~5': 'Higher delays attacks; rush should stay 0–5',
    '大于 1 会优先造基地车扩张，拖慢进攻': '>1 expands bases first and delays attacks',
    '提示：狂暴要早进攻，请把「凑齐几人就进攻」保持在 2~4，不要拉满': 'Tip: For early rush, keep “Attack after N units” at 2–4 — do not max it',
    '已生效（共 {n} 个人格）：开局在电脑槽位 AI 下拉选择「{name}」': 'Applied ({n} profiles). Pick “{name}” in the skirmish AI slot',
    '（当前偏龟缩：小队过大或分矿过多，会很晚才进攻）': '(Turtle-like: squad/MCV too high — attacks will be late)',

    # --- Hover tips (settings / AI / nav) ---
    '进入开始页：遭遇战、联机、载入存档，或直接进游戏主菜单。':
        'Open the start page: skirmish, multiplayer, load a save, or enter the main menu.',
    '画面、语言、玩家名称与战场界面显示相关选项。':
        'Display, language, player name, and battlefield UI options.',
    '音效、音乐、视频音量，以及资金提示音等音频选项。':
        'Sound, music, video volume, and cash-tick audio options.',
    '鼠标操作模式（经典/新模式）、地图滚动与缩放手感。':
        'Mouse control scheme (classic/modern), map scrolling, and zoom feel.',
    '自定义游戏内快捷键；修改后立即写入配置文件。':
        'Customize in-game hotkeys; changes write to the config immediately.',
    '调整电脑对手的经济、生产与进攻参数，并保存为可选人格。':
        'Tune AI economy, production, and attack settings, and save as selectable personalities.',
    '回放、存档、地图、素材浏览器等附加功能入口。':
        'Entry points for replays, saves, maps, asset browser, and other extras.',
    '关闭启动器。若游戏正在运行，不会强制退出游戏进程。':
        'Close the launcher. Running games are not force-quit.',
    '启动器界面语言；切换后立即刷新。游戏内语言需重新启动游戏后生效。':
        'Launcher UI language; refreshes immediately. Restart the game for in-game language.',
    '多人联机与遭遇战中显示的指挥官名称。':
        'Commander name shown in multiplayer and skirmish.',
    '你的玩家颜色。用于单位染色与小地图标识（部分地图可能重分配）。':
        'Your player color for unit tinting and the minimap (some maps may reassign colors).',
    '无边框全屏：像全屏但切换窗口更方便（推荐）。窗口化：可自由缩放。经典全屏：独占显示器。':
        'Borderless fullscreen: fullscreen feel with easier alt-tab (recommended). Windowed: resizable. Classic fullscreen: exclusive display.',
    '窗口化模式下的分辨率，格式为「宽,高」。可用右侧常用尺寸快速填入。':
        'Windowed resolution as width,height. Use the presets on the right to fill quickly.',
    '游戏界面整体放大比例。高分屏可选 125%/150%；「自动」按屏幕估算。':
        'Overall UI scale. Try 125%/150% on HiDPI; Auto estimates from the screen.',
    '游戏光标是否按双倍尺寸绘制，高 DPI 屏幕上更易看清。':
        'Draw the game cursor at double size — easier to see on HiDPI screens.',
    '图形后端。一般保持「自动」。画面异常时可试 ANGLE 或现代 OpenGL。':
        'Graphics backend. Keep Automatic unless you hit glitches — then try ANGLE or Modern OpenGL.',
    '多显示器时选择游戏显示在哪一块屏幕上。':
        'Which monitor shows the game when you have multiple displays.',
    '默认镜头远近。越远看见的战场越大，单位显得更小。':
        'Default camera distance. Farther shows more map; units look smaller.',
    '选中单位后显示攻击/移动目标连线。可禁用、按快捷键显示，或始终自动显示。':
        'Lines from selected units to attack/move targets. Off, hotkey-only, or always on.',
    '单位头顶血条/状态条：标准、仅受伤时显示，或始终显示。':
        'Health/status bars above units: standard, only when damaged, or always.',
    '单位头顶血条/状态条：标准、仅受伤时显示，或始终显示。游戏中也可按住 Alt 临时显示全部单位血条。':
        'Health/status bars above units: standard, damaged-only, or always. Hold Alt in-game to temporarily show all unit health bars.',
    '垂直同步：画面刷新与显示器同步，可减少撕裂，偶发增加输入延迟。':
        'VSync syncs frames to the display — less tearing, sometimes more input lag.',
    '把画面帧率限制为游戏逻辑帧。更省电、更冷静，但可能感觉不够丝滑。':
        'Cap render FPS to the game sim rate — saves power, may feel less smooth.',
    '改用软件绘制光标。系统光标有残影、闪烁或错位时可开启。':
        'Use a software cursor. Try this if the system cursor trails, flickers, or misaligns.',
    '开启后，敌方偏红、友方偏黄等关系色，便于识别敌我（不再仅用玩家本色）。':
        'Tint enemies reddish and allies yellowish by stance instead of raw player colors.',
    '屏幕上显示指令反馈与快捷聊天等界面提示文字。':
        'Show on-screen order feedback and quick-chat UI messages.',
    '显示「探测到敌军」「建筑丢失」等战场事件提示。':
        'Show battlefield event toasts such as enemy spotted or building lost.',
    '暂停菜单后是否继续播放主菜单背景演示地图动画。':
        'Keep playing the menu background map animation while paused.',
    '观看回放时隐藏聊天内容，界面更干净。':
        'Hide chat while watching replays for a cleaner view.',
    '一键静音全部游戏声音（音效与音乐）。':
        'Mute all game audio (sound and music).',
    '单位语音、爆炸、点击等音效的音量。':
        'Volume for unit speech, explosions, clicks, and other SFX.',
    '背景音乐音量。可与音效分开调节。':
        'Background music volume, separate from sound effects.',
    '过场/宣传片等视频的音量。':
        'Volume for cutscenes and promotional videos.',
    '资金增减时播放点钞提示音（经典 RA 手感）。':
        'Play the classic cash-tick sound when money changes.',
    '音乐列表随机播放，而不是按固定顺序。':
        'Shuffle the music playlist instead of fixed order.',
    '音乐列表播完后从头循环。':
        'Loop the music playlist when it finishes.',
    '新模式：左键选择，右键移动/攻击（类似现代 RTS）。经典：左键选择并下令，右键取消指令。':
        'Modern: left-select, right-move/attack. Classic: left selects and orders, right cancels.',
    '新模式：左键选择，右键移动/攻击。经典：左键选择并下令，右键取消；按住右键滑动可快速甩视角（原版手感）。':
        'Modern: left select, right move/attack. Classic: left select/order, right cancel; hold right and swipe to pan (original feel).',
    '按住中键/右键拖地图时的方向手感。摇杆式最接近原版；也可标准、反向或禁用。':
        'Feel when dragging the map with middle/right button. Joystick is closest to classic RA.',
    '按住滚动键平移地图的方式。经典模式建议「标准」：按住右键地图跟随鼠标快速移动；摇杆式是泰星点按方向持续滑屏。':
        'How holding the scroll button pans the map. Classic prefers Standard (map follows cursor); Joystick is TS-style directional scroll.',
    '使用备用按键（通常中键或右键）拖拽平移地图，与操作模式配合。':
        'Use the alternate button (usually middle or right) to drag-pan the map.',
    '鼠标移到屏幕边缘时自动卷动地图。关闭后需用拖拽或方向键。':
        'Auto-scroll the map at screen edges. If off, use drag or arrow keys.',
    '将鼠标限制在游戏窗口内，避免点到桌面（多屏或全屏时有用）。':
        'Lock the cursor inside the game window so you don’t click the desktop.',
    '屏幕边缘滚动或边缘平移时的地图移动速度。':
        'How fast the map pans when edge-scrolling.',
    '生产列表、聊天等界面滚轮/滚动条的速度。':
        'Scroll speed for production lists, chat, and other UI panels.',
    '镜头缩放快慢（滚轮缩放时）。':
        'How fast the camera zooms with the mouse wheel.',
    '滚轮缩放地图时需要按住的修饰键。选「无」则直接滚轮缩放。':
        'Modifier key required for wheel-zoom. None = zoom with the wheel alone.',
    '开房联机时尝试通过 NAT-PMP/UPnP 在路由器上自动映射端口，方便好友连入。':
        'When hosting, try NAT-PMP/UPnP so friends can connect through your router.',
    '游戏内叠加性能曲线图，用于排查掉帧与卡顿。':
        'Overlay a performance graph in-game to diagnose hitching.',
    '游戏内显示帧时、更新耗时等性能文字。':
        'Show frame time and update cost text in-game.',
    '把模拟性能数据写入 perf.log，仅在排查卡顿时需要。':
        'Write simulation perf data to perf.log — only needed when debugging stutter.',
    '在游戏内设置中显示隐藏的开发者选项页。':
        'Show the hidden Developer options page in in-game settings.',
    '在地图上叠加 AI 决策调试信息（开发/调参用）。':
        'Overlay AI decision debug info on the map (for tuning/dev).',
    '输出地图 Lua 脚本的调试日志。':
        'Emit debug logs from map Lua scripts.',
    '允许在回放中使用调试作弊指令。':
        'Allow debug cheat commands while watching replays.',
    '关闭 OpenGL 调试回调，偶发驱动报错或性能问题时可试。':
        'Disable OpenGL debug callbacks — try if drivers spam errors or hurt performance.',
    '本局使用的地图。括号内为人头上限；开局后跳过游戏内大厅。':
        'Map for this match. Player cap is in parentheses; starts without the in-game lobby.',
    '所有玩家开局现金。越高发育越快，对局节奏也越快。':
        'Starting cash for all players. Higher = faster boom and pace.',
    '游戏逻辑速度。越快单位移动、建造都更快。':
        'Simulation speed. Faster means quicker movement and building.',
    '可解锁的科技上限。「仅步兵」禁用高级载具；「无限制」可造全部单位。':
        'Tech cap. Infantry Only blocks advanced vehicles; Unrestricted unlocks everything.',
    '你的开局阵营。随机/随机盟军/随机苏军会在开局时抽取。':
        'Your starting faction. Random / Random Allies / Random Soviets are rolled at start.',
    '电脑对手数量，不能超过地图空余席位。每个人格可在下方单独选择。':
        'Number of AI opponents (limited by free map seats). Pick each personality below.',
    '开启后，消灭所有敌方单位与建筑即可获胜，无需占领或达成其他条件。':
        'Win by destroying all enemy units and buildings — no other victory conditions.',
    '从常见分辨率列表快速填入窗口尺寸。':
        'Quick-fill window size from common resolutions.',
    '创建一份空白 AI 配置，调整下方数值后点「保存配置并生效」。':
        'Create a blank AI profile; tune sliders, then Save & Apply.',
    '复制当前配置为新人格，便于在狂暴基础上微调。':
        'Duplicate the current profile to tweak from a Rush base.',
    '删除当前自定义配置（出厂预设不可删）。':
        'Delete this custom profile (built-ins cannot be removed).',
    '把当前数值写入配置文件并重新生成游戏内 AI 人格，遭遇战下拉即可选用。':
        'Write values to disk and regenerate in-game AI personalities for the skirmish dropdown.',
    '把内置人格（如狂暴 Rush 等）恢复为出厂默认数值并重新生成。':
        'Restore built-in personalities (e.g. Rush) to factory defaults and regenerate.',
    '选择一份 AI 人格进行编辑。★ 为出厂预设；保存后可在遭遇战电脑槽位中选用。':
        'Select an AI personality to edit. ★ = built-in; after saving, pick it in skirmish AI slots.',
    '此名称会出现在遭遇战电脑槽位的 AI 人格下拉列表中。':
        'This name appears in the skirmish AI personality dropdown.',
    '把该风格模板的推荐数值填入下方滑条；还需点「保存配置并生效」才会写入游戏。':
        'Fill the sliders with this style template; click Save & Apply to write into the game.',
    '把所有热键恢复为游戏默认绑定，并立即写入配置。':
        'Reset all hotkeys to game defaults and write the config immediately.',
    '点击后按下新的按键组合。Esc 取消，退格清除绑定。':
        'Click, then press a new key combo. Esc cancels; Backspace clears.',
    '将此热键恢复为游戏默认绑定。':
        'Restore this hotkey to the game default.',
    '启动游戏并直接打开引擎内置的完整设置界面。':
        'Launch the game and open the engine’s full settings panel.',
    '选好地图、规则与电脑对手后直接开局，跳过游戏内大厅。':
        'Pick map, rules, and AI, then start — skips the in-game lobby.',
    '在同一局域网内创建或加入房间，与朋友对战。':
        'Host or join a LAN room to play with friends.',
    '继续之前保存的遭遇战或战役进度。':
        'Continue a previously saved skirmish or campaign.',
    '启动游戏并进入引擎原版主菜单（可再从游戏内开局）。':
        'Launch into the engine main menu (start matches from there).',
    '使用上方地图、规则与 AI 设置直接开局。':
        'Start immediately with the map, rules, and AI settings above.',
    '为这名电脑对手选择人格、阵营与队伍。人格来自「AI 配置」页。':
        'Pick personality, faction, and team for this AI. Personalities come from AI Profiles.',
    'AI 人格：决定经济、造兵与进攻风格（在「AI 配置」页编辑）。':
        'AI personality: economy, production, and attack style (edit under AI Profiles).',
    '这名电脑对手开局使用的阵营。':
        'Starting faction for this AI opponent.',
    '队伍编号相同则为友军；「无」表示各自为战。':
        'Same team numbers are allies; None means free-for-all.',

    '凑齐这么多单位后才会发动进攻。越小越早冲；狂暴建议 3~5（太小容易被打散）。':
        'Attack only after gathering this many units. Lower = earlier rushes; Rush tip 3–5.',
    '实际出兵人数在「凑齐人数」之上再随机浮动。越大越晚凑齐；狂暴建议 2~5。':
        'Random extra units above the squad size. Higher delays attacks; Rush tip 2–5.',
    '每隔多久尝试一次 Rush 进攻。越小越频繁骚扰；过大则长时间龟缩。':
        'How often to attempt a Rush. Lower = more harassment; high values turtle longer.',
    '两波成队进攻之间的最短间隔，防止刚打完又立刻再冲。':
        'Minimum gap between formed attack waves so they don’t re-rush instantly.',
    '多久重新整理一次小队分工（攻击/护家等）。越小反应越快，也更吃 CPU。':
        'How often to reassign squad roles. Lower reacts faster but costs more CPU.',
    '多久检查一次是否可以集结部队出击。越小越勤快地找机会进攻。':
        'How often to check whether a strike force can launch.',
    'Rush 单位搜索敌人的格数半径。越大越容易绕去偷家，也可能跑偏。':
        'Cell radius Rush units search for enemies. Larger may flank or wander.',
    '空闲单位主动寻敌的半径。越大越爱追打，过大会分散阵型。':
        'Idle units’ hunt radius. Larger chases more; too large breaks formation.',
    '进攻状态下寻找目标的半径。影响推进与集火距离。':
        'Target search radius while attacking — push and focus distance.',
    '空军遇到防空威胁时的避险半径。越小越敢偷家；过大则遇防空就跑。':
        'Air danger radius. Lower = braver raids; higher flees AA sooner.',
    '基地附近发现敌军时，护家部队的反应半径。':
        'How far base guards react when enemies appear near the base.',
    '建筑队列连续下单的间隔。越小造建筑越快（狂暴要低）。':
        'Delay between building queue orders. Lower builds faster (Rush wants low).',
    '暂时没钱/没事可造时，多久再检查一次建造。':
        'When idle/broke, how often to re-check the build queue.',
    '多久做一次造兵决策。越小填队列越快，暴兵更猛。':
        'How often to decide unit production. Lower fills queues faster.',
    '现金低于此值时暂停非紧急建造，避免破产。过低可能造到没钱。':
        'Pause non-urgent builds below this cash to avoid bankruptcy.',
    '最多建造几座矿石精炼厂。越多经济越强，但占建筑时间。':
        'Max ore refineries. More economy, more build time spent.',
    '矿车数量上限。过少采不动，过多浪费车厂产能。':
        'Harvester cap. Too few starve eco; too many waste war-factory time.',
    '开局至少要有几座矿场才开始大规模造兵。':
        'Minimum refineries before heavy unit production.',
    '兵营建成后再强制补几座矿场。设为 0 可立刻造兵（狂暴推荐）。':
        'Extra refineries forced after barracks. 0 = train units immediately (Rush).',
    '兵营数量上限。越多步兵产能越高。':
        'Barracks cap — more means higher infantry output.',
    '战争工厂数量上限。决定坦克/载具产能。':
        'War factory cap — vehicle/tank output.',
    '机场数量上限。越多可同时挂更多战机（夜鹰从车厂出，不占此限）。':
        'Airpad cap for parked aircraft (Night Hawk comes from war factory).',
    '现金达到此值才考虑再建额外兵营/车厂。越低越早扩建。':
        'Cash needed before extra barracks/factories. Lower expands earlier.',
    '计划用几座基地扩张。大于 1 会优先造基地车分矿，拖慢进攻。':
        'Planned MCV expansions. >1 prioritizes expand and delays attacks.',
    '工程师尝试占领中立建筑/油井的间隔。':
        'How often engineers try to capture neutrals/oil.',
    '发动核弹等超级武器的意愿阈值。越低越早扔。':
        'Superweapon (nuke) attractiveness threshold. Lower fires earlier.',
    '尤里': 'Yuri',
    '小键盘': 'Numpad',
    ' · 启动中枢': ' · Launcher',
    '该按键已绑定：': 'This key is already bound: ',
    '\n\n仍要绑定吗？': '\n\nBind anyway?',
    '为「': 'Press a new key combination for “',
    '」按下新的按键组合': '”',
    '已删除「': 'Deleted “',
    '」并重新生成游戏人格': '” and regenerated game personalities',
    '已把「': 'Filled “',
    '」数值填入编辑器，保存后生效': '” values into the editor — save to apply',
    '已生效（共 ': 'Applied (',
    ' 个人格）：开局在电脑槽位 AI 下拉选择「': ' profiles): pick “',
    '读取回放失败：': 'Failed to read replay: ',
    '（无法读取：': ' (unreadable: ',
    '玩家': 'Player',
    '时间未知': 'Unknown time',
    '读取存档失败：': 'Failed to read save: ',
    '确定删除「': 'Delete “',
    '」吗？此操作不可恢复。': '”? This cannot be undone.',
    '读取地图失败：': 'Failed to read map: ',
    ' 玩家': ' players',
    '作者 ': 'Author ',
    '官方地图（': 'Official maps (',
    ' 张，只读）': ', read-only)',
    '玩家地图（': 'Player maps (',
    ' 张）': ')',
    '已导入到：\n': 'Imported to:\n',
    '\n\n重启游戏后即可在地图列表中使用。': '\n\nRestart the game to use it in the map list.',
    '全部': 'all',
    '缺失核心文件：': 'Missing core files: ',
    '目录：': 'Folder: ',
    '\n共 ': '\n',
    ' 个文件 · ': ' files · ',
    ' 人）': ' players)',
    '● 服务器运行中（PID ': '● Server running (PID ',
    '）· 端口 ': ') · Port ',
    '可连接': 'Connected',
    '暂时不可连接': 'Temporarily unreachable',
    '\n房间名：': '\nRoom: ',
    '启动失败：': 'Launch failed: ',
    '检测到游戏已经在运行。\n\n选「是」将强制退出旧进程并重新启动；\n选「否」则切到已有游戏窗口。':
        'A game is already running.\n\nChoose Yes to force-quit it and restart;\n'
        'choose No to switch to its window.',
    '未找到 dotnet。请安装 .NET 6/8 Runtime 后重试。\n常见路径：/usr/local/share/dotnet':
        'dotnet not found. Install the .NET 6/8 Runtime and try again.\n'
        'Common path: /usr/local/share/dotnet',
    '未找到游戏启动脚本：\n': 'Game launch script not found:\n',
    '龟缩防御': 'Turtle Defense',
    '均衡标准': 'Balanced Standard',
    '狂暴 Rush': 'Rush',
    '简单·龟缩': 'Easy · Turtle',
    '普通·均衡': 'Normal · Balanced',
    '进阶·狂暴': 'Advanced · Rush',
    '困难·压迫': 'Hard · Pressure',
    '精英·碾压': 'Elite · Steamroll',
    '地狱·狂潮': 'Hell · Onslaught',
    '【Red Alert 2 模组】': '【Red Alert 2 Mod】',
    '【OpenRA 引擎】': '【OpenRA Engine】',
    '未找到制作名单文件': 'Credits files were not found',
    '未知地图': 'Unknown map',
    '未知': 'Unknown',
    '未找到元数据': 'Metadata not found',
    '长度前缀越界': 'Length prefix is out of bounds',
    '文件过小': 'File is too small',
    '无效的 orasav 结尾标记': 'Invalid orasav end marker',
    '无效的元数据标记': 'Invalid metadata marker',
    '缺少 map.yaml': 'map.yaml is missing',
    '只允许删除用户目录下的文件': 'Only files in the user data folder can be deleted',
    '{minutes} 分 {seconds} 秒': '{minutes} min {seconds} sec',
    '该按键已绑定：{conflicts}\n\n仍要绑定吗？':
        'This key is already bound: {conflicts}\n\nBind anyway?',
    '已删除「{name}」并重新生成游戏人格':
        'Deleted “{name}” and regenerated game personalities',
    '已把「{name}」数值填入编辑器，保存后生效':
        'Filled “{name}” values into the editor — save to apply',
    '确定删除「{name}」吗？此操作不可恢复。':
        'Delete “{name}”? This cannot be undone.',
    '状态：{status}': 'Status: {status}',
    '目录：{directory}\n共 {count} 个文件 · {total_size}':
        'Folder: {directory}\n{count} files · {total_size}',
    '● 服务器运行中（PID {pid}）· 端口 {port} {connection}\n房间名：{name}':
        '● Server running (PID {pid}) · Port {port} {connection}\nRoom: {name}',
    '未找到游戏启动脚本：\n{game_sh}': 'Game launch script not found:\n{game_sh}',
    '{name}（无法读取：{error}）': '{name} (unreadable: {error})',
    '{players} 玩家': '{players} players',
    '作者 {author}': 'Author {author}',
    '官方地图（{count} 张，只读）': 'Official maps ({count}, read-only)',
    '玩家地图（{count} 张）': 'Player maps ({count})',
    '缺失核心文件：{files}': 'Missing core files: {files}',
    '缺少核心文件：{files}': 'Missing core files: {files}',
    '还需要：{files}': 'Still required: {files}',
    '请先导入你合法持有的游戏文件：{files}':
        'First import the legally owned game files: {files}',
    '从你合法持有的游戏目录、光盘或 MIX 文件离线导入；文件不会上传':
        'Import offline from your legally owned game folder, disc, or MIX files; nothing is uploaded',
    '{name}（{players} 人）': '{name} ({players} players)',
    '本机 IP：{addresses}': 'Local IP: {addresses}',
}


def _normalize_lang(code: str | None) -> str | None:
    if not code:
        return None
    s = str(code).strip().strip('"').strip("'")
    if not s:
        return None
    # Preserve the two legacy words, but validate all language tags before
    # interpreting their primary subtag.
    low = s.lower()
    if low == "chinese":
        return "zh-CN"
    if low == "english":
        return "en"
    tag = _clean_language_tag(s)
    if tag is None:
        return None
    primary = tag.split("-", 1)[0].lower()
    if primary == "zh":
        return "zh-CN"
    if primary == "en":
        return "en"
    return None


def _clean_language_tag(value: str | None) -> str | None:
    if not value:
        return None
    value = str(value).strip().strip('"').strip("'")
    value = value.split(".", 1)[0].split("@", 1)[0].replace("_", "-")
    if not value or value.upper() in {"C", "POSIX"}:
        return None
    if not re.fullmatch(r"[A-Za-z]{2,8}(?:-[A-Za-z0-9]{2,8})*", value):
        return None
    parts = value.split("-")
    normalized = [parts[0].lower()]
    for part in parts[1:]:
        if len(part) == 4 and part.isalpha():
            normalized.append(part.title())
        elif len(part) in (2, 3) and part.isalpha():
            normalized.append(part.upper())
        else:
            normalized.append(part)
    return "-".join(normalized)


def normalize_preference(value: str | None) -> str:
    if str(value or "").strip().lower() == "system":
        return "System"
    normalized = _normalize_lang(value)
    return normalized if normalized in {"zh-CN", "en"} else "System"


def _first_language_from_defaults_output(output: str) -> str | None:
    for match in re.finditer(r'"([^"]+)"', output):
        tag = _clean_language_tag(match.group(1))
        if tag:
            return tag
    for part in re.split(r"[\s,()]+", output):
        tag = _clean_language_tag(part)
        if tag:
            return tag
    return None


def _from_macos_defaults() -> str | None:
    # AppleLanguages is the user's UI-language preference and must win over
    # the regional-format AppleLocale value when they disagree.
    for key in ("AppleLanguages", "AppleLocale"):
        try:
            out = subprocess.check_output(
                ["defaults", "read", "-g", key],
                stderr=subprocess.DEVNULL,
                text=True,
                timeout=2,
            )
        except Exception:
            continue
        got = (
            _first_language_from_defaults_output(out)
            if key == "AppleLanguages"
            else _clean_language_tag(out)
        )
        if got:
            return got
    return None


def _from_env() -> str | None:
    for var in ("LC_ALL", "LC_MESSAGES", "LANG"):
        raw = os.environ.get(var) or ""
        got = _clean_language_tag(raw)
        if got:
            return got
    return None


def _from_locale_module() -> str | None:
    try:
        lang, _ = locale.getlocale()
        got = _clean_language_tag(lang)
        if got:
            return got
    except Exception:
        pass
    try:
        lang, _ = locale.getdefaultlocale()
        got = _clean_language_tag(lang)
        if got:
            return got
    except Exception:
        pass
    return None


def detect_system_language_tag() -> str:
    """Return the best raw system language tag; never raise."""
    try:
        for getter in (_from_macos_defaults, _from_env, _from_locale_module):
            try:
                got = getter()
            except Exception:
                got = None
            if got:
                return got
    except Exception:
        pass
    return "en-US"


def resolve_language(preference: str | None, system_tag: str | None) -> str:
    normalized = normalize_preference(preference)
    if normalized != "System":
        return normalized
    return _normalize_lang(system_tag) or "en"


def detect_system_language() -> str:
    return resolve_language("System", detect_system_language_tag())


_lang = detect_system_language()


def get_language() -> str:
    return _lang


def set_language(code: str) -> None:
    global _lang
    got = _normalize_lang(code)
    if got:
        _lang = got
    elif code in ("zh-CN", "en"):
        _lang = code


def is_chinese() -> bool:
    return str(get_language()).lower().startswith("zh")


def translate(text: str, effective_language: str | None = None) -> str:
    """Translate UI text. zh-CN returns as-is; en uses EN lookup with fallback."""
    language = get_language() if effective_language is None else effective_language
    if str(language).lower().startswith("zh"):
        return text
    return EN.get(text, text)


def format_text(text: str, effective_language: str | None = None, **values) -> str:
    return translate(text, effective_language).format(**values)


def t(text: str) -> str:
    return translate(text)
