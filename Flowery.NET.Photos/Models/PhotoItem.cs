using System.ComponentModel;
using System.IO;

namespace Flowery.NET.Photos.Models
{
    /// <summary>
    /// A single photo in the library.
    /// </summary>
    public sealed class PhotoItem : INotifyPropertyChanged
    {
        private bool _isFavorite;
        private bool _isSelected;

        public PhotoItem(string path)
        {
            Path = path;
            FileName = System.IO.Path.GetFileName(path);
            FolderPath = System.IO.Path.GetDirectoryName(path) ?? string.Empty;
            Extension = System.IO.Path.GetExtension(path).TrimStart('.').ToUpperInvariant();
        }

        public string Path { get; }
        public string FileName { get; }
        public string FolderPath { get; }
        public string Extension { get; }

        public long FileSize { get; set; }
        public DateTime DateModified { get; set; }
        public DateTime? DateTaken { get; set; }
        public int PixelWidth { get; set; }
        public int PixelHeight { get; set; }
        public string? CameraModel { get; set; }
        public Services.PhotoDetails? Details { get; set; }

        /// <summary>
        /// Incremented when the file content changes (e.g. after saving an edit) so cached thumbnails refresh.
        /// </summary>
        public int Revision { get; set; }

        public DateTime SortDate => DateTaken ?? DateModified;

        public double AspectRatio =>
            PixelWidth > 0 && PixelHeight > 0 ? Math.Clamp((double)PixelWidth / PixelHeight, 0.25, 4.0) : 1.5;

        public bool IsFavorite
        {
            get => _isFavorite;
            set
            {
                if (_isFavorite == value)
                    return;
                _isFavorite = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsFavorite)));
            }
        }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected == value)
                    return;
                _isSelected = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
            }
        }

        public bool Exists => File.Exists(Path);

        public event PropertyChangedEventHandler? PropertyChanged;

        public override string ToString() => FileName;
    }

    public sealed class PhotoAlbum
    {
        public PhotoAlbum(string name, IEnumerable<string>? paths = null)
        {
            Name = name;
            if (paths != null)
            {
                foreach (var p in paths)
                    Paths.Add(p);
            }
        }

        public string Name { get; set; }

        /// <summary>
        /// Ordered, unique photo paths.
        /// </summary>
        public List<string> Paths { get; } = [];
    }

    /// <summary>
    /// Folder summary used by the Folders view.
    /// </summary>
    public sealed record PhotoFolderInfo(string Path, string Name, int Count, PhotoItem? Cover, bool IsRoot);
}
