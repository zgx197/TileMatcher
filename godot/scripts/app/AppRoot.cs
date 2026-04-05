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
    /// <summary>默认关卡目录资源路径。</summary>
    private const string DefaultLevelCatalogPath = "res://configs/levels/default_levels.tres";
    /// <summary>默认规则档案目录路径。</summary>
    private const string DefaultProfileCatalogPath = "res://configs/layout_profiles/default_catalog.tres";
    /// <summary>默认宠物定义表路径。</summary>
    private const string DefaultPetCatalogPath = "res://configs/pets/pet_definitions.json";
    /// <summary>首次进入时默认赠送的初始宠物 id。</summary>
    private const string DefaultStarterPetId = "cream_cat";
    /// <summary>新进度的默认起始金币。</summary>
    private const int DefaultStarterCoinCount = 10;
    /// <summary>每日首次通关发放的金币数量。</summary>
    private const int DailyRewardCoinCount = 3;
    /// <summary>救助中心自动刷新的分钟间隔。</summary>
    private const int RescueRefreshMinutes = 10;

    /// <summary>随机昵称可选前缀池。</summary>
    private static readonly string[] PetNamePrefixes = ["小", "奶糖", "糯米", "团子", "布丁", "豆包", "泡芙", "栗栗"];
    /// <summary>猫类昵称后缀池。</summary>
    private static readonly string[] CatNameSuffixes = ["喵", "球", "酱", "宝", "咪"];
    /// <summary>狗类昵称后缀池。</summary>
    private static readonly string[] DogNameSuffixes = ["汪", "豆", "宝", "卷", "仔"];
    /// <summary>兔类昵称后缀池。</summary>
    private static readonly string[] BunnyNameSuffixes = ["兔", "团", "饼", "耳", "啾"];
    /// <summary>仓鼠类昵称后缀池。</summary>
    private static readonly string[] HamsterNameSuffixes = ["仓", "球", "团", "豆", "粒"];
    /// <summary>狐狸类昵称后缀池。</summary>
    private static readonly string[] FoxNameSuffixes = ["狐", "尾", "团", "灵", "宝"];
    /// <summary>未命中特定物种时的默认后缀池。</summary>
    private static readonly string[] DefaultNameSuffixes = ["宝", "团", "球", "仔", "咪"];

    /// <summary>启动页场景，可在 Inspector 中覆盖。</summary>
    [Export]
    public PackedScene BootLoadingPageScene { get; set; } = null!;

    /// <summary>首页场景，可在 Inspector 中覆盖。</summary>
    [Export]
    public PackedScene HomePageScene { get; set; } = null!;

    /// <summary>游戏页场景，可在 Inspector 中覆盖。</summary>
    [Export]
    public PackedScene GamePageScene { get; set; } = null!;

    /// <summary>通关页场景，可在 Inspector 中覆盖。</summary>
    [Export]
    public PackedScene LevelCompletePageScene { get; set; } = null!;

    /// <summary>失败页场景，可在 Inspector 中覆盖。</summary>
    [Export]
    public PackedScene LevelFailedPageScene { get; set; } = null!;

    /// <summary>每日奖励页场景，可在 Inspector 中覆盖。</summary>
    [Export]
    public PackedScene DailyRewardPageScene { get; set; } = null!;

    /// <summary>关卡目录资源。</summary>
    [Export]
    public LevelCatalog LevelCatalog { get; set; } = null!;

    /// <summary>规则档案目录资源。</summary>
    [Export]
    public LayoutProfileCatalog ProfileCatalog { get; set; } = null!;

    /// <summary>当前正在显示的页面节点。</summary>
    private Node? _currentPage;

    /// <summary>当前流程持有的关卡号。</summary>
    private int _currentLevelNumber = 1;

    /// <summary>当前玩家外围进度。</summary>
    private PlayerProgressData _progress = new();

    /// <summary>当前已加载的宠物定义目录。</summary>
    private PetCatalog _petCatalog = PetCatalog.Empty;

    /// <summary>通关后待展示的每日奖励信息。</summary>
    private DailyRewardSummary? _pendingDailyRewardSummary;

    /// <summary>外围流程使用的随机数生成器。</summary>
    private readonly RandomNumberGenerator _random = new();

    /// <summary>初始化外围流程资源、进度和第一页内容。</summary>
    public override void _Ready()
    {
        _random.Randomize();
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

    /// <summary>在安卓设备上尝试锁定为竖屏运行。</summary>
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

    /// <summary>显示启动加载页，并等待其完成最短展示时长。</summary>
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

    /// <summary>显示首页，并接通首页发出的流程事件。</summary>
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

    /// <summary>按照当前进度和宠物状态刷新首页展示内容。</summary>
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

    /// <summary>显示游戏页并启动指定关卡。</summary>
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

    /// <summary>显示通关结算页。</summary>
    private void ShowLevelCompletePage(LevelCompleteResult result)
    {
        var completePage = LevelCompletePageScene.Instantiate<LevelCompletePage>();
        completePage.Configure(result);
        completePage.ContinueRequested += OnContinueRequested;
        completePage.ReturnHomeRequested += OnBackToHomeRequested;

        SwitchToPage(completePage);
    }

    /// <summary>显示失败结算页。</summary>
    private void ShowLevelFailedPage(LevelFailedResult result)
    {
        var failedPage = LevelFailedPageScene.Instantiate<LevelFailedPage>();
        failedPage.Configure(result);
        failedPage.RetryRequested += OnRetryRequested;
        failedPage.ReturnHomeRequested += OnBackToHomeRequested;

        SwitchToPage(failedPage);
    }

    /// <summary>显示每日奖励页。</summary>
    private void ShowDailyRewardPage(DailyRewardSummary summary)
    {
        var rewardPage = DailyRewardPageScene.Instantiate<DailyRewardPage>();
        rewardPage.Configure(summary);
        rewardPage.ContinueRequested += OnDailyRewardContinueRequested;
        rewardPage.ReturnHomeRequested += OnBackToHomeRequested;

        SwitchToPage(rewardPage);
    }

    /// <summary>切换当前显示的页面，并释放旧页面。</summary>
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

    /// <summary>启动页播放完成后进入首页。</summary>
    private void OnBootLoadCompleted()
    {
        ShowHomePage();
    }

    /// <summary>响应首页“开始游戏”事件。</summary>
    private void OnStartGameRequested(int levelNumber)
    {
        _currentLevelNumber = Math.Max(1, levelNumber);
        _progress.CurrentLevelNumber = _currentLevelNumber;
        SaveProgress();
        ShowGamePage(_currentLevelNumber);
    }

    /// <summary>响应首页打开救助中心面板的请求。</summary>
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

    /// <summary>强制刷新救助中心内容，并把结果反馈到当前页面。</summary>
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

    /// <summary>处理调试面板的加金币请求。</summary>
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

    /// <summary>处理玩家领养救助宠物的请求。</summary>
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
        var generatedPetName = GenerateRandomPetName(definition);
        _progress.OwnedPets.Add(new OwnedPetData
        {
            PetId = definition.PetId,
            PetName = generatedPetName,
            AdoptedAtUtc = DateTime.UtcNow.ToString("O"),
            CurrentParkState = definition.ParkActivityText,
        });
        _progress.RemoveRescuePet(definition.PetId);
        SaveProgress();

        ConfigureHomePage(homePage, $"你领养了 {definition.DisplayName}，消耗 {definition.Cost} 枚金币。它已经入住中间的宠物乐园。");
        homePage.OpenRescueOverlay();
    }

    /// <summary>处理单局通关后的外围进度更新和后续流程跳转准备。</summary>
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

    /// <summary>处理单局失败后的回退流程。</summary>
    private void OnLevelFailed(LevelFailedResult result)
    {
        _pendingDailyRewardSummary = null;
        _currentLevelNumber = Math.Max(1, result.LevelNumber);
        _progress.CurrentLevelNumber = _currentLevelNumber;
        SaveProgress();
        ShowLevelFailedPage(result);
    }

    /// <summary>响应通关页继续按钮，必要时先转到奖励页。</summary>
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

    /// <summary>响应奖励页继续按钮，直接进入下一关。</summary>
    private void OnDailyRewardContinueRequested(int nextLevelNumber)
    {
        _currentLevelNumber = nextLevelNumber;
        _progress.CurrentLevelNumber = nextLevelNumber;
        SaveProgress();
        ShowGamePage(nextLevelNumber);
    }

    /// <summary>响应失败页重试按钮。</summary>
    private void OnRetryRequested(int levelNumber)
    {
        _pendingDailyRewardSummary = null;
        _currentLevelNumber = Math.Max(1, levelNumber);
        _progress.CurrentLevelNumber = _currentLevelNumber;
        SaveProgress();
        ShowGamePage(_currentLevelNumber);
    }

    /// <summary>统一返回首页。</summary>
    private void OnBackToHomeRequested()
    {
        ShowHomePage();
    }

    /// <summary>响应调试跳关请求，直接进入目标关卡。</summary>
    private void OnDebugLevelJumpRequested(int levelNumber)
    {
        _currentLevelNumber = Math.Max(1, levelNumber);
        _progress.CurrentLevelNumber = _currentLevelNumber;
        SaveProgress();
        ShowGamePage(_currentLevelNumber);
    }

    /// <summary>清空指定关卡的辅助资源使用记录。</summary>
    private void OnResetCurrentLevelAssistRequested(int levelNumber)
    {
        var usage = _progress.GetOrCreateLevelAssistUsage(levelNumber);
        usage.RestartUsedCount = 0;
        usage.HintUsedCount = 0;
        SaveProgress();
    }

    /// <summary>重置整个外围进度并重新初始化新手状态。</summary>
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

    /// <summary>加载关卡目录、规则目录和宠物定义表。</summary>
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

    /// <summary>从存档载入外围进度。</summary>
    private void LoadProgress()
    {
        _progress = PlayerProgressStore.LoadOrCreate();
        _currentLevelNumber = Math.Max(1, _progress.CurrentLevelNumber);
    }

    /// <summary>把当前外围进度写回存档。</summary>
    private void SaveProgress()
    {
        PlayerProgressStore.Save(_progress);
    }

    /// <summary>补齐首进游戏所需的默认金币、默认宠物和救助中心状态。</summary>
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
                PetName = GenerateRandomPetName(starterPet),
                AdoptedAtUtc = DateTime.UtcNow.ToString("O"),
                CurrentParkState = starterPet.ParkActivityText,
            });
        }

        EnsureOwnedPetNames();
        EnsureRescueCenterReady(forceRefresh: _progress.RescueCenterPetIds.Count == 0);
        SaveProgress();
    }

    /// <summary>确保所有已领养宠物都有可展示的昵称。</summary>
    private void EnsureOwnedPetNames()
    {
        foreach (var ownedPet in _progress.OwnedPets)
        {
            if (ownedPet is null || !string.IsNullOrWhiteSpace(ownedPet.PetName))
            {
                continue;
            }

            if (_petCatalog.TryGetDefinition(ownedPet.PetId, out var definition))
            {
                ownedPet.PetName = GenerateRandomPetName(definition);
                continue;
            }

            ownedPet.PetName = GenerateFallbackPetName();
        }
    }

    /// <summary>按宠物物种规则生成一个尽量不重复的随机昵称。</summary>
    private string GenerateRandomPetName(PetDefinition definition)
    {
        var suffixes = ResolvePetNameSuffixes(definition);
        for (var attempt = 0; attempt < 8; attempt++)
        {
            var candidate = $"{PetNamePrefixes[_random.RandiRange(0, PetNamePrefixes.Length - 1)]}{suffixes[_random.RandiRange(0, suffixes.Length - 1)]}";
            if (!HasOwnedPetName(candidate))
            {
                return candidate;
            }
        }

        return $"{GenerateFallbackPetName()}{_progress.OwnedPets.Count + 1}";
    }

    /// <summary>检查当前存档中是否已经存在相同昵称。</summary>
    private bool HasOwnedPetName(string petName)
    {
        foreach (var ownedPet in _progress.OwnedPets)
        {
            if (ownedPet is null)
            {
                continue;
            }

            if (string.Equals(ownedPet.PetName, petName, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>在常规生成失败时给出兜底昵称。</summary>
    private string GenerateFallbackPetName()
    {
        return $"{PetNamePrefixes[_random.RandiRange(0, PetNamePrefixes.Length - 1)]}{DefaultNameSuffixes[_random.RandiRange(0, DefaultNameSuffixes.Length - 1)]}";
    }

    /// <summary>根据宠物定义推断应使用哪组昵称后缀。</summary>
    private static string[] ResolvePetNameSuffixes(PetDefinition definition)
    {
        var petId = definition.PetId?.ToLowerInvariant() ?? string.Empty;
        return petId switch
        {
            var id when id.Contains("cat", StringComparison.Ordinal) => CatNameSuffixes,
            var id when id.Contains("dog", StringComparison.Ordinal) => DogNameSuffixes,
            var id when id.Contains("bunny", StringComparison.Ordinal) || id.Contains("rabbit", StringComparison.Ordinal) => BunnyNameSuffixes,
            var id when id.Contains("hamster", StringComparison.Ordinal) => HamsterNameSuffixes,
            var id when id.Contains("fox", StringComparison.Ordinal) => FoxNameSuffixes,
            _ => DefaultNameSuffixes,
        };
    }

    /// <summary>确保救助中心列表处于可展示状态，必要时立即刷新。</summary>
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

    /// <summary>判断当前是否已到救助中心刷新窗口。</summary>
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

    /// <summary>重新抽取一批尚未被领养的宠物进入救助中心。</summary>
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

    /// <summary>生成救助中心刷新成功后的反馈文案。</summary>
    private string BuildRescueRefreshFeedback()
    {
        var count = _progress.RescueCenterPetIds.Count;
        if (count == 0)
        {
            return "救助中心刚刚刷新，但暂时没有新的待救助动物。";
        }

        return $"救助中心已按北京时间刷新，本次来了 {count} 只等待救助的小动物。上次刷新时间：{_progress.LastRescueRefreshBeijingTime}。";
    }

    /// <summary>生成尚未到刷新窗口时的说明文案。</summary>
    private string BuildRescueStatusFeedback()
    {
        if (string.IsNullOrWhiteSpace(_progress.LastRescueRefreshBeijingTime))
        {
            return "救助中心正在准备新的待救助动物。";
        }

        return $"救助中心尚未到下一个刷新窗口。上次刷新时间：{_progress.LastRescueRefreshBeijingTime}（北京时间）。";
    }

    /// <summary>获取当前北京时间，并兼容不同平台的时区标识。</summary>
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

    /// <summary>解析存档中保存的北京时间文本。</summary>
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

    /// <summary>尝试发放当日首次通关奖励。</summary>
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

    /// <summary>按关卡号解析关卡配置，失败时回退到默认关卡。</summary>
    private LevelConfig? GetLevelOrFallback(int levelNumber)
    {
        return LevelCatalog?.ResolveLevelOrFallback(levelNumber);
    }

    /// <summary>生成关卡标题文本。</summary>
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

    /// <summary>生成首页和结算页会展示的关卡摘要。</summary>
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

    /// <summary>把规则档案 id 转成玩家可读名称。</summary>
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
