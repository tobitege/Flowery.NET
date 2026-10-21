using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Flowery.NET.Photos.Localization;
using Flowery.NET.Photos.Models;
using Flowery.NET.Photos.Services;

namespace Flowery.NET.Photos.Views;

public partial class PhotosShell
{
    private double _homeWidth;

    private void BuildHome()
    {
        if (!HomeScroll.IsVisible) return;
        _homeWidth = HomeScroll.Bounds.Width;
        var width = Math.Max(300, _homeWidth - 48);
        var empty = _library.Items.Count == 0;
        Home.Children.Clear();
        Home.Children.Add(CreateHero(empty, width));
        if (empty)
        {
            var features = new UniformGrid { Columns = width >= 760 ? 3 : 1 };
            foreach (var section in new[] { "Organize", "Edit", "Show" })
                features.Children.Add(new DaisyCard
                {
                    Classes = { "photo-card" }, Margin = new Thickness(8),
                    Content = new StackPanel
                    {
                        Spacing = 8,
                        Children =
                        {
                            new TextBlock { Text = L("Home_Feature" + section + "Title"), Classes = { "section-heading" } },
                            new TextBlock { Text = L("Home_Feature" + section + "Text"), TextWrapping = TextWrapping.Wrap }
                        }
                    }
                });
            Home.Children.Add(features);
            return;
        }

        var statistics = new UniformGrid { Columns = width >= 760 ? 4 : 2 };
        foreach (var (key, page, count) in new[]
        {
            ("Home_Photos", "Gallery", _library.Items.Count),
            ("Nav_Favorites", "Favorites", _library.Items.Count(i => i.IsFavorite)),
            ("Nav_Albums", "Albums", _library.Albums.Count),
            ("Nav_Folders", "Folders", _library.Folders.Count)
        })
        {
            var content = new StackPanel { Spacing = 4 };
            content.Children.Add(new TextBlock { Text = count.ToString("N0", PhotosLocalization.CurrentCulture), Classes = { "heading" } });
            content.Children.Add(new TextBlock { Text = L(key), Opacity = .7 });
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
            row.Children.Add(new PathIcon { Data = NavigationGeometry(page), Width = 28, Height = 28,
                Margin = new Thickness(0, 0, 14, 0), Foreground = this.FindResource("DaisyPrimaryBrush") as IBrush });
            Grid.SetColumn(content, 1); row.Children.Add(content);
            statistics.Children.Add(CreateCard("Photos.Stats." + page, L(key), row, () => NavigateAsync(page)));
        }
        Home.Children.Add(statistics);

        var recent = _library.Items.OrderByDescending(i => i.SortDate).ToList();
        var columns = Math.Max(1, (int)((width + 12) / 174));
        var side = Math.Floor(width / columns) - 16;
        AddPhotoSection("Home_Recent", "Gallery", recent, columns, side);
        AddPhotoSection("Nav_Favorites", "Favorites", recent.Where(i => i.IsFavorite).ToList(), columns, side);

        var folders = _library.GetFolderSummaries().ToList();
        if (folders.Count > 0)
        {
            var grid = new UniformGrid { Columns = Math.Max(1, (int)(width / 236)) };
            foreach (var folder in folders.Take(grid.Columns))
                grid.Children.Add(CreateCollectionCard(PhotoSourceKind.Folder, folder.Path, folder.Name,
                    folder.Path, folder.Count, folder.Cover, folder.IsRoot));
            Home.Children.Add(CreateSection("Nav_Folders", "Folders", grid));
        }
        if (_library.Albums.Count > 0)
        {
            var grid = new UniformGrid { Columns = Math.Max(1, (int)(width / 236)) };
            foreach (var album in _library.Albums.Take(grid.Columns))
            {
                var items = _library.GetAlbumItems(album).ToList();
                grid.Children.Add(CreateCollectionCard(PhotoSourceKind.Album, album.Name, album.Name,
                    AlbumSubtitle(items), items.Count, items.FirstOrDefault()));
            }
            Home.Children.Add(CreateSection("Nav_Albums", "Albums", grid));
        }
    }

    private DaisyCard CreateHero(bool empty, double width)
    {
        var content = new StackPanel { Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
        content.Children.Add(new TextBlock
        {
            Text = L(empty ? "Home_WelcomeTitle" : DateTime.Now.Hour is >= 5 and < 12 ? "Home_GreetingMorning" :
                DateTime.Now.Hour < 18 ? "Home_GreetingAfternoon" : "Home_GreetingEvening"),
            Classes = { "heading" }, TextWrapping = TextWrapping.Wrap
        });
        content.Children.Add(new TextBlock
        {
            Text = empty ? L("Home_WelcomeText") : PhotosLocalization.Format("Photos_Settings_LibraryStats",
                _library.Items.Count, _library.Folders.Count, FileOperations.FormatFileSize(_library.Items.Sum(i => i.FileSize))),
            Opacity = .75, TextWrapping = TextWrapping.Wrap
        });
        var actions = new WrapPanel { Margin = new Thickness(0, 12, 0, 0) };
        if (!empty) actions.Children.Add(ActionButton("Home_StartSlideshow", () => StartSlideshowAsync(_library.Items)));
        actions.Children.Add(ActionButton("AddFolder", AddFolderAsync));
        actions.Children.Add(ActionButton("ImportFiles", ImportAsync));
        if (empty || !_library.Folders.Contains(PhotoLibrary.SamplesFolder))
            actions.Children.Add(ActionButton("UseSamples", () => { _library.AddFolder(PhotoLibrary.SamplesFolder); return Task.CompletedTask; }));
        content.Children.Add(actions);

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        grid.Children.Add(content);
        if (width >= 760)
        {
            var art = new Grid { Width = 210, Height = 150, Margin = new Thickness(24, 0, 0, 0), IsHitTestVisible = false };
            var back = new Border { Width = 120, Height = 90, CornerRadius = new CornerRadius(12),
                Background = this.FindResource("DaisySecondaryBrush") as IBrush, Opacity = .35,
                RenderTransform = new RotateTransform(9), Margin = new Thickness(55, -20, 0, 0) };
            var front = new Border { Width = 130, Height = 100, CornerRadius = new CornerRadius(12),
                Background = this.FindResource("DaisyPrimaryBrush") as IBrush,
                RenderTransform = new RotateTransform(-7), Margin = new Thickness(-35, 20, 0, 0),
                Child = new PathIcon { Data = this.FindResource("PhotosIconCamera") as Geometry,
                    Foreground = this.FindResource("DaisyPrimaryContentBrush") as IBrush, Width = 48, Height = 48 } };
            art.Children.Add(back); art.Children.Add(front); Grid.SetColumn(art, 1); grid.Children.Add(art);
        }
        return new DaisyCard { Classes = { "photo-card" }, BodyPadding = new Thickness(28), Content = grid };
    }

    private void AddPhotoSection(string key, string page, List<PhotoItem> items, int columns, double side)
    {
        if (items.Count == 0) return;
        var grid = new UniformGrid { Columns = columns };
        foreach (var item in items.Take(columns))
        {
            var card = CreateCard("Photos.Home." + page + "." + Identity(item.Path), item.FileName,
                new PhotoThumbnail(item) { Height = side, ClipToBounds = true }, () => OpenAsync(item, items));
            card.BodyPadding = new Thickness(3);
            ToolTip.SetTip(card, item.FileName);
            grid.Children.Add(card);
        }
        Home.Children.Add(CreateSection(key, page, grid));
    }

    private StackPanel CreateSection(string key, string page, Control content)
    {
        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), Margin = new Thickness(8, 0) };
        header.Children.Add(new PathIcon { Data = NavigationGeometry(page), Width = 20, Height = 20,
            Foreground = this.FindResource("DaisyPrimaryBrush") as IBrush, Margin = new Thickness(0, 0, 10, 0) });
        var title = new TextBlock { Text = L(key), Classes = { "section-heading" }, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(title, 1); header.Children.Add(title);
        var showAll = ActionButton("Home_ShowAll", () => NavigateAsync(page));
        AutomationProperties.SetAutomationId(showAll, "Photos.ShowAll." + page);
        Grid.SetColumn(showAll, 2); header.Children.Add(showAll);
        var section = new StackPanel { Spacing = 8, Children = { header, content } };
        AutomationProperties.SetAutomationId(section, "Photos.Section." + page);
        return section;
    }

    private void BuildCollections()
    {
        if (_page is not ("Albums" or "Folders") || !CollectionsScroll.IsVisible) return;
        CollectionTitle.Text = L("Nav_" + _page);
        NewAlbum.IsVisible = _page == "Albums";
        AddCollectionFolder.IsVisible = _page == "Folders";
        Collections.Children.Clear();
        var search = Search.Text?.Trim();
        if (_page == "Albums")
        {
            foreach (var album in _library.Albums.Where(a => string.IsNullOrEmpty(search) || a.Name.Contains(search, StringComparison.CurrentCultureIgnoreCase)))
            {
                var items = _library.GetAlbumItems(album).ToList();
                Collections.Children.Add(CreateCollectionCard(PhotoSourceKind.Album, album.Name, album.Name,
                    AlbumSubtitle(items), items.Count, items.FirstOrDefault()));
            }
        }
        else foreach (var folder in _library.GetFolderSummaries().Where(f => string.IsNullOrEmpty(search) ||
            f.Path.Contains(search, StringComparison.CurrentCultureIgnoreCase)))
            Collections.Children.Add(CreateCollectionCard(PhotoSourceKind.Folder, folder.Path, folder.Name,
                folder.Path, folder.Count, folder.Cover, folder.IsRoot));
        CollectionCount.Text = PhotosLocalization.Format("Photos_ItemCount", Collections.Children.Count);
        CollectionEmpty.IsVisible = Collections.Children.Count == 0;
        CollectionEmptyTitle.Text = L("Empty_" + _page + "Title");
        CollectionEmptyText.Text = L("Empty_" + _page + "Text");
        var width = Math.Max(300, CollectionsScroll.Bounds.Width - 48);
        foreach (var card in Collections.Children) card.Width = Math.Floor(width / Math.Max(1, (int)(width / 236))) - 16;
    }

    private PhotoCard CreateCollectionCard(PhotoSourceKind kind, string key, string title, string subtitle,
        int count, PhotoItem? cover, bool root = false)
    {
        var image = new Grid { Height = 150, ClipToBounds = true };
        image.Children.Add(new PathIcon
        {
            Data = kind == PhotoSourceKind.Folder ? this.FindResource("PhotosIconFolder") as Geometry :
                this.FindResource("PhotosIconAlbum") as Geometry,
            Width = 44, Height = 44, Opacity = .35
        });
        if (cover != null) image.Children.Add(new PhotoThumbnail(cover));
        image.Children.Add(new DaisyBadge { Content = count.ToString("N0", PhotosLocalization.CurrentCulture),
            HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(8) });
        var content = new StackPanel { Spacing = 6, Children = { image,
            new TextBlock { Text = title, FontWeight = FontWeight.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis },
            new TextBlock { Text = subtitle, Opacity = .65, TextTrimming = TextTrimming.CharacterEllipsis } } };
        var card = CreateCard("Photos.Collection." + kind + "." + Identity(key), title + " · " + count, content,
            () => OpenCollectionAsync(kind, key, title));
        ToolTip.SetTip(card, subtitle);
        var menu = new ContextMenu();
        AddMenuItem(menu, "Open", () => OpenCollectionAsync(kind, key, title));
        AddMenuItem(menu, "Slideshow", () => StartSlideshowAsync(_library.Query(new PhotoQuery { Source = kind, SourceKey = key })));
        if (kind == PhotoSourceKind.Album)
        {
            AddMenuItem(menu, "RenameAlbum", async () =>
            {
                if (_library.FindAlbum(key) is not { } album) return;
                var prompt = new PromptDialog(L("RenameAlbum"), L("AlbumNamePrompt"), album.Name);
                if (await prompt.ShowDialog<int>(Owner) == 1 && !_library.RenameAlbum(album, prompt.Value ?? "")) Status.Text = L("AlbumNameTaken");
            });
            AddMenuItem(menu, "DeleteAlbum", async () =>
            {
                if (_library.FindAlbum(key) is { } album && await ConfirmAsync(L("DeleteAlbum"), PhotosLocalization.Format("Photos_DeleteAlbumConfirm", key)))
                    _library.DeleteAlbum(album);
            });
        }
        else
        {
            AddMenuItem(menu, "ShowInExplorer", () => { FileOperations.ShowInExplorer(key); return Task.CompletedTask; });
            if (root) AddMenuItem(menu, "RemoveFromLibrary", async () =>
            {
                if (await ConfirmAsync(L("RemoveFromLibrary"), PhotosLocalization.Format("Photos_RemoveFolderConfirm", key))) _library.RemoveFolder(key);
            });
        }
        card.ContextMenu = menu;
        return card;
    }

    private PhotoCard CreateCard(string id, string name, Control content, Func<Task> action)
    {
        var card = new PhotoCard(id, name) { Content = content, Margin = new Thickness(8) };
        card.Click += async (_, _) => { try { await action(); } catch (Exception ex) { Status.Text = ex.Message; } };
        return card;
    }

    private void AddMenuItem(ContextMenu menu, string key, Func<Task> action)
    {
        var item = new MenuItem { Header = L(key) };
        AutomationProperties.SetName(item, L(key));
        AutomationProperties.SetAutomationId(item, "Photos.CollectionAction." + key);
        item.Click += async (_, _) => { try { await action(); } catch (Exception ex) { Status.Text = ex.Message; } };
        menu.Items.Add(item);
    }

    private async Task StartSlideshowAsync(IReadOnlyList<PhotoItem> items)
    {
        if (items.Count == 0) return;
        await OpenAsync(items[0], items);
        Viewer.StartSlideshow();
    }

    private static string Identity(string key) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(key)))[..16];
    private Geometry? NavigationGeometry(string page) => this.FindResource(page switch
    {
        "Favorites" => "PhotosIconHeartOutline",
        "Albums" => "PhotosIconAlbum",
        "Folders" => "PhotosIconFolder",
        _ => "PhotosIconGallery"
    }) as Geometry;
    private static string AlbumSubtitle(List<PhotoItem> items) => items.Count == 0 ? L("EmptyAlbum") :
        items.Min(i => i.SortDate).ToString("Y", PhotosLocalization.CurrentCulture) + " – " + items.Max(i => i.SortDate).ToString("Y", PhotosLocalization.CurrentCulture);
}
