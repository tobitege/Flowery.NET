using System.Threading;
using System.Threading.Tasks;

namespace Flowery.NET.Kanban.Controls.Users;

/// <summary>
/// Optional interface for providers that can perform interactive authentication.
/// </summary>
public interface IInteractiveAuthProvider
{
    /// <summary>
    /// Launches the interactive auth flow and returns true on success.
    /// </summary>
    Task<bool> AuthenticateAsync(CancellationToken cancellation = default);
}
