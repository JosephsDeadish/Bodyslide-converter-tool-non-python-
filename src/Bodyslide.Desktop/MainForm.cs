using Bodyslide.Core;
using System.Text;
using System.Windows.Forms;

namespace Bodyslide.Desktop;

public sealed class MainForm : Form
{
    private readonly TextBox _inputTextBox;
    private readonly TextBox _outputTextBox;
    private readonly ComboBox _presetComboBox;
    private readonly TextBox _logTextBox;
    private readonly Button _convertButton;
    private readonly BatchConversionRunner _batchRunner;

    public MainForm()
    {
        Text = "SlideSmith v0.1";
        Width = 900;
        Height = 650;
        StartPosition = FormStartPosition.CenterScreen;

        _batchRunner = new BatchConversionRunner(StandaloneConversionModules.CreateDefault());

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
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        Controls.Add(layout);

        var dropPanel = new Panel
        {
            Height = 90,
            Dock = DockStyle.Top,
            BorderStyle = BorderStyle.FixedSingle,
            AllowDrop = true,
        };
        dropPanel.DragEnter += OnDragEnter;
        dropPanel.DragDrop += OnDragDrop;
        var dropLabel = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            Text = "Drag and drop a .nif file, .zip archive, or armor folder here",
        };
        dropPanel.Controls.Add(dropLabel);
        layout.Controls.Add(dropPanel, 0, 0);

        var inputRow = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 3,
            AutoSize = true,
        };
        inputRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        inputRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        inputRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        inputRow.Controls.Add(new Label { Text = "Input", Anchor = AnchorStyles.Left, AutoSize = true }, 0, 0);
        _inputTextBox = new TextBox { Dock = DockStyle.Fill, AllowDrop = true };
        _inputTextBox.DragEnter += OnDragEnter;
        _inputTextBox.DragDrop += OnDragDrop;
        inputRow.Controls.Add(_inputTextBox, 1, 0);
        var browseInputButton = new Button { Text = "Browse...", AutoSize = true };
        browseInputButton.Click += (_, _) => BrowseInput();
        inputRow.Controls.Add(browseInputButton, 2, 0);
        layout.Controls.Add(inputRow, 0, 1);

        var presetRow = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 2,
            AutoSize = true,
        };
        presetRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        presetRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        presetRow.Controls.Add(new Label { Text = "Preset", Anchor = AnchorStyles.Left, AutoSize = true }, 0, 0);
        _presetComboBox = new ComboBox
        {
            Dock = DockStyle.Fill,
            DropDownStyle = ComboBoxStyle.DropDownList,
        };
        foreach (var preset in PresetCatalog.All.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase))
        {
            _presetComboBox.Items.Add(preset.Name);
        }
        if (_presetComboBox.Items.Count > 0)
        {
            _presetComboBox.SelectedIndex = 0;
        }
        presetRow.Controls.Add(_presetComboBox, 1, 0);
        layout.Controls.Add(presetRow, 0, 2);

        var outputRow = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 3,
            AutoSize = true,
        };
        outputRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        outputRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        outputRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        outputRow.Controls.Add(new Label { Text = "Output (optional)", Anchor = AnchorStyles.Left, AutoSize = true }, 0, 0);
        _outputTextBox = new TextBox { Dock = DockStyle.Fill };
        outputRow.Controls.Add(_outputTextBox, 1, 0);
        var browseOutputButton = new Button { Text = "Browse...", AutoSize = true };
        browseOutputButton.Click += (_, _) => BrowseOutput();
        outputRow.Controls.Add(browseOutputButton, 2, 0);
        layout.Controls.Add(outputRow, 0, 3);

        var bottomPanel = new Panel { Dock = DockStyle.Fill };
        _convertButton = new Button
        {
            Text = "Convert",
            Width = 120,
            Height = 35,
            Top = 4,
            Left = 0,
            Anchor = AnchorStyles.Top | AnchorStyles.Left,
        };
        _convertButton.Click += async (_, _) => await ConvertAsync();
        bottomPanel.Controls.Add(_convertButton);

        _logTextBox = new TextBox
        {
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            Dock = DockStyle.Bottom,
            Height = 360,
            ReadOnly = true,
            Font = new Font("Consolas", 9f),
        };
        bottomPanel.Controls.Add(_logTextBox);
        layout.Controls.Add(bottomPanel, 0, 4);

        AppendLog("Ready. Choose input, pick a preset, then click Convert.");
    }

    private void BrowseInput()
    {
        using var fileDialog = new OpenFileDialog
        {
            Filter = "NIF/ZIP Files (*.nif;*.zip)|*.nif;*.zip|All Files (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false,
        };

        if (fileDialog.ShowDialog(this) == DialogResult.OK)
        {
            _inputTextBox.Text = fileDialog.FileName;
            return;
        }

        using var folderDialog = new FolderBrowserDialog
        {
            Description = "Select armor input folder",
            UseDescriptionForTitle = true,
        };

        if (folderDialog.ShowDialog(this) == DialogResult.OK)
        {
            _inputTextBox.Text = folderDialog.SelectedPath;
        }
    }

    private void BrowseOutput()
    {
        using var folderDialog = new FolderBrowserDialog
        {
            Description = "Select output folder",
            UseDescriptionForTitle = true,
        };

        if (folderDialog.ShowDialog(this) == DialogResult.OK)
        {
            _outputTextBox.Text = folderDialog.SelectedPath;
        }
    }

    private void OnDragEnter(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true)
        {
            e.Effect = DragDropEffects.Copy;
            return;
        }

        e.Effect = DragDropEffects.None;
    }

    private void OnDragDrop(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetData(DataFormats.FileDrop) is not string[] dropped || dropped.Length == 0)
        {
            return;
        }

        _inputTextBox.Text = dropped[0];
        AppendLog($"Input selected: {dropped[0]}");
    }

    private async Task ConvertAsync()
    {
        var input = _inputTextBox.Text.Trim();
        var preset = _presetComboBox.SelectedItem?.ToString();
        var output = string.IsNullOrWhiteSpace(_outputTextBox.Text) ? null : _outputTextBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(input))
        {
            MessageBox.Show(this, "Please select an input file/folder first.", "Missing input", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (!File.Exists(input) && !Directory.Exists(input))
        {
            MessageBox.Show(this, "Input path was not found.", "Invalid input", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        if (string.IsNullOrWhiteSpace(preset))
        {
            MessageBox.Show(this, "Please select a preset.", "Missing preset", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _convertButton.Enabled = false;
        UseWaitCursor = true;
        AppendLog("Starting conversion...");

        try
        {
            var request = new ConversionRequest(
                InputPath: input,
                TargetBody: string.Empty,
                OutputDirectory: output,
                Preset: preset);

            var results = await _batchRunner.ConvertAsync(request);

            AppendLog($"Converted {results.Count} armor item(s).");
            foreach (var result in results)
            {
                var builder = new StringBuilder();
                builder.AppendLine($"Output: {result.OutputDirectory}");
                foreach (var step in result.Steps)
                {
                    builder.AppendLine($"  - {step}");
                }
                AppendLog(builder.ToString().TrimEnd());
            }

            MessageBox.Show(this, "Conversion complete.", "SlideSmith", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            AppendLog($"Conversion failed: {ex.Message}");
            MessageBox.Show(this, $"Conversion failed:\n{ex.Message}", "SlideSmith", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            UseWaitCursor = false;
            _convertButton.Enabled = true;
        }
    }

    private void AppendLog(string message)
    {
        var timestamped = $"[{DateTime.Now:HH:mm:ss}] {message}";
        if (_logTextBox.TextLength == 0)
        {
            _logTextBox.Text = timestamped;
            return;
        }

        _logTextBox.AppendText(Environment.NewLine + timestamped);
    }
}
