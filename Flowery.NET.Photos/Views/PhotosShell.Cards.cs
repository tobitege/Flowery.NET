using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Ellipse = Avalonia.Controls.Shapes.Ellipse;
using Avalonia.Layout;
using Flowery.NET.Photos.Localization;
using Flowery.NET.Photos.Models;
using Flowery.NET.Photos.Services;

namespace Flowery.NET.Photos.Views;

public partial class PhotosShell
{
    private double _homeWidth;
    private sealed record CollectionEntry(PhotoSourceKind Kind, string Key, string Title, string Subtitle, int Count, PhotoItem? Cover, bool Root = false);
    private sealed record CollectionRow(CollectionEntry[] Entries, double Width);
    private static double SizeFactor => FlowerySizeManager.CurrentSize switch
    {
        DaisySize.ExtraSmall => .85, DaisySize.Small => 1, DaisySize.Medium => 1.12,
        DaisySize.Large => 1.25, DaisySize.ExtraLarge => 1.4, _ => 1
    };
    private void OnCardSizeChanged(object? sender, DaisySize size) { BuildHome(); BuildCollections(); }
    private Grid CreateIconBadge(Geometry? geometry, string brushKey)
    {
        var brush = this.FindResource(brushKey) as IBrush;
        var badge = new Grid { Width = 44 * SizeFactor, Height = 44 * SizeFactor, HorizontalAlignment = HorizontalAlignment.Left };
        badge.Children.Add(new Ellipse { Fill = brush, Opacity = .16 });
        badge.Children.Add(new PathIcon { Data = geometry, Foreground = brush, Width = 22 * SizeFactor, Height = 22 * SizeFactor });
        return badge;
    }

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
                            CreateIconBadge(this.FindResource(section switch { "Edit" => "PhotosIconTune", "Show" => "PhotosIconSlideshow", _ => "PhotosIconGallery" }) as Geometry,
                                section switch { "Edit" => "DaisySecondaryBrush", "Show" => "DaisyAccentBrush", _ => "DaisyPrimaryBrush" }),
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
            var badge = CreateIconBadge(NavigationGeometry(page), page switch
            { "Favorites" => "DaisyErrorBrush", "Albums" => "DaisyAccentBrush", "Folders" => "DaisyInfoBrush", _ => "DaisyPrimaryBrush" });
            badge.Margin = new Thickness(0, 0, 14, 0); row.Children.Add(badge);
            Grid.SetColumn(content, 1); row.Children.Add(content);
            statistics.Children.Add(CreateCard("Photos.Stats." + page, L(key), row, () => NavigateAsync(page)));
        }
        Home.Children.Add(statistics);

        var recent = _library.Items.OrderByDescending(i => i.SortDate).ToList();
        var columns = Math.Max(1, (int)((width + 12) / (150 * SizeFactor + 12)));
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
            Text = empty ? L("Home_WelcomeText") : PhotosLocalization.FormatLibraryStats(
                _library.Items.Count, _library.Folders.Count, FileOperations.FormatFileSize(_library.Items.Sum(i => i.FileSize))),
            Opacity = .75, TextWrapping = TextWrapping.Wrap
        });
        Panel actions = width < 560 ? new StackPanel() : new WrapPanel();
        actions.Margin = new Thickness(0, 14, 0, 0);
        if (!empty) actions.Children.Add(ActionButton("Home_StartSlideshow", () => StartSlideshowAsync(_library.Items.OrderByDescending(item => item.SortDate).ToList())));
        actions.Children.Add(ActionButton("AddFolder", AddFolderAsync));
        actions.Children.Add(ActionButton("ImportFiles", ImportAsync));
        if (empty && Directory.Exists(PhotoLibrary.SamplesFolder))
            actions.Children.Add(ActionButton("UseSamples", () => { AddFolder(PhotoLibrary.SamplesFolder); return Task.CompletedTask; }));
        foreach (var button in actions.Children.OfType<DaisyButton>())
            button.IconSymbol = button.Content?.ToString() == L("Home_StartSlideshow") ? Flowery.Enums.DaisyIconSymbol.Play :
                button.Content?.ToString() == L("AddFolder") ? Flowery.Enums.DaisyIconSymbol.Folder : Flowery.Enums.DaisyIconSymbol.Add;
        content.Children.Add(actions);

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = new Thickness(32,28) };
        grid.Children.Add(content);
        if (width >= 760)
        {
            var art = CreateHeroArt(); Grid.SetColumn(art, 1); grid.Children.Add(art);
        }
        var decoration = new Canvas { IsHitTestVisible = false, ClipToBounds = true };
        var primary = new Ellipse { Fill = this.FindResource("DaisyPrimaryBrush") as IBrush, Opacity = .14 };
        var secondary = new Ellipse { Fill = this.FindResource("DaisySecondaryBrush") as IBrush, Opacity = .12 };
        decoration.Children.Add(primary); decoration.Children.Add(secondary);
        decoration.SizeChanged += (_, e) =>
        {
            var p = Math.Max(80, Math.Min(190, e.NewSize.Height - 48));
            primary.Width = primary.Height = p;
            Canvas.SetLeft(primary, e.NewSize.Width - p * .55); Canvas.SetTop(primary, (e.NewSize.Height - p) / 2);
            var s = Math.Min(150, Math.Max(60, e.NewSize.Width * .18));
            secondary.Width = secondary.Height = s;
            Canvas.SetLeft(secondary, e.NewSize.Width * .42); Canvas.SetTop(secondary, e.NewSize.Height - s * .45);
        };
        return new DaisyCard { Classes = { "photo-card" }, BodyPadding = new Thickness(0), Content = new Grid { Children = { decoration, grid } } };
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
        var entries = new List<CollectionEntry>();
        var search = Search.Text?.Trim();
        if (_page == "Albums")
        {
            foreach (var album in _library.Albums.Where(a => string.IsNullOrEmpty(search) || a.Name.Contains(search, StringComparison.CurrentCultureIgnoreCase)))
            {
                var items = _library.GetAlbumItems(album).ToList();
                entries.Add(new CollectionEntry(PhotoSourceKind.Album, album.Name, album.Name,
                    AlbumSubtitle(items), items.Count, items.FirstOrDefault()));
            }
        }
        else foreach (var folder in _library.GetFolderSummaries().Where(f => string.IsNullOrEmpty(search) ||
            f.Path.Contains(search, StringComparison.CurrentCultureIgnoreCase)))
            entries.Add(new CollectionEntry(PhotoSourceKind.Folder, folder.Path, folder.Name,
                folder.Path, folder.Count, folder.Cover, folder.IsRoot));
        CollectionCount.Text = ItemCount(entries.Count);
        CollectionEmpty.IsVisible = entries.Count == 0;
        CollectionEmptyTitle.Text = L("Empty_" + _page + "Title");
        CollectionEmptyText.Text = L("Empty_" + _page + "Text");
        var width = Math.Max(200, CollectionsScroll.Bounds.Width - 24);
        var columns = Math.Max(1, (int)(width / 236));
        var cardWidth = Math.Floor(width / columns) - 16;
        Collections.ItemsSource = entries.Chunk(columns).Select(chunk => new CollectionRow(chunk, cardWidth)).ToArray();
        AutomationProperties.SetName(Collections, CollectionTitle.Text ?? string.Empty);
    }
    private Control? BuildCollectionRow(CollectionRow? row)
    {
        if (row == null) return null;
        var panel = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (var entry in row.Entries)
        {
            var card = CreateCollectionCard(entry.Kind, entry.Key, entry.Title, entry.Subtitle, entry.Count, entry.Cover, entry.Root);
            card.Width = row.Width; panel.Children.Add(card);
        }
        return panel;
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
                var prompt = new PromptDialog(L("RenameAlbum"), L("AlbumNamePrompt"), album.Name, accept: L("Rename"));
                if (await prompt.ShowDialog<int>(Owner) == 1 && !_library.RenameAlbum(album, prompt.Value ?? "")) ShowToast(L("AlbumNameTaken"), true);
            });
            AddMenuItem(menu, "DeleteAlbum", async () =>
            {
                if (_library.FindAlbum(key) is { } album && await ConfirmAsync(L("DeleteAlbum"), PhotosLocalization.Format("Photos_DeleteAlbumConfirm", key)))
                    _library.DeleteAlbum(album);
            });
        }
        else
        {
            AddMenuItem(menu, "OpenInExplorer", () => { FileOperations.OpenFolder(key); return Task.CompletedTask; });
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
        card.Click += async (_, _) => { try { await action(); } catch (Exception ex) { ShowToast(ex.Message, true); } };
        return card;
    }

    private void AddMenuItem(ContextMenu menu, string key, Func<Task> action)
    {
        var item = new MenuItem { Header = L(key) };
        AutomationProperties.SetName(item, L(key));
        AutomationProperties.SetAutomationId(item, "Photos.CollectionAction." + key);
        item.Click += async (_, _) => { try { await action(); } catch (Exception ex) { ShowToast(ex.Message, true); } };
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
        "Favorites" => "PhotosIconHeart",
        "Albums" => "PhotosIconAlbum",
        "Folders" => "PhotosIconFolder",
        _ => "PhotosIconGallery"
    }) as Geometry;
    private static string AlbumSubtitle(List<PhotoItem> items)
    {
        if (items.Count == 0) return L("EmptyAlbum");
        var from = items.Min(i => i.SortDate).ToString("Y", PhotosLocalization.CurrentCulture);
        var to = items.Max(i => i.SortDate).ToString("Y", PhotosLocalization.CurrentCulture);
        return from == to ? from : from + " – " + to;
    }
}
