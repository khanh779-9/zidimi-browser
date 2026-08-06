using System;
using System.Drawing;
using System.IO;
using System.Windows;
using Heco.Browser.Infrastructure;

namespace Heco.Browser.Infrastructure;

/// <summary>
/// Quản lý system tray icon cho "Run in background".
/// Khi window bị ẩn, icon xuất hiện để user có thể mở lại hoặc thoát hẳn app.
/// </summary>
public sealed class TrayIconManager : IDisposable
{
    private readonly System.Windows.Forms.NotifyIcon _notifyIcon;
    private readonly System.Windows.Forms.ContextMenuStrip _contextMenu;
    private bool _disposed;

    public TrayIconManager()
    {
        var menuItemOpen = new System.Windows.Forms.ToolStripMenuItem(LanguageManager.Instance["Tray_Open"]);
        menuItemOpen.Click += (_, _) => RestoreMainWindow();

        var menuItemExit = new System.Windows.Forms.ToolStripMenuItem(LanguageManager.Instance["Tray_Exit"]);
        menuItemExit.Click += (_, _) => ExitApplication();

        _contextMenu = new System.Windows.Forms.ContextMenuStrip();
        _contextMenu.Items.Add(menuItemOpen);
        _contextMenu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        _contextMenu.Items.Add(menuItemExit);

        _notifyIcon = new System.Windows.Forms.NotifyIcon
        {
            Text = "Heco Browser",
            Icon = LoadIcon(),
            ContextMenuStrip = _contextMenu,
            Visible = false,
        };
        _notifyIcon.DoubleClick += (_, _) => RestoreMainWindow();
    }

    private static Icon LoadIcon()
    {
        try
        {
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Heco_Browser.ico");
            if (File.Exists(path))
                return new Icon(path);
        }
        catch { /* fallback */ }
        return SystemIcons.Application;
    }

    /// <summary>Hiện icon trên tray (khi window bị ẩn).</summary>
    public void Show()
    {
        if (_disposed) return;
        _notifyIcon.Visible = true;
    }

    /// <summary>Ẩn icon (khi window hiện lại hoặc trước khi thoát).</summary>
    public void Hide()
    {
        if (_disposed) return;
        _notifyIcon.Visible = false;
    }

    /// <summary>Mở lại window chính.</summary>
    public static void RestoreMainWindow()
    {
        var main = Application.Current?.MainWindow;
        if (main == null) return;
        main.Show();
        if (main.WindowState == WindowState.Minimized)
            main.WindowState = WindowState.Normal;
        main.Activate();
        main.Topmost = true;
        main.Topmost = false;
        main.Focus();
    }

    /// <summary>Thoát hẳn ứng dụng (dù window đang ẩn).</summary>
    public static void ExitApplication()
    {
        Application.Current?.Shutdown();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _contextMenu.Dispose();
    }
}
