# OpenRA iPad Personal Edition：源码分析与技术路线

> Phase 1：源码分析 + 技术路线确认
>
> 分析对象：`ra2-mac`，分支 `engine-20250330`
>
> 目标：在不重写 OpenRA 游戏逻辑和 RA2 模组的前提下，先让游戏在 Mac 和 iPadOS 启动，再逐步加入触控和资源导入。

## 1. 结论

项目具备移植到 iPadOS 的基础，但不能把现有 macOS `.app` 或桌面启动器直接复制到 iPad。

推荐路线是：

1. 保留 `OpenRA.Game`、`OpenRA.Mods.Common`、`OpenRA.Mods.Cnc`、`OpenRA.Mods.RA2` 和 MiniYAML/Actor/Rules 体系。
2. 新增一个 iOS 宿主工程，负责 UIApplication 生命周期、资源目录、窗口创建和异常日志。
3. 在 `IPlatform` / `IPlatformWindow` / `IGraphicsContext` 接口之下增加 iOS 平台实现。
4. 首版优先复用 SDL2 的 iOS backend，并使用 OpenGL ES 兼容路径；只有在 SDL2-CS 或 native library 装载无法在 iOS AOT 下工作时，才替换窗口/图形后端。
5. 触控手势在平台输入层转换成现有 `MouseInput`，不重写 UI、订单系统或单位逻辑。
6. 资源加载沿用现有 MIX/SHP/VXL/INI 解析器；iPad 只新增沙盒内资源导入和持久化路径。

首个可验收目标不是“完整 iPad 版本”，而是：

> iPad 启动应用 → 创建游戏窗口/图形上下文 → 加载内置 RA2 mod → 显示主菜单或最小渲染画面 → 能安全退出/挂起恢复。

## 2. 当前工程结构

| 层 | 当前实现 | iPad 处理 |
|---|---|---|
| 游戏循环 | `engine/OpenRA.Game/Game.cs` 的 `InitializeAndRun`、`Loop`、`LogicTick`、`RenderTick` | 尽量复用；iOS 宿主负责生命周期和暂停/恢复 |
| 核心规则 | `engine/OpenRA.Game`、`OpenRA.Mods.Common` | 保留 |
| C&C/RA2 格式与行为 | `engine/OpenRA.Mods.Cnc`、`OpenRA.Mods.RA2`、仓库根部 `OpenRA.Mods.RA2` | 保留，不重写格式解析 |
| 平台抽象 | `engine/OpenRA.Game/Graphics/PlatformInterfaces.cs` | 作为 iOS 适配边界 |
| 桌面平台实现 | `engine/OpenRA.Platforms.Default` | 不破坏；新增 iOS 实现或 iOS 条件分支 |
| 启动器 | `engine/OpenRA.Launcher/Program.cs`、仓库根部 `launcher.py` | 不作为 iPad 入口；iPad 使用原生宿主 |
| 模组资源 | `mods/ra2` + `Platform.EngineDir`/`Platform.SupportDir` | 内置只读资源与沙盒可写资源分离 |

当前引擎版本由 `mod.config` 固定为 `release-20250330`。桌面构建仍以 `net6.0` 为主，`OpenRA.Platforms.Default` 依赖 `OpenRA-SDL2-CS`、`OpenRA-OpenAL-CS` 和 `OpenRA-Freetype6`。

## 3. 启动链路分析

现有启动链路如下：

```text
OpenRA.Launcher.Program.Main
  -> Game.InitializeAndRun
  -> Platform.CurrentPlatform / SupportDir / EngineDir
  -> Game.CreatePlatform("Default")
  -> 动态加载 OpenRA.Platforms.Default.dll
  -> Renderer + Sound
  -> InstalledMods / ModData
  -> Game.Loop
       -> LogicTick
       -> RenderTick
            -> Renderer.EndFrame
                 -> IPlatformWindow.PumpInput
```

其中 `Game.CreatePlatform` 使用 `AssemblyLoader` 动态装载平台 DLL。这个机制适合桌面，但 iOS 的 AOT、链接裁剪和应用包资源布局可能使运行时动态装载、`.deps.json` 探测和 native library 搜索失效。

因此 iOS 首版应采用以下二选一中的 A：

### A. 推荐：iOS 静态平台注册

- iOS 工程静态引用 `OpenRA.Game` 和 iOS 平台程序集。
- iOS 启动代码直接创建平台对象，或给 `Game` 增加可注入的 `IPlatform` 工厂。
- 保留桌面路径的动态加载逻辑。
- 避免依赖 iOS 上的 `Process.Start`、外部 DLL 扫描和运行时程序集解析。

### B. 备选：保留动态加载

仅在实际验证 iOS 的 .NET runtime 能稳定加载程序集、依赖清单和 native library 后采用。稳定性和裁剪可控性不如 A，不作为首版默认方案。

## 4. 图形系统分析

当前渲染接口已经足够形成适配边界：

- `IPlatform.CreateWindow` 创建窗口和图形上下文。
- `IPlatformWindow` 暴露尺寸、缩放、焦点、挂起、输入泵和光标。
- `IGraphicsContext` 暴露 buffer、texture、framebuffer、shader、混合、深度和 Present。
- `OpenRA.Platforms.Default/OpenGL.cs` 通过 SDL 获取 OpenGL 函数指针，并区分 `Modern` 与 `Embedded` profile。
- `Sdl2PlatformWindow` 创建 `SDL_WINDOW_OPENGL | SDL_WINDOW_ALLOW_HIGHDPI` 窗口。

这说明首版不需要重写 `Renderer` 和世界渲染器。风险集中在：

1. SDL2 是否以 iOS ARM64 方式编译并能被 .NET iOS 正确链接。
2. SDL2-CS 的 P/Invoke 名称和 native library 装载方式是否适合 iOS app bundle。
3. OpenGL ES profile 的 shader、纹理格式、framebuffer 和 BGRA 读回路径是否满足 iPad GPU。
4. iOS 应用挂起时，GL context 是否丢失；恢复后需要重新创建或重建 GPU 资源。
5. FreeType、OpenAL、Lua 等 native 依赖是否需要 iOS 原生库或替代实现。

推荐图形验证顺序：

1. 先只创建窗口和清屏，不加载 RA2。
2. 加载一个内置 shader、纹理和字体，显示测试画面。
3. 显示 OpenRA 主菜单。
4. 加载 RA2 地图并显示单位。

Metal 不应在 Phase 1 重写。若 SDL2 + GLES 在真实 iPad 上无法稳定运行，再单独评估 Metal 后端；此时仍实现 `IGraphicsContext`，而不是重做上层渲染。

## 5. 输入系统分析与触控路线

核心输入接口是 `IInputHandler`：

- `OnKeyInput(KeyInput)`
- `OnMouseInput(MouseInput)`
- `OnTextInput(string)`
- `ModifierKeys(Modifiers)`

桌面 `Sdl2Input` 当前只显式处理 SDL quit、window、mouse button/motion/wheel、text input 和 keyboard 事件，没有 iOS 手指手势映射。这个缺口应在平台层补齐。

建议的第一版映射：

| iPad 操作 | 输出到 OpenRA |
|---|---|
| 单指点击 | 左键 Down/Up |
| 单指拖动后松开 | 左键拖动，复用框选/拖拽 |
| 长按 | 右键 Down/Up，作为移动/取消的候选映射 |
| 双指平移 | 中键或当前视图支持的平移事件 |
| 双指捏合 | 转换为 Scroll，控制地图缩放 |
| 蓝牙鼠标 | 继续走 SDL mouse 事件 |
| 蓝牙键盘 | 继续走 SDL keyboard/text 事件 |

注意：`MouseInput` 的坐标是 `int2`，而 iPad 触摸坐标通常是逻辑点，渲染表面是 Retina 像素。iOS 平台实现必须统一 `NativeWindowScale`、`EffectiveWindowScale` 和触摸坐标，否则会出现点按位置偏移。

触控识别不应直接修改 `Ui.HandleInput` 或各个 Widget。平台层维护少量手势状态即可：触点数量、按下时间、起点、上次位置和当前手势类型。长按阈值、拖动阈值和双指冲突规则应写成可测试的纯逻辑。

## 6. 资源系统分析

OpenRA 已有通用文件系统和包加载器：

- `engine/OpenRA.Game/FileSystem/FileSystem.cs` 负责挂载和查找。
- `Folder`、`ZipFile` 支持目录和压缩包。
- `engine/OpenRA.Mods.Cnc/FileSystem/MixFile.cs` 支持 MIX。
- `engine/OpenRA.Mods.Cnc/Graphics/VoxelLoader.cs` 和 `VxlReader` 支持 VXL/HVA。
- C&C sprite loader 支持 SHP。
- INI/YAML/Fluent 等文本资源走现有文件系统。

因此 RA2 资源的正确路线是：

1. 将 mod 本身和引擎 shader/字体等只读资源放入 app bundle。
2. 将玩家导入的 RA2 MIX 文件复制到应用沙盒的 `Documents` 或 `Library/Application Support/OpenRA/Content/ra2/`。
3. 通过 iOS 文件选择器导入目录或文件，避免假设桌面绝对路径。
4. 让 `Platform.SupportDir` 指向沙盒可写目录，保存 settings、Saves、Replays、Logs。
5. 保持 `MixFile`、`VxlLoader`、`SHP` loader 和 mod YAML 不变。

当前 `Platform` 对 macOS/Linux/Windows 有硬编码路径和 `uname`/`system_profiler`/`hostnamectl` 探测。iOS 必须增加 `PlatformType.iOS` 或等价的 host override，禁止调用桌面命令，并由宿主注入 bundle/support/documents 路径。

## 7. iOS 特有阻塞点

这些问题必须在 Phase 2/3 用最小原型验证，不能仅凭桌面编译结果推断：

1. **运行时与 AOT**：当前目标框架是 `net6.0`，并使用反射、动态程序集加载和 `AssemblyLoadContext`。iOS 需要单独确认 .NET for iOS 版本和可用 API，并设计静态入口。
2. **native 依赖**：现有 bin 中有 `SDL2.dylib`、`freetype6.dylib`、`soft_oal.dylib`、`lua51.dylib`。这些桌面 dylib 不能直接进入 iOS；必须重新编译成 iOS ARM64 静态库或 framework，并由 Xcode/.NET iOS 工程链接。
3. **窗口生命周期**：桌面循环是持续 `Thread.Sleep` 的长循环。iOS 挂起/恢复时必须暂停逻辑和渲染，并处理图形上下文失效。
4. **文件访问**：iPad 不存在用户可任意访问的 home 目录；RA2 资源导入必须走 Files picker 或预置目录。
5. **网络/联机**：个人版首个启动目标不依赖联机。Socket/发现/后台服务应等单机稳定后再验证。
6. **动态外部 mod/launcher**：`Process.Start`、外部 mod 切换和桌面 launcher 不属于首版 iPad 启动路径，应禁用或改为 iOS 页面内导航。

## 8. 分阶段实施路线

### Phase 1：源码分析（本阶段）

- [x] 确认引擎、模组、平台和启动链路。
- [x] 确认图形、输入、资源和生命周期边界。
- [x] 确认 iPad 适配不需要重写游戏逻辑。
- [x] 形成本文档。
- [x] 执行现有构建和 mod YAML 测试。

### Phase 2：iOS 最小工程

- 建立 Xcode/.NET for iOS 宿主工程。
- ARM64 真机 build。
- 静态注册 iOS 平台入口。
- 只实现窗口、清屏、日志和退出/挂起恢复。

验收：iPad 安装并显示一个稳定的测试画面。

### Phase 3：OpenRA 启动

- 接入 `Game.Initialize` 所需的 iOS 参数和路径。
- 接入内置 mod 资源。
- 接入图形平台实现、字体和最小 shader。
- 处理 AOT/linker 保留项。

验收：iPad 显示 OpenRA/RA2 主菜单，可挂起恢复。

### Phase 4：触控

- 在 iOS/SDL 输入层增加触点事件。
- 加入单指点击、框选、长按、双指平移和捏合。
- 保留蓝牙鼠标键盘路径。

验收：主菜单操作、进入遭遇战、选择单位、移动视图和缩放。

### Phase 5：RA2 资源导入

- 实现 Files picker 导入。
- 沙盒资源目录映射到 `Platform.SupportDir`/Content loader。
- 加载 MIX、SHP、VXL、HVA、音频并显示 RA2 单位。

验收：RA2 资源可加载，能进入单机遭遇战。

## 9. Phase 1 测试记录

执行目录：`/Users/kevin/Projects/ra2-mac`

### `make test`

结果：通过。

- Release 构建成功。
- RA2 模组 MiniYAML 检查完成。
- 地图、默认序列和 Fluent reference 检查完成。
- 存在既有 Fluent 未使用/缺失 key 警告，但没有导致失败。

### `make check`

结果：未通过，但 Release 构建本身成功。

Debug `-warnaserror` 阶段报告 41 个既有分析器/格式错误，涉及 `OpenRA.Game`、`OpenRA.Mods.Common` 和当前 RA2 工作树修改；包括 IDE0055、SA1515、SA1137、RCS0056、RCS1080、CA1860 等。本阶段没有修改这些文件，也没有把这些无关错误混入 Phase 1 文档提交。

另外，当前仓库在开始分析前就存在大量 staged/unstaged/untracked 修改，后续实现必须继续保留这些用户改动，并按文件范围提交。

## 10. 技术路线确认

本项目后续采用以下路线：

> **原有 OpenRA 核心 + 原有 RA2 模组 + iOS 原生宿主 + iOS 平台适配层 + 触控到 MouseInput 的兼容层 + 沙盒资源导入。**

明确不采用：

- 不重写 OpenRA 游戏循环。
- 不重写 Actor、Trait、Rules、MiniYAML 或 RA2 格式解析器。
- 不把全部渲染器改写成另一套 2D/3D 引擎。
- 不把触控逻辑散落到所有 Widget。
- 不在首阶段引入战役、联网或单位编辑器功能。

当前最大技术风险是 iOS AOT + native SDL/音频/字体依赖，而不是 RA2 游戏逻辑。后续开发应先用最小 iOS 原型验证这一条链路，再决定继续沿用 SDL2/OpenGL ES 还是仅替换底层窗口/图形实现。
