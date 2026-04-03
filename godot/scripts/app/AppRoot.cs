using System;
using Godot;
using TileMatcher.Boot;
using TileMatcher.Config;
using TileMatcher.Game;
using TileMatcher.Home;
using TileMatcher.Result;

namespace TileMatcher.App;

/// <summary>
/// 外围流程主路由。
/// 当前版本负责把启动页、主页、游戏页、结算页和每日奖励页串成一条完整的外壳流程。
/// </summary>
public partial class AppRoot : Node
{
    private const string DefaultLevelCatalogPath = "res://configs/levels/default_levels.tres";
    private const string DefaultProfileCatalogPath = "res://configs/layout_profiles/default_catalog.tres";
    private const int DailyRewardLeafCount = 3;

    [Export]
    public PackedScene BootLoadingPageScene { get; set; } = null!;

    [Export]
    public PackedScene HomePageScene { get; set; } = null!;

    [Export]
    public PackedScene GamePageScene { get; set; } = null!;

    [Export]
    public PackedScene LevelCompletePageScene { get; set; } = null!;

    [Export]
    public PackedScene DailyRewardPageScene { get; set; } = null!;

    [Export]
    public LevelCatalog LevelCatalog { get; set; } = null!;

    [Export]
    public LayoutProfileCatalog ProfileCatalog { get; set; } = null!;

    private Node _currentPage = null!;
    private int _currentLevelNumber = 1;
    private PlayerProgressData _progress = new();
    private DailyRewardSummary? _pendingDailyRewardSummary;

    public override void _Ready()
    {
        BootLoadingPageScene ??= GD.Load<PackedScene>("res://scenes/boot/BootLoadingPage.tscn");
        HomePageScene ??= GD.Load<PackedScene>("res://scenes/home/HomePage.tscn");
        GamePageScene ??= GD.Load<PackedScene>("res://scenes/game/GameScene.tscn");
        LevelCompletePageScene ??= GD.Load<PackedScene>("res://scenes/result/LevelCompletePage.tscn");
        DailyRewardPageScene ??= GD.Load<PackedScene>("res://scenes/result/DailyRewardPage.tscn");

        EnsureCatalogsLoaded();
        LoadProgress();
        ShowBootLoadingPage();
    }

    /// <summary>
    /// 先显示启动页，再进入主页。
    /// 这样项目启动时就有正式应用外壳，而不再是直接裸切到主页或游戏页。
    /// </summary>
    private void ShowBootLoadingPage()
    {
        var bootPage = BootLoadingPageScene.Instantiate<BootLoadingPage>();
        bootPage.Configure(
            _progress.PlayerName,
            $"已解锁 {_progress.HighestUnlockedLevel} 关",
            "正在整理今日牌桌与旅程记录...");
        bootPage.LoadCompleted += OnBootLoadCompleted;

        SwitchToPage(bootPage);
    }

    /// <summary>
    /// 主页只接收轻量展示数据，不直接读取目录资源或牌桌运行时对象。
    /// 这样页面层始终是展示层，流程入口仍然只在 AppRoot。
    /// </summary>
    private void ShowHomePage()
    {
        var homePage = HomePageScene.Instantiate<HomePage>();
        var level = GetLevelOrFallback(_currentLevelNumber);

        homePage.Configure(
            _currentLevelNumber,
            _progress.PlayerName,
            _progress.LeafCount,
            $"最高解锁 L{_progress.HighestUnlockedLevel} · 已通关 {_progress.TotalCompletedLevelCount} 局",
            BuildLevelTitle(level, _currentLevelNumber),
            BuildLevelSummary(level));
        homePage.StartGameRequested += OnStartGameRequested;

        SwitchToPage(homePage);
    }

    private void ShowGamePage(int levelNumber)
    {
        var gamePage = GamePageScene.Instantiate<GameScene>();
        gamePage.AutoStartPrototype = false;
        gamePage.LevelCompleted += OnLevelCompleted;
        gamePage.BackToHomeRequested += OnBackToHomeRequested;

        SwitchToPage(gamePage);
        gamePage.StartLevel(levelNumber);
    }

    private void ShowLevelCompletePage(LevelCompleteResult result)
    {
        var completePage = LevelCompletePageScene.Instantiate<LevelCompletePage>();
        completePage.Configure(result);
        completePage.ContinueRequested += OnContinueRequested;
        completePage.ReturnHomeRequested += OnBackToHomeRequested;

        SwitchToPage(completePage);
    }

    private void ShowDailyRewardPage(DailyRewardSummary summary)
    {
        var rewardPage = DailyRewardPageScene.Instantiate<DailyRewardPage>();
        rewardPage.Configure(summary);
        rewardPage.ContinueRequested += OnDailyRewardContinueRequested;
        rewardPage.ReturnHomeRequested += OnBackToHomeRequested;

        SwitchToPage(rewardPage);
    }

    private void SwitchToPage(Node nextPage)
    {
        if (_currentPage is not null)
        {
            RemoveChild(_currentPage);
            _currentPage.QueueFree();
        }

        _currentPage = nextPage;
        AddChild(_currentPage);
    }

    private void OnBootLoadCompleted()
    {
        ShowHomePage();
    }

    private void OnStartGameRequested(int levelNumber)
    {
        _currentLevelNumber = Math.Max(1, levelNumber);
        _progress.CurrentLevelNumber = _currentLevelNumber;
        SaveProgress();
        ShowGamePage(_currentLevelNumber);
    }

    /// <summary>
    /// 结算发生时先更新持久化进度，再决定是否要插入每日奖励页。
    /// 奖励是否展示由流程层控制，不让游戏页自己承担外围页面编排。
    /// </summary>
    private void OnLevelCompleted(LevelCompleteResult result)
    {
        var nextLevelNumber = result.LevelNumber + 1;
        var nextLevel = GetLevelOrFallback(nextLevelNumber);
        var rewardGranted = TryGrantDailyReward(out var grantedLeafCount);

        _progress.TotalScore += result.Score;
        _progress.TotalMatches += result.MatchCount;
        _progress.TotalCompletedLevelCount += 1;
        _progress.CurrentLevelNumber = nextLevelNumber;
        _progress.HighestUnlockedLevel = Math.Max(_progress.HighestUnlockedLevel, nextLevelNumber);
        _currentLevelNumber = nextLevelNumber;
        SaveProgress();

        var completeResult = new LevelCompleteResult
        {
            LevelNumber = result.LevelNumber,
            Score = result.Score,
            MatchCount = result.MatchCount,
            ElapsedText = result.ElapsedText,
            NextLevelNumber = nextLevelNumber,
            NextLevelName = BuildLevelTitle(nextLevel, nextLevelNumber),
            NextLevelSummary = BuildLevelSummary(nextLevel),
            HasDailyReward = rewardGranted,
            DailyRewardLeafCount = grantedLeafCount,
        };

        _pendingDailyRewardSummary = rewardGranted
            ? new DailyRewardSummary
            {
                RewardLeafCount = grantedLeafCount,
                CurrentLeafTotal = _progress.LeafCount,
                RewardTitle = "每日首胜奖励",
                RewardDescription = $"今日首次通关已发放 +{grantedLeafCount} 叶子，可用于后续外层功能扩展。",
                NextLevelNumber = nextLevelNumber,
                NextLevelName = completeResult.NextLevelName,
                NextLevelSummary = completeResult.NextLevelSummary,
            }
            : null;

        ShowLevelCompletePage(completeResult);
    }

    private void OnContinueRequested(int nextLevelNumber)
    {
        if (_pendingDailyRewardSummary is not null)
        {
            var rewardSummary = _pendingDailyRewardSummary;
            _pendingDailyRewardSummary = null;
            ShowDailyRewardPage(rewardSummary);
            return;
        }

        _currentLevelNumber = nextLevelNumber;
        _progress.CurrentLevelNumber = nextLevelNumber;
        SaveProgress();
        ShowGamePage(nextLevelNumber);
    }

    private void OnDailyRewardContinueRequested(int nextLevelNumber)
    {
        _currentLevelNumber = nextLevelNumber;
        _progress.CurrentLevelNumber = nextLevelNumber;
        SaveProgress();
        ShowGamePage(nextLevelNumber);
    }

    private void OnBackToHomeRequested()
    {
        ShowHomePage();
    }

    private void EnsureCatalogsLoaded()
    {
        LevelCatalog ??= GD.Load<LevelCatalog>(DefaultLevelCatalogPath);
        ProfileCatalog ??= GD.Load<LayoutProfileCatalog>(DefaultProfileCatalogPath);

        if (LevelCatalog is null)
        {
            GD.PushError($"[AppRoot] 无法加载关卡目录: {DefaultLevelCatalogPath}");
        }

        if (ProfileCatalog is null)
        {
            GD.PushError($"[AppRoot] 无法加载规则目录: {DefaultProfileCatalogPath}");
        }
    }

    private void LoadProgress()
    {
        _progress = PlayerProgressStore.LoadOrCreate();
        _currentLevelNumber = Math.Max(1, _progress.CurrentLevelNumber);
    }

    private void SaveProgress()
    {
        PlayerProgressStore.Save(_progress);
    }

    /// <summary>
    /// 每日奖励当前按本地日期做一次性领取。
    /// 这是外围产品层规则，不进入牌桌交互层。
    /// </summary>
    private bool TryGrantDailyReward(out int grantedLeafCount)
    {
        var todayKey = DateTime.Now.ToString("yyyy-MM-dd");
        if (_progress.LastDailyRewardDate == todayKey)
        {
            grantedLeafCount = 0;
            return false;
        }

        _progress.LastDailyRewardDate = todayKey;
        _progress.LeafCount += DailyRewardLeafCount;
        grantedLeafCount = DailyRewardLeafCount;
        return true;
    }

    private LevelConfig? GetLevelOrFallback(int levelNumber)
    {
        if (LevelCatalog is null)
        {
            return null;
        }

        foreach (var level in LevelCatalog.Levels)
        {
            if (level is not null && level.LevelNumber == levelNumber)
            {
                return level;
            }
        }

        foreach (var level in LevelCatalog.Levels)
        {
            if (level is not null && level.LevelNumber == LevelCatalog.DefaultLevelNumber)
            {
                return level;
            }
        }

        return null;
    }

    private static string BuildLevelTitle(LevelConfig? level, int fallbackLevelNumber)
    {
        if (level is null)
        {
            return $"关卡 {fallbackLevelNumber}";
        }

        return string.IsNullOrWhiteSpace(level.DisplayName)
            ? $"关卡 {level.LevelNumber}"
            : level.DisplayName;
    }

    private string BuildLevelSummary(LevelConfig? level)
    {
        if (level is null)
        {
            return "未找到关卡配置，进入后将回退到默认布局。";
        }

        var profileName = ResolveProfileDisplayName(level.LayoutProfileId);
        var sourceText = level.LayoutSourceMode switch
        {
            LevelLayoutSourceMode.Prototype => "固定原型牌桌",
            LevelLayoutSourceMode.RandomGenerated => level.UseFixedSeed
                ? $"固定种子随机布局 #{level.RandomSeed}"
                : "动态随机布局",
            _ => "未知布局模式",
        };

        return $"规则档案：{profileName} | 布局：{sourceText}";
    }

    private string ResolveProfileDisplayName(string profileId)
    {
        if (ProfileCatalog is null || string.IsNullOrWhiteSpace(profileId))
        {
            return "默认规则";
        }

        foreach (var profile in ProfileCatalog.Profiles)
        {
            if (profile is not null && profile.ProfileId == profileId)
            {
                return string.IsNullOrWhiteSpace(profile.DisplayName)
                    ? profile.ProfileId
                    : profile.DisplayName;
            }
        }

        return profileId;
    }
}
