namespace Flowery.NET.Kanban.Controls.Users;

/// <summary>
/// Optional interface for providers that can persist auth tokens.
/// </summary>
internal interface ITokenSaveProvider
{
    /// <summary>
    /// Persists the provided token for future use as an atomic replacement.
    /// A failed call must leave the previously stored token unchanged.
    /// </summary>
    void SaveToken(string token);

    /// <summary>
    /// Removes the persisted token.
    /// </summary>
    void DeleteToken();
}
