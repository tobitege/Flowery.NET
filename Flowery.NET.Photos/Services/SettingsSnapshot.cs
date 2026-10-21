namespace Flowery.NET.Photos.Services
{
    /// <summary>
    /// Captures everything the settings dialog can change so "Cancel" can restore it.
    /// Changes apply live as a preview while the dialog is open.
    /// </summary>
    public sealed record SettingsSnapshot(
        string? Theme,
        DaisySize Size,
        string Culture,
        bool IncludeSubfolders,
        bool ConfirmDelete,
        bool WheelNavigates,
        double SlideshowSeconds,
        int JpegQuality,
        IReadOnlyList<string> Folders)
    {
        public static SettingsSnapshot Capture(PhotoLibrary library) => new(
            DaisyThemeManager.CurrentThemeName,
            FlowerySizeManager.CurrentSize,
            FloweryLocalization.CurrentCulture.Name,
            PhotosSettings.IncludeSubfolders,
            PhotosSettings.ConfirmDelete,
            PhotosSettings.WheelNavigates,
            PhotosSettings.SlideshowSeconds,
            PhotosSettings.JpegQuality,
            [.. library.Folders]);

        public void Restore(PhotoLibrary library)
        {
            if (Theme != null && !string.Equals(Theme, DaisyThemeManager.CurrentThemeName, StringComparison.OrdinalIgnoreCase))
                DaisyThemeManager.ApplyTheme(Theme);

            if (Size != FlowerySizeManager.CurrentSize)
                FlowerySizeManager.ApplySize(Size);

            if (!string.Equals(Culture, FloweryLocalization.CurrentCulture.Name, StringComparison.OrdinalIgnoreCase))
                FloweryLocalization.SetCulture(Culture);

            PhotosSettings.ConfirmDelete = ConfirmDelete;
            PhotosSettings.WheelNavigates = WheelNavigates;
            PhotosSettings.SlideshowSeconds = SlideshowSeconds;
            PhotosSettings.JpegQuality = JpegQuality;
            PhotosSettings.SaveOptions();

            library.RestoreFolders(Folders, IncludeSubfolders);
        }
    }
}
