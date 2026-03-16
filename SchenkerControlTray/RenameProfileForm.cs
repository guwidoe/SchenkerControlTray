namespace SchenkerControlTray;

internal sealed class RenameProfileForm : Form
{
    private readonly TextBox _nameTextBox = new() { Dock = DockStyle.Top };

    public RenameProfileForm(ProfileDefinition profile)
    {
        Text = "Rename profile";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        Width = 540;
        Height = 230;

        var currentLabel = new Label
        {
            AutoSize = true,
            Text = $"Current label: {profile.FriendlyName}",
        };
        var suggestedLabel = new Label
        {
            AutoSize = true,
            Text = $"Suggested label: {profile.SuggestedName ?? $"Profile {profile.ProfileIndex + 1}"}",
        };
        var hintLabel = new Label
        {
            AutoSize = true,
            Text = "Enter a custom name. Leave it blank to go back to the built-in descriptive name.",
            MaximumSize = new Size(480, 0),
        };

        _nameTextBox.Text = profile.AliasName ?? string.Empty;
        _nameTextBox.SelectAll();

        var okButton = new Button { Text = "OK", DialogResult = DialogResult.OK, Width = 90 };
        var cancelButton = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Width = 90 };

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            AutoSize = true,
            FlowDirection = FlowDirection.RightToLeft,
        };
        buttons.Controls.Add(cancelButton);
        buttons.Controls.Add(okButton);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Padding = new Padding(12),
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.Controls.Add(currentLabel, 0, 0);
        layout.Controls.Add(suggestedLabel, 0, 1);
        layout.Controls.Add(hintLabel, 0, 2);
        layout.Controls.Add(_nameTextBox, 0, 3);
        layout.Controls.Add(buttons, 0, 4);

        AcceptButton = okButton;
        CancelButton = cancelButton;
        Controls.Add(layout);
    }

    public string? AliasName => string.IsNullOrWhiteSpace(_nameTextBox.Text) ? null : _nameTextBox.Text.Trim();
}
