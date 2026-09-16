using Microsoft.VisualBasic.FileIO;
using System.IO;

namespace CDriveCleaner;

public sealed record CleanItem(string Name, string Path, long Bytes);
public sealed record CleanResult(int Moved, int Skipped);

public sealed class CleanerService
{
    private readonly string _user = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    public List<CleanItem> Scan()
    {
        var paths = new (string, string)[] {
            ("用户临时文件", Path.GetTempPath()),
            ("用户崩溃报告", Path.Combine(_user, "CrashDumps")),
            ("缩略图缓存", Path.Combine(_user, "Microsoft", "Windows", "Explorer"))
        };
        return paths.Select(x => new CleanItem(x.Item1, x.Item2, SafeSize(x.Item2))).Where(x => x.Bytes > 0).ToList();
    }

    public List<string> Preview(IEnumerable<CleanItem> items) => items.SelectMany(x => SafeFiles(x.Path)).ToList();

    public CleanResult Clean(IEnumerable<CleanItem> items)
    {
        int moved = 0, skipped = 0;
        foreach (var item in items)
            foreach (var file in SafeFiles(item.Path))
                try { FileSystem.DeleteFile(file, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin); moved++; }
                catch { skipped++; }
        return new(moved, skipped);
    }

    private static IEnumerable<string> SafeFiles(string root)
    {
        if (!Directory.Exists(root)) yield break;
        IEnumerable<string> files;
        try { files = Directory.EnumerateFiles(root, "*", System.IO.SearchOption.AllDirectories); }
        catch { yield break; }
        foreach (var file in files)
        {
            var name = Path.GetFileName(file).ToLowerInvariant();
            if (root.EndsWith("Explorer", StringComparison.OrdinalIgnoreCase) && !(name.StartsWith("thumbcache_") || name.StartsWith("iconcache_"))) continue;
            yield return file;
        }
    }

    private static long SafeSize(string root) => SafeFiles(root).Sum(f => { try { return new FileInfo(f).Length; } catch { return 0; } });
}
