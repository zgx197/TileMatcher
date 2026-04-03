using System.Collections.Generic;

namespace TileMatcher.Layout;

public sealed class LayoutValidationResult
{
    public List<string> Errors { get; } = [];

    public bool IsValid => Errors.Count == 0;
}
