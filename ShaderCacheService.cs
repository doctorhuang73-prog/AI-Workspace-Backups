using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace CDriveCleaner;

public sealed record CacheItem(string Path, long Bytes);
public sealed record MigrationResult(int Success, int Failed);

public sealed class ShaderCacheService
{
    private readonly string _local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    private readonly string _state = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CDriveCleaner", "shader-migrations.json");
    public string DefaultRoot => @"D:\SystemCache\ShaderCache";

    public List<CacheItem> Discover()
    {
        var roots = new[] { Path.Combine(_local, "D3DSCache"), Path.Combine(_local, "NVIDIA", "DXCache"), Path.Combine(_local, "NVIDIA", "GLCache"), Path.Combine(_local, "AMD", "DxCache"), Path.Combine(_local, "Intel", "ShaderCache") };
        return roots.Where(Directory.Exists).Select(p => new CacheItem(p, Size(p))).Where(x => x.Bytes > 0).ToList();
    }

    public string? GetUsableTarget()
    {
        try
        {
            var drive = new DriveInfo("D:\\");
            if (drive.DriveType != DriveType.Fixed || !drive.DriveFormat.Equals("NTFS", StringComparison.OrdinalIgnoreCase)) return null;
            Directory.CreateDirectory(DefaultRoot);
            return drive.AvailableFreeSpace >= 100 * 1024L * 1024L ? DefaultRoot : null;
        }
        catch { return null; }
    }

    public MigrationResult Migrate(IEnumerable<CacheItem> items, string targetRoot)
    {
        Directory.CreateDirectory(targetRoot);
        var mappings = Load(); int ok = 0, fail = 0;
        foreach (var item in items)
        {
            try
            {
                var drive = new DriveInfo(Path.GetPathRoot(targetRoot)!);
                if (drive.DriveType != DriveType.Fixed || !drive.DriveFormat.Equals("NTFS", StringComparison.OrdinalIgnoreCase)) { fail++; continue; }
                if (drive.AvailableFreeSpace < item.Bytes) { fail++; continue; }
                var target = Path.Combine(targetRoot, Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(item.Path))).Substring(0, 12));
                if (Directory.Exists(target) || File.Exists(target)) { fail++; continue; }
                Directory.Move(item.Path, target);
                Directory.CreateDirectory(Path.GetDirectoryName(item.Path)!);
                if (!CreateJunction(item.Path, target)) { Directory.Move(target, item.Path); fail++; continue; }
                if (!Directory.Exists(item.Path)) { RemoveJunction(item.Path); Directory.Move(target, item.Path); fail++; continue; }
                mappings[item.Path] = target; ok++;
            }
            catch { fail++; }
        }
        Save(mappings); return new(ok, fail);
    }

    public MigrationResult Restore()
    {
        var map = Load(); int ok = 0, fail = 0;
        foreach (var pair in map.ToArray())
            try { RemoveJunction(pair.Key); Directory.Move(pair.Value, pair.Key); map.Remove(pair.Key); ok++; }
            catch { fail++; }
        Save(map); return new(ok, fail);
    }

    private static bool CreateJunction(string link, string target)
    {
        var p = Process.Start(new ProcessStartInfo("cmd.exe", $"/c mklink /J \"{link}\" \"{target}\"") { UseShellExecute = false, CreateNoWindow = true }); p?.WaitForExit(); return p?.ExitCode == 0;
    }
    private static void RemoveJunction(string path) => Directory.Delete(path);
    private static long Size(string path) => Directory.EnumerateFiles(path, "*", System.IO.SearchOption.AllDirectories).Sum(f => { try { return new FileInfo(f).Length; } catch { return 0; } });
    private Dictionary<string, string> Load() => File.Exists(_state) ? JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(_state)) ?? new() : new();
    private void Save(Dictionary<string, string> map) { Directory.CreateDirectory(Path.GetDirectoryName(_state)!); File.WriteAllText(_state, JsonSerializer.Serialize(map, new JsonSerializerOptions { WriteIndented = true })); }
}
