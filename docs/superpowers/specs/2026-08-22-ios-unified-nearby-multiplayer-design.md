# iOS 统一“附近联机”设计

## 目标

在同一个多人游戏浏览器中可靠支持三种本地连接路径：同一 Wi-Fi、个人热点、以及没有路由器时的 Apple 点对点 Wi-Fi。房间一经在本机发现，必须立即显示，不得等待互联网主服务器查询。两种发现机制同时看到同一房间时只显示一项，并优先选择普通局域网直连。

## 已确认的现有故障

当前 iPad mini 已通过 UDP 收到 iPad Air 的房间广告，但 iOS 启动参数遗漏 `Engine.LaunchPath`，导致当前 RA2 Mod 没有注册进 `ExternalMods`。`GameServer` 因而把相同 Mod、相同版本的房间判为不兼容，默认过滤器随后将其隐藏。另一个独立问题是 `ServerListLogic` 把 LAN 合并放在互联网主服务器 HTTP 请求之后；查询进行中收到的 LAN 刷新会被丢弃。

## 用户体验

- 多人游戏页面继续使用一个房间列表，不增加彼此割裂的入口。
- 本地房间显示连接标签：`局域网`、`个人热点`或`附近直连`。
- 同一房间被 UDP 和 Bonjour 同时发现时合并为一行，普通 LAN 可用时优先 LAN。
- 没有互联网时，本地结果仍在下一次游戏 UI tick 内出现。
- 直接地址连接保留为故障保底。
- 点选附近直连房间后，系统立即停止浏览并建立连接，避免持续 AWDL 浏览影响游戏网络性能。

## 架构

### 1. 本地房间聚合器

新增纯托管的本地房间解析与合并层。输入为 UDP Beacon、Bonjour TXT 广告和缓存的互联网房间，输出为去重后的 `GameServer` 列表。聚合器保留房间的原始 Session GUID、传输种类和可选附近路由 ID；解析失败会写明来源和原因。

兼容性判断首先比较广告中的 Mod ID/Version 与当前正在运行的 Mod。完全一致时直接兼容，不再依赖持久化的外部 Mod 注册；外部 Mod 注册仍服务于桌面端切换 Mod 的原有场景。

### 2. 平台无关接口

`OpenRA.Game` 定义 `INearbyMultiplayer`、`NearbyGameAdvertisement` 和 `NearbyAdvertisementCodec`：

- 浏览附近服务并发布快照事件；
- 发布、更新、停止当前主机房间；
- 为一个附近路由建立本机回环连接目标；
- 暴露实际连接路径描述供日志和诊断使用。

非 iOS 平台保持空实现，不改变桌面行为。

### 3. iOS Network.framework 实现

iOS 使用 `_openra-ra2._tcp` Bonjour 服务以及 `NWParameters.IncludePeerToPeer = true`。`NWBrowser`、`NWListener` 和 `NWConnection` 同时覆盖基础 Wi-Fi、个人热点及 Apple 点对点 Wi-Fi。部署目标保持 iOS 15；不采用已经弃用的 Multipeer Connectivity。

房间 YAML 先压缩，再拆成不超过 220 字节的 TXT 分片，并附带版本、分片数和校验值。浏览端只有在分片完整且校验通过时才发布房间。

### 4. 保留 OpenRA 网络协议

不重写游戏同步协议：

- 主机继续在本机端口运行现有 OpenRA TCP Server；附近 `NWListener` 接受连接后，把字节流中继到 `127.0.0.1:<game-port>`。
- 客户端选择附近房间时启动一个短生命周期本机 `TcpListener`，返回 `127.0.0.1:<ephemeral-port>` 给现有 `NetworkConnection`；接受本机连接后，再通过对应 Bonjour endpoint 建立 `NWConnection` 并双向中继。

中继保持字节顺序、处理半关闭和取消，并使用有界缓冲与串行发送回调避免无界内存增长。

### 5. 生命周期

`MasterServerPinger` 在服务器启动、Lobby 变化、游戏开始/结束时同步更新附近广告；服务器关闭时停止广告。`ServerListLogic` 打开时开始浏览，关闭或加入房间时停止浏览。应用进入后台时暂停附近服务，回到前台后按当前 UI/服务器状态恢复。

## 错误处理与诊断

- Bonjour 权限、浏览状态、监听状态、TXT 解码、连接路径和中继关闭原因写入 client/server 日志。
- 本地房间不会因互联网请求失败而消失。
- P2P 建连失败会给出中文错误，并允许用户刷新或使用直接地址连接。
- 广告不包含聊天、IP、设备序列号或 Apple 账户信息。

## 验收

1. iPad Air 建房，iPad mini 在 3 秒内看到并加入；双方进入同一 Lobby。
2. 两台设备启动同一地图并运行至少 100 world ticks，无断线、异常或 desync。
3. 互联网主服务器不可达时，本地房间仍显示。
4. 同一房间被 UDP 和 Bonjour 同时发现时只显示一行。
5. 个人热点下完成同样的双机测试。
6. 无路由器且 Wi-Fi 功能保持开启时，通过点对点 Wi-Fi 完成同样测试，并在日志中记录 peer-to-peer 路径。

## 非目标

- 本阶段不实现公网中继、账号匹配或专用云服务器。
- 不替换 OpenRA 的确定性同步、Lobby、存档或回放协议。
- 不承诺在 Wi-Fi 功能被完全关闭时建立点对点连接。
