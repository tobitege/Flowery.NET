using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace Flowery.NET.Photos.Services;

internal static class FolderIdentity
{
    public static string Resolve(string path)
    {
        path = Path.GetFullPath(path.Length == 0 ? "." : path);
        return Path.TrimEndingDirectorySeparator(OperatingSystem.IsWindows() ? ResolveWindows(path) : ResolveUnix(path));
    }

    public static string? TryResolve(string path)
    {
        try { return Resolve(path); }
        catch (Win32Exception) { return null; }
    }

    public static FolderLocation? ReadFolder(string folder)
    {
        folder = Path.GetFullPath(folder.Length == 0 ? "." : folder);
        if (TryResolve(folder) is not { } target) return null;
        var parents = new List<string>();
        if (!PhotoLibrary.PathComparer.Equals(Path.TrimEndingDirectorySeparator(folder), target))
        {
            for (var parent = Path.GetDirectoryName(folder); parent != null; parent = Path.GetDirectoryName(parent))
                if (TryResolve(parent) is { } location) parents.Add(location);
        }
        return new FolderLocation(target, [.. parents]);
    }

    private static string ResolveWindows(string path)
    {
        const uint openExisting = 3;
        const uint backupSemantics = 0x02000000; // Required to open a directory handle.
        using var handle = CreateFile(path, 0, FileShare.ReadWrite | FileShare.Delete, IntPtr.Zero,
            openExisting, backupSemantics, IntPtr.Zero);
        if (handle.IsInvalid) throw new Win32Exception(Marshal.GetLastPInvokeError());

        var buffer = new StringBuilder(512);
        var length = GetFinalPathNameByHandle(handle, buffer, (uint)buffer.Capacity, 0);
        if (length >= buffer.Capacity)
        {
            buffer.EnsureCapacity(checked((int)length));
            length = GetFinalPathNameByHandle(handle, buffer, (uint)buffer.Capacity, 0);
        }
        if (length == 0 || length >= buffer.Capacity) throw new Win32Exception(Marshal.GetLastPInvokeError());

        var resolved = buffer.ToString();
        return resolved.StartsWith(@"\\?\UNC\", StringComparison.OrdinalIgnoreCase) ? @"\\" + resolved[8..] : resolved[4..];
    }

    private static string ResolveUnix(string path)
    {
        var resolved = RealPath(path, IntPtr.Zero);
        if (resolved == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastPInvokeError());
        try { return Marshal.PtrToStringUTF8(resolved)!; }
        finally { Free(resolved); }
    }

    [DllImport("kernel32.dll", EntryPoint = "CreateFileW", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(string path, uint access, FileShare share, IntPtr security,
        uint disposition, uint flags, IntPtr template);

    [DllImport("kernel32.dll", EntryPoint = "GetFinalPathNameByHandleW", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    private static extern uint GetFinalPathNameByHandle(SafeFileHandle handle, StringBuilder path, uint size, uint flags);

    [DllImport("libc", EntryPoint = "realpath", SetLastError = true)]
    private static extern IntPtr RealPath([MarshalAs(UnmanagedType.LPUTF8Str)] string path, IntPtr buffer);

    [DllImport("libc", EntryPoint = "free")]
    private static extern void Free(IntPtr pointer);
}

internal sealed record FolderLocation(string Path, string[] ResolvedParents)
{
    public string? HiddenRoot(HashSet<string> marked) =>
        FindMarkedAncestor(Path, marked) ?? ResolvedParents.FirstOrDefault(marked.Contains);

    public static string? FindMarkedAncestor(string? path, HashSet<string> marked)
    {
        for (var directory = path; directory != null; directory = System.IO.Path.GetDirectoryName(directory))
            if (marked.Contains(directory)) return directory;
        return null;
    }
}
