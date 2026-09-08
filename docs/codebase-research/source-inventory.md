# 源码与配置清单

## 基线

- 仓库：`/Users/kevin/Projects/ra2-mac`
- 分支：`engine-20250330`
- 研究起点：`3138e92 Record iPad game-loop hang diagnostics`
- 规模：约 79 个非生成 C# 文件、123 个非生成 YAML 文件；`mods/` 约 271 个文件，`OpenRA.Mods.RA2/` 约 74 个文件，`ios/` 约 22 个文件。
- 状态：脏工作区。RA2/Yuri 内容、资源、桌面打包、iOS 配置等存在既有修改或未跟踪文件，研究与提交必须使用精确暂存。

## 子系统边界

| 子系统 | 主要位置 | 职责 |
|---|---|---|
| 核心引擎 | `engine/OpenRA.Game/` | 启动、设置、平台接口、世界、Actor/Trait、订单、网络、渲染、输入和 Widget 基类 |
| 通用 Mod | `engine/OpenRA.Mods.Common/` | RTS 通用 Trait、活动、寻路、覆盖层、Chrome Logic、调试工具 |
| C&C 共用 | `engine/OpenRA.Mods.Cnc/` | Westwood 文件格式、渲染器、媒体与 C&C 系列行为 |
| RA2 代码 | `OpenRA.Mods.RA2/` | RA2 专用 Trait、活动、弹体、战斗、Widget 与文件系统扩展 |
| RA2 数据 | `mods/ra2/` | Mod 清单、MiniYAML 规则、序列、武器、Chrome、音频、地图、资源和本地化 |
| iOS 宿主 | `ios/OpenRA.iOS/` | UIKit 生命周期、启动参数、AOT 保留、应用资源、签名和 iPad App |
| iOS 平台层 | `ios/OpenRA.Platforms.iOS/`、`ios/SDL2-CS/` | SDL/图形输入桥接与本机库绑定 |
| 桌面入口 | `launcher.py`、`launch-*.sh` | Mod 获取、配置和桌面启动 |
| 构建打包 | `packaging/`、`engine-patches/` | macOS App、图标、补丁维护与分发 |
| 验证 | `engine/OpenRA.Test/`、`tests/`、`utility.sh` | 引擎单元测试、项目测试和 MiniYAML lint |

## 依赖方向

`OpenRA.Mods.RA2` 引用 `OpenRA.Game`、`OpenRA.Mods.Common` 和 `OpenRA.Mods.Cnc`。iOS App 引用核心引擎、iOS 平台层和 RA2 程序集，并静态链接 SDL2、平台桩与 FreeType。`mods/ra2/mod.yaml` 再通过 `Assemblies` 加载三套 Mod 程序集，通过 `Rules`、`Sequences`、`Weapons`、`ChromeLayout` 等清单建立数据图。

## RA2 数据入口

`mods/ra2/mod.yaml` 是内容图根节点。当前规则包含盟军、苏军、尤里、自定义编辑器覆盖；Chrome 同时组合 RA2 页面和 `mods/common` 通用页面；地图允许系统地图与支持目录用户地图并存。后续每项研究必须从此清单反查具体 YAML，而不能假设某文件会自动加载。

## 生成与外部内容

`bin/`、`obj/`、iOS Release App 属于构建产物，不作为源码结论依据，但必须用于核对打包结果。`bits/`、MIX/SHP/VXL 等资源决定最终视觉，不可只检查 C#。未跟踪的 Yuri 规则与素材属于项目扩展，需单独标记成熟度和依赖完整性。
