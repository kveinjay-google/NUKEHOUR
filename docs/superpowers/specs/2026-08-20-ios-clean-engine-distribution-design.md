# iOS 纯引擎公开发行设计

## 目标

将当前 iOS 版本拆分为“可公开分发的开源运行框架”和“玩家自己合法持有的原版游戏数据”。公开 IPA 不包含 EA 官方地图、模型、贴图、动画、音乐、语音、音效或影片；首次启动由玩家通过 iOS 系统文件选择器导入《Red Alert 2》和可选的《Yuri's Revenge》正版数据文件。

本设计只处理数据资源导入。公开版不得下载或加载托管程序集、原生库或其他可执行代码。

## 当前状态与缺口

项目已经使用 `ContentInstallerFileSystem`，并把外部内容挂载点定义为 `^SupportDir|Content/ra2`。`mods/ra2-content` 也已经列出 RA2 光盘、The First Decade 和 EA/Origin 安装来源。

当前 iOS 工程仍递归打包 `engine/mods/**/*` 和 `mods/ra2/**/*`。后者约 63 MB，包括内置地图、SHP/PNG 图像、菜单美术、字体、规则与序列。公开发行前必须逐文件确认来源和许可证。当前 iOS 包还没有显式打包根目录的 `mods/ra2-content`，因此全新设备在缺少内容时不能依赖现有安装器正常完成导入。

## 构建产品

### DevelopmentRetail

供本机与已连接 iPad 调试。可以从开发机的私有目录挂载合法购得的原版数据，但这些文件不得复制进 Git，也不得进入公开 Archive。

### PublicClean

公开发行候选。只包含：

- GPLv3 引擎与对应源代码入口；
- iOS 平台、触控、诊断与性能框架；
- RA2/YR 数据格式读取器和兼容规则；
- 权利来源明确的原创 UI、字体与空内容安装界面；
- `ra2-content` 导入清单、许可证和免责声明。

`PublicClean` 使用显式白名单，不使用“先全部打包再删除”的黑名单策略。构建完成后再对 `.app` 和 `.ipa` 做独立违禁资源扫描。

## 资源分类

每个随包文件必须登记到机器可读清单，至少包含路径、SHA-256、来源、作者/权利人、许可证、允许进入的构建模式和用途。

分类为：

1. `engine-source`：GPLv3 或兼容许可证的软件和数据定义；
2. `project-original`：项目原创美术、翻译、界面与音效，并有可验证来源；
3. `redistributable-third-party`：字体和库等可再分发内容，保存许可证文本；
4. `retail-user-content`：玩家必须自行提供，永远禁止进入公开包；
5. `unknown-or-derivative`：来源不明或可能由官方资源转换而来，默认禁止。

地图、SHP、VXL、HVA、AUD、MIX、BAG、IDX、官方影片及官方音乐一律从 `retail-user-content` 或 `unknown-or-derivative` 起步，只有取得明确再分发依据后才能变更分类。

## 首次启动与导入流程

1. 启动壳检查 `Application Support/OpenRA/Content/ra2`。
2. 如果缺少基础包，只显示内容导入、许可证、诊断和退出入口，不进入 RA2 主菜单。
3. 用户通过 `UIDocumentPickerViewController` 选择文件或安装目录。支持 iCloud Drive、Files、外接存储和 Finder 文件共享。
4. 导入器只接受清单允许的数据扩展名，并拒绝 DLL、dylib、framework、可执行 Mach-O、脚本程序集和未知压缩包内容。
5. 在临时目录复制并计算 SHA-256，验证必要文件和可识别版本。
6. RA2 基础数据是必需项；YR 数据是可选项，但选择尤里阵营前必须完整。
7. 所有校验通过后，以原子目录切换发布到 `Content/ra2`；失败时保留上一份可用内容。
8. 导入全过程离线完成，不上传、不分享、不自动从第三方网站下载官方资源。

## 地图与可扩展内容边界

公开包不内置官方地图。首版只加载玩家导入的零售地图和项目确认拥有再分发权的原创地图。为降低 App Store 2.5.2 风险，公开版不加载用户提供的 DLL、原生插件或任意 Mod 程序集；地图内的扩展规则和脚本在公开版默认禁用，直到单独完成安全与审核评估。

## 品牌与法律界面

公开包使用原创应用名、图标和宣传美术。可以用普通文字说明“兼容用户合法持有的 Red Alert 2 / Yuri's Revenge 数据文件”，但不得使用 EA/C&C 官方 Logo 或暗示官方认可。

应用、仓库和发行页面显著显示：

> EA has not endorsed and does not support this product.

公开分发必须同时提供对应版本的完整源代码、GPLv3 文本、修改声明、构建说明和安装信息。App Store/TestFlight 发布前需单独复核 GPLv3 与 Apple 分发条款；该复核不影响直接免费侧载包的技术开发。

## 组件边界

- `PublicBundlePolicy`：判断资源是否允许进入指定构建模式；不负责 UI。
- `ContentManifest`：读取资源清单、计算哈希和版本状态；不负责复制文件。
- `IosRetailContentPicker`：封装系统文件选择器；只返回安全作用域 URL。
- `RetailContentImporter`：分阶段复制、校验和原子发布；不操作游戏状态。
- `ContentReadiness`：向启动流程报告 Base/YR/Audio/Maps 的可用性。
- `PublicBundleAudit`：扫描产物并生成 JSON/Markdown 证据；发现违禁文件时返回非零状态。

## 错误处理

- 空间不足、文件不完整、版本不支持和安全作用域失效均提供中文可恢复提示。
- 取消导入不修改现有内容。
- 中途崩溃只留下可清理的 staging 目录。
- 同名文件必须经过哈希确认，不静默覆盖。
- 日志只记录文件类别、大小、哈希和错误，不记录用户云盘路径或账户信息。

## 验收标准

1. `PublicClean` IPA 解包后不含零售资源和来源不明素材。
2. 全新安装在没有内容时稳定进入中文导入向导。
3. 从正版 RA2 文件导入后可离线进入盟军/苏军遭遇战。
4. 再导入正版 YR 文件后可进入尤里遭遇战。
5. 导入取消、空间不足、文件损坏和重复导入均不会破坏上一份内容。
6. 导入前后的应用代码签名保持不变，导入目录中没有可执行代码。
7. DevelopmentRetail 保持现有真机开发效率，但公开 Archive 无法意外启用私有内容。
8. 每次公开构建生成资源 SBOM、违禁扩展扫描和 IPA 哈希报告。

## 分阶段交付

1. 资源审计与 PublicClean 构建白名单；
2. 无内容启动门和 iOS 文件选择器；
3. RA2/YR 校验、事务导入与恢复；
4. 品牌、许可证、免责声明与源码分发材料；
5. 全新 iPad 端到端验收和公开 IPA 审计。

## 参考依据

- OpenRA 官方说明：https://www.openra.net/download/
- EA C&C Modding Guidelines：https://www.ea.com/games/command-and-conquer/news/modding-faq
- Apple App Review Guidelines：https://developer.apple.com/app-store/review/guidelines/

