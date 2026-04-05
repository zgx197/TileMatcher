using System;
using System.Threading.Tasks;
using Godot;
using TileMatcher.Logging;

namespace TileMatcher.App;

public partial class AppRoot
{
    private const string BuildPackageNameSettingPath = "tilematcher_build/package_name";
    private const string BuildVersionNameSettingPath = "tilematcher_build/version_name";
    private const string BuildVersionCodeSettingPath = "tilematcher_build/version_code";

    private bool _globalExceptionHooksRegistered;

    public override void _EnterTree()
    {
        base._EnterTree();
        RuntimeLog.Initialize();
        RegisterGlobalExceptionHooks();
        RuntimeLog.Info("AppRoot", BuildStartupSummary());
    }

    public override void _ExitTree()
    {
        UnregisterGlobalExceptionHooks();
        RuntimeLog.Shutdown();
        base._ExitTree();
    }

    private void RegisterGlobalExceptionHooks()
    {
        if (_globalExceptionHooksRegistered)
        {
            return;
        }

        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        _globalExceptionHooksRegistered = true;
    }

    private void UnregisterGlobalExceptionHooks()
    {
        if (!_globalExceptionHooksRegistered)
        {
            return;
        }

        AppDomain.CurrentDomain.UnhandledException -= OnUnhandledException;
        TaskScheduler.UnobservedTaskException -= OnUnobservedTaskException;
        _globalExceptionHooksRegistered = false;
    }

    private void OnUnhandledException(object? sender, UnhandledExceptionEventArgs args)
    {
        if (args.ExceptionObject is Exception exception)
        {
            RuntimeLog.Fatal("AppRoot", $"捕获到未处理异常。IsTerminating={args.IsTerminating}", exception);
            return;
        }

        RuntimeLog.Fatal("AppRoot", $"捕获到未处理异常对象。IsTerminating={args.IsTerminating}, payload={args.ExceptionObject}");
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs args)
    {
        RuntimeLog.Error("AppRoot", "捕获到未观察的 Task 异常。", args.Exception);
        args.SetObserved();
    }

    private static string BuildStartupSummary()
    {
        var appName = ReadProjectSetting("application/config/name", "TileMatcher");
        var packageName = ReadProjectSetting(BuildPackageNameSettingPath, "unknown.package");
        var versionName = ReadProjectSetting(BuildVersionNameSettingPath, "0.0.0");
        var versionCode = ReadProjectSetting(BuildVersionCodeSettingPath, "0");
        return $"启动应用: name={appName}, package={packageName}, version={versionName} ({versionCode}), os={OS.GetName()}, log_dir={RuntimeLog.LogDirectoryPath}";
    }

    private static string ReadProjectSetting(string settingPath, string fallback)
    {
        if (!ProjectSettings.HasSetting(settingPath))
        {
            return fallback;
        }

        var value = ProjectSettings.GetSetting(settingPath).AsString();
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }
}
