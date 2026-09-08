# 后续开发检查表

## 修改前

- [ ] 记录仓库根、分支、HEAD、`git status --short`。
- [ ] 区分已有用户改动与本任务改动。
- [ ] 从 [architecture-index.md](architecture-index.md) 找到完整调用链，不只搜索一个符号。
- [ ] 写出可证伪的故障假设及相反证据。
- [ ] 涉及设备画面时保存基线截图、地图、阵营、时间与性能数据。
- [ ] 涉及卡死时先复制完整 Logs，避免下一次启动覆盖证据。

## 实现

- [ ] Bugfix 先添加能失败的最小测试或诊断边界。
- [ ] 一次 A/B 只改变一个变量。
- [ ] 不删除桌面功能来掩盖 iOS 平台故障。
- [ ] 不强制覆盖用户主动设置，除非该设置在 iOS 明确不可用且已有设计决定。
- [ ] 使用 `Platform.IsIOS`/`OperatingSystem.IsIOS()` 前确认初始化时序和测试覆盖。
- [ ] 新 Trait/Widget/Projectile 同时检查 YAML 引用、程序集、AOT 和 Fluent。

## 本地验证

- [ ] 目标单元测试先通过。
- [ ] 完整 `OpenRA.Test` 通过。
- [ ] `./utility.sh --check-yaml` 无错误；单独记录非阻断 warning。
- [ ] iOS `ios-arm64` Release build 零错误。
- [ ] 检查构建警告是否与本任务相关。

## 包内容门禁

- [ ] 对每个目标 C#/DLL、YAML、shader、资源记录源码与 App bundle SHA-256。
- [ ] 确认 App 签名时间晚于本次修改。
- [ ] `codesign --verify --deep --strict` 通过。
- [ ] 确认 Bundle ID、架构、provisioning profile 与目标设备匹配。
- [ ] 对 Bundle 与 Documents 中可能同名的资源分别核查来源。

## iPad 安装与实测

- [ ] 覆盖安装成功并返回预期 Bundle ID。
- [ ] 使用 `--terminate-existing` 启动，避免旧进程。
- [ ] 验证进程保持运行且主菜单可操作。
- [ ] 测试单指 tap/drag/long press、双指 pan/pinch。
- [ ] 测试基地车选择、移动、展开。
- [ ] 测试设置菜单、返回、滚动和大按钮命中区。
- [ ] 测试 Home/恢复后的第一次触摸。
- [ ] 在同一地图/视口获取前后截图。
- [ ] 稳定性测试至少 15 分钟，并保留完整 Logs。

## 网格纹路专项

- [ ] 对可见地形和未探索黑区分别判断。
- [ ] 打开/关闭 TerrainGeometryOverlay 做反证。
- [ ] 单变量比较 UV inset、GLSL highp、Shroud sprite 和 PixelArtScaling。
- [ ] 每个包记录 `combined.frag`、OpenRA.Game.dll、OpenRA.Mods.Common.dll 哈希。
- [ ] 只有纹路在物理 iPad 截图中消失且正常画面无回归，才可声称修复。

## 卡死专项

- [ ] 保存 `hang-diagnostic.log`、client/server/debug/graphics/exception/perf 日志。
- [ ] 记录卡死时最后 Logic/Render/Network frame。
- [ ] 区分 synchronous send、local immediate、remote packet、order resolve、world tick、present。
- [ ] 记录 Server 最后 event/ACK/order frame。
- [ ] 修复后重复原操作，并完成至少 15 分钟压力测试。
- [ ] 没有再次卡死只能作为阴性证据；必须确认诊断路径也正常工作。

## Git 交付

- [ ] `git diff --check` 通过。
- [ ] 仅暂存任务文件/精确 hunk。
- [ ] 展示完整 staged file list，确认无密钥、账号资料、大文件或用户无关改动。
- [ ] 提交信息描述行为或研究成果。
- [ ] 最终报告列出测试、构建、签名、安装、实测和仍未验证事项。
