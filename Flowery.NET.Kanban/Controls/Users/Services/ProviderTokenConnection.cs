using System;
using System.Threading;
using System.Threading.Tasks;

namespace Flowery.NET.Kanban.Controls.Users;

internal static class ProviderTokenConnection
{
    public static bool CanConnect(IUserProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        return provider is ITokenSaveProvider and ITokenValidationProvider;
    }

    public static async Task<ProviderTokenValidationResult> ValidateAndSaveAsync(
        IUserProvider provider,
        string token,
        CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(provider);
        if (string.IsNullOrWhiteSpace(token))
            throw new ArgumentException("Token must be provided.", nameof(token));

        if (provider is not ITokenSaveProvider tokenStore
            || provider is not ITokenValidationProvider validator)
        {
            throw new NotSupportedException(
                $"Provider '{provider.ProviderKey}' does not support validated token connections.");
        }

        var validation = await validator.ValidateAccessAsync(token, cancellation).ConfigureAwait(false)
                         ?? throw new InvalidOperationException(
                             $"Provider '{provider.ProviderKey}' returned no token validation result.");
        cancellation.ThrowIfCancellationRequested();
        if (!validation.IsSuccess)
            return validation;

        tokenStore.SaveToken(token);
        return validation;
    }

    public static void Disconnect(IUserProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        if (provider is not ITokenSaveProvider tokenStore)
        {
            throw new NotSupportedException(
                $"Provider '{provider.ProviderKey}' does not support token removal.");
        }

        tokenStore.DeleteToken();
    }
}
