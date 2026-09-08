# 构建、签名、安装与设备测试

## iOS 工程组成

`ios/OpenRA.iOS/OpenRA.iOS.csproj` 面向 `net8.0-ios`，仅支持 iPad（device family 2），最低 iOS 15，Bundle ID 为 `com.openra.ipad.personal`。它引用：

- `engine/OpenRA.Game`
- `ios/OpenRA.Platforms.iOS`（程序集名仍为 `OpenRA.Platforms.Default`）
- `OpenRA.Mods.RA2`
- 静态 SDL2、SDL platform stubs、FreeType

BundleResource 会递归复制 `engine/glsl`、`engine/mods` 和项目 `mods/ra2`。关闭程序集裁剪，并为三套 Mod 程序集启用 Mono interpreter，以支持 OpenRA 大量反射构造。

iOS 产品策略隐藏地图编辑器和素材浏览器入口；共享引擎、桌面 Chrome 及规则中的 `MapEditorData` 仍保留，避免影响 macOS 开发工具和正常地图/Actor 数据加载。该策略是 iOS UI/产品排除，不是删除共享编辑器代码。

`OpenRA.Platforms.iOS` 复用桌面 Default 平台源码，但排除桌面 Platform 和 OpenAL 实现，加入 `IosPlatform` 与 SDL2-CS，并定义 `IOS`。iOS App 还主动剔除桌面 dylib，避免 install-name/link 阶段污染。

## 物理设备构建

当前已配对设备为 iPad Air 11-inch (M4)，设备状态可由 `xcrun devicectl list devices` 核对。Release 构建必须指定：

- `RuntimeIdentifier=ios-arm64`
- Apple Development signing identity
- 匹配 Bundle ID 与设备的 provisioning profile
- `DOTNET_ROOT` 和本机 .NET 8 路径

研究开始时的现有 App 曾通过 `codesign --verify --deep --strict`，是 arm64 Mach-O，Authority 为 Apple Development，TeamIdentifier 为 `267KQ4A3N4`。本次重新构建后，MSBuild 仍识别到相同开发证书和 provisioning profile，但严格验证返回 `CSSMERR_TP_NOT_TRUSTED`。这属于当前包的未通过门禁，安装前必须重新验证证书链/钥匙串信任，不能沿用旧包的验证结果。

## 安装与启动

1. 用 `xcrun devicectl device install app --device <UDID> <OpenRA.iOS.app>` 覆盖安装。
2. 用 `xcrun devicectl device process launch --device <UDID> --terminate-existing com.openra.ipad.personal` 确保旧进程不继续运行。
3. 需要实时日志时加 `--console`，但长时间测试更适合启动后单独抓取容器日志。
4. 安装成功只证明 Bundle 被接受；必须另行确认进程、画面、触摸和资源版本。

## 持久化边界

覆盖安装会保留 App container 的 Documents。当前宿主使用 `Documents/OpenRA`，其中包括：

- `settings.yaml`
- `Logs/`
- Content/RA2 原始资源
- 用户地图、回放和截图

因此覆盖安装不是“干净安装”。删除 App 才通常会清掉容器，但这是破坏性操作，不能在没有明确必要性和备份判断时使用。验证新版时应优先通过日志、manifest 来源和文件哈希证明加载内容，而不是先清数据。

## 构建产物核验

必须在安装前核对源码与 App bundle：

```text
shasum -a 256 engine/glsl/combined.frag \
  ios/OpenRA.iOS/bin/Release/net8.0-ios/ios-arm64/OpenRA.iOS.app/glsl/combined.frag
```

同理核对目标 DLL、规则 YAML、Chrome YAML 和 shader。研究过程中曾确认 `combined.frag` 源码与 App 一致，而 `world.yaml` 因撤回证伪修改与当时旧 App 不一致；随后重新完成了 Release 构建。这个过程说明：**工作树、构建目录和设备是三个独立状态，不能用其中一个代表另外两个。** 每次安装仍需重新记录哈希，不能沿用本次结果。

## 增量构建风险

- BundleResource 的递归 glob 不保证所有内容编辑都会触发预期复制；修改后应检查目标文件时间和哈希。
- 共享平台源码通过另一个 csproj 编译，条件符号和缓存可能与主 App 不同。
- 静态 native archive 修改需要确认实际 relink，而不是只看到 managed build 成功。
- 同 Bundle ID 覆盖安装保留 Documents 设置和内容。
- `--terminate-existing` 只能处理进程，不清理已持久化的配置。

## 物理 iPad 冒烟矩阵

| 场景 | 验证点 | 证据 |
|---|---|---|
| 启动 | 横屏、无 fatal、主菜单可操作 | console + 截图 |
| 新遭遇战 | 地图、迷雾、侧边栏正常 | screenshot + debug.log |
| 基地车 | 选中、移动、展开 | 订单日志 + 连续截图 |
| 触摸 | tap、drag、long press、双指 pan/pinch | 输入 breadcrumb |
| 设置 | 大按钮、滚动、返回、性能文本偏好保留 | 截图 + settings.yaml |
| 生命周期 | Home 后恢复，无残留按键/触点 | touch reset 日志 |
| 网格 | 可见地形和黑色迷雾分别对比 | 同地图 A/B 截图 |
| 稳定性 | 至少 15 分钟建造/战斗 | hang log + frame counters |

## 日志取证

常规 channel 日志与 `hang-diagnostic.log` 都在 Documents/OpenRA/Logs。一次有效卡死取证必须保留：

- 完整 Logs 目录，而非只复制 watchdog 文件
- 测试开始/卡死的墙钟时间
- 当时地图、阵营和操作
- 右下角 PerfText 的最后 Tick/Render 值（用户主动开启，可作为有用证据）
- 卡死截图
- 安装 App 的签名时间、目标 DLL/shader/rule 哈希

这样才能把 watchdog stage 与 client/server/debug/graphics 日志按时间关联。
