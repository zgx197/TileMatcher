using System;
using Godot;
using TileMatcher.Boot;
using TileMatcher.Config;
using TileMatcher.Game;
using TileMatcher.Home;
using TileMatcher.Result;

namespace TileMatcher.App;

/// <summary>
/// 外围流程主入口。
/// 这一层负责在启动页、主页、游戏页、结算页、每日奖励页之间切换，
/// 同时统一管理玩家进度、当前关卡号和运行时竖屏锁定。
/// </summary>
public partial class AppRoot : Node
{
    private const string DefaultLevelCatalogPath = "res://configs/levels/default_levels.tres";
    private const string DefaultProfileCatalogPath = "res://configs/layout_profiles/default_catalog.tres";
    private const int DailyRewardLeafCount = 3;

    /// <summary>启动页场景资源。</summary>
    [Export]
    public PackedScene BootLoadingPageScene { get; set; } = null!;

    /// <summary>主页场景资源。</summary>
    [Export]
    public PackedScene HomePageScene { get; set; } = null!;

    /// <summary>游戏页场景资源。</summary>
    [Export]
    public PackedScene GamePageScene { get; set; } = null!;

    /// <summary>通关结算页场景资源。</summary>
    [Export]
    public PackedScene LevelCompletePageScene { get; set; } = null!;

    /// <summary>每日奖励页场景资源。</summary>
    [Export]
    public PackedScene DailyRewardPageScene { get; set; } = null!;

    /// <summary>运行时使用的关卡目录资源。</summary>
    [Export]
    public LevelCatalog LevelCatalog { get; set; } = null!;

    /// <summary>运行时使用的规则档案目录资源。</summary>
    [Export]
    public LayoutProfileCatalog ProfileCatalog { get; set; } = null!;

    /// <summary>当前挂在根节点下的页面实例。</summary>
    private Node? _currentPage;

    /// <summary>当前外围流程认定的关卡号。</summary>
    private int _currentLevelNumber = 1;

    /// <summary>当前玩家进度存档对象。</summary>
    private PlayerProgressData _progress = new();

    /// <summary>通关后待展示的每日奖励摘要，若为空则直接继续下一关。</summary>
    private DailyRewardSummary? _pendingDailyRewardSummary;

    /// <summary>初始化外围页面资源、进度数据和启动流程。</summary>
    public override void _Ready()
    {
        ApplyMobilePortraitOrientation();

        BootLoadingPageScene ??= GD.Load<PackedScene>("res://scenes/boot/BootLoadingPage.tscn");
        HomePageScene ??= GD.Load<PackedScene>("res://scenes/home/HomePage.tscn");
        GamePageScene ??= GD.Load<PackedScene>("res://scenes/game/GameScene.tscn");
        LevelCompletePageScene ??= GD.Load<PackedScene>("res://scenes/result/LevelCompletePage.tscn");
        DailyRewardPageScene ??= GD.Load<PackedScene>("res://scenes/result/DailyRewardPage.tscn");

        EnsureCatalogsLoaded();
        LoadProgress();
        ShowBootLoadingPage();

        // 某些安卓设备会在宿主界面刚建立时短暂覆盖方向设置，
        // 因此延后到下一帧再执行一次竖屏锁定。
        CallDeferred(MethodName.ApplyMobilePortraitOrientation);
    }

    /// <summary>
    /// 在支持方向控制的平台上显式请求竖屏。
    /// 主页、启动页和游戏页中的临时朝向诊断界面已经移除，
    /// 但真正生效的竖屏锁定逻辑仍然保留在这里。
    /// </summary>
    private void ApplyMobilePortraitOrientation()
    {
        var osName = OS.GetName();
        if (osName != "Android")
        {
            GD.Print($"[AppRoot] 非安卓平台，跳过运行时竖屏锁定。系统={osName}");
            return;
        }

        if (!DisplayServer.HasFeature(DisplayServer.Feature.Orientation))
        {
            GD.Print("[AppRoot] 当前平台不支持运行时方向控制，无法调用 ScreenSetOrientation。");
            return;
        }

        var before = DisplayServer.ScreenGetOrientation();
        DisplayServer.ScreenSetOrientation(DisplayServer.ScreenOrientation.Portrait);
        var after = DisplayServer.ScreenGetOrientation();
        GD.Print($"[AppRoot] 已执行运行时竖屏锁定。before={before}, after={after}");
    }

    /// <summary>切到启动加载页。</summary>
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

    /// <summary>切到主页，并注入当前玩家进度摘要。</summary>
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
        homePage.DebugLevelJumpRequested += OnDebugLevelJumpRequested;
        homePage.ResetCurrentLevelAssistRequested += OnResetCurrentLevelAssistRequested;
        homePage.ResetProgressRequested += OnResetProgressRequested;

        SwitchToPage(homePage);
    }

    /// <summary>切到游戏页，并按关卡号启动当前局。</summary>
    private void ShowGamePage(int levelNumber)
    {
        var gamePage = GamePageScene.Instantiate<GameScene>();
        gamePage.AutoStartPrototype = false;
        gamePage.BindProgressContext(_progress, SaveProgress);
        gamePage.LevelCompleted += OnLevelCompleted;
        gamePage.BackToHomeRequested += OnBackToHomeRequested;
        gamePage.DebugLevelJumpRequested += OnDebugLevelJumpRequested;
        gamePage.ResetProgressRequested += OnResetProgressRequested;

        SwitchToPage(gamePage);
        gamePage.StartLevel(levelNumber);
    }

    /// <summary>切到通关结算页。</summary>
    private void ShowLevelCompletePage(LevelCompleteResult result)
    {
        var completePage = LevelCompletePageScene.Instantiate<LevelCompletePage>();
        completePage.Configure(result);
        completePage.ContinueRequested += OnContinueRequested;
        completePage.ReturnHomeRequested += OnBackToHomeRequested;

        SwitchToPage(completePage);
    }

    /// <summary>切到每日奖励页。</summary>
    private void ShowDailyRewardPage(DailyRewardSummary summary)
    {
        var rewardPage = DailyRewardPageScene.Instantiate<DailyRewardPage>();
        rewardPage.Configure(summary);
        rewardPage.ContinueRequested += OnDailyRewardContinueRequested;
        rewardPage.ReturnHomeRequested += OnBackToHomeRequested;

        SwitchToPage(rewardPage);
    }

    /// <summary>统一替换当前显示页面。</summary>
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

    /// <summary>启动页加载完成后进入主页。</summary>
    private void OnBootLoadCompleted()
    {
        ShowHomePage();
    }

    /// <summary>响应主页“开始游戏”请求。</summary>
    private void OnStartGameRequested(int levelNumber)
    {
        _currentLevelNumber = Math.Max(1, levelNumber);
        _progress.CurrentLevelNumber = _currentLevelNumber;
        SaveProgress();
        ShowGamePage(_currentLevelNumber);
    }

    /// <summary>响应关卡完成事件，更新进度并决定是否展示奖励页。</summary>
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
                RewardDescription = $"今日首次通关已发放 +{grantedLeafCount} 叶子，可用于后续外围功能扩展。",
                NextLevelNumber = nextLevelNumber,
                NextLevelName = completeResult.NextLevelName,
                NextLevelSummary = completeResult.NextLevelSummary,
            }
            : null;

        ShowLevelCompletePage(completeResult);
    }

    /// <summary>响应结算页继续按钮，必要时先进入每日奖励页。</summary>
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

    /// <summary>响应每日奖励页继续按钮，直接进入下一关。</summary>
    private void OnDailyRewardContinueRequested(int nextLevelNumber)
    {
        _currentLevelNumber = nextLevelNumber;
        _progress.CurrentLevelNumber = nextLevelNumber;
        SaveProgress();
        ShowGamePage(nextLevelNumber);
    }

    /// <summary>响应返回主页请求。</summary>
    private void OnBackToHomeRequested()
    {
        ShowHomePage();
    }

    /// <summary>响应游戏页跳关请求，并通过外围流程重进目标关卡。</summary>
    private void OnDebugLevelJumpRequested(int levelNumber)
    {
        _currentLevelNumber = Math.Max(1, levelNumber);
        _progress.CurrentLevelNumber = _currentLevelNumber;
        SaveProgress();
        ShowGamePage(_currentLevelNumber);
    }

    /// <summary>响应首页调试面板的当前关卡辅助次数重置请求。</summary>
    private void OnResetCurrentLevelAssistRequested(int levelNumber)
    {
        var usage = _progress.GetOrCreateLevelAssistUsage(levelNumber);
        usage.RestartUsedCount = 0;
        usage.HintUsedCount = 0;
        SaveProgress();
    }

    /// <summary>响应清空账号数据请求，重置存档并回到主页。</summary>
    private void OnResetProgressRequested()
    {
        GD.Print("[AppRoot] 收到重置账号数据请求，正在清空进度并返回主页。");
        _progress = new PlayerProgressData();
        _pendingDailyRewardSummary = null;
        _currentLevelNumber = 1;
        _progress.CurrentLevelNumber = 1;
        _progress.HighestUnlockedLevel = 1;
        SaveProgress();
        ShowHomePage();
    }

    /// <summary>确保关卡目录和规则目录已经从默认路径加载。</summary>
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

    /// <summary>从本地存档加载玩家进度。</summary>
    private void LoadProgress()
    {
        _progress = PlayerProgressStore.LoadOrCreate();
        _currentLevelNumber = Math.Max(1, _progress.CurrentLevelNumber);
    }

    /// <summary>把当前进度写回本地存档。</summary>
    private void SaveProgress()
    {
        PlayerProgressStore.Save(_progress);
    }

    /// <summary>尝试发放今日首胜奖励。</summary>
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

    /// <summary>按关卡号查询关卡配置，必要时允许目录回退。</summary>
    private LevelConfig? GetLevelOrFallback(int levelNumber)
    {
        return LevelCatalog?.ResolveLevelOrFallback(levelNumber);
    }

    /// <summary>构造主页和结算页使用的关卡标题。</summary>
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

    /// <summary>构造主页和结算页使用的关卡摘要文本。</summary>
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
            LevelLayoutSourceMode.OfflineJson => string.IsNullOrWhiteSpace(level.OfflineLayoutJsonPath)
                ? string.IsNullOrWhiteSpace(level.OfflineCatalogJsonPath)
                    ? "离线正式关卡 JSON"
                    : $"离线关卡目录 | {level.OfflineCatalogJsonPath}"
                : $"离线正式关卡 JSON | {level.OfflineLayoutJsonPath}",
            _ => "未知布局模式",
        };

        return $"规则档案：{profileName} | 布局：{sourceText}";
    }

    /// <summary>把规则档案 id 解析为可读显示名。</summary>
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
