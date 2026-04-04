# Godot-CSharp-接入问题记录

## 背景

项目采用 Godot 4.6.1 Mono + C# 开发，因此在早期搭建阶段，除了游戏逻辑本身，还需要先解决工程接入、程序集加载和编辑器资源识别问题。

## 目标

目标是建立一套稳定的 C# 工程接入与排错路径，确保：

- 场景可以正常加载
- C# 程序集能被 Godot 识别
- 资源与脚本能在 Inspector 中正常工作

## 问题 1：Godot 场景加载失败与程序集未加载

### 现象

项目早期出现过：

- 场景无法正常进入
- Godot 提示程序集未加载
- C# 脚本无法正确绑定

### 原因

根因不在 `.tscn` 场景本身，而在于：

- `.csproj / .sln` 生成与加载链路未稳定接通
- Godot Mono 没有正确识别工程程序集

### 解决方案

- 修复 Godot C# 工程接入
- 确保 `dotnet build` 可以稳定通过
- 把日志抓取作为标准排错入口

### 当前结论

当前遇到“场景打不开”时，不应先假设场景损坏，而应优先检查：

- C# 工程是否正常生成
- 程序集是否正常加载
- Godot Mono 构建链路是否异常

## 问题 2：需要稳定抓取 Godot / Mono / 运行时日志

### 现象

仅靠编辑器输出面板，很多问题不够稳定，也不便于 AI agent 接手复现。

### 解决方案

项目明确采用日志抓取技能与脚本作为标准排错路径：

- `godot-log-capture`
- `capture-godot-logs.ps1`

后续又补充确认了一个 Windows + Godot 4.6.1 Mono 的诊断细节：

- 如果用 PowerShell 管道或 stdout/stderr 重定向去包裹 `Godot.exe --headless`
- Godot 可能会先报 `Failed to open 'user://logs/...'`
- 随后 headless 进程直接崩溃

因此仓库内新增了稳定版脚本：

- `scripts/capture-godot-logs.ps1`

这份脚本的策略是：

- 不再用 PowerShell 管道包裹 Godot 进程
- 改为让 Godot 自己通过 `--log-file` 直接落盘
- 再由脚本读取退出码、复制 runtime logs 和 Mono build logs

这样可以绕开 `user://logs` 链路在诊断场景下的崩溃点。

### 当前结论

后续遇到以下问题时，优先抓日志而不是先猜：

- 场景加载失败
- 按钮看似无反应
- Inspector 资源异常
- C# 构建问题

## 问题 3：Resource 资源在 Inspector 中看不到内容

### 现象

在引入 `TileShapeConfig / LayoutRuleConfig / LayoutProfileConfig / LayoutProfileCatalog` 后，一度出现：

- `.tres` 资源双击后 Inspector 空白
- Godot 输出出现资源类识别错误

### 关键报错

曾抓到的关键错误为：

- `ERROR: Cannot get class 'TileShapeConfig'.`

### 原因

问题并不是 C# 类本身没编译通过，而是：

- 手写 `.tres` 资源头时，把 `type` 直接写成了自定义 C# 资源类名
- 当前 Godot C# 资源加载链路对这种写法不稳定

### 解决方案

最终采用更兼容的写法：

- `type="Resource"`
- 保留 `script_class=...`
- 保留 `script = ExtResource(...)`

同时：

- 把 `default_catalog.tres` 明确挂到场景中的 `BoardController.ProfileCatalog`
- 保留默认路径回退作为兜底

### 当前结论

如果后续再次手写新的 `.tres` 配置资源，不要直接把 `type` 写成自定义 C# 类名，优先沿用当前已验证通过的格式。

## 当前结论

当前项目在 Godot C# 接入方面已经形成三条稳定约束：

- `dotnet build` 必须保持通过
- 日志抓取必须作为标准排错手段
- `.tres` 资源头格式必须使用当前已验证的兼容写法

## 后续待验证

后续仍可继续完善：

- 是否需要增加一份专门的“Godot 工程健康检查清单”
- 是否需要把常见接入问题整理成自动化诊断脚本输出
