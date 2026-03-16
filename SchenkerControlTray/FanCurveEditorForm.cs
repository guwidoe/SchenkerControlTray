using System.ComponentModel;
using System.Windows.Forms.DataVisualization.Charting;

namespace SchenkerControlTray;

internal sealed class FanCurveEditorForm : Form
{
    private readonly ControlCenterClient _client;
    private readonly FanTableService _fanTableService;

    private readonly ComboBox _profileCombo = new() { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly Button _renameProfileButton = new() { Text = "Rename…", AutoSize = true };
    private readonly Label _currentStatusLabel = new() { AutoSize = true };
    private readonly Label _tablePathLabel = new() { AutoSize = true };
    private readonly CheckBox _fanControlRespectiveCheckBox = new() { Text = "Fan control respective" };
    private readonly NumericUpDown _cpuMaxLevel = new() { Minimum = 0, Maximum = 15, Width = 60 };
    private readonly NumericUpDown _gpuMaxLevel = new() { Minimum = 0, Maximum = 15, Width = 60 };
    private readonly TabControl _curveTabs = new() { Dock = DockStyle.Fill };
    private readonly Chart _cpuChart = CreateChart("CPU curve", Color.OrangeRed);
    private readonly Chart _gpuChart = CreateChart("GPU curve", Color.MediumSeaGreen);
    private readonly DataGridView _cpuGrid = CreateGrid("CPU");
    private readonly DataGridView _gpuGrid = CreateGrid("GPU");
    private readonly Label _selectedCurveLabel = new() { AutoSize = true, Font = new Font(SystemFonts.DefaultFont, FontStyle.Bold) };
    private readonly NumericUpDown _selectedId = new() { Minimum = 0, Maximum = 15, ReadOnly = true, InterceptArrowKeys = false, Width = 60 };
    private readonly NumericUpDown _upTempInput = new() { Minimum = 0, Maximum = 255, Width = 70 };
    private readonly NumericUpDown _downTempInput = new() { Minimum = 0, Maximum = 255, Width = 70 };
    private readonly NumericUpDown _dutyInput = new() { Minimum = 0, Maximum = 100, Width = 70 };
    private readonly TrackBar _upTempSlider = new() { Minimum = 0, Maximum = 255, TickFrequency = 10, Width = 260 };
    private readonly TrackBar _downTempSlider = new() { Minimum = 0, Maximum = 255, TickFrequency = 10, Width = 260 };
    private readonly TrackBar _dutySlider = new() { Minimum = 0, Maximum = 100, TickFrequency = 5, Width = 260 };
    private readonly Button _reloadButton = new() { Text = "Reload" };
    private readonly Button _saveButton = new() { Text = "Save" };
    private readonly Button _saveAndActivateButton = new() { Text = "Save && Activate" };
    private readonly Button _closeButton = new() { Text = "Close" };

    private StatusSnapshot _snapshot;
    private FanTable? _loadedTable;
    private BindingList<FanPoint> _cpuPoints = new();
    private BindingList<FanPoint> _gpuPoints = new();
    private bool _suppressPointEditorEvents;

    public event EventHandler? ProfilesChanged;

    public FanCurveEditorForm(ControlCenterClient client, FanTableService fanTableService, StatusSnapshot snapshot)
    {
        _client = client;
        _fanTableService = fanTableService;
        _snapshot = snapshot;

        Text = "Schenker Fan Curve Editor";
        StartPosition = FormStartPosition.CenterScreen;
        Width = 1280;
        Height = 860;
        MinimumSize = new Size(1020, 700);

        BuildLayout();
        WireEvents();
        ReloadProfileDefinitions();

        if (snapshot.FanStatus is { } fanStatus)
        {
            SelectProfile(fanStatus.CurrentMode, fanStatus.CurrentProfileIndex);
        }

        UpdateSnapshot(snapshot);
    }

    public void UpdateSnapshot(StatusSnapshot snapshot)
    {
        _snapshot = snapshot;
        if (_snapshot.FanStatus is { } fanStatus)
        {
            var activeProfile = _fanTableService
                .GetProfileDefinitions()
                .FirstOrDefault(p => p.Mode == fanStatus.CurrentMode && p.ProfileIndex == fanStatus.CurrentProfileIndex);

            _currentStatusLabel.Text = activeProfile is null
                ? $"Current: {fanStatus.CurrentMode.DisplayName()} · Profile {fanStatus.CurrentProfileIndex + 1} · {fanStatus.FAN_TableName}"
                : $"Current: {fanStatus.CurrentMode.DisplayName()} · {activeProfile.MenuLabel}";
        }
        else
        {
            _currentStatusLabel.Text = "Current: unavailable";
        }
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Padding = new Padding(10),
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var top = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 3, AutoSize = true };
        top.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        top.Controls.Add(new Label { Text = "Target profile:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
        top.Controls.Add(_profileCombo, 1, 0);
        top.Controls.Add(_renameProfileButton, 2, 0);
        top.Controls.Add(new Label { Text = "Current status:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1);
        top.Controls.Add(_currentStatusLabel, 1, 1);
        top.SetColumnSpan(_currentStatusLabel, 2);
        top.Controls.Add(new Label { Text = "Fan table file:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 2);
        top.Controls.Add(_tablePathLabel, 1, 2);
        top.SetColumnSpan(_tablePathLabel, 2);

        var options = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true };
        options.Controls.Add(_fanControlRespectiveCheckBox);
        options.Controls.Add(new Label { Text = "CPU max level:", AutoSize = true, Margin = new Padding(16, 8, 3, 3) });
        options.Controls.Add(_cpuMaxLevel);
        options.Controls.Add(new Label { Text = "GPU max level:", AutoSize = true, Margin = new Padding(16, 8, 3, 3) });
        options.Controls.Add(_gpuMaxLevel);

        root.Controls.Add(top, 0, 0);
        root.Controls.Add(options, 0, 1);
        root.Controls.Add(new Label
        {
            Text = "Charts update live while you edit. Select a point in the grid, then use the sliders below for easier tuning. Save writes the JSON file. Save && Activate also reapplies that profile immediately.",
            AutoSize = true,
            Padding = new Padding(0, 4, 0, 8),
        }, 0, 2);
        root.Controls.Add(BuildEditorArea(), 0, 3);
        root.Controls.Add(BuildButtons(), 0, 4);

        Controls.Add(root);
    }

    private Control BuildEditorArea()
    {
        var content = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterDistance = 850,
        };

        var cpuPage = new TabPage("CPU") { Padding = new Padding(6) };
        var gpuPage = new TabPage("GPU") { Padding = new Padding(6) };
        cpuPage.Controls.Add(BuildChartAndGrid(_cpuChart, _cpuGrid));
        gpuPage.Controls.Add(BuildChartAndGrid(_gpuChart, _gpuGrid));
        _curveTabs.TabPages.Add(cpuPage);
        _curveTabs.TabPages.Add(gpuPage);
        content.Panel1.Controls.Add(_curveTabs);
        content.Panel2.Controls.Add(BuildPointEditorPanel());

        return content;
    }

    private static Control BuildChartAndGrid(Chart chart, DataGridView grid)
    {
        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            FixedPanel = FixedPanel.None,
            SplitterDistance = 320,
        };
        split.Panel1.Controls.Add(chart);
        split.Panel2.Controls.Add(grid);
        return split;
    }

    private Control BuildPointEditorPanel()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(8, 0, 0, 0),
        };
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        panel.Controls.Add(new Label
        {
            Text = "Selected point editor",
            AutoSize = true,
            Font = new Font(SystemFonts.DefaultFont, FontStyle.Bold),
        }, 0, 0);

        var group = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 3,
            AutoSize = true,
            Padding = new Padding(0, 8, 0, 0),
        };
        group.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        group.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        group.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        var row = 0;
        AddEditorRow(group, row++, "Curve", _selectedCurveLabel, null);
        AddEditorRow(group, row++, "Point ID", _selectedId, null);
        AddEditorRow(group, row++, "UpT", _upTempInput, _upTempSlider);
        AddEditorRow(group, row++, "DownT", _downTempInput, _downTempSlider);
        AddEditorRow(group, row++, "Duty", _dutyInput, _dutySlider);

        group.Controls.Add(new Label
        {
            Text = "Tip: you can still edit the grid directly. The chart mirrors the same data and updates instantly.",
            AutoSize = true,
            MaximumSize = new Size(320, 0),
            Padding = new Padding(0, 12, 0, 0),
        }, 0, row);
        group.SetColumnSpan(group.Controls[group.Controls.Count - 1], 3);

        panel.Controls.Add(group, 0, 1);
        return panel;
    }

    private static void AddEditorRow(TableLayoutPanel panel, int row, string label, Control input, Control? slider)
    {
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 8, 8, 8) }, 0, row);
        panel.Controls.Add(input, 1, row);
        if (slider is not null)
        {
            slider.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            panel.Controls.Add(slider, 2, row);
        }
    }

    private Control BuildButtons()
    {
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Right, AutoSize = true, FlowDirection = FlowDirection.RightToLeft };
        buttons.Controls.Add(_closeButton);
        buttons.Controls.Add(_saveAndActivateButton);
        buttons.Controls.Add(_saveButton);
        buttons.Controls.Add(_reloadButton);
        return buttons;
    }

    private void WireEvents()
    {
        _profileCombo.SelectedIndexChanged += (_, _) => LoadSelectedProfile();
        _renameProfileButton.Click += (_, _) => RenameSelectedProfile();
        _reloadButton.Click += (_, _) => LoadSelectedProfile();
        _saveButton.Click += async (_, _) => await SaveAsync(activate: false);
        _saveAndActivateButton.Click += async (_, _) => await SaveAsync(activate: true);
        _closeButton.Click += (_, _) => Close();

        _curveTabs.SelectedIndexChanged += (_, _) => LoadSelectedPointIntoEditor();
        _cpuGrid.SelectionChanged += (_, _) => LoadSelectedPointIntoEditor();
        _gpuGrid.SelectionChanged += (_, _) => LoadSelectedPointIntoEditor();
        _cpuGrid.CellValueChanged += (_, _) => RefreshChartsAndEditor();
        _gpuGrid.CellValueChanged += (_, _) => RefreshChartsAndEditor();

        _upTempInput.ValueChanged += (_, _) => UpdateSelectedPoint(p => p.UpT = Decimal.ToInt32(_upTempInput.Value), syncSlider: true);
        _downTempInput.ValueChanged += (_, _) => UpdateSelectedPoint(p => p.DownT = Decimal.ToInt32(_downTempInput.Value), syncSlider: true);
        _dutyInput.ValueChanged += (_, _) => UpdateSelectedPoint(p => p.Duty = Decimal.ToInt32(_dutyInput.Value), syncSlider: true);
        _upTempSlider.Scroll += (_, _) => _upTempInput.Value = _upTempSlider.Value;
        _downTempSlider.Scroll += (_, _) => _downTempInput.Value = _downTempSlider.Value;
        _dutySlider.Scroll += (_, _) => _dutyInput.Value = _dutySlider.Value;
    }

    private void ReloadProfileDefinitions()
    {
        var current = _profileCombo.SelectedItem as ProfileDefinition;
        var profiles = _fanTableService.GetProfileDefinitions();
        _profileCombo.DataSource = profiles.ToList();
        if (current is not null)
        {
            SelectProfile(current.Mode, current.ProfileIndex);
        }
    }

    private void SelectProfile(ProfileMode mode, int profileIndex)
    {
        for (var i = 0; i < _profileCombo.Items.Count; i++)
        {
            if (_profileCombo.Items[i] is ProfileDefinition profile && profile.Mode == mode && profile.ProfileIndex == profileIndex)
            {
                _profileCombo.SelectedIndex = i;
                return;
            }
        }
    }

    private ProfileDefinition? SelectedProfile => _profileCombo.SelectedItem as ProfileDefinition;
    private bool IsCpuTab => _curveTabs.SelectedIndex == 0;
    private DataGridView CurrentGrid => IsCpuTab ? _cpuGrid : _gpuGrid;
    private BindingList<FanPoint> CurrentPoints => IsCpuTab ? _cpuPoints : _gpuPoints;

    private void LoadSelectedProfile()
    {
        var profile = SelectedProfile;
        if (profile is null)
        {
            return;
        }

        _loadedTable = _fanTableService.LoadFanTable(profile.TableName).Clone();
        _tablePathLabel.Text = _fanTableService.GetFanTablePath(profile.TableName);
        _fanControlRespectiveCheckBox.Checked = _loadedTable.FanControlRespective;
        _cpuMaxLevel.Value = _loadedTable.CpuTemp_DefaultMaxLevel;
        _gpuMaxLevel.Value = _loadedTable.GpuTemp_DefaultMaxLevel;

        _cpuPoints = new BindingList<FanPoint>(_loadedTable.CPU.Select(p => p.Clone()).ToList());
        _gpuPoints = new BindingList<FanPoint>(_loadedTable.GPU.Select(p => p.Clone()).ToList());
        _cpuPoints.ListChanged += (_, _) => RefreshChartsAndEditor();
        _gpuPoints.ListChanged += (_, _) => RefreshChartsAndEditor();

        _cpuGrid.DataSource = _cpuPoints;
        _gpuGrid.DataSource = _gpuPoints;

        if (_cpuGrid.Rows.Count > 0)
        {
            _cpuGrid.Rows[0].Selected = true;
        }
        if (_gpuGrid.Rows.Count > 0)
        {
            _gpuGrid.Rows[0].Selected = true;
        }

        RefreshChartsAndEditor();
    }

    private void RenameSelectedProfile()
    {
        var profile = SelectedProfile;
        if (profile is null)
        {
            return;
        }

        using var dialog = new RenameProfileForm(profile);
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        _fanTableService.SaveProfileAlias(profile.Mode, profile.ProfileIndex, dialog.AliasName);
        ReloadProfileDefinitions();
        SelectProfile(profile.Mode, profile.ProfileIndex);
        UpdateSnapshot(_snapshot);
        ProfilesChanged?.Invoke(this, EventArgs.Empty);
    }

    private FanTable BuildTableFromEditor()
    {
        if (_loadedTable is null)
        {
            throw new InvalidOperationException("No fan table loaded.");
        }

        _cpuGrid.EndEdit();
        _gpuGrid.EndEdit();

        return new FanTable
        {
            Activated = _loadedTable.Activated,
            Name = _loadedTable.Name,
            FanControlRespective = _fanControlRespectiveCheckBox.Checked,
            CpuTemp_DefaultMaxLevel = Decimal.ToInt32(_cpuMaxLevel.Value),
            GpuTemp_DefaultMaxLevel = Decimal.ToInt32(_gpuMaxLevel.Value),
            CPU = _cpuPoints.Select(ClonePoint).ToList(),
            GPU = _gpuPoints.Select(ClonePoint).ToList(),
        };
    }

    private static FanPoint ClonePoint(FanPoint point) => new()
    {
        ID = point.ID,
        UpT = point.UpT,
        DownT = point.DownT,
        Duty = point.Duty,
    };

    private async Task SaveAsync(bool activate)
    {
        try
        {
            FanCurveRules.ValidatePoints(_cpuPoints, "CPU");
            FanCurveRules.ValidatePoints(_gpuPoints, "GPU");

            var profile = SelectedProfile ?? throw new InvalidOperationException("No profile selected.");
            var fanTable = BuildTableFromEditor();
            var backupPath = _fanTableService.SaveFanTable(fanTable);

            if (activate)
            {
                _snapshot = await _client.SetProfileAsync(profile.Mode, profile.ProfileIndex);
                UpdateSnapshot(_snapshot);
            }

            MessageBox.Show(
                $"Saved {fanTable.Name}.\nBackup: {backupPath}",
                Text,
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void RefreshChartsAndEditor()
    {
        UpdateChart(_cpuChart, _cpuPoints, Color.OrangeRed);
        UpdateChart(_gpuChart, _gpuPoints, Color.MediumSeaGreen);
        LoadSelectedPointIntoEditor();
    }

    private void UpdateChart(Chart chart, IEnumerable<FanPoint> points, Color accent)
    {
        chart.Series.Clear();

        var upSeries = new Series("Up curve")
        {
            ChartType = SeriesChartType.Line,
            BorderWidth = 2,
            Color = accent,
            MarkerStyle = MarkerStyle.Circle,
            MarkerSize = 7,
            XValueType = ChartValueType.Int32,
            YValueType = ChartValueType.Int32,
        };

        var downSeries = new Series("Down curve")
        {
            ChartType = SeriesChartType.Line,
            BorderDashStyle = ChartDashStyle.Dash,
            BorderWidth = 2,
            Color = ControlPaint.Dark(accent),
            MarkerStyle = MarkerStyle.Diamond,
            MarkerSize = 6,
            XValueType = ChartValueType.Int32,
            YValueType = ChartValueType.Int32,
        };

        foreach (var point in points.OrderBy(p => p.ID))
        {
            var upIndex = upSeries.Points.AddXY(point.UpT, point.Duty);
            upSeries.Points[upIndex].Label = point.ID.ToString();
            downSeries.Points.AddXY(point.DownT, point.Duty);
        }

        chart.Series.Add(upSeries);
        chart.Series.Add(downSeries);
    }

    private void LoadSelectedPointIntoEditor()
    {
        var point = GetSelectedPoint();
        _suppressPointEditorEvents = true;
        try
        {
            if (point is null)
            {
                _selectedCurveLabel.Text = "No point selected";
                return;
            }

            _selectedCurveLabel.Text = IsCpuTab ? "Editing CPU point" : "Editing GPU point";
            _selectedId.Value = Math.Clamp(point.ID, _selectedId.Minimum, _selectedId.Maximum);
            _upTempInput.Value = Math.Clamp(point.UpT, Decimal.ToInt32(_upTempInput.Minimum), Decimal.ToInt32(_upTempInput.Maximum));
            _downTempInput.Value = Math.Clamp(point.DownT, Decimal.ToInt32(_downTempInput.Minimum), Decimal.ToInt32(_downTempInput.Maximum));
            _dutyInput.Value = Math.Clamp(point.Duty, Decimal.ToInt32(_dutyInput.Minimum), Decimal.ToInt32(_dutyInput.Maximum));
            _upTempSlider.Value = Decimal.ToInt32(_upTempInput.Value);
            _downTempSlider.Value = Decimal.ToInt32(_downTempInput.Value);
            _dutySlider.Value = Decimal.ToInt32(_dutyInput.Value);
        }
        finally
        {
            _suppressPointEditorEvents = false;
        }
    }

    private FanPoint? GetSelectedPoint()
    {
        if (CurrentGrid.CurrentRow?.DataBoundItem is FanPoint point)
        {
            return point;
        }

        return CurrentPoints.FirstOrDefault();
    }

    private void UpdateSelectedPoint(Action<FanPoint> update, bool syncSlider)
    {
        if (_suppressPointEditorEvents)
        {
            return;
        }

        var point = GetSelectedPoint();
        if (point is null)
        {
            return;
        }

        update(point);
        if (syncSlider)
        {
            _upTempSlider.Value = Decimal.ToInt32(_upTempInput.Value);
            _downTempSlider.Value = Decimal.ToInt32(_downTempInput.Value);
            _dutySlider.Value = Decimal.ToInt32(_dutyInput.Value);
        }
        RefreshChartsAndEditor();
    }

    private static Chart CreateChart(string title, Color accent)
    {
        var chart = new Chart { Dock = DockStyle.Fill, BackColor = Color.WhiteSmoke };
        var area = new ChartArea("Area")
        {
            BackColor = Color.White,
        };
        area.AxisX.Minimum = 0;
        area.AxisX.Maximum = 255;
        area.AxisX.Interval = 10;
        area.AxisX.Title = "Temperature";
        area.AxisY.Minimum = 0;
        area.AxisY.Maximum = 100;
        area.AxisY.Interval = 10;
        area.AxisY.Title = "Fan duty (%)";
        area.AxisX.MajorGrid.LineColor = Color.Gainsboro;
        area.AxisY.MajorGrid.LineColor = Color.Gainsboro;
        chart.ChartAreas.Add(area);
        chart.Legends.Add(new Legend("Legend") { Docking = Docking.Top });
        chart.Titles.Add(new Title(title) { ForeColor = accent, Font = new Font(SystemFonts.DefaultFont, FontStyle.Bold) });
        return chart;
    }

    private static DataGridView CreateGrid(string prefix)
    {
        var grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            AutoGenerateColumns = false,
            AllowUserToAddRows = false,
            RowHeadersVisible = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
        };

        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "ID", DataPropertyName = nameof(FanPoint.ID), ReadOnly = true, FillWeight = 15 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = $"{prefix} UpT", DataPropertyName = nameof(FanPoint.UpT), FillWeight = 28 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = $"{prefix} DownT", DataPropertyName = nameof(FanPoint.DownT), FillWeight = 28 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Duty", DataPropertyName = nameof(FanPoint.Duty), FillWeight = 24 });
        return grid;
    }

}
