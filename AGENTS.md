# TileMatcher Agent 指南

## 项目目标

本仓库是一个使用 Godot 4.6.1 Mono 开发的游戏项目，目标是复刻一款参考 Vita Mahjong 的麻将配对消除游戏。

当前阶段明确为：

- 先做游戏页
- 不优先做首页和结果页
- 每一步都尽量留下可见、可运行、可测试的结果
- 避免在没有可见效果之前做大规模、纯抽象的扩展

当前的近期目标不是完整成品，而是逐步推进到：

- 稳定的网格系统
- 稳定的静态牌桌渲染
- 简单但可运行的游戏页 UI
- 可移动状态可视化
- 再进入拖拽与匹配逻辑

## 当前项目意图

当前项目已经确定以下方向：

- 使用 Godot 2D，不使用 Godot 3D 作为主运行方案
- 通过 2D 分层、阴影和绘制顺序表现 2.5D 立体感
- 使用 `(gx, gy, gz)` 表示麻将的逻辑坐标
- 每张牌当前默认使用 `4 x 6` 的矩形逻辑 footprint
- 上层合法性以“底面完整覆盖”为核心，而不是硬编码二支撑 / 四支撑
- Inspector 中的规则档案与牌形已开始通过 Godot `Resource` 配置资产驱动
- 采用“小步快跑”的模块化迭代方式，优先保证每轮修改后都能看到效果

## 进入项目后优先阅读

在进行非微小修改前，优先阅读以下文件：

- `README.md`
- `docs/README.md`
- `docs/文档导航约定.md`
- `docs/通用堆叠框架与第二代网格方案.md`
- `docs/代码架构与配置说明.md`
- `docs/研发记录/README.md`
- `docs/截图画面与渲染方式分析.md`

如需了解早期原型方案，再补充阅读：

- `docs/归档/第一代网格坐标系统设计.md`

如果用户提到以下问题，也应优先查看：

- 场景加载失败
- C# 脚本报错
- 程序集加载失败
- MSBuild 或 .NET 构建失败

这类问题要同时检查：

- `godot/logs/`

## 仓库结构

仓库当前结构如下：

- `docs/`：设计文档
- `godot/`：Godot 工程根目录
- `godot/scenes/`：场景文件
- `godot/scripts/`：C# 脚本
- `godot/logs/`：诊断日志

当前关键文件包括：

- `godot/project.godot`
- `godot/scenes/game/GameScene.tscn`
- `godot/scenes/tile/Tile.tscn`
- `godot/scripts/game/GameScene.cs`
- `godot/scripts/board/BoardController.cs`
- `godot/scripts/grid/GridConfig.cs`
- `godot/scripts/grid/GridMath.cs`
- `godot/scripts/data/LevelLayout.cs`
- `godot/scripts/tile/TileView.cs`

## 已确认的技术决策

以下决策已经确定，不应在没有新证据的情况下反复回退讨论：

- 运行时使用 Godot 2D
- 当前工程使用兼容渲染器
- 棋盘逻辑使用整数坐标系统
- 每张牌当前默认使用 `4 x 6` 的矩形逻辑 footprint
- 上层遮挡通过几何投影重叠判断
- 渲染逻辑与玩法逻辑分离
- 优先追求“看得见的进展”，而不是过早扩展复杂架构

## 本仓库的工作方式

在本项目中工作时，应遵循以下原则：

- 优先做能在 Godot 中直接看到或测试到的改动
- 当前界面还不能稳定观察时，不要跳到更复杂的系统
- 网格、牌桌拓扑、牌视图、交互逻辑尽量解耦
- 规则一旦达成一致，优先同步更新文档
- 文档和实现应尽量同步演进，避免文档严重滞后
- 如果一次工作改变了设计方向，或定位并解决了真实技术问题，应同步更新对应的 `docs/研发记录/*.md`

## 推荐优先使用的技能

本项目中如遇到合适场景，优先考虑以下技能：

- `godot-log-capture`
  - 适用场景：Godot 场景加载失败、C# 构建错误、程序集加载失败、需要稳定复现实验日志
  - 本地路径：`C:\Users\Guoxi\.codex\skills\godot-log-capture`

- `skill-creator`
  - 适用场景：需要新建或更新 Codex 技能

- `openai-docs`
  - 仅在用户明确要求 OpenAI 产品或 API 最新文档时使用

普通项目开发优先读取本地文档，不要滥用技能。

## Godot 诊断流程

如果 Godot 出现加载、构建或程序集问题，优先使用本地日志抓取技能或直接调用它的脚本。

推荐脚本：

- `C:\Users\Guoxi\.codex\skills\godot-log-capture\scripts\capture-godot-logs.ps1`

典型调用方式：

```powershell
powershell -ExecutionPolicy Bypass -File "C:\Users\Guoxi\.codex\skills\godot-log-capture\scripts\capture-godot-logs.ps1" `
  -GodotExe "D:\GodotCSharp\Godot_v4.6.1-stable_mono_win64\Godot_v4.6.1-stable_mono_win64.exe" `
  -ProjectPath "D:\UGit\TileMatcher\godot" `
  -IncludeEditorLogs
```

抓取日志后：

- 先看 `summary.txt`
- 再看 `godot-headless.log`
- 若存在构建日志，再看 `.NET build` 或 `MSBuild` 输出

## 当前已知问题

当前项目已经确认过一个重要诊断结果：

- Godot headless 运行时报错：`.NET: Failed to load project assembly`

后续代理进入本项目时，如果再遇到场景打不开，不应先假设是 `.tscn` 文件本身损坏，而应优先考虑：

- C# 工程未正确生成
- 程序集未被 Godot 正常加载
- 构建系统没有正确接通

## 默认开发优先级

如果用户没有重新指定优先级，则默认按以下顺序推进：

1. 让游戏页稳定可见、可测试
2. 保持牌桌渲染稳定
3. 可视化牌的状态，尤其是可移动和不可移动
4. 实现可移动判定
5. 实现拖拽交互
6. 实现匹配与消除

除非用户明确要求，否则不要提前跳到：

- 最终美术还原
- 完整流程页面
- 多关卡系统
- 商业化系统

## 编辑约束

- 默认保持 ASCII 编辑，除非文件本身就需要中文或其他非 ASCII 内容
- 手工代码编辑使用 `apply_patch`
- 不要回退用户未要求回退的改动
- 搜索文件和文本优先使用 `rg`
- 当 Godot 或 .NET 问题不明确时，优先抓日志，不要直接猜

## 什么算是有效进展

在本项目里，一次好的推进通常至少满足以下一项：

- 产生了新的可见界面变化
- 产生了新的可测试交互
- 减少了核心规则的不确定性
- 增强了下一步排错所需的诊断能力

纯抽象的重构或扩展只有在它明确服务于下一个可见结果时才值得做。
