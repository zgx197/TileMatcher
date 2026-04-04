namespace TileMatcher.Offline.Tests;

internal static class TestAssert
{
    public static void True(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    public static void Equal<T>(T expected, T actual, string message)
        where T : notnull
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException($"{message} Expected={expected}, Actual={actual}");
        }
    }

    public static void NearlyEqual(double expected, double actual, double tolerance, string message)
    {
        if (Math.Abs(expected - actual) > tolerance)
        {
            throw new InvalidOperationException(
                $"{message} Expected={expected:F4}, Actual={actual:F4}, Tolerance={tolerance:F4}");
        }
    }

    public static void Contains(string expectedSubstring, IEnumerable<string> values, string message)
    {
        if (!values.Any(value => value.Contains(expectedSubstring, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException($"{message} Missing={expectedSubstring}");
        }
    }
}
