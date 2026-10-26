using System.Globalization;
using Flowery.Localization;

namespace Flowery.NET.Tests;

/// <summary>Puts back the localization culture and the thread cultures when disposed.</summary>
internal sealed class CultureScope : IDisposable
{
    private readonly CultureInfo _localization = FloweryLocalization.CurrentCulture;
    private readonly CultureInfo _culture = CultureInfo.CurrentCulture;
    private readonly CultureInfo _uiCulture = CultureInfo.CurrentUICulture;
    private readonly CultureInfo? _defaultCulture = CultureInfo.DefaultThreadCurrentCulture;
    private readonly CultureInfo? _defaultUICulture = CultureInfo.DefaultThreadCurrentUICulture;

    public void Dispose()
    {
        FloweryLocalization.SetCulture(_localization);
        CultureInfo.CurrentCulture = _culture;
        CultureInfo.CurrentUICulture = _uiCulture;
        CultureInfo.DefaultThreadCurrentCulture = _defaultCulture;
        CultureInfo.DefaultThreadCurrentUICulture = _defaultUICulture;
    }
}
