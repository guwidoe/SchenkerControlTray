using System.Drawing;

namespace SchenkerControlTray;

internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly ControlCenterClient _client = new();
    private readonly FanTableService _fanTableService = new();
    private readonly StartupManager _startupManager = new();
    private readonly NotifyIcon _notifyIcon;
    private readonly ToolStripMenuItem _statusItem;
    private readonly ToolStripMenuItem _refreshItem;
    private readonly ToolStripMenuItem _startWithWindowsItem;
    private readonly ToolStripMenuItem _fanEditorItem;
    private readonly ToolStripMenuItem _openControlCenterItem;
    private readonly Dictionary<ProfileMode, ToolStripMenuItem> _modeMenus = new();
    private readonly System.Windows.Forms.Timer _refreshTimer;

    private StatusSnapshot? _lastSnapshot;
    private FanCurveEditorForm? _fanCurveEditor;
    private bool _refreshInProgress;

    public TrayApplicationContext()
    {
        var contextMenu = new ContextMenuStrip();
        _statusItem = new ToolStripMenuItem("Loading…") { Enabled = false };
        contextMenu.Items.Add(_statusItem);
        contextMenu.Items.Add(new ToolStripSeparator());

        foreach (var mode in Enum.GetValues<ProfileMode>())
        {
            var menu = new ToolStripMenuItem(mode.DisplayName());
            _modeMenus[mode] = menu;
            contextMenu.Items.Add(menu);
        }

        contextMenu.Items.Add(new ToolStripSeparator());

        _refreshItem = new ToolStripMenuItem("Refresh status", null, async (_, _) => await RefreshStatusAsync(showError: true));
        _startWithWindowsItem = new ToolStripMenuItem("Start with Windows")
        {
            Checked = _startupManager.IsEnabled(),
            CheckOnClick = false,
        };
        _startWithWindowsItem.Click += (_, _) => ToggleStartup();
        _fanEditorItem = new ToolStripMenuItem("Fan curve editor…", null, async (_, _) => await OpenFanCurveEditorAsync());
        _openControlCenterItem = new ToolStripMenuItem("Open OEM Control Center", null, (_, _) => _client.LaunchControlCenter());
        var exitItem = new ToolStripMenuItem("Exit", null, (_, _) => ExitThread());

        contextMenu.Items.Add(_refreshItem);
        contextMenu.Items.Add(_startWithWindowsItem);
        contextMenu.Items.Add(_fanEditorItem);
        contextMenu.Items.Add(_openControlCenterItem);
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add(exitItem);

        _notifyIcon = new NotifyIcon
        {
            ContextMenuStrip = contextMenu,
            Icon = SystemIcons.Application,
            Text = "Schenker Control Tray",
            Visible = true,
        };
        _notifyIcon.DoubleClick += async (_, _) => await OpenFanCurveEditorAsync();

        _refreshTimer = new System.Windows.Forms.Timer { Interval = 15000 };
        _refreshTimer.Tick += async (_, _) => await RefreshStatusAsync();
        _refreshTimer.Start();

        _ = RefreshStatusAsync(showError: true);
    }

    protected override void ExitThreadCore()
    {
        _refreshTimer.Stop();
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _refreshTimer.Dispose();
        _fanCurveEditor?.Close();
        base.ExitThreadCore();
    }

    private async Task RefreshStatusAsync(bool showError = false)
    {
        if (_refreshInProgress)
        {
            return;
        }

        try
        {
            _refreshInProgress = true;
            SetBusyState(true);
            _lastSnapshot = await _client.GetStatusAsync();
            UpdateMenuFromSnapshot(_lastSnapshot);
            _fanCurveEditor?.UpdateSnapshot(_lastSnapshot);
        }
        catch (Exception ex)
        {
            _statusItem.Text = "Status unavailable";
            if (showError)
            {
                ShowError(ex.Message);
            }
        }
        finally
        {
            _refreshInProgress = false;
            SetBusyState(false);
        }
    }

    private async Task SetProfileAsync(ProfileMode mode, int profileIndex)
    {
        try
        {
            SetBusyState(true);
            _lastSnapshot = await _client.SetProfileAsync(mode, profileIndex);
            UpdateMenuFromSnapshot(_lastSnapshot);
            _fanCurveEditor?.UpdateSnapshot(_lastSnapshot);

            var profile = _fanTableService.GetProfileDefinitions()
                .FirstOrDefault(p => p.Mode == mode && p.ProfileIndex == profileIndex);
            var profileText = profile is null ? $"Profile {profileIndex + 1}" : profile.FriendlyName;
            _notifyIcon.ShowBalloonTip(1500, "Profile changed", $"{mode.DisplayName()} · {profileText}", ToolTipIcon.Info);
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
        finally
        {
            SetBusyState(false);
        }
    }

    private async Task OpenFanCurveEditorAsync()
    {
        try
        {
            if (_lastSnapshot?.FanStatus is null)
            {
                await RefreshStatusAsync(showError: true);
            }

            if (_lastSnapshot?.FanStatus is null)
            {
                return;
            }

            if (_fanCurveEditor is null || _fanCurveEditor.IsDisposed)
            {
                _fanCurveEditor = new FanCurveEditorForm(_client, _fanTableService, _lastSnapshot);
                _fanCurveEditor.FormClosed += (_, _) => _fanCurveEditor = null;
                _fanCurveEditor.ProfilesChanged += (_, _) =>
                {
                    if (_lastSnapshot is not null)
                    {
                        UpdateMenuFromSnapshot(_lastSnapshot);
                        _fanCurveEditor?.UpdateSnapshot(_lastSnapshot);
                    }
                };
                _fanCurveEditor.Show();
            }
            else
            {
                _fanCurveEditor.UpdateSnapshot(_lastSnapshot);
                _fanCurveEditor.BringToFront();
                _fanCurveEditor.Focus();
            }
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
    }

    private void ToggleStartup()
    {
        try
        {
            var enable = !_startupManager.IsEnabled();
            _startupManager.SetEnabled(enable);
            _startWithWindowsItem.Checked = enable;
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
    }

    private void UpdateMenuFromSnapshot(StatusSnapshot snapshot)
    {
        var fanStatus = snapshot.FanStatus;
        if (fanStatus is null)
        {
            _statusItem.Text = "Status unavailable";
            return;
        }

        _startWithWindowsItem.Checked = _startupManager.IsEnabled();

        var profileDefinitions = _fanTableService.GetProfileDefinitions();
        var activeProfile = profileDefinitions.FirstOrDefault(p => p.Mode == fanStatus.CurrentMode && p.ProfileIndex == fanStatus.CurrentProfileIndex);
        _statusItem.Text = activeProfile is null
            ? $"{fanStatus.CurrentMode.DisplayName()} · Profile {fanStatus.CurrentProfileIndex + 1} · {fanStatus.FAN_TableName}"
            : $"{fanStatus.CurrentMode.DisplayName()} · {activeProfile.MenuLabel}";

        foreach (var (mode, menu) in _modeMenus)
        {
            menu.DropDownItems.Clear();
            var modeProfiles = profileDefinitions.Where(p => p.Mode == mode).ToList();
            var supported = modeProfiles.Count > 0 && (mode != ProfileMode.Turbo || fanStatus.TurboSupported);
            menu.Enabled = supported;
            if (!supported)
            {
                continue;
            }

            foreach (var profile in modeProfiles)
            {
                var item = new ToolStripMenuItem(profile.MenuLabel)
                {
                    Checked = fanStatus.CurrentMode == profile.Mode && fanStatus.CurrentProfileIndex == profile.ProfileIndex,
                };
                item.Click += async (_, _) => await SetProfileAsync(profile.Mode, profile.ProfileIndex);
                menu.DropDownItems.Add(item);
            }
        }
    }

    private void SetBusyState(bool busy)
    {
        _refreshItem.Enabled = !busy;
        _startWithWindowsItem.Enabled = !busy;
        _fanEditorItem.Enabled = !busy;
        _openControlCenterItem.Enabled = !busy;
        foreach (var (mode, menu) in _modeMenus)
        {
            menu.Enabled = !busy && IsModeSupported(mode);
        }
        _notifyIcon.Text = busy ? "Schenker Control Tray (working…)" : "Schenker Control Tray";
    }

    private bool IsModeSupported(ProfileMode mode)
    {
        if (!_modeMenus.TryGetValue(mode, out var menu) || menu.DropDownItems.Count == 0)
        {
            return false;
        }

        if (mode == ProfileMode.Turbo)
        {
            return _lastSnapshot?.FanStatus?.TurboSupported == true;
        }

        return true;
    }

    private void ShowError(string message)
    {
        MessageBox.Show(message, "Schenker Control Tray", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }
}
