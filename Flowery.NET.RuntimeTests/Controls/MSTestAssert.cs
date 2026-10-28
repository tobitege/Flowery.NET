using System.Collections;
using System.Diagnostics.CodeAnalysis;
using Xunit.Sdk;

namespace Flowery.NET.RuntimeTests.Controls;

// The tour tests were written for MSTest in Flowery.WinUI. These adapters keep their assertions and messages
// unchanged on xunit: a file aliases Assert, CollectionAssert and StringAssert to them.

/// <summary>An assertion failure with a message, like MSTest's exception of that name.</summary>
internal sealed class AssertFailedException : XunitException
{
    public AssertFailedException(string message) : base(message) { }
    public AssertFailedException(string message, Exception inner) : base(message, inner) { }
}

internal static class MSTestAssert
{
    public static void AreEqual<T>(T expected, T actual, string? message = null)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            Fail($"Expected:<{expected}>. Actual:<{actual}>. {message}");
    }

    public static void AreEqual(double expected, double actual, double delta, string? message = null)
    {
        // Like MSTest: a NaN or negative delta is a usage error; a NaN delta would pass every comparison, a negative one fail every comparison.
        if (double.IsNaN(delta) || delta < 0)
            throw new ArgumentOutOfRangeException(nameof(delta), delta, "The delta must be a nonnegative number.");
        if (double.IsNaN(expected) != double.IsNaN(actual) || Math.Abs(expected - actual) > delta)
            Fail($"Expected a difference no greater than <{delta}> between <{expected}> and <{actual}>. {message}");
    }

    public static void AreNotEqual<T>(T notExpected, T actual, string? message = null)
    {
        if (EqualityComparer<T>.Default.Equals(notExpected, actual))
            Fail($"Expected any value except:<{notExpected}>. Actual:<{actual}>. {message}");
    }

    public static void AreSame(object? expected, object? actual, string? message = null)
    {
        if (!ReferenceEquals(expected, actual))
            Fail($"Expected the same instance. Expected:<{expected}>. Actual:<{actual}>. {message}");
    }

    public static void AreNotSame(object? notExpected, object? actual, string? message = null)
    {
        if (ReferenceEquals(notExpected, actual))
            Fail($"Expected different instances. {message}");
    }

    public static void IsTrue([DoesNotReturnIf(false)] bool condition, string? message = null)
    {
        if (!condition)
            Fail($"Expected true. {message}");
    }

    public static void IsFalse([DoesNotReturnIf(true)] bool condition, string? message = null)
    {
        if (condition)
            Fail($"Expected false. {message}");
    }

    public static void IsNull(object? value, string? message = null)
    {
        if (value is not null)
            Fail($"Expected null. Actual:<{value}>. {message}");
    }

    public static void IsNotNull([NotNull] object? value, string? message = null)
    {
        if (value is null)
            Fail($"Expected a value. {message}");
    }

    public static T IsInstanceOfType<T>([NotNull] object? value, string? message = null)
    {
        if (value is not T typed)
            throw new AssertFailedException($"Expected type:<{typeof(T)}>. Actual type:<{value?.GetType()}>. {message}");
        return typed;
    }

    public static void HasCount(int expected, IEnumerable collection, string? message = null)
    {
        var actual = collection.Cast<object?>().Count();
        if (actual != expected)
            Fail($"Expected collection of size {expected}. Actual: {actual}. {message}");
    }

    public static void IsEmpty(IEnumerable collection, string? message = null) => HasCount(0, collection, message);

    public static void IsNotEmpty(IEnumerable collection, string? message = null)
    {
        if (!collection.Cast<object?>().Any())
            Fail($"Expected a nonempty collection. {message}");
    }

    [DoesNotReturn]
    public static void Fail(string? message = null) => throw new AssertFailedException(message ?? "Assert.Fail failed.");

    /// <summary>MSTest's inconclusive result is a skipped test in xunit.</summary>
    [DoesNotReturn]
    public static void Inconclusive(string? message = null)
    {
        Xunit.Assert.Skip(message ?? "Inconclusive.");
        throw new InvalidOperationException("Unreachable.");
    }

    public static T Throws<T>(Action action, string? message = null) where T : Exception
    {
        try { action(); }
        catch (T error) { return error; }
        catch (Exception error) { throw new AssertFailedException($"Expected {typeof(T)}, got {error.GetType()}: {error.Message}. {message}", error); }
        throw new AssertFailedException($"Expected {typeof(T)}, nothing was thrown. {message}");
    }

    public static T ThrowsExactly<T>(Action action, string? message = null) where T : Exception
    {
        try { action(); }
        catch (Exception error) when (error.GetType() == typeof(T)) { return (T)error; }
        catch (Exception error) { throw new AssertFailedException($"Expected exactly {typeof(T)}, got {error.GetType()}: {error.Message}. {message}", error); }
        throw new AssertFailedException($"Expected exactly {typeof(T)}, nothing was thrown. {message}");
    }

    public static async Task<T> ThrowsExactlyAsync<T>(Func<Task> action, string? message = null) where T : Exception
    {
        try { await action(); }
        catch (Exception error) when (error.GetType() == typeof(T)) { return (T)error; }
        catch (Exception error) { throw new AssertFailedException($"Expected exactly {typeof(T)}, got {error.GetType()}: {error.Message}. {message}", error); }
        throw new AssertFailedException($"Expected exactly {typeof(T)}, nothing was thrown. {message}");
    }
}

internal static class MSTestCollectionAssert
{
    public static void AreEqual(IEnumerable? expected, IEnumerable? actual, string? message = null)
    {
        if (BothNull(expected, actual, message)) return;
        var left = expected.Cast<object?>().ToArray();
        var right = actual.Cast<object?>().ToArray();
        if (!left.SequenceEqual(right))
            MSTestAssert.Fail($"Expected:<{string.Join(", ", left)}>. Actual:<{string.Join(", ", right)}>. {message}");
    }

    // The same elements, each as often, in any order; elements compare with Equals like MSTest.
    public static void AreEquivalent(IEnumerable? expected, IEnumerable? actual, string? message = null)
    {
        if (BothNull(expected, actual, message)) return;
        var left = expected.Cast<object?>().ToList();
        var right = actual.Cast<object?>().ToList();
        var unmatched = new List<object?>(right);
        var missing = left.Where(item =>
        {
            var index = unmatched.FindIndex(candidate => Equals(candidate, item));
            if (index < 0) return true;
            unmatched.RemoveAt(index);
            return false;
        }).ToList();
        if (missing.Count > 0 || unmatched.Count > 0)
            MSTestAssert.Fail($"Expected the elements:<{string.Join(", ", left)}>. Actual:<{string.Join(", ", right)}>. {message}");
    }

    // Like MSTest: two null collections are equal, and one null collection fails. True when nothing is left to compare.
    private static bool BothNull([NotNullWhen(false)] IEnumerable? expected, [NotNullWhen(false)] IEnumerable? actual, string? message)
    {
        if (expected is null && actual is null) return true;
        if (expected is null || actual is null)
            MSTestAssert.Fail($"Expected:<{(expected is null ? "(null)" : "a collection")}>. Actual:<{(actual is null ? "(null)" : "a collection")}>. {message}");
        return false;
    }
}

internal static class MSTestStringAssert
{
    public static void Contains(string? value, string substring, string? message = null)
    {
        if (value is null || !value.Contains(substring, StringComparison.Ordinal))
            MSTestAssert.Fail($"String '{value}' does not contain string '{substring}'. {message}");
    }

    public static void StartsWith(string? value, string prefix, string? message = null)
    {
        if (value is null || !value.StartsWith(prefix, StringComparison.Ordinal))
            MSTestAssert.Fail($"String '{value}' does not start with string '{prefix}'. {message}");
    }
}
