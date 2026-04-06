using System.Text.Json;

namespace TileMatcher.Offline;

internal static partial class OfflinePipeline
{
    internal static string ExportAnalysisDashboardForTests(
        OfflineBatchConfig config,
        string analysisDir,
        IReadOnlyList<OfflineCandidateRecord> candidates,
        IReadOnlyList<OfflineCandidateRecord> accepted,
        IReadOnlyList<OfflineCandidateRecord> review,
        DateTime generatedAtUtc)
    {
        return ExportAnalysisDashboard(config, analysisDir, candidates, accepted, review, generatedAtUtc);
    }

    private static string ExportAnalysisDashboard(
        OfflineBatchConfig config,
        string analysisDir,
        IReadOnlyList<OfflineCandidateRecord> candidates,
        IReadOnlyList<OfflineCandidateRecord> accepted,
        IReadOnlyList<OfflineCandidateRecord> review,
        DateTime generatedAtUtc)
    {
        var runtimeLevelLookup = accepted
            .Select((candidate, index) => new { candidate.CandidateId, LevelNumber = index + 1 })
            .ToDictionary(item => item.CandidateId, item => item.LevelNumber);

        var payload = new
        {
            batchName = config.BatchName,
            generatedAtUtc = generatedAtUtc.ToString("O"),
            summary = new
            {
                candidateCount = candidates.Count,
                acceptedCount = accepted.Count,
                needsReviewCount = review.Count,
                rejectedCount = candidates.Count(candidate => candidate.FilterResult.Decision == OfflineFilterDecision.AutoReject),
            },
            candidates = candidates
                .OrderByDescending(candidate => candidate.FilterResult.RecommendationScore)
                .ThenBy(candidate => candidate.BatchIndex)
                .Select(candidate => BuildDashboardCandidate(candidate, runtimeLevelLookup))
                .ToList(),
        };

        var htmlPath = Path.Combine(analysisDir, "index.html");
        var dashboardJson = JsonSerializer.Serialize(payload);
        File.WriteAllText(htmlPath, BuildAnalysisDashboardHtml(dashboardJson));
        return htmlPath;
    }

    private static object BuildDashboardCandidate(
        OfflineCandidateRecord candidate,
        IReadOnlyDictionary<string, int> runtimeLevelLookup)
    {
        runtimeLevelLookup.TryGetValue(candidate.CandidateId, out var runtimeLevelNumber);

        return new
        {
            candidateId = candidate.CandidateId,
            batchIndex = candidate.BatchIndex,
            seed = candidate.Seed,
            runtimeLevelNumber = runtimeLevelNumber == 0 ? (int?)null : runtimeLevelNumber,
            layout = new
            {
                candidateId = candidate.Layout.CandidateId,
                levelId = candidate.Layout.LevelId,
                tiles = candidate.Layout.Tiles
                    .Select(tile => new
                    {
                        id = tile.Id,
                        type = tile.Type,
                        gx = tile.GX,
                        gy = tile.GY,
                        gz = tile.GZ,
                        shape = new
                        {
                            widthUnits = tile.Shape.WidthUnits,
                            heightUnits = tile.Shape.HeightUnits,
                        },
                        removed = tile.Removed,
                        faceHiddenInitial = tile.FaceHiddenInitial,
                    })
                    .ToList(),
            },
            evaluation = new
            {
                hasSolution = candidate.Evaluation.HasSolution,
                solutionCountEstimate = candidate.Evaluation.SolutionCountEstimate,
                initialBranchCount = candidate.Evaluation.InitialBranchCount,
                averageBranchCount = candidate.Evaluation.AverageBranchCount,
                deadEndRate = candidate.Evaluation.DeadEndRate,
                randomPlaySurvivalRate = candidate.Evaluation.RandomPlaySurvivalRate,
                tileCount = candidate.Evaluation.TileCount,
                layerCount = candidate.Evaluation.LayerCount,
                searchVisitedStateCount = candidate.Evaluation.SearchVisitedStateCount,
                searchDeadEndStateCount = candidate.Evaluation.SearchDeadEndStateCount,
            },
            filterResult = new
            {
                decision = candidate.FilterResult.Decision.ToString(),
                difficultyBucket = candidate.FilterResult.DifficultyBucket,
                recommendationScore = candidate.FilterResult.RecommendationScore,
                rejectReasons = candidate.FilterResult.RejectReasons,
                tags = candidate.FilterResult.Tags,
                needsManualReview = candidate.FilterResult.NeedsManualReview,
            },
        };
    }

    private static string BuildAnalysisDashboardHtml(string dashboardJson)
    {
        return $$"""
<!DOCTYPE html>
<html lang="zh-CN">
<head>
    <meta charset="utf-8">
    <meta name="viewport" content="width=device-width, initial-scale=1">
    <title>TileMatcher 配对牌局分析台</title>
    <style>
        :root {
            --bg: #f6efe4;
            --panel: rgba(255, 251, 245, 0.86);
            --panel-strong: rgba(255, 248, 238, 0.95);
            --ink: #1f2a2e;
            --muted: #5d6a6f;
            --line: rgba(32, 58, 64, 0.16);
            --accent: #cf6b2c;
            --accent-soft: rgba(207, 107, 44, 0.14);
            --teal: #187c78;
            --teal-soft: rgba(24, 124, 120, 0.14);
            --berry: #b3475b;
            --berry-soft: rgba(179, 71, 91, 0.14);
            --gold: #c8952d;
            --shadow: 0 18px 42px rgba(83, 55, 24, 0.14);
            --radius-lg: 24px;
            --radius-md: 16px;
            --radius-sm: 12px;
        }

        * {
            box-sizing: border-box;
        }

        html, body {
            margin: 0;
            min-height: 100%;
            color: var(--ink);
            background:
                radial-gradient(circle at top left, rgba(24, 124, 120, 0.16), transparent 28%),
                radial-gradient(circle at top right, rgba(207, 107, 44, 0.14), transparent 24%),
                linear-gradient(180deg, #f8f2e8 0%, #f3eadc 48%, #efe1cf 100%);
            font-family: "Trebuchet MS", "Segoe UI", sans-serif;
        }

        body::before {
            content: "";
            position: fixed;
            inset: 0;
            pointer-events: none;
            background-image:
                linear-gradient(rgba(121, 91, 56, 0.05) 1px, transparent 1px),
                linear-gradient(90deg, rgba(121, 91, 56, 0.05) 1px, transparent 1px);
            background-size: 24px 24px;
            mask-image: linear-gradient(180deg, rgba(0, 0, 0, 0.45), transparent 85%);
        }

        .page-shell {
            width: min(1560px, calc(100vw - 32px));
            margin: 20px auto 32px;
        }

        .hero {
            padding: 28px 30px 24px;
            border: 1px solid rgba(104, 71, 33, 0.14);
            border-radius: 30px;
            background:
                linear-gradient(135deg, rgba(255, 251, 245, 0.96), rgba(246, 235, 217, 0.82)),
                linear-gradient(120deg, rgba(24, 124, 120, 0.08), rgba(207, 107, 44, 0.12));
            box-shadow: var(--shadow);
            backdrop-filter: blur(12px);
        }

        .hero-top {
            display: flex;
            flex-wrap: wrap;
            align-items: flex-end;
            justify-content: space-between;
            gap: 18px;
        }

        .eyebrow {
            display: inline-flex;
            align-items: center;
            gap: 8px;
            padding: 7px 12px;
            border-radius: 999px;
            background: var(--accent-soft);
            color: var(--accent);
            font-size: 13px;
            font-weight: 700;
            letter-spacing: 0.04em;
            text-transform: uppercase;
        }

        h1 {
            margin: 14px 0 10px;
            font-family: Georgia, "Times New Roman", serif;
            font-size: clamp(32px, 4.4vw, 54px);
            line-height: 1;
            letter-spacing: -0.03em;
        }

        .hero-copy {
            max-width: 920px;
            margin: 0;
            color: var(--muted);
            font-size: 15px;
            line-height: 1.7;
        }

        .hero-meta {
            display: grid;
            gap: 10px;
            min-width: 260px;
        }

        .meta-card {
            padding: 14px 16px;
            border: 1px solid rgba(24, 124, 120, 0.16);
            border-radius: 18px;
            background: rgba(255, 252, 247, 0.84);
        }

        .meta-label {
            display: block;
            color: var(--muted);
            font-size: 12px;
            text-transform: uppercase;
            letter-spacing: 0.08em;
        }

        .meta-value {
            margin-top: 6px;
            font-size: 16px;
            font-weight: 700;
        }

        .summary-grid {
            display: grid;
            grid-template-columns: repeat(4, minmax(0, 1fr));
            gap: 14px;
            margin-top: 22px;
        }

        .summary-card {
            position: relative;
            overflow: hidden;
            padding: 18px 18px 16px;
            border: 1px solid rgba(77, 53, 29, 0.12);
            border-radius: 22px;
            background: rgba(255, 252, 248, 0.9);
        }

        .summary-card::after {
            content: "";
            position: absolute;
            right: -26px;
            top: -22px;
            width: 96px;
            height: 96px;
            border-radius: 999px;
            background: linear-gradient(135deg, rgba(24, 124, 120, 0.12), rgba(207, 107, 44, 0.06));
        }

        .summary-label {
            position: relative;
            color: var(--muted);
            font-size: 13px;
        }

        .summary-value {
            position: relative;
            margin-top: 8px;
            font-size: clamp(28px, 3vw, 40px);
            font-weight: 800;
            line-height: 1;
        }

        .summary-note {
            position: relative;
            margin-top: 8px;
            color: var(--muted);
            font-size: 12px;
        }

        .workspace {
            display: grid;
            grid-template-columns: minmax(0, 1.18fr) minmax(360px, 0.82fr);
            gap: 18px;
            margin-top: 18px;
        }

        .stack {
            display: grid;
            gap: 18px;
            min-width: 0;
        }

        .panel {
            border: 1px solid rgba(89, 62, 35, 0.12);
            border-radius: var(--radius-lg);
            background: var(--panel);
            box-shadow: var(--shadow);
            backdrop-filter: blur(14px);
        }

        .panel-header {
            display: flex;
            align-items: center;
            justify-content: space-between;
            gap: 12px;
            padding: 18px 20px 0;
        }

        .panel-title {
            margin: 0;
            font-size: 18px;
            font-weight: 800;
        }

        .panel-subtitle {
            color: var(--muted);
            font-size: 13px;
        }

        .filters {
            padding: 18px 20px 20px;
        }

        .filter-grid {
            display: grid;
            grid-template-columns: repeat(3, minmax(0, 1fr));
            gap: 14px;
        }

        .filter-group {
            display: grid;
            gap: 8px;
        }

        .filter-group.span-2 {
            grid-column: span 2;
        }

        .filter-group label {
            color: var(--muted);
            font-size: 12px;
            font-weight: 700;
            letter-spacing: 0.04em;
            text-transform: uppercase;
        }

        .filter-group input,
        .filter-group select {
            width: 100%;
            padding: 12px 14px;
            border: 1px solid rgba(71, 52, 34, 0.14);
            border-radius: var(--radius-sm);
            background: rgba(255, 253, 250, 0.92);
            color: var(--ink);
            font: inherit;
        }

        .range-value {
            color: var(--accent);
            font-weight: 700;
        }

        .list-panel {
            overflow: hidden;
        }

        .table-wrap {
            overflow: auto;
            padding: 8px 20px 20px;
        }

        table {
            width: 100%;
            border-collapse: collapse;
            min-width: 880px;
        }

        thead th {
            position: sticky;
            top: 0;
            z-index: 1;
            padding: 14px 10px;
            border-bottom: 1px solid var(--line);
            background: rgba(247, 238, 224, 0.98);
            color: var(--muted);
            font-size: 12px;
            text-align: left;
            text-transform: uppercase;
            letter-spacing: 0.06em;
        }

        tbody td {
            padding: 14px 10px;
            border-bottom: 1px solid rgba(61, 49, 36, 0.08);
            vertical-align: top;
            font-size: 14px;
        }

        tbody tr {
            cursor: pointer;
            transition: background 0.18s ease, transform 0.18s ease;
        }

        tbody tr:hover {
            background: rgba(24, 124, 120, 0.08);
        }

        tbody tr.selected {
            background: rgba(207, 107, 44, 0.1);
        }

        .mono {
            font-family: Consolas, "SFMono-Regular", monospace;
        }

        .candidate-main {
            display: grid;
            gap: 4px;
        }

        .candidate-title {
            font-weight: 800;
        }

        .candidate-sub {
            color: var(--muted);
            font-size: 12px;
        }

        .chip-row {
            display: flex;
            flex-wrap: wrap;
            gap: 8px;
        }

        .chip {
            display: inline-flex;
            align-items: center;
            gap: 6px;
            padding: 6px 10px;
            border-radius: 999px;
            background: rgba(31, 42, 46, 0.08);
            color: var(--ink);
            font-size: 12px;
            font-weight: 700;
            line-height: 1;
        }

        .chip.teal {
            background: var(--teal-soft);
            color: var(--teal);
        }

        .chip.orange {
            background: var(--accent-soft);
            color: var(--accent);
        }

        .chip.berry {
            background: var(--berry-soft);
            color: var(--berry);
        }

        .chip.gold {
            background: rgba(200, 149, 45, 0.14);
            color: #976c12;
        }

        .detail-panel {
            position: sticky;
            top: 20px;
            align-self: start;
            padding-bottom: 20px;
        }

        .detail-body {
            padding: 18px 20px 0;
            display: grid;
            gap: 16px;
        }

        .detail-empty {
            padding: 40px 20px 28px;
            color: var(--muted);
            line-height: 1.8;
        }

        .detail-title {
            margin: 0;
            font-size: 26px;
            font-weight: 900;
        }

        .detail-subtitle {
            margin-top: 8px;
            color: var(--muted);
            line-height: 1.7;
        }

        .detail-grid {
            display: grid;
            grid-template-columns: repeat(2, minmax(0, 1fr));
            gap: 12px;
        }

        .metric-card {
            padding: 14px 14px 12px;
            border: 1px solid rgba(55, 47, 35, 0.1);
            border-radius: 18px;
            background: var(--panel-strong);
        }

        .metric-label {
            color: var(--muted);
            font-size: 12px;
            text-transform: uppercase;
            letter-spacing: 0.06em;
        }

        .metric-value {
            margin-top: 8px;
            font-size: 22px;
            font-weight: 800;
            line-height: 1;
        }

        .metric-note {
            margin-top: 6px;
            color: var(--muted);
            font-size: 12px;
        }

        .section-card {
            padding: 16px;
            border: 1px solid rgba(55, 47, 35, 0.1);
            border-radius: 20px;
            background: var(--panel-strong);
        }

        .section-title {
            margin: 0 0 10px;
            font-size: 15px;
            font-weight: 800;
        }

        .section-copy {
            color: var(--muted);
            font-size: 13px;
            line-height: 1.7;
        }

        .manual-mark {
            display: grid;
            gap: 8px;
        }

        .manual-mark select {
            width: 100%;
            padding: 12px 14px;
            border: 1px solid rgba(71, 52, 34, 0.14);
            border-radius: var(--radius-sm);
            background: rgba(255, 253, 250, 0.92);
            font: inherit;
            color: var(--ink);
        }

        .preview-header {
            display: flex;
            flex-wrap: wrap;
            align-items: center;
            justify-content: space-between;
            gap: 12px;
        }

        .layer-controls {
            display: flex;
            flex-wrap: wrap;
            gap: 8px;
        }

        .layer-button {
            padding: 7px 10px;
            border: 1px solid rgba(24, 124, 120, 0.16);
            border-radius: 999px;
            background: rgba(255, 255, 255, 0.7);
            color: var(--muted);
            font: inherit;
            cursor: pointer;
        }

        .layer-button.active {
            background: var(--teal);
            border-color: var(--teal);
            color: white;
        }

        .preview-shell {
            border-radius: 20px;
            background:
                radial-gradient(circle at top, rgba(24, 124, 120, 0.14), transparent 52%),
                linear-gradient(180deg, #fffaf3, #efe2cf);
            border: 1px solid rgba(73, 58, 35, 0.12);
            padding: 14px;
        }

        .preview-meta {
            margin-top: 10px;
            color: var(--muted);
            font-size: 12px;
        }

        .preview-svg {
            width: 100%;
            height: auto;
            display: block;
        }

        .tile-shadow {
            fill: rgba(76, 52, 30, 0.16);
        }

        .tile-face {
            stroke: rgba(77, 48, 16, 0.26);
            stroke-width: 0.14;
        }

        .tile-face.suit-bam {
            fill: #d9efe1;
        }

        .tile-face.suit-dot {
            fill: #fce2d7;
        }

        .tile-face.suit-chr {
            fill: #efe0f7;
        }

        .tile-face.suit-honor {
            fill: #f7ecca;
        }

        .tile-face.hidden {
            fill: #d7d9dd;
        }

        .tile-label {
            fill: #213035;
            font-size: 1.05px;
            font-weight: 700;
            text-anchor: middle;
            dominant-baseline: middle;
            font-family: Consolas, "SFMono-Regular", monospace;
            pointer-events: none;
        }

        .tile-label.hidden {
            fill: #46525a;
        }

        .legend {
            display: flex;
            flex-wrap: wrap;
            gap: 8px;
            margin-top: 12px;
        }

        .legend-item {
            display: inline-flex;
            align-items: center;
            gap: 8px;
            color: var(--muted);
            font-size: 12px;
        }

        .legend-swatch {
            width: 12px;
            height: 12px;
            border-radius: 4px;
            border: 1px solid rgba(61, 47, 35, 0.16);
        }

        .empty-state {
            padding: 28px 18px;
            color: var(--muted);
            text-align: center;
        }

        @media (max-width: 1180px) {
            .workspace {
                grid-template-columns: 1fr;
            }

            .detail-panel {
                position: static;
            }
        }

        @media (max-width: 900px) {
            .summary-grid,
            .filter-grid,
            .detail-grid {
                grid-template-columns: 1fr 1fr;
            }
        }

        @media (max-width: 640px) {
            .page-shell {
                width: min(100vw - 20px, 1560px);
                margin: 10px auto 24px;
            }

            .hero,
            .filters,
            .detail-body,
            .table-wrap {
                padding-left: 16px;
                padding-right: 16px;
            }

            .summary-grid,
            .filter-grid,
            .detail-grid {
                grid-template-columns: 1fr;
            }

            table {
                min-width: 720px;
            }
        }
    </style>
</head>
<body>
    <div class="page-shell">
        <section class="hero">
            <div class="hero-top">
                <div>
                    <div class="eyebrow">Offline Analysis Dashboard</div>
                    <h1>配对牌局分析台</h1>
                    <p class="hero-copy">
                        面向离线构造结果的第一版 Web 分析台。它优先解决“看清候选、快速筛选、定位边界样本、预览牌桌结构”这四件事，
                        先作为离线流程的浏览器，而不是在线重生成平台。
                    </p>
                </div>
                <div class="hero-meta">
                    <div class="meta-card">
                        <span class="meta-label">批次名</span>
                        <span class="meta-value mono" id="batchName">-</span>
                    </div>
                    <div class="meta-card">
                        <span class="meta-label">生成时间</span>
                        <span class="meta-value" id="generatedAt">-</span>
                    </div>
                </div>
            </div>

            <div class="summary-grid">
                <article class="summary-card">
                    <div class="summary-label">候选总数</div>
                    <div class="summary-value" id="summaryTotal">0</div>
                    <div class="summary-note">当前批次完整候选池</div>
                </article>
                <article class="summary-card">
                    <div class="summary-label">自动通过</div>
                    <div class="summary-value" id="summaryAccepted">0</div>
                    <div class="summary-note" id="summaryAcceptedNote">推荐池</div>
                </article>
                <article class="summary-card">
                    <div class="summary-label">待复核</div>
                    <div class="summary-value" id="summaryReview">0</div>
                    <div class="summary-note" id="summaryReviewNote">边界样本</div>
                </article>
                <article class="summary-card">
                    <div class="summary-label">自动淘汰</div>
                    <div class="summary-value" id="summaryRejected">0</div>
                    <div class="summary-note" id="summaryRejectedNote">硬约束或推荐分不足</div>
                </article>
            </div>
        </section>

        <main class="workspace">
            <section class="stack">
                <section class="panel">
                    <div class="panel-header">
                        <div>
                            <h2 class="panel-title">候选筛选器</h2>
                            <div class="panel-subtitle">先用自动指标缩小范围，再针对边界样本进详情页看结构。</div>
                        </div>
                        <div class="chip orange" id="filteredBadge">筛选后 0 / 0</div>
                    </div>
                    <div class="filters">
                        <div class="filter-grid">
                            <div class="filter-group span-2">
                                <label for="searchInput">搜索</label>
                                <input id="searchInput" type="search" placeholder="按 CandidateId、标签、人工标记搜索">
                            </div>
                            <div class="filter-group">
                                <label for="sortSelect">排序</label>
                                <select id="sortSelect">
                                    <option value="score_desc">推荐分从高到低</option>
                                    <option value="score_asc">推荐分从低到高</option>
                                    <option value="survival_desc">随机存活率从高到低</option>
                                    <option value="dead_end_desc">死局率从高到低</option>
                                    <option value="tile_count_desc">牌数从高到低</option>
                                    <option value="batch_index_asc">批次序号从低到高</option>
                                </select>
                            </div>
                            <div class="filter-group">
                                <label for="decisionSelect">自动筛选结果</label>
                                <select id="decisionSelect">
                                    <option value="all">全部</option>
                                    <option value="AutoAccept">AutoAccept</option>
                                    <option value="NeedsReview">NeedsReview</option>
                                    <option value="AutoReject">AutoReject</option>
                                </select>
                            </div>
                            <div class="filter-group">
                                <label for="difficultySelect">难度桶</label>
                                <select id="difficultySelect">
                                    <option value="all">全部</option>
                                </select>
                            </div>
                            <div class="filter-group">
                                <label for="tagSelect">标签</label>
                                <select id="tagSelect">
                                    <option value="all">全部</option>
                                </select>
                            </div>
                            <div class="filter-group">
                                <label for="manualMarkFilter">人工标记</label>
                                <select id="manualMarkFilter">
                                    <option value="all">全部</option>
                                    <option value="keep">保留</option>
                                    <option value="review">重点复核</option>
                                    <option value="reject">淘汰</option>
                                    <option value="intro">适合教学</option>
                                    <option value="cover">适合盖牌</option>
                                </select>
                            </div>
                            <div class="filter-group span-2">
                                <label for="scoreRange">最低推荐分 <span class="range-value" id="scoreRangeValue">0.00</span></label>
                                <input id="scoreRange" type="range" min="0" max="100" step="1" value="0">
                            </div>
                        </div>
                    </div>
                </section>

                <section class="panel list-panel">
                    <div class="panel-header">
                        <div>
                            <h2 class="panel-title">候选列表</h2>
                            <div class="panel-subtitle" id="tableSubtitle">按当前筛选条件展示候选。</div>
                        </div>
                    </div>
                    <div class="table-wrap">
                        <table>
                            <thead>
                                <tr>
                                    <th>候选</th>
                                    <th>筛选结果</th>
                                    <th>难度桶</th>
                                    <th>推荐分</th>
                                    <th>随机存活率</th>
                                    <th>死局率</th>
                                    <th>牌数 / 层数</th>
                                    <th>人工标记</th>
                                </tr>
                            </thead>
                            <tbody id="candidateTableBody"></tbody>
                        </table>
                    </div>
                </section>
            </section>

            <aside class="panel detail-panel">
                <div class="panel-header">
                    <div>
                        <h2 class="panel-title">候选详情</h2>
                        <div class="panel-subtitle">用结构预览和关键指标交叉看一局牌到底好不好。</div>
                    </div>
                </div>
                <div class="detail-body" id="detailBody">
                    <div class="detail-empty">左侧先选一条候选，这里会显示详细指标、自动筛选结论、人工标记入口和静态牌桌预览。</div>
                </div>
            </aside>
        </main>
    </div>

    <script id="dashboard-data" type="application/json">{{dashboardJson}}</script>
    <script>
        const dashboardData = JSON.parse(document.getElementById("dashboard-data").textContent);
        const manualMarkOptions = [
            { value: "", label: "未标记" },
            { value: "keep", label: "保留" },
            { value: "review", label: "重点复核" },
            { value: "reject", label: "淘汰" },
            { value: "intro", label: "适合教学" },
            { value: "cover", label: "适合盖牌" }
        ];

        const state = {
            search: "",
            decision: "all",
            difficulty: "all",
            tag: "all",
            manualMarkFilter: "all",
            sort: "score_desc",
            scoreThreshold: 0,
            selectedId: null,
            previewLayer: null
        };

        const storageKey = `tilematcher.analysis.${dashboardData.batchName}.marks`;
        let manualMarks = loadManualMarks();

        const ui = {
            batchName: document.getElementById("batchName"),
            generatedAt: document.getElementById("generatedAt"),
            summaryTotal: document.getElementById("summaryTotal"),
            summaryAccepted: document.getElementById("summaryAccepted"),
            summaryReview: document.getElementById("summaryReview"),
            summaryRejected: document.getElementById("summaryRejected"),
            summaryAcceptedNote: document.getElementById("summaryAcceptedNote"),
            summaryReviewNote: document.getElementById("summaryReviewNote"),
            summaryRejectedNote: document.getElementById("summaryRejectedNote"),
            filteredBadge: document.getElementById("filteredBadge"),
            tableSubtitle: document.getElementById("tableSubtitle"),
            searchInput: document.getElementById("searchInput"),
            decisionSelect: document.getElementById("decisionSelect"),
            difficultySelect: document.getElementById("difficultySelect"),
            tagSelect: document.getElementById("tagSelect"),
            manualMarkFilter: document.getElementById("manualMarkFilter"),
            sortSelect: document.getElementById("sortSelect"),
            scoreRange: document.getElementById("scoreRange"),
            scoreRangeValue: document.getElementById("scoreRangeValue"),
            candidateTableBody: document.getElementById("candidateTableBody"),
            detailBody: document.getElementById("detailBody")
        };

        wireControls();
        populateDynamicFilters();
        render();

        function wireControls() {
            ui.searchInput.addEventListener("input", event => {
                state.search = event.target.value.trim().toLowerCase();
                render();
            });

            ui.decisionSelect.addEventListener("change", event => {
                state.decision = event.target.value;
                render();
            });

            ui.difficultySelect.addEventListener("change", event => {
                state.difficulty = event.target.value;
                render();
            });

            ui.tagSelect.addEventListener("change", event => {
                state.tag = event.target.value;
                render();
            });

            ui.manualMarkFilter.addEventListener("change", event => {
                state.manualMarkFilter = event.target.value;
                render();
            });

            ui.sortSelect.addEventListener("change", event => {
                state.sort = event.target.value;
                render();
            });

            ui.scoreRange.addEventListener("input", event => {
                state.scoreThreshold = Number(event.target.value) / 100;
                ui.scoreRangeValue.textContent = formatScore(state.scoreThreshold);
                render();
            });
        }

        function populateDynamicFilters() {
            ui.batchName.textContent = dashboardData.batchName;
            ui.generatedAt.textContent = formatDateTime(dashboardData.generatedAtUtc);
            ui.scoreRangeValue.textContent = formatScore(state.scoreThreshold);

            const difficulties = [...new Set(dashboardData.candidates.map(candidate => candidate.filterResult.difficultyBucket))];
            difficulties.sort();
            appendOptions(ui.difficultySelect, difficulties);

            const tags = [...new Set(dashboardData.candidates.flatMap(candidate => candidate.filterResult.tags))];
            tags.sort();
            appendOptions(ui.tagSelect, tags);
        }

        function appendOptions(select, values) {
            values.forEach(value => {
                const option = document.createElement("option");
                option.value = value;
                option.textContent = value;
                select.appendChild(option);
            });
        }

        function render() {
            const filteredCandidates = getFilteredCandidates();
            syncSelection(filteredCandidates);
            const selectedCandidate = filteredCandidates.find(candidate => candidate.candidateId === state.selectedId) ?? null;

            renderSummary(filteredCandidates);
            renderTable(filteredCandidates);
            renderDetail(selectedCandidate);
        }

        function getFilteredCandidates() {
            return dashboardData.candidates
                .filter(candidate => matchesFilters(candidate))
                .sort(compareCandidates);
        }

        function matchesFilters(candidate) {
            if (candidate.filterResult.recommendationScore < state.scoreThreshold) {
                return false;
            }

            if (state.decision !== "all" && candidate.filterResult.decision !== state.decision) {
                return false;
            }

            if (state.difficulty !== "all" && candidate.filterResult.difficultyBucket !== state.difficulty) {
                return false;
            }

            if (state.tag !== "all" && !candidate.filterResult.tags.includes(state.tag)) {
                return false;
            }

            const manualMark = getManualMark(candidate.candidateId);
            if (state.manualMarkFilter !== "all" && manualMark !== state.manualMarkFilter) {
                return false;
            }

            if (!state.search) {
                return true;
            }

            const searchIndex = [
                candidate.candidateId,
                candidate.filterResult.decision,
                candidate.filterResult.difficultyBucket,
                candidate.filterResult.tags.join(" "),
                manualMarkLabel(manualMark)
            ].join(" ").toLowerCase();

            return searchIndex.includes(state.search);
        }

        function compareCandidates(left, right) {
            switch (state.sort) {
                case "score_asc":
                    return left.filterResult.recommendationScore - right.filterResult.recommendationScore;
                case "survival_desc":
                    return right.evaluation.randomPlaySurvivalRate - left.evaluation.randomPlaySurvivalRate
                        || right.filterResult.recommendationScore - left.filterResult.recommendationScore;
                case "dead_end_desc":
                    return right.evaluation.deadEndRate - left.evaluation.deadEndRate
                        || right.filterResult.recommendationScore - left.filterResult.recommendationScore;
                case "tile_count_desc":
                    return right.evaluation.tileCount - left.evaluation.tileCount
                        || right.filterResult.recommendationScore - left.filterResult.recommendationScore;
                case "batch_index_asc":
                    return left.batchIndex - right.batchIndex;
                case "score_desc":
                default:
                    return right.filterResult.recommendationScore - left.filterResult.recommendationScore
                        || left.batchIndex - right.batchIndex;
            }
        }

        function syncSelection(filteredCandidates) {
            if (!filteredCandidates.length) {
                state.selectedId = null;
                state.previewLayer = null;
                return;
            }

            const exists = filteredCandidates.some(candidate => candidate.candidateId === state.selectedId);
            if (!exists) {
                state.selectedId = filteredCandidates[0].candidateId;
                state.previewLayer = null;
            }
        }

        function renderSummary(filteredCandidates) {
            const filteredAccepted = filteredCandidates.filter(candidate => candidate.filterResult.decision === "AutoAccept").length;
            const filteredReview = filteredCandidates.filter(candidate => candidate.filterResult.decision === "NeedsReview").length;
            const filteredRejected = filteredCandidates.filter(candidate => candidate.filterResult.decision === "AutoReject").length;

            ui.summaryTotal.textContent = dashboardData.summary.candidateCount;
            ui.summaryAccepted.textContent = dashboardData.summary.acceptedCount;
            ui.summaryReview.textContent = dashboardData.summary.needsReviewCount;
            ui.summaryRejected.textContent = dashboardData.summary.rejectedCount;

            ui.summaryAcceptedNote.textContent = `当前筛选命中 ${filteredAccepted} 条`;
            ui.summaryReviewNote.textContent = `当前筛选命中 ${filteredReview} 条`;
            ui.summaryRejectedNote.textContent = `当前筛选命中 ${filteredRejected} 条`;

            ui.filteredBadge.textContent = `筛选后 ${filteredCandidates.length} / ${dashboardData.summary.candidateCount}`;
            ui.tableSubtitle.textContent = filteredCandidates.length
                ? `共命中 ${filteredCandidates.length} 条，点击任意一条查看详情。`
                : "当前条件下没有候选命中，建议放宽筛选范围。";
        }

        function renderTable(filteredCandidates) {
            if (!filteredCandidates.length) {
                ui.candidateTableBody.innerHTML = `
                    <tr>
                        <td colspan="8">
                            <div class="empty-state">没有匹配结果，可以先降低推荐分阈值或切回全部筛选结果。</div>
                        </td>
                    </tr>`;
                return;
            }

            ui.candidateTableBody.innerHTML = filteredCandidates.map(candidate => {
                const manualMark = getManualMark(candidate.candidateId);
                return `
                    <tr data-candidate-id="${escapeHtml(candidate.candidateId)}" class="${candidate.candidateId === state.selectedId ? "selected" : ""}">
                        <td>
                            <div class="candidate-main">
                                <div class="candidate-title mono">${escapeHtml(candidate.candidateId)}</div>
                                <div class="candidate-sub">
                                    批次序号 ${candidate.batchIndex}
                                    ${candidate.runtimeLevelNumber ? ` · 已进入正式关卡 #${candidate.runtimeLevelNumber}` : ""}
                                </div>
                            </div>
                        </td>
                        <td>${renderChip(formatDecision(candidate.filterResult.decision), chipToneForDecision(candidate.filterResult.decision))}</td>
                        <td>${renderChip(candidate.filterResult.difficultyBucket, chipToneForBucket(candidate.filterResult.difficultyBucket))}</td>
                        <td class="mono">${formatScore(candidate.filterResult.recommendationScore)}</td>
                        <td class="mono">${formatPercent(candidate.evaluation.randomPlaySurvivalRate)}</td>
                        <td class="mono">${formatPercent(candidate.evaluation.deadEndRate)}</td>
                        <td class="mono">${candidate.evaluation.tileCount} / ${candidate.evaluation.layerCount}</td>
                        <td>${manualMark ? renderChip(manualMarkLabel(manualMark), "gold") : '<span class="candidate-sub">未标记</span>'}</td>
                    </tr>`;
            }).join("");

            ui.candidateTableBody.querySelectorAll("tr[data-candidate-id]").forEach(row => {
                row.addEventListener("click", () => {
                    state.selectedId = row.dataset.candidateId;
                    state.previewLayer = null;
                    render();
                });
            });
        }

        function renderDetail(candidate) {
            if (!candidate) {
                ui.detailBody.innerHTML = '<div class="detail-empty">当前筛选结果为空，暂时没有可展示的候选详情。</div>';
                return;
            }

            const hiddenCount = candidate.layout.tiles.filter(tile => tile.faceHiddenInitial).length;
            const maxLayer = Math.max(...candidate.layout.tiles.map(tile => tile.gz));
            if (state.previewLayer === null || state.previewLayer > maxLayer) {
                state.previewLayer = maxLayer;
            }

            ui.detailBody.innerHTML = `
                <section>
                    <h3 class="detail-title mono">${escapeHtml(candidate.candidateId)}</h3>
                    <div class="detail-subtitle">
                        批次序号 ${candidate.batchIndex} · 随机种子 ${candidate.seed}
                        ${candidate.runtimeLevelNumber ? ` · 正式关卡 #${candidate.runtimeLevelNumber}` : " · 当前未进入正式关卡目录"}
                    </div>
                    <div class="chip-row" style="margin-top: 12px;">
                        ${renderChip(formatDecision(candidate.filterResult.decision), chipToneForDecision(candidate.filterResult.decision))}
                        ${renderChip(candidate.filterResult.difficultyBucket, chipToneForBucket(candidate.filterResult.difficultyBucket))}
                        ${candidate.filterResult.needsManualReview ? renderChip("需要人工复核", "berry") : ""}
                        ${hiddenCount > 0 ? renderChip(`初始盖牌 ${hiddenCount}`, "gold") : ""}
                    </div>
                </section>

                <section class="detail-grid">
                    ${renderMetric("推荐分", formatScore(candidate.filterResult.recommendationScore), "综合推荐排序的第一入口")}
                    ${renderMetric("随机存活率", formatPercent(candidate.evaluation.randomPlaySurvivalRate), "越高说明乱走时越不容易直接死局")}
                    ${renderMetric("死局率", formatPercent(candidate.evaluation.deadEndRate), "越高越需要重点检查结构脆弱点")}
                    ${renderMetric("解路径估计", candidate.evaluation.solutionCountEstimate, "当前受限搜索下的估计值")}
                    ${renderMetric("初始分支数", candidate.evaluation.initialBranchCount, "开局可直接操作的合法配对数")}
                    ${renderMetric("平均分支数", candidate.evaluation.averageBranchCount.toFixed(2), "越低越容易收窄成单线题")}
                    ${renderMetric("牌数 / 层数", `${candidate.evaluation.tileCount} / ${candidate.evaluation.layerCount}`, "结构规模")}
                    ${renderMetric("状态搜索", `${candidate.evaluation.searchVisitedStateCount} / ${candidate.evaluation.searchDeadEndStateCount}`, "访问状态数 / 死局状态数")}
                </section>

                <section class="section-card">
                    <h4 class="section-title">自动筛选与人工标记</h4>
                    <div class="manual-mark">
                        <div class="section-copy">
                            这条候选的自动筛选结果是 <strong>${escapeHtml(candidate.filterResult.decision)}</strong>。
                            第一版分析台支持本地人工标记，保存在当前浏览器的本地存储里，不会回写仓库数据。
                        </div>
                        <select id="manualMarkSelect">
                            ${manualMarkOptions.map(option => `
                                <option value="${option.value}" ${option.value === getManualMark(candidate.candidateId) ? "selected" : ""}>${option.label}</option>
                            `).join("")}
                        </select>
                    </div>
                </section>

                <section class="section-card">
                    <h4 class="section-title">标签</h4>
                    <div class="chip-row">
                        ${candidate.filterResult.tags.length
                            ? candidate.filterResult.tags.map(tag => renderChip(tag, chipToneForTag(tag))).join("")
                            : '<span class="section-copy">当前没有额外标签。</span>'}
                    </div>
                </section>

                <section class="section-card">
                    <h4 class="section-title">淘汰原因 / 风险说明</h4>
                    <div class="chip-row">
                        ${candidate.filterResult.rejectReasons.length
                            ? candidate.filterResult.rejectReasons.map(reason => renderChip(reason, "berry")).join("")
                            : '<span class="section-copy">当前没有触发硬约束淘汰原因。</span>'}
                    </div>
                </section>

                <section class="section-card">
                    <div class="preview-header">
                        <div>
                            <h4 class="section-title" style="margin-bottom: 4px;">牌桌结构预览</h4>
                            <div class="section-copy">当前用静态 SVG 表达层级、牌面类型和盖牌标记，先服务于观察与筛选。</div>
                        </div>
                        <div class="layer-controls" id="layerControls"></div>
                    </div>
                    <div class="preview-shell" id="previewHost"></div>
                    <div class="legend">
                        <span class="legend-item"><span class="legend-swatch" style="background:#d9efe1;"></span>条子</span>
                        <span class="legend-item"><span class="legend-swatch" style="background:#fce2d7;"></span>筒子</span>
                        <span class="legend-item"><span class="legend-swatch" style="background:#efe0f7;"></span>万子</span>
                        <span class="legend-item"><span class="legend-swatch" style="background:#f7ecca;"></span>字牌</span>
                        <span class="legend-item"><span class="legend-swatch" style="background:#d7d9dd;"></span>盖牌</span>
                    </div>
                </section>
            `;

            const manualMarkSelect = document.getElementById("manualMarkSelect");
            manualMarkSelect.addEventListener("change", event => {
                setManualMark(candidate.candidateId, event.target.value);
            });

            renderLayerControls(candidate);
            renderPreview(candidate);
        }

        function renderLayerControls(candidate) {
            const host = document.getElementById("layerControls");
            const maxLayer = Math.max(...candidate.layout.tiles.map(tile => tile.gz));
            const layers = [null, ...Array.from({ length: maxLayer + 1 }, (_, index) => index)];
            host.innerHTML = layers.map(layer => {
                const isActive = layer === state.previewLayer;
                const label = layer === null ? "全部层" : `至第 ${layer} 层`;
                return `<button class="layer-button ${isActive ? "active" : ""}" data-layer="${layer === null ? "all" : layer}">${label}</button>`;
            }).join("");

            host.querySelectorAll(".layer-button").forEach(button => {
                button.addEventListener("click", () => {
                    const layerValue = button.dataset.layer;
                    state.previewLayer = layerValue === "all" ? null : Number(layerValue);
                    renderDetail(candidate);
                });
            });
        }

        function renderPreview(candidate) {
            const previewHost = document.getElementById("previewHost");
            const tiles = candidate.layout.tiles
                .filter(tile => state.previewLayer === null || tile.gz <= state.previewLayer)
                .slice()
                .sort((left, right) => left.gz - right.gz || left.gy - right.gy || left.gx - right.gx || left.id - right.id);

            if (!tiles.length) {
                previewHost.innerHTML = '<div class="empty-state">当前层级筛选下没有可显示的牌块。</div>';
                return;
            }

            const minX = Math.min(...tiles.map(tile => tile.gx));
            const minY = Math.min(...tiles.map(tile => tile.gy));
            const maxX = Math.max(...tiles.map(tile => tile.gx + tile.shape.widthUnits));
            const maxY = Math.max(...tiles.map(tile => tile.gy + tile.shape.heightUnits));
            const maxLayer = Math.max(...tiles.map(tile => tile.gz));
            const pad = 2;
            const layerShiftX = 1.35;
            const layerLiftY = 1.7;
            const baseLiftY = maxLayer * layerLiftY;
            const viewWidth = maxX - minX + pad * 2 + maxLayer * layerShiftX;
            const viewHeight = maxY - minY + pad * 2 + baseLiftY;

            const tilesMarkup = tiles.map(tile => {
                const x = pad + (tile.gx - minX) + tile.gz * layerShiftX;
                const y = pad + baseLiftY + (tile.gy - minY) - tile.gz * layerLiftY;
                const width = tile.shape.widthUnits;
                const height = tile.shape.heightUnits;
                const suitClass = tile.faceHiddenInitial ? "hidden" : suitClassForType(tile.type);
                const label = tile.faceHiddenInitial ? "?" : tileLabel(tile.type);
                const labelClass = tile.faceHiddenInitial ? "tile-label hidden" : "tile-label";

                return `
                    <g>
                        <rect class="tile-shadow" x="${(x + 0.55).toFixed(2)}" y="${(y + 0.75).toFixed(2)}" width="${width}" height="${height}" rx="0.6"></rect>
                        <rect class="tile-face ${suitClass}" x="${x.toFixed(2)}" y="${y.toFixed(2)}" width="${width}" height="${height}" rx="0.6"></rect>
                        <text class="${labelClass}" x="${(x + width / 2).toFixed(2)}" y="${(y + height / 2).toFixed(2)}">${escapeHtml(label)}</text>
                    </g>`;
            }).join("");

            previewHost.innerHTML = `
                <svg class="preview-svg" viewBox="0 0 ${viewWidth.toFixed(2)} ${viewHeight.toFixed(2)}" preserveAspectRatio="xMidYMid meet">
                    ${tilesMarkup}
                </svg>
                <div class="preview-meta">
                    当前展示 ${tiles.length} 张牌，最高层 ${maxLayer}。预览偏向结构观察，不等价于运行时最终美术。
                </div>`;
        }

        function renderMetric(label, value, note) {
            return `
                <article class="metric-card">
                    <div class="metric-label">${escapeHtml(label)}</div>
                    <div class="metric-value mono">${escapeHtml(String(value))}</div>
                    <div class="metric-note">${escapeHtml(note)}</div>
                </article>`;
        }

        function renderChip(label, tone) {
            return `<span class="chip ${tone}">${escapeHtml(label)}</span>`;
        }

        function formatDecision(decision) {
            switch (decision) {
                case "AutoAccept":
                    return "自动通过";
                case "NeedsReview":
                    return "待复核";
                case "AutoReject":
                default:
                    return "自动淘汰";
            }
        }

        function chipToneForDecision(decision) {
            switch (decision) {
                case "AutoAccept":
                    return "teal";
                case "NeedsReview":
                    return "orange";
                case "AutoReject":
                default:
                    return "berry";
            }
        }

        function chipToneForBucket(bucket) {
            switch (bucket) {
                case "easy_core":
                    return "teal";
                case "normal_core":
                    return "orange";
                case "hard_core":
                    return "berry";
                default:
                    return "gold";
            }
        }

        function chipToneForTag(tag) {
            if (tag.includes("risk")) {
                return "berry";
            }

            if (tag.includes("intro")) {
                return "teal";
            }

            if (tag.includes("cover")) {
                return "gold";
            }

            return "orange";
        }

        function formatPercent(value) {
            return `${(value * 100).toFixed(1)}%`;
        }

        function formatScore(value) {
            return Number(value).toFixed(2);
        }

        function formatDateTime(isoText) {
            const date = new Date(isoText);
            return Number.isNaN(date.getTime()) ? isoText : date.toLocaleString("zh-CN", { hour12: false });
        }

        function suitClassForType(type) {
            if (type.startsWith("Bam")) {
                return "suit-bam";
            }

            if (type.startsWith("Dot")) {
                return "suit-dot";
            }

            if (type.startsWith("Chr")) {
                return "suit-chr";
            }

            return "suit-honor";
        }

        function tileLabel(type) {
            if (type.startsWith("Bam")) {
                return `B${type.slice(3)}`;
            }

            if (type.startsWith("Dot")) {
                return `D${type.slice(3)}`;
            }

            if (type.startsWith("Chr")) {
                return `C${type.slice(3)}`;
            }

            switch (type) {
                case "East":
                    return "E";
                case "South":
                    return "S";
                case "West":
                    return "W";
                case "North":
                    return "N";
                case "Red":
                    return "R";
                case "Green":
                    return "G";
                case "White":
                    return "Wh";
                default:
                    return type.slice(0, 2);
            }
        }

        function loadManualMarks() {
            try {
                const raw = localStorage.getItem(storageKey);
                return raw ? JSON.parse(raw) : {};
            } catch (error) {
                return {};
            }
        }

        function saveManualMarks() {
            try {
                localStorage.setItem(storageKey, JSON.stringify(manualMarks));
            } catch (error) {
                // 当前第一版分析台允许在无法写入 localStorage 时静默降级。
            }
        }

        function getManualMark(candidateId) {
            return manualMarks[candidateId] || "";
        }

        function setManualMark(candidateId, value) {
            if (!value) {
                delete manualMarks[candidateId];
            } else {
                manualMarks[candidateId] = value;
            }

            saveManualMarks();
            render();
        }

        function manualMarkLabel(mark) {
            const match = manualMarkOptions.find(option => option.value === mark);
            return match ? match.label : "未标记";
        }

        function escapeHtml(value) {
            return String(value)
                .replaceAll("&", "&amp;")
                .replaceAll("<", "&lt;")
                .replaceAll(">", "&gt;")
                .replaceAll('"', "&quot;")
                .replaceAll("'", "&#39;");
        }
    </script>
</body>
</html>
""";
    }
}
