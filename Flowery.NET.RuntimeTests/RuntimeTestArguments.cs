namespace Flowery.NET.RuntimeTests;

internal static class RuntimeTestArguments
{
    private const string Option = "--runtime-tests";

    /// <summary>
    /// Reads the report path from <c>--runtime-tests &lt;path&gt;</c>, <c>--runtime-tests=&lt;path&gt;</c>
    /// or the <c>FLOWERY_RUNTIME_TESTS_PATH</c> environment variable.
    /// </summary>
    public static bool TryGetReportPath(string[] args, out string reportPath)
    {
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i].StartsWith(Option + "=", StringComparison.OrdinalIgnoreCase))
            {
                reportPath = args[i][(Option.Length + 1)..];
                return Complete(ref reportPath);
            }

            if (string.Equals(args[i], Option, StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                reportPath = args[i + 1];
                return Complete(ref reportPath);
            }
        }

        reportPath = Environment.GetEnvironmentVariable("FLOWERY_RUNTIME_TESTS_PATH") ?? string.Empty;
        return Complete(ref reportPath);
    }

    private static bool Complete(ref string reportPath)
    {
        reportPath = reportPath.Trim().Trim('"');
        if (reportPath.Length == 0)
            return false;

        reportPath = Path.GetFullPath(reportPath);
        return true;
    }
}
