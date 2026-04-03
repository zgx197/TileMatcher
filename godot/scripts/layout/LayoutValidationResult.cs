using System.Collections.Generic;

namespace TileMatcher.Layout;

/// <summary>
/// 布局校验结果。
/// </summary>
/// <remarks>
/// 当前保留全部错误，而不是第一条错误就返回，
/// 这样调试布局时可以一次看到完整问题列表。
/// </remarks>
public sealed class LayoutValidationResult
{
    /// <summary>所有校验错误文本。</summary>
    public List<string> Errors { get; } = [];

    /// <summary>当前是否通过全部校验。</summary>
    public bool IsValid => Errors.Count == 0;
}
