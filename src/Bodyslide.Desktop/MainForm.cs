using Bodyslide.Core;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Windows.Forms;

namespace Bodyslide.Desktop;

public sealed class MainForm : Form
{
    private static readonly string[] PreviewFileCandidates = ["preview-workbench.html", "preview.html"];

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
    private readonly ComboBox _themeComboBox;
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
    private readonly Button _openGuidanceTargetButton;
    private readonly Button _openArtifactButton;
    private readonly Button _loadCustomProfileButton;
    private readonly Button _saveProfileButton;
    private readonly Button _inspectCacheButton;
    private readonly Button _runSelfCheckButton;
    private readonly Button _openCustomProfileButton;
    private readonly Button _removeCustomProfileButton;
    private readonly Button _clearCustomProfilesButton;
    private readonly RadioButton _usePresetRadio;
    private readonly RadioButton _useCustomTargetRadio;
    private readonly CheckBox _outputZipCheckBox;
    private readonly CheckBox _buildSlidersCheckBox;
    private readonly Label _statusLabel;
    private readonly Label _presetDetailsLabel;
    private readonly Label _targetDetailsLabel;
    private readonly Label _sourceDetailsLabel;
    private readonly Label _physicsDetailsLabel;
    private readonly ProgressBar _progressBar;
    private readonly TabControl _resultsTabControl;
    private readonly TabPage _previewTabPage;
    private readonly Panel _previewPanel;
    private readonly Label _previewStatusLabel;
    private readonly TabPage _inspectTabPage;
    private readonly ListView _inspectListView;
    private readonly TabPage _summaryTabPage;
    private readonly ListView _summaryListView;
    private readonly TabPage _guidanceTabPage;
    private readonly ListView _guidanceListView;
    private readonly TabPage _reportsTabPage;
    private readonly ListView _reportsListView;
    private readonly TabPage _catalogTabPage;
    private readonly ListView _catalogListView;
    private readonly TabPage _readinessTabPage;
    private readonly ListView _readinessListView;
    private readonly TabPage _artifactsTabPage;
    private readonly ListView _artifactsListView;
    private readonly TabPage _cacheTabPage;
    private readonly ListView _cacheListView;
    private readonly ListView _customProfilesListView;
    private readonly BatchConversionRunner _batchRunner;
    private readonly ConversionInspector _inspector;
    private readonly ToolTip _optionToolTip;

    private CancellationTokenSource? _activeConversion;
    private string? _lastOutputDirectory;
    private string? _lastPreviewPath;
    private string? _lastBatchReportPath;
    private WebView2? _previewWebView;
    private readonly List<string> _customProfilePaths = [];
    private UiTheme _currentTheme;
    private static readonly string[] ReportFileNames =
    [
        "armor-pack-validation.json",
        "batch-report.json",
        "conversion-quality.json",
        "dependency-map.json",
        "skeleton-compatibility.json",
        "texture-summary.json",
        "pose-simulation-report.json",
        "world-physics.json",
        "plugin-patches.json",
    ];

    private enum UiTheme
    {
        Light,
        Dark,
    }

    private sealed record UiThemePalette(
        Color AppBackground,
        Color SurfaceBackground,
        Color InputBackground,
        Color Foreground,
        Color SecondaryForeground,
        Color Accent,
        Color Border,
        Color WarningBackground,
        Color WarningForeground);

    private sealed record GuidanceEntry(string Area, string Priority, string Guidance, string? TargetPath);

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
        _optionToolTip = new ToolTip
        {
            AutoPopDelay = 12000,
            InitialDelay = 300,
            ReshowDelay = 150,
            ShowAlways = true,
        };

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
        layout.Controls.Add(CreateSection("Quick import", dropPanel), 0, 0);

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
            Text = "Preset mode (recommended quick setup)",
            AutoSize = true,
            Checked = true,
        };
        _useCustomTargetRadio = new RadioButton
        {
            Text = "Manual mode (choose the destination body yourself)",
            AutoSize = true,
        };
        _usePresetRadio.CheckedChanged += (_, _) => RefreshModeState();
        _useCustomTargetRadio.CheckedChanged += (_, _) => RefreshModeState();
        modeRow.Controls.Add(_usePresetRadio);
        modeRow.Controls.Add(_useCustomTargetRadio);
        modeRow.Controls.Add(new Label
        {
            AutoSize = true,
            Margin = new Padding(12, 4, 0, 0),
            Text = "FROM body = what the original armor was built for. TO body = what you want the converted output to fit.",
        });
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
        var conversionGuideLabel = new Label
        {
            Text = "Use a preset when you want one named output setup. Presets choose the destination body, slider shape, and default output physics for you.",
            Anchor = AnchorStyles.Left,
            AutoSize = true,
            MaximumSize = new Size(420, 0),
        };
        leftOptions.Controls.Add(conversionGuideLabel, 0, 0);
        leftOptions.SetColumnSpan(conversionGuideLabel, 2);
        leftOptions.Controls.Add(new Label { Text = "Preset (destination body + shape)", Anchor = AnchorStyles.Left, AutoSize = true }, 0, 1);
        _presetComboBox = new ComboBox
        {
            Dock = DockStyle.Fill,
            DropDownStyle = ComboBoxStyle.DropDownList,
        };
        foreach (var preset in PresetCatalog.All.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase))
        {
            _presetComboBox.Items.Add(preset.Name);
        }

        _presetComboBox.SelectedIndexChanged += (_, _) =>
        {
            UpdatePresetDetails();
            UpdateTargetDetails();
            UpdatePhysicsDetails();
        };
        leftOptions.Controls.Add(_presetComboBox, 1, 1);
        leftOptions.Controls.Add(new Label { Text = "Preset batch list (optional)", Anchor = AnchorStyles.Left, AutoSize = true }, 0, 2);
        _presetBatchTextBox = new TextBox
        {
            Dock = DockStyle.Fill,
            PlaceholderText = "Example: 3BA Curvy, HIMBO Lean",
        };
        leftOptions.Controls.Add(_presetBatchTextBox, 1, 2);
        leftOptions.Controls.Add(new Label { Text = "To body / destination body", Anchor = AnchorStyles.Left, AutoSize = true }, 0, 3);
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
        _targetComboBox.SelectedIndexChanged += (_, _) =>
        {
            UpdateTargetDetails();
            UpdatePhysicsDetails();
        };
        _targetComboBox.TextChanged += (_, _) =>
        {
            UpdateTargetDetails();
            UpdatePhysicsDetails();
        };
        leftOptions.Controls.Add(_targetComboBox, 1, 3);
        leftOptions.Controls.Add(new Label { Text = "Destination body batch list (optional)", Anchor = AnchorStyles.Left, AutoSize = true }, 0, 4);
        _targetBatchTextBox = new TextBox
        {
            Dock = DockStyle.Fill,
            PlaceholderText = "Example: CBBE, 3BA, HIMBO",
        };
        leftOptions.Controls.Add(_targetBatchTextBox, 1, 4);
        leftOptions.Controls.Add(new Label(), 0, 5);
        var allBodiesButton = new Button
        {
            Text = "Convert to every supported body",
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 2, 0, 4),
        };
        allBodiesButton.Click += (_, _) =>
        {
            _useCustomTargetRadio.Checked = true;
            _targetBatchTextBox.Text = "all";
            AppendLog("Destination set to all supported body types.");
        };
        leftOptions.Controls.Add(allBodiesButton, 1, 5);
        leftOptions.Controls.Add(new Label { Text = "Preset details", Anchor = AnchorStyles.Left, AutoSize = true }, 0, 6);
        _presetDetailsLabel = new Label
        {
            Anchor = AnchorStyles.Left,
            AutoSize = true,
            MaximumSize = new Size(420, 0),
        };
        leftOptions.Controls.Add(_presetDetailsLabel, 1, 6);
        leftOptions.Controls.Add(new Label { Text = "Destination body details", Anchor = AnchorStyles.Left, AutoSize = true }, 0, 7);
        _targetDetailsLabel = new Label
        {
            Anchor = AnchorStyles.Left,
            AutoSize = true,
            MaximumSize = new Size(420, 0),
        };
        leftOptions.Controls.Add(_targetDetailsLabel, 1, 7);
        if (_presetComboBox.Items.Count > 0)
        {
            _presetComboBox.SelectedIndex = 0;
        }
        conversionOptionsPanel.Controls.Add(CreateSection("Destination setup (what you want to build)", leftOptions), 0, 0);

        var rightOptions = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            ColumnCount = 2,
        };
        rightOptions.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        rightOptions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        var overrideGuideLabel = new Label
        {
            Text = "These fields are optional hints or overrides. They help the converter understand the source armor or change the output behavior when auto-detection is not enough.",
            Anchor = AnchorStyles.Left,
            AutoSize = true,
            MaximumSize = new Size(420, 0),
        };
        rightOptions.Controls.Add(overrideGuideLabel, 0, 0);
        rightOptions.SetColumnSpan(overrideGuideLabel, 2);
        rightOptions.Controls.Add(new Label { Text = "Shape profile (optional)", Anchor = AnchorStyles.Left, AutoSize = true }, 0, 1);
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
        rightOptions.Controls.Add(_profileComboBox, 1, 1);

        rightOptions.Controls.Add(new Label { Text = "From body / source armor body (optional)", Anchor = AnchorStyles.Left, AutoSize = true }, 0, 2);
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
        _sourceComboBox.SelectedIndexChanged += (_, _) => UpdateSourceDetails();
        _sourceComboBox.TextChanged += (_, _) => UpdateSourceDetails();
        rightOptions.Controls.Add(_sourceComboBox, 1, 2);
        rightOptions.Controls.Add(new Label { Text = "Source body details", Anchor = AnchorStyles.Left, AutoSize = true }, 0, 3);
        _sourceDetailsLabel = new Label
        {
            Anchor = AnchorStyles.Left,
            AutoSize = true,
            MaximumSize = new Size(420, 0),
        };
        rightOptions.Controls.Add(_sourceDetailsLabel, 1, 3);

        rightOptions.Controls.Add(new Label { Text = "Physics for converted output (optional override)", Anchor = AnchorStyles.Left, AutoSize = true }, 0, 4);
        _physicsComboBox = new ComboBox
        {
            Dock = DockStyle.Fill,
            DropDownStyle = ComboBoxStyle.DropDownList,
        };
        _physicsComboBox.Items.Add("(auto)");
        foreach (var profile in PhysicsProfileCatalog.All)
        {
            _physicsComboBox.Items.Add(PhysicsProfileCatalog.ToDisplayName(profile));
        }
        _physicsComboBox.SelectedIndex = 0;
        _physicsComboBox.SelectedIndexChanged += (_, _) => UpdatePhysicsDetails();
        rightOptions.Controls.Add(_physicsComboBox, 1, 4);
        rightOptions.Controls.Add(new Label { Text = "Physics details", Anchor = AnchorStyles.Left, AutoSize = true }, 0, 5);
        _physicsDetailsLabel = new Label
        {
            Anchor = AnchorStyles.Left,
            AutoSize = true,
            MaximumSize = new Size(420, 0),
        };
        rightOptions.Controls.Add(_physicsDetailsLabel, 1, 5);

        rightOptions.Controls.Add(new Label { Text = "Dropped-item / world mesh mode (optional)", Anchor = AnchorStyles.Left, AutoSize = true }, 0, 6);
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
        rightOptions.Controls.Add(_worldModeComboBox, 1, 6);

        rightOptions.Controls.Add(new Label { Text = "Skeleton NIF for bone mapping (optional)", Anchor = AnchorStyles.Left, AutoSize = true }, 0, 7);
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
        rightOptions.Controls.Add(skeletonNifPanel, 1, 7);

        conversionOptionsPanel.Controls.Add(CreateSection("Source hints, output overrides, and support files", rightOptions), 1, 0);
        layout.Controls.Add(CreateSection("Conversion setup", conversionOptionsPanel), 0, 3);

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
        layout.Controls.Add(CreateSection("Custom profiles", customProfilesPanel), 0, 6);

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
            Text = "Package output as zip",
            AutoSize = true,
            Margin = new Padding(0, 8, 12, 0),
        };
        _buildSlidersCheckBox = new CheckBox
        {
            Text = "Generate BodySlide project files",
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
        _openGuidanceTargetButton = new Button
        {
            Text = "Open next action",
            Width = 125,
            Height = 34,
            Enabled = false,
            Margin = new Padding(0, 0, 8, 0),
        };
        _openGuidanceTargetButton.Click += async (_, _) => await OpenSelectedGuidanceTargetAsync();
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
        _runSelfCheckButton = new Button
        {
            Text = "Run self-check",
            Width = 120,
            Height = 34,
            Margin = new Padding(8, 0, 0, 0),
        };
        _runSelfCheckButton.Click += (_, _) => RunSelfCheck();
        var themeLabel = new Label
        {
            Text = "Theme",
            AutoSize = true,
            Margin = new Padding(16, 8, 4, 0),
        };
        _themeComboBox = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 110,
            Margin = new Padding(0, 4, 0, 0),
        };
        _themeComboBox.Items.Add(UiTheme.Light.ToString());
        _themeComboBox.Items.Add(UiTheme.Dark.ToString());
        _themeComboBox.SelectedIndexChanged += (_, _) => OnThemeSelectionChanged();
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
        actionRow.Controls.Add(_openGuidanceTargetButton);
        actionRow.Controls.Add(_openArtifactButton);
        actionRow.Controls.Add(_loadCustomProfileButton);
        actionRow.Controls.Add(_saveProfileButton);
        actionRow.Controls.Add(_inspectCacheButton);
        actionRow.Controls.Add(_runSelfCheckButton);
        actionRow.Controls.Add(themeLabel);
        actionRow.Controls.Add(_themeComboBox);
        layout.Controls.Add(CreateSection("Actions", actionRow), 0, 7);

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
        _guidanceTabPage = new TabPage("Next actions");
        _guidanceListView = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            HeaderStyle = ColumnHeaderStyle.Nonclickable,
            ShowItemToolTips = true,
        };
        _guidanceListView.Columns.Add("Area", 150);
        _guidanceListView.Columns.Add("Priority", 90);
        _guidanceListView.Columns.Add("Guidance", -2);
        _guidanceListView.SelectedIndexChanged += (_, _) => _openGuidanceTargetButton.Enabled = _guidanceListView.SelectedItems.Count > 0 &&
            _guidanceListView.SelectedItems[0].Tag is string targetPath &&
            (File.Exists(targetPath) || Directory.Exists(targetPath));
        _guidanceListView.DoubleClick += async (_, _) => await OpenSelectedGuidanceTargetAsync();
        _guidanceTabPage.Controls.Add(_guidanceListView);
        _resultsTabControl.TabPages.Add(_guidanceTabPage);
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
        _readinessTabPage = new TabPage("Readiness");
        _readinessListView = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            HeaderStyle = ColumnHeaderStyle.Nonclickable,
        };
        _readinessListView.Columns.Add("Area", 180);
        _readinessListView.Columns.Add("Status", 90);
        _readinessListView.Columns.Add("Details", -2);
        _readinessTabPage.Controls.Add(_readinessListView);
        _resultsTabControl.TabPages.Add(_readinessTabPage);
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
        layout.Controls.Add(CreateSection("Results and diagnostics", bottomPanel), 0, 8);

        RefreshModeState();
        UpdatePresetDetails();
        UpdateTargetDetails();
        UpdateSourceDetails();
        UpdatePhysicsDetails();
        PopulateCatalogTab();
        PopulateReadinessTab(CreateDesktopReadinessReport());
        PopulateGuidanceTab(Array.Empty<string>(), null);
        RefreshCustomProfilesList();
        UpdatePathActionStates();
        ClearInspectionTab("Select an input and click Inspect Input to preview body detection, mesh analysis, and skeleton compatibility.");
        PopulateReportsTab([], null);
        PopulateCacheTab([], null);
        ShowPreviewStatus("Run a conversion to render preview-workbench.html in-app.");
        ConfigureOptionTooltips();
        _currentTheme = LoadThemePreference();
        _themeComboBox.SelectedItem = _currentTheme.ToString();
        ApplyTheme(_currentTheme);
        AppendLog("Ready. Choose armor/clothing input, confirm FROM body (what the armor was made for) and TO body (what you want to build), then click Convert.");
    }

    internal string GetSmokeTestSummary()
    {
        return
            $"title=\"{Text}\", " +
            $"presets={_presetComboBox.Items.Count}, " +
            $"targets={_targetComboBox.Items.Count}, " +
            $"profiles={_profileComboBox.Items.Count}, " +
            $"physics={_physicsComboBox.Items.Count}, " +
            $"tabs={_resultsTabControl.TabPages.Count}";
    }

    private static GroupBox CreateSection(string title, Control content)
    {
        content.Dock = DockStyle.Fill;
        return new GroupBox
        {
            Text = title,
            Dock = DockStyle.Fill,
            Padding = new Padding(10),
            Margin = new Padding(0, 8, 0, 0),
            Controls = { content }
        };
    }

    private void OnThemeSelectionChanged()
    {
        if (_themeComboBox.SelectedItem is not string selectedTheme ||
            !Enum.TryParse<UiTheme>(selectedTheme, ignoreCase: true, out var theme))
        {
            return;
        }

        _currentTheme = theme;
        ApplyTheme(theme);
        SaveThemePreference(theme);
        AppendLog($"Theme switched to {theme} mode.");
    }

    private void ApplyTheme(UiTheme theme)
    {
        var palette = CreateThemePalette(theme);
        SuspendLayout();
        ApplyThemeToControl(this, palette);
        ResumeLayout(performLayout: true);
        Invalidate(true);
    }

    private static UiThemePalette CreateThemePalette(UiTheme theme) =>
        theme == UiTheme.Dark
            ? new UiThemePalette(
                AppBackground: Color.FromArgb(30, 34, 40),
                SurfaceBackground: Color.FromArgb(44, 49, 58),
                InputBackground: Color.FromArgb(22, 27, 34),
                Foreground: Color.FromArgb(236, 239, 244),
                SecondaryForeground: Color.FromArgb(185, 192, 203),
                Accent: Color.FromArgb(88, 166, 255),
                Border: Color.FromArgb(90, 98, 110),
                WarningBackground: Color.FromArgb(87, 63, 18),
                WarningForeground: Color.FromArgb(255, 235, 150))
            : new UiThemePalette(
                AppBackground: Color.FromArgb(244, 246, 249),
                SurfaceBackground: Color.White,
                InputBackground: Color.White,
                Foreground: Color.FromArgb(32, 37, 43),
                SecondaryForeground: Color.FromArgb(90, 98, 110),
                Accent: Color.FromArgb(0, 120, 215),
                Border: Color.FromArgb(201, 209, 217),
                WarningBackground: Color.FromArgb(255, 248, 196),
                WarningForeground: Color.FromArgb(120, 60, 0));

    private void ApplyThemeToControl(Control control, UiThemePalette palette)
    {
        switch (control)
        {
            case GroupBox:
            case TabPage:
                control.BackColor = palette.SurfaceBackground;
                control.ForeColor = palette.Foreground;
                break;
            case Form:
            case Panel:
                control.BackColor = palette.AppBackground;
                control.ForeColor = palette.Foreground;
                break;
            case Label label:
                label.BackColor = Color.Transparent;
                label.ForeColor = ReferenceEquals(label, _statusLabel) ||
                    ReferenceEquals(label, _presetDetailsLabel) ||
                    ReferenceEquals(label, _targetDetailsLabel) ||
                    ReferenceEquals(label, _sourceDetailsLabel) ||
                    ReferenceEquals(label, _physicsDetailsLabel)
                    ? palette.SecondaryForeground
                    : palette.Foreground;
                break;
            case TextBox textBox:
                textBox.BorderStyle = BorderStyle.FixedSingle;
                break;
            case ListView listView:
                listView.BackColor = palette.SurfaceBackground;
                listView.ForeColor = palette.Foreground;
                if (ReferenceEquals(listView, _guidanceListView))
                {
                    ApplyGuidanceItemStyles(palette);
                }
                break;
            case Button button:
                button.UseVisualStyleBackColor = false;
                button.FlatStyle = FlatStyle.Flat;
                button.FlatAppearance.BorderColor = palette.Border;
                button.FlatAppearance.MouseDownBackColor = BlendColors(palette.SurfaceBackground, palette.Accent, 0.35);
                button.FlatAppearance.MouseOverBackColor = BlendColors(palette.SurfaceBackground, palette.Accent, 0.18);
                button.BackColor = palette.SurfaceBackground;
                button.ForeColor = palette.Foreground;
                break;
            case CheckBox checkBox:
                checkBox.BackColor = Color.Transparent;
                checkBox.ForeColor = palette.Foreground;
                break;
            case RadioButton radioButton:
                radioButton.BackColor = Color.Transparent;
                radioButton.ForeColor = palette.Foreground;
                break;
            case ProgressBar:
                control.BackColor = palette.SurfaceBackground;
                control.ForeColor = palette.Accent;
                break;
            default:
                control.BackColor = palette.SurfaceBackground;
                control.ForeColor = palette.Foreground;
                break;
        }

        foreach (Control child in control.Controls)
        {
            ApplyThemeToControl(child, palette);
        }
    }

    private static Color BlendColors(Color background, Color accent, double amount)
    {
        amount = Math.Clamp(amount, 0d, 1d);
        return Color.FromArgb(
            (int)Math.Round(background.R + ((accent.R - background.R) * amount)),
            (int)Math.Round(background.G + ((accent.G - background.G) * amount)),
            (int)Math.Round(background.B + ((accent.B - background.B) * amount)));
    }

    private UiTheme LoadThemePreference()
    {
        try
        {
            var settingsPath = GetThemeSettingsPath();
            if (!File.Exists(settingsPath))
            {
                return UiTheme.Dark;
            }

            var json = File.ReadAllText(settingsPath);
            var settings = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            if (settings is not null &&
                settings.TryGetValue("theme", out var themeValue) &&
                Enum.TryParse<UiTheme>(themeValue, ignoreCase: true, out var theme))
            {
                return theme;
            }
        }
        catch
        {
        }

        return UiTheme.Dark;
    }

    private void SaveThemePreference(UiTheme theme)
    {
        try
        {
            var settingsPath = GetThemeSettingsPath();
            Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
            var json = JsonSerializer.Serialize(
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["theme"] = theme.ToString()
                },
                new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(settingsPath, json);
        }
        catch (Exception ex)
        {
            AppendLog($"Failed to save theme preference: {ex.Message}");
        }
    }

    private static string GetThemeSettingsPath() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SlideSmith",
            "ui-settings.json");

    private void PopulateCatalogTab()
    {
        _catalogListView.BeginUpdate();
        _catalogListView.Items.Clear();

        // ── Presets ──────────────────────────────────────────────────────────
        foreach (var preset in PresetCatalog.All.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase))
        {
            _catalogListView.Items.Add(new ListViewItem(
            [
                "Preset",
                preset.Name,
                $"Builds for {preset.TargetBody}; shape={preset.DeformationProfile}; output physics={PhysicsProfileCatalog.ToDisplayName(preset.PhysicsProfile)} [{preset.PhysicsProfile}]",
            ]));
        }

        // ── Bodies ───────────────────────────────────────────────────────────
        // Each body shows its default physics profile, skeleton foundation, and the
        // physics bones that are AVAILABLE if a non-none physics profile is applied.
        // Bodies with default physics "none" do NOT use soft-body physics by default;
        // their physics bones are only activated via the Physics override option.
        foreach (var body in BodyTypeCatalog.All.OrderBy(b => b.Name, StringComparer.OrdinalIgnoreCase))
        {
            var vertexRange = body.VertexCountMin > 0
                ? $"{body.VertexCountMin}-{body.VertexCountMax}"
                : "n/a";

            string details;
            if (BuiltInBodyMetadataCatalog.TryGet(body.Name, out var metadata) &&
                BodyTechnicalProfileCatalog.TryGet(body.Name, out var profile))
            {
                var aliases = metadata.Aliases.Count == 0
                    ? "none"
                    : string.Join(", ", metadata.Aliases);
                var physicsLabel = PhysicsProfileCatalog.ToDisplayName(profile.DefaultPhysics);
                var recommendedLabel = PhysicsProfileCatalog.ToDisplayName(profile.RecommendedPhysicsProfile);
                var bonesLabel = profile.SupportsPhysics
                    ? string.Join(", ", profile.RequiredPhysicsBones.Take(6)) + (profile.RequiredPhysicsBones.Count > 6 ? ", ..." : string.Empty)
                    : "none";
                var slidersLabel = metadata.SliderNames.Count == 0
                    ? "none listed"
                    : string.Join(", ", metadata.SliderNames.Take(6)) + (metadata.SliderNames.Count > 6 ? ", ..." : string.Empty);
                details = $"Gender={metadata.Gender}; Aliases={aliases}; Vertex range={vertexRange}; Skeleton={profile.SkeletonFoundation}; Default output physics={physicsLabel} [{profile.DefaultPhysics}]; Recommended override={recommendedLabel} [{profile.RecommendedPhysicsProfile}]; Physics bones={bonesLabel}; Example sliders={slidersLabel}; Notes={profile.Notes}";
            }
            else
            {
                details = $"Vertex range={vertexRange}; No built-in body notes available. You can still target it manually and apply any physics override.";
            }

            _catalogListView.Items.Add(new ListViewItem(["Body", body.Name, details]));
        }

        // ── Deformation profiles ─────────────────────────────────────────────
        foreach (var deformProfile in DeformationProfileModifier.All.OrderBy(static p => p, StringComparer.OrdinalIgnoreCase))
        {
            _catalogListView.Items.Add(new ListViewItem(["Deformation profile", deformProfile, ""]));
        }

        // ── Physics profiles ─────────────────────────────────────────────────
        // All profiles can be applied to ANY body via the Physics override dropdown.
        foreach (var physics in PhysicsProfileCatalog.All.OrderBy(static p => p, StringComparer.OrdinalIgnoreCase))
        {
            PhysicsProfileCatalog.Descriptions.TryGetValue(physics, out var physDesc);
            var displayName = PhysicsProfileCatalog.ToDisplayName(physics);
            var name = string.Equals(displayName, physics, StringComparison.OrdinalIgnoreCase)
                ? physics
                : $"{displayName} [{physics}]";
            _catalogListView.Items.Add(new ListViewItem(["Physics profile", name, physDesc ?? "Applies to the converted output, not to the original source armor."]));
        }

        // ── World drop modes ─────────────────────────────────────────────────
        foreach (var worldMode in WorldDropModeCatalog.All.OrderBy(static mode => mode, StringComparer.OrdinalIgnoreCase))
        {
            _catalogListView.Items.Add(new ListViewItem(["World drop mode", worldMode, "Controls world-physics.json mode output."]));
        }

        _catalogListView.Items.Add(new ListViewItem(["Target alias", "all / any / *", "Expands to every supported body type."]));
        _catalogListView.EndUpdate();
    }

    private IReadOnlyList<RuntimeReadinessCheck> CreateDesktopReadinessReport()
    {
        var checks = RuntimeReadinessReporter.CreateDesktopReport(Environment.ProcessPath).ToList();

        try
        {
            var version = CoreWebView2Environment.GetAvailableBrowserVersionString();
            checks.Add(new RuntimeReadinessCheck("Preview runtime", "OK", $"WebView2 runtime detected ({version})."));
        }
        catch (WebView2RuntimeNotFoundException)
        {
            checks.Add(new RuntimeReadinessCheck("Preview runtime", "Warning", "WebView2 runtime not found; preview-workbench.html (or preview.html fallback) will open in your default browser instead."));
        }
        catch (Exception ex)
        {
            checks.Add(new RuntimeReadinessCheck("Preview runtime", "Warning", $"WebView2 runtime probe failed: {ex.Message}"));
        }

        return checks;
    }

    private void PopulateReadinessTab(IReadOnlyList<RuntimeReadinessCheck> checks)
    {
        _readinessListView.BeginUpdate();
        _readinessListView.Items.Clear();

        foreach (var check in checks)
        {
            _readinessListView.Items.Add(new ListViewItem([check.Area, check.Status, check.Details]));
        }

        _readinessListView.EndUpdate();
    }

    private void RunSelfCheck()
    {
        var checks = CreateDesktopReadinessReport();
        PopulateReadinessTab(checks);
        _resultsTabControl.SelectedTab = _readinessTabPage;
        var summary = string.Join(", ", checks.Select(static check => $"{check.Status}:{check.Area}"));
        AppendLog($"Self-check completed — {summary}");
        _statusLabel.Text = "Readiness self-check completed.";
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

        UpdatePresetDetails();
        UpdateTargetDetails();
        UpdatePhysicsDetails();
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
            MessageBox.Show(this, "Please select at least one destination body (TO body).", "Missing destination body", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _activeConversion = new CancellationTokenSource();
        SetBusyState(isBusy: true);
        _progressBar.Style = ProgressBarStyle.Marquee;
        _progressBar.MarqueeAnimationSpeed = 30;
        _progressBar.Minimum = 0;
        _progressBar.Maximum = 100;
        _progressBar.Value = 0;
        _statusLabel.Text = $"Converting: {Path.GetFileName(input)}";
        AppendLog(usingPreset
            ? $"Starting conversion (presets: {string.Join(", ", selectedPresets)})..."
            : $"Starting conversion (destination bodies: {string.Join(", ", selectedTargets)})...");

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
            string? lastProgressLogMessage = null;
            var progress = new Progress<BatchProgressUpdate>(update =>
            {
                var total = Math.Max(1, update.Total);
                var completed = Math.Clamp(update.Completed, 0, total);
                double progressUnits = completed;
                if (!update.IsItemCompleted && update.StageCount > 0)
                {
                    var stageFraction = Math.Clamp((double)update.StageIndex / update.StageCount, 0d, 1d);
                    progressUnits = Math.Min(total, completed + stageFraction);
                }

                var percent = (int)Math.Round(progressUnits / total * 100d, MidpointRounding.AwayFromZero);
                var activeItem = update.IsItemCompleted
                    ? completed
                    : Math.Min(total, Math.Max(1, completed + 1));
                var statusSuffix = string.IsNullOrWhiteSpace(update.Stage)
                    ? update.CurrentFile
                    : $"{update.CurrentFile} — {update.Stage}";
                _progressBar.Style = ProgressBarStyle.Continuous;
                _progressBar.MarqueeAnimationSpeed = 0;
                _progressBar.Maximum = 100;
                _progressBar.Value = Math.Clamp(percent, 0, 100);
                _statusLabel.Text = $"Converting {activeItem}/{total} ({percent}%): {statusSuffix}";

                if (!string.IsNullOrWhiteSpace(update.Stage))
                {
                    var logMessage = $"Processing {activeItem}/{total}: {update.CurrentFile} — {update.Stage}";
                    if (!string.Equals(logMessage, lastProgressLogMessage, StringComparison.Ordinal))
                    {
                        AppendLog(logMessage);
                        lastProgressLogMessage = logMessage;
                    }
                }
            });

            var results = await Task.Run(
                () => _batchRunner.ConvertAsync(request, cancellationToken, progress),
                cancellationToken);
            _lastOutputDirectory = GetBestOutputDirectory(results);
            _lastPreviewPath = GetFirstExistingOutputFile(results, PreviewFileCandidates);
            _lastBatchReportPath = GetFirstExistingOutputFile(results, "batch-report.json");
            UpdatePathActionStates();
            _ = await LoadPreviewInAppAsync(_lastPreviewPath);
            PopulateSummaryTab(results);
            PopulateReportsTab(results);
            PopulateArtifactsTab(results);
            var guidanceNeedsReview = PopulateGuidanceTab(results, _lastPreviewPath);

            _resultsTabControl.SelectedTab = guidanceNeedsReview
                ? _guidanceTabPage
                : !string.IsNullOrWhiteSpace(_lastPreviewPath)
                    ? _previewTabPage
                    : _summaryTabPage;

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

            if (guidanceNeedsReview)
            {
                AppendLog("Review recommended next actions in the Next actions tab before installing or sharing the output.");
            }
            else if (!string.IsNullOrWhiteSpace(_lastPreviewPath))
            {
                AppendLog("Preview looks ready for review. Open the Preview tab for a final visual pass before installing or sharing.");
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
            ApplyDetectedSourceBodySelection(inspection.Detection);
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

    private void ApplyDetectedSourceBodySelection(BodyDetectionReport detection)
    {
        if (!IsSourceAutoSelection() ||
            string.IsNullOrWhiteSpace(detection.Body) ||
            detection.Body.Equals("CUSTOM", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var index = _sourceComboBox.FindStringExact(detection.Body);
        if (index < 0)
        {
            return;
        }

        _sourceComboBox.SelectedIndex = index;
        AppendLog($"Auto-selected source body from inspection: {detection.Body} ({detection.Confidence:P0}).");
    }

    private bool IsSourceAutoSelection() =>
        string.IsNullOrWhiteSpace(_sourceComboBox.Text) ||
        string.Equals(_sourceComboBox.Text, "(auto)", StringComparison.OrdinalIgnoreCase);

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
        _openGuidanceTargetButton.Enabled = !isBusy && _guidanceListView.SelectedItems.Count > 0 &&
            _guidanceListView.SelectedItems[0].Tag is string selectedGuidanceTarget &&
            (File.Exists(selectedGuidanceTarget) || Directory.Exists(selectedGuidanceTarget));
        _openArtifactButton.Enabled = !isBusy && _artifactsListView.SelectedItems.Count > 0;
        UseWaitCursor = isBusy;
        if (!isBusy)
        {
            _progressBar.Style = ProgressBarStyle.Continuous;
        }
        _progressBar.MarqueeAnimationSpeed = _progressBar.Style == ProgressBarStyle.Marquee ? 30 : 0;
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
            Description = "Select a previous SlideSmith output folder containing preview-workbench.html or preview.html",
            UseDescriptionForTitle = true,
        };

        if (folderDialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        var selectedFolder = folderDialog.SelectedPath;
        var previewPath = ResolvePreviewPath(selectedFolder);

        if (previewPath is null)
        {
            MessageBox.Show(
                this,
                $"No preview-workbench.html or preview.html was found in the selected folder.{Environment.NewLine}{selectedFolder}",
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
        var guidanceNeedsReview = PopulateGuidanceTab(selectedFolder, previewPath);
        _resultsTabControl.SelectedTab = guidanceNeedsReview ? _guidanceTabPage : _previewTabPage;
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
                else if (step.StartsWith("cross-gender-conversion:", StringComparison.Ordinal))
                    Add("Cross-gender conversion", step["cross-gender-conversion:".Length..]);
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

    private void ApplyGuidanceItemStyles(UiThemePalette palette)
    {
        foreach (ListViewItem item in _guidanceListView.Items)
        {
            var priority = item.SubItems.Count > 1 ? item.SubItems[1].Text : string.Empty;
            var severity = GetGuidancePriorityRank(priority);
            if (severity <= 0)
            {
                item.BackColor = palette.SurfaceBackground;
                item.ForeColor = palette.Foreground;
            }
            else if (severity >= 3)
            {
                item.BackColor = BlendColors(palette.WarningBackground, palette.Accent, 0.12);
                item.ForeColor = palette.WarningForeground;
            }
            else
            {
                item.BackColor = BlendColors(palette.WarningBackground, palette.SurfaceBackground, 0.35);
                item.ForeColor = palette.Foreground;
            }
        }
    }

    private bool PopulateGuidanceTab(IReadOnlyList<ConversionResult> results, string? previewPath)
    {
        var outputDirectories = results
            .Select(result => result.OutputDirectory)
            .Where(static directory => !string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return PopulateGuidanceTab(outputDirectories, previewPath);
    }

    private bool PopulateGuidanceTab(string? outputDirectory, string? previewPath)
    {
        if (string.IsNullOrWhiteSpace(outputDirectory) || !Directory.Exists(outputDirectory))
        {
            return PopulateGuidanceTab(Array.Empty<string>(), previewPath);
        }

        return PopulateGuidanceTab([outputDirectory], previewPath);
    }

    private bool PopulateGuidanceTab(IReadOnlyList<string> outputDirectories, string? previewPath)
    {
        var requiresReview = false;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var entries = new List<GuidanceEntry>();

        _guidanceListView.BeginUpdate();
        try
        {
            _guidanceListView.Items.Clear();

            void Add(string area, string priority, string guidance, string? targetPath = null)
            {
                if (string.IsNullOrWhiteSpace(guidance) ||
                    !seen.Add($"{area}|{priority}|{guidance}"))
                {
                    return;
                }

                entries.Add(new GuidanceEntry(area, priority, guidance, targetPath));
            }

            if (outputDirectories.Count == 0 && string.IsNullOrWhiteSpace(previewPath))
            {
                Add("Status", "Info", "Run or load a conversion to see preview guidance, warnings, and recommended next actions here.");
                return false;
            }

            if (!string.IsNullOrWhiteSpace(previewPath) && File.Exists(previewPath))
            {
                Add(
                    "Preview",
                    "Info",
                    "Open the Preview tab to visually inspect the converted mesh, then compare Summary and Reports before installing or sharing the output.",
                    previewPath);
            }
            else
            {
                requiresReview = true;
                Add(
                    "Preview",
                    "Warning",
                    "No preview-workbench.html or preview.html was found. Open the output folder and inspect conversion-quality.json and batch-report.json manually.",
                    outputDirectories.FirstOrDefault(static directory => !string.IsNullOrWhiteSpace(directory)));
            }

            foreach (var outputDirectory in outputDirectories)
            {
                AppendGuidanceFromConversionQuality(outputDirectory, previewPath, Add, ref requiresReview);
                AppendGuidanceFromPackValidation(outputDirectory, previewPath, Add, ref requiresReview);
                AppendGuidanceFromPluginPatches(outputDirectory, previewPath, Add, ref requiresReview);
                AppendGuidanceFromWorldPhysics(outputDirectory, previewPath, Add, ref requiresReview);
            }

            if (requiresReview &&
                !string.IsNullOrWhiteSpace(previewPath) &&
                File.Exists(previewPath))
            {
                Add(
                    "Review flow",
                    "Action",
                    "Start with the Preview tab for visual review, then work through the targeted report actions below before installing or sharing the output.",
                    previewPath);
            }

            if (entries.Count == 0)
            {
                Add("Status", "Info", "Run or load a conversion to see preview guidance, warnings, and recommended next actions here.");
            }

            foreach (var entry in entries
                         .OrderByDescending(entry => GetGuidancePriorityRank(entry.Priority))
                         .ThenBy(entry => entry.Area, StringComparer.OrdinalIgnoreCase)
                         .ThenBy(entry => entry.Guidance, StringComparer.OrdinalIgnoreCase))
            {
                var item = new ListViewItem([entry.Area, entry.Priority, entry.Guidance])
                {
                    Tag = entry.TargetPath,
                    ToolTipText = string.IsNullOrWhiteSpace(entry.TargetPath)
                        ? entry.Guidance
                        : $"{entry.Guidance}{Environment.NewLine}{entry.TargetPath}"
                };
                _guidanceListView.Items.Add(item);
            }

            ApplyGuidanceItemStyles(CreateThemePalette(_currentTheme));
        }
        finally
        {
            _guidanceListView.EndUpdate();
        }

        _openGuidanceTargetButton.Enabled = _guidanceListView.SelectedItems.Count > 0 &&
            _guidanceListView.SelectedItems[0].Tag is string selectedTargetPath &&
            (File.Exists(selectedTargetPath) || Directory.Exists(selectedTargetPath));

        return requiresReview;
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
            Add("FROM body (source selection)", IsSourceAutoSelection() ? "(auto-detect)" : _sourceComboBox.Text.Trim());
            if (!string.IsNullOrWhiteSpace(inspection.RequestedTargetBody))
            {
                Add("TO body (destination selection)", inspection.RequestedTargetBody);
            }

            Add("Detected body", $"{inspection.Detection.Body} ({inspection.Detection.Confidence:P1})");
            Add("Detection evidence", inspection.Detection.Evidence.Count == 0
                ? "None"
                : string.Join(", ", inspection.Detection.Evidence));
            if (BodyTechnicalProfileCatalog.TryGet(inspection.Detection.Body, out var detectedProfile))
            {
                Add("Detected skeleton base", detectedProfile.SkeletonFoundation);
                Add("SupportsPhysics", detectedProfile.SupportsPhysics ? "true" : "false");
                Add("Default physics", $"{PhysicsProfileCatalog.ToDisplayName(detectedProfile.DefaultPhysics)} [{detectedProfile.DefaultPhysics}]");
                Add("Recommended physics", $"{PhysicsProfileCatalog.ToDisplayName(detectedProfile.RecommendedPhysicsProfile)} [{detectedProfile.RecommendedPhysicsProfile}]");
                if (detectedProfile.SupportsPhysics)
                {
                    Add("Required physics bones", string.Join(", ", detectedProfile.RequiredPhysicsBones));
                }
                Add("Detected body notes", detectedProfile.Notes);
            }
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
            if (inspection.NifSupport is { Count: > 0 } nifSupport)
            {
                Add("NIF support", string.Join(", ",
                    nifSupport.Select(report => $"{Path.GetFileName(report.MeshPath)}={report.Status}/{report.ParseMode}")));
                var heelReports = nifSupport
                    .Where(static report => report.HeelAnalysis is not null)
                    .Select(report => $"{Path.GetFileName(report.MeshPath)}={report.HeelAnalysis!.Profile} ({report.HeelAnalysis.Confidence:P0})")
                    .ToList();
                if (heelReports.Count > 0)
                {
                    Add("Heel / footwear detection", string.Join(", ", heelReports));
                }
            }
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

            // Show the physics profile that will actually be used for the conversion.
            if (!string.IsNullOrWhiteSpace(inspection.RequestedTargetBody))
            {
                var effectivePhysics = ResolveEffectivePhysicsProfile(inspection.RequestedTargetBody);
                Add("Effective physics for target", $"{PhysicsProfileCatalog.ToDisplayName(effectivePhysics)} [{effectivePhysics}]" +
                    (string.Equals(effectivePhysics, "none", StringComparison.OrdinalIgnoreCase)
                        ? " — select a physics override above to enable soft-body output"
                        : " — soft-body bones will be injected into the converted mesh"));
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

    private async Task OpenSelectedGuidanceTargetAsync()
    {
        if (_guidanceListView.SelectedItems.Count == 0)
        {
            return;
        }

        if (_guidanceListView.SelectedItems[0].Tag is not string targetPath ||
            string.IsNullOrWhiteSpace(targetPath))
        {
            MessageBox.Show(this, "This next-action item does not have a direct file or folder to open.", "Open next action", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (Directory.Exists(targetPath))
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = targetPath,
                UseShellExecute = true,
            });
            return;
        }

        if (!File.Exists(targetPath))
        {
            MessageBox.Show(this, "The file for this next-action item was not found.", "Open next action", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            PopulateGuidanceTab(GetPreferredOutputDirectoryForOpen(), _lastPreviewPath);
            return;
        }

        if (PreviewFileCandidates.Contains(Path.GetFileName(targetPath), StringComparer.OrdinalIgnoreCase))
        {
            _lastPreviewPath = targetPath;
            UpdatePathActionStates();
            await LoadPreviewInAppAsync(targetPath);
            _resultsTabControl.SelectedTab = _previewTabPage;
            return;
        }

        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = targetPath,
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

    private static string? GetFirstExistingOutputFile(IReadOnlyList<ConversionResult> results, params string[] fileNames)
    {
        if (fileNames.Length == 0)
        {
            return null;
        }

        return results
            .SelectMany(r => r.OutputFiles)
            .Where(File.Exists)
            .OrderBy(path => GetPreviewCandidateRank(Path.GetFileName(path), fileNames))
            .FirstOrDefault(path =>
                fileNames.Any(fileName => Path.GetFileName(path).Equals(fileName, StringComparison.OrdinalIgnoreCase)));
    }

    private static int GetPreviewCandidateRank(string? fileName, IReadOnlyList<string> fileNames)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return int.MaxValue;
        }

        for (var index = 0; index < fileNames.Count; index++)
        {
            if (fileName.Equals(fileNames[index], StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return int.MaxValue;
    }

    private static string? ResolvePreviewPath(string folder)
    {
        foreach (var candidate in PreviewFileCandidates)
        {
            var path = Path.Combine(folder, candidate);
            if (File.Exists(path))
            {
                return path;
            }
        }

        return null;
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

        _presetDetailsLabel.Text =
            $"Preset output: build for {preset.TargetBody}, use the {preset.DeformationProfile} shape profile, and default to {PhysicsProfileCatalog.ToDisplayName(preset.PhysicsProfile)} output physics.";
    }

    private void UpdateTargetDetails()
    {
        var targetBody = BodyTypeCatalog.ResolveName(ResolveProfileTargetName());
        if (string.IsNullOrWhiteSpace(targetBody) || string.Equals(targetBody, "CUSTOM", StringComparison.OrdinalIgnoreCase))
        {
            _targetDetailsLabel.Text = "Type or select the body you want the converted armor to fit.";
            return;
        }

        _targetDetailsLabel.Text = BuildBodyDetailsText(
            targetBody,
            defaultText: $"This is the destination body the converted armor will be reshaped for.",
            isSourceContext: false);
    }

    private void UpdateSourceDetails()
    {
        var rawSource = string.IsNullOrWhiteSpace(_sourceComboBox.Text)
            ? _sourceComboBox.SelectedItem?.ToString()
            : _sourceComboBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(rawSource) ||
            string.Equals(rawSource, "(auto)", StringComparison.OrdinalIgnoreCase))
        {
            _sourceDetailsLabel.Text =
                "Auto means the app tries to detect what body the original armor was built for from meshes, plugins, and BodySlide support files. Choose a source body only if detection is wrong or the mod is unusual.";
            return;
        }

        var resolvedSource = BodyTypeCatalog.ResolveName(rawSource);
        _sourceDetailsLabel.Text =
            $"Source hint only: treat the original armor as built for {resolvedSource}. This does not change the destination body or output physics. " +
            BuildBodyDetailsText(resolvedSource, defaultText: string.Empty, isSourceContext: true);
    }

    private void UpdatePhysicsDetails()
    {
        var effectiveTarget = BodyTypeCatalog.ResolveName(ResolveProfileTargetName());
        var selectedPhysics = ReadOptionalComboValue(_physicsComboBox);
        if (PhysicsProfileCatalog.TryNormalize(selectedPhysics, out var normalizedOverride))
        {
            _physicsDetailsLabel.Text =
                $"Output physics override: the converted armor will use {PhysicsProfileCatalog.ToDisplayName(normalizedOverride)} regardless of the source armor. " +
                BuildPhysicsHelpSuffix(normalizedOverride, effectiveTarget);
            return;
        }

        var effectivePhysics = ResolveEffectivePhysicsProfile(effectiveTarget);
        _physicsDetailsLabel.Text =
            $"Auto output physics: the converter will use {PhysicsProfileCatalog.ToDisplayName(effectivePhysics)} based on the selected preset/body. " +
            "This setting controls the converted output, not the original source armor's physics. " +
            BuildPhysicsHelpSuffix(effectivePhysics, effectiveTarget);
    }

    private void ConfigureOptionTooltips()
    {
        _optionToolTip.SetToolTip(_usePresetRadio,
            "Recommended for most users. A preset picks the destination body, shape profile, and default output physics together.");
        _optionToolTip.SetToolTip(_useCustomTargetRadio,
            "Use this when you want to type or choose the destination body directly instead of starting from a preset.");
        _optionToolTip.SetToolTip(_presetComboBox,
            "Quick setup for the output you want. Presets do not describe the original source armor body.");
        _optionToolTip.SetToolTip(_presetBatchTextBox,
            "Optional comma-separated preset list for batch conversion. Example: 3BA Curvy, HIMBO Lean");
        _optionToolTip.SetToolTip(_targetComboBox,
            "The body you want the converted armor to fit. This is the destination/output body.");
        _optionToolTip.SetToolTip(_targetBatchTextBox,
            "Optional comma-separated destination body list for batch conversion. Use all to build every supported body.");
        _optionToolTip.SetToolTip(_profileComboBox,
            "Optional shape override for the converted output. Leave Auto unless you specifically want a different slider/deformation profile.");
        _optionToolTip.SetToolTip(_sourceComboBox,
            "What body the original armor was built for. Leave Auto unless detection gets it wrong. This does not choose the output body.");
        _optionToolTip.SetToolTip(_physicsComboBox,
            "Controls the converted output physics, not the source armor.\n" +
            "Auto = use the preset/body default.\n" +
            "None = no soft-body bones.\n" +
            "CBPC = CPU physics bones.\n" +
            "SMP = GPU cloth/soft-body bones.\n" +
            "Soft Body (CBPC + SMP) = combined setup.");
        _optionToolTip.SetToolTip(_worldModeComboBox,
            "Controls how dropped-item/world meshes are reported and packaged for the converted output.");
        _optionToolTip.SetToolTip(_skeletonNifTextBox,
            "Optional skeleton file used to improve bone mapping. Leave blank if the input mod already includes the right skeleton support.");
        _optionToolTip.SetToolTip(_outputZipCheckBox,
            "Create a ready-to-share zip package of the converted output.");
        _optionToolTip.SetToolTip(_buildSlidersCheckBox,
            "Generate BodySlide project files for the converted result so it can be rebuilt or adjusted later.");
    }

    private static string BuildBodyDetailsText(string bodyName, string defaultText, bool isSourceContext)
    {
        if (!BuiltInBodyMetadataCatalog.TryGet(bodyName, out var metadata) ||
            !BodyTechnicalProfileCatalog.TryGet(bodyName, out var profile))
        {
            return defaultText;
        }

        var aliases = metadata.Aliases.Count == 0
            ? string.Empty
            : $" Also known as {string.Join(", ", metadata.Aliases)}.";
        var physics = PhysicsProfileCatalog.ToDisplayName(profile.DefaultPhysics);
        var roleText = isSourceContext
            ? "Use this if the original armor was authored for this body family."
            : "Use this if you want the converted armor to fit this body family.";
        return $"{roleText} {metadata.Gender} body. Skeleton: {profile.SkeletonFoundation}. Default output physics: {physics}.{aliases} {metadata.Notes}".Trim();
    }

    private static string BuildPhysicsHelpSuffix(string physicsProfile, string targetBody)
    {
        if (!BodyTechnicalProfileCatalog.TryGet(targetBody, out var profile))
        {
            return "Any supported body can use any physics option.";
        }

        if (string.Equals(physicsProfile, "none", StringComparison.OrdinalIgnoreCase))
        {
            return "No extra soft-body bones will be injected into the converted meshes.";
        }

        return profile.SupportsPhysics
            ? $"Typical bones for {targetBody} include {string.Join(", ", profile.RequiredPhysicsBones.Take(4))}{(profile.RequiredPhysicsBones.Count > 4 ? ", ..." : string.Empty)}."
            : $"{targetBody} has no built-in body-specific physics-bone catalog, so this acts as a general output override.";
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
        _openGuidanceTargetButton.Enabled = _guidanceListView.SelectedItems.Count > 0 &&
            _guidanceListView.SelectedItems[0].Tag is string selectedGuidanceTarget &&
            (File.Exists(selectedGuidanceTarget) || Directory.Exists(selectedGuidanceTarget));
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

        var effectiveTarget = BodyTypeCatalog.ResolveName(ResolveProfileTargetName());
        if (string.IsNullOrWhiteSpace(effectiveTarget))
        {
            MessageBox.Show(this, "Select or type a target body before saving a profile.", "Save profile", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var effectivePhysics = ResolveEffectivePhysicsProfile(effectiveTarget);
        var effectiveProfile = ResolveEffectiveDeformationProfile();
        var baseField = CreateBaseTransformationField(effectiveTarget);
        var transformedField = ApplyDeformationProfile(baseField, effectiveProfile);
        BodyTypeCatalog.TryResolve(effectiveTarget, out var bodyInfo);
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
        BodyTypeCatalog.IsMaleBody(targetBody);

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
                    AddReportMetric(reportName, "Pack status", TryReadString(root, "PackReadinessStatus"), filePath);
                    AddReportMetric(reportName, "Avg validation score", TryReadString(root, "AverageValidationScore"), filePath);
                    AddReportMetric(reportName, "Ready", TryReadInt(root, "ReadyCount"), filePath);
                    AddReportMetric(reportName, "Needs review", TryReadInt(root, "NeedsReviewCount"), filePath);
                    AddReportMetric(reportName, "High risk", TryReadInt(root, "HighRiskCount"), filePath);
                    AddReportMetric(reportName, "Missing quality", TryReadInt(root, "MissingQualityReportCount"), filePath);
                    break;
                case "armor-pack-validation.json":
                    AddReportMetric(reportName, "Target body", TryReadString(root, "TargetBody"), filePath);
                    AddReportMetric(reportName, "Conversion label", TryReadString(root, "ConversionLabel"), filePath);
                    AddReportMetric(reportName, "Pack status", TryReadString(root, "PackReadinessStatus"), filePath);
                    AddReportMetric(reportName, "Items", TryReadInt(root, "TotalCount"), filePath);
                    AddReportMetric(reportName, "Quality reports", TryReadInt(root, "QualityReportCount"), filePath);
                    AddReportMetric(reportName, "Avg validation score", TryReadString(root, "AverageValidationScore"), filePath);
                    AddReportMetric(reportName, "Ready", TryReadInt(root, "ReadyCount"), filePath);
                    AddReportMetric(reportName, "Needs review", TryReadInt(root, "NeedsReviewCount"), filePath);
                    AddReportMetric(reportName, "High risk", TryReadInt(root, "HighRiskCount"), filePath);
                    break;
                case "conversion-quality.json":
                    AddReportMetric(reportName, "Source body", TryReadString(root, "DetectedSourceBody"), filePath);
                    AddReportMetric(reportName, "Target body", TryReadString(root, "TargetBody"), filePath);
                    AddReportMetric(reportName, "Mesh type", TryReadString(root, "MeshType"), filePath);
                    AddReportMetric(reportName, "Strategy", TryReadString(root, "Strategy"), filePath);
                    AddReportMetric(reportName, "Clipping detected", TryReadBool(root, "ClippingDetected"), filePath);
                    AddReportMetric(reportName, "Correction applied", TryReadBool(root, "CorrectionApplied"), filePath);
                    AddReportMetric(reportName, "Topology risk", TryReadBool(root, "TopologyMismatchRisk"), filePath);
                    AddReportMetric(reportName, "Validation status", TryReadNestedString(root, "ValidationSummary", "Status"), filePath);
                    AddReportMetric(reportName, "Validation score", TryReadNestedString(root, "ValidationSummary", "Score"), filePath);
                    AddReportMetric(reportName, "High-risk poses", TryReadInt(root, "HighRiskPoseCount"), filePath);
                    AddReportMetric(reportName, "Missing normals", TryReadInt(root, "MissingNormalCount"), filePath);
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

    private static string? TryReadNestedString(JsonElement element, string objectPropertyName, string nestedPropertyName) =>
        TryGetProperty(element, objectPropertyName, out var nested) && nested.ValueKind == JsonValueKind.Object
            ? TryReadString(nested, nestedPropertyName)
            : null;

    private static int? TryReadIntValue(JsonElement element, string propertyName) =>
        TryGetProperty(element, propertyName, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var result)
            ? result
            : null;

    private static int TryReadArrayCount(JsonElement element, string propertyName) =>
        TryGetProperty(element, propertyName, out var value) && value.ValueKind == JsonValueKind.Array
            ? value.GetArrayLength()
            : 0;

    private static ConversionValidationSummary? TryReadValidationSummary(JsonElement element)
    {
        if (!TryGetProperty(element, "ValidationSummary", out var summary) || summary.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var status = TryReadString(summary, "Status");
        if (string.IsNullOrWhiteSpace(status))
        {
            return null;
        }

        var issues = new List<ConversionValidationIssue>();
        if (TryGetProperty(summary, "Issues", out var issuesValue) && issuesValue.ValueKind == JsonValueKind.Array)
        {
            foreach (var issue in issuesValue.EnumerateArray())
            {
                var code = TryReadString(issue, "Code");
                var severity = TryReadString(issue, "Severity");
                var message = TryReadString(issue, "Message");
                if (string.IsNullOrWhiteSpace(code) ||
                    string.IsNullOrWhiteSpace(severity) ||
                    string.IsNullOrWhiteSpace(message))
                {
                    continue;
                }

                issues.Add(new ConversionValidationIssue(code, severity, message));
            }
        }

        return new ConversionValidationSummary(
            status,
            TryReadIntValue(summary, "Score") ?? 0,
            TryReadIntValue(summary, "HighSeverityCount") ?? 0,
            TryReadIntValue(summary, "MediumSeverityCount") ?? 0,
            TryReadIntValue(summary, "LowSeverityCount") ?? 0,
            issues);
    }

    private static void AppendGuidanceFromConversionQuality(
        string outputDirectory,
        string? previewPath,
        Action<string, string, string, string?> add,
        ref bool requiresReview)
    {
        var qualityPath = Path.Combine(outputDirectory, "conversion-quality.json");
        if (!File.Exists(qualityPath))
        {
            return;
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(qualityPath));
            var root = document.RootElement;
            var targetBody = TryReadString(root, "TargetBody") ?? "target body";
            var validationSummary = TryReadValidationSummary(root);
            if (validationSummary is null)
            {
                return;
            }

            if (!validationSummary.Status.Equals("READY", StringComparison.OrdinalIgnoreCase) ||
                validationSummary.Issues.Count > 0)
            {
                requiresReview = true;
            }

            add(
                "Validation",
                validationSummary.Status.Equals("READY", StringComparison.OrdinalIgnoreCase) ? "Info" : "Warning",
                $"Validation status: {validationSummary.Status} (score {validationSummary.Score}). Open conversion-quality.json for the full breakdown.",
                qualityPath);

            foreach (var issue in ConversionValidationGuidance.PrioritizeIssues(validationSummary, maxIssues: 3))
            {
                add(
                    GetGuidanceAreaForIssueCode(issue.Code),
                    ToDisplayPriority(issue.Severity),
                    issue.Message,
                    ResolveGuidanceTargetPath(outputDirectory, previewPath, issue.Code, qualityPath));
            }

            foreach (var issue in ConversionValidationGuidance.PrioritizeIssues(validationSummary, maxIssues: 4))
            {
                var action = ConversionValidationGuidance.GetIssueFollowUp(issue, targetBody);
                if (string.IsNullOrWhiteSpace(action))
                {
                    continue;
                }

                add(
                    $"{GetGuidanceAreaForIssueCode(issue.Code)} next step",
                    "Action",
                    action,
                    ResolveGuidanceTargetPath(outputDirectory, previewPath, issue.Code, qualityPath));
            }
        }
        catch (Exception ex)
        {
            requiresReview = true;
            add("Validation", "Warning", $"Could not read conversion-quality.json: {ex.Message}", qualityPath);
        }
    }

    private static void AppendGuidanceFromPackValidation(
        string outputDirectory,
        string? previewPath,
        Action<string, string, string, string?> add,
        ref bool requiresReview)
    {
        var reportPath = Path.Combine(outputDirectory, "armor-pack-validation.json");
        if (!File.Exists(reportPath))
        {
            return;
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(reportPath));
            var root = document.RootElement;
            var status = TryReadString(root, "PackReadinessStatus");
            var needsReviewCount = TryReadIntValue(root, "NeedsReviewCount") ?? 0;
            var highRiskCount = TryReadIntValue(root, "HighRiskCount") ?? 0;
            if (string.IsNullOrWhiteSpace(status))
            {
                return;
            }

            if (!status.Equals("READY", StringComparison.OrdinalIgnoreCase) ||
                needsReviewCount > 0 ||
                highRiskCount > 0)
            {
                requiresReview = true;
            }

            add(
                "Packaging",
                status.Equals("READY", StringComparison.OrdinalIgnoreCase) ? "Info" : "Warning",
                $"Pack readiness: {status}. Needs review: {needsReviewCount}. High risk: {highRiskCount}. Open armor-pack-validation.json before publishing or sharing.",
                reportPath);

            if (TryGetProperty(root, "TopIssueCodes", out var topIssueCodes) && topIssueCodes.ValueKind == JsonValueKind.Array)
            {
                foreach (var issue in topIssueCodes.EnumerateArray().Take(3))
                {
                    var code = TryReadString(issue, "Code");
                    var count = TryReadIntValue(issue, "Count") ?? 0;
                    var guidance = BuildPackIssueGuidance(code, count);
                    if (string.IsNullOrWhiteSpace(guidance))
                    {
                        continue;
                    }

                    add(
                        "Packaging review",
                        count > 0 ? "Action" : "Info",
                        guidance,
                        ResolveGuidanceTargetPath(outputDirectory, previewPath, code, reportPath));
                }
            }
        }
        catch (Exception ex)
        {
            requiresReview = true;
            add("Packaging", "Warning", $"Could not read armor-pack-validation.json: {ex.Message}", reportPath);
        }
    }

    private static void AppendGuidanceFromPluginPatches(
        string outputDirectory,
        string? previewPath,
        Action<string, string, string, string?> add,
        ref bool requiresReview)
    {
        var patchPath = Path.Combine(outputDirectory, "plugin-patches.json");
        if (!File.Exists(patchPath))
        {
            return;
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(patchPath));
            var root = document.RootElement;
            var patchSteps = TryReadArrayCount(root, "ProposedPatchSteps");
            var rewriteMappings = TryReadArrayCount(root, "RewriteMappings");
            if (patchSteps <= 0 && rewriteMappings <= 0)
            {
                return;
            }

            requiresReview = true;
            add(
                "Plugin patching",
                "Action",
                $"Open plugin-patches.json if the mod ships ESP/ESM/ESL files. Review {rewriteMappings} rewrite mapping(s) and {patchSteps} proposed patch step(s) in xEdit context before release.",
                ResolveGuidanceTargetPath(outputDirectory, previewPath, "plugin-rewrite-verification-warning", patchPath));

            if (TryGetProperty(root, "PluginInstallHints", out var pluginInstallHints) && pluginInstallHints.ValueKind == JsonValueKind.Array)
            {
                foreach (var hint in pluginInstallHints.EnumerateArray().Take(2))
                {
                    var sourcePlugin = TryReadString(hint, "SourcePlugin") ?? "plugin";
                    var generatedPatch = TryReadString(hint, "GeneratedPatchPlugin");
                    var manualReview = TryReadBool(hint, "ManualReviewRequired");
                    var loadAfter = TryReadArray(hint, "RecommendedPluginLoadAfter");
                    var placement = TryReadString(hint, "RecommendedModManagerPlacement");
                    var notes = TryReadArray(hint, "Notes");

                    if (!string.IsNullOrWhiteSpace(generatedPatch))
                    {
                        add(
                            "Plugin install",
                            "Action",
                            $"{generatedPatch} should load after {sourcePlugin}{(string.IsNullOrWhiteSpace(loadAfter) || string.Equals(loadAfter, "None", StringComparison.OrdinalIgnoreCase) ? string.Empty : $" ({loadAfter})")}. {placement}",
                            patchPath);
                    }

                    if (string.Equals(manualReview, "Yes", StringComparison.OrdinalIgnoreCase))
                    {
                        add(
                            "Plugin review",
                            "Warning",
                            $"{sourcePlugin} still needs manual xEdit review before release. {notes}",
                            patchPath);
                    }
                    else if (!string.IsNullOrWhiteSpace(notes) &&
                             !string.Equals(notes, "None", StringComparison.OrdinalIgnoreCase))
                    {
                        add("Plugin notes", "Info", $"{sourcePlugin}: {notes}", patchPath);
                    }
                }
            }

            if (TryGetProperty(root, "LinkedArmorFamilyReviewSteps", out var linkedReviewSteps) && linkedReviewSteps.ValueKind == JsonValueKind.Array)
            {
                foreach (var step in linkedReviewSteps.EnumerateArray().Take(2))
                {
                    var armorRecord = TryReadString(step, "ArmorRecord") ?? "linked armor family";
                    var sourcePlugin = TryReadString(step, "OwningPluginFileName") ?? "plugin";
                    var reason = TryReadString(step, "ManualReviewReason");
                    var action = TryReadString(step, "SuggestedXEditAction");
                    var priority = TryReadString(step, "ReviewPriority");

                    add(
                        "Linked armor family",
                        string.Equals(priority, "high", StringComparison.OrdinalIgnoreCase) ? "High" : "Action",
                        $"{armorRecord} ({sourcePlugin}): {action ?? reason ?? "Review linked ARMA members in xEdit before release."}",
                        patchPath);
                }
            }
        }
        catch (Exception ex)
        {
            requiresReview = true;
            add("Plugin patching", "Warning", $"Could not read plugin-patches.json: {ex.Message}", patchPath);
        }
    }

    private static void AppendGuidanceFromWorldPhysics(
        string outputDirectory,
        string? previewPath,
        Action<string, string, string, string?> add,
        ref bool requiresReview)
    {
        var reportPath = Path.Combine(outputDirectory, "world-physics.json");
        if (!File.Exists(reportPath))
        {
            return;
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(reportPath));
            var root = document.RootElement;
            var mode = TryReadString(root, "Mode");
            var groundMeshAvailable = TryReadBool(root, "GroundMeshAvailable");
            if (!string.IsNullOrWhiteSpace(mode))
            {
                add(
                    "World / ground mesh",
                    "Info",
                    $"World-object mode: {mode}. Ground mesh available: {groundMeshAvailable ?? "Unknown"}. Open world-physics.json if you need exact dropped-item recommendations.",
                    reportPath);
            }

            if (TryGetProperty(root, "Recommendations", out var recommendations) && recommendations.ValueKind == JsonValueKind.Array)
            {
                foreach (var recommendation in recommendations.EnumerateArray()
                             .Select(FormatJsonValue)
                             .Where(static value => !string.IsNullOrWhiteSpace(value))
                             .Take(3))
                {
                    var priority = recommendation.Contains("manually verify", StringComparison.OrdinalIgnoreCase) ||
                                   recommendation.Contains("fallback", StringComparison.OrdinalIgnoreCase)
                        ? "Action"
                        : "Info";
                    if (priority == "Action")
                    {
                        requiresReview = true;
                    }

                    add("World / ground mesh", priority, recommendation, reportPath);
                }
            }

            var heelProfile = TryReadNestedString(root, "HeelAnalysis", "Profile");
            if (heelProfile is "high-heel" or "raised-heel")
            {
                requiresReview = true;
                add(
                    "Footwear",
                    "High",
                    $"Detected {heelProfile} footwear. Validate heel height, toe angle, and ground contact in preview-workbench.html and world-physics.json before shipping.",
                    ResolveGuidanceTargetPath(outputDirectory, previewPath, "heel-offset-review", reportPath));
            }
        }
        catch (Exception ex)
        {
            requiresReview = true;
            add("World / ground mesh", "Warning", $"Could not read world-physics.json: {ex.Message}", reportPath);
        }
    }

    private static string GetGuidanceAreaForIssueCode(string? code)
    {
        var normalized = code?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return "Next action";
        }

        if (IsPreviewDrivenGuidanceCode(normalized))
        {
            return "Preview review";
        }

        if (normalized is "unsupported-bones")
        {
            return "Skeleton review";
        }

        if (normalized is "missing-normal-maps")
        {
            return "Texture review";
        }

        if (normalized is "incomplete-source-fallback" or "bodyslide-incompatible" ||
            normalized.StartsWith("missing-bodyslide-", StringComparison.OrdinalIgnoreCase))
        {
            return "BodySlide support";
        }

        if (normalized.StartsWith("plugin-", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("race-compatibility-warning", StringComparison.OrdinalIgnoreCase))
        {
            return "Plugin review";
        }

        if (normalized.StartsWith("zip-", StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith("missing-", StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith("fomod-", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("invalid-output-zip", StringComparison.OrdinalIgnoreCase))
        {
            return "Packaging review";
        }

        return "Next action";
    }

    private static string ResolveGuidanceTargetPath(
        string outputDirectory,
        string? previewPath,
        string? issueCode,
        string fallbackPath)
    {
        var normalized = issueCode?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return fallbackPath;
        }

        if (IsPreviewDrivenGuidanceCode(normalized) &&
            !string.IsNullOrWhiteSpace(previewPath) &&
            File.Exists(previewPath))
        {
            return previewPath;
        }

        if (normalized is "unsupported-bones")
        {
            return ResolveExistingGuidancePath(outputDirectory, "skeleton-compatibility.json") ?? fallbackPath;
        }

        if (normalized is "missing-normal-maps")
        {
            return ResolveExistingGuidancePath(outputDirectory, "texture-summary.json") ?? fallbackPath;
        }

        if (normalized.StartsWith("plugin-", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("race-compatibility-warning", StringComparison.OrdinalIgnoreCase))
        {
            return ResolveExistingGuidancePath(outputDirectory, "plugin-patches.json") ?? fallbackPath;
        }

        if (normalized is "missing-staged-cbpc-config")
        {
            return ResolveExistingGuidancePath(outputDirectory, Path.Combine("SKSE", "Plugins", "CBPCSystem", "cbpc-config.xml")) ?? fallbackPath;
        }

        if (normalized is "missing-staged-smp-config")
        {
            return ResolveExistingGuidancePath(outputDirectory, Path.Combine("SKSE", "Plugins", "hdtSMP64", "smp-config.xml")) ?? fallbackPath;
        }

        if (normalized is "bodyslide-incompatible" or "incomplete-source-fallback" ||
            normalized.StartsWith("missing-bodyslide-", StringComparison.OrdinalIgnoreCase))
        {
            return ResolveExistingGuidancePath(outputDirectory, Path.Combine("CalienteTools", "BodySlide")) ?? fallbackPath;
        }

        if (normalized.StartsWith("zip-missing-", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("missing-output-zip", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("invalid-output-zip", StringComparison.OrdinalIgnoreCase))
        {
            return Directory
                       .EnumerateFiles(outputDirectory, "*.zip", SearchOption.TopDirectoryOnly)
                       .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                       .FirstOrDefault()
                   ?? outputDirectory;
        }

        if (normalized.StartsWith("fomod-", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("missing-fomod-module-config", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("missing-fomod-info", StringComparison.OrdinalIgnoreCase))
        {
            return ResolveExistingGuidancePath(outputDirectory, Path.Combine("fomod", "ModuleConfig.xml"))
                ?? ResolveExistingGuidancePath(outputDirectory, Path.Combine("fomod", "info.xml"))
                ?? ResolveExistingGuidancePath(outputDirectory, "fomod")
                ?? fallbackPath;
        }

        if (normalized is "missing-staged-mesh-output" or "zip-missing-staged-mesh-output")
        {
            return ResolveExistingGuidancePath(outputDirectory, Path.Combine("meshes", "slidesmith")) ?? outputDirectory;
        }

        return fallbackPath;
    }

    private static bool IsPreviewDrivenGuidanceCode(string code) =>
        code.Equals("low-detection-confidence", StringComparison.OrdinalIgnoreCase) ||
        code.Equals("low-body-match", StringComparison.OrdinalIgnoreCase) ||
        code.Equals("unsupported-nif-layout", StringComparison.OrdinalIgnoreCase) ||
        code.Equals("heuristic-nif-read", StringComparison.OrdinalIgnoreCase) ||
        code.Equals("synthetic-morph-fallback", StringComparison.OrdinalIgnoreCase) ||
        code.Equals("retargeted-morph-reuse", StringComparison.OrdinalIgnoreCase) ||
        code.Equals("topology-mismatch-risk", StringComparison.OrdinalIgnoreCase) ||
        code.Equals("clipping-detected", StringComparison.OrdinalIgnoreCase) ||
        code.Equals("voxel-penetration", StringComparison.OrdinalIgnoreCase) ||
        code.Equals("pose-risk", StringComparison.OrdinalIgnoreCase) ||
        code.Equals("auto-correction-applied", StringComparison.OrdinalIgnoreCase) ||
        code.Equals("heel-offset-review", StringComparison.OrdinalIgnoreCase) ||
        code.Equals("plugin-link-unsupported-nif-layout", StringComparison.OrdinalIgnoreCase) ||
        code.Equals("missing-preview-workbench", StringComparison.OrdinalIgnoreCase) ||
        code.Equals("missing-preview-html", StringComparison.OrdinalIgnoreCase) ||
        code.Equals("missing-preview-svg", StringComparison.OrdinalIgnoreCase);

    private static string? ResolveExistingGuidancePath(string outputDirectory, string relativePath)
    {
        var fullPath = Path.Combine(outputDirectory, relativePath);
        if (File.Exists(fullPath) || Directory.Exists(fullPath))
        {
            return fullPath;
        }

        return null;
    }

    private static int GetGuidancePriorityRank(string priority) =>
        priority.Equals("High", StringComparison.OrdinalIgnoreCase) ? 4
        : priority.Equals("Warning", StringComparison.OrdinalIgnoreCase) ? 3
        : priority.Equals("Action", StringComparison.OrdinalIgnoreCase) ? 2
        : priority.Equals("Medium", StringComparison.OrdinalIgnoreCase) ? 2
        : priority.Equals("Low", StringComparison.OrdinalIgnoreCase) ? 1
        : 0;

    private static string ToDisplayPriority(string severity) =>
        severity.Equals("high", StringComparison.OrdinalIgnoreCase) ? "High"
        : severity.Equals("medium", StringComparison.OrdinalIgnoreCase) ? "Medium"
        : severity.Equals("low", StringComparison.OrdinalIgnoreCase) ? "Low"
        : "Info";

    private static string? BuildPackIssueGuidance(string? code, int count)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        var normalized = code.Trim();
        var issueCountLabel = count > 1 ? $" ({count} cases)" : string.Empty;
        var body = normalized.StartsWith("zip-missing-", StringComparison.OrdinalIgnoreCase)
            ? normalized["zip-missing-".Length..]
            : normalized.StartsWith("missing-", StringComparison.OrdinalIgnoreCase)
                ? normalized["missing-".Length..]
                : string.Empty;
        if (normalized.StartsWith("zip-missing-", StringComparison.OrdinalIgnoreCase))
        {
            return body.Equals("staged-mesh-output", StringComparison.OrdinalIgnoreCase)
                ? $"Rebuild the distributable zip and confirm it contains the full Data-relative meshes/slidesmith output that your mod manager is expected to install{issueCountLabel}."
                : body.Equals("plugin-patch-report", StringComparison.OrdinalIgnoreCase)
                    ? $"Rebuild the distributable zip and confirm it contains plugin-patches.json so plugin rewrite/load-order review survives outside the raw output folder{issueCountLabel}."
                    : $"Rebuild the distributable zip and confirm it contains {DescribePackArtifact(body)}{issueCountLabel}.";
        }

        if (normalized.StartsWith("missing-", StringComparison.OrdinalIgnoreCase))
        {
            return body.Equals("preview-workbench", StringComparison.OrdinalIgnoreCase)
                ? $"Regenerate preview-workbench.html and open it before publishing so the converted mesh can be visually reviewed outside the desktop app{issueCountLabel}."
                : body.Equals("bodyslide-reference-nif", StringComparison.OrdinalIgnoreCase)
                    ? $"Regenerate the BodySlide reference NIF inside CalienteTools/BodySlide/ShapeData before publishing so Outfit Studio and BodySlide can open the generated project correctly{issueCountLabel}."
                    : body.Equals("staged-mesh-output", StringComparison.OrdinalIgnoreCase)
                        ? $"Regenerate the Data-relative meshes/slidesmith output before publishing so installed/generated meshes match the validated conversion results{issueCountLabel}."
                        : body.Equals("output-zip", StringComparison.OrdinalIgnoreCase)
                            ? $"Regenerate the final distributable zip before publishing and confirm it matches the validated output folder contents{issueCountLabel}."
                            : $"Regenerate or copy {DescribePackArtifact(body)} into the output folder before publishing{issueCountLabel}.";
        }

        if (normalized.StartsWith("fomod-", StringComparison.OrdinalIgnoreCase))
        {
            return $"Open fomod/ModuleConfig.xml and fix the installer entries for {normalized["fomod-".Length..].Replace('-', ' ')}{issueCountLabel}.";
        }

        return $"Review armor-pack-validation.json for {normalized.Replace('-', ' ')}{issueCountLabel}.";
    }

    private static string DescribePackArtifact(string suffix) =>
        suffix.ToLowerInvariant() switch
        {
            "readme" => "README.txt",
            "dependency-map" => "dependency-map.json",
            "preview-html" => "preview.html",
            "preview-workbench" => "preview-workbench.html",
            "fomod-module-config" => "fomod/ModuleConfig.xml",
            "fomod-info" => "fomod/info.xml",
            "staged-mesh-output" => "the generated meshes/slidesmith output",
            "xedit-script" => "patch-armor.pas",
            "plugin-patch-report" => "plugin-patches.json",
            "output-zip" => "the final distributable zip",
            "bodyslide-osp" => "the generated BodySlide SliderSets .osp file",
            "bodyslide-shape-data" => "the generated BodySlide ShapeData payloads",
            "bodyslide-reference-nif" => "the BodySlide reference NIF",
            "bodyslide-slider-payload" => "the generated BSD/TRI slider payloads",
            "root-plugin" => "the copied/generated plugin file at the package root",
            "root-plugin-entry" => "the plugin root-file FOMOD entry",
            "staged-cbpc-config" => "the staged CBPC config",
            "staged-smp-config" => "the staged SMP config",
            _ => suffix.Replace('-', ' ')
        };

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
