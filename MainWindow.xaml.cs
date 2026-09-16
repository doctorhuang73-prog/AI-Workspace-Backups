using System.Security.Principal;
using System.IO;
using System.Windows;
using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxImage = System.Windows.MessageBoxImage;
using MessageBoxResult = System.Windows.MessageBoxResult;

namespace CDriveCleaner;

public partial class MainWindow : Window
{
    private readonly CleanerService _cleaner = new();
    private readonly ShaderCacheService _shader = new();

    public MainWindow()
    {
        InitializeComponent();
        StatusText.Text = $"当前用户：{Environment.UserName}    管理员权限：{(IsAdmin() ? "是" : "否")}";
    }

    private void Clean_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show("是否开始扫描当前用户的 C 盘可清理项目？\n浏览器缓存和着色器缓存不会处理。", "开始扫描", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        var items = _cleaner.Scan();
        var summary = string.Join("\n", items.Select(x => $"{x.Name}：{x.Bytes / 1024d / 1024d:0.##} MB"));
        if (items.Count == 0) { MessageBox.Show("没有发现可处理的项目。", "扫描完成"); return; }
        if (MessageBox.Show($"发现 {items.Count} 类可处理项目：\n\n{summary}\n\n是否模拟执行？模拟执行不会修改任何文件。", "扫描完成", MessageBoxButton.YesNo, MessageBoxImage.Information) != MessageBoxResult.Yes) return;
        var preview = _cleaner.Preview(items);
        if (MessageBox.Show($"模拟结果：将有 {preview.Count} 个文件进入回收站。\n\n是否确认清理？", "确认清理", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        var result = _cleaner.Clean(items);
        MessageBox.Show($"清理完成：已处理 {result.Moved} 个文件，跳过 {result.Skipped} 个文件。", "清理完成", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void Shader_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show("请先关闭游戏、游戏平台和显卡管理程序。是否继续？", "准备迁移", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        var found = _shader.Discover();
        if (found.Count == 0) { MessageBox.Show("没有发现可迁移的着色器缓存目录。", "扫描完成"); return; }
        var text = string.Join("\n", found.Select(x => $"{x.Path}：{x.Bytes / 1024d / 1024d:0.##} MB"));
        var target = _shader.GetUsableTarget() ?? PickLocalFolder();
        if (target is null) { MessageBox.Show("没有选择可用的本地 NTFS 磁盘，迁移已取消。", "无法迁移", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
        if (MessageBox.Show($"发现 {found.Count} 个缓存目录：\n\n{text}\n\n目标：{target}\n\n是否模拟迁移？", "扫描完成", MessageBoxButton.YesNo, MessageBoxImage.Information) != MessageBoxResult.Yes) return;
        if (MessageBox.Show("模拟迁移不会修改文件。是否确认执行真实迁移？", "确认迁移", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        var result = _shader.Migrate(found, target);
        MessageBox.Show($"迁移完成：成功 {result.Success} 个，跳过或失败 {result.Failed} 个。", "迁移结果", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void Restore_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show("是否恢复已迁移的着色器缓存原位置？", "确认恢复", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        var result = _shader.Restore();
        MessageBox.Show($"恢复完成：成功 {result.Success} 个，失败 {result.Failed} 个。", "恢复结果", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private static bool IsAdmin() => new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);

    private static string? PickLocalFolder()
    {
        using var dialog = new System.Windows.Forms.FolderBrowserDialog { Description = "请选择本地 NTFS 磁盘上的缓存目录" };
        if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return null;
        var root = Path.GetPathRoot(dialog.SelectedPath);
        if (root is null) return null;
        try
        {
            var drive = new DriveInfo(root);
            return drive.DriveType == DriveType.Fixed && drive.DriveFormat.Equals("NTFS", StringComparison.OrdinalIgnoreCase) ? dialog.SelectedPath : null;
        }
        catch { return null; }
    }
}
