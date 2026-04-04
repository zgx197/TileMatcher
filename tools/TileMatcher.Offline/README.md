# TileMatcher.Offline

## 目标

`TileMatcher.Offline` 是当前项目的离线侧 MVP 工具。

它用于独立于 Godot 运行时完成以下流程：

1. 批量生成候选牌局
2. 计算最小评估指标
3. 运行自动筛选
4. 导出分析结果
5. 导出供运行时消费的正式关卡 JSON

当前定位是：

- 离线批处理工具
- 关卡候选与筛选结果导出器
- 后续 Web 分析台的数据来源

Godot 侧后续只需要负责：

- 关卡编号到数据文件路径的映射
- 运行时读取导出的正式关卡 JSON

## 当前入口

项目文件：

- `tools/TileMatcher.Offline/TileMatcher.Offline.csproj`

默认配置文件：

- `tools/TileMatcher.Offline/batch-config.sample.json`

程序入口：

- `tools/TileMatcher.Offline/Program.cs`

## 运行方式

在仓库根目录执行：

```powershell
dotnet run --project tools\TileMatcher.Offline\TileMatcher.Offline.csproj
```

默认会读取：

```text
tools/TileMatcher.Offline/batch-config.sample.json
```

如果要指定自定义配置文件：

```powershell
dotnet run --project tools\TileMatcher.Offline\TileMatcher.Offline.csproj -- path\to\your-batch-config.json
```

## 最小测试

当前仓库已经补了一套不依赖 Godot 的最小离线单测骨架，重点覆盖：

1. 默认配置稳定性
2. 最小可解 / 不可解样例的评估结果
3. 自动筛选决策分档
4. 运行时关卡导出格式与文件名稳定性

一键运行：

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\run-offline-tests.ps1
```

测试结果会输出到：

```text
artifacts/test-results/offline-tests/summary.json
```

## 配置文件说明

当前配置文件分为三部分：

### 1. 批次级配置

- `batchName`
  - 当前离线批次名称

- `candidateCount`
  - 本次希望生成多少候选牌局

- `maxAcceptedLevels`
  - 最多导出多少个自动通过的正式关卡

- `randomSeed`
  - 批次级随机种子

- `analysisOutputDir`
  - 分析结果输出目录

- `runtimeOutputDir`
  - 运行时关卡 JSON 输出目录

### 2. `rules`

用于控制结构牌局构造的规则，例如：

- 层数范围
- 底层最少牌数
- 是否要求完整支撑
- 是否要求上层严格收缩
- 随机底层宽高范围
- 挖洞概率
- 底层步长
- 上层偏移模式

### 3. `filter`

用于控制自动筛选的最小规则，例如：

- 牌数区间
- 层数区间
- 最小初始分支数
- 最小可解路径估计数
- 最大死局率
- 最小随机推进存活率
- 自动通过阈值
- 待复核阈值
- 随机模拟次数
- 最大解路径搜索上限

## 当前评估指标

当前 MVP 已经输出以下最小指标集：

- `HasSolution`
- `SolutionCountEstimate`
- `InitialBranchCount`
- `AverageBranchCount`
- `DeadEndRate`
- `RandomPlaySurvivalRate`
- `TileCount`
- `LayerCount`
- `SearchVisitedStateCount`
- `SearchDeadEndStateCount`

## 自动筛选结果

当前自动筛选会输出三类结果：

- `AutoAccept`
  - 自动进入推荐池

- `NeedsReview`
  - 指标接近边界，需要人工复核

- `AutoReject`
  - 未通过筛选

同时会输出：

- `DifficultyBucket`
- `RecommendationScore`
- `RejectReasons`
- `Tags`

## 输出目录约定

默认输出目录位于：

```text
artifacts/mahjong-mvp/
```

分为两部分：

### 1. 分析输出

目录：

```text
artifacts/mahjong-mvp/analysis/
```

当前包含：

- `summary.json`
  - 当前批次的统计摘要

- `candidates.json`
  - 全部候选牌局、评估结果和筛选结果

- `accepted.json`
  - 自动通过的候选

- `needs-review.json`
  - 待人工复核的候选

### 2. 运行时输出

目录：

```text
artifacts/mahjong-mvp/runtime-levels/
```

当前包含：

- `level-catalog.json`
  - 正式关卡目录

- `levels/level_001.json`
- `levels/level_002.json`
- ...
  - 具体的正式关卡文件

## 输出格式说明

### 1. `summary.json`

当前用于快速查看本批次结果概况。

主要字段：

- `BatchName`
- `CandidateCount`
- `AcceptedCount`
- `NeedsReviewCount`
- `RejectedCount`
- `GeneratedAtUtc`

### 2. `candidates.json`

这是最完整的分析产物，每个候选项包含三部分：

- `Layout`
  - 牌局本身

- `Evaluation`
  - 最小评估指标

- `FilterResult`
  - 自动筛选结果

单个候选当前结构大致为：

- `CandidateId`
- `BatchIndex`
- `Seed`
- `Layout`
- `Evaluation`
- `FilterResult`

### 3. `accepted.json`

结构与 `candidates.json` 相同，但只保留自动通过的候选。

后续 Web 分析台可以直接用它作为“推荐池”输入。

### 4. `needs-review.json`

结构与 `candidates.json` 相同，但只保留需要人工复核的候选。

后续策划和开发可以优先查看这一组。

### 5. `level-catalog.json`

这是运行时消费的最小目录文件。

当前字段包括：

- `LevelNumber`
- `CandidateId`
- `DifficultyBucket`
- `RecommendationScore`
- `FileName`

Godot 侧后续可以先从这里读取：

- 第几关对应哪一个 JSON 文件

### 6. `levels/level_XXX.json`

这是单关正式关卡文件。

当前结构包括：

- `LevelNumber`
- `CandidateId`
- `DifficultyBucket`
- `RecommendationScore`
- `Layout`

其中 `Layout` 当前包含：

- `CandidateId`
- `LevelId`
- `Tiles`

而 `Tiles` 中的每张牌当前包含：

- `Id`
- `Type`
- `GX`
- `GY`
- `GZ`
- `Shape`
- `Removed`

后续如果 Godot 运行时要正式接入，建议优先围绕这份文件做加载器，而不是反向依赖分析输出。

## 当前边界

当前 MVP 已完成：

- 候选批量生成
- 最小评估
- 自动筛选
- JSON 导出

当前尚未完成：

- Godot 运行时消费这些 JSON
- Web 分析台读取并展示这些结果
- 更强的状态图分析
- 盖牌等变种导出
- 更细的难度档案配置

## 后续建议

下一步推荐按这个顺序推进：

1. 在 Godot 侧增加“按 JSON 文件加载正式关卡”的能力
2. 明确运行时关卡 JSON 的稳定字段约定
3. 再接一个最小 Web 分析页读取 `analysis/` 下的结果
4. 再继续增强评估指标与自动筛选规则
