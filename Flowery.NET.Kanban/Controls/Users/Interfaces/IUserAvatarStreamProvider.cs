using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Flowery.NET.Kanban.Controls.Users;

/// <summary>
/// Provides authenticated or provider-specific avatar content without requiring
/// the Kanban UI to fetch arbitrary user-supplied URLs.
/// </summary>
internal interface IUserAvatarStreamProvider
{
    /// <summary>
    /// Opens an avatar stream for the specified user, or returns null when no avatar is available.
    /// The caller owns and disposes the returned stream.
    /// </summary>
    Task<Stream?> OpenAvatarStreamAsync(
        IFlowUser user,
        CancellationToken cancellation = default);
}
