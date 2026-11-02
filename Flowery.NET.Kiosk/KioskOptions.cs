namespace Flowery.NET.Kiosk;

/// <summary>The command line of the kiosk: <c>--manual</c> and <c>--smoke-test &lt;absolute XML path&gt;</c>.</summary>
public sealed record KioskOptions(bool Manual, string? SmokeReport)
{
    public static KioskOptions Parse(IReadOnlyList<string> arguments)
    {
        var manual = false;
        string? report = null;
        for (var index = 0; index < arguments.Count; index++)
        {
            switch (arguments[index])
            {
                case "--manual":
                    manual = true;
                    break;
                case "--smoke-test":
                    if (++index >= arguments.Count || !Path.IsPathFullyQualified(arguments[index]))
                        throw new ArgumentException("--smoke-test requires an absolute XML report path.");
                    report = Path.GetFullPath(arguments[index]);
                    manual = true;
                    break;
                default:
                    throw new ArgumentException($"Unknown kiosk option '{arguments[index]}'.");
            }
        }
        return new KioskOptions(manual, report);
    }
}
