using Bodyslide.Core;
using Microsoft.Web.WebView2.WinForms;
using System.Reflection;
using System.Text;
using System.Windows.Forms;

namespace Bodyslide.Desktop;

public sealed class MainForm : Form
{
    private readonly TextBox _inputTextBox;
    private readonly TextBox _outputTextBox;
    private readonly ComboBox _presetComboBox;
    private readonly ComboBox _targetComboBox;
    private readonly TextBox _presetBatchTextBox;
    private readonly TextBox _targetBatchTextBox;
    private readonly ComboBox _profileComboBox;
    private readonly ComboBox _sourceComboBox;
    private readonly TextBox _logTextBox;
    private readonly Button _convertButton;
    private readonly Button _cancelButton;
    private readonly Button _clearLogButton;
    private readonly Button _openInputButton;
    private readonly Button _openOutputButton;
    private readonly Button _openPreviewButton;
    private readonly Button _loadResultButton;
    private readonly Button _openBatchReportButton;
    private readonly RadioButton _usePresetRadio;
    private readonly RadioButton _useCustomTargetRadio;
    private readonly CheckBox _outputZipCheckBox;
    private readonly Label _statusLabel;
    private readonly Label _presetDetailsLabel;
    private readonly ProgressBar _progressBar;
    private readonly TabControl _resultsTabControl;
    private readonly TabPage _previewTabPage;
    private readonly Panel _previewPanel;
    private readonly Label _previewStatusLabel;
    private readonly BatchConversionRunner _batchRunner;

    private CancellationTokenSource? _activeConversion;
    private string? _lastOutputDirectory;
    private string? _lastPreviewPath;
    private string? _lastBatchReportPath;
    private WebView2? _previewWebView;

    public MainForm()
    {
        var appVersion = Assembly
            .GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion ?? "1.0";
        Text = $"SlideSmith v{appVersion}";
        Width = 960;
        Height = 760;
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(860, 680);

        _batchRunner = new BatchConversionRunner(StandaloneConversionModules.CreateDefault());

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 7,
            Padding = new Padding(12),
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
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
            Text = "Drag and drop a .nif file, archive (.zip/.7z/.tar/.tar.gz/.tgz), or armor folder here",
        };
        dropPanel.Controls.Add(dropLabel);
        layout.Controls.Add(dropPanel, 0, 0);

        var inputRow = CreateThreeColumnRow("Input", out _inputTextBox);
        _inputTextBox.AllowDrop = true;
        _inputTextBox.DragEnter += OnDragEnter;
        _inputTextBox.DragDrop += OnDragDrop;
        _inputTextBox.TextChanged += (_, _) => UpdatePathActionStates();
        var browseInputButton = new Button { Text = "Browse...", AutoSize = true };
        browseInputButton.Click += (_, _) => BrowseInput();
        _openInputButton = new Button
        {
            Text = "Open",
            AutoSize = true,
            Enabled = false,
            Margin = new Padding(6, 0, 0, 0),
        };
        _openInputButton.Click += (_, _) => OpenInputPath();
        var inputActions = new FlowLayoutPanel
        {
            AutoSize = true,
            WrapContents = false,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = new Padding(0),
            Padding = new Padding(0),
            Dock = DockStyle.Fill,
        };
        inputActions.Controls.Add(browseInputButton);
        inputActions.Controls.Add(_openInputButton);
        inputRow.Controls.Add(inputActions, 2, 0);
        layout.Controls.Add(inputRow, 0, 1);

        var modeRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            WrapContents = false,
            Margin = new Padding(0, 6, 0, 0),
        };
        _usePresetRadio = new RadioButton
        {
            Text = "Use preset",
            AutoSize = true,
            Checked = true,
        };
        _useCustomTargetRadio = new RadioButton
        {
            Text = "Use custom target",
            AutoSize = true,
        };
        _usePresetRadio.CheckedChanged += (_, _) => RefreshModeState();
        _useCustomTargetRadio.CheckedChanged += (_, _) => RefreshModeState();
        modeRow.Controls.Add(_usePresetRadio);
        modeRow.Controls.Add(_useCustomTargetRadio);
        layout.Controls.Add(modeRow, 0, 2);

        var conversionOptionsPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 2,
            AutoSize = true,
            Margin = new Padding(0, 6, 0, 0),
        };
        conversionOptionsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
        conversionOptionsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));

        var leftOptions = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            ColumnCount = 2,
        };
        leftOptions.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        leftOptions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        leftOptions.Controls.Add(new Label { Text = "Preset", Anchor = AnchorStyles.Left, AutoSize = true }, 0, 0);
        _presetComboBox = new ComboBox
        {
            Dock = DockStyle.Fill,
            DropDownStyle = ComboBoxStyle.DropDownList,
        };
        foreach (var preset in PresetCatalog.All.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase))
        {
            _presetComboBox.Items.Add(preset.Name);
        }
        _presetComboBox.SelectedIndexChanged += (_, _) => UpdatePresetDetails();
        if (_presetComboBox.Items.Count > 0)
        {
            _presetComboBox.SelectedIndex = 0;
        }
        leftOptions.Controls.Add(_presetComboBox, 1, 0);
        leftOptions.Controls.Add(new Label { Text = "Preset batch (optional)", Anchor = AnchorStyles.Left, AutoSize = true }, 0, 1);
        _presetBatchTextBox = new TextBox
        {
            Dock = DockStyle.Fill,
            PlaceholderText = "Example: 3BA Curvy, HIMBO Lean",
        };
        leftOptions.Controls.Add(_presetBatchTextBox, 1, 1);
        leftOptions.Controls.Add(new Label { Text = "Target Body", Anchor = AnchorStyles.Left, AutoSize = true }, 0, 2);
        _targetComboBox = new ComboBox
        {
            Dock = DockStyle.Fill,
            DropDownStyle = ComboBoxStyle.DropDown,
        };
        foreach (var body in BodyTypeCatalog.All.OrderBy(b => b.Name, StringComparer.OrdinalIgnoreCase))
        {
            _targetComboBox.Items.Add(body.Name);
        }
        if (_targetComboBox.Items.Count > 0)
        {
            _targetComboBox.SelectedIndex = 0;
        }
        leftOptions.Controls.Add(_targetComboBox, 1, 2);
        leftOptions.Controls.Add(new Label { Text = "Target batch (optional)", Anchor = AnchorStyles.Left, AutoSize = true }, 0, 3);
        _targetBatchTextBox = new TextBox
        {
            Dock = DockStyle.Fill,
            PlaceholderText = "Example: CBBE, 3BA, HIMBO",
        };
        leftOptions.Controls.Add(_targetBatchTextBox, 1, 3);
        leftOptions.Controls.Add(new Label { Text = "Preset details", Anchor = AnchorStyles.Left, AutoSize = true }, 0, 4);
        _presetDetailsLabel = new Label
        {
            Anchor = AnchorStyles.Left,
            AutoSize = true,
        };
        leftOptions.Controls.Add(_presetDetailsLabel, 1, 4);
        conversionOptionsPanel.Controls.Add(leftOptions, 0, 0);

        var rightOptions = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            ColumnCount = 2,
        };
        rightOptions.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        rightOptions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        rightOptions.Controls.Add(new Label { Text = "Profile (optional)", Anchor = AnchorStyles.Left, AutoSize = true }, 0, 0);
        _profileComboBox = new ComboBox
        {
            Dock = DockStyle.Fill,
            DropDownStyle = ComboBoxStyle.DropDownList,
        };
        _profileComboBox.Items.Add("(auto)");
        foreach (var profile in DeformationProfileModifier.All.OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
        {
            _profileComboBox.Items.Add(profile);
        }
        _profileComboBox.SelectedIndex = 0;
        rightOptions.Controls.Add(_profileComboBox, 1, 0);

        rightOptions.Controls.Add(new Label { Text = "Source Body (optional)", Anchor = AnchorStyles.Left, AutoSize = true }, 0, 1);
        _sourceComboBox = new ComboBox
        {
            Dock = DockStyle.Fill,
            DropDownStyle = ComboBoxStyle.DropDown,
        };
        _sourceComboBox.Items.Add("(auto)");
        foreach (var body in BodyTypeCatalog.All.OrderBy(b => b.Name, StringComparer.OrdinalIgnoreCase))
        {
            _sourceComboBox.Items.Add(body.Name);
        }
        _sourceComboBox.SelectedIndex = 0;
        rightOptions.Controls.Add(_sourceComboBox, 1, 1);
        conversionOptionsPanel.Controls.Add(rightOptions, 1, 0);
        layout.Controls.Add(conversionOptionsPanel, 0, 3);

        var outputRow = CreateThreeColumnRow("Output (optional)", out _outputTextBox);
        _outputTextBox.TextChanged += (_, _) => UpdatePathActionStates();
        var browseOutputButton = new Button { Text = "Browse...", AutoSize = true };
        browseOutputButton.Click += (_, _) => BrowseOutput();
        outputRow.Controls.Add(browseOutputButton, 2, 0);
        layout.Controls.Add(outputRow, 0, 4);

        var actionRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            Margin = new Padding(0, 6, 0, 0),
            WrapContents = true,
        };
        _outputZipCheckBox = new CheckBox
        {
            Text = "Create output zip",
            AutoSize = true,
            Margin = new Padding(0, 8, 12, 0),
        };
        _convertButton = new Button
        {
            Text = "Convert",
            Width = 110,
            Height = 34,
            Margin = new Padding(0, 0, 8, 0),
        };
        _convertButton.Click += async (_, _) => await ConvertAsync();
        _cancelButton = new Button
        {
            Text = "Cancel",
            Width = 90,
            Height = 34,
            Enabled = false,
            Margin = new Padding(0, 0, 8, 0),
        };
        _cancelButton.Click += (_, _) => CancelConversion();
        _clearLogButton = new Button
        {
            Text = "Clear log",
            Width = 90,
            Height = 34,
            Margin = new Padding(0, 0, 8, 0),
        };
        _clearLogButton.Click += (_, _) => ClearLog();
        _openOutputButton = new Button
        {
            Text = "Open output",
            Width = 110,
            Height = 34,
            Enabled = false,
            Margin = new Padding(0, 0, 8, 0),
        };
        _openOutputButton.Click += (_, _) => OpenOutputDirectory();
        _openPreviewButton = new Button
        {
            Text = "Show preview",
            Width = 110,
            Height = 34,
            Enabled = false,
            Margin = new Padding(0, 0, 8, 0),
        };
        _openPreviewButton.Click += async (_, _) => await ShowPreviewReportAsync();
        _loadResultButton = new Button
        {
            Text = "Load result...",
            Width = 110,
            Height = 34,
            Margin = new Padding(0, 0, 8, 0),
        };
        _loadResultButton.Click += async (_, _) => await LoadPreviousResultAsync();
        _openBatchReportButton = new Button
        {
            Text = "Batch report",
            Width = 110,
            Height = 34,
            Enabled = false,
        };
        _openBatchReportButton.Click += (_, _) => OpenBatchReport();
        actionRow.Controls.Add(_outputZipCheckBox);
        actionRow.Controls.Add(_convertButton);
        actionRow.Controls.Add(_cancelButton);
        actionRow.Controls.Add(_clearLogButton);
        actionRow.Controls.Add(_openOutputButton);
        actionRow.Controls.Add(_openPreviewButton);
        actionRow.Controls.Add(_loadResultButton);
        actionRow.Controls.Add(_openBatchReportButton);
        layout.Controls.Add(actionRow, 0, 5);

        var bottomPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
        };
        bottomPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        bottomPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        bottomPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        _statusLabel = new Label
        {
            AutoSize = true,
            Text = "Ready.",
            Margin = new Padding(0, 4, 0, 2),
        };
        _progressBar = new ProgressBar
        {
            Dock = DockStyle.Top,
            Height = 14,
            Style = ProgressBarStyle.Continuous,
            Value = 0,
        };
        _logTextBox = new TextBox
        {
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            Dock = DockStyle.Fill,
            ReadOnly = true,
            Font = new Font("Consolas", 9f),
        };
        _resultsTabControl = new TabControl
        {
            Dock = DockStyle.Fill,
        };
        var logTabPage = new TabPage("Log");
        logTabPage.Controls.Add(_logTextBox);
        _previewTabPage = new TabPage("Preview");
        _previewPanel = new Panel
        {
            Dock = DockStyle.Fill,
        };
        _previewStatusLabel = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
        };
        _previewPanel.Controls.Add(_previewStatusLabel);
        _previewTabPage.Controls.Add(_previewPanel);
        _resultsTabControl.TabPages.Add(logTabPage);
        _resultsTabControl.TabPages.Add(_previewTabPage);
        bottomPanel.Controls.Add(_statusLabel, 0, 0);
        bottomPanel.Controls.Add(_progressBar, 0, 1);
        bottomPanel.Controls.Add(_resultsTabControl, 0, 2);
        layout.Controls.Add(bottomPanel, 0, 6);

        RefreshModeState();
        UpdatePresetDetails();
        UpdatePathActionStates();
        ShowPreviewStatus("Run a conversion to render preview.html in-app.");
        AppendLog("Ready. Choose input, configure options, then click Convert.");
    }

    private static TableLayoutPanel CreateThreeColumnRow(string labelText, out TextBox textBox)
    {
        var row = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 3,
            AutoSize = true,
        };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        row.Controls.Add(new Label { Text = labelText, Anchor = AnchorStyles.Left, AutoSize = true }, 0, 0);
        textBox = new TextBox { Dock = DockStyle.Fill };
        row.Controls.Add(textBox, 1, 0);
        return row;
    }

    private void BrowseInput()
    {
        using var fileDialog = new OpenFileDialog
        {
            Filter = "NIF/Archive Files (*.nif;*.zip;*.7z;*.tar;*.tar.gz;*.tgz)|*.nif;*.zip;*.7z;*.tar;*.tar.gz;*.tgz|All Files (*.*)|*.*",
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
        UpdatePathActionStates();
        AppendLog($"Input selected: {dropped[0]}");
    }

    private void RefreshModeState()
    {
        var usingPreset = _usePresetRadio.Checked;
        _presetComboBox.Enabled = usingPreset;
        _presetBatchTextBox.Enabled = usingPreset;
        _targetComboBox.Enabled = !usingPreset;
        _targetBatchTextBox.Enabled = !usingPreset;
        if (usingPreset && TryGetSelectedPreset(out var preset))
        {
            var targetIndex = _targetComboBox.FindStringExact(preset.TargetBody);
            if (targetIndex >= 0)
            {
                _targetComboBox.SelectedIndex = targetIndex;
            }
        }
    }

    private async Task ConvertAsync()
    {
        var input = _inputTextBox.Text.Trim();
        var output = string.IsNullOrWhiteSpace(_outputTextBox.Text) ? null : _outputTextBox.Text.Trim();
        var usingPreset = _usePresetRadio.Checked;
        var preset = _presetComboBox.SelectedItem?.ToString();
        var target = string.IsNullOrWhiteSpace(_targetComboBox.Text) ? _targetComboBox.SelectedItem?.ToString() : _targetComboBox.Text.Trim();
        var selectedPresets = CombineSelections(preset, ParseDelimitedValues(_presetBatchTextBox.Text));
        var selectedTargets = CombineSelections(target, ParseDelimitedValues(_targetBatchTextBox.Text));
        var profile = ReadOptionalComboValue(_profileComboBox);
        var sourceOverride = string.IsNullOrWhiteSpace(_sourceComboBox.Text) || string.Equals(_sourceComboBox.Text, "(auto)", StringComparison.OrdinalIgnoreCase)
            ? null
            : _sourceComboBox.Text.Trim();

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

        if (usingPreset && selectedPresets.Count == 0)
        {
            MessageBox.Show(this, "Please select at least one preset.", "Missing preset", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (!usingPreset && selectedTargets.Count == 0)
        {
            MessageBox.Show(this, "Please select at least one target body.", "Missing target body", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _activeConversion = new CancellationTokenSource();
        SetBusyState(isBusy: true);
        _statusLabel.Text = "Converting...";
        AppendLog(usingPreset
            ? $"Starting conversion (presets: {string.Join(", ", selectedPresets)})..."
            : $"Starting conversion (targets: {string.Join(", ", selectedTargets)})...");

        try
        {
            var request = new ConversionRequest(
                InputPath: input,
                TargetBody: usingPreset ? string.Empty : selectedTargets.First(),
                OutputDirectory: output,
                Preset: usingPreset ? selectedPresets.First() : null,
                OutputZip: _outputZipCheckBox.Checked,
                DeformationProfile: profile,
                SourceBodyOverride: sourceOverride,
                TargetBodies: !usingPreset && selectedTargets.Count > 1 ? selectedTargets : null,
                Presets: usingPreset && selectedPresets.Count > 1 ? selectedPresets : null);

            var cancellationToken = _activeConversion.Token;

            // Wire a per-item progress callback so the progress bar advances
            // during batch runs instead of showing a marquee spinner throughout.
            var progress = new Progress<BatchProgressUpdate>(update =>
            {
                _progressBar.Style = ProgressBarStyle.Continuous;
                _progressBar.Maximum = update.Total;
                _progressBar.Value = Math.Min(update.Completed, update.Total);
                _statusLabel.Text = $"Converting {update.Completed}/{update.Total}: {update.CurrentFile}";
            });

            var results = await Task.Run(
                () => _batchRunner.ConvertAsync(request, cancellationToken, progress),
                cancellationToken);
            _lastOutputDirectory = GetBestOutputDirectory(results);
            _lastPreviewPath = GetFirstExistingOutputFile(results, "preview.html");
            _lastBatchReportPath = GetFirstExistingOutputFile(results, "batch-report.json");
            UpdatePathActionStates();
            await LoadPreviewInAppAsync(_lastPreviewPath);

            AppendLog($"Converted {results.Count} armor item(s).");
            if (!string.IsNullOrWhiteSpace(_lastPreviewPath))
            {
                AppendLog($"Preview report available: {_lastPreviewPath}");
            }
            if (!string.IsNullOrWhiteSpace(_lastBatchReportPath))
            {
                AppendLog($"Batch report available: {_lastBatchReportPath}");
            }
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

            _statusLabel.Text = "Conversion complete.";
            MessageBox.Show(this, "Conversion complete.", "SlideSmith", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (OperationCanceledException)
        {
            AppendLog("Conversion cancelled.");
            _statusLabel.Text = "Conversion cancelled.";
        }
        catch (Exception ex)
        {
            AppendLog($"Conversion failed: {ex.Message}");
            _statusLabel.Text = "Conversion failed.";
            MessageBox.Show(this, $"Conversion failed:\n{ex.Message}", "SlideSmith", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _activeConversion?.Dispose();
            _activeConversion = null;
            SetBusyState(isBusy: false);
        }
    }

    private void CancelConversion()
    {
        if (_activeConversion is null)
        {
            return;
        }

        _cancelButton.Enabled = false;
        _activeConversion.Cancel();
        AppendLog("Cancellation requested...");
        _statusLabel.Text = "Cancelling...";
    }

    private void SetBusyState(bool isBusy)
    {
        _convertButton.Enabled = !isBusy;
        _cancelButton.Enabled = isBusy;
        _clearLogButton.Enabled = !isBusy;
        _loadResultButton.Enabled = !isBusy;
        _openInputButton.Enabled = !isBusy && InputPathExists();
        _openOutputButton.Enabled = !isBusy && GetPreferredOutputDirectoryForOpen() is not null;
        _openPreviewButton.Enabled = !isBusy && File.Exists(_lastPreviewPath);
        _openBatchReportButton.Enabled = !isBusy && File.Exists(_lastBatchReportPath);
        UseWaitCursor = isBusy;
        _progressBar.Style = isBusy ? ProgressBarStyle.Marquee : ProgressBarStyle.Continuous;
        _progressBar.Value = 0;
    }

    private void OpenInputPath()
    {
        var inputPath = _inputTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(inputPath))
        {
            MessageBox.Show(this, "No input path is currently selected.", "Open input", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (Directory.Exists(inputPath))
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = inputPath,
                UseShellExecute = true,
            });
            return;
        }

        if (File.Exists(inputPath))
        {
            var quotedPath = inputPath.Replace("\"", "\\\"", StringComparison.Ordinal);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"/select,\"{quotedPath}\"",
                UseShellExecute = true,
            });
            return;
        }

        MessageBox.Show(this, "Input path was not found.", "Open input", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        UpdatePathActionStates();
    }

    private void OpenOutputDirectory()
    {
        var outputDirectory = GetPreferredOutputDirectoryForOpen();
        if (outputDirectory is null)
        {
            MessageBox.Show(this, "No output folder is currently available.", "Open output", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = outputDirectory,
            UseShellExecute = true,
        });
    }

    private async Task ShowPreviewReportAsync()
    {
        if (!File.Exists(_lastPreviewPath))
        {
            MessageBox.Show(this, "No preview report is currently available.", "Show preview", MessageBoxButtons.OK, MessageBoxIcon.Information);
            UpdatePathActionStates();
            return;
        }

        await LoadPreviewInAppAsync(_lastPreviewPath);
        _resultsTabControl.SelectedTab = _previewTabPage;
    }

    private async Task LoadPreviousResultAsync()
    {
        using var folderDialog = new FolderBrowserDialog
        {
            Description = "Select a previous SlideSmith output folder containing preview.html",
            UseDescriptionForTitle = true,
        };

        if (folderDialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        var selectedFolder = folderDialog.SelectedPath;
        var previewPath = Path.Combine(selectedFolder, "preview.html");

        if (!File.Exists(previewPath))
        {
            MessageBox.Show(
                this,
                $"No preview.html was found in the selected folder.{Environment.NewLine}{selectedFolder}",
                "Load result",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        _lastPreviewPath = previewPath;
        _lastOutputDirectory = selectedFolder;

        var batchReportCandidate = Path.Combine(selectedFolder, "batch-report.json");
        if (File.Exists(batchReportCandidate))
        {
            _lastBatchReportPath = batchReportCandidate;
        }

        UpdatePathActionStates();
        await LoadPreviewInAppAsync(previewPath);
        _resultsTabControl.SelectedTab = _previewTabPage;
        AppendLog($"Loaded previous result from: {selectedFolder}");
    }

    private async Task LoadPreviewInAppAsync(string? previewPath)
    {
        if (string.IsNullOrWhiteSpace(previewPath) || !File.Exists(previewPath))
        {
            ShowPreviewStatus("No preview report is currently available.");
            return;
        }

        if (!await EnsurePreviewWebViewReadyAsync())
        {
            ShowPreviewStatus("Embedded preview is unavailable (WebView2 runtime missing).");
            return;
        }

        try
        {
            _previewWebView!.Visible = true;
            _previewStatusLabel.Visible = false;
            _previewWebView.Source = new Uri(previewPath, UriKind.Absolute);
        }
        catch (Exception ex)
        {
            ShowPreviewStatus($"Failed to load in-app preview: {ex.Message}");
        }
    }

    private async Task<bool> EnsurePreviewWebViewReadyAsync()
    {
        if (_previewWebView is not null)
        {
            return true;
        }

        try
        {
            _previewWebView = new WebView2
            {
                Dock = DockStyle.Fill,
                Visible = false,
            };
            _previewPanel.Controls.Add(_previewWebView);
            _previewWebView.BringToFront();
            await _previewWebView.EnsureCoreWebView2Async();
            return true;
        }
        catch
        {
            _previewWebView?.Dispose();
            _previewWebView = null;
            return false;
        }
    }

    private void ShowPreviewStatus(string message)
    {
        if (_previewWebView is not null)
        {
            _previewWebView.Visible = false;
        }

        _previewStatusLabel.Text = message;
        _previewStatusLabel.Visible = true;
    }

    private void OpenBatchReport()
    {
        if (!File.Exists(_lastBatchReportPath))
        {
            MessageBox.Show(this, "No batch report is currently available.", "Open batch report", MessageBoxButtons.OK, MessageBoxIcon.Information);
            UpdatePathActionStates();
            return;
        }

        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = _lastBatchReportPath,
            UseShellExecute = true,
        });
    }

    private void ClearLog()
    {
        _logTextBox.Clear();
    }

    private static string? ReadOptionalComboValue(ComboBox comboBox)
    {
        var selected = comboBox.SelectedItem?.ToString();
        if (string.IsNullOrWhiteSpace(selected) || selected.Equals("(auto)", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return selected;
    }

    private static string? GetBestOutputDirectory(IReadOnlyList<ConversionResult> results)
    {
        if (results.Count == 0)
        {
            return null;
        }

        var first = results[0].OutputDirectory;
        if (results.All(r => string.Equals(r.OutputDirectory, first, StringComparison.OrdinalIgnoreCase)))
        {
            return first;
        }

        var commonRoot = FindCommonDirectory(results.Select(r => r.OutputDirectory));
        if (!string.IsNullOrWhiteSpace(commonRoot) && Directory.Exists(commonRoot))
        {
            return commonRoot;
        }

        return Directory.Exists(first) ? first : Path.GetDirectoryName(first);
    }

    private static string? GetFirstExistingOutputFile(IReadOnlyList<ConversionResult> results, string fileName)
    {
        return results
            .SelectMany(r => r.OutputFiles)
            .FirstOrDefault(path =>
                Path.GetFileName(path).Equals(fileName, StringComparison.OrdinalIgnoreCase) &&
                File.Exists(path));
    }

    private static string? FindCommonDirectory(IEnumerable<string> directories)
    {
        var normalized = directories
            .Where(static path => !string.IsNullOrWhiteSpace(path))
            .Select(Path.GetFullPath)
            .ToArray();
        if (normalized.Length == 0)
        {
            return null;
        }

        var candidate = normalized[0];
        while (!string.IsNullOrWhiteSpace(candidate))
        {
            var matchPrefix = candidate.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var allMatch = normalized.All(path =>
                string.Equals(path, candidate, StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith(matchPrefix, StringComparison.OrdinalIgnoreCase));
            if (allMatch)
            {
                return candidate;
            }

            candidate = Path.GetDirectoryName(candidate);
        }

        return null;
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

    private void UpdatePresetDetails()
    {
        if (!TryGetSelectedPreset(out var preset))
        {
            _presetDetailsLabel.Text = "—";
            return;
        }

        _presetDetailsLabel.Text = $"Target: {preset.TargetBody} | Deformation: {preset.DeformationProfile} | Physics: {preset.PhysicsProfile}";
    }

    private bool TryGetSelectedPreset(out ConversionPreset preset)
    {
        preset = default!;
        var selectedPreset = _presetComboBox.SelectedItem?.ToString();
        return !string.IsNullOrWhiteSpace(selectedPreset) && PresetCatalog.TryGet(selectedPreset, out preset);
    }

    private bool InputPathExists()
    {
        var inputPath = _inputTextBox.Text.Trim();
        return File.Exists(inputPath) || Directory.Exists(inputPath);
    }

    private string? GetPreferredOutputDirectoryForOpen()
    {
        if (!string.IsNullOrWhiteSpace(_lastOutputDirectory) && Directory.Exists(_lastOutputDirectory))
        {
            return _lastOutputDirectory;
        }

        var userSelectedOutput = _outputTextBox.Text.Trim();
        return Directory.Exists(userSelectedOutput) ? userSelectedOutput : null;
    }

    private void UpdatePathActionStates()
    {
        if (_activeConversion is not null)
        {
            return;
        }

        _openInputButton.Enabled = InputPathExists();
        _openOutputButton.Enabled = GetPreferredOutputDirectoryForOpen() is not null;
        _openPreviewButton.Enabled = File.Exists(_lastPreviewPath);
        _openBatchReportButton.Enabled = File.Exists(_lastBatchReportPath);
    }

    private static IReadOnlyList<string> ParseDelimitedValues(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? []
            : value
                .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

    private static IReadOnlyList<string> CombineSelections(string? selectedValue, IReadOnlyList<string> enteredValues)
    {
        var combined = new List<string>();
        if (!string.IsNullOrWhiteSpace(selectedValue))
        {
            combined.Add(selectedValue);
        }

        combined.AddRange(enteredValues);
        return combined
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
