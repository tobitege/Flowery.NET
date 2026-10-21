using System.Diagnostics;
using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;

namespace Flowery.NET.Photos.Services;

public static class FileOperations
{
    public static bool MoveToRecycleBin(string path)
    {
        if (!File.Exists(path)) return false;
        try
        {
            if (OperatingSystem.IsWindows())
            {
                Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(path,
                    Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                    Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
                return !File.Exists(path);
            }

            var info = new ProcessStartInfo(OperatingSystem.IsMacOS() ? "/usr/bin/osascript" : "gio")
            {
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true
            };
            if (OperatingSystem.IsMacOS())
            {
                info.ArgumentList.Add("-l");
                info.ArgumentList.Add("JavaScript");
                info.ArgumentList.Add("-e");
                info.ArgumentList.Add("""
                    ObjC.import('Foundation');
                    function run(args) {
                        var error = Ref();
                        if (!$.NSFileManager.defaultManager.trashItemAtURLResultingItemURLError(
                            $.NSURL.fileURLWithPath(args[0]), null, error))
                            throw new Error('Unable to move the temporary export to Trash.');
                    }
                    """);
            }
            else
            {
                info.ArgumentList.Add("trash");
                info.ArgumentList.Add("--");
            }
            info.ArgumentList.Add(Path.GetFullPath(path));
            using var process = Process.Start(info);
            if (process == null) return false;
            process.StandardError.ReadToEnd();
            process.WaitForExit();
            return process.ExitCode == 0 && !File.Exists(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or OperationCanceledException
            or System.ComponentModel.Win32Exception)
        { return false; }
    }

    public static string GetUniquePath(string path)
    {
        var directory = Path.GetDirectoryName(path) ?? string.Empty;
        var name = Path.GetFileNameWithoutExtension(path);
        var extension = Path.GetExtension(path);
        for (var i = 1; File.Exists(path); i++)
            path = Path.Combine(directory, $"{name} ({i}){extension}");
        return path;
    }

    public static void ShowInExplorer(string path)
    {
        var info = OperatingSystem.IsWindows() ? new ProcessStartInfo("explorer.exe")
            : new ProcessStartInfo(OperatingSystem.IsMacOS() ? "open" : "xdg-open");
        info.ArgumentList.Add(OperatingSystem.IsWindows() ? "/select," + path
            : OperatingSystem.IsMacOS() ? "-R" : Path.GetDirectoryName(path)!);
        if (OperatingSystem.IsMacOS()) info.ArgumentList.Add(path);
        info.UseShellExecute = false;
        using var process = Process.Start(info);
    }

    public static void OpenFolder(string path)
    {
        if (!Directory.Exists(path)) throw new DirectoryNotFoundException(path);
        var info = new ProcessStartInfo(OperatingSystem.IsWindows() ? "explorer.exe"
            : OperatingSystem.IsMacOS() ? "open" : "xdg-open") { UseShellExecute = false };
        info.ArgumentList.Add(Path.GetFullPath(path));
        using var process = Process.Start(info);
    }

    public static void OpenWith(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            var info = new ProcessStartInfo("rundll32.exe") { UseShellExecute = false };
            info.ArgumentList.Add("shell32.dll,OpenAs_RunDLL");
            info.ArgumentList.Add(path);
            using var process = Process.Start(info);
        }
        else
        {
            using var process = Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
    }

    public static bool SetAsWallpaper(string path) => OperatingSystem.IsWindows()
        && SystemParametersInfo(0x0014, 0, path, 0x03);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SystemParametersInfo(int action, int parameter, string path, int flags);

    public static async Task<int> CopyFilesAsync(TopLevel top, IReadOnlyList<string> paths)
    {
        if (top.Clipboard == null) throw new InvalidOperationException(Localization.PhotosLocalization.GetString("Photos_ActionFailed"));
        using var transfer = new DataTransfer();
        var files = new List<IStorageFile>();
        try
        {
            foreach (var path in paths)
            {
                var file = await top.StorageProvider.TryGetFileFromPathAsync(path);
                if (file == null) continue;
                files.Add(file);
                transfer.Add(DataTransferItem.CreateFile(file));
            }
            if (files.Count > 0) await top.Clipboard.SetDataAsync(transfer);
            return files.Count;
        }
        finally { foreach (var file in files) file.Dispose(); }
    }

    public static string FormatFileSize(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double size = bytes;
        var index = 0;
        while (size >= 1024 && index < units.Length - 1) { size /= 1024; index++; }
        return string.Format(Localization.PhotosLocalization.CurrentCulture, "{0:0.#} {1}", size, units[index]);
    }
}
