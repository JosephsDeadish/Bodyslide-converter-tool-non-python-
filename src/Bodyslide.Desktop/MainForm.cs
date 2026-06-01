using Bodyslide.Core;
using Microsoft.Web.WebView2.WinForms;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Windows.Forms;

namespace Bodyslide.Desktop;

public sealed class MainForm : Form
{
    private readonly TextBox _inputTextBox;
    private readonly TextBox _outputTextBox;
    private readonly TextBox _cachePathTextBox;
    private readonly TextBox _skeletonNifTextBox;
    private readonly ComboBox _presetComboBox;
    private readonly ComboBox _targetComboBox;
    private readonly TextBox _presetBatchTextBox;
    private readonly TextBox _targetBatchTextBox;
    private readonly ComboBox _profileComboBox;
    private readonly ComboBox _sourceComboBox;
    private readonly ComboBox _physicsComboBox;
    private readonly ComboBox _worldModeComboBox;
    private readonly TextBox _logTextBox;
    private readonly Button _convertButton;
    private readonly Button _cancelButton;
    private readonly Button _clearLogButton;
    private readonly Button _inspectInputButton;
    private readonly Button _openInputButton;
    private readonly Button _openOutputButton;
    private readonly Button _openPreviewButton;
    private readonly Button _loadResultButton;
    private readonly Button _openBatchReportButton;
    private readonly Button _openReportButton;
    private readonly Button _openArtifactButton;
    private readonly Button _loadCustomProfileButton;
    private readonly Button _saveProfileButton;
    private readonly Button _inspectCacheButton;
    private readonly Button _openCustomProfileButton;
    private readonly Button _removeCustomProfileButton;
    private readonly Button _clearCustomProfilesButton;
    private readonly RadioButton _usePresetRadio;
    private readonly RadioButton _useCustomTargetRadio;
    private readonly CheckBox _outputZipCheckBox;
    private readonly CheckBox _buildSlidersCheckBox;
    private readonly Label _statusLabel;
    private readonly Label _presetDetailsLabel;
    private readonly ProgressBar _progressBar;
    private readonly TabControl _resultsTabControl;
    private readonly TabPage _previewTabPage;
    private readonly Panel _previewPanel;
    private readonly Label _previewStatusLabel;
    private readonly TabPage _inspectTabPage;
    private readonly ListView _inspectListView;
    private readonly TabPage _summaryTabPage;
    private readonly ListView _summaryListView;
    private readonly TabPage _reportsTabPage;
    private readonly ListView _reportsListView;
    private readonly TabPage _catalogTabPage;
    private readonly ListView _catalogListView;
    private readonly TabPage _artifactsTabPage;
    private readonly ListView _artifactsListView;
    private readonly TabPage _cacheTabPage;
    private readonly ListView _cacheListView;
    private readonly ListView _customProfilesListView;
    private readonly BatchConversionRunner _batchRunner;
    private readonly ConversionInspector _inspector;

    private CancellationTokenSource? _activeConversion;
    private string? _lastOutputDirectory;
    private string? _lastPreviewPath;
    private string? _lastBatchReportPath;
    private WebView2? _previewWebView;
    private readonly List<string> _customProfilePaths = [];
    private static readonly string[] ReportFileNames =
    [
        "batch-report.json",
        "conversion-quality.json",
        "dependency-map.json",
        "skeleton-compatibility.json",
        "texture-summary.json",
        "pose-simulation-report.json",
        "world-physics.json",
        "plugin-patches.json",
    ];

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
        _inspector = StandaloneConversionModules.CreateInspector();

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 9,
            Padding = new Padding(12),
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
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
            Text = "Drag and drop a .nif, plugin (.esp/.esm/.esl), archive (.zip/.7z/.tar/.tar.gz/.tgz), or armor folder here",
        };
        dropPanel.Controls.Add(dropLabel);
        layout.Controls.Add(dropPanel, 0, 0);

        var inputRow = CreateThreeColumnRow("Input", out _inputTextBox);
        _inputTextBox.AllowDrop = true;
        _inputTextBox.DragEnter += OnDragEnter;
        _inputTextBox.DragDrop += OnDragDrop;
        _inputTextBox.TextChanged += (_, _) =>
        {
            UpdatePathActionStates();
            ClearInspectionTab("Input changed. Click Inspect Input to refresh detection and compatibility details.");
        };
        var browseInputFileButton = new Button { Text = "File...", AutoSize = true };
        browseInputFileButton.Click += (_, _) => BrowseInputFile();
        var browseInputFolderButton = new Button { Text = "Folder...", AutoSize = true, Margin = new Padding(4, 0, 0, 0) };
        browseInputFolderButton.Click += (_, _) => BrowseInputFolder();
        _inspectInputButton = new Button
        {
            Text = "Inspect Input",
            AutoSize = true,
            Enabled = false,
            Margin = new Padding(6, 0, 0, 0),
        };
        _inspectInputButton.Click += async (_, _) => await InspectInputAsync();
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
        inputActions.Controls.Add(browseInputFileButton);
        inputActions.Controls.Add(browseInputFolderButton);
        inputActions.Controls.Add(_inspectInputButton);
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
        leftOptions.Controls.Add(new Label(), 0, 4);
        var allBodiesButton = new Button
        {
            Text = "Convert to All Bodies",
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 2, 0, 4),
        };
        allBodiesButton.Click += (_, _) =>
        {
            _useCustomTargetRadio.Checked = true;
            _targetBatchTextBox.Text = "all";
            AppendLog("Target set to all supported body types.");
        };
        leftOptions.Controls.Add(allBodiesButton, 1, 4);
        leftOptions.Controls.Add(new Label { Text = "Preset details", Anchor = AnchorStyles.Left, AutoSize = true }, 0, 5);
        _presetDetailsLabel = new Label
        {
            Anchor = AnchorStyles.Left,
            AutoSize = true,
        };
        leftOptions.Controls.Add(_presetDetailsLabel, 1, 5);
        if (_presetComboBox.Items.Count > 0)
        {
            _presetComboBox.SelectedIndex = 0;
        }
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

        rightOptions.Controls.Add(new Label { Text = "Physics (optional)", Anchor = AnchorStyles.Left, AutoSize = true }, 0, 2);
        _physicsComboBox = new ComboBox
        {
            Dock = DockStyle.Fill,
            DropDownStyle = ComboBoxStyle.DropDownList,
        };
        _physicsComboBox.Items.Add("(auto)");
        foreach (var profile in PhysicsProfileCatalog.All)
        {
            _physicsComboBox.Items.Add(profile);
        }
        _physicsComboBox.SelectedIndex = 0;
        rightOptions.Controls.Add(_physicsComboBox, 1, 2);

        rightOptions.Controls.Add(new Label { Text = "World drop mode (optional)", Anchor = AnchorStyles.Left, AutoSize = true }, 0, 3);
        _worldModeComboBox = new ComboBox
        {
            Dock = DockStyle.Fill,
            DropDownStyle = ComboBoxStyle.DropDownList,
        };
        _worldModeComboBox.Items.Add("(auto)");
        foreach (var worldMode in WorldDropModeCatalog.All)
        {
            _worldModeComboBox.Items.Add(worldMode);
        }
        _worldModeComboBox.SelectedIndex = 0;
        rightOptions.Controls.Add(new Label { Text = "World drop mode (optional)", Anchor = AnchorStyles.Left, AutoSize = true }, 0, 3);
        _worldModeComboBox = new ComboBox
        {
            Dock = DockStyle.Fill,
            DropDownStyle = ComboBoxStyle.DropDownList,
        };
        _worldModeComboBox.Items.Add("(auto)");
        foreach (var worldMode in WorldDropModeCatalog.All)
        {
            _worldModeComboBox.Items.Add(worldMode);
        }
        _worldModeComboBox.SelectedIndex = 0;
        rightOptions.Controls.Add(_worldModeComboBox, 1, 3);

        rightOptions.Controls.Add(new Label { Text = "Skeleton NIF (optional)", Anchor = AnchorStyles.Left, AutoSize = true }, 0, 4);
        var skeletonNifPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            AutoSize = true,
            Margin = new Padding(0),
            Padding = new Padding(0),
        };
        skeletonNifPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        skeletonNifPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _skeletonNifTextBox = new TextBox
        {
            Dock = DockStyle.Fill,
            PlaceholderText = "Optional: path to skeleton.nif (e.g. XPMSSE)",
        };
        var browseSkeletonNifButton = new Button { Text = "Browse...", AutoSize = true };
        browseSkeletonNifButton.Click += (_, _) => BrowseSkeletonNif();
        skeletonNifPanel.Controls.Add(_skeletonNifTextBox, 0, 0);
        skeletonNifPanel.Controls.Add(browseSkeletonNifButton, 1, 0);
        rightOptions.Controls.Add(skeletonNifPanel, 1, 4);

        conversionOptionsPanel.Controls.Add(rightOptions, 1, 0);
        layout.Controls.Add(conversionOptionsPanel, 0, 3);

        var outputRow = CreateThreeColumnRow("Output (optional)", out _outputTextBox);
        _outputTextBox.TextChanged += (_, _) => UpdatePathActionStates();
        var browseOutputButton = new Button { Text = "Browse...", AutoSize = true };
        browseOutputButton.Click += (_, _) => BrowseOutput();
        outputRow.Controls.Add(browseOutputButton, 2, 0);
        layout.Controls.Add(outputRow, 0, 4);

        var cacheRow = CreateThreeColumnRow("Learning cache (optional)", out _cachePathTextBox);
        _cachePathTextBox.PlaceholderText = "Custom path for .conversion-learning-cache.json";
        var browseCacheButton = new Button { Text = "Browse...", AutoSize = true };
        browseCacheButton.Click += (_, _) => BrowseCachePath();
        cacheRow.Controls.Add(browseCacheButton, 2, 0);
        layout.Controls.Add(cacheRow, 0, 5);

        var customProfilesPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 2,
            AutoSize = true,
            Margin = new Padding(0, 6, 0, 0),
        };
        customProfilesPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        customProfilesPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        customProfilesPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        customProfilesPanel.Controls.Add(new Label
        {
            Text = "Loaded custom profiles",
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 0, 0, 4),
        }, 0, 0);

        _customProfilesListView = new ListView
        {
            Dock = DockStyle.Fill,
            Height = 92,
            View = View.Details,
            FullRowSelect = true,
            HideSelection = false,
            MultiSelect = true,
        };
        _customProfilesListView.Columns.Add("Name", 160);
        _customProfilesListView.Columns.Add("Physics", 90);
        _customProfilesListView.Columns.Add("Gender", 80);
        _customProfilesListView.Columns.Add("File", 300);
        _customProfilesListView.SelectedIndexChanged += (_, _) => UpdatePathActionStates();
        _customProfilesListView.DoubleClick += (_, _) => OpenSelectedCustomProfile();
        customProfilesPanel.Controls.Add(_customProfilesListView, 0, 1);

        var customProfileActions = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Margin = new Padding(8, 0, 0, 0),
        };
        _openCustomProfileButton = new Button { Text = "Open profile", AutoSize = true, Enabled = false };
        _openCustomProfileButton.Click += (_, _) => OpenSelectedCustomProfile();
        _removeCustomProfileButton = new Button { Text = "Remove", AutoSize = true, Enabled = false };
        _removeCustomProfileButton.Click += (_, _) => RemoveSelectedCustomProfiles();
        _clearCustomProfilesButton = new Button { Text = "Clear all", AutoSize = true, Enabled = false };
        _clearCustomProfilesButton.Click += (_, _) => ClearCustomProfiles();
        customProfileActions.Controls.Add(_openCustomProfileButton);
        customProfileActions.Controls.Add(_removeCustomProfileButton);
        customProfileActions.Controls.Add(_clearCustomProfilesButton);
        customProfilesPanel.Controls.Add(customProfileActions, 1, 1);
        layout.Controls.Add(customProfilesPanel, 0, 6);

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
        _buildSlidersCheckBox = new CheckBox
        {
            Text = "Build BodySlide files",
            AutoSize = true,
            Checked = true,
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
        _openReportButton = new Button
        {
            Text = "Open report",
            Width = 110,
            Height = 34,
            Enabled = false,
            Margin = new Padding(0, 0, 8, 0),
        };
        _openReportButton.Click += (_, _) => OpenSelectedReport();
        _openArtifactButton = new Button
        {
            Text = "Open file",
            Width = 110,
            Height = 34,
            Enabled = false,
            Margin = new Padding(8, 0, 8, 0),
        };
        _openArtifactButton.Click += (_, _) => OpenSelectedArtifact();
        _loadCustomProfileButton = new Button
        {
            Text = "Load custom profile...",
            Width = 140,
            Height = 34,
            Margin = new Padding(0, 0, 8, 0),
        };
        _loadCustomProfileButton.Click += (_, _) => LoadCustomProfileFile();
        _saveProfileButton = new Button
        {
            Text = "Save profile...",
            Width = 110,
            Height = 34,
            Margin = new Padding(0, 0, 8, 0),
        };
        _saveProfileButton.Click += (_, _) => SaveCurrentProfile();
        _inspectCacheButton = new Button
        {
            Text = "Inspect cache",
            Width = 110,
            Height = 34,
        };
        _inspectCacheButton.Click += async (_, _) => await InspectLearningCacheAsync();
        actionRow.Controls.Add(_outputZipCheckBox);
        actionRow.Controls.Add(_buildSlidersCheckBox);
        actionRow.Controls.Add(_convertButton);
        actionRow.Controls.Add(_cancelButton);
        actionRow.Controls.Add(_clearLogButton);
        actionRow.Controls.Add(_openOutputButton);
        actionRow.Controls.Add(_openPreviewButton);
        actionRow.Controls.Add(_loadResultButton);
        actionRow.Controls.Add(_openBatchReportButton);
        actionRow.Controls.Add(_openReportButton);
        actionRow.Controls.Add(_openArtifactButton);
        actionRow.Controls.Add(_loadCustomProfileButton);
        actionRow.Controls.Add(_saveProfileButton);
        actionRow.Controls.Add(_inspectCacheButton);
        layout.Controls.Add(actionRow, 0, 7);

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
        _inspectTabPage = new TabPage("Inspect");
        _inspectListView = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            HeaderStyle = ColumnHeaderStyle.Nonclickable,
        };
        _inspectListView.Columns.Add("Property", 200);
        _inspectListView.Columns.Add("Value", -2);
        _inspectTabPage.Controls.Add(_inspectListView);
        _resultsTabControl.TabPages.Add(_inspectTabPage);
        _summaryTabPage = new TabPage("Summary");
        _summaryListView = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            HeaderStyle = ColumnHeaderStyle.Nonclickable,
        };
        _summaryListView.Columns.Add("Property", 200);
        _summaryListView.Columns.Add("Value", -2);
        _summaryTabPage.Controls.Add(_summaryListView);
        _resultsTabControl.TabPages.Add(_summaryTabPage);
        _reportsTabPage = new TabPage("Reports");
        _reportsListView = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            HeaderStyle = ColumnHeaderStyle.Nonclickable,
        };
        _reportsListView.Columns.Add("Report", 260);
        _reportsListView.Columns.Add("Property", 180);
        _reportsListView.Columns.Add("Value", -2);
        _reportsListView.SelectedIndexChanged += (_, _) => _openReportButton.Enabled = _reportsListView.SelectedItems.Count > 0;
        _reportsListView.DoubleClick += (_, _) => OpenSelectedReport();
        _reportsTabPage.Controls.Add(_reportsListView);
        _resultsTabControl.TabPages.Add(_reportsTabPage);
        _catalogTabPage = new TabPage("Catalog");
        _catalogListView = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            HeaderStyle = ColumnHeaderStyle.Nonclickable,
        };
        _catalogListView.Columns.Add("Category", 170);
        _catalogListView.Columns.Add("Name", 180);
        _catalogListView.Columns.Add("Details", -2);
        _catalogTabPage.Controls.Add(_catalogListView);
        _resultsTabControl.TabPages.Add(_catalogTabPage);
        _artifactsTabPage = new TabPage("Files");
        _artifactsListView = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
        };
        _artifactsListView.Columns.Add("File", 260);
        _artifactsListView.Columns.Add("Path", -2);
        _artifactsListView.SelectedIndexChanged += (_, _) => _openArtifactButton.Enabled = _artifactsListView.SelectedItems.Count > 0;
        _artifactsListView.DoubleClick += (_, _) => OpenSelectedArtifact();
        _artifactsTabPage.Controls.Add(_artifactsListView);
        _resultsTabControl.TabPages.Add(_artifactsTabPage);
        _cacheTabPage = new TabPage("Cache");
        _cacheListView = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            HeaderStyle = ColumnHeaderStyle.Nonclickable,
        };
        _cacheListView.Columns.Add("Key", 220);
        _cacheListView.Columns.Add("Target", 80);
        _cacheListView.Columns.Add("Mesh", 120);
        _cacheListView.Columns.Add("Strategy", 180);
        _cacheListView.Columns.Add("Clipping", 70);
        _cacheListView.Columns.Add("Correction", 100);
        _cacheListView.Columns.Add("Cached at (UTC)", 150);
        _cacheListView.Columns.Add("Regions", -2);
        _cacheTabPage.Controls.Add(_cacheListView);
        _resultsTabControl.TabPages.Add(_cacheTabPage);
        bottomPanel.Controls.Add(_statusLabel, 0, 0);
        bottomPanel.Controls.Add(_progressBar, 0, 1);
        bottomPanel.Controls.Add(_resultsTabControl, 0, 2);
        layout.Controls.Add(bottomPanel, 0, 8);

        RefreshModeState();
        UpdatePresetDetails();
        PopulateCatalogTab();
        RefreshCustomProfilesList();
        UpdatePathActionStates();
        ClearInspectionTab("Select an input and click Inspect Input to preview body detection, mesh analysis, and skeleton compatibility.");
        PopulateReportsTab([], null);
        PopulateCacheTab([], null);
        ShowPreviewStatus("Run a conversion to render preview.html in-app.");
        AppendLog("Ready. Choose input, configure options, then click Convert.");
    }

    private void PopulateCatalogTab()
    {
        _catalogListView.BeginUpdate();
        _catalogListView.Items.Clear();

        foreach (var preset in PresetCatalog.All.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase))
        {
            _catalogListView.Items.Add(new ListViewItem(
            [
                "Preset",
                preset.Name,
                $"Target={preset.TargetBody}; Deformation={preset.DeformationProfile}; Physics={preset.PhysicsProfile}",
            ]));
        }

        foreach (var body in BodyTypeCatalog.All.OrderBy(b => b.Name, StringComparer.OrdinalIgnoreCase))
        {
            var vertexRange = body.VertexCountMin > 0
                ? $"{body.VertexCountMin}-{body.VertexCountMax}"
                : "n/a";
            _catalogListView.Items.Add(new ListViewItem(
            [
                "Body",
                body.Name,
                $"Tokens=[{string.Join(", ", body.DetectionTokens)}]; Vertices={vertexRange}",
            ]));
        }

        foreach (var profile in DeformationProfileModifier.All.OrderBy(static p => p, StringComparer.OrdinalIgnoreCase))
        {
            _catalogListView.Items.Add(new ListViewItem(["Deformation profile", profile, ""]));
        }

        foreach (var physics in PhysicsProfileCatalog.All.OrderBy(static p => p, StringComparer.OrdinalIgnoreCase))
        {
            _catalogListView.Items.Add(new ListViewItem(["Physics profile", physics, ""]));
        }

        foreach (var worldMode in WorldDropModeCatalog.All.OrderBy(static mode => mode, StringComparer.OrdinalIgnoreCase))
        {
            _catalogListView.Items.Add(new ListViewItem(["World drop mode", worldMode, "Controls world-physics.json mode output."]));
        }

        _catalogListView.Items.Add(new ListViewItem(["Target alias", "all / any / *", "Expands to every supported body type."]));
        _catalogListView.EndUpdate();
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

    private void BrowseInputFile()
    {
        using var fileDialog = new OpenFileDialog
        {
            Filter = "Armor Files (*.nif;*.esp;*.esm;*.esl;*.zip;*.7z;*.tar;*.tar.gz;*.tgz)|*.nif;*.esp;*.esm;*.esl;*.zip;*.7z;*.tar;*.tar.gz;*.tgz|All Files (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false,
        };

        if (fileDialog.ShowDialog(this) == DialogResult.OK)
        {
            _inputTextBox.Text = fileDialog.FileName;
        }
    }

    private void BrowseInputFolder()
    {
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

    private void BrowseSkeletonNif()
    {
        using var fileDialog = new OpenFileDialog
        {
            Title = "Select skeleton.nif",
            Filter = "NIF Files (*.nif)|*.nif|All Files (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false,
        };

        if (fileDialog.ShowDialog(this) == DialogResult.OK)
        {
            _skeletonNifTextBox.Text = fileDialog.FileName;
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

    private void BrowseCachePath()
    {
        using var dialog = new SaveFileDialog
        {
            Title = "Select learning cache file path",
            Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
            FileName = ".conversion-learning-cache.json",
            DefaultExt = "json",
            AddExtension = true,
            CheckPathExists = true,
            OverwritePrompt = false,
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _cachePathTextBox.Text = dialog.FileName;
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
        var physicsOverride = ReadOptionalComboValue(_physicsComboBox);
        var worldModeOverride = ReadOptionalComboValue(_worldModeComboBox);
        var cachePathOverride = ReadOptionalPathValue(_cachePathTextBox.Text);
        var sourceOverride = string.IsNullOrWhiteSpace(_sourceComboBox.Text) || string.Equals(_sourceComboBox.Text, "(auto)", StringComparison.OrdinalIgnoreCase)
            ? null
            : _sourceComboBox.Text.Trim();
        var skeletonNifPath = string.IsNullOrWhiteSpace(_skeletonNifTextBox.Text) ? null : _skeletonNifTextBox.Text.Trim();

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
                Presets: usingPreset && selectedPresets.Count > 1 ? selectedPresets : null,
                PhysicsProfileOverride: physicsOverride,
                GenerateBodySlideFiles: _buildSlidersCheckBox.Checked,
                CustomProfilePaths: _customProfilePaths.Count > 0 ? [.. _customProfilePaths] : null,
                WorldDropModeOverride: worldModeOverride,
                SkeletonNifPath: skeletonNifPath);

            ConversionLearningCache.SetGlobalCachePath(cachePathOverride);
            if (!string.IsNullOrWhiteSpace(cachePathOverride))
            {
                AppendLog($"Learning cache override: {cachePathOverride}");
            }

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
            _ = await LoadPreviewInAppAsync(_lastPreviewPath);
            PopulateSummaryTab(results);
            PopulateReportsTab(results);
            PopulateArtifactsTab(results);

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

    private async Task InspectInputAsync()
    {
        var input = _inputTextBox.Text.Trim();
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

        try
        {
            SetBusyState(isBusy: true);
            _statusLabel.Text = "Inspecting input...";
            ClearInspectionTab("Inspecting input...");

            var inspection = await _inspector.InspectAsync(
                input,
                ResolveInspectionTargetBody(),
                _customProfilePaths.Count > 0 ? [.. _customProfilePaths] : null,
                skeletonNifPath: string.IsNullOrWhiteSpace(_skeletonNifTextBox.Text) ? null : _skeletonNifTextBox.Text.Trim());

            PopulateInspectionTab(inspection);
            _resultsTabControl.SelectedTab = _inspectTabPage;
            _statusLabel.Text = "Inspection complete.";
            AppendLog($"Inspection complete: body={inspection.Detection.Body} ({inspection.Detection.Confidence:P0}), mesh={inspection.Analysis.MeshType}.");
        }
        catch (Exception ex)
        {
            ClearInspectionTab($"Inspection failed: {ex.Message}");
            _statusLabel.Text = "Inspection failed.";
            MessageBox.Show(this, $"Failed to inspect input:\n{ex.Message}", "Inspect input", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            SetBusyState(isBusy: false);
        }
    }

    private string? ResolveInspectionTargetBody()
    {
        if (_usePresetRadio.Checked && TryGetSelectedPreset(out var preset))
        {
            return preset.TargetBody;
        }

        return string.IsNullOrWhiteSpace(_targetComboBox.Text)
            ? _targetComboBox.SelectedItem?.ToString()
            : _targetComboBox.Text.Trim();
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
        _inspectInputButton.Enabled = !isBusy && InputPathExists();
        _loadResultButton.Enabled = !isBusy;
        _inspectCacheButton.Enabled = !isBusy;
        _openInputButton.Enabled = !isBusy && InputPathExists();
        _openOutputButton.Enabled = !isBusy && GetPreferredOutputDirectoryForOpen() is not null;
        _openPreviewButton.Enabled = !isBusy && File.Exists(_lastPreviewPath);
        _openBatchReportButton.Enabled = !isBusy && File.Exists(_lastBatchReportPath);
        _openReportButton.Enabled = !isBusy && _reportsListView.SelectedItems.Count > 0;
        _openArtifactButton.Enabled = !isBusy && _artifactsListView.SelectedItems.Count > 0;
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

        _ = await LoadPreviewInAppAsync(_lastPreviewPath);
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
        _ = await LoadPreviewInAppAsync(previewPath);
        PopulateReportsTab(selectedFolder);
        PopulateArtifactsTab(selectedFolder);
        _resultsTabControl.SelectedTab = _previewTabPage;
        AppendLog($"Loaded previous result from: {selectedFolder}");
    }

    private async Task<bool> LoadPreviewInAppAsync(string? previewPath)
    {
        if (string.IsNullOrWhiteSpace(previewPath) || !File.Exists(previewPath))
        {
            ShowPreviewStatus("No preview report is currently available.");
            return false;
        }

        if (!await EnsurePreviewWebViewReadyAsync())
        {
            ShowPreviewStatus("Embedded preview is unavailable (WebView2 runtime missing). Opening preview in your default browser.");
            OpenPreviewExternally(previewPath);
            return false;
        }

        try
        {
            _previewWebView!.Visible = true;
            _previewStatusLabel.Visible = false;
            _previewWebView.Source = new Uri(previewPath, UriKind.Absolute);
            return true;
        }
        catch (Exception ex)
        {
            ShowPreviewStatus($"Failed to load in-app preview: {ex.Message}");
            OpenPreviewExternally(previewPath);
            return false;
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

    private void OpenPreviewExternally(string previewPath)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = previewPath,
                UseShellExecute = true,
            });
            AppendLog($"Opened preview in external browser: {previewPath}");
        }
        catch (Exception ex)
        {
            AppendLog($"Failed to open preview externally: {ex.Message}");
        }
    }

    private void PopulateSummaryTab(IReadOnlyList<ConversionResult> results)
    {
        _summaryListView.Items.Clear();

        void Add(string property, string value) =>
            _summaryListView.Items.Add(new ListViewItem([property, value]));

        Add("Items converted", results.Count.ToString());

        // Aggregate key steps across all results.
        foreach (var result in results)
        {
            if (results.Count > 1)
            {
                _summaryListView.Items.Add(new ListViewItem([string.Empty, string.Empty]));
                Add("Output", result.OutputDirectory);
            }

            foreach (var step in result.Steps)
            {
                if (step.StartsWith("detected-body:", StringComparison.Ordinal))
                    Add("Detected body", step["detected-body:".Length..]);
                else if (step.StartsWith("source-body-override:", StringComparison.Ordinal))
                    Add("Source body (override)", step["source-body-override:".Length..]);
                else if (step.StartsWith("mesh-type:", StringComparison.Ordinal))
                    Add("Mesh type", step["mesh-type:".Length..]);
                else if (step.StartsWith("cage:", StringComparison.Ordinal))
                    Add("Cage mode", step["cage:".Length..]);
                else if (step.StartsWith("mesh-converted:", StringComparison.Ordinal))
                    Add("Conversion strategy", step["mesh-converted:".Length..]);
                else if (step.StartsWith("physics:", StringComparison.Ordinal))
                    Add("Physics profile", step["physics:".Length..]);
                else if (step.StartsWith("physics-override:", StringComparison.Ordinal))
                    Add("Physics (override)", step["physics-override:".Length..]);
                else if (step.StartsWith("world-mode-override:", StringComparison.Ordinal))
                    Add("World drop mode (override)", step["world-mode-override:".Length..]);
                else if (step.StartsWith("skeleton:", StringComparison.Ordinal))
                    Add("Skeleton mapping", step["skeleton:".Length..]);
                else if (step.StartsWith("skeleton-warnings:", StringComparison.Ordinal))
                    Add("Skeleton warnings", step["skeleton-warnings:".Length..]);
                else if (step.StartsWith("morphs:", StringComparison.Ordinal))
                    Add("Morphs", step["morphs:".Length..]);
                else if (step.StartsWith("clipping:", StringComparison.Ordinal))
                    Add("Clipping", step["clipping:".Length..]);
                else if (step.StartsWith("correction:", StringComparison.Ordinal))
                    Add("Auto-correction", step["correction:".Length..]);
                else if (step.StartsWith("correction-applied:", StringComparison.Ordinal))
                    Add("Correction regions", step["correction-applied:".Length..]);
                else if (step.StartsWith("voxel-collision:", StringComparison.Ordinal))
                    Add("Voxel collision", step["voxel-collision:".Length..]);
                else if (step.StartsWith("voxel-push-applied:", StringComparison.Ordinal))
                    Add("Voxel push-out", step["voxel-push-applied:".Length..]);
                else if (step.StartsWith("weights:", StringComparison.Ordinal))
                    Add("Weight profile", step["weights:".Length..]);
                else if (step.StartsWith("weight-solver:", StringComparison.Ordinal))
                    Add("Weight solver", step["weight-solver:".Length..]);
                else if (step.StartsWith("physics-injection:", StringComparison.Ordinal))
                    Add("Physics bone injection", step["physics-injection:".Length..]);
                else if (step.StartsWith("bodyslide:", StringComparison.Ordinal))
                    Add("BodySlide project", step["bodyslide:".Length..]);
                else if (step.StartsWith("regions:", StringComparison.Ordinal))
                    Add("Armor regions", step["regions:".Length..]);
                else if (step.StartsWith("rigid-islands:", StringComparison.Ordinal))
                    Add("Rigid islands", step["rigid-islands:".Length..]);
                else if (step.StartsWith("normals:", StringComparison.Ordinal))
                    Add("Normal recalc", step["normals:".Length..]);
                else if (step.StartsWith("partitions:", StringComparison.Ordinal))
                    Add("Partitions", step["partitions:".Length..]);
                else if (step.StartsWith("biped-slots-passthrough:", StringComparison.Ordinal))
                    Add("Biped slots (plugin)", step["biped-slots-passthrough:".Length..]);
                else if (step.StartsWith("pose-simulation:", StringComparison.Ordinal))
                    Add("Pose simulation", step["pose-simulation:".Length..]);
                else if (step.StartsWith("plugins:", StringComparison.Ordinal))
                    Add("Plugins", step["plugins:".Length..]);
                else if (step.StartsWith("vanilla-armor:", StringComparison.Ordinal))
                    Add("Vanilla armor", step["vanilla-armor:".Length..]);
                else if (step.StartsWith("vanilla-profile:", StringComparison.Ordinal))
                    Add("Vanilla profile", step["vanilla-profile:".Length..]);
                else if (step.StartsWith("weight-variants:", StringComparison.Ordinal))
                    Add("Weight variants (_0/_1)", step["weight-variants:".Length..]);
                else if (step.StartsWith("smp-bones:", StringComparison.Ordinal))
                    Add("SMP bones", step["smp-bones:".Length..]);
                else if (step.StartsWith("race-compat:", StringComparison.Ordinal))
                    Add("Race compatibility", step["race-compat:".Length..]);
                else if (step.StartsWith("learning-cache:", StringComparison.Ordinal))
                    Add("Learning cache", step["learning-cache:".Length..]);
                else if (step.StartsWith("conversion-delta:", StringComparison.Ordinal))
                    Add("Conversion delta", step["conversion-delta:".Length..]);
                else if (step.StartsWith("textures:", StringComparison.Ordinal))
                    Add("Texture warnings", step["textures:".Length..]);
                else if (step.StartsWith("imported:", StringComparison.Ordinal))
                    Add("Imported assets", step["imported:".Length..]);
                else if (step.StartsWith("exported:", StringComparison.Ordinal))
                    Add("Output directory", step["exported:".Length..]);
            }

            Add("Output files", result.OutputFiles.Count.ToString());
        }
    }

    private void PopulateInspectionTab(ConversionInspectionResult inspection)
    {
        _inspectListView.BeginUpdate();
        try
        {
            _inspectListView.Items.Clear();

            void Add(string property, string value) =>
                _inspectListView.Items.Add(new ListViewItem([property, value]));

            Add("Input", inspection.InputPath);
            if (!string.IsNullOrWhiteSpace(inspection.RequestedTargetBody))
            {
                Add("Target body", inspection.RequestedTargetBody);
            }

            Add("Detected body", $"{inspection.Detection.Body} ({inspection.Detection.Confidence:P1})");
            Add("Detection evidence", inspection.Detection.Evidence.Count == 0
                ? "None"
                : string.Join(", ", inspection.Detection.Evidence));
            Add("Mesh type", inspection.Analysis.MeshType);
            Add("Physics enabled", inspection.Analysis.PhysicsEnabled ? "Yes" : "No");
            if (!string.IsNullOrWhiteSpace(inspection.Analysis.HeadgearSubType))
            {
                Add("Headgear subtype", inspection.Analysis.HeadgearSubType);
            }

            Add("Mesh files", inspection.Armor.MeshFiles.Count.ToString());
            Add("Texture files", inspection.Armor.TextureFiles.Count.ToString());
            Add("Physics files", inspection.Armor.PhysicsFiles.Count.ToString());
            Add("Body references", inspection.Armor.BodyReferenceFiles.Count.ToString());
            Add("Weight variants", inspection.Armor.WeightVariantPairs?.Count.ToString() ?? "0");
            Add("Custom body profiles", inspection.Armor.CustomBodyProfiles?.Count.ToString() ?? "0");
            if (inspection.Armor.CustomBodyProfiles is { Count: > 0 } customProfiles)
            {
                Add("Custom body names", string.Join(", ", customProfiles.Select(profile => profile.Name)));
            }

            if (inspection.SkeletonMapping is not null)
            {
                Add("Source skeleton", inspection.SkeletonMapping.SourceSkeleton);
                Add("Target skeleton", inspection.SkeletonMapping.TargetSkeleton);
                Add("Mapped bones", inspection.SkeletonMapping.BoneMappings.Count.ToString());
                Add("Unsupported bones", inspection.SkeletonMapping.UnsupportedBones.Count == 0
                    ? "None"
                    : string.Join(", ", inspection.SkeletonMapping.UnsupportedBones));
            }
            else
            {
                Add("Skeleton mapping", "Select a target or preset to inspect compatibility.");
            }
        }
        finally
        {
            _inspectListView.EndUpdate();
        }
    }

    private void ClearInspectionTab(string message)
    {
        if (_inspectListView is null)
        {
            return;
        }

        _inspectListView.BeginUpdate();
        try
        {
            _inspectListView.Items.Clear();
            _inspectListView.Items.Add(new ListViewItem(["Status", message]));
        }
        finally
        {
            _inspectListView.EndUpdate();
        }
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

    private void OpenSelectedReport()
    {
        if (_reportsListView.SelectedItems.Count == 0)
        {
            return;
        }

        if (_reportsListView.SelectedItems[0].Tag is not string filePath || !File.Exists(filePath))
        {
            MessageBox.Show(this, "Selected report file was not found.", "Open report", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            PopulateReportsTab(GetPreferredOutputDirectoryForOpen());
            return;
        }

        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = filePath,
            UseShellExecute = true,
        });
    }

    private void OpenSelectedArtifact()
    {
        if (_artifactsListView.SelectedItems.Count == 0)
        {
            return;
        }

        if (_artifactsListView.SelectedItems[0].Tag is not string filePath || !File.Exists(filePath))
        {
            MessageBox.Show(this, "Selected output file was not found.", "Open file", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            PopulateArtifactsTab(GetPreferredOutputDirectoryForOpen());
            return;
        }

        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = filePath,
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

    private static string? ReadOptionalPathValue(string? path) =>
        string.IsNullOrWhiteSpace(path) ? null : path.Trim();

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

        _inspectInputButton.Enabled = InputPathExists();
        _openInputButton.Enabled = InputPathExists();
        _openOutputButton.Enabled = GetPreferredOutputDirectoryForOpen() is not null;
        _openPreviewButton.Enabled = File.Exists(_lastPreviewPath);
        _openBatchReportButton.Enabled = File.Exists(_lastBatchReportPath);
        _openReportButton.Enabled = _reportsListView.SelectedItems.Count > 0;
        _openArtifactButton.Enabled = _artifactsListView.SelectedItems.Count > 0;
        _openCustomProfileButton.Enabled = _customProfilesListView.SelectedItems.Count == 1;
        _removeCustomProfileButton.Enabled = _customProfilesListView.SelectedItems.Count > 0;
        _clearCustomProfilesButton.Enabled = _customProfilePaths.Count > 0;
    }

    private static IReadOnlyList<string> ParseDelimitedValues(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? []
            : value
                .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

    private async Task InspectLearningCacheAsync()
    {
        try
        {
            var cachePathOverride = ReadOptionalPathValue(_cachePathTextBox.Text);
            ConversionLearningCache.SetGlobalCachePath(cachePathOverride);

            var entries = await ConversionLearningCache.LoadMergedEntriesAsync(string.Empty, CancellationToken.None);
            if (entries.Count == 0)
            {
                AppendLog("Learning cache is empty.");
                PopulateCacheTab([], cachePathOverride);
                _resultsTabControl.SelectedTab = _cacheTabPage;
                MessageBox.Show(this, "Learning cache is empty.", "Inspect cache", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            AppendLog($"Learning cache: {entries.Count} entr{(entries.Count == 1 ? "y" : "ies")}.");
            foreach (var entry in entries.OrderBy(e => e.Key, StringComparer.OrdinalIgnoreCase))
            {
                AppendLog($"[{entry.Key}] target={entry.TargetBody}, mesh={entry.MeshType}, strategy={entry.Strategy}, cached={entry.LastSuccessfulConversion:u}");
            }
            PopulateCacheTab(entries, cachePathOverride);
            _resultsTabControl.SelectedTab = _cacheTabPage;

            MessageBox.Show(this, $"Loaded {entries.Count} learning-cache entr{(entries.Count == 1 ? "y" : "ies")}. Details were added to the Cache tab and log.", "Inspect cache", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Failed to inspect learning cache:\n{ex.Message}", "Inspect cache", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void PopulateCacheTab(IReadOnlyList<ConversionCacheEntry> entries, string? cachePathOverride)
    {
        _cacheListView.BeginUpdate();
        try
        {
            _cacheListView.Items.Clear();
            if (entries.Count == 0)
            {
                var cacheLabel = string.IsNullOrWhiteSpace(cachePathOverride)
                    ? "(global cache)"
                    : cachePathOverride;
                _cacheListView.Items.Add(new ListViewItem(
                [
                    "Status",
                    "",
                    "",
                    "",
                    "",
                    "",
                    "",
                    $"No cache entries loaded. Source: {cacheLabel}",
                ]));
                return;
            }

            foreach (var entry in entries.OrderBy(e => e.Key, StringComparer.OrdinalIgnoreCase))
            {
                var regions = entry.RegionalMorphing.Count == 0
                    ? "None"
                    : string.Join(", ", entry.RegionalMorphing
                        .OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
                        .Select(kv => $"{kv.Key}={kv.Value:F3}"));
                _cacheListView.Items.Add(new ListViewItem(
                [
                    entry.Key,
                    entry.TargetBody,
                    entry.MeshType,
                    entry.Strategy,
                    entry.HadClipping ? "Yes" : "No",
                    entry.CorrectionMethod,
                    entry.LastSuccessfulConversion.ToString("u"),
                    regions,
                ]));
            }
        }
        finally
        {
            _cacheListView.EndUpdate();
        }
    }

    private void RefreshCustomProfilesList(string? selectedPath = null)
    {
        _customProfilesListView.BeginUpdate();
        try
        {
            _customProfilesListView.Items.Clear();
            foreach (var path in _customProfilePaths.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(static p => p, StringComparer.OrdinalIgnoreCase))
            {
                var summary = ReadCustomProfileSummary(path);
                var item = new ListViewItem(summary.Name);
                item.SubItems.Add(summary.PhysicsProfile);
                item.SubItems.Add(summary.Gender);
                item.SubItems.Add(Path.GetFileName(path));
                item.Tag = path;
                item.ToolTipText = path;
                _customProfilesListView.Items.Add(item);

                if (!string.IsNullOrWhiteSpace(selectedPath) &&
                    string.Equals(path, selectedPath, StringComparison.OrdinalIgnoreCase))
                {
                    item.Selected = true;
                }
            }
        }
        finally
        {
            _customProfilesListView.EndUpdate();
        }

        UpdatePathActionStates();
    }

    private static (string Name, string PhysicsProfile, string Gender) ReadCustomProfileSummary(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            using var document = JsonDocument.Parse(stream);
            var root = document.RootElement;
            var name = root.TryGetProperty("Name", out var nameProperty) && nameProperty.ValueKind == JsonValueKind.String
                ? nameProperty.GetString()
                : null;
            var physics = root.TryGetProperty("PhysicsProfile", out var physicsProperty) && physicsProperty.ValueKind == JsonValueKind.String
                ? physicsProperty.GetString()
                : null;
            var gender = root.TryGetProperty("Gender", out var genderProperty) && genderProperty.ValueKind == JsonValueKind.String
                ? genderProperty.GetString()
                : null;
            return (
                string.IsNullOrWhiteSpace(name) ? Path.GetFileNameWithoutExtension(path) ?? "Custom" : name.Trim(),
                string.IsNullOrWhiteSpace(physics) ? "none" : physics.Trim(),
                string.IsNullOrWhiteSpace(gender) ? "female" : gender.Trim());
        }
        catch
        {
            return (Path.GetFileNameWithoutExtension(path) ?? "Custom", "?", "?");
        }
    }

    private string? GetSelectedCustomProfilePath() =>
        _customProfilesListView.SelectedItems.Count == 1
            ? _customProfilesListView.SelectedItems[0].Tag as string
            : null;

    private void OpenSelectedCustomProfile()
    {
        var path = GetSelectedCustomProfilePath();
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            MessageBox.Show(this, "Selected custom profile file was not found.", "Open profile", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true,
        });
    }

    private void RemoveSelectedCustomProfiles()
    {
        var selectedPaths = _customProfilesListView.SelectedItems
            .Cast<ListViewItem>()
            .Select(static item => item.Tag as string)
            .Where(static path => !string.IsNullOrWhiteSpace(path))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (selectedPaths.Length == 0)
        {
            return;
        }

        _customProfilePaths.RemoveAll(path => selectedPaths.Contains(path, StringComparer.OrdinalIgnoreCase));
        RefreshCustomProfilesList();
        AppendLog($"Removed {selectedPaths.Length} custom profile file(s).");
        ClearInspectionTab("Custom body profiles changed. Click Inspect Input to refresh detection and compatibility details.");
    }

    private void ClearCustomProfiles()
    {
        if (_customProfilePaths.Count == 0)
        {
            return;
        }

        _customProfilePaths.Clear();
        RefreshCustomProfilesList();
        AppendLog("Cleared all loaded custom profile files.");
        ClearInspectionTab("Custom body profiles changed. Click Inspect Input to refresh detection and compatibility details.");
    }

    private void LoadCustomProfileFile()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Load Custom Body Profile",
            Filter = "JSON profile files (*.json)|*.json|All files (*.*)|*.*",
            Multiselect = true,
        };

        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        var added = 0;
        foreach (var path in dialog.FileNames)
        {
            if (!_customProfilePaths.Contains(path, StringComparer.OrdinalIgnoreCase))
            {
                _customProfilePaths.Add(path);
                added++;
            }
        }

        if (added > 0)
        {
            AppendLog($"Loaded {added} custom profile file(s): {string.Join(", ", dialog.FileNames.Select(Path.GetFileName))}");
            RefreshCustomProfilesList(dialog.FileNames[0]);
            ClearInspectionTab("Custom body profiles changed. Click Inspect Input to refresh detection and compatibility details.");
        }
    }

    private void SaveCurrentProfile()
    {
        using var dialog = new SaveFileDialog
        {
            Title = "Save Current Settings as Profile",
            Filter = "JSON profile files (*.json)|*.json",
            FileName = "custom-body-profile.json",
            DefaultExt = "json",
        };

        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        var effectiveTarget = ResolveProfileTargetName();
        if (string.IsNullOrWhiteSpace(effectiveTarget))
        {
            MessageBox.Show(this, "Select or type a target body before saving a profile.", "Save profile", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var effectivePhysics = ResolveEffectivePhysicsProfile(effectiveTarget);
        var effectiveProfile = ResolveEffectiveDeformationProfile();
        var baseField = CreateBaseTransformationField(effectiveTarget);
        var transformedField = ApplyDeformationProfile(baseField, effectiveProfile);
        var bodyInfo = BodyTypeCatalog.All.FirstOrDefault(body => string.Equals(body.Name, effectiveTarget, StringComparison.OrdinalIgnoreCase));
        var gender = IsMaleBody(effectiveTarget) ? "male" : "female";
        var payload = new
        {
            Name = effectiveTarget,
            DetectionTokens = bodyInfo?.DetectionTokens ?? [effectiveTarget],
            VertexCountMin = bodyInfo?.VertexCountMin ?? 0,
            VertexCountMax = bodyInfo?.VertexCountMax ?? 0,
            TransformationField = transformedField,
            SliderNames = ResolveSliderNames(effectiveTarget),
            PhysicsProfile = effectivePhysics,
            BodyOutputPath = ResolveBodyOutputPath(effectiveTarget),
            Gender = gender,
        };

        try
        {
            var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(dialog.FileName, json, Encoding.UTF8);
            AppendLog($"Saved profile to: {dialog.FileName}");

            if (!_customProfilePaths.Contains(dialog.FileName, StringComparer.OrdinalIgnoreCase))
            {
                _customProfilePaths.Add(dialog.FileName);
            }

            RefreshCustomProfilesList(dialog.FileName);
            ClearInspectionTab("Custom body profiles changed. Click Inspect Input to refresh detection and compatibility details.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            MessageBox.Show(this, $"Failed to save profile:\n{ex.Message}", "Save profile", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private string ResolveProfileTargetName()
    {
        if (_usePresetRadio.Checked && TryGetSelectedPreset(out var preset))
        {
            return preset.TargetBody;
        }

        var targetText = string.IsNullOrWhiteSpace(_targetComboBox.Text)
            ? _targetComboBox.SelectedItem?.ToString()
            : _targetComboBox.Text.Trim();
        return string.IsNullOrWhiteSpace(targetText) ? "CUSTOM" : targetText;
    }

    private string? ResolveEffectiveDeformationProfile()
    {
        var overrideProfile = ReadOptionalComboValue(_profileComboBox);
        if (!string.IsNullOrWhiteSpace(overrideProfile))
        {
            return overrideProfile;
        }

        return _usePresetRadio.Checked && TryGetSelectedPreset(out var preset)
            ? preset.DeformationProfile
            : null;
    }

    private string ResolveEffectivePhysicsProfile(string targetBody)
    {
        var overridePhysics = ReadOptionalComboValue(_physicsComboBox);
        if (PhysicsProfileCatalog.TryNormalize(overridePhysics, out var normalizedOverride))
        {
            return normalizedOverride;
        }

        if (_usePresetRadio.Checked &&
            TryGetSelectedPreset(out var preset) &&
            PhysicsProfileCatalog.TryNormalize(preset.PhysicsProfile, out var normalizedPreset))
        {
            return normalizedPreset;
        }

        return PhysicsProfileCatalog.GetDefaultForTargetBody(targetBody);
    }

    private static Dictionary<string, double> CreateBaseTransformationField(string targetBody)
    {
        var field = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
        {
            ["chest"] = 1.02,
            ["waist"] = 0.99,
            ["pelvis"] = 1.02,
            ["legs"] = 1.01,
            ["shoulders"] = 1.00,
            ["breasts"] = 1.02,
            ["butt"] = 1.01,
            ["belly"] = 1.01,
            ["arms"] = 1.00,
            ["thighs"] = 1.01,
            ["calves"] = 1.01,
        };

        foreach (var (region, value) in targetBody.Trim().ToUpperInvariant() switch
        {
            "CBBE" => new (string, double)[] { ("chest", 1.08), ("waist", 0.96), ("pelvis", 1.05), ("legs", 1.03), ("shoulders", 1.01), ("breasts", 1.09), ("butt", 1.06), ("belly", 1.02), ("arms", 1.01), ("thighs", 1.04), ("calves", 1.02) },
            "3BA" => new (string, double)[] { ("chest", 1.12), ("waist", 0.95), ("pelvis", 1.06), ("legs", 1.04), ("shoulders", 1.01), ("breasts", 1.13), ("butt", 1.08), ("belly", 1.03), ("arms", 1.02), ("thighs", 1.05), ("calves", 1.03) },
            "BHUNP" => new (string, double)[] { ("chest", 1.10), ("waist", 0.94), ("pelvis", 1.07), ("legs", 1.04), ("shoulders", 1.01), ("breasts", 1.11), ("butt", 1.07), ("belly", 1.03), ("arms", 1.01), ("thighs", 1.05), ("calves", 1.03) },
            "UNP" => new (string, double)[] { ("chest", 1.04), ("waist", 0.97), ("pelvis", 1.02), ("legs", 1.01), ("shoulders", 1.00), ("breasts", 1.04), ("butt", 1.02), ("belly", 1.01), ("arms", 1.00), ("thighs", 1.02), ("calves", 1.01) },
            "TBD" => new (string, double)[] { ("chest", 1.06), ("waist", 0.96), ("pelvis", 1.04), ("legs", 1.02), ("shoulders", 1.00), ("breasts", 1.07), ("butt", 1.04), ("belly", 1.02), ("arms", 1.00), ("thighs", 1.03), ("calves", 1.02) },
            "UBE" => new (string, double)[] { ("chest", 1.06), ("waist", 0.97), ("pelvis", 1.03), ("legs", 1.02), ("shoulders", 1.01), ("breasts", 1.06), ("butt", 1.03), ("belly", 1.02), ("arms", 1.01), ("thighs", 1.03), ("calves", 1.02) },
            "HIMBO" => new (string, double)[] { ("chest", 1.10), ("waist", 1.02), ("pelvis", 1.04), ("legs", 1.06), ("shoulders", 1.12), ("breasts", 1.08), ("butt", 1.05), ("belly", 1.03), ("arms", 1.10), ("thighs", 1.07), ("calves", 1.05) },
            "SAM" => new (string, double)[] { ("chest", 1.08), ("waist", 1.01), ("pelvis", 1.03), ("legs", 1.05), ("shoulders", 1.10), ("breasts", 1.05), ("butt", 1.04), ("belly", 1.02), ("arms", 1.08), ("thighs", 1.06), ("calves", 1.04) },
            "SOS" => new (string, double)[] { ("chest", 1.05), ("waist", 1.00), ("pelvis", 1.02), ("legs", 1.04), ("shoulders", 1.06), ("breasts", 1.03), ("butt", 1.03), ("belly", 1.01), ("arms", 1.05), ("thighs", 1.04), ("calves", 1.03) },
            "VANILLA" => new (string, double)[] { ("chest", 1.00), ("waist", 1.00), ("pelvis", 1.00), ("legs", 1.00), ("shoulders", 1.00), ("breasts", 1.00), ("butt", 1.00), ("belly", 1.00), ("arms", 1.00), ("thighs", 1.00), ("calves", 1.00) },
            _ => []
        })
        {
            field[region] = value;
        }

        return field;
    }

    private static IReadOnlyDictionary<string, double> ApplyDeformationProfile(
        IReadOnlyDictionary<string, double> field,
        string? profile)
    {
        if (string.IsNullOrWhiteSpace(profile))
        {
            return field.ToDictionary(static pair => pair.Key, static pair => pair.Value, StringComparer.OrdinalIgnoreCase);
        }

        return DeformationProfileModifier.Apply(field, profile)
            .ToDictionary(static pair => pair.Key, static pair => Math.Round(pair.Value, 4), StringComparer.OrdinalIgnoreCase);
    }

    private static string[] ResolveSliderNames(string targetBody) => targetBody.Trim().ToUpperInvariant() switch
    {
        "CBBE" => ["Belly", "Butt", "BreastsShape", "BreastsSmall", "BreastsLarge", "WaistWidth", "HipWidth", "Thighs", "Calves", "Arms", "Shoulders", "NarrowWaist"],
        "3BA" => ["Belly", "Butt", "BreastsShape", "BreastsSmall", "BreastsLarge", "WaistWidth", "HipWidth", "Thighs", "Calves", "Arms", "Shoulders", "NarrowWaist", "BreastsPhysics", "ButtPhysics", "BellyPhysics"],
        "BHUNP" => ["Belly", "Butt", "BreastsShape", "BreastsSmall", "BreastsLarge", "WaistWidth", "HipWidth", "Thighs", "Calves", "Arms", "Shoulders", "NarrowWaist", "BreastsPhysics", "ButtPhysics"],
        "UNP" => ["Belly", "Butt", "BreastsShape", "BreastsSmall", "BreastsLarge", "WaistWidth", "HipWidth", "Thighs", "Calves", "Arms", "Shoulders"],
        "HIMBO" => ["Body", "Chest", "Waist", "Arms", "Legs", "Shoulders", "Butt", "Pecs"],
        "SAM" => ["Body", "Chest", "Waist", "Arms", "Legs", "Shoulders", "Butt"],
        "SOS" => ["Body", "Chest", "Waist", "Arms", "Legs", "Shoulders", "Butt"],
        "TBD" => ["Belly", "Butt", "BreastsShape", "BreastsSmall", "BreastsLarge", "WaistWidth", "HipWidth", "Thighs", "Calves"],
        "UBE" => ["Belly", "Butt", "BreastsShape", "WaistWidth", "HipWidth", "Thighs"],
        "VANILLA" => ["Belly", "Butt", "WaistWidth", "HipWidth", "Thighs"],
        _ => ["Belly", "Butt", "BreastsShape", "WaistWidth", "HipWidth"]
    };

    private static string ResolveBodyOutputPath(string targetBody) =>
        IsMaleBody(targetBody)
            ? @"meshes\actors\character\character assets male\"
            : @"meshes\actors\character\character assets\";

    private static bool IsMaleBody(string targetBody) =>
        targetBody.Equals("HIMBO", StringComparison.OrdinalIgnoreCase) ||
        targetBody.Equals("SAM", StringComparison.OrdinalIgnoreCase) ||
        targetBody.Equals("SOS", StringComparison.OrdinalIgnoreCase);

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

    private void PopulateArtifactsTab(IReadOnlyList<ConversionResult> results)
    {
        var files = results
            .SelectMany(result => result.OutputFiles)
            .Where(File.Exists)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        PopulateArtifactsTab(files, FindCommonDirectory(results.Select(result => result.OutputDirectory)));
    }

    private void PopulateArtifactsTab(string? outputDirectory)
    {
        if (string.IsNullOrWhiteSpace(outputDirectory) || !Directory.Exists(outputDirectory))
        {
            PopulateArtifactsTab([], null);
            return;
        }

        var files = Directory
            .EnumerateFiles(outputDirectory, "*", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        PopulateArtifactsTab(files, outputDirectory);
    }

    private void PopulateArtifactsTab(IReadOnlyList<string> files, string? baseDirectory)
    {
        _artifactsListView.BeginUpdate();
        try
        {
            _artifactsListView.Items.Clear();
            foreach (var file in files)
            {
                var displayPath = !string.IsNullOrWhiteSpace(baseDirectory)
                    ? Path.GetRelativePath(baseDirectory, file)
                    : file;
                var item = new ListViewItem([Path.GetFileName(file), displayPath]) { Tag = file };
                _artifactsListView.Items.Add(item);
            }
        }
        finally
        {
            _artifactsListView.EndUpdate();
        }

        _openArtifactButton.Enabled = _artifactsListView.SelectedItems.Count > 0;
    }

    private void PopulateReportsTab(IReadOnlyList<ConversionResult> results)
    {
        var outputDirectories = results
            .Select(result => result.OutputDirectory)
            .Where(static directory => !string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var files = outputDirectories
            .SelectMany(EnumerateKnownReportFiles)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        PopulateReportsTab(files, FindCommonDirectory(outputDirectories));
    }

    private void PopulateReportsTab(string? outputDirectory)
    {
        if (string.IsNullOrWhiteSpace(outputDirectory) || !Directory.Exists(outputDirectory))
        {
            PopulateReportsTab([], null);
            return;
        }

        PopulateReportsTab(
            EnumerateKnownReportFiles(outputDirectory)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            outputDirectory);
    }

    private void PopulateReportsTab(IReadOnlyList<string> files, string? baseDirectory)
    {
        _reportsListView.BeginUpdate();
        try
        {
            _reportsListView.Items.Clear();

            if (files.Count == 0)
            {
                _reportsListView.Items.Add(new ListViewItem(["Status", "Reports", "Run or load a conversion to inspect JSON diagnostics in-app."]));
                return;
            }

            foreach (var file in files)
            {
                var reportName = !string.IsNullOrWhiteSpace(baseDirectory)
                    ? Path.GetRelativePath(baseDirectory, file)
                    : Path.GetFileName(file);
                AppendReportSummary(reportName, file);
            }
        }
        finally
        {
            _reportsListView.EndUpdate();
        }

        _openReportButton.Enabled = _reportsListView.SelectedItems.Count > 0;
    }

    private void AppendReportSummary(string reportName, string filePath)
    {
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(filePath));
            var root = document.RootElement;
            var fileName = Path.GetFileName(filePath);

            switch (fileName)
            {
                case "batch-report.json":
                    AddReportMetric(reportName, "Target body", TryReadString(root, "TargetBody"), filePath);
                    AddReportMetric(reportName, "Conversion label", TryReadString(root, "ConversionLabel"), filePath);
                    AddReportMetric(reportName, "Total items", TryReadInt(root, "TotalCount"), filePath);
                    AddReportMetric(reportName, "Succeeded", TryReadInt(root, "SuccessCount"), filePath);
                    AddReportMetric(reportName, "Failed", TryReadInt(root, "FailedCount"), filePath);
                    break;
                case "conversion-quality.json":
                    AddReportMetric(reportName, "Source body", TryReadString(root, "DetectedSourceBody"), filePath);
                    AddReportMetric(reportName, "Target body", TryReadString(root, "TargetBody"), filePath);
                    AddReportMetric(reportName, "Mesh type", TryReadString(root, "MeshType"), filePath);
                    AddReportMetric(reportName, "Strategy", TryReadString(root, "Strategy"), filePath);
                    AddReportMetric(reportName, "Clipping detected", TryReadBool(root, "ClippingDetected"), filePath);
                    AddReportMetric(reportName, "Correction applied", TryReadBool(root, "CorrectionApplied"), filePath);
                    AddReportMetric(reportName, "Topology risk", TryReadBool(root, "TopologyMismatchRisk"), filePath);
                    AddReportMetric(reportName, "Quality warnings", TryReadArray(root, "QualityWarnings"), filePath);
                    break;
                case "dependency-map.json":
                    AddReportMetric(reportName, "Entries", CountElements(root), filePath);
                    AddReportMetric(reportName, "Detected bodies", DistinctArrayValues(root, "DetectedSourceBody"), filePath);
                    AddReportMetric(reportName, "Source skeletons", DistinctArrayValues(root, "SourceSkeleton"), filePath);
                    AddReportMetric(reportName, "Linked ARMA IDs", SumNestedArrayCounts(root, "LinkedArmaFormIds"), filePath);
                    break;
                case "skeleton-compatibility.json":
                    AddReportMetric(reportName, "Source skeleton", TryReadString(root, "SourceSkeleton"), filePath);
                    AddReportMetric(reportName, "Target skeleton", TryReadString(root, "TargetSkeleton"), filePath);
                    AddReportMetric(reportName, "Mapped bones", CountNestedArray(root, "BoneMappings"), filePath);
                    AddReportMetric(reportName, "Unsupported bones", TryReadArray(root, "UnsupportedBones"), filePath);
                    break;
                case "texture-summary.json":
                    AddReportMetric(reportName, "Textures", TryReadInt(root, "TotalCount"), filePath);
                    AddReportMetric(reportName, "Diffuse", CountNestedArray(root, "DiffuseFiles"), filePath);
                    AddReportMetric(reportName, "Normal", CountNestedArray(root, "NormalFiles"), filePath);
                    AddReportMetric(reportName, "Missing normals", CountNestedArray(root, "MissingNormals"), filePath);
                    AddReportMetric(reportName, "Glow", CountNestedArray(root, "GlowFiles"), filePath);
                    break;
                case "pose-simulation-report.json":
                    AddReportMetric(reportName, "Tested poses", CountNestedArray(root, "TestedPoses"), filePath);
                    AddReportMetric(reportName, "At-risk poses", TryReadInt(root, "TotalPosesAtRisk"), filePath);
                    AddReportMetric(reportName, "High-risk regions", TryReadArray(root, "HighRiskRegions"), filePath);
                    break;
                case "world-physics.json":
                    AddReportMetric(reportName, "Mode", TryReadString(root, "Mode"), filePath);
                    AddReportMetric(reportName, "Collision shape", TryReadString(root, "CollisionShape"), filePath);
                    AddReportMetric(reportName, "Source physics", TryReadBool(root, "SourcePhysicsDetected"), filePath);
                    AddReportMetric(reportName, "Ground mesh", TryReadBool(root, "GroundMeshAvailable"), filePath);
                    AddReportMetric(reportName, "Recommendations", TryReadArray(root, "Recommendations"), filePath);
                    break;
                case "plugin-patches.json":
                    AddReportMetric(reportName, "Plugins", CountNestedArray(root, "ScannedPlugins"), filePath);
                    AddReportMetric(reportName, "Armor add-ons", CountNestedArray(root, "ArmorAddons"), filePath);
                    AddReportMetric(reportName, "Rewrite mappings", CountNestedArray(root, "RewriteMappings"), filePath);
                    AddReportMetric(reportName, "Patch steps", CountNestedArray(root, "ProposedPatchSteps"), filePath);
                    break;
                default:
                    AddReportMetric(reportName, "Status", "Open this report for full details.", filePath);
                    break;
            }
        }
        catch (Exception ex)
        {
            AddReportMetric(reportName, "Status", $"Failed to read report: {ex.Message}", filePath);
        }
    }

    private void AddReportMetric(string reportName, string property, string? value, string filePath)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        _reportsListView.Items.Add(new ListViewItem([reportName, property, value]) { Tag = filePath });
    }

    private static IEnumerable<string> EnumerateKnownReportFiles(string outputDirectory) =>
        Directory
            .EnumerateFiles(outputDirectory, "*.json", SearchOption.AllDirectories)
            .Where(path => ReportFileNames.Contains(Path.GetFileName(path), StringComparer.OrdinalIgnoreCase));

    private static string? TryReadString(JsonElement element, string propertyName)
    {
        if (!TryGetProperty(element, propertyName, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.ToString(),
            JsonValueKind.True => "Yes",
            JsonValueKind.False => "No",
            _ => null,
        };
    }

    private static string? TryReadInt(JsonElement element, string propertyName) =>
        TryGetProperty(element, propertyName, out var value) && value.ValueKind == JsonValueKind.Number
            ? value.ToString()
            : null;

    private static string? TryReadBool(JsonElement element, string propertyName) =>
        TryGetProperty(element, propertyName, out var value) && (value.ValueKind == JsonValueKind.True || value.ValueKind == JsonValueKind.False)
            ? (value.GetBoolean() ? "Yes" : "No")
            : null;

    private static string? TryReadArray(JsonElement element, string propertyName)
    {
        if (!TryGetProperty(element, propertyName, out var value) || value.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var items = value
            .EnumerateArray()
            .Select(FormatJsonValue)
            .Where(static item => !string.IsNullOrWhiteSpace(item))
            .ToArray();
        if (items.Length == 0)
        {
            return "None";
        }

        const int previewCount = 4;
        return items.Length <= previewCount
            ? string.Join(", ", items)
            : $"{string.Join(", ", items.Take(previewCount))} (+{items.Length - previewCount} more)";
    }

    private static string CountNestedArray(JsonElement element, string propertyName) =>
        TryGetProperty(element, propertyName, out var value) && value.ValueKind == JsonValueKind.Array
            ? value.GetArrayLength().ToString()
            : "0";

    private static string CountElements(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Array => element.GetArrayLength().ToString(),
        JsonValueKind.Object => element.EnumerateObject().Count().ToString(),
        _ => "0",
    };

    private static string DistinctArrayValues(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Array)
        {
            return "None";
        }

        var values = element
            .EnumerateArray()
            .Select(item => TryReadString(item, propertyName))
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return values.Length == 0 ? "None" : string.Join(", ", values);
    }

    private static string SumNestedArrayCounts(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Array)
        {
            return "0";
        }

        var total = 0;
        foreach (var item in element.EnumerateArray())
        {
            if (TryGetProperty(item, propertyName, out var value) && value.ValueKind == JsonValueKind.Array)
            {
                total += value.GetArrayLength();
            }
        }

        return total.ToString();
    }

    private static string FormatJsonValue(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => value.GetString() ?? string.Empty,
        JsonValueKind.Number => value.ToString(),
        JsonValueKind.True => "Yes",
        JsonValueKind.False => "No",
        JsonValueKind.Object => $"{value.EnumerateObject().Count()} field(s)",
        JsonValueKind.Array => $"{value.GetArrayLength()} item(s)",
        _ => string.Empty,
    };

    private static bool TryGetProperty(JsonElement element, string propertyName, out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
            }
        }

        value = default;
        return false;
    }
}
