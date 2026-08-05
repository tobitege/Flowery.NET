namespace Flowery.NET.Kanban.Controls.Users;

/// <summary>
/// Optional interface for providers that can persist auth tokens.
/// </summary>
public interface ITokenSaveProvider
{
    /// <summary>
    /// Persists the provided token for future use.
    /// </summary>
    void SaveToken(string token);
}
