using Forms = System.Windows.Forms;

namespace TripleSpaceTranslator.App.Infrastructure;

public sealed class NotifyIconHost : IDisposable
{
    private readonly Forms.NotifyIcon _notifyIcon;

    public NotifyIconHost()
    {
        var contextMenu = new Forms.ContextMenuStrip();
        contextMenu.Items.Add("设置", image: null, (_, _) => SettingsRequested?.Invoke(this, EventArgs.Empty));
        contextMenu.Items.Add("退出", image: null, (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty));

        _notifyIcon = new Forms.NotifyIcon
        {
            ContextMenuStrip = contextMenu,
            Icon = System.Drawing.SystemIcons.Application,
            Text = "TripleSpaceTranslator",
            Visible = true
        };

        _notifyIcon.DoubleClick += (_, _) => SettingsRequested?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler? SettingsRequested;

    public event EventHandler? ExitRequested;

    public void ShowMessage(string title, string message, Forms.ToolTipIcon icon)
    {
        _notifyIcon.BalloonTipIcon = icon;
        _notifyIcon.BalloonTipTitle = title;
        _notifyIcon.BalloonTipText = message;
        _notifyIcon.ShowBalloonTip(2500);
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }
}
