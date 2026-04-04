using System;
using System.Collections.Generic;
using Godot;
using TileMatcher.Boot;
using TileMatcher.Config;
using TileMatcher.Game;
using TileMatcher.Home;
using TileMatcher.Pets;
using TileMatcher.Result;

namespace TileMatcher.App;

/// <summary>
/// 外围流程主入口。
/// 负责启动页、首页、游戏页、结算页和失败页之间的页面切换，
/// 同时统一管理玩家进度、宠物领养、救助中心刷新、金币奖励与移动端竖屏设置。
/// </summary>
public partial class AppRoot : Node
{
    private const string DefaultLevelCatalogPath = "res://configs/levels/default_levels.tres";
    private const string DefaultProfileCatalogPath = "res://configs/layout_profiles/default_catalog.tres";
    private const string DefaultPetCatalogPath = "res://configs/pets/pet_definitions.json";
    private const string DefaultStarterPetId = "cream_cat";
    private const int DefaultStarterCoinCount = 10;
    private const int DailyRewardCoinCount = 3;
    private const int RescueRefreshMinutes = 10;

    [Export]
    public PackedScene BootLoadingPageScene { get; set; } = null!;

    [Export]
    public PackedScene HomePageScene { get; set; } = null!;

    [Export]
    public PackedScene GamePageScene { get; set; } = null!;

    [Export]
    public PackedScene LevelCompletePageScene { get; set; } = null!;

    [Export]
    public PackedScene LevelFailedPageScene { get; set; } = null!;

    [Export]
    public PackedScene DailyRewardPageScene { get; set; } = null!;

    [Export]
    public LevelCatalog LevelCatalog { get; set; } = null!;

    [Export]
    public LayoutProfileCatalog ProfileCatalog { get; set; } = null!;

    private Node? _currentPage;
    private int _currentLevelNumber = 1;
    private PlayerProgressData _progress = new();
    private PetCatalog _petCatalog = PetCatalog.Empty;
    private DailyRewardSummary? _pendingDailyRewardSummary;

    public override void _Ready()
    {
        ApplyMobilePortraitOrientation();

        BootLoadingPageScene ??= GD.Load<PackedScene>("res://scenes/boot/BootLoadingPage.tscn");
        HomePageScene ??= GD.Load<PackedScene>("res://scenes/home/HomePage.tscn");
        GamePageScene ??= GD.Load<PackedScene>("res://scenes/game/GameScene.tscn");
        LevelCompletePageScene ??= GD.Load<PackedScene>("res://scenes/result/LevelCompletePage.tscn");
        LevelFailedPageScene ??= GD.Load<PackedScene>("res://scenes/result/LevelFailedPage.tscn");
        DailyRewardPageScene ??= GD.Load<PackedScene>("res://scenes/result/DailyRewardPage.tscn");

        EnsureCatalogsLoaded();
        LoadProgress();
        EnsureStarterProgress();
        ShowBootLoadingPage();

        CallDeferred(MethodName.ApplyMobilePortraitOrientation);
    }

    private void ApplyMobilePortraitOrientation()
    {
        var osName = OS.GetName();
        if (osName != "Android")
        {
            GD.Print($"[AppRoot] 非安卓平台，跳过运行时竖屏锁定。system={osName}");
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
            "毛球碰碰乐",
            $"已解锁 {_progress.HighestUnlockedLevel} 关",
            "正在整理今天等待回家的小伙伴...");
        bootPage.LoadCompleted += OnBootLoadCompleted;

        SwitchToPage(bootPage);
    }

    private void ShowHomePage()
    {
        var homePage = HomePageScene.Instantiate<HomePage>();
        ConfigureHomePage(homePage);
        homePage.StartGameRequested += OnStartGameRequested;
        homePage.AdoptPetRequested += OnAdoptPetRequested;
        homePage.RescuePanelRequested += OnRescuePanelRequested;
        homePage.RefreshRescueCenterRequested += OnRefreshRescueCenterRequested;
        homePage.AddCoinRequested += OnAddCoinRequested;
        homePage.DebugLevelJumpRequested += OnDebugLevelJumpRequested;
        homePage.ResetCurrentLevelAssistRequested += OnResetCurrentLevelAssistRequested;
        homePage.ResetProgressRequested += OnResetProgressRequested;

        SwitchToPage(homePage);
    }

    private void ConfigureHomePage(HomePage homePage, string rescueFeedback = "")
    {
        var level = GetLevelOrFallback(_currentLevelNumber);
        homePage.Configure(
            _currentLevelNumber,
            _progress.PlayerName,
            _progress.CoinCount,
            $"最高解锁 {_progress.HighestUnlockedLevel} 关，已完成 {_progress.TotalCompletedLevelCount} 关",
            BuildLevelTitle(level, _currentLevelNumber),
            BuildLevelSummary(level),
            _petCatalog.ResolveDefinitions(_progress.RescueCenterPetIds),
            _progress.OwnedPets,
            rescueFeedback);
    }

    private void ShowGamePage(int levelNumber)
    {
        var gamePage = GamePageScene.Instantiate<GameScene>();
        gamePage.AutoStartPrototype = false;
        gamePage.BindProgressContext(_progress, SaveProgress);
        gamePage.LevelCompleted += OnLevelCompleted;
        gamePage.LevelFailed += OnLevelFailed;
        gamePage.BackToHomeRequested += OnBackToHomeRequested;
        gamePage.DebugLevelJumpRequested += OnDebugLevelJumpRequested;
        gamePage.RefreshRescueCenterRequested += OnRefreshRescueCenterRequested;
        gamePage.AddCoinRequested += OnAddCoinRequested;
        gamePage.ResetProgressRequested += OnResetProgressRequested;

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

    private void ShowLevelFailedPage(LevelFailedResult result)
    {
        var failedPage = LevelFailedPageScene.Instantiate<LevelFailedPage>();
        failedPage.Configure(result);
        failedPage.RetryRequested += OnRetryRequested;
        failedPage.ReturnHomeRequested += OnBackToHomeRequested;

        SwitchToPage(failedPage);
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

    private void OnRescuePanelRequested()
    {
        if (_currentPage is not HomePage homePage)
        {
            return;
        }

        var refreshed = EnsureRescueCenterReady(forceRefresh: false);
        var feedback = refreshed
            ? BuildRescueRefreshFeedback()
            : BuildRescueStatusFeedback();

        ConfigureHomePage(homePage, feedback);
        homePage.OpenRescueOverlay();
    }

    private void OnRefreshRescueCenterRequested()
    {
        EnsureRescueCenterReady(forceRefresh: true);
        var feedback = BuildRescueRefreshFeedback();

        if (_currentPage is HomePage homePage)
        {
            ConfigureHomePage(homePage, feedback);
            _progress.CurrentLevelNumber = _currentLevelNumber;
            SaveProgress();
            return;
        }

        if (_currentPage is GameScene gamePage)
        {
            SaveProgress();
            gamePage.ShowExternalDebugTip(feedback);
        }
    }

    private void OnAddCoinRequested(int coinAmount)
    {
        var safeCoinAmount = Math.Max(1, coinAmount);
        _progress.CoinCount += safeCoinAmount;
        SaveProgress();
        var feedback = $"已通过调试面板添加 {safeCoinAmount} 金币，当前共 {_progress.CoinCount} 金币。";

        if (_currentPage is HomePage homePage)
        {
            ConfigureHomePage(homePage, feedback);
            return;
        }

        if (_currentPage is GameScene gamePage)
        {
            gamePage.ShowExternalDebugTip(feedback);
        }
    }

    private void OnAdoptPetRequested(string petId)
    {
        if (_currentPage is not HomePage homePage)
        {
            return;
        }

        if (!_petCatalog.TryGetDefinition(petId, out var definition))
        {
            ConfigureHomePage(homePage, "这只宠物的定义暂时没有找到，请检查宠物配置表。");
            homePage.OpenRescueOverlay();
            return;
        }

        if (_progress.HasAdoptedPet(petId))
        {
            ConfigureHomePage(homePage, $"{definition.DisplayName} 已经住进乐园了，不需要重复领养。");
            homePage.OpenRescueOverlay();
            return;
        }

        if (_progress.CoinCount < definition.Cost)
        {
            var deficit = definition.Cost - _progress.CoinCount;
            ConfigureHomePage(homePage, $"金币不足，还差 {deficit} 枚金币才能带 {definition.DisplayName} 回家。");
            homePage.OpenRescueOverlay();
            return;
        }

        _progress.CoinCount -= definition.Cost;
        _progress.OwnedPets.Add(new OwnedPetData
        {
            PetId = definition.PetId,
            AdoptedAtUtc = DateTime.UtcNow.ToString("O"),
            CurrentParkState = definition.ParkActivityText,
        });
        _progress.RemoveRescuePet(definition.PetId);
        SaveProgress();

        ConfigureHomePage(homePage, $"你领养了 {definition.DisplayName}，消耗 {definition.Cost} 枚金币。它已经入住中间的宠物乐园。");
        homePage.OpenRescueOverlay();
    }

    private void OnLevelCompleted(LevelCompleteResult result)
    {
        var nextLevelNumber = result.LevelNumber + 1;
        var nextLevel = GetLevelOrFallback(nextLevelNumber);
        var rewardGranted = TryGrantDailyReward(out var grantedCoinCount);

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
            DailyRewardCoinCount = grantedCoinCount,
        };

        _pendingDailyRewardSummary = rewardGranted
            ? new DailyRewardSummary
            {
                RewardCoinCount = grantedCoinCount,
                CurrentCoinTotal = _progress.CoinCount,
                RewardTitle = "今日金币奖励",
                RewardDescription = $"今天第一次完成关卡，已获得 +{grantedCoinCount} 金币。",
                NextLevelNumber = nextLevelNumber,
                NextLevelName = completeResult.NextLevelName,
                NextLevelSummary = completeResult.NextLevelSummary,
            }
            : null;

        ShowLevelCompletePage(completeResult);
    }

    private void OnLevelFailed(LevelFailedResult result)
    {
        _pendingDailyRewardSummary = null;
        _currentLevelNumber = Math.Max(1, result.LevelNumber);
        _progress.CurrentLevelNumber = _currentLevelNumber;
        SaveProgress();
        ShowLevelFailedPage(result);
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

    private void OnRetryRequested(int levelNumber)
    {
        _pendingDailyRewardSummary = null;
        _currentLevelNumber = Math.Max(1, levelNumber);
        _progress.CurrentLevelNumber = _currentLevelNumber;
        SaveProgress();
        ShowGamePage(_currentLevelNumber);
    }

    private void OnBackToHomeRequested()
    {
        ShowHomePage();
    }

    private void OnDebugLevelJumpRequested(int levelNumber)
    {
        _currentLevelNumber = Math.Max(1, levelNumber);
        _progress.CurrentLevelNumber = _currentLevelNumber;
        SaveProgress();
        ShowGamePage(_currentLevelNumber);
    }

    private void OnResetCurrentLevelAssistRequested(int levelNumber)
    {
        var usage = _progress.GetOrCreateLevelAssistUsage(levelNumber);
        usage.RestartUsedCount = 0;
        usage.HintUsedCount = 0;
        SaveProgress();
    }

    private void OnResetProgressRequested()
    {
        GD.Print("[AppRoot] 收到重置账号数据请求，正在清空进度并返回首页。");
        _progress = new PlayerProgressData();
        EnsureStarterProgress();
        _pendingDailyRewardSummary = null;
        _currentLevelNumber = 1;
        _progress.CurrentLevelNumber = 1;
        _progress.HighestUnlockedLevel = 1;
        SaveProgress();
        ShowHomePage();
    }

    private void EnsureCatalogsLoaded()
    {
        LevelCatalog ??= GD.Load<LevelCatalog>(DefaultLevelCatalogPath);
        ProfileCatalog ??= GD.Load<LayoutProfileCatalog>(DefaultProfileCatalogPath);
        _petCatalog = PetCatalog.Load(DefaultPetCatalogPath);

        if (LevelCatalog is null)
        {
            GD.PushError($"[AppRoot] 无法加载关卡目录: {DefaultLevelCatalogPath}");
        }

        if (ProfileCatalog is null)
        {
            GD.PushError($"[AppRoot] 无法加载规则目录: {DefaultProfileCatalogPath}");
        }

        if (_petCatalog.Definitions.Count == 0)
        {
            GD.PushWarning($"[AppRoot] 宠物定义表为空: {DefaultPetCatalogPath}");
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

    private void EnsureStarterProgress()
    {
        if (_progress.CoinCount < DefaultStarterCoinCount
            && _progress.OwnedPets.Count == 0
            && _progress.TotalCompletedLevelCount == 0
            && _progress.TotalMatches == 0
            && string.IsNullOrWhiteSpace(_progress.LastRescueRefreshBeijingTime))
        {
            _progress.CoinCount = DefaultStarterCoinCount;
        }

        if (!_progress.HasAdoptedPet(DefaultStarterPetId) && _petCatalog.TryGetDefinition(DefaultStarterPetId, out var starterPet))
        {
            _progress.OwnedPets.Insert(0, new OwnedPetData
            {
                PetId = starterPet.PetId,
                AdoptedAtUtc = DateTime.UtcNow.ToString("O"),
                CurrentParkState = starterPet.ParkActivityText,
            });
        }

        EnsureRescueCenterReady(forceRefresh: _progress.RescueCenterPetIds.Count == 0);
        SaveProgress();
    }

    private bool EnsureRescueCenterReady(bool forceRefresh)
    {
        var beijingNow = GetBeijingNow();
        if (!forceRefresh && !ShouldRefreshRescueCenter(beijingNow))
        {
            return false;
        }

        RefreshRescueCenter(beijingNow);
        SaveProgress();
        return true;
    }

    private bool ShouldRefreshRescueCenter(DateTimeOffset beijingNow)
    {
        if (_progress.RescueCenterPetIds.Count == 0)
        {
            return true;
        }

        if (!TryParseBeijingRefreshTime(_progress.LastRescueRefreshBeijingTime, out var lastRefresh))
        {
            return true;
        }

        return beijingNow - lastRefresh >= TimeSpan.FromMinutes(RescueRefreshMinutes);
    }

    private void RefreshRescueCenter(DateTimeOffset beijingNow)
    {
        var candidates = new List<PetDefinition>();
        foreach (var definition in _petCatalog.Definitions)
        {
            if (_progress.HasAdoptedPet(definition.PetId))
            {
                continue;
            }

            candidates.Add(definition);
        }

        var random = new RandomNumberGenerator();
        random.Randomize();
        for (var i = candidates.Count - 1; i > 0; i--)
        {
            var swapIndex = random.RandiRange(0, i);
            (candidates[i], candidates[swapIndex]) = (candidates[swapIndex], candidates[i]);
        }

        var targetCount = candidates.Count <= 3
            ? candidates.Count
            : random.RandiRange(3, Math.Min(5, candidates.Count));

        _progress.RescueCenterPetIds.Clear();
        for (var index = 0; index < targetCount; index++)
        {
            _progress.RescueCenterPetIds.Add(candidates[index].PetId);
        }

        _progress.LastRescueRefreshBeijingTime = beijingNow.ToString("yyyy-MM-dd HH:mm:ss");
    }

    private string BuildRescueRefreshFeedback()
    {
        var count = _progress.RescueCenterPetIds.Count;
        if (count == 0)
        {
            return "救助中心刚刚刷新，但暂时没有新的待救助动物。";
        }

        return $"救助中心已按北京时间刷新，本次来了 {count} 只等待救助的小动物。上次刷新时间：{_progress.LastRescueRefreshBeijingTime}。";
    }

    private string BuildRescueStatusFeedback()
    {
        if (string.IsNullOrWhiteSpace(_progress.LastRescueRefreshBeijingTime))
        {
            return "救助中心正在准备新的待救助动物。";
        }

        return $"救助中心尚未到下一个刷新窗口。上次刷新时间：{_progress.LastRescueRefreshBeijingTime}（北京时间）。";
    }

    private static DateTimeOffset GetBeijingNow()
    {
        var utcNow = DateTimeOffset.UtcNow;

        try
        {
            var chinaTimeZone = TimeZoneInfo.FindSystemTimeZoneById("China Standard Time");
            return TimeZoneInfo.ConvertTime(utcNow, chinaTimeZone);
        }
        catch
        {
            try
            {
                var chinaTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Shanghai");
                return TimeZoneInfo.ConvertTime(utcNow, chinaTimeZone);
            }
            catch
            {
                return utcNow.ToOffset(TimeSpan.FromHours(8));
            }
        }
    }

    private static bool TryParseBeijingRefreshTime(string text, out DateTimeOffset timestamp)
    {
        if (DateTimeOffset.TryParse(text, out timestamp))
        {
            return true;
        }

        if (DateTime.TryParse(text, out var localTime))
        {
            timestamp = new DateTimeOffset(localTime, TimeSpan.FromHours(8));
            return true;
        }

        timestamp = default;
        return false;
    }

    private bool TryGrantDailyReward(out int grantedCoinCount)
    {
        var todayKey = DateTime.Now.ToString("yyyy-MM-dd");
        if (_progress.LastDailyRewardDate == todayKey)
        {
            grantedCoinCount = 0;
            return false;
        }

        _progress.LastDailyRewardDate = todayKey;
        _progress.CoinCount += DailyRewardCoinCount;
        grantedCoinCount = DailyRewardCoinCount;
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
            return $"第 {fallbackLevelNumber} 关";
        }

        return string.IsNullOrWhiteSpace(level.DisplayName)
            ? $"第 {level.LevelNumber} 关"
            : level.DisplayName;
    }

    private string BuildLevelSummary(LevelConfig? level)
    {
        if (level is null)
        {
            return "关卡资料暂未准备好，进入后会使用默认关卡。";
        }

        var profileName = ResolveProfileDisplayName(level.LayoutProfileId);
        var sourceText = level.LayoutSourceMode switch
        {
            LevelLayoutSourceMode.Prototype => "原型体验关卡",
            LevelLayoutSourceMode.RandomGenerated => level.UseFixedSeed
                ? $"调试随机关卡 #{level.RandomSeed}"
                : "调试随机关卡",
            LevelLayoutSourceMode.OfflineJson => "离线正式关卡",
            _ => "默认关卡",
        };

        return $"玩法规则：{profileName} | 当前关卡：{sourceText}";
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
