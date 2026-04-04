using System.Text.Json;

namespace TileMatcher.Offline;

internal static partial class OfflinePipeline
{
    private static readonly string[] PairTypePool =
    [
        "Bam1", "Bam2", "Bam3", "Bam4", "Bam5", "Bam6", "Bam7", "Bam8", "Bam9",
        "Dot1", "Dot2", "Dot3", "Dot4", "Dot5", "Dot6", "Dot7", "Dot8", "Dot9",
        "Chr1", "Chr2", "Chr3", "Chr4", "Chr5", "Chr6", "Chr7", "Chr8", "Chr9",
        "East", "South", "West", "North", "Red", "Green", "White",
    ];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    public static OfflineBatchSummary Run(OfflineBatchConfig config, string repoRoot)
    {
        var analysisDir = ResolveOutputPath(repoRoot, config.AnalysisOutputDir);
        var runtimeDir = ResolveOutputPath(repoRoot, config.RuntimeOutputDir);
        Directory.CreateDirectory(analysisDir);
        Directory.CreateDirectory(runtimeDir);

        var rng = config.RandomSeed.HasValue ? new Random(config.RandomSeed.Value) : new Random();
        var candidates = new List<OfflineCandidateRecord>();
        var batchAttempts = 0;
        var maxBatchAttempts = Math.Max(config.CandidateCount * 10, config.CandidateCount + 8);

        while (candidates.Count < config.CandidateCount && batchAttempts < maxBatchAttempts)
        {
            batchAttempts++;
            var candidateSeed = rng.Next();
            try
            {
                var layout = GenerateCandidate(candidates.Count + 1, candidateSeed, config.Rules);
                var evaluation = Evaluate(layout, config.Filter, candidateSeed);
                var filterResult = Filter(layout, evaluation, config.Filter);
                candidates.Add(new OfflineCandidateRecord
                {
                    CandidateId = layout.CandidateId,
                    BatchIndex = candidates.Count + 1,
                    Seed = candidateSeed,
                    Layout = layout,
                    Evaluation = evaluation,
                    FilterResult = filterResult,
                });
            }
            catch (InvalidOperationException)
            {
                // 单个随机种子失败时直接跳过，避免整批离线构造中断。
            }
        }

        if (candidates.Count == 0)
        {
            throw new InvalidOperationException("离线批次未生成任何候选牌局。");
        }

        var accepted = candidates
            .Where(candidate => candidate.FilterResult.Decision == OfflineFilterDecision.AutoAccept)
            .OrderByDescending(candidate => candidate.FilterResult.RecommendationScore)
            .Take(config.MaxAcceptedLevels)
            .ToList();

        var review = candidates
            .Where(candidate => candidate.FilterResult.Decision == OfflineFilterDecision.NeedsReview)
            .OrderByDescending(candidate => candidate.FilterResult.RecommendationScore)
            .ToList();

        ExportAnalysisFiles(config, analysisDir, candidates, accepted, review);
        ExportRuntimeLevels(runtimeDir, accepted);

        return new OfflineBatchSummary
        {
            BatchName = config.BatchName,
            CandidateCount = candidates.Count,
            AcceptedCount = accepted.Count,
            NeedsReviewCount = review.Count,
            RejectedCount = candidates.Count(candidate => candidate.FilterResult.Decision == OfflineFilterDecision.AutoReject),
            AnalysisOutputDir = analysisDir,
            RuntimeOutputDir = runtimeDir,
        };
    }

    private static void ExportAnalysisFiles(
        OfflineBatchConfig config,
        string analysisDir,
        IReadOnlyList<OfflineCandidateRecord> candidates,
        IReadOnlyList<OfflineCandidateRecord> accepted,
        IReadOnlyList<OfflineCandidateRecord> review)
    {
        WriteJson(Path.Combine(analysisDir, "candidates.json"), candidates);
        WriteJson(Path.Combine(analysisDir, "accepted.json"), accepted);
        WriteJson(Path.Combine(analysisDir, "needs-review.json"), review);
        WriteJson(
            Path.Combine(analysisDir, "summary.json"),
            new
            {
                config.BatchName,
                config.CandidateCount,
                AcceptedCount = accepted.Count,
                NeedsReviewCount = review.Count,
                RejectedCount = candidates.Count(candidate => candidate.FilterResult.Decision == OfflineFilterDecision.AutoReject),
                GeneratedAtUtc = DateTime.UtcNow,
            });
    }

    private static void ExportRuntimeLevels(string runtimeDir, IReadOnlyList<OfflineCandidateRecord> accepted)
    {
        var levelsDir = Path.Combine(runtimeDir, "levels");
        Directory.CreateDirectory(levelsDir);
        var catalog = new List<object>();

        for (var index = 0; index < accepted.Count; index++)
        {
            var levelNumber = index + 1;
            var runtimeLevel = new OfflineRuntimeLevel
            {
                LevelNumber = levelNumber,
                CandidateId = accepted[index].CandidateId,
                DifficultyBucket = accepted[index].FilterResult.DifficultyBucket,
                RecommendationScore = accepted[index].FilterResult.RecommendationScore,
                Layout = accepted[index].Layout,
            };

            var fileName = $"level_{levelNumber:000}.json";
            WriteJson(Path.Combine(levelsDir, fileName), runtimeLevel);
            catalog.Add(new
            {
                LevelNumber = levelNumber,
                runtimeLevel.CandidateId,
                runtimeLevel.DifficultyBucket,
                runtimeLevel.RecommendationScore,
                FileName = fileName,
            });
        }

        WriteJson(Path.Combine(runtimeDir, "level-catalog.json"), catalog);
    }

    private static string ResolveOutputPath(string repoRoot, string configuredPath)
    {
        return Path.GetFullPath(Path.Combine(repoRoot, configuredPath));
    }

    private static void WriteJson<T>(string path, T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(value, JsonOptions));
    }

    private static OfflineLevelLayout GenerateCandidate(int levelId, int seed, OfflineLayoutRules rules)
    {
        for (var attempt = 1; attempt <= rules.GenerationMaxAttempts; attempt++)
        {
            var rng = new Random(seed + attempt - 1);
            var shape = new OfflineTileShape { WidthUnits = rules.TileWidthUnits, HeightUnits = rules.TileHeightUnits };
            var width = rng.Next(rules.RandomWidthMin, rules.RandomWidthMax + 1);
            var height = rng.Next(rules.RandomHeightMin, rules.RandomHeightMax + 1);
            var bottomLayer = BuildBottomLayer(rng, width, height, rules);
            var layers = new List<List<(int X, int Y)>> { bottomLayer };
            var previousLayer = CreateTemporaryLayer(bottomLayer, 0, shape);
            var targetLayerCount = rng.Next(rules.MinLayerCount, rules.MaxLayerCount + 1);

            for (var z = 1; z < targetLayerCount; z++)
            {
                var layerOffset = ResolveLayerOffset(z, rules, shape);
                var candidates = GetSupportedCandidates(previousLayer, shape, layerOffset);
                if (candidates.Count == 0)
                {
                    break;
                }

                var minCount = Math.Max(1, Math.Min(candidates.Count, Math.Max(1, previousLayer.Count / 2)));
                var maxCount = Math.Max(1, Math.Min(candidates.Count, previousLayer.Count - 1));
                if (maxCount < minCount)
                {
                    break;
                }

                var targetCount = rng.Next(minCount, maxCount + 1);
                Shuffle(rng, candidates);
                var nextLayer = SelectNonOverlappingCandidates(candidates, targetCount, shape)
                    .OrderBy(position => position.Y)
                    .ThenBy(position => position.X)
                    .ToList();

                if (nextLayer.Count == 0 || nextLayer.Count >= previousLayer.Count)
                {
                    break;
                }

                layers.Add(nextLayer);
                previousLayer = CreateTemporaryLayer(nextLayer, z, shape);
            }

            var generatedTiles = new List<OfflineTileData>();
            var nextId = 1;
            for (var z = 0; z < layers.Count; z++)
            {
                foreach (var position in layers[z])
                {
                    generatedTiles.Add(new OfflineTileData
                    {
                        Id = nextId++,
                        Type = string.Empty,
                        GX = position.X,
                        GY = position.Y,
                        GZ = z,
                        Shape = shape,
                    });
                }
            }

            if (!ValidateLayout(generatedTiles, rules))
            {
                continue;
            }

            if (!TryAssignSolvablePairs(generatedTiles, rng, out var typeByTileId))
            {
                continue;
            }

            return new OfflineLevelLayout
            {
                CandidateId = $"candidate_{levelId:0000}_{seed}",
                LevelId = levelId,
                Tiles = generatedTiles.Select(tile => new OfflineTileData
                {
                    Id = tile.Id,
                    Type = typeByTileId[tile.Id],
                    GX = tile.GX,
                    GY = tile.GY,
                    GZ = tile.GZ,
                    Shape = new OfflineTileShape
                    {
                        WidthUnits = tile.Shape.WidthUnits,
                        HeightUnits = tile.Shape.HeightUnits,
                    },
                }).ToList(),
            };
        }

        throw new InvalidOperationException($"未能在限定次数内生成候选牌局。seed={seed}");
    }

    private static OfflineEvaluation Evaluate(OfflineLevelLayout layout, OfflineFilterRules rules, int randomSeed)
    {
        if (layout.Tiles.Count > 64)
        {
            throw new InvalidOperationException("当前 MVP 评估器只支持最多 64 张牌。");
        }

        var initialMask = BuildInitialMask(layout.Tiles.Count);
        var stateCache = new Dictionary<ulong, StateEvaluation>();
        var initialPairs = GetLegalPairs(layout.Tiles, initialMask);
        var state = EvaluateState(layout.Tiles, initialMask, rules.MaxSolutionCountSearch, stateCache);

        return new OfflineEvaluation
        {
            HasSolution = state.HasSolution,
            SolutionCountEstimate = state.SolutionCountEstimate,
            InitialBranchCount = initialPairs.Count,
            AverageBranchCount = state.NonTerminalStateCount == 0
                ? 0.0
                : (double)state.BranchSum / state.NonTerminalStateCount,
            DeadEndRate = state.VisitedStateCount == 0
                ? 0.0
                : (double)state.DeadEndStateCount / state.VisitedStateCount,
            RandomPlaySurvivalRate = EstimateRandomPlaySurvival(layout.Tiles, rules.RandomSimulationCount, randomSeed),
            TileCount = layout.Tiles.Count,
            LayerCount = layout.Tiles.Select(tile => tile.GZ).Distinct().Count(),
            SearchVisitedStateCount = state.VisitedStateCount,
            SearchDeadEndStateCount = state.DeadEndStateCount,
        };
    }

    private static OfflineFilterResult Filter(
        OfflineLevelLayout layout,
        OfflineEvaluation evaluation,
        OfflineFilterRules rules)
    {
        var rejectReasons = new List<string>();
        if (!evaluation.HasSolution)
        {
            rejectReasons.Add("不存在完整通关路径");
        }

        if (evaluation.TileCount < rules.MinTileCount || evaluation.TileCount > rules.MaxTileCount)
        {
            rejectReasons.Add($"牌数不在目标区间：{evaluation.TileCount}");
        }

        if (evaluation.LayerCount < rules.MinLayerCount || evaluation.LayerCount > rules.MaxLayerCount)
        {
            rejectReasons.Add($"层数不在目标区间：{evaluation.LayerCount}");
        }

        if (evaluation.InitialBranchCount < rules.MinInitialBranchCount)
        {
            rejectReasons.Add($"初始分支不足：{evaluation.InitialBranchCount}");
        }

        if (evaluation.SolutionCountEstimate < rules.MinSolutionCountEstimate)
        {
            rejectReasons.Add($"可解路径估计过低：{evaluation.SolutionCountEstimate}");
        }

        if (evaluation.DeadEndRate > rules.MaxDeadEndRate)
        {
            rejectReasons.Add($"死局率过高：{evaluation.DeadEndRate:F3}");
        }

        if (evaluation.RandomPlaySurvivalRate < rules.MinRandomPlaySurvivalRate)
        {
            rejectReasons.Add($"随机推进存活率过低：{evaluation.RandomPlaySurvivalRate:F3}");
        }

        if (rejectReasons.Count > 0)
        {
            return new OfflineFilterResult
            {
                Decision = OfflineFilterDecision.AutoReject,
                DifficultyBucket = "rejected",
                RecommendationScore = 0.0,
                RejectReasons = rejectReasons,
                Tags = BuildTags(evaluation, "rejected"),
            };
        }

        var score = BuildRecommendationScore(evaluation);
        var bucket = ResolveDifficultyBucket(evaluation);
        var decision = score >= rules.AutoAcceptScore
            ? OfflineFilterDecision.AutoAccept
            : score >= rules.NeedsReviewScore
                ? OfflineFilterDecision.NeedsReview
                : OfflineFilterDecision.AutoReject;

        if (decision == OfflineFilterDecision.AutoReject)
        {
            rejectReasons.Add("综合推荐分未达到最低阈值");
        }

        return new OfflineFilterResult
        {
            Decision = decision,
            DifficultyBucket = decision == OfflineFilterDecision.AutoReject ? "rejected" : bucket,
            RecommendationScore = score,
            RejectReasons = rejectReasons,
            Tags = BuildTags(evaluation, bucket),
        };
    }

    private static double BuildRecommendationScore(OfflineEvaluation evaluation)
    {
        var branchScore = Math.Clamp(evaluation.AverageBranchCount / 4.0, 0.0, 1.0);
        var survivalScore = Math.Clamp(evaluation.RandomPlaySurvivalRate, 0.0, 1.0);
        var deadEndPenalty = 1.0 - Math.Clamp(evaluation.DeadEndRate, 0.0, 1.0);
        var solutionScore = Math.Clamp(evaluation.SolutionCountEstimate / 8.0, 0.0, 1.0);
        return Math.Round(
            branchScore * 0.28
            + survivalScore * 0.34
            + deadEndPenalty * 0.24
            + solutionScore * 0.14,
            4);
    }

    private static string ResolveDifficultyBucket(OfflineEvaluation evaluation)
    {
        if (evaluation.RandomPlaySurvivalRate >= 0.45 && evaluation.AverageBranchCount >= 2.8)
        {
            return "easy_core";
        }

        if (evaluation.RandomPlaySurvivalRate >= 0.24 && evaluation.AverageBranchCount >= 1.8)
        {
            return "normal_core";
        }

        return "hard_core";
    }

    private static List<string> BuildTags(OfflineEvaluation evaluation, string bucket)
    {
        var tags = new List<string> { bucket };
        if (evaluation.InitialBranchCount >= 4)
        {
            tags.Add("good_for_intro");
        }

        if (evaluation.RandomPlaySurvivalRate >= 0.35)
        {
            tags.Add("good_for_cover_variant");
        }

        if (evaluation.DeadEndRate >= 0.25)
        {
            tags.Add("high_dead_end_risk");
        }

        if (evaluation.AverageBranchCount < 1.6)
        {
            tags.Add("low_branch_risk");
        }

        return tags;
    }

    private static List<(int X, int Y)> BuildBottomLayer(Random rng, int width, int height, OfflineLayoutRules rules)
    {
        var bottomLayer = new List<(int X, int Y)>();
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var isCorner = (x == 0 || x == width - 1) && (y == 0 || y == height - 1);
                if (!isCorner && rng.NextDouble() < rules.BottomLayerHoleChance)
                {
                    continue;
                }

                bottomLayer.Add((x * rules.BottomLayerStepX, y * rules.BottomLayerStepY));
            }
        }

        if (bottomLayer.Count < rules.MinBottomLayerTileCount)
        {
            bottomLayer.Clear();
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    bottomLayer.Add((x * rules.BottomLayerStepX, y * rules.BottomLayerStepY));
                }
            }
        }

        return bottomLayer;
    }

    private static List<(int X, int Y)> GetSupportedCandidates(
        IReadOnlyCollection<OfflineTileData> lowerLayer,
        OfflineTileShape tileShape,
        (int X, int Y) layerOffset)
    {
        var candidates = new HashSet<(int X, int Y)>();
        var minX = lowerLayer.Min(tile => tile.GX);
        var minY = lowerLayer.Min(tile => tile.GY);
        var maxX = lowerLayer.Max(tile => tile.GX + tile.FootprintWidth);
        var maxY = lowerLayer.Max(tile => tile.GY + tile.FootprintHeight);
        var startX = GetFirstAlignedCoordinate(minX, tileShape.WidthUnits, layerOffset.X);
        var startY = GetFirstAlignedCoordinate(minY, tileShape.HeightUnits, layerOffset.Y);

        for (var y = startY; y <= maxY - tileShape.HeightUnits; y += tileShape.HeightUnits)
        {
            for (var x = startX; x <= maxX - tileShape.WidthUnits; x += tileShape.WidthUnits)
            {
                var candidate = new OfflineTileData
                {
                    GX = x,
                    GY = y,
                    GZ = lowerLayer.First().GZ + 1,
                    Shape = tileShape,
                };

                if (HasFullSupportFromLowerLayer(candidate, lowerLayer))
                {
                    candidates.Add((x, y));
                }
            }
        }

        return candidates.ToList();
    }

    private static List<(int X, int Y)> SelectNonOverlappingCandidates(
        IReadOnlyList<(int X, int Y)> candidates,
        int targetCount,
        OfflineTileShape tileShape)
    {
        var selected = new List<(int X, int Y)>();
        foreach (var candidate in candidates)
        {
            if (selected.Count >= targetCount)
            {
                break;
            }

            if (selected.All(existing => !DoAnchorsOverlap(existing, candidate, tileShape)))
            {
                selected.Add(candidate);
            }
        }

        return selected;
    }

    private static List<OfflineTileData> CreateTemporaryLayer(
        IEnumerable<(int X, int Y)> anchors,
        int layer,
        OfflineTileShape shape)
    {
        return anchors.Select(anchor => new OfflineTileData
        {
            GX = anchor.X,
            GY = anchor.Y,
            GZ = layer,
            Shape = shape,
        }).ToList();
    }

    private static (int X, int Y) ResolveLayerOffset(int layer, OfflineLayoutRules rules, OfflineTileShape shape)
    {
        if (layer % 2 == 0)
        {
            return (0, 0);
        }

        var halfX = shape.WidthUnits / 2;
        var halfY = shape.HeightUnits / 2;
        return rules.UpperLayerOffsetMode switch
        {
            OfflineLayerOffsetMode.HalfX => (halfX, 0),
            OfflineLayerOffsetMode.HalfY => (0, halfY),
            OfflineLayerOffsetMode.HalfXY => (halfX, halfY),
            _ => (0, 0),
        };
    }

    private static int GetFirstAlignedCoordinate(int minValue, int step, int offset)
    {
        var value = offset;
        while (value < minValue)
        {
            value += step;
        }

        return value;
    }

    private static bool DoAnchorsOverlap((int X, int Y) a, (int X, int Y) b, OfflineTileShape shape)
    {
        return a.X < b.X + shape.WidthUnits
            && a.X + shape.WidthUnits > b.X
            && a.Y < b.Y + shape.HeightUnits
            && a.Y + shape.HeightUnits > b.Y;
    }

    private static void Shuffle<T>(Random rng, IList<T> items)
    {
        for (var i = items.Count - 1; i > 0; i--)
        {
            var swapIndex = rng.Next(i + 1);
            (items[i], items[swapIndex]) = (items[swapIndex], items[i]);
        }
    }
}
