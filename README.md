# TileMatcher

一款以 2D 叠层配对消除为核心、并逐步接入宠物收养养成内容的 Godot 4 C# 游戏项目。

工作重点是先把“可游玩关卡 + 稳定导出 + 可持续迭代的离线关卡链路”跑通，
再逐步把项目从原型验证推进到完整产品形态。

![Godot](https://img.shields.io/badge/Godot-4.6.1--mono-478CBF?logo=godotengine&logoColor=white)
![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet&logoColor=white)
![Renderer](https://img.shields.io/badge/Renderer-GL%20Compatibility-3C873A)
![Status](https://img.shields.io/badge/Status-Playable%20Prototype-F0AD4E)
![Android](https://img.shields.io/badge/Android-GitHub%20Hosted%20Release-34A853?logo=android&logoColor=white)

| 维度 | 说明 |
| --- | --- |
| 核心玩法 | 多层叠放牌面配对消除，强调可推进、可验证、可调试 |
| 当前内容 | 已接通离线导出关卡、Godot 关卡消费、首页养宠原型、安卓发布链路 |
| 技术路线 | Godot 4.6.1 Mono + C# + 离线关卡构造与评估工具 + GitHub Actions |
| 当前阶段 | 以“稳定玩法闭环 + 构建发布稳定性 + 数据驱动能力”作为第一优先级 |

本页用于快速说明项目定位、当前能力和主要入口；详细设计与研发记录请继续查看下方文档导航。

## 快速导航

- [这是什么](#这是什么)
- [项目定位](#项目定位)
- [当前状态](#当前状态)
- [功能亮点](#功能亮点)
- [技术架构](#技术架构)
- [运行与构建](#运行与构建)
- [仓库结构](#仓库结构)
- [相关文档](#相关文档)

## 这是什么

`TileMatcher` 当前不是单纯的麻将复刻，也不是单纯的技术实验仓库。

它正在逐步演进成一个包含两条主线的项目：

1. 一条是可持续扩展的 2D 叠层配对消除玩法。
2. 一条是围绕宠物救助、宠物乐园、金币奖励展开的轻养成外层循环。

项目里已经完成或接通的关键链路包括：

- 离线关卡构造、评估、筛选与导出。
- Godot 侧读取导出关卡目录并运行。
- 调试面板、提示、重开、失败结算等基础玩法辅助能力。
- 首页宠物救助中心、宠物乐园与金币消费的 MVP 原型。
- Android 本地打包脚本与 GitHub-hosted runner 发布流程。

## 项目定位

这个项目的目标不是快速堆一个“能跑”的演示，而是验证下面这条完整链路是否能长期成立：

离线构造关卡 -> 自动筛选 -> 导出正式数据 -> Godot 消费运行 -> 本地与 CI 稳定出包 -> 逐步补齐产品内容

当前阶段更关注三类问题：

- 玩法是否稳定，不会轻易出现死局和流程卡死。
- 数据链路是否清晰，关卡和运行时职责是否解耦。
- 构建链路是否可靠，本地和 CI 是否能使用同一套标准脚本。

## 当前状态

仓库已经具备以下基础能力：

- 已有一批离线导出的正式关卡数据，Godot 可直接消费目录索引。
- 游戏内已经支持提示、重开、失败窗口、调试入口等基础闭环。
- 首页已经进入“宠物救助 + 乐园展示 + 益智赚金币”方向的产品化原型。
- Android 发布流程已经切换到 GitHub-hosted runner，并完成首轮冒烟验证。

当前最值得关注的方向是：

- 继续稳住关卡体验与关卡数据质量。
- 继续推进首页养宠玩法和主循环衔接。
- 持续降低本地打包、CI 打包和资源同步的维护成本。

## 功能亮点

- 离线侧与 Godot 运行时解耦：离线工具负责导出关卡数据，Godot 只负责消费和运行。
- 关卡目录驱动：不再依赖大量手工维护的 `.tres`，可以直接消费 `level-catalog.json`。
- 调试能力较完整：支持关卡跳转、次数重置、自动消除、日志定位等。
- 玩法原型持续产品化：从单局消除扩展到金币奖励与宠物收养外层循环。
- Android 发布更稳定：本地脚本与 GitHub Actions 共用同一套打包逻辑。

## 技术架构

当前主要由四部分组成：

| 模块 | 说明 |
| --- | --- |
| Godot 客户端 | 承担场景、交互、UI、存档、运行时关卡消费 |
| 离线关卡工具 | 承担关卡构造、评估、导出、目录组织 |
| 调试与诊断链路 | 承担日志、调试面板、失败定位、运行时辅助 |
| 构建发布链路 | 承担本地 Android 出包、GitHub Actions hosted-runner 发布 |

当前安卓构建链路的原则已经明确：

- 不依赖手工临时补丁。
- 不依赖误入库的大型 Android 二进制。
- 优先从标准导出模板和标准脚本中补齐缺失资源。
- 允许 Godot 导出进程异常滞留，但不允许因此让脚本无响应卡住。

## 运行与构建

### 本地运行

Godot 工程目录：

- [godot](d:/UGit/TileMatcher/godot)

常用入口：

- 主工程配置：[project.godot](d:/UGit/TileMatcher/godot/project.godot)
- Android 导出预设：[export_presets.cfg](d:/UGit/TileMatcher/godot/export_presets.cfg)

### Android 打包

标准脚本：

- [build-android.ps1](d:/UGit/TileMatcher/scripts/build-android.ps1)

当前脚本已经补上两类关键保障：

- 会在导出前自动补齐 Android 导出所需的主题资源、启动背景资源和 Godot 模板归档。
- 会在等待 Godot 导出时持续输出心跳，并基于稳定产物判定继续后续流程，避免本地长时间看起来像卡死。

详细说明见：

- [安卓打包脚本说明.md](d:/UGit/TileMatcher/docs/安卓打包脚本说明.md)

### GitHub Actions 发布

工作流：

- [android-export.yml](d:/UGit/TileMatcher/.github/workflows/android-export.yml)

当前已完成：

- 使用 GitHub-hosted runner 发布 Android 包。
- 固定 Java、Android SDK、Gradle、Godot 版本。
- 通过 GitHub Secrets 恢复 keystore 并完成签名。
- `publish_release=false` 冒烟验证已经通过。

## 仓库结构

- [docs](d:/UGit/TileMatcher/docs)：设计文档、说明文档、研发记录
- [godot](d:/UGit/TileMatcher/godot)：Godot 工程
- [scripts](d:/UGit/TileMatcher/scripts)：本地与 CI 复用脚本
- [tools](d:/UGit/TileMatcher/tools)：离线关卡工具与测试
- [.github](d:/UGit/TileMatcher/.github)：GitHub Actions 工作流

## 相关文档

- [文档总览](d:/UGit/TileMatcher/docs/README.md)
- [文档导航约定](d:/UGit/TileMatcher/docs/文档导航约定.md)
- [麻将牌局构造与评估系统设计](d:/UGit/TileMatcher/docs/麻将牌局构造与评估系统设计.md)
- [动物消消乐与宠物店产品设计草案](d:/UGit/TileMatcher/docs/动物消消乐与宠物店产品设计草案.md)
- [项目阶段总结与对外说明](d:/UGit/TileMatcher/docs/项目阶段总结与对外说明.md)
- [安卓打包脚本说明](d:/UGit/TileMatcher/docs/安卓打包脚本说明.md)
