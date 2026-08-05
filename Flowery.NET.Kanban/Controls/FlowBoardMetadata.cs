using System;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace Flowery.NET.Kanban.Controls
{
    /// <summary>
    /// Lightweight metadata for listing available Kanban boards.
    /// </summary>
    public sealed class FlowBoardMetadata : INotifyPropertyChanged
    {
        private string _id = string.Empty;
        private string _title = string.Empty;
        private DateTime _lastModified;
        private Geometry? _thumbnailGeometry;

        public event PropertyChangedEventHandler? PropertyChanged;

        public string Id
        {
            get => _id;
            set => SetProperty(ref _id, value ?? string.Empty);
        }

        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value ?? string.Empty);
        }

        public DateTime LastModified
        {
            get => _lastModified;
            set
            {
                if (SetProperty(ref _lastModified, value))
                {
                    OnPropertyChanged(nameof(LastModifiedDisplay));
                }
            }
        }

        public string LastModifiedDisplay => FormatLastModified(_lastModified);

        public Geometry? ThumbnailGeometry
        {
            get => _thumbnailGeometry;
            set => SetProperty(ref _thumbnailGeometry, value);
        }

        private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, value))
                return false;

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private static string FormatLastModified(DateTime value)
        {
            if (value == DateTime.MinValue)
                return string.Empty;

            var local = value.Kind == DateTimeKind.Local ? value : value.ToLocalTime();
            var culture = CultureInfo.CurrentCulture;
            return string.Concat(local.ToString("d", culture), " ", local.ToString("t", culture));
        }
    }
}
