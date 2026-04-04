using System.Text.Json;
using TileMatcher.Offline;

namespace TileMatcher.Offline.Tests;

internal static class OfflinePipelineTests
{
    public static IReadOnlyList<TestCaseResult> RunAll(string repoRoot)
    {
        return
        [
            Run(nameof(Config_Defaults_AreStable), Config_Defaults_AreStable),
            Run(nameof(EvaluateLayout_FindsSolution_ForSimplePair), EvaluateLayout_FindsSolution_ForSimplePair),
            Run(nameof(EvaluateLayout_RejectsUnsolvableLayout), EvaluateLayout_RejectsUnsolvableLayout),
            Run(nameof(FilterLayout_ProducesExpectedDecisions), FilterLayout_ProducesExpectedDecisions),
            Run(nameof(ExportRuntimeLevels_WritesStableCatalogAndFiles), () => ExportRuntimeLevels_WritesStableCatalogAndFiles(repoRoot)),
        ];
    }

    private static TestCaseResult Run(string name, Action action)
    {
        try
        {
            action();
            return TestCaseResult.CreatePassed(name);
        }
        catch (Exception exception)
        {
            return TestCaseResult.CreateFailed(name, exception.Message);
        }
    }

    private static void Config_Defaults_AreStable()
    {
        var config = new OfflineBatchConfig();
        TestAssert.Equal("mvp-batch", config.BatchName, "Default batch name changed unexpectedly.");
        TestAssert.Equal(24, config.CandidateCount, "Default candidate count changed unexpectedly.");
        TestAssert.Equal(8, config.MaxAcceptedLevels, "Default accepted export count changed unexpectedly.");
        TestAssert.Equal("artifacts/mahjong-mvp/analysis", config.AnalysisOutputDir, "Default analysis output directory should stay stable.");
        TestAssert.Equal("artifacts/mahjong-mvp/runtime-levels", config.RuntimeOutputDir, "Default runtime output directory should stay stable.");
        TestAssert.Equal(48, config.Filter.RandomSimulationCount, "Default random simulation count should stay stable.");
        TestAssert.Equal(int.MaxValue, config.HiddenFace.StartLevel, "Default hidden-face start level should keep the feature disabled.");
        TestAssert.Equal(0, config.HiddenFace.HiddenCount, "Default hidden-face count should keep the feature disabled.");
    }

    private static void EvaluateLayout_FindsSolution_ForSimplePair()
    {
        var layout = CreateLayout(
            "solvable_pair",
            (1, "Bam1", 0, 0, 0),
            (2, "Bam1", 4, 0, 0));

        var evaluation = OfflineTestHooks.EvaluateLayout(layout, new OfflineFilterRules(), 1234);
        TestAssert.True(evaluation.HasSolution, "A minimal matching pair should be solvable.");
        TestAssert.Equal(1, evaluation.InitialBranchCount, "A minimal matching pair should expose one initial branch.");
        TestAssert.Equal(1, evaluation.SolutionCountEstimate, "A minimal matching pair should expose one solution.");
        TestAssert.NearlyEqual(1.0, evaluation.RandomPlaySurvivalRate, 0.0001, "A single legal path should always survive random play.");
        TestAssert.Equal(2, evaluation.TileCount, "The minimal solvable sample should contain two tiles.");
    }

    private static void EvaluateLayout_RejectsUnsolvableLayout()
    {
        var layout = CreateLayout(
            "unsolvable_pair",
            (1, "Bam1", 0, 0, 0),
            (2, "Dot1", 4, 0, 0));

        var evaluation = OfflineTestHooks.EvaluateLayout(layout, new OfflineFilterRules(), 5678);
        TestAssert.True(!evaluation.HasSolution, "A mismatched pair should not be solvable.");
        TestAssert.Equal(0, evaluation.InitialBranchCount, "An unsolvable pair should expose no initial branch.");
        TestAssert.Equal(0, evaluation.SolutionCountEstimate, "An unsolvable pair should expose zero solutions.");
        TestAssert.NearlyEqual(0.0, evaluation.RandomPlaySurvivalRate, 0.0001, "An unsolvable pair should never survive random play.");
    }

    private static void FilterLayout_ProducesExpectedDecisions()
    {
        var layout = CreateLayout(
            "filter_layout",
            (1, "Bam1", 0, 0, 0),
            (2, "Bam1", 4, 0, 0),
            (3, "Dot1", 0, 6, 1),
            (4, "Dot1", 4, 6, 1));

        var rules = new OfflineFilterRules
        {
            MinTileCount = 4,
            MaxTileCount = 16,
            MinLayerCount = 2,
            MaxLayerCount = 4,
            MinInitialBranchCount = 1,
            MinSolutionCountEstimate = 1,
            MaxDeadEndRate = 0.50,
            MinRandomPlaySurvivalRate = 0.20,
            AutoAcceptScore = 0.62,
            NeedsReviewScore = 0.42,
        };

        var acceptedEvaluation = new OfflineEvaluation
        {
            HasSolution = true,
            SolutionCountEstimate = 6,
            InitialBranchCount = 3,
            AverageBranchCount = 2.8,
            DeadEndRate = 0.08,
            RandomPlaySurvivalRate = 0.58,
            TileCount = 4,
            LayerCount = 2,
        };

        var reviewEvaluation = new OfflineEvaluation
        {
            HasSolution = true,
            SolutionCountEstimate = 4,
            InitialBranchCount = 3,
            AverageBranchCount = 1.5,
            DeadEndRate = 0.10,
            RandomPlaySurvivalRate = 0.40,
            TileCount = 4,
            LayerCount = 2,
        };

        var rejectedEvaluation = new OfflineEvaluation
        {
            HasSolution = true,
            SolutionCountEstimate = 6,
            InitialBranchCount = 0,
            AverageBranchCount = 2.8,
            DeadEndRate = 0.08,
            RandomPlaySurvivalRate = 0.05,
            TileCount = 4,
            LayerCount = 2,
        };

        var accepted = OfflineTestHooks.FilterLayout(layout, acceptedEvaluation, rules);
        var review = OfflineTestHooks.FilterLayout(layout, reviewEvaluation, rules);
        var rejected = OfflineTestHooks.FilterLayout(layout, rejectedEvaluation, rules);

        TestAssert.Equal(OfflineFilterDecision.AutoAccept, accepted.Decision, "High quality evaluations should auto-accept.");
        TestAssert.Equal("easy_core", accepted.DifficultyBucket, "Strong branch density and survival should map to easy_core.");
        TestAssert.Contains("good_for_cover_variant", accepted.Tags, "High survival samples should retain the cover-variant tag.");

        TestAssert.Equal(OfflineFilterDecision.NeedsReview, review.Decision, "Mid-range evaluations should route to review.");
        TestAssert.Equal("hard_core", review.DifficultyBucket, "Low branch density samples should map to hard_core.");
        TestAssert.Contains("low_branch_risk", review.Tags, "Low branch samples should carry the low_branch_risk tag.");

        TestAssert.Equal(OfflineFilterDecision.AutoReject, rejected.Decision, "Hard constraint failures should auto-reject.");
        TestAssert.Equal("rejected", rejected.DifficultyBucket, "Rejected samples should stay in the rejected bucket.");
        TestAssert.Contains("初始分支", rejected.RejectReasons, "Reject reasons should record the missing initial branch.");
    }

    private static void ExportRuntimeLevels_WritesStableCatalogAndFiles(string repoRoot)
    {
        var runtimeDir = Path.Combine(repoRoot, "artifacts", "test-results", "offline-runtime-export");
        if (Directory.Exists(runtimeDir))
        {
            Directory.Delete(runtimeDir, recursive: true);
        }

        var accepted = new List<OfflineCandidateRecord>
        {
            CreateAcceptedRecord(1, "candidate_alpha", "normal_core", 0.7123),
            CreateAcceptedRecord(2, "candidate_beta", "hard_core", 0.5345),
        };

        OfflineTestHooks.ExportRuntimeLevels(
            runtimeDir,
            accepted,
            new OfflineHiddenFaceConfig
            {
                StartLevel = 1,
                HiddenCount = 1,
                MaxHiddenCount = 2,
            });

        var catalogPath = Path.Combine(runtimeDir, "level-catalog.json");
        var levelOnePath = Path.Combine(runtimeDir, "levels", "level_001.json");
        var levelTwoPath = Path.Combine(runtimeDir, "levels", "level_002.json");

        TestAssert.True(File.Exists(catalogPath), "The runtime export should write level-catalog.json.");
        TestAssert.True(File.Exists(levelOnePath), "The runtime export should write level_001.json.");
        TestAssert.True(File.Exists(levelTwoPath), "The runtime export should write level_002.json.");

        var catalog = JsonSerializer.Deserialize<List<RuntimeCatalogEntry>>(File.ReadAllText(catalogPath));
        var levelOne = JsonSerializer.Deserialize<OfflineRuntimeLevel>(File.ReadAllText(levelOnePath));
        var levelTwo = JsonSerializer.Deserialize<OfflineRuntimeLevel>(File.ReadAllText(levelTwoPath));

        TestAssert.True(catalog is not null && catalog.Count == 2, "The catalog should contain exactly two entries.");
        TestAssert.Equal(1, catalog![0].LevelNumber, "The first catalog entry should be level 1.");
        TestAssert.Equal("level_001.json", catalog[0].FileName, "The first catalog file name should stay stable.");
        TestAssert.Equal(2, catalog[1].LevelNumber, "The second catalog entry should be level 2.");
        TestAssert.Equal("level_002.json", catalog[1].FileName, "The second catalog file name should stay stable.");

        TestAssert.True(levelOne is not null, "The first exported level should deserialize.");
        TestAssert.True(levelTwo is not null, "The second exported level should deserialize.");
        TestAssert.Equal(1, levelOne!.LevelNumber, "The first exported level should keep level number 1.");
        TestAssert.Equal("candidate_alpha", levelOne.CandidateId, "The first exported level should retain its candidate id.");
        TestAssert.Equal(2, levelTwo!.LevelNumber, "The second exported level should keep level number 2.");
        TestAssert.Equal("candidate_beta", levelTwo.CandidateId, "The second exported level should retain its candidate id.");
        TestAssert.Equal(1, levelOne.Layout.Tiles.Count(tile => tile.FaceHiddenInitial), "The first exported level should mark one tile as initially hidden.");
        TestAssert.Equal(1, levelTwo.Layout.Tiles.Count(tile => tile.FaceHiddenInitial), "The second exported level should mark one tile as initially hidden.");
    }

    private static OfflineLevelLayout CreateLayout(string candidateId, params (int Id, string Type, int GX, int GY, int GZ)[] tiles)
    {
        return new OfflineLevelLayout
        {
            CandidateId = candidateId,
            LevelId = 1,
            Tiles = tiles.Select(tile => new OfflineTileData
            {
                Id = tile.Id,
                Type = tile.Type,
                GX = tile.GX,
                GY = tile.GY,
                GZ = tile.GZ,
                Shape = new OfflineTileShape
                {
                    WidthUnits = 4,
                    HeightUnits = 6,
                },
            }).ToList(),
        };
    }

    private static OfflineCandidateRecord CreateAcceptedRecord(
        int levelId,
        string candidateId,
        string difficultyBucket,
        double recommendationScore)
    {
        return new OfflineCandidateRecord
        {
            CandidateId = candidateId,
            BatchIndex = levelId,
            Seed = levelId * 100,
            Layout = CreateLayout(
                candidateId,
                (1, "Bam1", 0, 0, 0),
                (2, "Bam1", 4, 0, 0)),
            Evaluation = new OfflineEvaluation
            {
                HasSolution = true,
                SolutionCountEstimate = 1,
                InitialBranchCount = 1,
                AverageBranchCount = 1.0,
                DeadEndRate = 0.0,
                RandomPlaySurvivalRate = 1.0,
                TileCount = 2,
                LayerCount = 1,
            },
            FilterResult = new OfflineFilterResult
            {
                Decision = OfflineFilterDecision.AutoAccept,
                DifficultyBucket = difficultyBucket,
                RecommendationScore = recommendationScore,
            },
        };
    }

    private sealed class RuntimeCatalogEntry
    {
        public int LevelNumber { get; set; }
        public string CandidateId { get; set; } = string.Empty;
        public string DifficultyBucket { get; set; } = string.Empty;
        public double RecommendationScore { get; set; }
        public string FileName { get; set; } = string.Empty;
    }
}
