using System.Threading;
using System.Threading.Tasks;

namespace Flowery.NET.Kanban.Controls.Users;

internal interface ITokenValidationProvider
{
    Task<ProviderTokenValidationResult> ValidateAccessAsync(
        string token,
        CancellationToken cancellation = default);
}

internal enum ProviderTokenValidationStatus
{
    Success,
    MissingScope,
    Invalid,
    NoToken,
    Error
}

internal sealed class ProviderTokenValidationResult
{
    private ProviderTokenValidationResult(ProviderTokenValidationStatus status, string? message)
    {
        Status = status;
        Message = message;
    }

    public ProviderTokenValidationStatus Status { get; }
    public string? Message { get; }

    public bool IsSuccess => Status == ProviderTokenValidationStatus.Success;
    public bool IsMissingScope => Status == ProviderTokenValidationStatus.MissingScope;

    public static ProviderTokenValidationResult Success() => new(ProviderTokenValidationStatus.Success, null);
    public static ProviderTokenValidationResult MissingScope(string? message) => new(ProviderTokenValidationStatus.MissingScope, message);
    public static ProviderTokenValidationResult Invalid(string? message) => new(ProviderTokenValidationStatus.Invalid, message);
    public static ProviderTokenValidationResult NoToken() => new(ProviderTokenValidationStatus.NoToken, null);
    public static ProviderTokenValidationResult Error(string? message) => new(ProviderTokenValidationStatus.Error, message);
}
