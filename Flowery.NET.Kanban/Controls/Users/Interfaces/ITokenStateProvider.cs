namespace Flowery.NET.Kanban.Controls.Users;

/// <summary>
/// Optional interface for providers that expose token state.
/// </summary>
public interface ITokenStateProvider
{
    /// <summary>
    /// True when a token is currently available.
    /// </summary>
    bool HasToken { get; }
}
