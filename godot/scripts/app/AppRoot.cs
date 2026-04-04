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

    private Node? _currentPage;
    private int _currentLevelNumber = 1;
    private PlayerProgressData _progress = new();
    private DailyRewardSummary? _pendingDailyRewardSummary;

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
        return LevelCatalog?.ResolveLevelOrFallback(levelNumber);
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
            LevelLayoutSourceMode.OfflineJson => string.IsNullOrWhiteSpace(level.OfflineLayoutJsonPath)
                ? string.IsNullOrWhiteSpace(level.OfflineCatalogJsonPath)
                    ? "离线正式关卡 JSON"
                    : $"离线关卡目录 | {level.OfflineCatalogJsonPath}"
                : $"离线正式关卡 JSON | {level.OfflineLayoutJsonPath}",
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
