using Bodyslide.Core;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.Versioning;
using System.Text;
using System.Text.Json;
using System.Windows.Forms;

namespace Bodyslide.Desktop;

public sealed class MainForm : Form
{
    private static readonly string[] PreviewFileCandidates = ["preview-workbench.html", "preview.html"];
    private const string CreatorSupportLink = "https://www.patreon.com/cw/DeadOnTheInside";
    private static readonly string[] CreatorSupportTooltips =
    [
        "Click to fund the sacred coffee ritual that keeps conversions alive.",
        "Support the creator: every click adds +1 morale and +3 bug-fixing stamina.",
        "Press the heart to cast 'Sustain Development' (duration: one billing cycle).",
        "This button converts spare change into fewer headaches. Allegedly.",
        "Feed the dev goblin so it patches bugs instead of eating keyboards.",
        "Click here to upgrade from 'works on my machine' to 'works for everyone.'",
        "Support button: because GPU fans run on love and monthly pledges.",
        "Tap the heart if you want more fixes and fewer cursed edge cases.",
        "Open Patreon and become an official sponsor of late-night debugging."
    ];
    private static readonly string[] ConversionPipelineStages =
    [
        "Importing input",
        "Analyzing textures",
        "Scanning plugins",
        "Checking plugin race compatibility",
        "Detecting source body",
        "Analyzing mesh",
        "Binding armor regions",
        "Building deformation cage",
        "Converting mesh",
        "Transferring weights",
        "Mapping skeleton",
        "Generating morphs",
        "Rebuilding partitions",
        "Detecting clipping",
        "Correcting mesh fit",
        "Running collision and pose checks",
        "Building physics and BodySlide data",
        "Exporting outputs"
    ];
    private static readonly JsonSerializerOptions ReportJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

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
    private readonly Button _copyCurrentViewButton;
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
    private readonly Button _copyMo2SetupButton;
    private readonly Button _openCustomProfileButton;
    private readonly Button _removeCustomProfileButton;
    private readonly Button _clearCustomProfilesButton;
    private readonly RadioButton _usePresetRadio;
    private readonly RadioButton _useCustomTargetRadio;
    private readonly CheckBox _showAdvancedOptionsCheckBox;
    private readonly CheckBox _outputZipCheckBox;
    private readonly CheckBox _buildSlidersCheckBox;
    private readonly Label _outputHintLabel;
    private readonly Label _statusLabel;
    private readonly Label _progressDetailsLabel;
    private readonly Label _modeStatusLabel;
    private readonly Label _bodySelectionSummaryLabel;
    private readonly Label _targetSelectionLabel;
    private readonly Label _targetModeHintLabel;
    private readonly Label _targetBatchLabel;
    private readonly Label _presetDetailsLabel;
    private readonly Label _targetDetailsLabel;
    private readonly Label _sourceDetailsLabel;
    private readonly Label _physicsDetailsLabel;
    private readonly Label _startupHandoffStatusValueLabel;
    private readonly Label _startupHandoffDetailsValueLabel;
    private readonly ProgressBar _progressBar;
    private readonly SplitContainer _mainSplitContainer;
    private readonly TabControl _resultsTabControl;
    private readonly TabPage _previewTabPage;
    private readonly Panel _previewPanel;
    private readonly Label _previewStatusLabel;
    private readonly TabPage _inspectTabPage;
    private readonly ListView _inspectListView;
    private readonly TabPage _summaryTabPage;
    private readonly ListView _summaryListView;
    private readonly TabPage _pipelineTabPage;
    private readonly ListView _pipelineListView;
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
    private readonly TableLayoutPanel _topLayoutPanel;
    private readonly TableLayoutPanel _conversionOptionsPanel;
    private readonly GroupBox _destinationSetupGroupBox;
    private readonly GroupBox _sourceHintsGroupBox;
    private readonly GroupBox _customProfilesGroupBox;
    private readonly TextBox _presetTargetTextBox;

    private CancellationTokenSource? _activeConversion;
    private CancellationTokenSource? _autoInspectDebounce;
    private CancellationTokenSource? _autoCacheInspectDebounce;
    private CancellationTokenSource? _activeCacheInspection;
    private string? _lastOutputDirectory;
    private string? _lastPreviewPath;
    private string? _lastBatchReportPath;
    private WebView2? _previewWebView;
    private readonly List<string> _customProfilePaths = [];
    private readonly DesktopLaunchOptions _launchOptions;
    private UiTheme _currentTheme;
    private string? _autoDetectedSourceBody;
    private double? _autoDetectedSourceConfidence;
    private bool _suppressThemeSelectionChanged;
    private bool _suppressTargetSelectionChanged;
    private bool _allowUserMainSplitOverride;
    private bool _startupResultLoadHandled;
    private bool _cacheInspectionInitialized;
    private DateTimeOffset? _conversionCancellationRequestedAtUtc;
    private bool _userAdjustedMainSplit;
    private bool? _usesSingleColumnConversionLayout;
    private int? _userPreferredMainSplitDistance;
    private int _lastCreatorTooltipIndex = -1;
    private readonly object _uiSettingsSaveSync = new();
    private DesktopUiSettings? _pendingUiSettingsSave;
    private Task? _uiSettingsSaveTask;
    private static readonly string[] ReportFileNames =
    [
        "armor-pack-validation.json",
        "batch-report.json",
        "conversion-quality.json",
        "dependency-map.json",
        "in-game-validation.json",
        "live-game-execution.json",
        "mod-stack-cross-validation.json",
        "proof-harness-bundle.json",
        "proof-result-bundle.json",
        "topology-correspondence.json",
        "runtime-validation-plan.json",
        "runtime-validation-harness.json",
        "desktop-workflow-automation.json",
        "windows-ui-e2e-automation.json",
        "skeleton-compatibility.json",
        "race-compatibility.json",
        "texture-summary.json",
        "pose-simulation-report.json",
        "world-physics.json",
        "plugin-patches.json",
        "support-coverage-signals.json",
        "runtime-stress-pass.json",
        "remaining-gaps-checklist.md",
        "remaining-gaps-pack-checklist.md",
    ];

    private const int MainSplitPreferredDistance = 560;
    private const int MainSplitPanel1Minimum = 360;
    private const int MainSplitPanel2Minimum = 220;
    private const int MaxLogCharacters = 120000;
    private const int TrimmedLogCharacters = 90000;
    private const int MaxLoggedStepsPerResult = 80;
    private const int MaxAutoCacheLogEntries = 20;
    private const int AutoInspectDebounceMilliseconds = 700;
    private const double AutoDetectedSourceConfidenceFloor = 0.75;
    private static readonly TimeSpan AutoInspectTimeout = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan PreviewLoadTimeout = TimeSpan.FromSeconds(8);
    private static readonly TimeSpan StartupOperationTimeout = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan ArchiveProgressUiRefreshInterval = TimeSpan.FromMilliseconds(1200);
    private static readonly TimeSpan ArchiveProgressLogInterval = TimeSpan.FromSeconds(20);
    private const long LargeInputStressThresholdBytes = 100L * 1024L * 1024L;

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
    private sealed record GuidanceBuildResult(IReadOnlyList<GuidanceEntry> Entries, bool RequiresReview, string GateStatus);
    private sealed record RuntimeStressPassReport(
        string InputPath,
        string InputType,
        long? InputSizeBytes,
        bool LargeInputDetected,
        int ProgressUiUpdateCount,
        double? AverageUiUpdateGapMilliseconds,
        double? MaxUiUpdateGapMilliseconds,
        double? CancellationLatencyMilliseconds,
        string? ArchiveFormat,
        int ArchiveProgressSampleCount,
        long? ArchiveBytesCopied,
        long? ArchiveBytesEstimated,
        double? ArchiveThroughputP10MiBPerSecond,
        double? ArchiveThroughputP50MiBPerSecond,
        double? ArchiveThroughputP90MiBPerSecond,
        double? ArchiveThroughputBaselineMiBPerSecond,
        string? ArchiveThroughputHardwareTier,
        int ArchiveThroughputBaselineSampleCount,
        bool ArchiveThroughputBelowBaseline,
        string Outcome,
        DateTimeOffset RecordedAtUtc);
    private sealed record ArchiveThroughputSample(
        string ArchiveFormat,
        string HardwareTier,
        double ThroughputMiBPerSecond,
        string Profile);
    private sealed record ArchiveThroughputBaselineResult(
        string ArchiveFormat,
        string HardwareTier,
        int SampleCount,
        double BaselineMiBPerSecond,
        IReadOnlyList<string> SampleProfiles);

    private static readonly ArchiveThroughputSample[] ArchiveThroughputSamples =
    [
        new("zip", "low", 5.2, "Ryzen 5 1600 + SATA SSD"),
        new("zip", "low", 5.8, "i5-7400 + HDD"),
        new("zip", "mid", 10.4, "Ryzen 5 3600 + SATA SSD"),
        new("zip", "mid", 11.2, "i5-12400 + SATA SSD"),
        new("zip", "high", 18.8, "Ryzen 7 5800X + NVMe Gen4"),
        new("zip", "high", 21.4, "i7-13700K + NVMe Gen4"),
        new("7z", "low", 1.4, "Ryzen 5 1600 + SATA SSD"),
        new("7z", "low", 1.8, "i5-7400 + HDD"),
        new("7z", "mid", 2.6, "Ryzen 5 3600 + SATA SSD"),
        new("7z", "mid", 3.1, "i5-12400 + SATA SSD"),
        new("7z", "high", 4.2, "Ryzen 7 5800X + NVMe Gen4"),
        new("7z", "high", 4.8, "i7-13700K + NVMe Gen4"),
        new("tar", "low", 13.1, "Ryzen 5 1600 + SATA SSD"),
        new("tar", "low", 12.4, "i5-7400 + HDD"),
        new("tar", "mid", 23.5, "Ryzen 5 3600 + SATA SSD"),
        new("tar", "mid", 24.7, "i5-12400 + SATA SSD"),
        new("tar", "high", 38.2, "Ryzen 7 5800X + NVMe Gen4"),
        new("tar", "high", 40.6, "i7-13700K + NVMe Gen4"),
        new("tar.gz", "low", 4.8, "Ryzen 5 1600 + SATA SSD"),
        new("tar.gz", "low", 4.4, "i5-7400 + HDD"),
        new("tar.gz", "mid", 7.9, "Ryzen 5 3600 + SATA SSD"),
        new("tar.gz", "mid", 8.6, "i5-12400 + SATA SSD"),
        new("tar.gz", "high", 13.5, "Ryzen 7 5800X + NVMe Gen4"),
        new("tar.gz", "high", 14.8, "i7-13700K + NVMe Gen4"),
    ];

    private sealed class CoalescingBatchProgress(Control owner, Action<BatchProgressUpdate> onUiThread) : IProgress<BatchProgressUpdate>, IDisposable
    {
        private readonly object _gate = new();
        private BatchProgressUpdate? _latest;
        private bool _scheduled;
        private bool _disposed;

        public void Report(BatchProgressUpdate value)
        {
            var shouldSchedule = false;
            lock (_gate)
            {
                if (_disposed)
                {
                    return;
                }

                _latest = value;
                if (!_scheduled)
                {
                    _scheduled = true;
                    shouldSchedule = true;
                }
            }

            if (!shouldSchedule)
            {
                return;
            }

            try
            {
                if (!owner.IsDisposed && owner.IsHandleCreated)
                {
                    owner.BeginInvoke((System.Windows.Forms.MethodInvoker)Flush);
                }
            }
            catch (InvalidOperationException)
            {
            }
        }

        private void Flush()
        {
            while (true)
            {
                BatchProgressUpdate? latest;
                lock (_gate)
                {
                    if (_disposed)
                    {
                        _scheduled = false;
                        return;
                    }

                    latest = _latest;
                    _latest = null;
                    if (latest is null)
                    {
                        _scheduled = false;
                        return;
                    }
                }

                onUiThread(latest);
            }
        }

        public void Dispose()
        {
            lock (_gate)
            {
                _disposed = true;
                _latest = null;
                _scheduled = false;
            }
        }
    }

    public MainForm()
        : this(null)
    {
    }

    internal MainForm(DesktopLaunchOptions? launchOptions)
    {
        _launchOptions = launchOptions ?? DesktopLaunchOptions.Empty;
        var appVersion = Assembly
            .GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion ?? "1.0";
        var stableVersion = appVersion.Split('+', 2)[0];
        Text = $"SlideSmith v{stableVersion}";
        Name = "mainForm";
        AutoScaleMode = AutoScaleMode.Dpi;
        Width = 1240;
        Height = 920;
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(980, 760);

        _batchRunner = new BatchConversionRunner(StandaloneConversionModules.CreateDefault());
        _inspector = StandaloneConversionModules.CreateInspector();
        _optionToolTip = new ToolTip
        {
            AutoPopDelay = 12000,
            InitialDelay = 300,
            ReshowDelay = 150,
            ShowAlways = true,
        };

        _mainSplitContainer = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            SplitterWidth = 8,
        };
        _mainSplitContainer.Panel1.AutoScroll = true;
        _mainSplitContainer.SizeChanged += (_, _) => UpdateMainSplitLayout();
        _mainSplitContainer.SplitterMoved += (_, _) =>
        {
            if (_allowUserMainSplitOverride)
            {
                _userAdjustedMainSplit = true;
                _userPreferredMainSplitDistance = _mainSplitContainer.SplitterDistance;
            }
        };
        Controls.Add(_mainSplitContainer);

        _topLayoutPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 1,
            RowCount = 8,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(12),
        };
        _topLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        _topLayoutPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _topLayoutPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _topLayoutPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _topLayoutPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _topLayoutPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _topLayoutPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _topLayoutPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _topLayoutPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _mainSplitContainer.Panel1.Controls.Add(_topLayoutPanel);

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
            Text = "Drag and drop a .nif, plugin (.esp/.esm/.esl), archive (.zip/.7z/.rar/.tar/.tar.gz/.tgz), or armor folder here",
            AllowDrop = true,
        };
        dropLabel.DragEnter += OnDragEnter;
        dropLabel.DragDrop += OnDragDrop;
        dropPanel.Controls.Add(dropLabel);
        var startupHandoffPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 2,
            RowCount = 2,
            AutoSize = true,
            Margin = new Padding(0, 6, 0, 0),
            Padding = new Padding(6),
        };
        startupHandoffPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        startupHandoffPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        startupHandoffPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        startupHandoffPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        startupHandoffPanel.Controls.Add(new Label { Text = "Status", AutoSize = true, Margin = new Padding(0, 0, 8, 0) }, 0, 0);
        _startupHandoffStatusValueLabel = new Label
        {
            Text = "Direct launch",
            AutoSize = true,
            Dock = DockStyle.Fill
        };
        startupHandoffPanel.Controls.Add(_startupHandoffStatusValueLabel, 1, 0);
        startupHandoffPanel.Controls.Add(new Label { Text = "Details", AutoSize = true, Margin = new Padding(0, 4, 8, 0) }, 0, 1);
        _startupHandoffDetailsValueLabel = new Label
        {
            Text = "No launcher handoff diagnostics were provided.",
            AutoEllipsis = true,
            MaximumSize = new Size(980, 0),
            AutoSize = true,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 4, 0, 0)
        };
        startupHandoffPanel.Controls.Add(_startupHandoffDetailsValueLabel, 1, 1);

        var quickImportPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 1,
            RowCount = 2,
            AutoSize = true,
        };
        quickImportPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        quickImportPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        quickImportPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        quickImportPanel.Controls.Add(dropPanel, 0, 0);
        quickImportPanel.Controls.Add(startupHandoffPanel, 0, 1);

        _topLayoutPanel.Controls.Add(CreateAutoSizeSection("Quick import", quickImportPanel), 0, 0);

        var inputRow = CreateThreeColumnRow("Input", out _inputTextBox);
        _inputTextBox.Name = "inputPathTextBox";
        _inputTextBox.PlaceholderText = "Select armor input (.nif/.esp/.esm/.esl/.zip/.7z/.rar/.tar) or an armor folder";
        _inputTextBox.AllowDrop = true;
        _inputTextBox.DragEnter += OnDragEnter;
        _inputTextBox.DragDrop += OnDragDrop;
        _inputTextBox.TextChanged += (_, _) =>
        {
            UpdatePathActionStates();
            var inputPath = _inputTextBox.Text.Trim();
            ClearInspectionTab(ShouldAutoInspectInputPath(inputPath)
                ? "Input changed. Auto-inspecting detection and compatibility details..."
                : "Input changed. Auto-inspection is limited to local .nif/.esp/.esm/.esl files; use “Inspect input now” for folders or archives.");
            UpdateOutputHint();
            ScheduleAutoInspectInput();
        };
        var browseInputFileButton = new Button { Name = "browseInputFileButton", Text = "File...", AutoSize = true };
        browseInputFileButton.Click += (_, _) => BrowseInputFile();
        var browseInputFolderButton = new Button { Name = "browseInputFolderButton", Text = "Folder...", AutoSize = true, Margin = new Padding(4, 0, 0, 0) };
        browseInputFolderButton.Click += (_, _) => BrowseInputFolder();
        _inspectInputButton = new Button
        {
            Name = "inspectInputButton",
            Text = "Inspect input now",
            AutoSize = true,
            Enabled = false,
            Margin = new Padding(6, 0, 0, 0),
        };
        _inspectInputButton.Click += async (_, _) => await InspectInputAsync(showDialogs: true, switchToInspectTab: true, automaticTrigger: false);
        _openInputButton = new Button
        {
            Name = "openInputButton",
            Text = "Open",
            AutoSize = true,
            Enabled = false,
            Margin = new Padding(6, 0, 0, 0),
        };
        _openInputButton.Click += (_, _) => OpenInputPath();
        var inputActions = new FlowLayoutPanel
        {
            AutoSize = true,
            WrapContents = true,
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

        var modeRow = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 2,
            AutoSize = true,
            Margin = new Padding(0, 6, 0, 0),
        };
        modeRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        modeRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        modeRow.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        modeRow.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        modeRow.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        modeRow.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        modeRow.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var modeHeaderRow = new TableLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            Margin = new Padding(0, 0, 0, 6),
        };
        modeHeaderRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        modeHeaderRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        var modeHeaderLabel = new Label
        {
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 6),
            Text = "Step 1: choose a quick preset or switch to manual mode if you want to pick the TO body yourself."
        };
        modeHeaderRow.Controls.Add(modeHeaderLabel, 0, 0);
        var creatorSupportButton = new Button
        {
            Name = "creatorSupportButton",
            Text = "❤ Support creator",
            AutoSize = true,
            MinimumSize = new Size(170, 34),
            Font = new Font(Font, FontStyle.Bold),
            Margin = new Padding(8, 0, 0, 0),
        };
        creatorSupportButton.Click += (_, _) => OpenCreatorSupportLink();
        creatorSupportButton.MouseEnter += (_, _) =>
            _optionToolTip.SetToolTip(creatorSupportButton, GetRandomCreatorSupportTooltip());
        modeHeaderRow.Controls.Add(creatorSupportButton, 1, 0);
        modeRow.Controls.Add(modeHeaderRow, 0, 0);
        modeRow.SetColumnSpan(modeHeaderRow, 2);
        var modeSelectorPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            WrapContents = true,
            Margin = new Padding(0),
        };
        _usePresetRadio = new RadioButton
        {
            Text = "Quick preset mode (recommended)",
            AutoSize = true,
            Checked = false,
        };
        _useCustomTargetRadio = new RadioButton
        {
            Text = "Manual mode (I will choose the TO body)",
            AutoSize = true,
            Checked = true,
        };
        _usePresetRadio.CheckedChanged += (_, _) =>
        {
            if (_usePresetRadio.Checked)
            {
                RefreshModeState();
            }
        };
        _useCustomTargetRadio.CheckedChanged += (_, _) =>
        {
            if (_useCustomTargetRadio.Checked)
            {
                RefreshModeState();
            }
        };
        modeSelectorPanel.Controls.Add(_usePresetRadio);
        modeSelectorPanel.Controls.Add(_useCustomTargetRadio);
        modeRow.Controls.Add(modeSelectorPanel, 0, 1);
        var themePanel = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(12, 0, 0, 0),
        };
        var themeLabel = new Label
        {
            Text = "Theme",
            AutoSize = true,
            Margin = new Padding(0, 8, 4, 0),
        };
        _themeComboBox = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 120,
            Margin = new Padding(0, 4, 0, 0),
        };
        _themeComboBox.Items.Add(UiTheme.Light.ToString());
        _themeComboBox.Items.Add(UiTheme.Dark.ToString());
        _themeComboBox.SelectedIndexChanged += (_, _) => OnThemeSelectionChanged();
        themePanel.Controls.Add(themeLabel);
        themePanel.Controls.Add(_themeComboBox);
        modeRow.Controls.Add(themePanel, 1, 1);
        var modeQuickActionsPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            WrapContents = true,
            Margin = new Padding(0, 4, 0, 0),
        };
        var useRecommendedSetupButton = new Button
        {
            Name = "useRecommendedSetupButton",
            Text = "Use recommended setup",
            AutoSize = true,
            Margin = new Padding(0, 0, 8, 0),
        };
        useRecommendedSetupButton.Click += (_, _) => ApplyRecommendedUiSetup();
        var autoMapTargetButton = new Button
        {
            Name = "autoMapTargetButton",
            Text = "Auto-map TO body from FROM body",
            AutoSize = true,
            Margin = new Padding(0),
        };
        autoMapTargetButton.Click += (_, _) => ApplyAutoMappedTargetFromSource();
        modeQuickActionsPanel.Controls.Add(useRecommendedSetupButton);
        modeQuickActionsPanel.Controls.Add(autoMapTargetButton);
        modeRow.Controls.Add(modeQuickActionsPanel, 0, 2);
        modeRow.SetColumnSpan(modeQuickActionsPanel, 2);
        _showAdvancedOptionsCheckBox = new CheckBox
        {
            Name = "showAdvancedOptionsCheckBox",
            Text = "Show advanced options (source overrides, custom profiles, support tuning)",
            AutoSize = true,
            Checked = false,
            Margin = new Padding(0, 4, 0, 0),
        };
        _showAdvancedOptionsCheckBox.CheckedChanged += (_, _) => ApplyAdvancedOptionsVisibility();
        modeRow.Controls.Add(_showAdvancedOptionsCheckBox, 0, 3);
        modeRow.SetColumnSpan(_showAdvancedOptionsCheckBox, 2);
        _modeStatusLabel = new Label
        {
            AutoSize = true,
            Margin = new Padding(0, 6, 0, 0),
            Text = "FROM body = what the original armor was built for. TO body = what you want the converted output to fit.",
        };
        modeRow.Controls.Add(_modeStatusLabel, 0, 4);
        modeRow.SetColumnSpan(_modeStatusLabel, 2);
        _bodySelectionSummaryLabel = new Label
        {
            AutoSize = true,
            Margin = new Padding(0, 8, 0, 0),
            Padding = new Padding(8, 6, 8, 6),
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font(Font, FontStyle.Bold),
            Text = "FROM body: (auto)  →  TO body: (not selected)",
        };
        modeRow.Controls.Add(_bodySelectionSummaryLabel, 0, 5);
        modeRow.SetColumnSpan(_bodySelectionSummaryLabel, 2);
        _topLayoutPanel.Controls.Add(modeRow, 0, 2);

        _conversionOptionsPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 2,
            AutoSize = true,
            Margin = new Padding(0, 6, 0, 0),
        };
        _conversionOptionsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
        _conversionOptionsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));

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
            Text = "Use a preset when you want one named output setup. Preset mode locks the TO body automatically. Switch to Manual mode only when you want to choose one or more TO bodies yourself.",
            Anchor = AnchorStyles.Left,
            AutoSize = true,
            MaximumSize = new Size(420, 0),
        };
        leftOptions.Controls.Add(conversionGuideLabel, 0, 0);
        leftOptions.SetColumnSpan(conversionGuideLabel, 2);
        leftOptions.Controls.Add(new Label { Text = "Preset (destination body + shape)", Anchor = AnchorStyles.Left, AutoSize = true }, 0, 1);
        _presetComboBox = new ComboBox
        {
            Name = "presetBodyComboBox",
            Dock = DockStyle.Fill,
            DropDownStyle = ComboBoxStyle.DropDownList,
            MinimumSize = new Size(260, 0),
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
            UpdateOutputHint();
            UpdateBodySelectionSummary();
        };
        leftOptions.Controls.Add(_presetComboBox, 1, 1);
        leftOptions.Controls.Add(new Label { Text = "Preset batch list (optional)", Anchor = AnchorStyles.Left, AutoSize = true }, 0, 2);
        _presetBatchTextBox = new TextBox
        {
            Name = "presetBatchTextBox",
            Dock = DockStyle.Fill,
            PlaceholderText = "Example: 3BA Curvy, HIMBO Lean",
        };
        leftOptions.Controls.Add(_presetBatchTextBox, 1, 2);
        _targetSelectionLabel = new Label
        {
            Text = "TO body from preset",
            Anchor = AnchorStyles.Left,
            AutoSize = true,
        };
        leftOptions.Controls.Add(_targetSelectionLabel, 0, 3);
        var targetSelectorPanel = new Panel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            Margin = new Padding(0),
            Padding = new Padding(0),
        };
        _presetTargetTextBox = new TextBox
        {
            Name = "presetTargetBodyTextBox",
            Dock = DockStyle.Fill,
            ReadOnly = true,
            TabStop = false,
        };
        _targetComboBox = new ComboBox
        {
            Name = "targetBodyComboBox",
            Dock = DockStyle.Fill,
            DropDownStyle = ComboBoxStyle.DropDown,
            AutoCompleteMode = AutoCompleteMode.SuggestAppend,
            AutoCompleteSource = AutoCompleteSource.ListItems,
            MinimumSize = new Size(260, 0),
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
            if (_suppressTargetSelectionChanged)
            {
                return;
            }

            UpdateTargetDetails();
            UpdatePhysicsDetails();
            UpdateOutputHint();
            UpdateBodySelectionSummary();
        };
        _targetComboBox.TextChanged += (_, _) =>
        {
            if (_suppressTargetSelectionChanged)
            {
                return;
            }

            UpdateTargetDetails();
            UpdatePhysicsDetails();
            UpdateOutputHint();
            UpdateBodySelectionSummary();
        };
        targetSelectorPanel.Controls.Add(_presetTargetTextBox);
        targetSelectorPanel.Controls.Add(_targetComboBox);
        leftOptions.Controls.Add(targetSelectorPanel, 1, 3);
        _targetModeHintLabel = new Label
        {
            Anchor = AnchorStyles.Left,
            AutoSize = true,
            MaximumSize = new Size(420, 0),
            Margin = new Padding(0, 4, 0, 0),
        };
        leftOptions.Controls.Add(_targetModeHintLabel, 0, 4);
        leftOptions.SetColumnSpan(_targetModeHintLabel, 2);
        _targetBatchLabel = new Label
        {
            Text = "Destination body batch list (optional)",
            Anchor = AnchorStyles.Left,
            AutoSize = true,
        };
        leftOptions.Controls.Add(_targetBatchLabel, 0, 5);
        _targetBatchTextBox = new TextBox
        {
            Name = "targetBatchTextBox",
            Dock = DockStyle.Fill,
            PlaceholderText = "Example: 3BA, HIMBO (mixed female + male pack)",
        };
        leftOptions.Controls.Add(_targetBatchTextBox, 1, 5);
        leftOptions.Controls.Add(new Label(), 0, 6);
        var mixedTargetsButton = new Button
        {
            Name = "mixedTargetsButton",
            Text = "Mixed female + male pack",
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 2, 8, 4),
        };
        mixedTargetsButton.Click += (_, _) => ApplySuggestedMixedGenderTargets();
        var chooseTargetsButton = new Button
        {
            Name = "chooseTargetsButton",
            Text = "Choose TO bodies...",
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 2, 8, 4),
        };
        chooseTargetsButton.Click += (_, _) => OpenTargetBodySelectionDialog();
        var allBodiesButton = new Button
        {
            Name = "allBodiesButton",
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
        var targetBatchActions = new FlowLayoutPanel
        {
            AutoSize = true,
            WrapContents = true,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = new Padding(0),
            Padding = new Padding(0),
            Dock = DockStyle.Fill,
        };
        targetBatchActions.Controls.Add(chooseTargetsButton);
        targetBatchActions.Controls.Add(mixedTargetsButton);
        targetBatchActions.Controls.Add(allBodiesButton);
        leftOptions.Controls.Add(targetBatchActions, 1, 6);
        leftOptions.Controls.Add(new Label { Text = "Preset details", Anchor = AnchorStyles.Left, AutoSize = true }, 0, 7);
        _presetDetailsLabel = new Label
        {
            Anchor = AnchorStyles.Left,
            AutoSize = true,
            MaximumSize = new Size(420, 0),
        };
        leftOptions.Controls.Add(_presetDetailsLabel, 1, 7);
        leftOptions.Controls.Add(new Label { Text = "Destination body details", Anchor = AnchorStyles.Left, AutoSize = true }, 0, 8);
        _targetDetailsLabel = new Label
        {
            Anchor = AnchorStyles.Left,
            AutoSize = true,
            MaximumSize = new Size(420, 0),
        };
        leftOptions.Controls.Add(_targetDetailsLabel, 1, 8);
        if (_presetComboBox.Items.Count > 0)
        {
            _presetComboBox.SelectedIndex = 0;
        }
        _destinationSetupGroupBox = CreateAutoSizeSection("Destination setup (what you want to build)", leftOptions);
        _conversionOptionsPanel.Controls.Add(_destinationSetupGroupBox, 0, 0);

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
            Text = "These fields describe the original armor or adjust optional output behavior. Most users only need the FROM body here when auto-detection is wrong.",
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
            MinimumSize = new Size(260, 0),
        };
        _profileComboBox.Items.Add("(auto)");
        foreach (var profile in DeformationProfileModifier.All.OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
        {
            _profileComboBox.Items.Add(profile);
        }
        _profileComboBox.SelectedIndex = 0;
        rightOptions.Controls.Add(_profileComboBox, 1, 1);

        rightOptions.Controls.Add(new Label { Text = "FROM body / original armor body (usually leave Auto)", Anchor = AnchorStyles.Left, AutoSize = true }, 0, 2);
        _sourceComboBox = new ComboBox
        {
            Dock = DockStyle.Fill,
            DropDownStyle = ComboBoxStyle.DropDown,
            AutoCompleteMode = AutoCompleteMode.SuggestAppend,
            AutoCompleteSource = AutoCompleteSource.ListItems,
            MinimumSize = new Size(260, 0),
        };
        _sourceComboBox.Items.Add("(auto)");
        foreach (var body in BodyTypeCatalog.All.OrderBy(b => b.Name, StringComparer.OrdinalIgnoreCase))
        {
            _sourceComboBox.Items.Add(body.Name);
        }
        _sourceComboBox.SelectedIndex = 0;
        _sourceComboBox.SelectedIndexChanged += (_, _) =>
        {
            UpdateSourceDetails();
            UpdateBodySelectionSummary();
        };
        _sourceComboBox.TextChanged += (_, _) =>
        {
            UpdateSourceDetails();
            UpdateBodySelectionSummary();
        };
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
            MinimumSize = new Size(260, 0),
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
            MinimumSize = new Size(260, 0),
        };
        _worldModeComboBox.Items.Add("(auto)");
        foreach (var worldMode in WorldDropModeCatalog.All)
        {
            _worldModeComboBox.Items.Add(worldMode);
        }
        _worldModeComboBox.SelectedIndex = 0;
        rightOptions.Controls.Add(_worldModeComboBox, 1, 6);

        rightOptions.Controls.Add(new Label { Text = "Skeleton support path (optional)", Anchor = AnchorStyles.Left, AutoSize = true }, 0, 7);
        var skeletonNifPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            AutoSize = true,
            Margin = new Padding(0),
            Padding = new Padding(0),
        };
        skeletonNifPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        skeletonNifPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        skeletonNifPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _skeletonNifTextBox = new TextBox
        {
            Dock = DockStyle.Fill,
            PlaceholderText = "Optional: skeleton .nif, XP32/XPMSSE folder, or related .pex file",
            AllowDrop = true,
        };
        _skeletonNifTextBox.DragEnter += OnDragEnter;
        _skeletonNifTextBox.DragDrop += OnDragDrop;
        var browseSkeletonNifButton = new Button { Text = "File...", AutoSize = true };
        browseSkeletonNifButton.Click += (_, _) => BrowseSkeletonSupportFile();
        var browseSkeletonFolderButton = new Button { Text = "Folder...", AutoSize = true, Margin = new Padding(4, 0, 0, 0) };
        browseSkeletonFolderButton.Click += (_, _) => BrowseSkeletonSupportFolder();
        skeletonNifPanel.Controls.Add(_skeletonNifTextBox, 0, 0);
        skeletonNifPanel.Controls.Add(browseSkeletonNifButton, 1, 0);
        skeletonNifPanel.Controls.Add(browseSkeletonFolderButton, 2, 0);
        rightOptions.Controls.Add(skeletonNifPanel, 1, 7);
        var supportGuideLabel = new Label
        {
            Anchor = AnchorStyles.Left,
            AutoSize = true,
            MaximumSize = new Size(420, 0),
            Text = "XP32/XPMSSE mod folders and related .pex files are accepted here. Footwear / heel cases are auto-detected during Inspect and Convert, then surfaced in preview-workbench.html and world-physics.json guidance.",
        };
        rightOptions.Controls.Add(supportGuideLabel, 1, 8);

        _sourceHintsGroupBox = CreateAutoSizeSection("Original armor source hints, optional output overrides, and support files", rightOptions);
        _conversionOptionsPanel.Controls.Add(_sourceHintsGroupBox, 1, 0);
        _topLayoutPanel.Controls.Add(CreateAutoSizeSection("Conversion setup", _conversionOptionsPanel), 0, 3);

        var outputRow = CreateThreeColumnRow("Output (optional)", out _outputTextBox);
        _outputTextBox.Name = "outputPathTextBox";
        _outputTextBox.AllowDrop = true;
        _outputTextBox.DragEnter += OnDragEnter;
        _outputTextBox.DragDrop += OnDragDrop;
        _outputTextBox.TextChanged += (_, _) =>
        {
            UpdatePathActionStates();
            UpdateOutputHint();
        };
        var browseOutputButton = new Button { Text = "Browse...", AutoSize = true };
        browseOutputButton.Click += (_, _) => BrowseOutput();
        outputRow.Controls.Add(browseOutputButton, 2, 0);
        var outputSection = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 1,
            AutoSize = true,
            Margin = new Padding(0),
        };
        outputSection.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        outputSection.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        outputSection.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        outputSection.Controls.Add(outputRow, 0, 0);
        _outputHintLabel = new Label
        {
            AutoSize = true,
            Margin = new Padding(0, 4, 0, 0),
            MaximumSize = new Size(920, 0),
        };
        outputSection.Controls.Add(_outputHintLabel, 0, 1);
        var pathSelectionPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 2,
            AutoSize = true,
            Margin = new Padding(0),
            Padding = new Padding(0),
        };
        pathSelectionPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
        pathSelectionPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
        pathSelectionPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        pathSelectionPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        pathSelectionPanel.Controls.Add(inputRow, 0, 0);
        pathSelectionPanel.Controls.Add(outputSection, 1, 0);
        pathSelectionPanel.Controls.Add(new Label
        {
            AutoSize = true,
            Margin = new Padding(0, 4, 0, 0),
            MaximumSize = new Size(560, 0),
            Text = "Input selection: choose File... or Folder..., then use “Inspect input now” (next to the Input field) for full analysis.",
        }, 0, 1);
        _topLayoutPanel.Controls.Add(pathSelectionPanel, 0, 1);

        var cacheRow = CreateThreeColumnRow("Learning cache (optional)", out _cachePathTextBox);
        _cachePathTextBox.PlaceholderText = "Custom path for .conversion-learning-cache.json";
        _cachePathTextBox.AllowDrop = true;
        _cachePathTextBox.DragEnter += OnDragEnter;
        _cachePathTextBox.DragDrop += OnDragDrop;
        _cachePathTextBox.TextChanged += (_, _) => ScheduleAutoInspectLearningCache();
        var browseCacheButton = new Button { Text = "Browse...", AutoSize = true };
        browseCacheButton.Click += (_, _) => BrowseCachePath();
        cacheRow.Controls.Add(browseCacheButton, 2, 0);
        _topLayoutPanel.Controls.Add(cacheRow, 0, 5);

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
            Name = "customProfilesListView",
            Dock = DockStyle.Fill,
            Height = 92,
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            HeaderStyle = ColumnHeaderStyle.Nonclickable,
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
        _customProfilesGroupBox = CreateAutoSizeSection("Custom profiles", customProfilesPanel);
        _topLayoutPanel.Controls.Add(_customProfilesGroupBox, 0, 6);

        var primaryActionRow = new FlowLayoutPanel
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
            Checked = true,
            Margin = new Padding(0, 8, 12, 0),
        };
        _outputZipCheckBox.CheckedChanged += (_, _) => UpdateOutputHint();
        _buildSlidersCheckBox = new CheckBox
        {
            Text = "Generate BodySlide project files",
            AutoSize = true,
            Checked = true,
            Margin = new Padding(0, 8, 12, 0),
        };
        _convertButton = new Button
        {
            Name = "convertButton",
            Text = "START CONVERSION",
            Width = 220,
            Height = 48,
            Margin = new Padding(0, 0, 12, 0),
            Font = new Font(Font, FontStyle.Bold),
        };
        _convertButton.Click += async (_, _) => await ConvertAsync();
        _cancelButton = new Button
        {
            Name = "cancelButton",
            Text = "Cancel",
            Width = 90,
            Height = 34,
            Enabled = false,
            Margin = new Padding(0, 0, 8, 0),
        };
        _cancelButton.Click += (_, _) => CancelConversion();
        _clearLogButton = new Button
        {
            Name = "clearLogButton",
            Text = "Clear log",
            Width = 90,
            Height = 34,
            Margin = new Padding(0, 0, 8, 0),
        };
        _clearLogButton.Click += (_, _) => ClearLog();
        _copyCurrentViewButton = new Button
        {
            Name = "copyCurrentViewButton",
            Text = "Copy view",
            Width = 100,
            Height = 34,
            Margin = new Padding(0, 0, 8, 0),
        };
        _copyCurrentViewButton.Click += (_, _) => CopyCurrentViewToClipboard();
        _openOutputButton = new Button
        {
            Name = "openOutputButton",
            Text = "Open output",
            Width = 110,
            Height = 34,
            Enabled = false,
            Margin = new Padding(0, 0, 8, 0),
        };
        _openOutputButton.Click += (_, _) => OpenOutputDirectory();
        _openPreviewButton = new Button
        {
            Name = "showPreviewButton",
            Text = "Show preview",
            Width = 110,
            Height = 34,
            Enabled = false,
            Margin = new Padding(0, 0, 8, 0),
        };
        _openPreviewButton.Click += async (_, _) => await ShowPreviewReportAsync();
        _loadResultButton = new Button
        {
            Name = "loadResultButton",
            Text = "Load result...",
            Width = 110,
            Height = 34,
            Margin = new Padding(0, 0, 8, 0),
        };
        _loadResultButton.Click += async (_, _) => await LoadPreviousResultAsync();
        _openBatchReportButton = new Button
        {
            Name = "openBatchReportButton",
            Text = "Batch report",
            Width = 110,
            Height = 34,
            Enabled = false,
        };
        _openBatchReportButton.Click += (_, _) => OpenBatchReport();
        _openReportButton = new Button
        {
            Name = "openReportButton",
            Text = "Open report",
            Width = 110,
            Height = 34,
            Enabled = false,
            Margin = new Padding(0, 0, 8, 0),
        };
        _openReportButton.Click += (_, _) => OpenSelectedReport();
        _openGuidanceTargetButton = new Button
        {
            Name = "openGuidanceTargetButton",
            Text = "Open next action",
            Width = 125,
            Height = 34,
            Enabled = false,
            Margin = new Padding(0, 0, 8, 0),
        };
        _openGuidanceTargetButton.Click += async (_, _) => await OpenSelectedGuidanceTargetAsync();
        _openArtifactButton = new Button
        {
            Name = "openArtifactButton",
            Text = "Open file",
            Width = 110,
            Height = 34,
            Enabled = false,
            Margin = new Padding(8, 0, 8, 0),
        };
        _openArtifactButton.Click += (_, _) => OpenSelectedArtifact();
        _loadCustomProfileButton = new Button
        {
            Name = "loadCustomProfileButton",
            Text = "Load custom profile...",
            Width = 140,
            Height = 34,
            Margin = new Padding(0, 0, 8, 0),
        };
        _loadCustomProfileButton.Click += (_, _) => LoadCustomProfileFile();
        _saveProfileButton = new Button
        {
            Name = "saveProfileButton",
            Text = "Save profile...",
            Width = 110,
            Height = 34,
            Margin = new Padding(0, 0, 8, 0),
        };
        _saveProfileButton.Click += (_, _) => SaveCurrentProfile();
        _inspectCacheButton = new Button
        {
            Name = "inspectCacheButton",
            Text = "Inspect cache",
            Width = 110,
            Height = 34,
        };
        _inspectCacheButton.Click += async (_, _) => await InspectLearningCacheAsync(showDialogs: true, switchToTab: true);
        _runSelfCheckButton = new Button
        {
            Name = "runSelfCheckButton",
            Text = "Run self-check",
            Width = 120,
            Height = 34,
            Margin = new Padding(8, 0, 0, 0),
        };
        _runSelfCheckButton.Click += (_, _) => RunSelfCheck();
        _copyMo2SetupButton = new Button
        {
            Name = "copyMo2SetupButton",
            Text = "Copy Mod Manager setup",
            Width = 200,
            Height = 34,
            Margin = new Padding(8, 0, 0, 0),
        };
        _copyMo2SetupButton.Click += (_, _) => CopyMo2SetupGuidance();
        var secondaryActionRow = new FlowLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Top,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            Margin = new Padding(0, 8, 0, 0),
        };
        primaryActionRow.Controls.Add(_convertButton);
        primaryActionRow.Controls.Add(_cancelButton);
        primaryActionRow.Controls.Add(_outputZipCheckBox);
        primaryActionRow.Controls.Add(_buildSlidersCheckBox);
        secondaryActionRow.Controls.Add(_clearLogButton);
        secondaryActionRow.Controls.Add(_copyCurrentViewButton);
        secondaryActionRow.Controls.Add(_openOutputButton);
        secondaryActionRow.Controls.Add(_openPreviewButton);
        secondaryActionRow.Controls.Add(_loadResultButton);
        secondaryActionRow.Controls.Add(_openBatchReportButton);
        secondaryActionRow.Controls.Add(_openReportButton);
        secondaryActionRow.Controls.Add(_openGuidanceTargetButton);
        secondaryActionRow.Controls.Add(_openArtifactButton);
        secondaryActionRow.Controls.Add(_loadCustomProfileButton);
        secondaryActionRow.Controls.Add(_saveProfileButton);
        secondaryActionRow.Controls.Add(_inspectCacheButton);
        secondaryActionRow.Controls.Add(_runSelfCheckButton);
        secondaryActionRow.Controls.Add(_copyMo2SetupButton);
        var actionLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 1,
            AutoSize = true,
            Margin = new Padding(0),
        };
        actionLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        actionLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        actionLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        actionLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        actionLayout.Controls.Add(new Label
        {
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 6),
            Text = "Step 4: review the setup above, then use Start conversion. The buttons below it are optional tools and reports."
        }, 0, 0);
        actionLayout.Controls.Add(primaryActionRow, 0, 1);
        actionLayout.Controls.Add(secondaryActionRow, 0, 2);
        _topLayoutPanel.Controls.Add(CreateAutoSizeSection("Actions", actionLayout), 0, 7);

        var bottomPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
        };
        bottomPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        bottomPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        bottomPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        bottomPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        _statusLabel = new Label
        {
            AutoSize = true,
            Text = "Ready.",
            Margin = new Padding(0, 4, 0, 2),
        };
        _progressDetailsLabel = new Label
        {
            AutoSize = true,
            Text = "Progress details: idle",
            Margin = new Padding(0, 0, 0, 4),
        };
        _progressBar = new ProgressBar
        {
            Dock = DockStyle.Top,
            Height = 18,
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
            Name = "resultsTabControl",
            Dock = DockStyle.Fill,
            Multiline = false,
        };
        var logTabPage = new TabPage(DesktopSmokeTestContract.LogTabTitle) { Name = "logTabPage" };
        logTabPage.Controls.Add(_logTextBox);
        _previewTabPage = new TabPage(DesktopSmokeTestContract.PreviewTabTitle) { Name = "previewTabPage" };
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
        _inspectTabPage = new TabPage(DesktopSmokeTestContract.InspectTabTitle) { Name = "inspectTabPage" };
        _inspectListView = new ListView
        {
            Name = "inspectListView",
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
        _summaryTabPage = new TabPage(DesktopSmokeTestContract.SummaryTabTitle) { Name = "summaryTabPage" };
        _summaryListView = new ListView
        {
            Name = "summaryListView",
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
        _pipelineTabPage = new TabPage(DesktopSmokeTestContract.PipelineTabTitle) { Name = "pipelineTabPage" };
        _pipelineListView = new ListView
        {
            Name = "pipelineListView",
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            HeaderStyle = ColumnHeaderStyle.Nonclickable,
        };
        _pipelineListView.Columns.Add("Step", 60);
        _pipelineListView.Columns.Add("Stage", 260);
        _pipelineListView.Columns.Add("Status", 120);
        _pipelineListView.Columns.Add("Progress", 120);
        _pipelineListView.Columns.Add("ETA / note", -2);
        _pipelineTabPage.Controls.Add(_pipelineListView);
        _resultsTabControl.TabPages.Add(_pipelineTabPage);
        _guidanceTabPage = new TabPage(DesktopSmokeTestContract.NextActionsTabTitle) { Name = "guidanceTabPage" };
        _guidanceListView = new ListView
        {
            Name = "guidanceListView",
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            HeaderStyle = ColumnHeaderStyle.Nonclickable,
            ShowItemToolTips = true,
        };
        _guidanceListView.Columns.Add("Area", 150);
        _guidanceListView.Columns.Add("Status", 90);
        _guidanceListView.Columns.Add("Details / next step", -2);
        _guidanceListView.SelectedIndexChanged += (_, _) => UpdatePathActionStates();
        _guidanceListView.DoubleClick += async (_, _) => await OpenSelectedGuidanceTargetAsync();
        _guidanceTabPage.Controls.Add(_guidanceListView);
        _reportsTabPage = new TabPage(DesktopSmokeTestContract.ReportsTabTitle) { Name = "reportsTabPage" };
        _reportsListView = new ListView
        {
            Name = "reportsListView",
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            HeaderStyle = ColumnHeaderStyle.Nonclickable,
        };
        _reportsListView.Columns.Add("Report", 260);
        _reportsListView.Columns.Add("Property", 180);
        _reportsListView.Columns.Add("Value", -2);
        _reportsListView.SelectedIndexChanged += (_, _) => UpdatePathActionStates();
        _reportsListView.DoubleClick += (_, _) => OpenSelectedReport();
        _reportsTabPage.Controls.Add(_reportsListView);
        _resultsTabControl.TabPages.Add(_reportsTabPage);
        _catalogTabPage = new TabPage(DesktopSmokeTestContract.CatalogTabTitle) { Name = "catalogTabPage" };
        _catalogListView = new ListView
        {
            Name = "catalogListView",
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
        _readinessTabPage = new TabPage(DesktopSmokeTestContract.ReadinessTabTitle) { Name = "readinessTabPage" };
        _readinessListView = new ListView
        {
            Name = "readinessListView",
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
        _artifactsTabPage = new TabPage(DesktopSmokeTestContract.FilesTabTitle) { Name = "artifactsTabPage" };
        _artifactsListView = new ListView
        {
            Name = "artifactsListView",
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            HeaderStyle = ColumnHeaderStyle.Nonclickable,
        };
        _artifactsListView.Columns.Add("File", 260);
        _artifactsListView.Columns.Add("Path", -2);
        _artifactsListView.SelectedIndexChanged += (_, _) => UpdatePathActionStates();
        _artifactsListView.DoubleClick += (_, _) => OpenSelectedArtifact();
        _artifactsTabPage.Controls.Add(_artifactsListView);
        _resultsTabControl.TabPages.Add(_artifactsTabPage);
        _cacheTabPage = new TabPage(DesktopSmokeTestContract.CacheTabTitle) { Name = "cacheTabPage" };
        _cacheListView = new ListView
        {
            Name = "cacheListView",
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
        _resultsTabControl.SelectedIndexChanged += async (_, _) =>
        {
            if (_resultsTabControl.SelectedTab != _cacheTabPage || _cacheInspectionInitialized)
            {
                return;
            }

            _cacheInspectionInitialized = true;
            await InspectLearningCacheAsync(showDialogs: false, switchToTab: false);
        };
        AttachListViewCopySupport(_inspectListView);
        AttachListViewCopySupport(_summaryListView);
        AttachListViewCopySupport(_pipelineListView);
        AttachListViewCopySupport(_guidanceListView);
        AttachListViewCopySupport(_reportsListView);
        AttachListViewCopySupport(_catalogListView);
        AttachListViewCopySupport(_readinessListView);
        AttachListViewCopySupport(_artifactsListView);
        AttachListViewCopySupport(_cacheListView);
        AttachTextCopySupport(_logTextBox);
        bottomPanel.Controls.Add(_statusLabel, 0, 0);
        bottomPanel.Controls.Add(_progressBar, 0, 1);
        bottomPanel.Controls.Add(_progressDetailsLabel, 0, 2);
        bottomPanel.Controls.Add(_resultsTabControl, 0, 3);
        _mainSplitContainer.Panel2.Controls.Add(CreateSection("Results and diagnostics", bottomPanel));

        RefreshModeState();
        ApplyAdvancedOptionsVisibility();
        UpdateResponsiveLayout();
        UpdateMainSplitLayout();
        UpdateSourceDetails();
        UpdateBodySelectionSummary();
        PopulateCatalogTab();
        PopulateReadinessTab(CreateDesktopReadinessReport());
        ResetPipelineTimeline();
        PopulateGuidanceTab(Array.Empty<string>(), null);
        LoadUiSettings();
        RefreshCustomProfilesList();
        UpdatePathActionStates();
        UpdateOutputHint();
        ClearInspectionTab("Select an input, then click “Inspect input now” (next to the Input field) to preview body detection, mesh analysis, and skeleton compatibility.");
        PopulateReportsTab(Array.Empty<DesktopWorkflowReportMetric>());
        PopulateCacheTab([], null);
        ShowPreviewStatus("Run a conversion to render preview-workbench.html in-app.");
        ConfigureOptionTooltips();
        _suppressThemeSelectionChanged = true;
        _themeComboBox.SelectedItem = _currentTheme.ToString();
        _suppressThemeSelectionChanged = false;
        ApplyTheme(_currentTheme);
        UpdateResponsiveLayout();
        UpdateMainSplitLayout();
        AppendLog("Ready. Choose armor/clothing input, confirm FROM body (what the armor was made for) and TO body (what you want to build), then click Convert.");
        SizeChanged += (_, _) =>
        {
            UpdateResponsiveLayout();
            UpdateMainSplitLayout();
            UpdateListViewColumnLayouts();
        };
        FormClosing += (_, _) => SaveUiSettings(flush: true);
        Shown += async (_, _) =>
        {
            _allowUserMainSplitOverride = true;
            ApplyLauncherContextGuidance();
            UpdateStartupHandoffTelemetry("Direct launch", "No launcher handoff diagnostics were provided.");
            if (!string.IsNullOrWhiteSpace(_launchOptions.StartupDiagnostics))
            {
                AppendLog($"Launcher handoff diagnostics: {_launchOptions.StartupDiagnostics}");
                UpdateStartupHandoffTelemetry("Launcher handoff detected", _launchOptions.StartupDiagnostics);
                if (string.IsNullOrWhiteSpace(_statusLabel.Text) ||
                    _statusLabel.Text.StartsWith("Ready", StringComparison.OrdinalIgnoreCase))
                {
                    _statusLabel.Text = "Ready — launcher handoff diagnostics recorded in log.";
                }
            }

            var startupPlan = DesktopWorkflowSupport.BuildStartupHandoffPlan(
                _launchOptions,
                ShouldAutoInspectInputPath);

            if (!_startupResultLoadHandled && startupPlan.ShouldLoadStartupResult)
            {
                _startupResultLoadHandled = true;
                var startupOutputDirectory = _launchOptions.StartupOutputDirectory;
                if (string.IsNullOrWhiteSpace(startupOutputDirectory))
                {
                    return;
                }
                UpdateStartupHandoffTelemetry(
                    _launchOptions.FromModOrganizerLauncher ? "Mod manager handoff" : "Launcher handoff",
                    $"Startup result directory: {startupOutputDirectory}");
                await RunStartupOperationWithTimeoutAsync(
                    "loading startup result",
                    cancellationToken => LoadResultDirectoryAsync(
                        startupOutputDirectory,
                        _launchOptions.FromModOrganizerLauncher ? "mod manager launcher argument" : "launcher argument",
                        cancellationToken));
                return;
            }

            if (startupPlan.ShouldApplyStartupInput &&
                !string.IsNullOrWhiteSpace(startupPlan.StartupInputPath))
            {
                _inputTextBox.Text = startupPlan.StartupInputPath;
                UpdateStartupHandoffTelemetry(
                    _launchOptions.FromModOrganizerLauncher ? "Mod manager handoff" : "Launcher handoff",
                    $"Startup input path: {startupPlan.StartupInputPath}");
                _statusLabel.Text = _launchOptions.FromModOrganizerLauncher
                    ? "Ready — input loaded from mod manager launcher."
                    : "Ready — input loaded from launcher.";
                AppendLog(_launchOptions.FromModOrganizerLauncher
                    ? $"Startup input loaded from mod manager launcher: {startupPlan.StartupInputPath}"
                    : $"Startup input loaded from launcher: {startupPlan.StartupInputPath}");
                if (startupPlan.ShouldQueueStartupAutoInspect)
                {
                    ScheduleAutoInspectInput();
                    _statusLabel.Text = "Ready — startup input loaded. Auto-inspection queued.";
                    AppendLog("Startup input auto-inspection queued.");
                }
                else
                {
                    _statusLabel.Text = "Ready — startup input loaded. Click “Inspect input now” (next to the Input field) to run analysis.";
                    AppendLog("Auto-inspection skipped for startup input to keep startup responsive.");
                }
            }
        };
    }

    internal DesktopSmokeTestSummary GetSmokeTestSummary()
    {
        return DesktopSmokeTestContract.Create(
            Text,
            GetSelectableOptionItems(_presetComboBox),
            GetSelectableOptionItems(_targetComboBox),
            GetSelectableOptionItems(_profileComboBox),
            GetSelectableOptionItems(_physicsComboBox),
            GetTabTitles(_resultsTabControl));
    }

    internal string GetSmokeTestSummaryJson()
    {
        return DesktopSmokeTestContract.Serialize(GetSmokeTestSummary());
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

    private static GroupBox CreateAutoSizeSection(string title, Control content)
    {
        content.Dock = DockStyle.Top;
        return new GroupBox
        {
            Text = title,
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(12),
            Margin = new Padding(0, 10, 0, 0),
            Controls = { content }
        };
    }

    private void UpdateResponsiveLayout()
    {
        if (_conversionOptionsPanel.IsDisposed)
        {
            return;
        }

        var useSingleColumn = ClientSize.Width < 1500 ||
                              _conversionOptionsPanel.DisplayRectangle.Width < 1100;
        if (_usesSingleColumnConversionLayout == useSingleColumn)
        {
            return;
        }

        _conversionOptionsPanel.SuspendLayout();
        try
        {
            _usesSingleColumnConversionLayout = useSingleColumn;
            _conversionOptionsPanel.ColumnStyles.Clear();
            _conversionOptionsPanel.RowStyles.Clear();

            if (useSingleColumn)
            {
                _conversionOptionsPanel.ColumnCount = 1;
                _conversionOptionsPanel.RowCount = 2;
                _conversionOptionsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
                _conversionOptionsPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                _conversionOptionsPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                _conversionOptionsPanel.SetCellPosition(_destinationSetupGroupBox, new TableLayoutPanelCellPosition(0, 0));
                _conversionOptionsPanel.SetCellPosition(_sourceHintsGroupBox, new TableLayoutPanelCellPosition(0, 1));
            }
            else
            {
                _conversionOptionsPanel.ColumnCount = 2;
                _conversionOptionsPanel.RowCount = 1;
                _conversionOptionsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
                _conversionOptionsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
                _conversionOptionsPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                _conversionOptionsPanel.SetCellPosition(_destinationSetupGroupBox, new TableLayoutPanelCellPosition(0, 0));
                _conversionOptionsPanel.SetCellPosition(_sourceHintsGroupBox, new TableLayoutPanelCellPosition(1, 0));
            }
        }
        finally
        {
            _conversionOptionsPanel.ResumeLayout(performLayout: true);
        }
    }

    private void UpdateMainSplitLayout()
    {
        if (_mainSplitContainer.IsDisposed)
        {
            return;
        }

        var availableHeight = _mainSplitContainer.ClientSize.Height - _mainSplitContainer.SplitterWidth;
        if (availableHeight <= 0)
        {
            return;
        }

        var panel1Minimum = Math.Min(MainSplitPanel1Minimum, Math.Max(0, availableHeight - 1));
        var panel2Minimum = Math.Min(MainSplitPanel2Minimum, Math.Max(0, availableHeight - panel1Minimum));
        var maxSplitterDistance = Math.Max(panel1Minimum, availableHeight - panel2Minimum);
        var preferredPanel1Height = MainSplitPreferredDistance;
        if (_topLayoutPanel is not null && !_topLayoutPanel.IsDisposed)
        {
            var widthBudget = Math.Max(0, _mainSplitContainer.Panel1.ClientSize.Width - SystemInformation.VerticalScrollBarWidth);
            preferredPanel1Height = Math.Max(
                panel1Minimum,
                _topLayoutPanel.GetPreferredSize(new Size(widthBudget, 0)).Height + 16);
        }

        if (_mainSplitContainer.Panel1MinSize != panel1Minimum)
        {
            _mainSplitContainer.Panel1MinSize = panel1Minimum;
        }

        if (_mainSplitContainer.Panel2MinSize != panel2Minimum)
        {
            _mainSplitContainer.Panel2MinSize = panel2Minimum;
        }

        var splitterDistance = _userAdjustedMainSplit
            ? Math.Clamp(_userPreferredMainSplitDistance ?? _mainSplitContainer.SplitterDistance, panel1Minimum, maxSplitterDistance)
            : Math.Clamp(preferredPanel1Height, panel1Minimum, maxSplitterDistance);
        if (_mainSplitContainer.SplitterDistance != splitterDistance)
        {
            _mainSplitContainer.SplitterDistance = splitterDistance;
        }
    }

    private void UpdateListViewColumnLayouts()
    {
        AutoSizeListViewColumns(_inspectListView, 240, 520);
        AutoSizeListViewColumns(_summaryListView, 220, 420);
        AutoSizeListViewColumns(_pipelineListView, 70, 260, 120, 120, 320);
        AutoSizeListViewColumns(_guidanceListView, 180, 100, 460);
        AutoSizeListViewColumns(_reportsListView, 260, 180, 420);
        AutoSizeListViewColumns(_catalogListView, 170, 180, 480);
        AutoSizeListViewColumns(_readinessListView, 180, 110, 420);
        AutoSizeListViewColumns(_artifactsListView, 260, 520);
        AutoSizeListViewColumns(_cacheListView, 220, 80, 120, 180, 70, 100, 150, 320);
        AutoSizeListViewColumns(_customProfilesListView, 520);
    }

    private static void AutoSizeListViewColumns(ListView listView, params int[] minimumWidths)
    {
        if (listView.IsDisposed ||
            listView.View != View.Details ||
            listView.Columns.Count == 0)
        {
            return;
        }

        var widths = new int[listView.Columns.Count];
        try
        {
            listView.AutoResizeColumns(ColumnHeaderAutoResizeStyle.HeaderSize);
            for (var index = 0; index < listView.Columns.Count; index++)
            {
                widths[index] = listView.Columns[index].Width;
            }

            if (listView.Items.Count > 0)
            {
                listView.AutoResizeColumns(ColumnHeaderAutoResizeStyle.ColumnContent);
                for (var index = 0; index < listView.Columns.Count; index++)
                {
                    widths[index] = Math.Max(widths[index], listView.Columns[index].Width);
                }
            }
        }
        catch
        {
            return;
        }

        var availableWidth = Math.Max(0, listView.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 4);
        var fixedWidthTotal = 0;
        for (var index = 0; index < listView.Columns.Count - 1; index++)
        {
            widths[index] = Math.Max(widths[index], index < minimumWidths.Length ? minimumWidths[index] : 0);
            fixedWidthTotal += widths[index];
        }

        var lastIndex = listView.Columns.Count - 1;
        var minimumLastWidth = lastIndex < minimumWidths.Length ? minimumWidths[lastIndex] : 220;
        widths[lastIndex] = Math.Max(widths[lastIndex], minimumLastWidth);
        widths[lastIndex] = Math.Max(widths[lastIndex], availableWidth - fixedWidthTotal);

        for (var index = 0; index < listView.Columns.Count; index++)
        {
            listView.Columns[index].Width = widths[index];
        }
    }

    private void OnThemeSelectionChanged()
    {
        if (_suppressThemeSelectionChanged)
        {
            return;
        }

        if (_themeComboBox.SelectedItem is not string selectedTheme ||
            !Enum.TryParse<UiTheme>(selectedTheme, ignoreCase: true, out var theme))
        {
            return;
        }

        if (theme == _currentTheme)
        {
            return;
        }

        _currentTheme = theme;
        ApplyTheme(theme);
        SaveUiSettings();
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
                    ReferenceEquals(label, _progressDetailsLabel) ||
                    ReferenceEquals(label, _presetDetailsLabel) ||
                    ReferenceEquals(label, _targetDetailsLabel) ||
                    ReferenceEquals(label, _sourceDetailsLabel) ||
                    ReferenceEquals(label, _physicsDetailsLabel)
                    ? palette.SecondaryForeground
                    : palette.Foreground;
                break;
            case TextBox textBox:
                textBox.BackColor = palette.InputBackground;
                textBox.ForeColor = palette.Foreground;
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
                if (ReferenceEquals(button, _convertButton))
                {
                    button.FlatAppearance.BorderColor = palette.Accent;
                    button.FlatAppearance.MouseDownBackColor = BlendColors(palette.Accent, palette.SurfaceBackground, 0.15);
                    button.FlatAppearance.MouseOverBackColor = BlendColors(palette.Accent, palette.SurfaceBackground, 0.25);
                    button.BackColor = palette.Accent;
                    button.ForeColor = Color.White;
                }
                else
                {
                    button.FlatAppearance.BorderColor = palette.Border;
                    button.FlatAppearance.MouseDownBackColor = BlendColors(palette.SurfaceBackground, palette.Accent, 0.35);
                    button.FlatAppearance.MouseOverBackColor = BlendColors(palette.SurfaceBackground, palette.Accent, 0.18);
                    button.BackColor = palette.SurfaceBackground;
                    button.ForeColor = palette.Foreground;
                }
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

    private void LoadUiSettings()
    {
        var loadTimer = System.Diagnostics.Stopwatch.StartNew();
        _currentTheme = GetSystemPreferredTheme();
        _customProfilePaths.Clear();

        var settingsPath = GetUiSettingsPath();
        try
        {
            var settings = DesktopUiSettingsStore.Load(settingsPath);
            _customProfilePaths.AddRange(settings.CustomProfilePaths ?? []);

            if (!string.IsNullOrWhiteSpace(settings.Theme) &&
                Enum.TryParse<UiTheme>(settings.Theme, ignoreCase: true, out var theme))
            {
                _currentTheme = theme;
            }
        }
        catch (IOException ex)
        {
            System.Diagnostics.Trace.TraceWarning($"Failed to read theme preference from '{settingsPath}': {ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            System.Diagnostics.Trace.TraceWarning($"Failed to access theme preference at '{settingsPath}': {ex.Message}");
        }
        catch (JsonException ex)
        {
            System.Diagnostics.Trace.TraceWarning($"Failed to parse theme preference from '{settingsPath}': {ex.Message}");
        }
        finally
        {
            loadTimer.Stop();
            System.Diagnostics.Trace.TraceInformation($"UI settings load completed in {loadTimer.ElapsedMilliseconds} ms.");
        }
    }

    private DesktopUiSettings CaptureUiSettingsSnapshot() =>
        new(
            _currentTheme.ToString(),
            _customProfilePaths.Count > 0
                ? [.. _customProfilePaths]
                : []);

    private void SaveUiSettings(bool flush = false)
    {
        var snapshot = CaptureUiSettingsSnapshot();
        if (flush)
        {
            FlushPendingUiSettingsSave(snapshot);
            return;
        }

        lock (_uiSettingsSaveSync)
        {
            _pendingUiSettingsSave = snapshot;
            if (_uiSettingsSaveTask is null || _uiSettingsSaveTask.IsCompleted)
            {
                _uiSettingsSaveTask = Task.Run(ProcessPendingUiSettingsSaves);
            }
        }
    }

    private void FlushPendingUiSettingsSave(DesktopUiSettings snapshot)
    {
        Task? pendingSaveTask;
        lock (_uiSettingsSaveSync)
        {
            _pendingUiSettingsSave = snapshot;
            pendingSaveTask = _uiSettingsSaveTask;
        }

        if (pendingSaveTask is not null && !pendingSaveTask.IsCompleted)
        {
            pendingSaveTask.GetAwaiter().GetResult();
            return;
        }

        TrySaveUiSettingsSnapshot(snapshot);
    }

    private void ProcessPendingUiSettingsSaves()
    {
        while (true)
        {
            DesktopUiSettings? snapshot;
            lock (_uiSettingsSaveSync)
            {
                snapshot = _pendingUiSettingsSave;
                _pendingUiSettingsSave = null;
                if (snapshot is null)
                {
                    _uiSettingsSaveTask = null;
                    return;
                }
            }

            TrySaveUiSettingsSnapshot(snapshot);
        }
    }

    private void TrySaveUiSettingsSnapshot(DesktopUiSettings snapshot)
    {
        try
        {
            DesktopUiSettingsStore.Save(GetUiSettingsPath(), snapshot);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            ReportUiSettingsSaveFailure(ex.Message);
        }
    }

    private void ReportUiSettingsSaveFailure(string message)
    {
        if (IsDisposed || Disposing || !IsHandleCreated)
        {
            System.Diagnostics.Trace.TraceWarning($"Failed to save UI settings: {message}");
            return;
        }

        if (InvokeRequired)
        {
            BeginInvoke(() => AppendLog($"Failed to save UI settings: {message}"));
            return;
        }

        AppendLog($"Failed to save UI settings: {message}");
    }

    private bool PruneMissingCustomProfiles(string operation)
    {
        if (_customProfilePaths.Count == 0)
        {
            return false;
        }

        var missingPaths = _customProfilePaths
            .Where(static path => !File.Exists(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (missingPaths.Length == 0)
        {
            return false;
        }

        _customProfilePaths.RemoveAll(path => missingPaths.Contains(path, StringComparer.OrdinalIgnoreCase));
        RefreshCustomProfilesList();
        SaveUiSettings();
        ClearInspectionTab("Custom body profiles changed. Auto-inspection will refresh detection and compatibility details.");
        ScheduleAutoInspectInput();

        var missingNames = string.Join(", ", missingPaths.Select(Path.GetFileName));
        AppendLog($"Removed {missingPaths.Length} missing custom profile file(s) before {operation}: {missingNames}");
        MessageBox.Show(
            this,
            $"Removed {missingPaths.Length} missing custom profile file(s) before {operation}:\n{missingNames}",
            "Missing custom profiles",
            MessageBoxButtons.OK,
            MessageBoxIcon.Warning);
        return true;
    }

    private static string GetUiSettingsPath() => DesktopUiSettingsStore.GetDefaultSettingsPath();

    private static UiTheme GetSystemPreferredTheme()
    {
        if (OperatingSystem.IsWindows())
        {
            try
            {
                if (TryGetWindowsRegistryTheme() is { } registryTheme)
                {
                    return registryTheme;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceWarning($"Failed to detect system theme preference from the registry: {ex.Message}");
            }
        }

        var background = SystemColors.Window;
        var luminance = ((background.R * 0.2126) + (background.G * 0.7152) + (background.B * 0.0722)) / 255d;
        return luminance < 0.5d ? UiTheme.Dark : UiTheme.Light;
    }

    [SupportedOSPlatform("windows")]
    private static UiTheme? TryGetWindowsRegistryTheme()
    {
        using var personalizeKey = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
        return personalizeKey?.GetValue("AppsUseLightTheme") is int appsUseLightTheme
            ? appsUseLightTheme == 0 ? UiTheme.Dark : UiTheme.Light
            : null;
    }

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
        AutoSizeListViewColumns(_catalogListView, 170, 180, 480);
    }

    private IReadOnlyList<RuntimeReadinessCheck> CreateDesktopReadinessReport()
    {
        var checks = RuntimeReadinessReporter.CreateDesktopReport(Environment.ProcessPath).ToList();
        AppendModOrganizerHealthChecks(checks);
        AppendLatestOutputVerificationChecks(checks);

        try
        {
            var version = CoreWebView2Environment.GetAvailableBrowserVersionString();
            checks.Add(new RuntimeReadinessCheck("Preview runtime", "OK", $"WebView2 runtime detected ({version})."));
        }
        catch (WebView2RuntimeNotFoundException)
        {
            checks.Add(new RuntimeReadinessCheck("Preview runtime", "OK", "WebView2 runtime not found; preview-workbench.html (or preview.html fallback) will open in your default browser instead."));
        }
        catch (Exception ex)
        {
            checks.Add(new RuntimeReadinessCheck("Preview runtime", "Warning", $"WebView2 runtime probe failed: {ex.Message}"));
        }

        return checks;
    }

    private void AppendLatestOutputVerificationChecks(List<RuntimeReadinessCheck> checks)
    {
        if (string.IsNullOrWhiteSpace(_lastOutputDirectory) || !Directory.Exists(_lastOutputDirectory))
        {
            checks.Add(new RuntimeReadinessCheck(
                "Latest output verification",
                "Info",
                "Run a conversion or load an output folder to verify Skyrim packaging, BodySlide assets, and remaining proof blockers."));
            return;
        }

        var outputDirectory = _lastOutputDirectory;
        var stagedMeshDirectory = Path.Combine(outputDirectory, "meshes", "slidesmith");
        var stagedMeshCount = Directory.Exists(stagedMeshDirectory)
            ? Directory.EnumerateFiles(stagedMeshDirectory, "*.nif", SearchOption.AllDirectories).Count()
            : 0;
        var hasModuleConfig = File.Exists(Path.Combine(outputDirectory, "fomod", "ModuleConfig.xml"));
        var hasInfoXml = File.Exists(Path.Combine(outputDirectory, "fomod", "info.xml"));
        var hasMetaIni = File.Exists(Path.Combine(outputDirectory, "meta.ini"));
        var hasPackageScaffold = stagedMeshCount > 0 && hasModuleConfig && hasInfoXml && hasMetaIni;

        checks.Add(new RuntimeReadinessCheck(
            "Skyrim package recognition",
            hasPackageScaffold ? "OK" : "Warning",
            hasPackageScaffold
                ? $"Packaged output looks installable: {stagedMeshCount} staged mesh(es), FOMOD ModuleConfig/info.xml, and meta.ini found."
                : $"Latest output is missing required package pieces. Staged meshes={stagedMeshCount}, ModuleConfig={FormatYesNo(hasModuleConfig)}, info.xml={FormatYesNo(hasInfoXml)}, meta.ini={FormatYesNo(hasMetaIni)}."));

        var sliderSetsDirectory = Path.Combine(outputDirectory, "CalienteTools", "BodySlide", "SliderSets");
        var sliderGroupsDirectory = Path.Combine(outputDirectory, "CalienteTools", "BodySlide", "SliderGroups");
        var shapeDataDirectory = Path.Combine(outputDirectory, "CalienteTools", "BodySlide", "ShapeData");
        var ospCount = Directory.Exists(sliderSetsDirectory)
            ? Directory.EnumerateFiles(sliderSetsDirectory, "*.osp", SearchOption.TopDirectoryOnly).Count()
            : 0;
        var sliderGroupCount = Directory.Exists(sliderGroupsDirectory)
            ? Directory.EnumerateFiles(sliderGroupsDirectory, "*.xml", SearchOption.TopDirectoryOnly).Count()
            : 0;
        var shapeDataNifCount = Directory.Exists(shapeDataDirectory)
            ? Directory.EnumerateFiles(shapeDataDirectory, "*.nif", SearchOption.AllDirectories).Count()
            : 0;
        var shapeDataPayloadCount = Directory.Exists(shapeDataDirectory)
            ? Directory.EnumerateFiles(shapeDataDirectory, "*.tri", SearchOption.AllDirectories).Count() +
              Directory.EnumerateFiles(shapeDataDirectory, "*.bsd", SearchOption.AllDirectories).Count()
            : 0;
        var hasBodySlideScaffold = ospCount > 0 && sliderGroupCount > 0 && shapeDataNifCount > 0 && shapeDataPayloadCount > 0;

        checks.Add(new RuntimeReadinessCheck(
            "BodySlide recognizability",
            hasBodySlideScaffold ? "OK" : "Warning",
            hasBodySlideScaffold
                ? $"BodySlide scaffold detected: {ospCount} SliderSets project(s), {sliderGroupCount} SliderGroups file(s), {shapeDataNifCount} ShapeData NIF(s), {shapeDataPayloadCount} TRI/BSD payload(s)."
                : $"BodySlide scaffold is incomplete. SliderSets(.osp)={ospCount}, SliderGroups(.xml)={sliderGroupCount}, ShapeData NIFs={shapeDataNifCount}, TRI/BSD payloads={shapeDataPayloadCount}."));

        var checklistPath = File.Exists(Path.Combine(outputDirectory, "remaining-gaps-checklist.json"))
            ? Path.Combine(outputDirectory, "remaining-gaps-checklist.json")
            : File.Exists(Path.Combine(outputDirectory, "remaining-gaps-pack-checklist.json"))
                ? Path.Combine(outputDirectory, "remaining-gaps-pack-checklist.json")
                : null;
        if (string.IsNullOrWhiteSpace(checklistPath))
        {
            checks.Add(new RuntimeReadinessCheck(
                "Proof blockers",
                "Info",
                "No remaining-gaps checklist report found in the loaded output. Re-run conversion to generate strict/universal blocker tracking."));
            return;
        }

        try
        {
            using var document = OpenJsonDocument(checklistPath);
            var root = document.RootElement;
            var strictProofReady = TryReadBoolValue(root, "StrictProofReady") == true;
            var proofCoverage = TryReadString(root, "ProofCoverage") ?? "unknown";
            var sourceReport = TryReadString(root, "SourceReport") ?? Path.GetFileName(checklistPath);
            var remainingGapCount = TryReadArrayCount(root, "RemainingGaps");

            checks.Add(new RuntimeReadinessCheck(
                "Proof blockers",
                strictProofReady || remainingGapCount == 0 ? "OK" : "Warning",
                strictProofReady || remainingGapCount == 0
                    ? $"Strict/universal blocker checklist is clear for the loaded output ({sourceReport}; coverage={proofCoverage})."
                    : $"Loaded output still has {remainingGapCount} strict/universal blocker(s) ({sourceReport}; coverage={proofCoverage}). Use Summary recommendations and the checklist report to close them."));
        }
        catch (Exception ex)
        {
            checks.Add(new RuntimeReadinessCheck(
                "Proof blockers",
                "Warning",
                $"Could not read blocker checklist report '{Path.GetFileName(checklistPath)}': {ex.Message}"));
        }
    }

    private static string FormatYesNo(bool value) => value ? "Yes" : "No";

    private void AppendModOrganizerHealthChecks(List<RuntimeReadinessCheck> checks)
    {
        var mo2EnvironmentDetected = IsLikelyModManagerEnvironment();
        var mo2ContextDetected = _launchOptions.FromModOrganizerLauncher || mo2EnvironmentDetected;
        if (!mo2ContextDetected)
        {
            checks.Add(new RuntimeReadinessCheck(
                "Mod manager health check",
                "Info",
                "Mod manager context was not detected in this session. If launching from MO2 or Vortex, use 'Copy Mod Manager setup' and include --mo2-launcher (MO2) or --vortex-launcher (Vortex)."));
            return;
        }

        checks.Add(new RuntimeReadinessCheck(
            "Mod manager health check",
            "OK",
            "Mod manager context detected. Verify the launcher targets the desktop executable, keeps Start in on the same folder, and passes a launcher flag so MO2's VFS can hook before startup."));

        var executablePath = Environment.ProcessPath ?? Application.ExecutablePath;
        var executableName = Path.GetFileName(executablePath);
        var executableDirectory = Path.GetDirectoryName(executablePath) ?? Environment.CurrentDirectory;
        var normalizedExecutable = executablePath.Replace('\\', '/');
        var looksLikeCliTarget =
            executableName.Equals("SlideSmith-CLI.exe", StringComparison.OrdinalIgnoreCase) ||
            normalizedExecutable.Contains("/cli/", StringComparison.OrdinalIgnoreCase);

        if (looksLikeCliTarget)
        {
            var suggestedDesktopPath = FindSiblingDesktopExecutable(executableDirectory) ?? "SlideSmith.exe";
            checks.Add(new RuntimeReadinessCheck(
                "Mod manager executable target",
                "Warning",
                $"Current launch path looks like CLI ({executableName}). MO2's VFS/USVFS hook needs the desktop executable as Binary and the same folder as Start in so it can inject mods before startup. Set launcher Binary to {suggestedDesktopPath}, Start In to {Path.GetDirectoryName(suggestedDesktopPath) ?? "desktop folder"}, and use --mo2-launcher (MO2) or --vortex-launcher (Vortex). Use 'Copy Mod Manager setup' for a ready-to-paste fix."));
        }
        else
        {
            checks.Add(new RuntimeReadinessCheck(
                "Mod manager executable target",
                "OK",
                $"Desktop executable target looks valid ({executableName})."));
        }

        if (!_launchOptions.FromModOrganizerLauncher && mo2EnvironmentDetected)
        {
            var launcherInputDetected =
                !string.IsNullOrWhiteSpace(_launchOptions.StartupInputPath) ||
                !string.IsNullOrWhiteSpace(_launchOptions.StartupOutputDirectory);
            checks.Add(new RuntimeReadinessCheck(
                "Mod manager launcher arguments",
                launcherInputDetected ? "OK" : "Warning",
                launcherInputDetected
                    ? "Launcher startup arguments were detected from mod manager context even without an explicit launcher flag. Keep Binary and Start in on the desktop executable folder so MO2's VFS can inject correctly."
                    : "Mod manager environment variables were detected but a launcher flag was not present. Add --mo2-launcher (MO2) or --vortex-launcher (Vortex), and keep Binary + Start in on the desktop executable folder so MO2's VFS can hook before startup."));
        }
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
        AutoSizeListViewColumns(_readinessListView, 180, 110, 420);
    }

    private void RunSelfCheck()
    {
        var checks = CreateDesktopReadinessReport();
        PopulateReadinessTab(checks);
        _resultsTabControl.SelectedTab = _readinessTabPage;
        var summary = string.Join(", ", checks.Select(static check => $"{check.Status}:{check.Area}"));
        AppendLog($"Self-check completed — {summary}");
        var errorCount = checks.Count(static check => check.Status.Equals("Error", StringComparison.OrdinalIgnoreCase));
        var warningCount = checks.Count(static check => check.Status.Equals("Warning", StringComparison.OrdinalIgnoreCase));
        var infoCount = checks.Count(static check => check.Status.Equals("Info", StringComparison.OrdinalIgnoreCase));
        _statusLabel.Text = errorCount > 0
            ? $"Readiness self-check completed — {errorCount} error(s), {warningCount} warning(s), {infoCount} info note(s)."
            : warningCount > 0 || infoCount > 0
                ? $"Readiness self-check completed — {warningCount} warning(s), {infoCount} info note(s)."
                : "Readiness self-check completed — no warnings.";
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
            Filter = "Armor Files (*.nif;*.esp;*.esm;*.esl;*.zip;*.7z;*.rar;*.tar;*.tar.gz;*.tgz)|*.nif;*.esp;*.esm;*.esl;*.zip;*.7z;*.rar;*.tar;*.tar.gz;*.tgz|All Files (*.*)|*.*",
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

    private void BrowseSkeletonSupportFile()
    {
        using var fileDialog = new OpenFileDialog
        {
            Title = "Select a skeleton support file",
            Filter = "Skeleton support files (*.nif;*.pex)|*.nif;*.pex|NIF Files (*.nif)|*.nif|Papyrus Scripts (*.pex)|*.pex|All Files (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false,
        };

        if (fileDialog.ShowDialog(this) == DialogResult.OK)
        {
            _skeletonNifTextBox.Text = fileDialog.FileName;
        }
    }

    private void BrowseSkeletonSupportFolder()
    {
        using var folderDialog = new FolderBrowserDialog
        {
            Description = "Select an XP32/XPMSSE or other skeleton-support mod folder",
            UseDescriptionForTitle = true,
        };

        if (folderDialog.ShowDialog(this) == DialogResult.OK)
        {
            _skeletonNifTextBox.Text = folderDialog.SelectedPath;
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

        if (sender == _skeletonNifTextBox)
        {
            var chosen = dropped.FirstOrDefault(static path => File.Exists(path) || Directory.Exists(path));
            if (string.IsNullOrWhiteSpace(chosen))
            {
                return;
            }

            _skeletonNifTextBox.Text = chosen;
            AppendLog($"Skeleton support path selected: {chosen}");
            return;
        }

        if (sender == _outputTextBox)
        {
            var chosen = dropped
                .Select(static path => Directory.Exists(path)
                    ? path
                    : File.Exists(path)
                        ? Path.GetDirectoryName(path)
                        : null)
                .FirstOrDefault(static path => !string.IsNullOrWhiteSpace(path));
            if (string.IsNullOrWhiteSpace(chosen))
            {
                return;
            }

            _outputTextBox.Text = chosen;
            UpdatePathActionStates();
            UpdateOutputHint();
            AppendLog($"Output folder selected: {chosen}");
            return;
        }

        if (sender == _cachePathTextBox)
        {
            var chosen = dropped.FirstOrDefault(static path => File.Exists(path) || Directory.Exists(path));
            if (string.IsNullOrWhiteSpace(chosen))
            {
                return;
            }

            _cachePathTextBox.Text = Directory.Exists(chosen)
                ? Path.Combine(chosen, ".conversion-learning-cache.json")
                : chosen;
            AppendLog($"Learning cache path selected: {_cachePathTextBox.Text}");
            return;
        }

        var inputCandidate = dropped.FirstOrDefault(static path => File.Exists(path) || Directory.Exists(path));
        if (string.IsNullOrWhiteSpace(inputCandidate))
        {
            return;
        }

        if (TryResolveSupportOnlyInputScanRoot(inputCandidate, out var scanRoot, out var supportFileName))
        {
            _inputTextBox.Text = scanRoot;
            AppendLog($"Support file '{supportFileName}' selected. Using containing folder as input scan root: {scanRoot}");
        }
        else
        {
            _inputTextBox.Text = inputCandidate;
        }
        UpdatePathActionStates();
        AppendLog($"Input selected: {_inputTextBox.Text}");
    }

    private void ApplySuggestedMixedGenderTargets()
    {
        var suggestedTargets = GetSuggestedMixedGenderTargets();
        if (suggestedTargets.Count == 0)
        {
            MessageBox.Show(
                this,
                "No built-in mixed female/male target suggestion is available right now. Switch to Manual mode and enter the TO bodies you want in the batch list.",
                "Mixed target shortcut",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        _useCustomTargetRadio.Checked = true;
        _targetBatchTextBox.Text = string.Join(", ", suggestedTargets);

        var primaryTarget = suggestedTargets[0];
        var targetIndex = _targetComboBox.FindStringExact(primaryTarget);
        if (targetIndex >= 0)
        {
            _targetComboBox.SelectedIndex = targetIndex;
        }
        else
        {
            _targetComboBox.Text = primaryTarget;
        }

        AppendLog($"Manual mixed female/male targets selected: {string.Join(", ", suggestedTargets)}.");
    }

    private void ApplyRecommendedUiSetup()
    {
        _usePresetRadio.Checked = true;
        if (_presetComboBox.Items.Count > 0 && _presetComboBox.SelectedIndex < 0)
        {
            _presetComboBox.SelectedIndex = 0;
        }

        _presetBatchTextBox.Text = string.Empty;
        _targetBatchTextBox.Text = string.Empty;
        _sourceComboBox.Text = "(auto)";
        _profileComboBox.SelectedIndex = 0;
        _physicsComboBox.SelectedIndex = 0;
        _worldModeComboBox.SelectedIndex = 0;
        _skeletonNifTextBox.Text = string.Empty;
        _cachePathTextBox.Text = string.Empty;

        RefreshModeState();
        UpdateSourceDetails();
        AppendLog("Recommended setup applied: preset mode with auto source, shape, physics, and world settings.");
    }

    private void ApplyAutoMappedTargetFromSource()
    {
        var sourceHint = string.Equals(_sourceComboBox.Text?.Trim(), "(auto)", StringComparison.OrdinalIgnoreCase)
            ? _autoDetectedSourceBody
            : _sourceComboBox.Text?.Trim();
        var mappedTarget = ResolveAutoMappedTargetFromSource(sourceHint);
        if (string.IsNullOrWhiteSpace(mappedTarget))
        {
            MessageBox.Show(
                this,
                "Could not auto-map a TO body from the current FROM body hint yet. Pick a target manually or inspect input first for better source detection.",
                "Auto-map destination body",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        _useCustomTargetRadio.Checked = true;
        _targetBatchTextBox.Text = string.Empty;
        var index = _targetComboBox.FindStringExact(mappedTarget);
        if (index >= 0)
        {
            _targetComboBox.SelectedIndex = index;
        }
        else
        {
            _targetComboBox.Text = mappedTarget;
        }

        UpdateTargetDetails();
        UpdatePhysicsDetails();
        UpdateOutputHint();
        AppendLog($"Auto-mapped TO body '{mappedTarget}' from FROM body hint '{sourceHint ?? "(auto)"}'.");
    }

    private static string? ResolveAutoMappedTargetFromSource(string? sourceHint)
    {
        var canonicalSource = BodyTypeCatalog.ResolveName(sourceHint);
        if (!string.IsNullOrWhiteSpace(canonicalSource) &&
            BodyTypeCatalog.All.Any(body => body.Name.Equals(canonicalSource, StringComparison.OrdinalIgnoreCase)))
        {
            return canonicalSource;
        }

        if (!string.IsNullOrWhiteSpace(canonicalSource) &&
            BodyTypeCatalog.TryGetGender(canonicalSource, out var sourceGender))
        {
            return string.Equals(sourceGender, "female", StringComparison.OrdinalIgnoreCase)
                ? ResolveSuggestedTargetForGender(null, sourceGender, "3BA", "CBBE", "UUNP", "BHUNP", "UNP")
                : ResolveSuggestedTargetForGender(null, sourceGender, "HIMBO", "SAM Light", "SAM", "SOS", "TNG");
        }

        return null;
    }

    private IReadOnlyList<string> GetSuggestedMixedGenderTargets()
    {
        var currentTarget = BodyTypeCatalog.ResolveName(ResolveProfileTargetName());
        var femaleTarget = ResolveSuggestedTargetForGender(currentTarget, "female", "3BA", "CBBE", "UUNP", "BHUNP", "UNP");
        var maleTarget = ResolveSuggestedTargetForGender(currentTarget, "male", "HIMBO", "SAM Light", "SAM", "SOS", "TNG");

        return [.. new[] { femaleTarget, maleTarget }
            .Where(static target => !string.IsNullOrWhiteSpace(target))
            .Select(static target => target!)
            .Distinct(StringComparer.OrdinalIgnoreCase)];
    }

    private static string? ResolveSuggestedTargetForGender(string? currentTarget, string gender, params string[] preferredFallbacks)
    {
        if (!string.IsNullOrWhiteSpace(currentTarget) &&
            BodyTypeCatalog.TryGetGender(currentTarget, out var currentGender) &&
            string.Equals(currentGender, gender, StringComparison.OrdinalIgnoreCase))
        {
            return currentTarget;
        }

        foreach (var fallback in preferredFallbacks)
        {
            if (BodyTypeCatalog.All.Any(body =>
                    string.Equals(body.Name, fallback, StringComparison.OrdinalIgnoreCase)) &&
                BodyTypeCatalog.TryGetGender(fallback, out var fallbackGender) &&
                string.Equals(fallbackGender, gender, StringComparison.OrdinalIgnoreCase))
            {
                return fallback;
            }
        }

        return BodyTypeCatalog.All
            .Select(static body => body.Name)
            .FirstOrDefault(body =>
                BodyTypeCatalog.TryGetGender(body, out var candidateGender) &&
                string.Equals(candidateGender, gender, StringComparison.OrdinalIgnoreCase));
    }

    private void RefreshModeState()
    {
        var usingPreset = _usePresetRadio.Checked;
        _presetComboBox.Enabled = usingPreset;
        _presetBatchTextBox.Enabled = usingPreset;
        _targetComboBox.Enabled = !usingPreset;
        _targetComboBox.Visible = !usingPreset;
        _presetTargetTextBox.Visible = usingPreset;
        _targetBatchTextBox.Enabled = !usingPreset;
        _targetSelectionLabel.Text = usingPreset
            ? "TO body from preset"
            : "TO body / destination body";
        _targetBatchLabel.Text = usingPreset
            ? "Manual multi-target list (switches to manual mode)"
            : "Destination body batch list (optional)";
        _modeStatusLabel.Text = usingPreset
            ? "Preset mode is active. The selected preset chooses the TO body below for you. Switch to Manual mode above if you want to change the TO body yourself."
            : "Manual mode is active. Use the TO body box below to choose the converted output body. FROM body stays in the Source hints section.";
        if (usingPreset && TryGetSelectedPreset(out var preset))
        {
            _presetTargetTextBox.Text = preset.TargetBody;
            var targetIndex = _targetComboBox.FindStringExact(preset.TargetBody);
            if (targetIndex >= 0)
            {
                try
                {
                    _suppressTargetSelectionChanged = true;
                    if (_targetComboBox.SelectedIndex != targetIndex)
                    {
                        _targetComboBox.SelectedIndex = targetIndex;
                    }
                }
                finally
                {
                    _suppressTargetSelectionChanged = false;
                }
            }
        }
        else if (!usingPreset)
        {
            _presetTargetTextBox.Text = string.Empty;
        }

        _targetModeHintLabel.Text = usingPreset
            ? "Preset mode locks the TO body to the preset above. The editable FROM body is in the Source hints section on the right. Need a mixed female/male or cross-body pack? Use the shortcut below or switch to Manual mode and enter multiple TO bodies like 3BA, HIMBO."
            : "Manual mode lets you choose the TO body directly. Use the batch list for multiple outputs or mixed female/male packs such as 3BA, HIMBO.";

        UpdatePresetDetails();
        UpdateTargetDetails();
        UpdatePhysicsDetails();
        UpdateOutputHint();
        ApplyAdvancedOptionsVisibility();
        UpdateBodySelectionSummary();
    }

    private void ApplyAdvancedOptionsVisibility()
    {
        var showAdvanced = _showAdvancedOptionsCheckBox?.Checked ?? false;
        if (_sourceHintsGroupBox is not null && !_sourceHintsGroupBox.IsDisposed)
        {
            _sourceHintsGroupBox.Visible = true;
        }

        if (_customProfilesGroupBox is not null && !_customProfilesGroupBox.IsDisposed)
        {
            _customProfilesGroupBox.Visible = showAdvanced;
        }

        _modeStatusLabel.Text = showAdvanced
            ? "Advanced options are visible. FROM body = original armor body hint, TO body = converted output body target."
            : "Quick layout is active. FROM body stays visible so you can always verify source hints while choosing TO targets.";
        UpdateMainSplitLayout();
    }

    private void OpenCreatorSupportLink()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = CreatorSupportLink,
                UseShellExecute = true
            });
            AppendLog($"Opened creator support link: {CreatorSupportLink}");
        }
        catch (Exception ex)
        {
            AppendLog($"Could not open creator support link: {ex.Message}");
            MessageBox.Show(
                this,
                $"Could not open link automatically.\n{CreatorSupportLink}",
                "Open support link",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
    }

    private string GetRandomCreatorSupportTooltip()
    {
        if (CreatorSupportTooltips.Length == 0)
        {
            return "Support the creator";
        }

        if (CreatorSupportTooltips.Length == 1)
        {
            return CreatorSupportTooltips[0];
        }

        var index = _lastCreatorTooltipIndex;
        while (index == _lastCreatorTooltipIndex)
        {
            index = Random.Shared.Next(CreatorSupportTooltips.Length);
        }

        _lastCreatorTooltipIndex = index;
        return CreatorSupportTooltips[index];
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
        var sourceOverride = DesktopWorkflowSupport.ResolveSourceBodyOverride(
            _sourceComboBox.Text,
            _sourceComboBox.SelectedItem?.ToString(),
            _autoDetectedSourceBody);
        if (!TryResolveSkeletonSupportPath(ReadOptionalPathValue(_skeletonNifTextBox.Text), out var skeletonNifPath))
        {
            return;
        }

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

        if (TryResolveSupportOnlyInputScanRoot(input, out var supportScanRoot, out var supportFileName))
        {
            input = supportScanRoot;
            AppendLog($"Conversion input resolved support file '{supportFileName}' to scan root: {supportScanRoot}");
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

        if (!usingPreset && !ConfirmManualTargetBodies(selectedTargets))
        {
            return;
        }

        if (PruneMissingCustomProfiles("conversion"))
        {
            return;
        }

        _activeConversion = new CancellationTokenSource();
        SetBusyState(isBusy: true);
        _cancelButton.Text = "Cancel";
        ShowBusyProgress($"Preparing conversion: {Path.GetFileName(input)}");
        AppendLog(usingPreset
            ? $"Starting conversion (presets: {string.Join(", ", selectedPresets)})..."
            : $"Starting conversion (destination bodies: {string.Join(", ", selectedTargets)})...");
        AppendLog($"Output folder: {ResolveEffectiveOutputDirectoryPreview()}");
        await Task.Yield();

        var runtimeStressInputType = DetectInputType(input);
        var runtimeStressInputSizeBytes = TryGetInputSizeBytes(input);
        var runtimeStressLargeInputDetected = runtimeStressInputSizeBytes is { } bytes && bytes >= LargeInputStressThresholdBytes;
        var runtimeStressIsCandidate = runtimeStressInputType is "folder" or "zip" or "7z";
        var runtimeStressProgressUiUpdateCount = 0;
        var runtimeStressUiGapTotalMs = 0d;
        var runtimeStressUiGapSampleCount = 0;
        var runtimeStressUiGapMaxMs = 0d;
        var runtimeStressArchiveThroughputSamples = new List<double>();
        string? runtimeStressArchiveFormat = null;
        long? runtimeStressArchiveBytesCopied = null;
        long? runtimeStressArchiveBytesEstimated = null;
        DateTime? runtimeStressLastUiUpdateUtc = null;
        string runtimeStressOutcome = "running";
        _conversionCancellationRequestedAtUtc = null;

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
            string? lastProgressStatus = null;
            var lastProgressUiUpdateUtc = DateTime.MinValue;
            var lastProgressLogUtc = DateTime.MinValue;
            string? lastProgressLogSignature = null;
            var conversionStartedUtc = DateTime.UtcNow;
            string? activeStageKey = null;
            string? activeStageName = null;
            DateTime? activeStageStartedUtc = null;
            var completedStageDurationSamples = new Dictionary<string, (double TotalSeconds, int Count)>(StringComparer.OrdinalIgnoreCase);
            ResetPipelineTimeline();
            var progress = new CoalescingBatchProgress(this, update =>
            {
                var total = Math.Max(1, update.Total);
                var completed = Math.Clamp(update.Completed, 0, total);
                double progressUnits = completed;
                var isArchiveExtractionStage = IsArchiveExtractionStage(update.Stage);
                if (!update.IsItemCompleted && isArchiveExtractionStage &&
                    update.ExtractionTotalBytesEstimated is > 0 &&
                    update.ExtractionTotalBytesCopied is >= 0)
                {
                    var byteStageFraction = Math.Clamp(
                        update.ExtractionTotalBytesCopied.Value / (double)update.ExtractionTotalBytesEstimated.Value,
                        0d,
                        1d);
                    progressUnits = Math.Min(total, completed + byteStageFraction);
                }
                else if (!update.IsItemCompleted && update.StageCount > 0)
                {
                    var stageFraction = Math.Clamp((double)update.StageIndex / update.StageCount, 0d, 1d);
                    progressUnits = Math.Min(total, completed + stageFraction);
                }

                var percent = (int)Math.Round(progressUnits / total * 100d, MidpointRounding.AwayFromZero);
                var activeItem = update.IsItemCompleted
                    ? completed
                    : Math.Min(total, Math.Max(1, completed + 1));
                var stageLogLabel = BuildProgressLogStage(update);
                var statusSuffix = string.IsNullOrWhiteSpace(update.Stage)
                    ? update.CurrentFile
                    : $"{update.CurrentFile} — {stageLogLabel}";
                var now = DateTime.UtcNow;
                if (isArchiveExtractionStage)
                {
                    runtimeStressArchiveFormat ??= string.IsNullOrWhiteSpace(update.ExtractionArchiveFormat)
                        ? null
                        : update.ExtractionArchiveFormat.Trim();
                    if (update.ExtractionTotalBytesCopied is { } copiedBytes)
                    {
                        runtimeStressArchiveBytesCopied = runtimeStressArchiveBytesCopied is { } existingCopied
                            ? Math.Max(existingCopied, copiedBytes)
                            : copiedBytes;
                    }

                    if (update.ExtractionTotalBytesEstimated is { } estimatedBytes && estimatedBytes > 0)
                    {
                        runtimeStressArchiveBytesEstimated = runtimeStressArchiveBytesEstimated is { } existingEstimated
                            ? Math.Max(existingEstimated, estimatedBytes)
                            : estimatedBytes;
                    }

                    if (update.ExtractionThroughputMiBPerSecond is > 0d and var throughput)
                    {
                        runtimeStressArchiveThroughputSamples.Add(throughput);
                    }
                }
                var uiRefreshInterval = isArchiveExtractionStage
                    ? ArchiveProgressUiRefreshInterval
                    : TimeSpan.FromMilliseconds(250);
                var shouldRefreshUi = update.IsItemCompleted ||
                    !string.Equals(statusSuffix, lastProgressStatus, StringComparison.Ordinal) ||
                    now - lastProgressUiUpdateUtc >= uiRefreshInterval;
                if (!shouldRefreshUi)
                {
                    return;
                }

                if (runtimeStressLastUiUpdateUtc is { } previousUiUpdateUtc)
                {
                    var gapMs = (now - previousUiUpdateUtc).TotalMilliseconds;
                    runtimeStressUiGapTotalMs += gapMs;
                    runtimeStressUiGapSampleCount++;
                    if (gapMs > runtimeStressUiGapMaxMs)
                    {
                        runtimeStressUiGapMaxMs = gapMs;
                    }
                }

                runtimeStressLastUiUpdateUtc = now;
                runtimeStressProgressUiUpdateCount++;

                lastProgressStatus = statusSuffix;
                lastProgressUiUpdateUtc = now;
                if (_progressBar.Style != ProgressBarStyle.Continuous)
                {
                    _progressBar.Style = ProgressBarStyle.Continuous;
                    _progressBar.MarqueeAnimationSpeed = 0;
                }
                _progressBar.Maximum = 100;
                _progressBar.Value = Math.Clamp(percent, 0, 100);
                var stageDisplay = BuildProgressStageDisplay(update);
                var normalizedStageForTracking = isArchiveExtractionStage
                    ? "Extracting archive"
                    : update.Stage;
                var stageKey = $"{activeItem}|{update.CurrentFile}|{normalizedStageForTracking}";
                if (!string.Equals(stageKey, activeStageKey, StringComparison.Ordinal))
                {
                    if (activeStageStartedUtc is { } previousStageStartedUtc &&
                        !string.IsNullOrWhiteSpace(activeStageName))
                    {
                        var previousStageElapsed = now - previousStageStartedUtc;
                        if (previousStageElapsed > TimeSpan.Zero)
                        {
                            if (completedStageDurationSamples.TryGetValue(activeStageName, out var existingSample))
                            {
                                completedStageDurationSamples[activeStageName] = (
                                    existingSample.TotalSeconds + previousStageElapsed.TotalSeconds,
                                    existingSample.Count + 1);
                            }
                            else
                            {
                                completedStageDurationSamples[activeStageName] = (previousStageElapsed.TotalSeconds, 1);
                            }
                        }
                    }

                    activeStageKey = stageKey;
                    activeStageStartedUtc = now;
                    activeStageName = NormalizeProgressStageName(update.Stage);
                }

                var logStage = stageLogLabel;
                var logSignature = isArchiveExtractionStage
                    ? $"{activeItem}/{total}|Extracting archive"
                    : $"{activeItem}/{total}|{logStage}";
                var logInterval = isArchiveExtractionStage ? ArchiveProgressLogInterval : TimeSpan.FromSeconds(5);
                if (!string.Equals(logSignature, lastProgressLogSignature, StringComparison.Ordinal) ||
                    now - lastProgressLogUtc >= logInterval)
                {
                    AppendLog($"Progress {percent}% ({activeItem}/{total}): {logStage}");
                    lastProgressLogUtc = now;
                    lastProgressLogSignature = logSignature;
                }

                var overallElapsed = now - conversionStartedUtc;
                var overallEta = EstimateRemainingDuration(conversionStartedUtc, now, progressUnits, total);
                var stageElapsed = activeStageStartedUtc is { } startedUtc ? now - startedUtc : (TimeSpan?)null;
                var stageEta = EstimateStageRemainingDuration(activeStageName, stageElapsed, completedStageDurationSamples, update.IsItemCompleted);
                _statusLabel.Text = $"Converting {activeItem}/{total} ({percent}%): {stageDisplay}";
                _progressDetailsLabel.Text = $"Overall {percent}% • Item {activeItem}/{total} • Elapsed {FormatDuration(overallElapsed)} • Remaining {FormatDuration(overallEta)} • {statusSuffix}";
                UpdatePipelineTimeline(update, stageElapsed, stageEta, overallElapsed, overallEta);

                if (update.IsItemCompleted)
                {
                    AppendLog($"Completed {completed}/{total}: {update.CurrentFile} ({(update.Success ? "ok" : "review needed")}).");
                }
            });

            IReadOnlyList<ConversionResult> results;
            using (progress)
            {
                results = await Task.Run(
                    () => _batchRunner.ConvertAsync(request, cancellationToken, progress),
                    cancellationToken);
            }

            ShowProgressValue(Math.Max(_progressBar.Value, 88), "Conversion finished. Loading preview...");
            await Task.Yield();
            _lastOutputDirectory = GetBestOutputDirectory(results);
            _lastPreviewPath = GetFirstExistingOutputFile(results, PreviewFileCandidates);
            _lastBatchReportPath = GetFirstExistingOutputFile(results, "batch-report.json");
            UpdatePathActionStates();
            _ = await LoadPreviewInAppWithTimeoutAsync(_lastPreviewPath);
            ShowProgressValue(Math.Max(_progressBar.Value, 92), "Preview ready. Building overview and report views...");
            await Task.Yield();
            var workflowSnapshot = await BuildWorkflowSnapshotAsync(results, _lastPreviewPath, cancellationToken);
            PopulateSummaryTab(workflowSnapshot.SummaryRows);
            PopulateReportsTab(workflowSnapshot.ReportMetrics);
            ShowProgressValue(Math.Max(_progressBar.Value, 96), "Reports ready. Building files and next actions...");
            await Task.Yield();
            PopulateArtifactsTab(workflowSnapshot.Artifacts);
            var guidanceNeedsReview = await PopulateGuidanceTabAsync(results, _lastPreviewPath, cancellationToken);
            ApplyValidationGatePresentation(workflowSnapshot.ValidationState, guidanceNeedsReview);
            ShowProgressValue(100, guidanceNeedsReview
                ? "Conversion complete. Review next actions before shipping."
                : "Conversion complete. Preview and reports are ready.");

            _resultsTabControl.SelectedTab = guidanceNeedsReview
                ? _summaryTabPage
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
                var displayedSteps = result.Steps.Take(MaxLoggedStepsPerResult).ToList();
                foreach (var step in displayedSteps)
                {
                    builder.AppendLine($"  - {step}");
                }

                if (result.Steps.Count > displayedSteps.Count)
                {
                    builder.AppendLine($"  - ... {result.Steps.Count - displayedSteps.Count} additional steps omitted from the desktop log for responsiveness.");
                }
                AppendLog(builder.ToString().TrimEnd());
            }

            AppendLog(BuildValidationOutcomeLogMessage(
                results.Select(static result => result.OutputDirectory).ToArray(),
                _lastPreviewPath,
                guidanceNeedsReview));
            runtimeStressOutcome = guidanceNeedsReview ? "completed-needs-review" : "completed";
            MessageBox.Show(this, "Conversion complete.", "SlideSmith", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (OperationCanceledException)
        {
            AppendLog("Conversion cancelled.");
            _statusLabel.Text = "Conversion cancelled.";
            runtimeStressOutcome = "cancelled";
            if (_conversionCancellationRequestedAtUtc is { } requestedAtUtc)
            {
                var latencyMs = (DateTimeOffset.UtcNow - requestedAtUtc).TotalMilliseconds;
                AppendLog($"Cancellation latency: {latencyMs:0} ms from user request to stop.");
            }
        }
        catch (Exception ex)
        {
            AppendLog($"Conversion failed: {ex.Message}");
            _statusLabel.Text = "Conversion failed.";
            runtimeStressOutcome = "failed";
            MessageBox.Show(this, $"Conversion failed:\n{ex.Message}", "SlideSmith", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            if (runtimeStressIsCandidate && _lastOutputDirectory is not null)
            {
                var avgGapMs = runtimeStressUiGapSampleCount > 0
                    ? runtimeStressUiGapTotalMs / runtimeStressUiGapSampleCount
                    : (double?)null;
                var cancellationLatencyMs = _conversionCancellationRequestedAtUtc is { } requestedAtUtc
                    ? (double?)Math.Max(0d, (DateTimeOffset.UtcNow - requestedAtUtc).TotalMilliseconds)
                    : null;
                var archiveP10Throughput = CalculatePercentile(runtimeStressArchiveThroughputSamples, 10d);
                var archiveP50Throughput = CalculatePercentile(runtimeStressArchiveThroughputSamples, 50d);
                var archiveP90Throughput = CalculatePercentile(runtimeStressArchiveThroughputSamples, 90d);
                var archiveThroughputBaseline = GetArchiveThroughputBaseline(runtimeStressArchiveFormat ?? runtimeStressInputType);
                var archiveThroughputBelowBaseline = archiveThroughputBaseline is { } baselineResult &&
                                                    archiveP10Throughput is { } p10 &&
                                                    p10 < baselineResult.BaselineMiBPerSecond;
                if (archiveThroughputBelowBaseline)
                {
                    AppendLog($"Archive extraction throughput warning: P10 {archiveP10Throughput:0.##} MiB/s is below sampled {archiveThroughputBaseline?.HardwareTier ?? "unknown"}-tier baseline {archiveThroughputBaseline?.BaselineMiBPerSecond:0.##} MiB/s for {runtimeStressArchiveFormat ?? runtimeStressInputType}.");
                }

                var runtimeStressReport = new RuntimeStressPassReport(
                    InputPath: input,
                    InputType: runtimeStressInputType,
                    InputSizeBytes: runtimeStressInputSizeBytes,
                    LargeInputDetected: runtimeStressLargeInputDetected,
                    ProgressUiUpdateCount: runtimeStressProgressUiUpdateCount,
                    AverageUiUpdateGapMilliseconds: avgGapMs,
                    MaxUiUpdateGapMilliseconds: runtimeStressUiGapSampleCount > 0 ? runtimeStressUiGapMaxMs : null,
                    CancellationLatencyMilliseconds: cancellationLatencyMs,
                    ArchiveFormat: runtimeStressArchiveFormat,
                    ArchiveProgressSampleCount: runtimeStressArchiveThroughputSamples.Count,
                    ArchiveBytesCopied: runtimeStressArchiveBytesCopied,
                    ArchiveBytesEstimated: runtimeStressArchiveBytesEstimated,
                    ArchiveThroughputP10MiBPerSecond: archiveP10Throughput,
                    ArchiveThroughputP50MiBPerSecond: archiveP50Throughput,
                    ArchiveThroughputP90MiBPerSecond: archiveP90Throughput,
                    ArchiveThroughputBaselineMiBPerSecond: archiveThroughputBaseline?.BaselineMiBPerSecond,
                    ArchiveThroughputHardwareTier: archiveThroughputBaseline?.HardwareTier,
                    ArchiveThroughputBaselineSampleCount: archiveThroughputBaseline?.SampleCount ?? 0,
                    ArchiveThroughputBelowBaseline: archiveThroughputBelowBaseline,
                    Outcome: runtimeStressOutcome,
                    RecordedAtUtc: DateTimeOffset.UtcNow);
                WriteRuntimeStressPassReport(_lastOutputDirectory, runtimeStressReport);
            }

            _activeConversion?.Dispose();
            _activeConversion = null;
            _conversionCancellationRequestedAtUtc = null;
            SetBusyState(isBusy: false);
        }
    }

    private void ScheduleAutoInspectInput()
    {
        var input = _inputTextBox.Text.Trim();
        if (!ShouldAutoInspectInputPath(input))
        {
            _autoInspectDebounce?.Cancel();
            _autoInspectDebounce?.Dispose();
            _autoInspectDebounce = null;
            if (!string.IsNullOrWhiteSpace(input))
            {
                _statusLabel.Text = "Ready — click “Inspect input now” (next to the Input field) for archive or folder analysis.";
            }
            return;
        }

        _autoInspectDebounce?.Cancel();
        _autoInspectDebounce?.Dispose();
        _autoInspectDebounce = new CancellationTokenSource();
        _ = RunAutoInspectInputAsync(_autoInspectDebounce.Token);
    }

    private async Task RunAutoInspectInputAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(AutoInspectDebounceMilliseconds, cancellationToken);
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            var input = _inputTextBox.Text.Trim();
            if (!ShouldAutoInspectInputPath(input))
            {
                return;
            }

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(AutoInspectTimeout);
            await InspectInputAsync(
                showDialogs: false,
                switchToInspectTab: false,
                automaticTrigger: true,
                externalCancellationToken: timeoutCts.Token);
        }
        catch (OperationCanceledException)
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                _statusLabel.Text = "Auto-inspection timed out. Click “Inspect input now” (next to the Input field) to run full analysis.";
                ClearInspectionTab("Auto-inspection timed out. Click “Inspect input now” (next to the Input field) for a full pass.");
                AppendLog("Auto-inspection timed out to keep the UI responsive.");
            }
        }
    }

    private async Task InspectInputAsync(
        bool showDialogs,
        bool switchToInspectTab,
        bool automaticTrigger,
        CancellationToken externalCancellationToken = default)
    {
        if (_activeConversion is not null)
        {
            return;
        }

        var input = _inputTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(input))
        {
            if (showDialogs)
            {
                MessageBox.Show(this, "Please select an input file/folder first.", "Missing input", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            return;
        }

        if (!File.Exists(input) && !Directory.Exists(input))
        {
            if (showDialogs)
            {
                MessageBox.Show(this, "Input path was not found.", "Invalid input", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            return;
        }

        if (PruneMissingCustomProfiles("inspection"))
        {
            return;
        }

        if (TryResolveSupportOnlyInputScanRoot(input, out var supportScanRoot, out var supportFileName))
        {
            input = supportScanRoot;
            if (!automaticTrigger)
            {
                AppendLog($"Inspect input resolved support file '{supportFileName}' to scan root: {supportScanRoot}");
            }
        }

        _activeConversion = CancellationTokenSource.CreateLinkedTokenSource(externalCancellationToken);
        try
        {
            if (!TryResolveSkeletonSupportPath(ReadOptionalPathValue(_skeletonNifTextBox.Text), out var skeletonNifPath))
            {
                return;
            }

            if (!automaticTrigger)
            {
                SetBusyState(isBusy: true);
            }
            ShowBusyProgress(automaticTrigger ? "Auto-inspecting input..." : "Inspecting input...");
            ClearInspectionTab(automaticTrigger ? "Auto-inspecting input..." : "Inspecting input...");
            var inspectionTargetBody = ResolveInspectionTargetBody();
            var customProfilePaths = _customProfilePaths.Count > 0 ? _customProfilePaths.ToArray() : null;

            var inspection = await Task.Run(
                async () => await _inspector.InspectAsync(
                    input,
                    inspectionTargetBody,
                    customProfilePaths,
                    _activeConversion.Token,
                    skeletonNifPath: skeletonNifPath),
                _activeConversion.Token);

            PopulateInspectionTab(inspection);
            ApplyDetectedSourceBodySelection(inspection.Detection);
            if (switchToInspectTab)
            {
                _resultsTabControl.SelectedTab = _inspectTabPage;
            }
            _statusLabel.Text = automaticTrigger ? "Input auto-inspection complete." : "Inspection complete.";
            AppendLog(automaticTrigger
                ? $"Input auto-inspection updated: body={inspection.Detection.Body} ({inspection.Detection.Confidence:P0}), mesh={inspection.Analysis.MeshType}."
                : $"Inspection complete: body={inspection.Detection.Body} ({inspection.Detection.Confidence:P0}), mesh={inspection.Analysis.MeshType}.");
        }
        catch (OperationCanceledException)
        {
            ClearInspectionTab("Inspection cancelled.");
            _statusLabel.Text = "Inspection cancelled.";
            if (!automaticTrigger)
            {
                AppendLog("Inspection cancelled.");
            }
        }
        catch (Exception ex)
        {
            ClearInspectionTab($"Inspection failed: {ex.Message}");
            _statusLabel.Text = "Inspection failed.";
            if (showDialogs)
            {
                MessageBox.Show(this, $"Failed to inspect input:\n{ex.Message}", "Inspect input", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            else
            {
                AppendLog($"Input auto-inspection failed: {ex.Message}");
            }
        }
        finally
        {
            _activeConversion?.Dispose();
            _activeConversion = null;
            if (!automaticTrigger)
            {
                SetBusyState(isBusy: false);
            }
            else
            {
                _inspectInputButton.Enabled = InputPathExists();
                _convertButton.Enabled = CanStartConversion();
            }
        }
    }

    private static bool ShouldAutoInspectInputPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var normalized = path.Trim();
        if (Directory.Exists(normalized))
        {
            return false;
        }

        if (!File.Exists(normalized))
        {
            return false;
        }

        return normalized.EndsWith(".nif", StringComparison.OrdinalIgnoreCase) ||
               normalized.EndsWith(".esp", StringComparison.OrdinalIgnoreCase) ||
               normalized.EndsWith(".esm", StringComparison.OrdinalIgnoreCase) ||
               normalized.EndsWith(".esl", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryResolveSupportOnlyInputScanRoot(string inputPath, out string scanRoot, out string supportFileName)
    {
        scanRoot = inputPath;
        supportFileName = string.Empty;
        if (string.IsNullOrWhiteSpace(inputPath) || !File.Exists(inputPath))
        {
            return false;
        }

        var extension = Path.GetExtension(inputPath);
        var isSupportOnly = extension.Equals(".xml", StringComparison.OrdinalIgnoreCase) ||
                            extension.Equals(".osp", StringComparison.OrdinalIgnoreCase) ||
                            extension.Equals(".osd", StringComparison.OrdinalIgnoreCase) ||
                            extension.Equals(".bsd", StringComparison.OrdinalIgnoreCase) ||
                            extension.Equals(".tri", StringComparison.OrdinalIgnoreCase);
        if (!isSupportOnly)
        {
            return false;
        }

        var directory = Path.GetDirectoryName(inputPath);
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            return false;
        }

        scanRoot = directory;
        supportFileName = Path.GetFileName(inputPath);
        return true;
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
            detection.Body.Equals("CUSTOM", StringComparison.OrdinalIgnoreCase) ||
            detection.Confidence < AutoDetectedSourceConfidenceFloor)
        {
            ClearAutoDetectedSourceHint(refreshDetails: true);
            if (IsSourceAutoSelection() &&
                detection.Confidence < AutoDetectedSourceConfidenceFloor &&
                !string.IsNullOrWhiteSpace(detection.Body))
            {
                AppendLog($"Auto-detect confidence too low ({detection.Confidence:P0}) to lock source body hint. Review Inspect details and set FROM body manually if needed.");
            }
            return;
        }

        var index = _sourceComboBox.FindStringExact(detection.Body);
        if (index < 0)
        {
            ClearAutoDetectedSourceHint(refreshDetails: true);
            return;
        }

        _autoDetectedSourceBody = detection.Body;
        _autoDetectedSourceConfidence = Math.Clamp(detection.Confidence, 0d, 1d);

        UpdateSourceDetails();
        AppendLog($"Auto-detected source body from inspection: {detection.Body} ({detection.Confidence:P0}).");
    }

    private bool IsSourceAutoSelection() =>
        DesktopWorkflowSupport.IsAutoSelectionText(_sourceComboBox.Text);

    private void CancelConversion()
    {
        if (_activeConversion is null)
        {
            return;
        }

        if (_activeConversion.IsCancellationRequested)
        {
            _statusLabel.Text = "Still cancelling — waiting for the current conversion step to stop.";
            return;
        }

        _cancelButton.Text = "Cancelling...";
        _conversionCancellationRequestedAtUtc = DateTimeOffset.UtcNow;
        _activeConversion.Cancel();
        AppendLog("Cancellation requested...");
        _statusLabel.Text = "Cancelling — waiting for the current conversion step to stop.";
    }

    private void ShowBusyProgress(string statusText)
    {
        _progressBar.Style = ProgressBarStyle.Marquee;
        _progressBar.MarqueeAnimationSpeed = 30;
        _progressBar.Minimum = 0;
        _progressBar.Maximum = 100;
        _progressBar.Value = 0;
        _statusLabel.Text = statusText;
        _progressDetailsLabel.Text = "Overall progress: starting…";
    }

    private void ShowProgressValue(int percent, string statusText)
    {
        _progressBar.Style = ProgressBarStyle.Continuous;
        _progressBar.MarqueeAnimationSpeed = 0;
        _progressBar.Minimum = 0;
        _progressBar.Maximum = 100;
        _progressBar.Value = Math.Clamp(percent, 0, 100);
        _statusLabel.Text = statusText;
        _progressDetailsLabel.Text = $"Overall progress: {_progressBar.Value}%";
    }

    private static string DetectInputType(string inputPath)
    {
        if (Directory.Exists(inputPath))
        {
            return "folder";
        }

        var extension = Path.GetExtension(inputPath);
        if (string.Equals(extension, ".zip", StringComparison.OrdinalIgnoreCase))
        {
            return "zip";
        }

        if (string.Equals(extension, ".7z", StringComparison.OrdinalIgnoreCase))
        {
            return "7z";
        }

        if (string.Equals(extension, ".rar", StringComparison.OrdinalIgnoreCase))
        {
            return "rar";
        }

        if (string.Equals(extension, ".tar", StringComparison.OrdinalIgnoreCase))
        {
            return "tar";
        }

        if (inputPath.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(extension, ".tgz", StringComparison.OrdinalIgnoreCase))
        {
            return "tar.gz";
        }

        return "file";
    }

    private static ArchiveThroughputBaselineResult? GetArchiveThroughputBaseline(string? archiveFormat)
    {
        if (string.IsNullOrWhiteSpace(archiveFormat))
        {
            return null;
        }

        var normalizedFormat = archiveFormat.Trim().ToLowerInvariant() switch
        {
            "tgz" => "tar.gz",
            var value => value
        };
        var hardwareTier = DetectRuntimeHardwareTier();
        var tierSamples = ArchiveThroughputSamples
            .Where(sample => sample.ArchiveFormat.Equals(normalizedFormat, StringComparison.OrdinalIgnoreCase) &&
                             sample.HardwareTier.Equals(hardwareTier, StringComparison.OrdinalIgnoreCase))
            .Select(sample => sample.ThroughputMiBPerSecond)
            .ToList();
        if (tierSamples.Count == 0)
        {
            tierSamples = ArchiveThroughputSamples
                .Where(sample => sample.ArchiveFormat.Equals(normalizedFormat, StringComparison.OrdinalIgnoreCase))
                .Select(sample => sample.ThroughputMiBPerSecond)
                .ToList();
        }

        if (tierSamples.Count == 0)
        {
            return null;
        }

        var baseline = CalculatePercentile(tierSamples, 25d);
        if (baseline is null)
        {
            return null;
        }

        var profiles = ArchiveThroughputSamples
            .Where(sample => sample.ArchiveFormat.Equals(normalizedFormat, StringComparison.OrdinalIgnoreCase) &&
                             sample.HardwareTier.Equals(hardwareTier, StringComparison.OrdinalIgnoreCase))
            .Select(sample => sample.Profile)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static value => value, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (profiles.Count == 0)
        {
            profiles = ArchiveThroughputSamples
                .Where(sample => sample.ArchiveFormat.Equals(normalizedFormat, StringComparison.OrdinalIgnoreCase))
                .Select(sample => sample.Profile)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(static value => value, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        return new ArchiveThroughputBaselineResult(
            ArchiveFormat: normalizedFormat,
            HardwareTier: hardwareTier,
            SampleCount: tierSamples.Count,
            BaselineMiBPerSecond: baseline.Value,
            SampleProfiles: profiles);
    }

    private static string DetectRuntimeHardwareTier()
    {
        try
        {
            var cpuThreads = Environment.ProcessorCount;
            var totalMemoryBytes = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
            var totalMemoryGiB = totalMemoryBytes > 0
                ? totalMemoryBytes / (1024d * 1024d * 1024d)
                : 0d;
            if (cpuThreads >= 16 && totalMemoryGiB >= 24d)
            {
                return "high";
            }

            if (cpuThreads >= 8 && totalMemoryGiB >= 12d)
            {
                return "mid";
            }
        }
        catch
        {
        }

        return "low";
    }

    private static double? CalculatePercentile(IReadOnlyList<double> values, double percentile)
    {
        if (values.Count == 0)
        {
            return null;
        }

        var sorted = values
            .Where(static value => !double.IsNaN(value) && !double.IsInfinity(value) && value > 0d)
            .OrderBy(static value => value)
            .ToList();
        if (sorted.Count == 0)
        {
            return null;
        }

        var normalizedPercentile = Math.Clamp(percentile, 0d, 100d) / 100d;
        var position = normalizedPercentile * (sorted.Count - 1);
        var lowerIndex = (int)Math.Floor(position);
        var upperIndex = (int)Math.Ceiling(position);
        if (lowerIndex == upperIndex)
        {
            return sorted[lowerIndex];
        }

        var weight = position - lowerIndex;
        return sorted[lowerIndex] + (sorted[upperIndex] - sorted[lowerIndex]) * weight;
    }

    private static long? TryGetInputSizeBytes(string inputPath)
    {
        try
        {
            if (File.Exists(inputPath))
            {
                return new FileInfo(inputPath).Length;
            }

            if (!Directory.Exists(inputPath))
            {
                return null;
            }

            var totalBytes = 0L;
            foreach (var file in Directory.EnumerateFiles(inputPath, "*", SearchOption.AllDirectories))
            {
                try
                {
                    totalBytes += new FileInfo(file).Length;
                }
                catch
                {
                }
            }

            return totalBytes;
        }
        catch
        {
            return null;
        }
    }

    private void WriteRuntimeStressPassReport(string outputDirectory, RuntimeStressPassReport report)
    {
        try
        {
            Directory.CreateDirectory(outputDirectory);
            var reportPath = Path.Combine(outputDirectory, "runtime-stress-pass.json");
            var payload = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(reportPath, payload);
            AppendLog($"Runtime stress pass report updated: {reportPath}");
        }
        catch (Exception ex)
        {
            AppendLog($"Could not write runtime stress pass report: {ex.Message}");
        }
    }

    private static string BuildProgressStageDisplay(BatchProgressUpdate update)
    {
        var stageName = BuildProgressLogStage(update);
        if (update.StageCount <= 0)
        {
            return stageName;
        }

        var stageIndex = update.IsItemCompleted
            ? update.StageCount
            : Math.Clamp(update.StageIndex, 1, update.StageCount);
        return $"{stageName} (stage {stageIndex}/{update.StageCount})";
    }

    private void ResetPipelineTimeline()
    {
        _pipelineListView.BeginUpdate();
        try
        {
            _pipelineListView.Items.Clear();
            for (var index = 0; index < ConversionPipelineStages.Length; index++)
            {
                _pipelineListView.Items.Add(new ListViewItem(
                [
                    (index + 1).ToString(CultureInfo.InvariantCulture),
                    ConversionPipelineStages[index],
                    "Pending",
                    "0%",
                    "Waiting to start"
                ]));
            }
        }
        finally
        {
            _pipelineListView.EndUpdate();
        }
    }

    private void UpdatePipelineTimeline(
        BatchProgressUpdate update,
        TimeSpan? stageElapsed,
        TimeSpan? stageEta,
        TimeSpan overallElapsed,
        TimeSpan? overallEta)
    {
        if (_pipelineListView.Items.Count == 0)
        {
            ResetPipelineTimeline();
        }

        var stageCount = Math.Min(ConversionPipelineStages.Length, Math.Max(update.StageCount, 0));
        if (stageCount == 0)
        {
            return;
        }

        var stageIndex = update.IsItemCompleted
            ? stageCount
            : Math.Clamp(update.StageIndex, 1, stageCount);
        for (var index = 0; index < _pipelineListView.Items.Count; index++)
        {
            var item = _pipelineListView.Items[index];
            if (index < stageIndex - 1)
            {
                item.SubItems[2].Text = "Completed";
                item.SubItems[3].Text = "100%";
                if (string.IsNullOrWhiteSpace(item.SubItems[4].Text) || item.SubItems[4].Text.Equals("Waiting to start", StringComparison.Ordinal))
                {
                    item.SubItems[4].Text = "Done";
                }
            }
            else if (index == stageIndex - 1)
            {
                var stagePercent = stageCount > 0
                    ? (int)Math.Round(Math.Clamp((double)stageIndex / stageCount, 0d, 1d) * 100d, MidpointRounding.AwayFromZero)
                    : 0;
                item.SubItems[2].Text = update.IsItemCompleted ? "Completed" : "In progress";
                item.SubItems[3].Text = $"{Math.Clamp(stagePercent, 0, 100)}%";
                var note = update.IsItemCompleted
                    ? "Done"
                    : $"Stage elapsed {FormatDuration(stageElapsed)} • Stage remaining {FormatDuration(stageEta)} • Overall elapsed {FormatDuration(overallElapsed)} • Overall remaining {FormatDuration(overallEta)}";
                item.SubItems[4].Text = note;
            }
            else if (index == stageIndex)
            {
                item.SubItems[2].Text = "Next";
                item.SubItems[3].Text = "—";
                item.SubItems[4].Text = "Queued";
            }
            else
            {
                item.SubItems[2].Text = "Pending";
                item.SubItems[3].Text = "0%";
                item.SubItems[4].Text = "Waiting to start";
            }
        }
    }

    private static TimeSpan? EstimateRemainingDuration(DateTime startedUtc, DateTime nowUtc, double completedUnits, int totalUnits)
    {
        if (totalUnits <= 0 || completedUnits <= 0d)
        {
            return null;
        }

        var elapsed = nowUtc - startedUtc;
        if (elapsed <= TimeSpan.Zero)
        {
            return null;
        }

        var clampedCompleted = Math.Clamp(completedUnits, 0d, totalUnits);
        if (clampedCompleted <= 0d || clampedCompleted >= totalUnits)
        {
            return clampedCompleted >= totalUnits ? TimeSpan.Zero : null;
        }

        var remainingUnits = totalUnits - clampedCompleted;
        var secondsPerUnit = elapsed.TotalSeconds / clampedCompleted;
        var remainingSeconds = Math.Max(0d, remainingUnits * secondsPerUnit);
        return TimeSpan.FromSeconds(remainingSeconds);
    }

    private static TimeSpan? EstimateStageRemainingDuration(
        string? stageName,
        TimeSpan? stageElapsed,
        IReadOnlyDictionary<string, (double TotalSeconds, int Count)> completedStageDurationSamples,
        bool stageCompleted)
    {
        if (stageCompleted)
        {
            return TimeSpan.Zero;
        }

        if (string.IsNullOrWhiteSpace(stageName) ||
            stageElapsed is null ||
            stageElapsed.Value <= TimeSpan.Zero ||
            !completedStageDurationSamples.TryGetValue(stageName, out var stageSample) ||
            stageSample.Count <= 0)
        {
            return null;
        }

        var averageStageSeconds = stageSample.TotalSeconds / stageSample.Count;
        if (averageStageSeconds <= 0d)
        {
            return null;
        }

        var remainingSeconds = Math.Max(0d, averageStageSeconds - stageElapsed.Value.TotalSeconds);
        return TimeSpan.FromSeconds(remainingSeconds);
    }

    private static string NormalizeProgressStageName(string? stage) =>
        string.IsNullOrWhiteSpace(stage)
            ? "Processing"
            : stage.Trim();

    private static string BuildProgressLogStage(BatchProgressUpdate update)
    {
        var stage = update.Stage;
        if (string.IsNullOrWhiteSpace(stage))
        {
            return "Processing";
        }

        var value = stage.Trim();
        var copyingPrefixIndex = value.IndexOf("copying:", StringComparison.OrdinalIgnoreCase);
        if (copyingPrefixIndex >= 0)
        {
            var copiedTarget = value[(copyingPrefixIndex + "copying:".Length)..].Trim();
            var sizeSeparator = copiedTarget.IndexOf(" (", StringComparison.Ordinal);
            if (sizeSeparator > 0)
            {
                copiedTarget = copiedTarget[..sizeSeparator].Trim();
            }

            var fileName = Path.GetFileName(copiedTarget.Replace('\\', '/').Trim());
            if (!string.IsNullOrWhiteSpace(fileName))
            {
                copiedTarget = fileName;
            }

            var extractionSuffix = BuildArchiveExtractionTelemetrySuffix(update);
            return string.IsNullOrWhiteSpace(copiedTarget)
                ? "Extracting archive — copying entry data"
                : $"Extracting archive — copying {copiedTarget}{extractionSuffix}";
        }

        if (value.StartsWith("Extracting archive", StringComparison.OrdinalIgnoreCase))
        {
            var entrySeparator = value.IndexOf('—');
            if (entrySeparator > 0)
            {
                value = value[..entrySeparator].Trim();
            }
        }

        return value + BuildArchiveExtractionTelemetrySuffix(update);
    }

    private static string BuildArchiveExtractionTelemetrySuffix(BatchProgressUpdate update)
    {
        if (!IsArchiveExtractionStage(update.Stage))
        {
            return string.Empty;
        }

        var parts = new List<string>();
        if (update.ExtractionTotalBytesCopied is { } copiedBytes && copiedBytes > 0)
        {
            if (update.ExtractionTotalBytesEstimated is { } estimatedBytes && estimatedBytes > 0)
            {
                parts.Add($"{FormatByteCount(copiedBytes)}/{FormatByteCount(estimatedBytes)}");
            }
            else
            {
                parts.Add(FormatByteCount(copiedBytes));
            }
        }

        if (update.ExtractionThroughputMiBPerSecond is > 0d and var throughput)
        {
            parts.Add($"{throughput:0.##} MiB/s");
        }

        return parts.Count == 0 ? string.Empty : $" ({string.Join(" • ", parts)})";
    }

    private static string FormatByteCount(long bytes)
    {
        if (bytes < 1024)
        {
            return $"{bytes} B";
        }

        var kib = bytes / 1024d;
        if (kib < 1024d)
        {
            return $"{kib:0.##} KiB";
        }

        var mib = kib / 1024d;
        if (mib < 1024d)
        {
            return $"{mib:0.##} MiB";
        }

        var gib = mib / 1024d;
        return $"{gib:0.##} GiB";
    }

    private static bool IsArchiveExtractionStage(string? stage)
    {
        if (string.IsNullOrWhiteSpace(stage))
        {
            return false;
        }

        var value = stage.Trim();
        return value.StartsWith("Extracting archive", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("copying:", StringComparison.OrdinalIgnoreCase);
    }

    private static string FormatDuration(TimeSpan? duration)
    {
        if (duration is null)
        {
            return "calculating…";
        }

        var value = duration.Value;
        if (value <= TimeSpan.Zero)
        {
            return "0s";
        }

        if (value.TotalHours >= 1d)
        {
            return $"{(int)value.TotalHours}h {value.Minutes}m";
        }

        return value.TotalMinutes >= 1d
            ? $"{(int)value.TotalMinutes}m {value.Seconds}s"
            : $"{Math.Max(1, value.Seconds)}s";
    }

    private void SetBusyState(bool isBusy)
    {
        _convertButton.Enabled = !isBusy;
        _convertButton.Text = isBusy ? "CONVERTING..." : "START CONVERSION";
        _cancelButton.Enabled = isBusy && _activeConversion is not null;
        _clearLogButton.Enabled = !isBusy;
        _copyCurrentViewButton.Enabled = !isBusy;
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
            _cancelButton.Text = "Cancel";
            _progressBar.Style = ProgressBarStyle.Continuous;
        }
        _progressBar.MarqueeAnimationSpeed = _progressBar.Style == ProgressBarStyle.Marquee ? 30 : 0;
        _progressBar.Value = 0;
        if (!isBusy)
        {
            _progressDetailsLabel.Text = "Progress details: idle";
        }
        if (!isBusy)
        {
            UpdateReportActionButtonState();
            UpdateArtifactActionButtonState();
            UpdateGuidanceActionButtonState();
        }
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

        _ = await LoadPreviewInAppWithTimeoutAsync(_lastPreviewPath);
        _resultsTabControl.SelectedTab = _previewTabPage;
    }

    private async Task LoadPreviousResultAsync()
    {
        using var folderDialog = new FolderBrowserDialog
        {
            Description = "Select a previous SlideSmith output folder or any nested report/FOMOD folder within it",
            UseDescriptionForTitle = true,
        };

        if (folderDialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        if (DesktopWorkflowSupport.TryResolveResultOutputDirectory(folderDialog.SelectedPath) is not { } selectedFolder ||
            !DesktopWorkflowSupport.LooksLikeSlideSmithOutputDirectory(selectedFolder))
        {
            MessageBox.Show(
                this,
                $"No recognizable SlideSmith result folder was found in the selected location.{Environment.NewLine}{folderDialog.SelectedPath}",
                "Load result",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        await LoadResultDirectoryAsync(selectedFolder, "manual selection");
    }

    private async Task LoadResultDirectoryAsync(
        string selectedFolder,
        string sourceLabel,
        CancellationToken cancellationToken = default)
    {
        var resetProgressWhenDone = _activeConversion is null;
        if (resetProgressWhenDone)
        {
            ShowBusyProgress("Loading previous result...");
        }
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var previewPath = ResolvePreviewPath(selectedFolder);
            _lastPreviewPath = previewPath;
            _lastOutputDirectory = selectedFolder;

            var batchReportCandidate = Path.Combine(selectedFolder, "batch-report.json");
            _lastBatchReportPath = File.Exists(batchReportCandidate)
                ? batchReportCandidate
                : null;

            UpdatePathActionStates();
            cancellationToken.ThrowIfCancellationRequested();
            _ = await LoadPreviewInAppWithTimeoutAsync(previewPath);
            cancellationToken.ThrowIfCancellationRequested();
            var snapshot = await BuildWorkflowSnapshotAsync(selectedFolder, previewPath, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            PopulateSummaryTab(snapshot.SummaryRows);
            PopulateReportsTab(snapshot.ReportMetrics);
            PopulateArtifactsTab(snapshot.Artifacts);
            var guidanceNeedsReview = await PopulateGuidanceTabAsync([selectedFolder], previewPath, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            ApplyValidationGatePresentation(snapshot.ValidationState, guidanceNeedsReview);
            _resultsTabControl.SelectedTab = guidanceNeedsReview
                ? _summaryTabPage
                : previewPath is not null
                    ? _previewTabPage
                    : _summaryTabPage;
            AppendLog($"Loaded previous result from {sourceLabel}: {selectedFolder}");
            if (previewPath is null)
            {
                AppendLog("Loaded reports/artifacts without an embedded preview; review Overview recommendations, Reports, and Files for packaging/runtime details.");
            }

            AppendLog(BuildValidationOutcomeLogMessage([selectedFolder], previewPath, guidanceNeedsReview));
            _statusLabel.Text = $"Loaded previous result ({sourceLabel}).";
        }
        finally
        {
            if (resetProgressWhenDone)
            {
                _progressBar.Style = ProgressBarStyle.Continuous;
                _progressBar.MarqueeAnimationSpeed = 0;
                _progressBar.Value = 0;
            }
        }
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

    private async Task<bool> LoadPreviewInAppWithTimeoutAsync(string? previewPath)
    {
        var loadTask = LoadPreviewInAppAsync(previewPath);
        if (loadTask.IsCompleted)
        {
            return await loadTask;
        }

        var completedTask = await Task.WhenAny(loadTask, Task.Delay(PreviewLoadTimeout));
        if (completedTask == loadTask)
        {
            return await loadTask;
        }

        ShowPreviewStatus("Embedded preview is taking too long to initialize. Conversion output is ready; open preview-workbench.html manually if needed.");
        AppendLog("Preview initialization timed out; continuing without blocking the rest of the desktop workflow.");
        return false;
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
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceWarning($"Failed to initialize in-app preview WebView2: {ex.Message}");
            _previewWebView?.Dispose();
            _previewWebView = null;
            ShowPreviewStatus($"In-app preview is unavailable on this machine: {ex.Message}");
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
        PopulateSummaryTab(DesktopWorkflowAutomation.BuildFromResults(results, _lastPreviewPath).SummaryRows);
    }

    private void PopulateSummaryTab(IReadOnlyList<DesktopWorkflowSummaryRow> summaryRows)
    {
        _summaryListView.Items.Clear();
        foreach (var row in summaryRows)
        {
            _summaryListView.Items.Add(new ListViewItem([row.Property, row.Value]));
        }

        AutoSizeListViewColumns(_summaryListView, 220, 420);
    }

    private void MergeGuidanceIntoSummary(IReadOnlyList<GuidanceEntry> entries)
    {
        for (var index = _summaryListView.Items.Count - 1; index >= 0; index--)
        {
            if (_summaryListView.Items[index].Text.StartsWith("Recommended action", StringComparison.OrdinalIgnoreCase))
            {
                _summaryListView.Items.RemoveAt(index);
            }
        }

        var topActions = entries
            .OrderByDescending(entry => GetGuidancePriorityRank(entry.Priority))
            .ThenBy(entry => entry.Area, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.Guidance, StringComparer.OrdinalIgnoreCase)
            .Take(4)
            .ToList();
        for (var index = 0; index < topActions.Count; index++)
        {
            var action = topActions[index];
            var label = $"Recommended action {index + 1}";
            var value = $"{action.Area} — {action.Guidance}";
            _summaryListView.Items.Add(new ListViewItem([label, value]));
        }

        AutoSizeListViewColumns(_summaryListView, 220, 420);
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

    private async Task<bool> PopulateGuidanceTabAsync(
        IReadOnlyList<ConversionResult> results,
        string? previewPath,
        CancellationToken cancellationToken)
    {
        var outputDirectories = results
            .Select(result => result.OutputDirectory)
            .Where(static directory => !string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return await PopulateGuidanceTabAsync(outputDirectories, previewPath, cancellationToken);
    }

    private bool PopulateGuidanceTab(string? outputDirectory, string? previewPath)
    {
        if (string.IsNullOrWhiteSpace(outputDirectory) || !Directory.Exists(outputDirectory))
        {
            return PopulateGuidanceTab(Array.Empty<string>(), previewPath);
        }

        return PopulateGuidanceTab([outputDirectory], previewPath);
    }

    private async Task<bool> PopulateGuidanceTabAsync(string? outputDirectory, string? previewPath)
    {
        if (string.IsNullOrWhiteSpace(outputDirectory) || !Directory.Exists(outputDirectory))
        {
            return await PopulateGuidanceTabAsync(Array.Empty<string>(), previewPath, CancellationToken.None);
        }

        return await PopulateGuidanceTabAsync([outputDirectory], previewPath, CancellationToken.None);
    }

    private bool PopulateGuidanceTab(IReadOnlyList<string> outputDirectories, string? previewPath)
        => PopulateGuidanceTab(BuildGuidanceBuildResult(outputDirectories, previewPath));

    private async Task<bool> PopulateGuidanceTabAsync(
        IReadOnlyList<string> outputDirectories,
        string? previewPath,
        CancellationToken cancellationToken)
    {
        var buildResult = await Task.Run(() => BuildGuidanceBuildResult(outputDirectories, previewPath), cancellationToken);
        return PopulateGuidanceTab(buildResult);
    }

    private bool PopulateGuidanceTab(GuidanceBuildResult buildResult)
    {
        var gateRank = ConversionValidationPresentation.GetGateRank(buildResult.GateStatus);
        _guidanceListView.BeginUpdate();
        try
        {
            _guidanceListView.Items.Clear();
            foreach (var entry in buildResult.Entries
                         .OrderByDescending(entry => GetGuidancePriorityRank(entry.Priority))
                         .ThenBy(entry => entry.Area, StringComparer.OrdinalIgnoreCase)
                         .ThenBy(entry => entry.Guidance, StringComparer.OrdinalIgnoreCase))
            {
                var item = new ListViewItem([FormatGuidanceAreaLabel(entry.Area, entry.Priority), entry.Priority, entry.Guidance])
                {
                    Tag = entry.TargetPath,
                    ToolTipText = string.IsNullOrWhiteSpace(entry.TargetPath)
                        ? $"{entry.Priority}: {entry.Guidance}"
                        : $"{entry.Priority}: {entry.Guidance}{Environment.NewLine}{entry.TargetPath}"
                };
                _guidanceListView.Items.Add(item);
            }

            ApplyGuidanceItemStyles(CreateThemePalette(_currentTheme));
        }
        finally
        {
            _guidanceListView.EndUpdate();
        }

        AutoSizeListViewColumns(_guidanceListView, 180, 100, 460);
        MergeGuidanceIntoSummary(buildResult.Entries);
        UpdateGuidanceActionButtonState();

        return buildResult.RequiresReview ||
            gateRank >= ConversionValidationPresentation.GetGateRank("needs-review");
    }

    private static GuidanceBuildResult BuildGuidanceBuildResult(IReadOnlyList<string> outputDirectories, string? previewPath)
    {
        foreach (var outputDirectory in outputDirectories)
        {
            ExternalProofHarnessSupport.RefreshImportedProofState(outputDirectory);
        }

        var requiresReview = false;
        var gateStatus = "ready";
        var gateRank = ConversionValidationPresentation.GetGateRank(gateStatus);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var entries = new List<GuidanceEntry>();

        void Add(string area, string priority, string guidance, string? targetPath = null)
        {
            if (string.IsNullOrWhiteSpace(guidance) ||
                !seen.Add($"{area}|{priority}|{guidance}"))
            {
                return;
            }

            entries.Add(new GuidanceEntry(area, priority, guidance, targetPath));
        }

        void PromoteGate(string? candidateStatus)
        {
            var candidateRank = ConversionValidationPresentation.GetGateRank(candidateStatus);
            if (candidateRank > gateRank)
            {
                gateRank = candidateRank;
                gateStatus = candidateStatus ?? gateStatus;
            }
        }

        if (outputDirectories.Count == 0 && string.IsNullOrWhiteSpace(previewPath))
        {
            Add("Status", "Info", "Run or load a conversion to see preview guidance, warnings, and recommended next actions here.");
        }
        else if (!string.IsNullOrWhiteSpace(previewPath) && File.Exists(previewPath))
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

        if (outputDirectories.Count > 0 || (!string.IsNullOrWhiteSpace(previewPath) && File.Exists(previewPath)))
        {
            var firstOutput = outputDirectories.FirstOrDefault(static directory => !string.IsNullOrWhiteSpace(directory));
            Add(
                "How to use this tab",
                "Info",
                "Purpose: this tab is your prioritized checklist. Work from High/Warning rows first, then Action rows, and double-click a row to open the linked report/artifact.",
                firstOutput);
        }

        foreach (var outputDirectory in outputDirectories)
        {
            AppendGuidanceFromBatchReport(outputDirectory, previewPath, Add, ref requiresReview, PromoteGate);
            AppendGuidanceFromConversionQuality(outputDirectory, previewPath, Add, ref requiresReview, PromoteGate);
            AppendGuidanceFromPackValidation(outputDirectory, previewPath, Add, ref requiresReview, PromoteGate);
            AppendGuidanceFromFomodArtifacts(outputDirectory, Add);
            AppendGuidanceFromSkeletonCompatibility(outputDirectory, previewPath, Add, ref requiresReview);
            AppendGuidanceFromTextureSummary(outputDirectory, previewPath, Add, ref requiresReview);
            AppendGuidanceFromDependencyMap(outputDirectory, previewPath, Add, ref requiresReview);
            AppendGuidanceFromPluginPatches(outputDirectory, previewPath, Add, ref requiresReview);
            AppendGuidanceFromModStackCrossValidation(outputDirectory, previewPath, Add, ref requiresReview);
            AppendGuidanceFromConversionMatrixProof(outputDirectory, previewPath, Add, ref requiresReview);
            AppendGuidanceFromWorldPhysics(outputDirectory, previewPath, Add, ref requiresReview);
            AppendGuidanceFromInGameValidation(outputDirectory, previewPath, Add, ref requiresReview);
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

        var actionableEntries = entries.ToArray();
        if (actionableEntries.Length > 0)
        {
            var guidanceTarget = !string.IsNullOrWhiteSpace(previewPath) && File.Exists(previewPath)
                ? previewPath
                : outputDirectories.FirstOrDefault(static directory => !string.IsNullOrWhiteSpace(directory));
            Add(
                "Overall status",
                gateRank >= ConversionValidationPresentation.GetGateRank("needs-review") || requiresReview ? "Warning" : "Info",
                BuildGuidanceOverview(actionableEntries, requiresReview, gateStatus),
                guidanceTarget);
        }

        if (entries.Count == 0)
        {
            Add("Status", "Info", "Run or load a conversion to see preview guidance, warnings, and recommended next actions here.");
        }

        return new GuidanceBuildResult(entries, requiresReview, gateStatus);
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

        AutoSizeListViewColumns(_inspectListView, 240, 520);
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

        AutoSizeListViewColumns(_inspectListView, 180, 420);
        ClearAutoDetectedSourceHint(refreshDetails: true);
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
        var selectedGuidanceItem = _guidanceListView.SelectedItems.Count > 0
            ? _guidanceListView.SelectedItems[0]
            : _guidanceListView.Items
                .Cast<ListViewItem>()
                .FirstOrDefault(item => item.Tag is string candidate && !string.IsNullOrWhiteSpace(candidate));
        if (selectedGuidanceItem is null)
        {
            return;
        }

        if (selectedGuidanceItem.Tag is not string targetPath ||
            string.IsNullOrWhiteSpace(targetPath))
        {
            MessageBox.Show(this, "This recommendation does not have a direct file or folder to open.", "Open recommended action", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
            MessageBox.Show(this, "The file for this recommendation was not found.", "Open recommended action", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            PopulateGuidanceTab(GetPreferredOutputDirectoryForOpen(), _lastPreviewPath);
            return;
        }

        if (PreviewFileCandidates.Contains(Path.GetFileName(targetPath), StringComparer.OrdinalIgnoreCase))
        {
            _lastPreviewPath = targetPath;
            UpdatePathActionStates();
            await LoadPreviewInAppWithTimeoutAsync(targetPath);
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

    private static void AttachListViewCopySupport(ListView listView)
    {
        listView.KeyDown += OnCopyKeyDown;

        var contextMenu = new ContextMenuStrip();
        var copySelectedItem = new ToolStripMenuItem("Copy selected row(s)");
        var copyAllItem = new ToolStripMenuItem("Copy all rows");

        copySelectedItem.Click += (_, _) =>
        {
            if (listView.SelectedItems.Count == 0)
            {
                return;
            }

            Clipboard.SetText(BuildListViewClipboardText(listView));
        };
        copyAllItem.Click += (_, _) => Clipboard.SetText(BuildAllListViewClipboardText(listView));
        contextMenu.Opening += (_, _) =>
        {
            copySelectedItem.Enabled = listView.SelectedItems.Count > 0;
            copyAllItem.Enabled = listView.Items.Count > 0;
        };
        contextMenu.Items.Add(copySelectedItem);
        contextMenu.Items.Add(copyAllItem);
        listView.ContextMenuStrip = contextMenu;

        listView.MouseDown += (_, e) =>
        {
            if (e.Button != MouseButtons.Right)
            {
                return;
            }

            var hit = listView.HitTest(e.Location);
            if (hit.Item is null)
            {
                return;
            }

            if (!hit.Item.Selected)
            {
                listView.SelectedItems.Clear();
                hit.Item.Selected = true;
            }
        };
    }

    private static void AttachTextCopySupport(TextBox textBox)
    {
        textBox.KeyDown += OnCopyKeyDown;

        var contextMenu = new ContextMenuStrip();
        var copySelectionItem = new ToolStripMenuItem("Copy");
        var copyAllItem = new ToolStripMenuItem("Copy all");

        copySelectionItem.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(textBox.SelectedText))
            {
                return;
            }

            Clipboard.SetText(textBox.SelectedText);
        };
        copyAllItem.Click += (_, _) =>
        {
            if (!string.IsNullOrWhiteSpace(textBox.Text))
            {
                Clipboard.SetText(textBox.Text);
            }
        };
        contextMenu.Opening += (_, _) =>
        {
            copySelectionItem.Enabled = !string.IsNullOrWhiteSpace(textBox.SelectedText);
            copyAllItem.Enabled = !string.IsNullOrWhiteSpace(textBox.Text);
        };
        contextMenu.Items.Add(copySelectionItem);
        contextMenu.Items.Add(copyAllItem);
        textBox.ContextMenuStrip = contextMenu;
    }

    private static void OnCopyKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Control && e.KeyCode == Keys.C)
        {
            switch (sender)
            {
                case TextBox textBox when !string.IsNullOrWhiteSpace(textBox.SelectedText):
                    Clipboard.SetText(textBox.SelectedText);
                    break;
                case TextBox textBox:
                    Clipboard.SetText(textBox.Text);
                    break;
                case ListView listView when listView.SelectedItems.Count > 0:
                    Clipboard.SetText(BuildListViewClipboardText(listView));
                    break;
            }

            e.Handled = true;
            e.SuppressKeyPress = true;
        }
    }

    private void CopyCurrentViewToClipboard()
    {
        if (_resultsTabControl.SelectedTab?.Controls.Count > 0 &&
            _resultsTabControl.SelectedTab.Controls[0] is ListView listView &&
            listView.SelectedItems.Count > 0)
        {
            Clipboard.SetText(BuildListViewClipboardText(listView));
            AppendLog($"Copied {listView.SelectedItems.Count} selected row(s) from {_resultsTabControl.SelectedTab.Text}.");
            return;
        }

        Clipboard.SetText(_logTextBox.Text);
        AppendLog("Copied full log to clipboard.");
    }

    private static string BuildListViewClipboardText(ListView listView)
    {
        var headers = listView.Columns.Cast<ColumnHeader>().Select(static header => header.Text).ToArray();
        var selectedRows = listView.SelectedItems.Count > 0
            ? listView.SelectedItems.Cast<ListViewItem>()
            : listView.Items.Cast<ListViewItem>();

        var lines = new List<string>();
        if (headers.Length > 0)
        {
            lines.Add(string.Join('\t', headers));
        }

        foreach (var item in selectedRows)
        {
            var values = item.SubItems.Cast<ListViewItem.ListViewSubItem>().Select(static subItem => subItem.Text).ToArray();
            lines.Add(string.Join('\t', values));
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string BuildAllListViewClipboardText(ListView listView)
    {
        var headers = listView.Columns.Cast<ColumnHeader>().Select(static header => header.Text).ToArray();
        var lines = new List<string>();
        if (headers.Length > 0)
        {
            lines.Add(string.Join('\t', headers));
        }

        foreach (var item in listView.Items.Cast<ListViewItem>())
        {
            var values = item.SubItems.Cast<ListViewItem.ListViewSubItem>().Select(static subItem => subItem.Text).ToArray();
            lines.Add(string.Join('\t', values));
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string? ReadOptionalComboValue(ComboBox? comboBox)
    {
        if (comboBox is null || comboBox.IsDisposed)
        {
            return null;
        }

        return DesktopWorkflowSupport.ReadOptionalSelection(comboBox.SelectedItem?.ToString());
    }

    private static string[] GetSelectableOptionItems(ComboBox comboBox)
    {
        ArgumentNullException.ThrowIfNull(comboBox);

        return comboBox.Items
            .Cast<object?>()
            .Select(static item => item?.ToString())
            .Where(static value => !string.IsNullOrWhiteSpace(value) &&
                                   !value.Trim().Equals("(auto)", StringComparison.OrdinalIgnoreCase))
            .Select(static value => value!.Trim())
            .ToArray();
    }

    private static string[] GetTabTitles(TabControl tabControl)
    {
        ArgumentNullException.ThrowIfNull(tabControl);

        return tabControl.TabPages
            .Cast<TabPage>()
            .Select(static tabPage => tabPage.Text?.Trim())
            .Where(static title => !string.IsNullOrWhiteSpace(title))
            .Select(static title => title!)
            .ToArray();
    }

    private static string? ReadOptionalPathValue(string? path) =>
        DesktopWorkflowSupport.ReadOptionalPath(path);

    private static string? GetBestOutputDirectory(IReadOnlyList<ConversionResult> results)
        => DesktopWorkflowSupport.GetBestOutputDirectory(results);

    private static string? GetFirstExistingOutputFile(IReadOnlyList<ConversionResult> results, params string[] fileNames)
        => DesktopWorkflowSupport.GetFirstExistingOutputFile(results, fileNames);

    private static int GetPreviewCandidateRank(string? fileName, IReadOnlyList<string> fileNames)
        => DesktopWorkflowSupport.GetPreviewCandidateRank(fileName, fileNames);

    private static string? ResolvePreviewPath(string folder)
        => DesktopWorkflowSupport.ResolvePreviewPath(folder, PreviewFileCandidates);

    private static string? FindCommonDirectory(IEnumerable<string> directories)
        => DesktopWorkflowSupport.FindCommonDirectory(directories);

    private void AppendLog(string message)
    {
        var timestamped = $"[{DateTime.Now:HH:mm:ss}] {message}";
        if (_logTextBox.TextLength == 0)
        {
            _logTextBox.Text = timestamped;
            return;
        }

        _logTextBox.AppendText(Environment.NewLine + timestamped);
        TrimLogIfNeeded();
    }

    private void TrimLogIfNeeded()
    {
        if (_logTextBox.TextLength <= MaxLogCharacters)
        {
            return;
        }

        var logText = _logTextBox.Text;
        if (logText.Length <= TrimmedLogCharacters)
        {
            return;
        }

        var trimStart = logText.Length - TrimmedLogCharacters;
        var nextLineBreak = logText.IndexOf(Environment.NewLine, trimStart, StringComparison.Ordinal);
        var sliceStart = nextLineBreak >= 0
            ? nextLineBreak + Environment.NewLine.Length
            : trimStart;
        _logTextBox.Text = logText[sliceStart..];
        _logTextBox.SelectionStart = _logTextBox.TextLength;
        _logTextBox.ScrollToCaret();
    }

    private void ApplyValidationGatePresentation(
        IReadOnlyList<string> outputDirectories,
        string? previewPath,
        bool requiresReview)
    {
        var state = DesktopWorkflowAutomation.BuildValidationState(outputDirectories, previewPath);
        ApplyValidationGatePresentation(state, requiresReview);
    }

    private void ApplyValidationGatePresentation(DesktopWorkflowValidationState state, bool requiresReview)
    {
        var effectiveStatus = requiresReview && ConversionValidationPresentation.GetGateRank(state.EffectiveStatus) < ConversionValidationPresentation.GetGateRank("needs-review")
            ? "needs-review"
            : state.EffectiveStatus;
        _previewTabPage.Text = ConversionValidationPresentation.BuildDesktopResultTabTitle("Preview", effectiveStatus);
        _statusLabel.Text = ConversionValidationPresentation.BuildDesktopStatusLabel(effectiveStatus, state.PreviewAvailable);
    }

    private static string BuildValidationOutcomeLogMessage(
        IReadOnlyList<string> outputDirectories,
        string? previewPath,
        bool requiresReview)
    {
        var state = DesktopWorkflowAutomation.BuildValidationState(outputDirectories, previewPath);
        var validationSummary = TryReadWorstValidationSummary(outputDirectories);
        if (!requiresReview || validationSummary is null)
        {
            return state.OutcomeSummary;
        }

        var effectiveStatus = ConversionValidationPresentation.GetGateRank(state.EffectiveStatus) >= ConversionValidationPresentation.GetGateRank("needs-review")
            ? state.EffectiveStatus
            : "needs-review";
        return ConversionValidationPresentation.BuildOutcomeSummary(
            status: effectiveStatus,
            highSeverityCount: validationSummary.HighSeverityCount,
            mediumSeverityCount: validationSummary.MediumSeverityCount,
            lowSeverityCount: validationSummary.LowSeverityCount,
            previewAvailable: state.PreviewAvailable);
    }

    private string? ResolveEffectiveOutputDirectoryPreview()
    {
        var explicitOutput = _outputTextBox.Text.Trim();
        if (!string.IsNullOrWhiteSpace(explicitOutput))
        {
            return explicitOutput;
        }

        var input = _inputTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(input))
        {
            return Path.Combine(ExecutionEnvironment.GetDefaultOutputRoot(), ResolveProfileTargetName(), "...");
        }

        var fileNameStem = Path.GetFileNameWithoutExtension(input);
        if (string.IsNullOrWhiteSpace(fileNameStem) && Directory.Exists(input))
        {
            fileNameStem = "batch";
        }

        if (string.IsNullOrWhiteSpace(fileNameStem))
        {
            fileNameStem = "output";
        }

        return Path.Combine(
            ExecutionEnvironment.GetDefaultOutputRootForInput(input),
            ResolveProfileTargetName(),
            fileNameStem);
    }

    private void UpdateOutputHint()
    {
        if (_outputHintLabel is null || _outputHintLabel.IsDisposed)
        {
            return;
        }

        var explicitOutput = _outputTextBox.Text.Trim();
        var packageHint = _outputZipCheckBox.Checked
            ? " Raw output will include README.txt plus fomod/ metadata, and a distributable zip will be created."
            : " Raw output will include README.txt plus fomod/ metadata; enable Package output as zip to bundle them into a distributable archive.";
        _outputHintLabel.Text = string.IsNullOrWhiteSpace(explicitOutput)
            ? $"If you leave Output blank, SlideSmith will save to: {ResolveEffectiveOutputDirectoryPreview()}.{packageHint}"
            : $"Converted files will be saved to: {explicitOutput}.{packageHint}";
    }

    private void UpdatePresetDetails()
    {
        if (!TryGetSelectedPreset(out var preset))
        {
            if (_presetTargetTextBox is not null && !_presetTargetTextBox.IsDisposed)
            {
                _presetTargetTextBox.Text = string.Empty;
            }
            _presetDetailsLabel.Text = "—";
            return;
        }

        if (_presetTargetTextBox is not null && !_presetTargetTextBox.IsDisposed)
        {
            _presetTargetTextBox.Text = preset.TargetBody;
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

        if (!HasKnownBodyMetadata(targetBody))
        {
            _targetDetailsLabel.Text =
                $"Custom target '{targetBody}': conversion can still run, but support certainty drops until you load or save a custom profile with the right sliders, transform field, and physics metadata for this body.";
            return;
        }

        _targetDetailsLabel.Text = BuildBodyDetailsText(
            targetBody,
            defaultText: $"This is the destination body the converted armor will be reshaped for.",
            isSourceContext: false);
        UpdateBodySelectionSummary();
    }

    private void UpdateSourceDetails()
    {
        var rawSource = ResolveDisplayedSourceBody();
        if (string.IsNullOrWhiteSpace(rawSource) ||
            string.Equals(rawSource, "(auto)", StringComparison.OrdinalIgnoreCase))
        {
            _sourceDetailsLabel.Text =
                "Auto means the app tries to detect what body the original armor was built for from meshes, plugins, and BodySlide support files. Choose a source body only if detection is wrong or the mod is unusual.";
            return;
        }

        var resolvedSource = BodyTypeCatalog.ResolveName(rawSource);
        if (!HasKnownBodyMetadata(resolvedSource))
        {
            _sourceDetailsLabel.Text =
                $"Custom source hint '{resolvedSource}': use this only when auto-detection is wrong. Unrecognized source bodies keep conversion possible, but they increase the chance that manual Outfit Studio cleanup or a custom profile will still be needed.";
            return;
        }

        if (TryGetAutoDetectedSourceConfidence(rawSource, resolvedSource, out var confidence))
        {
            _sourceDetailsLabel.Text =
                $"Auto-detected from the last inspection: {resolvedSource} ({confidence:P0} confidence). Review this hint if the mod mixes body families, has sparse meshes, or uses unusual source assets. " +
                BuildBodyDetailsText(resolvedSource, defaultText: string.Empty, isSourceContext: true);
            return;
        }

        _sourceDetailsLabel.Text =
            $"Source hint only: treat the original armor as built for {resolvedSource}. This does not change the destination body or output physics. " +
            BuildBodyDetailsText(resolvedSource, defaultText: string.Empty, isSourceContext: true);
        UpdateBodySelectionSummary();
    }

    private void UpdateBodySelectionSummary()
    {
        if (_bodySelectionSummaryLabel is null || _bodySelectionSummaryLabel.IsDisposed)
        {
            return;
        }

        var fromBody = ResolveDisplayedSourceBody();
        var resolvedFromBody = string.IsNullOrWhiteSpace(fromBody) || string.Equals(fromBody, "(auto)", StringComparison.OrdinalIgnoreCase)
            ? "(auto-detect)"
            : BodyTypeCatalog.ResolveName(fromBody);
        var resolvedToBody = BodyTypeCatalog.ResolveName(ResolveProfileTargetName());
        if (string.IsNullOrWhiteSpace(resolvedToBody))
        {
            resolvedToBody = "(not selected)";
        }

        _bodySelectionSummaryLabel.Text = $"FROM body: {resolvedFromBody}  →  TO body: {resolvedToBody}";
    }

    private void ClearAutoDetectedSourceHint(bool refreshDetails)
    {
        var hadHint = !string.IsNullOrWhiteSpace(_autoDetectedSourceBody) || _autoDetectedSourceConfidence.HasValue;
        _autoDetectedSourceBody = null;
        _autoDetectedSourceConfidence = null;

        if (refreshDetails && hadHint && _sourceDetailsLabel is not null && !_sourceDetailsLabel.IsDisposed)
        {
            UpdateSourceDetails();
        }
    }

    private string? ResolveDisplayedSourceBody()
    {
        if (_sourceComboBox is null || _sourceComboBox.IsDisposed)
        {
            return _autoDetectedSourceBody;
        }

        return DesktopWorkflowSupport.ResolveDisplayedSourceBody(
            _sourceComboBox.Text,
            _sourceComboBox.SelectedItem?.ToString(),
            _autoDetectedSourceBody);
    }

    private bool TryGetAutoDetectedSourceConfidence(string rawSource, string resolvedSource, out double confidence)
    {
        confidence = 0d;
        if (_autoDetectedSourceConfidence is not double detectedConfidence ||
            string.IsNullOrWhiteSpace(_autoDetectedSourceBody))
        {
            return false;
        }

        if (!string.Equals(_autoDetectedSourceBody, rawSource, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(_autoDetectedSourceBody, resolvedSource, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        confidence = detectedConfidence;
        return true;
    }

    private void UpdatePhysicsDetails()
    {
        if (_physicsDetailsLabel is null)
        {
            return;
        }

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
            "Use this when you want to type or choose one or more TO bodies directly instead of letting a preset lock the destination body.");
        _optionToolTip.SetToolTip(_showAdvancedOptionsCheckBox,
            "Show or hide advanced/manual tuning controls.\n" +
            "Keep this unchecked for a cleaner default layout.");
        _optionToolTip.SetToolTip(_presetComboBox,
            "Quick setup for the output you want. Presets do not describe the original source armor body.");
        _optionToolTip.SetToolTip(_presetBatchTextBox,
            "Optional comma-separated preset list for batch conversion. Example: 3BA Curvy, HIMBO Lean");
        _optionToolTip.SetToolTip(_targetComboBox,
            "The body you want the converted armor to fit. This is the TO/output body and is only editable in Manual mode.");
        _optionToolTip.SetToolTip(_targetBatchTextBox,
            "Optional comma-separated destination body list for batch conversion. Use all to build every supported body.\n" +
            "For mixed male/female packs you can use 'Choose TO bodies...' to select targets like 3BA and HIMBO so female body assets stay on the female target and male body assets stay on the male target.");
        _optionToolTip.SetToolTip(_profileComboBox,
            "Optional shape override for the converted output. Leave Auto unless you specifically want a different slider/deformation profile.");
        _optionToolTip.SetToolTip(_sourceComboBox,
            "What body the original armor was built for. Leave Auto unless detection gets it wrong. This FROM body hint does not choose the output body.");
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
            "Optional skeleton support path used to improve bone mapping.\n" +
            "You can select a skeleton .nif directly, an XP32/XPMSSE mod folder, or a related .pex file from the same mod.");
        _optionToolTip.SetToolTip(_outputZipCheckBox,
            "Create a ready-to-share zip package of the converted output.\n" +
            "The raw output folder always includes README.txt plus fomod/ installer metadata.");
        _optionToolTip.SetToolTip(_buildSlidersCheckBox,
            "Generate BodySlide project files for the converted result so it can be rebuilt or adjusted later.");
        _optionToolTip.SetToolTip(_copyCurrentViewButton,
            "Copies the selected rows from the active diagnostics tab.\n" +
            "If nothing is selected, copies the full log text.");
        _optionToolTip.SetToolTip(_copyMo2SetupButton,
            "Copies recommended MO2/Vortex setup values (binary, start-in, and launcher arguments) for this SlideSmith build.");
    }

    private void ApplyLauncherContextGuidance()
    {
        if (!_launchOptions.FromModOrganizerLauncher && !IsLikelyModManagerEnvironment())
        {
            return;
        }

        _statusLabel.Text = "Ready — launched from mod manager context.";
        UpdateStartupHandoffTelemetry("Mod manager context", "Launcher environment variables indicate mod manager startup context.");
        AppendLog("Mod manager context detected. Use 'Copy Mod Manager setup' for recommended executable/profile values.");
    }

    private void UpdateStartupHandoffTelemetry(string status, string details)
    {
        _startupHandoffStatusValueLabel.Text = string.IsNullOrWhiteSpace(status) ? "Unknown" : status.Trim();
        _startupHandoffDetailsValueLabel.Text = TruncateForUi(details, 220);
    }

    private static string TruncateForUi(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "n/a";
        }

        var normalized = value.ReplaceLineEndings(" ").Trim();
        if (normalized.Length <= maxLength)
        {
            return normalized;
        }

        return normalized[..Math.Max(0, maxLength - 1)] + "…";
    }

    private void CopyMo2SetupGuidance()
    {
        var processPath = Environment.ProcessPath ?? Application.ExecutablePath;
        var processDirectory = Path.GetDirectoryName(processPath) ?? Environment.CurrentDirectory;
        var executableName = Path.GetFileName(processPath);
        var normalizedExecutable = processPath.Replace('\\', '/');
        var looksLikeCliTarget =
            executableName.Equals("SlideSmith-CLI.exe", StringComparison.OrdinalIgnoreCase) ||
            normalizedExecutable.Contains("/cli/", StringComparison.OrdinalIgnoreCase);
        var desktopPath = looksLikeCliTarget
            ? FindSiblingDesktopExecutable(processDirectory) ?? processPath
            : processPath;
        var workingDirectory = Path.GetDirectoryName(desktopPath) ?? processDirectory;
        var cliPath = FindSiblingCliExecutable(workingDirectory);

        var guidance = new StringBuilder()
            .AppendLine("Recommended mod manager setup for SlideSmith")
            .AppendLine($"Title: SlideSmith (Desktop)")
            .AppendLine($"Binary: {desktopPath}")
            .AppendLine($"Start in: {workingDirectory}")
            .AppendLine("MO2 note: keep Binary and Start in on the exact desktop executable folder so the VFS/USVFS hook can inject mods before startup.")
            .AppendLine("Arguments (MO2): --mo2-launcher")
            .AppendLine("Arguments (Vortex): --vortex-launcher")
            .AppendLine();
        if (looksLikeCliTarget && !desktopPath.Equals(processPath, StringComparison.OrdinalIgnoreCase))
        {
            guidance.AppendLine($"Detected current process as CLI ({processPath}) and switched suggested MO2 binary to desktop executable ({desktopPath}).")
                .AppendLine();
        }
        if (!string.IsNullOrWhiteSpace(cliPath))
        {
            guidance.AppendLine("Optional CLI entry:")
                .AppendLine("Title: SlideSmith CLI")
                .AppendLine($"Binary: {cliPath}")
                .AppendLine($"Start in: {Path.GetDirectoryName(cliPath)}")
                .AppendLine("Arguments: --mo2-launcher");
        }

        try
        {
            Clipboard.SetText(guidance.ToString());
            AppendLog("Copied recommended mod manager setup to clipboard.");
            _statusLabel.Text = "Copied mod manager setup guidance to clipboard.";
        }
        catch (Exception ex)
        {
            AppendLog($"Could not copy mod manager setup guidance: {ex.Message}");
        }
    }

    private static bool IsLikelyModManagerEnvironment() =>
        HasEnvironmentVariable("MO2_INSTANCE") ||
        HasEnvironmentVariable("USVFS_PARAMETERS") ||
        HasEnvironmentVariable("USVFS_PROCESS") ||
        HasEnvironmentVariable("USVFS_PROXY") ||
        HasEnvironmentVariable("MODORGANIZER_INSTANCE") ||
        HasEnvironmentVariable("MODORGANIZER_PATH") ||
        HasEnvironmentVariable("MODORGANIZER_ROOT") ||
        HasEnvironmentVariable("VORTEX_USERDATA") ||
        HasEnvironmentVariable("VORTEX_PROFILE_ID") ||
        HasEnvironmentVariable("VORTEX_STAGING_FOLDER");

    private static bool HasEnvironmentVariable(string name) =>
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(name));

    private static string? FindSiblingCliExecutable(string desktopDirectory)
    {
        var sameDirectory = Path.Combine(desktopDirectory, "SlideSmith-CLI.exe");
        if (File.Exists(sameDirectory))
        {
            return sameDirectory;
        }

        var sameDirectoryStandaloneName = Path.Combine(desktopDirectory, "SlideSmith.exe");
        if (File.Exists(sameDirectoryStandaloneName))
        {
            return sameDirectoryStandaloneName;
        }

        var siblingCliDirectory = Path.GetFullPath(Path.Combine(desktopDirectory, "..", "cli"));
        foreach (var candidate in new[]
                 {
                     Path.Combine(siblingCliDirectory, "SlideSmith-CLI.exe"),
                     Path.Combine(siblingCliDirectory, "SlideSmith.exe")
                 })
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static string? FindSiblingDesktopExecutable(string executableDirectory)
    {
        foreach (var directory in DesktopLaunchPathResolver.GetLikelyDesktopCandidateDirectories(executableDirectory))
        {
            foreach (var candidate in new[]
                     {
                         Path.Combine(directory, "SlideSmith.exe"),
                         Path.Combine(directory, "Bodyslide.Desktop.exe"),
                         Path.Combine(directory, "SlideSmith-Desktop.exe")
                     })
            {
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        return null;
    }

    private bool TryResolveSkeletonSupportPath(string? inputPath, out string? skeletonNifPath)
    {
        skeletonNifPath = null;
        if (string.IsNullOrWhiteSpace(inputPath))
        {
            return true;
        }

        if (SkeletonSupportPathResolver.TryResolveSkeletonNifPath(inputPath, out skeletonNifPath))
        {
            if (skeletonNifPath is not null &&
                !string.Equals(
                    Path.GetFullPath(inputPath),
                    Path.GetFullPath(skeletonNifPath),
                    StringComparison.OrdinalIgnoreCase))
            {
                AppendLog($"Resolved skeleton support path to: {skeletonNifPath}");
            }

            return true;
        }

        MessageBox.Show(
            this,
            "Could not locate a usable skeleton .nif from that path.\n\n" +
            "Select a skeleton .nif directly, an XP32/XPMSSE mod folder, or a related .pex file from the same mod.",
            "Invalid skeleton support path",
            MessageBoxButtons.OK,
            MessageBoxIcon.Warning);
        return false;
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

    private static bool HasKnownBodyMetadata(string bodyName) =>
        BuiltInBodyMetadataCatalog.TryGet(bodyName, out _) &&
        BodyTechnicalProfileCatalog.TryGet(bodyName, out _);

    private bool ConfirmManualTargetBodies(IReadOnlyList<string> selectedTargets)
    {
        if (selectedTargets.Count == 0)
        {
            return true;
        }

        var unresolved = selectedTargets
            .Select(BodyTypeCatalog.ResolveName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(targetBody => !HasKnownBodyMetadata(targetBody))
            .ToArray();
        if (unresolved.Length == 0)
        {
            return true;
        }

        var suggestions = unresolved
            .Select(targetBody =>
            {
                var hint = BodyTypeCatalog.All
                    .Select(static body => body.Name)
                    .Where(name => name.Contains(targetBody, StringComparison.OrdinalIgnoreCase) ||
                                   targetBody.Contains(name, StringComparison.OrdinalIgnoreCase))
                    .Take(3)
                    .ToArray();
                return hint.Length == 0
                    ? $"{targetBody}: no close catalog match"
                    : $"{targetBody}: did you mean {string.Join(", ", hint)}?";
            })
            .ToArray();

        var message =
            "One or more destination bodies are not recognized by the built-in catalogs.\n\n" +
            $"{string.Join("\n", suggestions)}\n\n" +
            "You can continue with custom bodies, but conversion confidence may drop unless matching custom profiles are loaded.\n\n" +
            "Continue anyway?";
        return MessageBox.Show(
                   this,
                   message,
                   "Unrecognized destination body",
                   MessageBoxButtons.YesNo,
                   MessageBoxIcon.Warning,
                   MessageBoxDefaultButton.Button2) == DialogResult.Yes;
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

    private bool CanStartConversion()
    {
        if (!InputPathExists())
        {
            return false;
        }

        if (_usePresetRadio.Checked)
        {
            return TryGetSelectedPreset(out _);
        }

        return _targetComboBox.SelectedItem is not null || _targetComboBox.Items.Count > 0;
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
        UpdateReportActionButtonState();
        UpdateArtifactActionButtonState();
        UpdateGuidanceActionButtonState();
    }

    private static IReadOnlyList<string> ParseDelimitedValues(string? value) =>
        DesktopWorkflowSupport.ParseDelimitedValues(value);

    private void UpdateGuidanceActionButtonState()
    {
        _openGuidanceTargetButton.Text = "Open recommended action";
        if (_activeConversion is not null)
        {
            _openGuidanceTargetButton.Enabled = false;
            return;
        }

        var selectedGuidanceTarget = _guidanceListView.SelectedItems.Count > 0
            ? _guidanceListView.SelectedItems[0].Tag as string
            : _guidanceListView.Items
                .Cast<ListViewItem>()
                .Select(item => item.Tag as string)
                .FirstOrDefault(path => !string.IsNullOrWhiteSpace(path));
        if (string.IsNullOrWhiteSpace(selectedGuidanceTarget))
        {
            _openGuidanceTargetButton.Enabled = false;
            return;
        }

        var targetExists = File.Exists(selectedGuidanceTarget) || Directory.Exists(selectedGuidanceTarget);
        _openGuidanceTargetButton.Enabled = targetExists;
        if (!targetExists)
        {
            return;
        }

        _openGuidanceTargetButton.Text = BuildOpenPathButtonText(selectedGuidanceTarget, "Open recommended action");
    }

    private void UpdateReportActionButtonState()
    {
        _openReportButton.Text = "Open report";
        if (_activeConversion is not null ||
            _reportsListView.SelectedItems.Count == 0 ||
            _reportsListView.SelectedItems[0].Tag is not string selectedReportPath ||
            !File.Exists(selectedReportPath))
        {
            _openReportButton.Enabled = false;
            return;
        }

        _openReportButton.Enabled = true;
        _openReportButton.Text = BuildOpenPathButtonText(selectedReportPath, "Open report");
    }

    private void UpdateArtifactActionButtonState()
    {
        _openArtifactButton.Text = "Open file";
        if (_activeConversion is not null ||
            _artifactsListView.SelectedItems.Count == 0 ||
            _artifactsListView.SelectedItems[0].Tag is not string selectedArtifactPath ||
            (!File.Exists(selectedArtifactPath) && !Directory.Exists(selectedArtifactPath)))
        {
            _openArtifactButton.Enabled = false;
            return;
        }

        _openArtifactButton.Enabled = true;
        _openArtifactButton.Text = BuildOpenPathButtonText(selectedArtifactPath, "Open file");
    }

    private static string BuildOpenPathButtonText(string targetPath, string fallback)
    {
        if (Directory.Exists(targetPath))
        {
            return "Open folder";
        }

        var fileName = Path.GetFileName(targetPath);
        return fileName.ToLowerInvariant() switch
        {
            "preview-workbench.html" or "preview.html" => "Open preview",
            "conversion-matrix-proof.json" or "conversion-matrix-pack-proof.json" => "Open matrix proof",
            "remaining-gaps-checklist.json" or "remaining-gaps-pack-checklist.json" or
            "remaining-gaps-checklist.md" or "remaining-gaps-pack-checklist.md" => "Open gaps checklist",
            "windows-ui-e2e-automation.json" => "Open UI harness plan",
            "desktop-workflow-automation.json" => "Open desktop flow",
            "live-game-execution.json" => "Open live-game plan",
            "proof-harness-bundle.json" => "Open proof manifest",
            "proof-result-bundle.json" => "Open imported proof",
            "runtime-validation-plan.json" or "runtime-validation-harness.json" => "Open runtime plan",
            "topology-correspondence.json" => "Open topology report",
            "mod-stack-cross-validation.json" => "Open mod-stack proof",
            "plugin-patches.json" => "Open plugin patch report",
            "skeleton-compatibility.json" => "Open skeleton report",
            "conversion-quality.json" => "Open quality report",
            _ => fallback
        };
    }

    private static Task<DesktopWorkflowAutomationSnapshot> BuildWorkflowSnapshotAsync(
        IReadOnlyList<ConversionResult> results,
        string? previewPath,
        CancellationToken cancellationToken) =>
        Task.Run(() => DesktopWorkflowAutomation.BuildFromResults(results, previewPath), cancellationToken);

    private static Task<DesktopWorkflowAutomationSnapshot> BuildWorkflowSnapshotAsync(
        string outputDirectory,
        string? previewPath) =>
        Task.Run(() => DesktopWorkflowAutomation.BuildFromOutputDirectory(outputDirectory, previewPath));

    private static Task<DesktopWorkflowAutomationSnapshot> BuildWorkflowSnapshotAsync(
        string outputDirectory,
        string? previewPath,
        CancellationToken cancellationToken) =>
        Task.Run(() => DesktopWorkflowAutomation.BuildFromOutputDirectory(outputDirectory, previewPath), cancellationToken);

    private async Task RunStartupOperationWithTimeoutAsync(
        string operationLabel,
        Func<CancellationToken, Task> operation)
    {
        using var timeoutSource = new CancellationTokenSource(StartupOperationTimeout);
        try
        {
            await operation(timeoutSource.Token);
            UpdateStartupHandoffTelemetry("Startup handoff completed", $"Startup {operationLabel} completed.");
        }
        catch (OperationCanceledException) when (timeoutSource.IsCancellationRequested)
        {
            _statusLabel.Text = "Startup work timed out. Continue manually from the buttons above.";
            AppendLog($"Startup {operationLabel} timed out after {StartupOperationTimeout.TotalSeconds:0} seconds.");
            UpdateStartupHandoffTelemetry("Startup handoff timed out", $"Startup {operationLabel} timed out after {StartupOperationTimeout.TotalSeconds:0} seconds.");
        }
        catch (Exception ex)
        {
            _statusLabel.Text = "Startup work failed. Continue manually from the buttons above.";
            AppendLog($"Startup {operationLabel} failed: {ex.Message}");
            UpdateStartupHandoffTelemetry("Startup handoff failed", $"Startup {operationLabel} failed: {ex.Message}");
        }
    }

    private void ScheduleAutoInspectLearningCache()
    {
        _autoCacheInspectDebounce?.Cancel();
        _autoCacheInspectDebounce?.Dispose();
        _autoCacheInspectDebounce = new CancellationTokenSource();
        _ = RunAutoInspectLearningCacheAsync(_autoCacheInspectDebounce.Token);
    }

    private async Task RunAutoInspectLearningCacheAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(AutoInspectDebounceMilliseconds, cancellationToken);
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            await InspectLearningCacheAsync(showDialogs: false, switchToTab: false);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task InspectLearningCacheAsync(bool showDialogs, bool switchToTab)
    {
        _activeCacheInspection?.Cancel();
        _activeCacheInspection?.Dispose();
        var cacheInspection = new CancellationTokenSource();
        _activeCacheInspection = cacheInspection;
        var inspectionToken = cacheInspection.Token;

        var inspectTimer = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var cachePathOverride = ReadOptionalPathValue(_cachePathTextBox.Text);
            ConversionLearningCache.SetGlobalCachePath(cachePathOverride);

            var entries = await ConversionLearningCache.LoadMergedEntriesAsync(string.Empty, inspectionToken);
            if (inspectionToken.IsCancellationRequested)
            {
                return;
            }
            if (entries.Count == 0)
            {
                if (showDialogs)
                {
                    AppendLog("Learning cache is empty.");
                }
                PopulateCacheTab([], cachePathOverride);
                if (switchToTab)
                {
                    _resultsTabControl.SelectedTab = _cacheTabPage;
                }
                if (showDialogs)
                {
                    MessageBox.Show(this, "Learning cache is empty.", "Inspect cache", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                return;
            }

            AppendLog($"Learning cache: {entries.Count} entr{(entries.Count == 1 ? "y" : "ies")}.");
            var orderedEntries = entries.OrderBy(e => e.Key, StringComparer.OrdinalIgnoreCase).ToList();
            var maxLogEntries = showDialogs ? MaxAutoCacheLogEntries : Math.Min(8, MaxAutoCacheLogEntries);
            foreach (var entry in orderedEntries.Take(maxLogEntries))
            {
                AppendLog($"[{entry.Key}] target={entry.TargetBody}, mesh={entry.MeshType}, strategy={entry.Strategy}, cached={entry.LastSuccessfulConversion:u}");
            }

            if (orderedEntries.Count > maxLogEntries)
            {
                AppendLog($"Cache log output truncated to {maxLogEntries} entries for responsiveness.");
            }

            PopulateCacheTab(orderedEntries, cachePathOverride);
            if (switchToTab)
            {
                _resultsTabControl.SelectedTab = _cacheTabPage;
            }

            if (showDialogs)
            {
                MessageBox.Show(this, $"Loaded {entries.Count} learning-cache entr{(entries.Count == 1 ? "y" : "ies")}. Details were added to the Cache tab and log.", "Inspect cache", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            if (showDialogs)
            {
                MessageBox.Show(this, $"Failed to inspect learning cache:\n{ex.Message}", "Inspect cache", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            else
            {
                AppendLog($"Automatic cache inspection failed: {ex.Message}");
            }
        }
        finally
        {
            inspectTimer.Stop();
            System.Diagnostics.Trace.TraceInformation($"Learning cache inspection completed in {inspectTimer.ElapsedMilliseconds} ms.");
            if (ReferenceEquals(_activeCacheInspection, cacheInspection))
            {
                _activeCacheInspection.Dispose();
                _activeCacheInspection = null;
            }
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

        AutoSizeListViewColumns(_cacheListView, 220, 80, 120, 180, 70, 100, 150, 320);
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

        AutoSizeListViewColumns(_customProfilesListView, 520);
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
        SaveUiSettings();
        AppendLog($"Removed {selectedPaths.Length} custom profile file(s).");
        ClearInspectionTab("Custom body profiles changed. Auto-inspection will refresh detection and compatibility details.");
        ScheduleAutoInspectInput();
    }

    private void ClearCustomProfiles()
    {
        if (_customProfilePaths.Count == 0)
        {
            return;
        }

        _customProfilePaths.Clear();
        RefreshCustomProfilesList();
        SaveUiSettings();
        AppendLog("Cleared all loaded custom profile files.");
        ClearInspectionTab("Custom body profiles changed. Auto-inspection will refresh detection and compatibility details.");
        ScheduleAutoInspectInput();
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
            SaveUiSettings();
            ClearInspectionTab("Custom body profiles changed. Auto-inspection will refresh detection and compatibility details.");
            ScheduleAutoInspectInput();
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

        var template = DesktopCustomBodyProfileTemplateCatalog.Resolve(effectiveTarget);
        var effectivePhysics = ResolveEffectivePhysicsProfile(effectiveTarget);
        var effectiveProfile = ResolveEffectiveDeformationProfile();
        var baseField = template.TransformationField;
        var transformedField = ApplyDeformationProfile(baseField, effectiveProfile);
        var payload = new
        {
            Name = template.Name,
            DetectionTokens = template.DetectionTokens,
            VertexCountMin = template.VertexCountMin,
            VertexCountMax = template.VertexCountMax,
            TransformationField = transformedField,
            SliderNames = template.SliderNames,
            PhysicsProfile = effectivePhysics,
            BodyOutputPath = template.BodyOutputPath,
            Gender = template.Gender,
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
            SaveUiSettings();
            ClearInspectionTab("Custom body profiles changed. Auto-inspection will refresh detection and compatibility details.");
            ScheduleAutoInspectInput();
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

    private static IReadOnlyList<string> CombineSelections(string? selectedValue, IReadOnlyList<string> enteredValues)
        => DesktopWorkflowSupport.CombineSelections(selectedValue, enteredValues);

    private void OpenTargetBodySelectionDialog()
    {
        using var dialog = new Form
        {
            Text = "Choose destination TO bodies",
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MinimizeBox = false,
            MaximizeBox = false,
            ShowInTaskbar = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(10)
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            ColumnCount = 1,
            RowCount = 3
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        dialog.Controls.Add(layout);

        layout.Controls.Add(new Label
        {
            AutoSize = true,
            Text = "Select one or more destination bodies for this conversion run:"
        }, 0, 0);

        var checkedBodies = new CheckedListBox
        {
            CheckOnClick = true,
            IntegralHeight = false,
            Height = 260,
            Width = 360
        };
        var bodyNames = BodyTypeCatalog.All
            .Select(static body => body.Name)
            .OrderBy(static name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        checkedBodies.Items.AddRange(bodyNames);

        var currentSelection = CombineSelections(
            string.IsNullOrWhiteSpace(_targetComboBox.Text) ? _targetComboBox.SelectedItem?.ToString() : _targetComboBox.Text.Trim(),
            ParseDelimitedValues(_targetBatchTextBox.Text));
        for (var index = 0; index < checkedBodies.Items.Count; index++)
        {
            var candidate = checkedBodies.Items[index]?.ToString();
            if (!string.IsNullOrWhiteSpace(candidate) &&
                currentSelection.Contains(candidate, StringComparer.OrdinalIgnoreCase))
            {
                checkedBodies.SetItemChecked(index, true);
            }
        }

        layout.Controls.Add(checkedBodies, 0, 1);

        var buttons = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.RightToLeft,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 8, 0, 0)
        };
        var okButton = new Button { Text = "OK", DialogResult = DialogResult.OK, AutoSize = true };
        var cancelButton = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true };
        buttons.Controls.Add(okButton);
        buttons.Controls.Add(cancelButton);
        layout.Controls.Add(buttons, 0, 2);

        dialog.AcceptButton = okButton;
        dialog.CancelButton = cancelButton;
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        var selectedBodies = checkedBodies.CheckedItems
            .Cast<object>()
            .Select(static entry => entry.ToString())
            .Where(static entry => !string.IsNullOrWhiteSpace(entry))
            .Select(static entry => entry!)
            .ToArray();

        _useCustomTargetRadio.Checked = true;
        if (selectedBodies.Length == 0)
        {
            _targetBatchTextBox.Text = string.Empty;
            return;
        }

        _targetComboBox.Text = selectedBodies[0];
        _targetBatchTextBox.Text = selectedBodies.Length > 1
            ? string.Join(", ", selectedBodies.Skip(1))
            : string.Empty;
        AppendLog($"Selected destination bodies: {string.Join(", ", selectedBodies)}");
    }

    private void PopulateArtifactsTab(IReadOnlyList<ConversionResult> results)
    {
        PopulateArtifactsTab(DesktopWorkflowAutomation.BuildFromResults(results, _lastPreviewPath).Artifacts);
    }

    private void PopulateArtifactsTab(string? outputDirectory)
    {
        PopulateArtifactsTab(DesktopWorkflowAutomation.BuildFromOutputDirectory(outputDirectory, _lastPreviewPath).Artifacts);
    }

    private void PopulateArtifactsTab(IReadOnlyList<DesktopWorkflowArtifact> artifacts)
    {
        _artifactsListView.BeginUpdate();
        try
        {
            _artifactsListView.Items.Clear();
            foreach (var artifact in artifacts)
            {
                var item = new ListViewItem([artifact.Name, artifact.DisplayPath]) { Tag = artifact.FullPath };
                _artifactsListView.Items.Add(item);
            }
        }
        finally
        {
            _artifactsListView.EndUpdate();
        }

        AutoSizeListViewColumns(_artifactsListView, 260, 520);
        _openArtifactButton.Enabled = _artifactsListView.SelectedItems.Count > 0;
    }

    private void PopulateReportsTab(IReadOnlyList<ConversionResult> results)
    {
        PopulateReportsTab(DesktopWorkflowAutomation.BuildFromResults(results, _lastPreviewPath).ReportMetrics);
    }

    private void PopulateReportsTab(string? outputDirectory)
    {
        PopulateReportsTab(DesktopWorkflowAutomation.BuildFromOutputDirectory(outputDirectory, _lastPreviewPath).ReportMetrics);
    }

    private void PopulateReportsTab(IReadOnlyList<DesktopWorkflowReportMetric> metrics)
    {
        _reportsListView.BeginUpdate();
        try
        {
            _reportsListView.Items.Clear();

            if (metrics.Count == 0)
            {
                _reportsListView.Items.Add(new ListViewItem(["Status", "Reports", "Run or load a conversion to inspect JSON diagnostics in-app."]));
                return;
            }

            foreach (var metric in metrics)
            {
                _reportsListView.Items.Add(new ListViewItem([metric.ReportName, metric.Property, metric.Value]) { Tag = metric.FilePath });
            }
        }
        finally
        {
            _reportsListView.EndUpdate();
        }

        AutoSizeListViewColumns(_reportsListView, 260, 180, 420);
        _openReportButton.Enabled = _reportsListView.SelectedItems.Count > 0;
    }

    private void AppendReportSummary(string reportName, string filePath)
    {
        try
        {
            using var document = OpenJsonDocument(filePath);
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
                    foreach (var metric in ConversionQualityReportMetrics.Read(root))
                    {
                        AddReportMetric(reportName, metric.Property, metric.Value, filePath);
                    }
                    break;
                case "dependency-map.json":
                    AddReportMetric(reportName, "Entries", CountElements(root), filePath);
                    AddReportMetric(reportName, "Detected bodies", DistinctArrayValues(root, "DetectedSourceBody"), filePath);
                    AddReportMetric(reportName, "Source skeletons", DistinctArrayValues(root, "SourceSkeleton"), filePath);
                    AddReportMetric(reportName, "Linked ARMA IDs", SumNestedArrayCounts(root, "LinkedArmaFormIds"), filePath);
                    break;
                case "skeleton-compatibility.json":
                    AddReportMetric(reportName, "Source skeleton", TryReadString(root, "SourceSkeleton"), filePath);
                    AddReportMetric(reportName, "Source skeleton confidence", TryReadString(root, "SourceSkeletonConfidence"), filePath);
                    AddReportMetric(reportName, "Source skeleton evidence", TryReadArray(root, "SourceSkeletonEvidence"), filePath);
                    AddReportMetric(reportName, "Sparse source inference", FormatBool(TryReadBoolValue(root, "SourceSkeletonUsedSparseInference")), filePath);
                    AddReportMetric(reportName, "Source skeleton reliability", TryReadString(root, "SourceSkeletonInferenceReliability"), filePath);
                    AddReportMetric(reportName, "Source skeleton summary", TryReadString(root, "SourceSkeletonInferenceSummary"), filePath);
                    AddReportMetric(reportName, "Source skeleton candidates", CountNestedArray(root, "SourceSkeletonCandidates"), filePath);
                    AddReportMetric(reportName, "Target skeleton", TryReadString(root, "TargetSkeleton"), filePath);
                    AddReportMetric(reportName, "Mapped bones", CountNestedArray(root, "BoneMappings"), filePath);
                    AddReportMetric(reportName, "Unsupported bones", TryReadArray(root, "UnsupportedBones"), filePath);
                    AddReportMetric(reportName, "Support tier", TryReadString(root, "SupportTier"), filePath);
                    AddReportMetric(reportName, "Can convert", FormatBool(TryReadNestedBoolValue(root, "ConversionReadiness", "CanConvert")), filePath);
                    AddReportMetric(reportName, "Can physics-convert", FormatBool(TryReadNestedBoolValue(root, "ConversionReadiness", "CanPhysicsConvert")), filePath);
                    AddReportMetric(reportName, "Can safely animate", FormatBool(TryReadNestedBoolValue(root, "ConversionReadiness", "CanSafelyAnimate")), filePath);
                    if (TryGetProperty(root, "PhysicsCompatibility", out var physicsCompatibility))
                    {
                        AddReportMetric(reportName, "Physics profile", TryReadString(physicsCompatibility, "RequestedProfile"), filePath);
                        AddReportMetric(reportName, "Physics ready", FormatBool(TryReadBoolValue(physicsCompatibility, "IsCompatible")), filePath);
                        AddReportMetric(reportName, "Expected physics configs", TryReadArray(physicsCompatibility, "ExpectedRuntimeConfigs"), filePath);
                        AddReportMetric(reportName, "Generated physics configs", TryReadArray(physicsCompatibility, "GeneratedRuntimeConfigs"), filePath);
                        AddReportMetric(reportName, "Generated physics bones", TryReadArray(physicsCompatibility, "GeneratedPhysicsBones"), filePath);
                        AddReportMetric(reportName, "Missing physics configs", TryReadArray(physicsCompatibility, "MissingRuntimeConfigs"), filePath);
                        AddReportMetric(reportName, "Missing physics bones", TryReadArray(physicsCompatibility, "MissingBones"), filePath);
                        AddReportMetric(reportName, "Remapped physics bones", TryReadArray(physicsCompatibility, "RemappedBones"), filePath);
                        AddReportMetric(reportName, "Expected physics slots", TryReadInt(physicsCompatibility, "ExpectedMinimumPhysicsSlotCount"), filePath);
                        AddReportMetric(reportName, "Generated physics slots", TryReadInt(physicsCompatibility, "GeneratedPhysicsSlotCount"), filePath);
                        AddReportMetric(reportName, "Expected chain depth", TryReadInt(physicsCompatibility, "ExpectedMinimumPhysicsChainDepth"), filePath);
                        AddReportMetric(reportName, "Generated chain depth", TryReadInt(physicsCompatibility, "GeneratedPhysicsChainDepth"), filePath);
                        AddReportMetric(reportName, "Expected physics families", TryReadInt(physicsCompatibility, "ExpectedMinimumPhysicsFamilyCount"), filePath);
                        AddReportMetric(reportName, "Generated physics families", TryReadInt(physicsCompatibility, "GeneratedPhysicsFamilyCount"), filePath);
                        AddReportMetric(reportName, "Collision complexity", TryReadString(physicsCompatibility, "CollisionComplexity"), filePath);
                        AddReportMetric(reportName, "Physics coverage sufficient", FormatBool(TryReadBoolValue(physicsCompatibility, "HasSufficientPhysicsCoverage")), filePath);
                    }
                    break;
                case "in-game-validation.json":
                    AddReportMetric(reportName, "Target body", TryReadString(root, "TargetBody"), filePath);
                    AddReportMetric(reportName, "Validation gate", TryReadString(root, "ValidationGate"), filePath);
                    AddReportMetric(reportName, "Support tier", TryReadString(root, "SupportTier"), filePath);
                    AddReportMetric(reportName, "Can convert", FormatBool(TryReadNestedBoolValue(root, "ConversionReadiness", "CanConvert")), filePath);
                    AddReportMetric(reportName, "Can physics-convert", FormatBool(TryReadNestedBoolValue(root, "ConversionReadiness", "CanPhysicsConvert")), filePath);
                    AddReportMetric(reportName, "Can safely animate", FormatBool(TryReadNestedBoolValue(root, "ConversionReadiness", "CanSafelyAnimate")), filePath);
                    AddReportMetric(reportName, "Core body regions", TryReadArray(root, "CoreBodyRegions"), filePath);
                    AddReportMetric(reportName, "Sensitive regions", TryReadArray(root, "SensitiveRegions"), filePath);
                    AddReportMetric(reportName, "Manual cleanup likely", FormatBool(TryReadBoolValue(root, "ManualCleanupLikely")), filePath);
                    AddReportMetric(reportName, "Runtime verification required", FormatBool(TryReadBoolValue(root, "RuntimeVerificationRequired")), filePath);
                    AddReportMetric(reportName, "Topology correspondence", TryReadNestedString(root, "TopologyCorrespondence", "Classification"), filePath);
                    AddReportMetric(reportName, "True semantic correspondence", FormatBool(TryReadNestedBoolValue(root, "TopologyCorrespondence", "UsesTrueSemanticCorrespondence")), filePath);
                    AddReportMetric(reportName, "Semantic vertex matching", TryReadNestedString(root, "TopologyCorrespondence", "SemanticVertexMatchingStatus"), filePath);
                    AddReportMetric(reportName, "Semantic anchor profile", TryReadNestedString(root, "TopologyCorrespondence", "SemanticAnchorProfile"), filePath);
                    AddReportMetric(reportName, "Semantic anchor coverage", TryReadNestedString(root, "TopologyCorrespondence", "SemanticAnchorCoverage"), filePath);
                    AddReportMetric(reportName, "Heuristic-heavy topology", FormatBool(TryReadNestedBoolValue(root, "TopologyCorrespondence", "HeuristicHeavy")), filePath);
                    AddReportMetric(reportName, "Topology focus regions", TryReadNestedArray(root, "TopologyCorrespondence", "FocusRegions"), filePath);
                    AddReportMetric(reportName, "Unmatched topology focus regions", TryReadNestedArray(root, "TopologyCorrespondence", "UnmatchedFocusRegions"), filePath);
                    AddReportMetric(reportName, "Scenario matrix", CountNestedArray(root, "ScenarioMatrix"), filePath);
                    AddReportMetric(reportName, "Caveats", TryReadArray(root, "Caveats"), filePath);
                    AddReportMetric(reportName, "Scenario highlights", TryReadScenarioHighlights(root), filePath);
                    AddReportMetric(reportName, "High-priority scenarios", CountScenarioPriorities(root, "High", "Action"), filePath);
                    AddReportMetric(reportName, "Checklist items", CountNestedArray(root, "Checklist"), filePath);
                    break;
                case "runtime-validation-plan.json":
                    AddReportMetric(reportName, "Target body", TryReadString(root, "TargetBody"), filePath);
                    AddReportMetric(reportName, "Validation gate", TryReadString(root, "ValidationGate"), filePath);
                    AddReportMetric(reportName, "Support tier", TryReadString(root, "SupportTier"), filePath);
                    AddReportMetric(reportName, "Can convert", FormatBool(TryReadNestedBoolValue(root, "ConversionReadiness", "CanConvert")), filePath);
                    AddReportMetric(reportName, "Can physics-convert", FormatBool(TryReadNestedBoolValue(root, "ConversionReadiness", "CanPhysicsConvert")), filePath);
                    AddReportMetric(reportName, "Can safely animate", FormatBool(TryReadNestedBoolValue(root, "ConversionReadiness", "CanSafelyAnimate")), filePath);
                    AddReportMetric(reportName, "Planned execution coverage", TryReadString(root, "PlannedExecutionCoverage"), filePath);
                    AddReportMetric(reportName, "Execution coverage", TryReadString(root, "ExecutionCoverage"), filePath);
                    AddReportMetric(reportName, "Runtime proof execution", TryReadNestedString(root, "ProofExecution", "ExecutedStatus"), filePath);
                    AddReportMetric(reportName, "Runtime imported proof", FormatBool(TryReadNestedBoolValue(root, "ProofExecution", "ImportedResultAvailable")), filePath);
                    AddReportMetric(reportName, "Missing imported probes", TryReadNestedArray(root, "ProofExecution", "MissingItems"), filePath);
                    AddReportMetric(reportName, "Live game required", FormatBool(TryReadBoolValue(root, "RequiresLiveGameExecution")), filePath);
                    AddReportMetric(reportName, "Automated game execution", FormatBool(TryReadBoolValue(root, "SupportsAutomatedGameExecution")), filePath);
                    AddReportMetric(reportName, "External game harness", FormatBool(TryReadBoolValue(root, "RequiresExternalGameHarness")), filePath);
                    AddReportMetric(reportName, "Modded test environment", FormatBool(TryReadBoolValue(root, "RequiresModdedTestEnvironment")), filePath);
                    AddReportMetric(reportName, "Execution phases", DistinctNestedArrayValues(root, "Steps", "Phase"), filePath);
                    AddReportMetric(reportName, "Runtime steps", CountNestedArray(root, "Steps"), filePath);
                    AddReportMetric(reportName, "Blocking runtime steps", CountObjectsWithBool(root, "Steps", "BlocksRelease", expected: true), filePath);
                    AddReportMetric(reportName, "Runtime execution highlights", TryReadExecutionHighlights(root), filePath);
                    break;
                case "runtime-validation-harness.json":
                    AddReportMetric(reportName, "Target body", TryReadString(root, "TargetBody"), filePath);
                    AddReportMetric(reportName, "Harness automation coverage", TryReadString(root, "AutomationCoverage"), filePath);
                    AddReportMetric(reportName, "Blocking proof axes", TryReadArray(root, "BlockingProofAxes"), filePath);
                    AddReportMetric(reportName, "Matrix combinations targeted", TryReadArray(root, "MatrixCombinationsTargeted"), filePath);
                    AddReportMetric(reportName, "External game harness", FormatBool(TryReadBoolValue(root, "RequiresExternalGameHarness")), filePath);
                    AddReportMetric(reportName, "Modded test environment", FormatBool(TryReadBoolValue(root, "RequiresModdedTestEnvironment")), filePath);
                    AddReportMetric(reportName, "Artifact preflight automation", FormatBool(TryReadBoolValue(root, "SupportsArtifactPreflightAutomation")), filePath);
                    AddReportMetric(reportName, "Scenario dispatch automation", FormatBool(TryReadBoolValue(root, "SupportsScenarioDispatchAutomation")), filePath);
                    AddReportMetric(reportName, "Manual assertion required", FormatBool(TryReadBoolValue(root, "RequiresManualAssertion")), filePath);
                    AddReportMetric(reportName, "Harness probes", CountNestedArray(root, "Probes"), filePath);
                    AddReportMetric(reportName, "Load-order probes", CountObjectsWithBool(root, "Probes", "RequiresFullLoadOrderLaunch", expected: true), filePath);
                    break;
                case "live-game-execution.json":
                    AddReportMetric(reportName, "Target body", TryReadString(root, "TargetBody"), filePath);
                    AddReportMetric(reportName, "Planned live-game coverage", TryReadString(root, "PlannedIntegrationCoverage"), filePath);
                    AddReportMetric(reportName, "Live-game integration coverage", TryReadString(root, "IntegrationCoverage"), filePath);
                    AddReportMetric(reportName, "Live-game proof execution", TryReadNestedString(root, "ProofExecution", "ExecutedStatus"), filePath);
                    AddReportMetric(reportName, "Live-game imported proof", FormatBool(TryReadNestedBoolValue(root, "ProofExecution", "ImportedResultAvailable")), filePath);
                    AddReportMetric(reportName, "Missing live-game scenarios", TryReadNestedArray(root, "ProofExecution", "MissingItems"), filePath);
                    AddReportMetric(reportName, "Blocking proof axes", TryReadArray(root, "BlockingProofAxes"), filePath);
                    AddReportMetric(reportName, "Matrix combinations targeted", TryReadArray(root, "MatrixCombinationsTargeted"), filePath);
                    AddReportMetric(reportName, "Windows host required", FormatBool(TryReadBoolValue(root, "RequiresWindowsHost")), filePath);
                    AddReportMetric(reportName, "External live-game harness", FormatBool(TryReadBoolValue(root, "RequiresExternalHarness")), filePath);
                    AddReportMetric(reportName, "SKSE launcher required", FormatBool(TryReadBoolValue(root, "RequiresSkseOrEquivalentLauncher")), filePath);
                    AddReportMetric(reportName, "Mod-manager load order required", FormatBool(TryReadBoolValue(root, "RequiresDeployedModManagerLoadOrder")), filePath);
                    AddReportMetric(reportName, "Host capabilities", TryReadArray(root, "RequiredHostCapabilities"), filePath);
                    AddReportMetric(reportName, "Observation channels", TryReadArray(root, "ObservationChannels"), filePath);
                    AddReportMetric(reportName, "Validation save profiles", TryReadArray(root, "ValidationSaveProfiles"), filePath);
                    AddReportMetric(reportName, "Live-game launch sequence", TryReadArray(root, "LaunchSequence"), filePath);
                    AddReportMetric(reportName, "Live-game probes", CountNestedArray(root, "Probes"), filePath);
                    break;
                case "conversion-matrix-proof.json":
                    AddReportMetric(reportName, "Target body", TryReadString(root, "TargetBody"), filePath);
                    AddReportMetric(reportName, "Target body family", TryReadString(root, "TargetBodyFamily"), filePath);
                    AddReportMetric(reportName, "Support tier", TryReadString(root, "SupportTier"), filePath);
                    AddReportMetric(reportName, "Matrix coordinate key", TryReadString(root, "MatrixCoordinateKey"), filePath);
                    AddReportMetric(reportName, "Matrix coordinates", TryReadArray(root, "MatrixCoordinates"), filePath);
                    AddReportMetric(reportName, "Planned proof coverage", TryReadString(root, "PlannedProofCoverage"), filePath);
                    AddReportMetric(reportName, "Proof coverage", TryReadString(root, "ProofCoverage"), filePath);
                    AddReportMetric(reportName, "Proof execution status", TryReadString(root, "ProofExecutionStatus"), filePath);
                    AddReportMetric(reportName, "Imported proof executions", CountNestedArray(root, "ProofExecutions"), filePath);
                    AddReportMetric(reportName, "Strict proof ready", FormatBool(TryReadBoolValue(root, "StrictProofReady")), filePath);
                    AddReportMetric(reportName, "Missing proof axes", TryReadArray(root, "MissingProofAxes"), filePath);
                    AddReportMetric(reportName, "Blocking proof gaps", TryReadArray(root, "BlockingGaps"), filePath);
                    AddReportMetric(reportName, "Matrix axes", CountNestedArray(root, "Axes"), filePath);
                    AddReportMetric(reportName, "Strictly proven axes", CountObjectsWithBool(root, "Axes", "StrictlyProven", expected: true), filePath);
                    AddReportMetric(reportName, "Review artifacts", TryReadArray(root, "ReviewArtifacts"), filePath);
                    break;
                case "support-coverage-signals.json":
                    AddReportMetric(reportName, "Target body", TryReadString(root, "TargetBody"), filePath);
                    AddReportMetric(reportName, "Support tier", TryReadString(root, "SupportTier"), filePath);
                    AddReportMetric(reportName, "Proof execution status", TryReadString(root, "ProofExecutionStatus"), filePath);
                    AddReportMetric(reportName, "Physics compatibility status", TryReadString(root, "PhysicsCompatibilityStatus"), filePath);
                    AddReportMetric(reportName, "Coverage categories", CountNestedArray(root, "Categories"), filePath);
                    AddReportMetric(reportName, "Manual proof categories", CountObjectsWithBool(root, "Categories", "NeedsManualProof", expected: true), filePath);
                    AddReportMetric(reportName, "Detected categories", CountObjectsWithString(root, "Categories", "CoverageStatus", "detected"), filePath);
                    AddReportMetric(reportName, "Partial categories", CountObjectsWithString(root, "Categories", "CoverageStatus", "partial"), filePath);
                    break;
                case "runtime-stress-pass.json":
                    AddReportMetric(reportName, "Input type", TryReadString(root, "InputType"), filePath);
                    AddReportMetric(reportName, "Large input detected", FormatBool(TryReadBoolValue(root, "LargeInputDetected")), filePath);
                    AddReportMetric(reportName, "Input size bytes", TryReadInt(root, "InputSizeBytes"), filePath);
                    AddReportMetric(reportName, "UI updates", TryReadInt(root, "ProgressUiUpdateCount"), filePath);
                    AddReportMetric(reportName, "Avg UI update gap ms", TryReadString(root, "AverageUiUpdateGapMilliseconds"), filePath);
                    AddReportMetric(reportName, "Max UI update gap ms", TryReadString(root, "MaxUiUpdateGapMilliseconds"), filePath);
                    AddReportMetric(reportName, "Cancellation latency ms", TryReadString(root, "CancellationLatencyMilliseconds"), filePath);
                    AddReportMetric(reportName, "Archive format", TryReadString(root, "ArchiveFormat"), filePath);
                    AddReportMetric(reportName, "Archive extraction samples", TryReadInt(root, "ArchiveProgressSampleCount"), filePath);
                    AddReportMetric(reportName, "Archive bytes copied", TryReadString(root, "ArchiveBytesCopied"), filePath);
                    AddReportMetric(reportName, "Archive bytes estimated", TryReadString(root, "ArchiveBytesEstimated"), filePath);
                    AddReportMetric(reportName, "Archive throughput P10 MiB/s", TryReadString(root, "ArchiveThroughputP10MiBPerSecond"), filePath);
                    AddReportMetric(reportName, "Archive throughput P50 MiB/s", TryReadString(root, "ArchiveThroughputP50MiBPerSecond"), filePath);
                    AddReportMetric(reportName, "Archive throughput P90 MiB/s", TryReadString(root, "ArchiveThroughputP90MiBPerSecond"), filePath);
                    AddReportMetric(reportName, "Archive throughput baseline MiB/s", TryReadString(root, "ArchiveThroughputBaselineMiBPerSecond"), filePath);
                    AddReportMetric(reportName, "Archive throughput baseline hardware tier", TryReadString(root, "ArchiveThroughputHardwareTier"), filePath);
                    AddReportMetric(reportName, "Archive throughput baseline sample count", TryReadString(root, "ArchiveThroughputBaselineSampleCount"), filePath);
                    AddReportMetric(reportName, "Archive throughput below baseline", FormatBool(TryReadBoolValue(root, "ArchiveThroughputBelowBaseline")), filePath);
                    AddReportMetric(reportName, "Outcome", TryReadString(root, "Outcome"), filePath);
                    break;
                case "conversion-matrix-pack-proof.json":
                    AddReportMetric(reportName, "Target body", TryReadString(root, "TargetBody"), filePath);
                    AddReportMetric(reportName, "Total conversions", TryReadInt(root, "TotalCount"), filePath);
                    AddReportMetric(reportName, "Strict proof ready count", TryReadInt(root, "StrictProofReadyCount"), filePath);
                    AddReportMetric(reportName, "Non-strict proof count", TryReadInt(root, "NonStrictProofCount"), filePath);
                    AddReportMetric(reportName, "Unique matrix coordinates", TryReadInt(root, "UniqueMatrixCoordinateCount"), filePath);
                    AddReportMetric(reportName, "Target bodies", TryReadArray(root, "DistinctTargetBodies"), filePath);
                    AddReportMetric(reportName, "Unique target bodies", TryReadInt(root, "UniqueTargetBodyCount"), filePath);
                    AddReportMetric(reportName, "Target body families", TryReadArray(root, "DistinctTargetBodyFamilies"), filePath);
                    AddReportMetric(reportName, "Support tiers", TryReadArray(root, "DistinctSupportTiers"), filePath);
                    AddReportMetric(reportName, "Proof coverage", TryReadString(root, "ProofCoverage"), filePath);
                    AddReportMetric(reportName, "Strict proof ready", FormatBool(TryReadBoolValue(root, "StrictProofReady")), filePath);
                    AddReportMetric(reportName, "Missing proof axes", TryReadArray(root, "MissingProofAxes"), filePath);
                    AddReportMetric(reportName, "Missing matrix dimensions", TryReadArray(root, "MissingMatrixDimensions"), filePath);
                    AddReportMetric(reportName, "Missing matrix combinations", TryReadArray(root, "MissingMatrixCombinations"), filePath);
                    AddReportMetric(reportName, "Blocking proof gaps", TryReadArray(root, "BlockingGaps"), filePath);
                    AddReportMetric(reportName, "Axis summaries", CountNestedArray(root, "Axes"), filePath);
                    AddReportMetric(reportName, "Matrix dimensions", CountNestedArray(root, "MatrixDimensionCoverage"), filePath);
                    AddReportMetric(reportName, "Matrix dimensions meeting minimum coverage", CountObjectsWithBool(root, "MatrixDimensionCoverage", "MeetsMinimumCoverage", expected: true), filePath);
                    AddReportMetric(reportName, "Matrix combinations", CountNestedArray(root, "MatrixCombinationCoverage"), filePath);
                    AddReportMetric(reportName, "Matrix combinations meeting minimum coverage", CountObjectsWithBool(root, "MatrixCombinationCoverage", "MeetsMinimumCoverage", expected: true), filePath);
                    AddReportMetric(reportName, "Review artifacts", TryReadArray(root, "ReviewArtifacts"), filePath);
                    break;
                case "remaining-gaps-checklist.json":
                case "remaining-gaps-pack-checklist.json":
                    AddReportMetric(reportName, "Target body", TryReadString(root, "TargetBody"), filePath);
                    AddReportMetric(reportName, "Checklist source report", TryReadString(root, "SourceReport"), filePath);
                    AddReportMetric(reportName, "Proof coverage", TryReadString(root, "ProofCoverage"), filePath);
                    AddReportMetric(reportName, "Strict proof ready", FormatBool(TryReadBoolValue(root, "StrictProofReady")), filePath);
                    AddReportMetric(reportName, "Remaining proof gaps", CountNestedArray(root, "RemainingGaps"), filePath);
                    AddReportMetric(reportName, "Remaining gap categories", DistinctNestedArrayValues(root, "RemainingGaps", "Category"), filePath);
                    AddReportMetric(reportName, "Top remaining gaps", TryReadRemainingGapHighlights(root), filePath);
                    AddReportMetric(reportName, "Review artifacts", TryReadArray(root, "ReviewArtifacts"), filePath);
                    break;
                case "topology-correspondence.json":
                    AddReportMetric(reportName, "Target body", TryReadString(root, "TargetBody"), filePath);
                    AddReportMetric(reportName, "Support tier", TryReadString(root, "SupportTier"), filePath);
                    AddReportMetric(reportName, "Can convert", FormatBool(TryReadNestedBoolValue(root, "ConversionReadiness", "CanConvert")), filePath);
                    AddReportMetric(reportName, "Can safely animate", FormatBool(TryReadNestedBoolValue(root, "ConversionReadiness", "CanSafelyAnimate")), filePath);
                    AddReportMetric(reportName, "Manual cleanup likely", FormatBool(TryReadBoolValue(root, "ManualCleanupLikely")), filePath);
                    AddReportMetric(reportName, "Runtime verification required", FormatBool(TryReadBoolValue(root, "RuntimeVerificationRequired")), filePath);
                    AddReportMetric(reportName, "Topology correspondence", TryReadNestedString(root, "TopologyCorrespondence", "Classification"), filePath);
                    AddReportMetric(reportName, "Topology matching mode", TryReadNestedString(root, "TopologyCorrespondence", "MatchingMode"), filePath);
                    AddReportMetric(reportName, "True semantic correspondence", FormatBool(TryReadNestedBoolValue(root, "TopologyCorrespondence", "UsesTrueSemanticCorrespondence")), filePath);
                    AddReportMetric(reportName, "Semantic vertex matching", TryReadNestedString(root, "TopologyCorrespondence", "SemanticVertexMatchingStatus"), filePath);
                    AddReportMetric(reportName, "Semantic anchor profile", TryReadNestedString(root, "TopologyCorrespondence", "SemanticAnchorProfile"), filePath);
                    AddReportMetric(reportName, "Semantic anchor coverage", TryReadNestedString(root, "TopologyCorrespondence", "SemanticAnchorCoverage"), filePath);
                    AddReportMetric(reportName, "Topology focus regions", TryReadNestedArray(root, "TopologyCorrespondence", "FocusRegions"), filePath);
                    AddReportMetric(reportName, "Unmatched topology focus regions", TryReadNestedArray(root, "TopologyCorrespondence", "UnmatchedFocusRegions"), filePath);
                    AddReportMetric(reportName, "Topology recommendations", TryReadNestedArray(root, "TopologyCorrespondence", "Recommendations"), filePath);
                    AddReportMetric(reportName, "Review artifacts", TryReadArray(root, "ReviewArtifacts"), filePath);
                    break;
                case "desktop-workflow-automation.json":
                    AddReportMetric(reportName, "Preview tab", TryReadNestedString(root, "ValidationState", "PreviewTabTitle"), filePath);
                    AddReportMetric(reportName, "Guidance tab", TryReadNestedString(root, "ValidationState", "GuidanceTabTitle"), filePath);
                    AddReportMetric(reportName, "Desktop automation coverage", TryReadNestedString(root, "AutomationContract", "Coverage"), filePath);
                    AddReportMetric(reportName, "Manual WinForms interaction", FormatBool(TryReadNestedBoolValue(root, "AutomationContract", "RequiresManualWinFormsInteraction")), filePath);
                    AddReportMetric(reportName, "Windows host required", FormatBool(TryReadNestedBoolValue(root, "AutomationContract", "RequiresWindowsHost")), filePath);
                    AddReportMetric(reportName, "Embedded preview runtime dependency", FormatBool(TryReadNestedBoolValue(root, "AutomationContract", "RequiresWebViewRuntimeForEmbeddedPreview")), filePath);
                    AddReportMetric(reportName, "Automated WebView interaction", FormatBool(TryReadNestedBoolValue(root, "AutomationContract", "SupportsAutomatedWebViewInteraction")), filePath);
                    AddReportMetric(reportName, "True UI E2E automation", FormatBool(TryReadNestedBoolValue(root, "AutomationContract", "SupportsTrueUiEndToEndAutomation")), filePath);
                    AddReportMetric(reportName, "GUI flow steps", CountNestedArray(root, "SuggestedGuiFlow"), filePath);
                    AddReportMetric(reportName, "Blocking GUI steps", CountObjectsWithBool(root, "SuggestedGuiFlow", "Blocking", expected: true), filePath);
                    AddReportMetric(reportName, "GUI flow highlights", TryReadGuiFlowHighlights(root), filePath);
                    break;
                case "windows-ui-e2e-automation.json":
                    AddReportMetric(reportName, "Planned UI coverage", TryReadString(root, "PlannedCoverage"), filePath);
                    AddReportMetric(reportName, "UI automation coverage", TryReadString(root, "Coverage"), filePath);
                    AddReportMetric(reportName, "UI proof execution", TryReadNestedString(root, "ProofExecution", "ExecutedStatus"), filePath);
                    AddReportMetric(reportName, "UI imported proof", FormatBool(TryReadNestedBoolValue(root, "ProofExecution", "ImportedResultAvailable")), filePath);
                    AddReportMetric(reportName, "Missing UI flows", TryReadNestedArray(root, "ProofExecution", "MissingItems"), filePath);
                    AddReportMetric(reportName, "Blocking proof axes", TryReadArray(root, "BlockingProofAxes"), filePath);
                    AddReportMetric(reportName, "Matrix combinations targeted", TryReadArray(root, "MatrixCombinationsTargeted"), filePath);
                    AddReportMetric(reportName, "Windows host required", FormatBool(TryReadBoolValue(root, "RequiresWindowsHost")), filePath);
                    AddReportMetric(reportName, "External UI harness", FormatBool(TryReadBoolValue(root, "RequiresExternalUiHarness")), filePath);
                    AddReportMetric(reportName, "Embedded preview runtime required", FormatBool(TryReadBoolValue(root, "RequiresEmbeddedPreviewRuntimeForInAppPreview")), filePath);
                    AddReportMetric(reportName, "Supported UI flows", TryReadArray(root, "SupportedFlows"), filePath);
                    AddReportMetric(reportName, "Automation signals", TryReadArray(root, "AutomationSignals"), filePath);
                    AddReportMetric(reportName, "UI selectors", CountNestedArray(root, "Selectors"), filePath);
                    AddReportMetric(reportName, "UI automation steps", CountNestedArray(root, "Steps"), filePath);
                    AddReportMetric(reportName, "UI step highlights", TryReadWindowsUiStepHighlights(root), filePath);
                    break;
                case "proof-harness-bundle.json":
                    AddReportMetric(reportName, "Target body", TryReadString(root, "TargetBody"), filePath);
                    AddReportMetric(reportName, "Canonical entrypoint", TryReadString(root, "CanonicalEntryPoint"), filePath);
                    AddReportMetric(reportName, "Harness components", CountNestedArray(root, "Components"), filePath);
                    AddReportMetric(reportName, "Host requirements", CountNestedArray(root, "HostRequirements"), filePath);
                    AddReportMetric(reportName, "Expected result files", TryReadArray(root, "ExpectedResultFiles"), filePath);
                    break;
                case "proof-result-bundle.json":
                    AddReportMetric(reportName, "Harness overall status", TryReadString(root, "OverallStatus"), filePath);
                    AddReportMetric(reportName, "Harness kind", TryReadString(root, "HarnessKind"), filePath);
                    AddReportMetric(reportName, "Proof host OS", TryReadNestedString(root, "Host", "OperatingSystem"), filePath);
                    AddReportMetric(reportName, "Proof runner", TryReadNestedString(root, "Host", "HarnessRunner"), filePath);
                    AddReportMetric(reportName, "Proof components", CountNestedArray(root, "ComponentResults"), filePath);
                    AddReportMetric(reportName, "Imported scenarios", CountNestedArray(root, "ScenarioResults"), filePath);
                    AddReportMetric(reportName, "Imported probes", CountNestedArray(root, "ProbeResults"), filePath);
                    AddReportMetric(reportName, "Missing expected probes", TryReadArray(root, "MissingExpectedProbes"), filePath);
                    break;
                case "mod-stack-cross-validation.json":
                    AddReportMetric(reportName, "Target body", TryReadString(root, "TargetBody"), filePath);
                    AddReportMetric(reportName, "Target body family", TryReadString(root, "TargetBodyFamily"), filePath);
                    AddReportMetric(reportName, "Support tier", TryReadString(root, "SupportTier"), filePath);
                    AddReportMetric(reportName, "Source skeleton reliability", TryReadString(root, "SourceSkeletonReliability"), filePath);
                    AddReportMetric(reportName, "Requires load-order validation", FormatBool(TryReadBoolValue(root, "RequiresLoadOrderValidation")), filePath);
                    AddReportMetric(reportName, "Requires plugin patch review", FormatBool(TryReadBoolValue(root, "RequiresPluginPatchReview")), filePath);
                    AddReportMetric(reportName, "Plugins", TryReadInt(root, "ScannedPluginCount"), filePath);
                    AddReportMetric(reportName, "Armor add-ons", TryReadInt(root, "ArmorAddonCount"), filePath);
                    AddReportMetric(reportName, "Armor records", TryReadInt(root, "ArmorRecordCount"), filePath);
                    AddReportMetric(reportName, "Ambiguous plugins", TryReadInt(root, "AmbiguousPluginCount"), filePath);
                    AddReportMetric(reportName, "Declared masters", TryReadInt(root, "DistinctDeclaredMasterCount"), filePath);
                    AddReportMetric(reportName, "Linked armor families", TryReadInt(root, "LinkedArmorFamilyCount"), filePath);
                    AddReportMetric(reportName, "Plugin mesh families", TryReadArray(root, "DistinctMeshFamilies"), filePath);
                    AddReportMetric(reportName, "Race warnings", TryReadArray(root, "RaceWarnings"), filePath);
                    AddReportMetric(reportName, "Incompatible races", TryReadArray(root, "IncompatibleRaces"), filePath);
                    AddReportMetric(reportName, "Recommended runtime scenarios", TryReadArray(root, "RecommendedRuntimeScenarios"), filePath);
                    AddReportMetric(reportName, "Suggested harness actions", TryReadArray(root, "SuggestedHarnessActions"), filePath);
                    break;
                case "race-compatibility.json":
                    AddReportMetric(reportName, "Target body", TryReadString(root, "TargetBody"), filePath);
                    AddReportMetric(reportName, "Plugins", CountNestedArray(root, "ScannedPlugins"), filePath);
                    AddReportMetric(reportName, "Armor add-ons", TryReadInt(root, "ArmorAddonCount"), filePath);
                    AddReportMetric(reportName, "Compatible", FormatBool(TryReadBoolValue(root, "IsCompatible")), filePath);
                    AddReportMetric(reportName, "Incompatible races", TryReadArray(root, "IncompatibleRaces"), filePath);
                    AddReportMetric(reportName, "Warnings", TryReadArray(root, "Warnings"), filePath);
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
                    AddReportMetric(reportName, "Source physics", FormatBool(TryReadBoolValue(root, "SourcePhysicsDetected")), filePath);
                    AddReportMetric(reportName, "Ground mesh", FormatBool(TryReadBoolValue(root, "GroundMeshAvailable")), filePath);
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

        var item = new ListViewItem([reportName, property, value]) { Tag = filePath };
        ApplyReportMetricHighlighting(item, reportName, property, value);
        _reportsListView.Items.Add(item);
    }

    private static void ApplyReportMetricHighlighting(ListViewItem item, string reportName, string property, string value)
    {
        if (!reportName.Equals("runtime-stress-pass.json", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (!property.Equals("Archive throughput below baseline", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (!value.Equals("Yes", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        item.BackColor = Color.FromArgb(255, 246, 214);
        item.ForeColor = Color.FromArgb(149, 73, 0);
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

    private static string? FormatBool(bool? value) =>
        value is null ? null : value.Value ? "Yes" : "No";

    private static bool? TryReadBoolValue(JsonElement element, string propertyName) =>
        TryGetProperty(element, propertyName, out var value) && (value.ValueKind == JsonValueKind.True || value.ValueKind == JsonValueKind.False)
            ? value.GetBoolean()
            : null;

    private static string? TryReadNestedString(JsonElement element, string objectPropertyName, string nestedPropertyName) =>
        TryGetProperty(element, objectPropertyName, out var nested) && nested.ValueKind == JsonValueKind.Object
            ? TryReadString(nested, nestedPropertyName)
            : null;

    private static bool? TryReadNestedBoolValue(JsonElement element, string objectPropertyName, string nestedPropertyName) =>
        TryGetProperty(element, objectPropertyName, out var nested) && nested.ValueKind == JsonValueKind.Object
            ? TryReadBoolValue(nested, nestedPropertyName)
            : null;

    private static string? TryReadNestedArray(JsonElement element, string objectPropertyName, string nestedPropertyName) =>
        TryGetProperty(element, objectPropertyName, out var nested) && nested.ValueKind == JsonValueKind.Object
            ? TryReadArray(nested, nestedPropertyName)
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

    private static ConversionValidationSummary? TryReadWorstValidationSummary(IReadOnlyList<string> outputDirectories)
    {
        return outputDirectories
            .Where(static directory => !string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .SelectMany(EnumerateValidationSummaryCandidates)
            .Select(TryReadValidationSummaryFromReport)
            .Where(static summary => summary is not null)
            .Cast<ConversionValidationSummary>()
            .OrderByDescending(static summary => ConversionValidationPresentation.GetGateRank(summary.Status))
            .ThenByDescending(static summary => summary.HighSeverityCount)
            .ThenByDescending(static summary => summary.MediumSeverityCount)
            .ThenByDescending(static summary => summary.LowSeverityCount)
            .ThenBy(static summary => summary.Score)
            .FirstOrDefault();
    }

    private static IEnumerable<string> EnumerateValidationSummaryCandidates(string outputDirectory)
    {
        var conversionQualityPath = Path.Combine(outputDirectory, "conversion-quality.json");
        if (File.Exists(conversionQualityPath))
        {
            yield return conversionQualityPath;
        }

        var batchReportPath = Path.Combine(outputDirectory, "batch-report.json");
        if (File.Exists(batchReportPath))
        {
            yield return batchReportPath;
        }
    }

    private static ConversionValidationSummary? TryReadValidationSummaryFromReport(string reportPath)
    {
        try
        {
            using var document = OpenJsonDocument(reportPath);
            return TryReadValidationSummary(document.RootElement)
                ?? TryReadBatchValidationSummary(document.RootElement);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            System.Diagnostics.Trace.TraceWarning($"Failed to read validation summary from '{reportPath}': {ex.Message}");
            return null;
        }
    }

    private static ConversionValidationSummary? TryReadBatchValidationSummary(JsonElement element)
    {
        if (!TryGetProperty(element, "Results", out var results) || results.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var status = TryReadString(element, "PackReadinessStatus");
        if (string.IsNullOrWhiteSpace(status))
        {
            status = results.EnumerateArray()
                .Select(static item => TryReadString(item, "ValidationStatus"))
                .Where(static value => !string.IsNullOrWhiteSpace(value))
                .OrderByDescending(static value => ConversionValidationPresentation.GetGateRank(value!))
                .FirstOrDefault();
        }

        if (string.IsNullOrWhiteSpace(status))
        {
            return null;
        }

        var score = TryReadIntValue(element, "AverageValidationScore")
            ?? results.EnumerateArray()
                .Select(static item => TryReadIntValue(item, "ValidationScore"))
                .Where(static value => value.HasValue)
                .Select(static value => value!.Value)
                .DefaultIfEmpty(0)
                .Min();

        var explicitHighSeverityCount = TryReadIntValue(element, "HighSeverityCount");
        var explicitMediumSeverityCount = TryReadIntValue(element, "MediumSeverityCount");
        var explicitLowSeverityCount = TryReadIntValue(element, "LowSeverityCount");
        var resultStatuses = results.EnumerateArray()
            .Select(static item => TryReadString(item, "ValidationStatus"))
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => value!)
            .ToArray();
        var highSeverityCount = explicitHighSeverityCount
            ?? resultStatuses.Count(static value => ConversionValidationPresentation.GetGateRank(value) >= ConversionValidationPresentation.GetGateRank("high-risk"));
        var mediumSeverityCount = explicitMediumSeverityCount
            ?? resultStatuses.Count(static value => ConversionValidationPresentation.GetGateRank(value) == ConversionValidationPresentation.GetGateRank("needs-review"));
        var lowSeverityCount = explicitLowSeverityCount
            ?? resultStatuses.Count(static value => ConversionValidationPresentation.GetGateRank(value) == ConversionValidationPresentation.GetGateRank("ready"));

        return new ConversionValidationSummary(
            status,
            score,
            highSeverityCount,
            mediumSeverityCount,
            lowSeverityCount,
            []);
    }

    private static void AppendGuidanceFromBatchReport(
        string outputDirectory,
        string? previewPath,
        Action<string, string, string, string?> add,
        ref bool requiresReview,
        Action<string?> promoteGate)
    {
        var reportPath = Path.Combine(outputDirectory, "batch-report.json");
        if (!File.Exists(reportPath))
        {
            return;
        }

        try
        {
            using var document = OpenJsonDocument(reportPath);
            var root = document.RootElement;
            var totalCount = TryReadIntValue(root, "TotalCount") ?? 0;
            var successCount = TryReadIntValue(root, "SuccessCount") ?? 0;
            var failedCount = TryReadIntValue(root, "FailedCount") ?? 0;
            var needsReviewCount = TryReadIntValue(root, "NeedsReviewCount") ?? 0;
            var highRiskCount = TryReadIntValue(root, "HighRiskCount") ?? 0;
            var missingQualityCount = TryReadIntValue(root, "MissingQualityReportCount") ?? 0;
            var packStatus = TryReadString(root, "PackReadinessStatus") ?? "Unknown";

            if (totalCount <= 0)
            {
                return;
            }

            promoteGate(NormalizeGateStatus(packStatus));

            if (failedCount > 0 || needsReviewCount > 0 || highRiskCount > 0 || missingQualityCount > 0)
            {
                requiresReview = true;
            }

            add(
                "Batch results",
                failedCount > 0 || highRiskCount > 0 ? "Warning" : "Info",
                $"Batch summary: {successCount}/{totalCount} succeeded, {failedCount} failed, {needsReviewCount} need review, {highRiskCount} are high risk. Pack status: {packStatus}.",
                reportPath);

            if (failedCount > 0)
            {
                add(
                    "Action card",
                    "Warning",
                    $"Failed conversions detected ({failedCount}). Recommended flow: 1) open batch-report.json, 2) re-run failed items only, 3) validate conversion-quality.json before packaging.",
                    reportPath);
            }
            else if (needsReviewCount > 0 || highRiskCount > 0)
            {
                add(
                    "Action card",
                    "Action",
                    $"Partial conversion state: {needsReviewCount} need review and {highRiskCount} are high risk. Review preview-workbench + conversion-quality before treating this pack as release-ready.",
                    reportPath);
            }

            if (failedCount > 0)
            {
                add(
                    "Batch follow-up",
                    "Action",
                    $"Open batch-report.json and re-run or isolate the {failedCount} failed item(s) before publishing the pack.",
                    reportPath);
            }

            if (missingQualityCount > 0)
            {
                add(
                    "Batch follow-up",
                    "Action",
                    $"Some outputs are missing conversion-quality.json ({missingQualityCount} item(s)). Re-run those conversions before installing or sharing the results.",
                    ResolveGuidanceTargetPath(outputDirectory, previewPath, "missing-conversion-quality-report", reportPath));
            }
        }
        catch (Exception ex)
        {
            requiresReview = true;
            add("Batch results", "Warning", $"Could not read batch-report.json: {ex.Message}", reportPath);
        }
    }

    private static void AppendGuidanceFromConversionQuality(
        string outputDirectory,
        string? previewPath,
        Action<string, string, string, string?> add,
        ref bool requiresReview,
        Action<string?> promoteGate)
    {
        var qualityPath = Path.Combine(outputDirectory, "conversion-quality.json");
        if (!File.Exists(qualityPath))
        {
            return;
        }

        try
        {
            using var document = OpenJsonDocument(qualityPath);
            var root = document.RootElement;
            var targetBody = TryReadString(root, "TargetBody") ?? "target body";
            var validationSummary = TryReadValidationSummary(root);
            if (validationSummary is null)
            {
                return;
            }

            promoteGate(validationSummary.Status);

            if (!validationSummary.Status.Equals("READY", StringComparison.OrdinalIgnoreCase) ||
                validationSummary.Issues.Count > 0)
            {
                requiresReview = true;
            }

            add(
                "Validation",
                validationSummary.Status.Equals("READY", StringComparison.OrdinalIgnoreCase) ? "Info" : "Warning",
                $"{ConversionValidationPresentation.GetGateLabel(validationSummary.Status)}: {ConversionValidationPresentation.GetDispositionMessage(validationSummary.Status)} Score {validationSummary.Score}. Open conversion-quality.json for the full breakdown.",
                qualityPath);

            if (!validationSummary.Status.Equals("READY", StringComparison.OrdinalIgnoreCase))
            {
                add(
                    "Action card",
                    ConversionValidationPresentation.GetGateRank(validationSummary.Status) >= ConversionValidationPresentation.GetGateRank("high-risk")
                        ? "Warning"
                        : "Action",
                    $"Validation is {ConversionValidationPresentation.GetGateLabel(validationSummary.Status)} for {targetBody}. Use preview-workbench and top listed issues to resolve blockers, then re-run conversion before publishing.",
                    qualityPath);
            }

            var prioritizedIssues = ConversionValidationGuidance.PrioritizeIssues(validationSummary, maxIssues: 4).ToArray();

            foreach (var issue in prioritizedIssues.Take(3))
            {
                add(
                    GetGuidanceAreaForIssueCode(issue.Code),
                    ToDisplayPriority(issue.Severity),
                    issue.Message,
                    ResolveGuidanceTargetPath(outputDirectory, previewPath, issue.Code, qualityPath));
            }

            foreach (var issue in prioritizedIssues)
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

    private static void AppendGuidanceFromSkeletonCompatibility(
        string outputDirectory,
        string? previewPath,
        Action<string, string, string, string?> add,
        ref bool requiresReview)
    {
        var reportPath = Path.Combine(outputDirectory, "skeleton-compatibility.json");
        if (!File.Exists(reportPath))
        {
            return;
        }

        try
        {
            using var document = OpenJsonDocument(reportPath);
            var root = document.RootElement;
            var sourceSkeleton = TryReadString(root, "SourceSkeleton") ?? "unknown";
            var targetSkeleton = TryReadString(root, "TargetSkeleton") ?? "unknown";
            var unsupportedBones = ReadArrayValues(root, "UnsupportedBones");
            JsonElement physicsCompatibility = default;
            var hasPhysicsCompatibility = TryGetProperty(root, "PhysicsCompatibility", out physicsCompatibility);
            var physicsMissingBones = hasPhysicsCompatibility ? ReadArrayValues(physicsCompatibility, "MissingBones") : [];
            var physicsRemappedBones = hasPhysicsCompatibility ? ReadArrayValues(physicsCompatibility, "RemappedBones") : [];
            var physicsMissingConfigs = hasPhysicsCompatibility ? ReadArrayValues(physicsCompatibility, "MissingRuntimeConfigs") : [];
            var physicsGeneratedConfigs = hasPhysicsCompatibility ? ReadArrayValues(physicsCompatibility, "GeneratedRuntimeConfigs") : [];
            var physicsRequestedProfile = hasPhysicsCompatibility
                ? TryReadString(physicsCompatibility, "RequestedProfile")
                : null;
            var physicsSummary = hasPhysicsCompatibility
                ? TryReadString(physicsCompatibility, "Summary")
                : null;
            var physicsCompatible = hasPhysicsCompatibility
                ? TryReadBoolValue(physicsCompatibility, "IsCompatible")
                : null;
            var targetBodySupportsPhysics = hasPhysicsCompatibility
                ? TryReadBoolValue(physicsCompatibility, "TargetBodySupportsPhysics")
                : null;
            if (unsupportedBones.Count == 0)
            {
                add(
                    "Skeleton review",
                    "Info",
                    $"Skeleton mapping looks clean: {sourceSkeleton} → {targetSkeleton}. No unsupported bones were reported.",
                    reportPath);
            }
            else
            {
                requiresReview = true;
                add(
                    "Skeleton review",
                    "Warning",
                    $"{unsupportedBones.Count} unsupported bone(s) were reported while mapping {sourceSkeleton} → {targetSkeleton}: {BuildListPreview(unsupportedBones)}.",
                    ResolveGuidanceTargetPath(outputDirectory, previewPath, "unsupported-bones", reportPath));
                add(
                    "Skeleton next step",
                    "Action",
                    "Open skeleton-compatibility.json and verify follower/custom/beast bones plus any required physics chains before installing the converted mesh in-game.",
                    ResolveGuidanceTargetPath(outputDirectory, previewPath, "unsupported-bones", reportPath));
            }

            if (hasPhysicsCompatibility && !string.IsNullOrWhiteSpace(physicsRequestedProfile))
            {
                if (targetBodySupportsPhysics == false)
                {
                    requiresReview = true;
                    add(
                        "Physics compatibility",
                        "Warning",
                        string.IsNullOrWhiteSpace(physicsSummary)
                            ? $"Physics profile {physicsRequestedProfile} is not supported by target skeleton/body {targetSkeleton}. Generated configs: {BuildListPreview(physicsGeneratedConfigs)}."
                            : physicsSummary,
                        ResolveGuidanceTargetPath(outputDirectory, previewPath, "physics-profile-unsupported", reportPath));
                    add(
                        "Physics next step",
                        "Action",
                        "Switch to a physics-capable target body or skeleton, or set Physics to None before packaging this output.",
                        ResolveGuidanceTargetPath(outputDirectory, previewPath, "physics-profile-unsupported", reportPath));
                }
                else if (physicsMissingConfigs.Count > 0)
                {
                    requiresReview = true;
                    add(
                        "Physics compatibility",
                        "Warning",
                        string.IsNullOrWhiteSpace(physicsSummary)
                            ? $"Physics profile {physicsRequestedProfile} did not generate all required runtime configs: {BuildListPreview(physicsMissingConfigs)}."
                            : physicsSummary,
                        ResolveGuidanceTargetPath(outputDirectory, previewPath, "physics-config-mismatch", reportPath));
                    add(
                        "Physics next step",
                        "Action",
                        "Open skeleton-compatibility.json and world-physics.json, then restore the missing runtime config outputs or choose a compatible physics profile before sharing the package.",
                        ResolveGuidanceTargetPath(outputDirectory, previewPath, "physics-config-mismatch", reportPath));
                }
                else if (physicsCompatible == false || physicsMissingBones.Count > 0)
                {
                    requiresReview = true;
                    add(
                        "Physics compatibility",
                        "Warning",
                        string.IsNullOrWhiteSpace(physicsSummary)
                            ? $"Physics profile {physicsRequestedProfile} is not fully compatible with {targetSkeleton}: {BuildListPreview(physicsMissingBones)}."
                            : physicsSummary,
                        ResolveGuidanceTargetPath(outputDirectory, previewPath, "physics-bone-missing", reportPath));
                    add(
                        "Physics next step",
                        "Action",
                        "Open skeleton-compatibility.json, compare the requested physics profile against the expected and missing target bones, then disable or replace unsupported physics chains before shipping.",
                        ResolveGuidanceTargetPath(outputDirectory, previewPath, "physics-bone-missing", reportPath));
                }
                else if (physicsRemappedBones.Count > 0)
                {
                    requiresReview = true;
                    add(
                        "Physics compatibility",
                        "Warning",
                        string.IsNullOrWhiteSpace(physicsSummary)
                            ? $"Physics profile {physicsRequestedProfile} needed remapped chains: {BuildListPreview(physicsRemappedBones)}."
                            : physicsSummary,
                        ResolveGuidanceTargetPath(outputDirectory, previewPath, "physics-bone-remap", reportPath));
                }
                else
                {
                    add(
                        "Physics compatibility",
                        "Info",
                        string.IsNullOrWhiteSpace(physicsSummary)
                            ? $"Physics profile {physicsRequestedProfile} matches the target skeleton/body capability."
                            : physicsSummary,
                        reportPath);
                }
            }
        }
        catch (Exception ex)
        {
            requiresReview = true;
            add("Skeleton review", "Warning", $"Could not read skeleton-compatibility.json: {ex.Message}", reportPath);
        }
    }

    private static void AppendGuidanceFromTextureSummary(
        string outputDirectory,
        string? previewPath,
        Action<string, string, string, string?> add,
        ref bool requiresReview)
    {
        var reportPath = Path.Combine(outputDirectory, "texture-summary.json");
        if (!File.Exists(reportPath))
        {
            return;
        }

        try
        {
            using var document = OpenJsonDocument(reportPath);
            var root = document.RootElement;
            var missingNormals = ReadArrayValues(root, "MissingNormals");
            if (missingNormals.Count == 0)
            {
                return;
            }

            requiresReview = true;
            add(
                "Texture review",
                "Warning",
                $"{missingNormals.Count} texture set(s) are missing normal maps: {BuildListPreview(missingNormals)}.",
                ResolveGuidanceTargetPath(outputDirectory, previewPath, "missing-normal-maps", reportPath));
            add(
                "Texture next step",
                "Action",
                "Copy or generate the missing normal maps before packaging so the converted armor does not lose surface detail or look flat in-game.",
                ResolveGuidanceTargetPath(outputDirectory, previewPath, "missing-normal-maps", reportPath));
        }
        catch (Exception ex)
        {
            requiresReview = true;
            add("Texture review", "Warning", $"Could not read texture-summary.json: {ex.Message}", reportPath);
        }
    }

    private static void AppendGuidanceFromDependencyMap(
        string outputDirectory,
        string? previewPath,
        Action<string, string, string, string?> add,
        ref bool requiresReview)
    {
        var reportPath = Path.Combine(outputDirectory, "dependency-map.json");
        if (!File.Exists(reportPath))
        {
            return;
        }

        try
        {
            using var document = OpenJsonDocument(reportPath);
            var root = document.RootElement;
            var sourceBodies = ReadDistinctArrayPropertyValues(root, "DetectedSourceBody");
            var sourceSkeletons = ReadDistinctArrayPropertyValues(root, "SourceSkeleton");
            var linkedArmorFamilies = SumNestedArrayInt(root, "LinkedArmaFormIds");

            if (sourceBodies.Count > 1)
            {
                requiresReview = true;
                add(
                    "Ecosystem mix",
                    "Action",
                    $"The output references multiple detected source body ecosystems ({BuildListPreview(sourceBodies)}). Smoke-test the converted pack in your mod manager before release.",
                    reportPath);
            }

            if (sourceSkeletons.Count > 1)
            {
                requiresReview = true;
                add(
                    "Ecosystem mix",
                    "Warning",
                    $"Multiple source skeleton families were detected ({BuildListPreview(sourceSkeletons)}). Recheck race/follower coverage and plugin load order before publishing.",
                    reportPath);
            }

            if (linkedArmorFamilies > 0)
            {
                add(
                    "Plugin context",
                    "Info",
                    $"Dependency map includes {linkedArmorFamilies} linked ARMA reference(s). Keep plugin-patches.json with the packaged output so install/load-order guidance ships with the conversion.",
                    ResolveGuidanceTargetPath(outputDirectory, previewPath, "plugin-rewrite-verification-warning", reportPath));
            }
        }
        catch (Exception ex)
        {
            requiresReview = true;
            add("Dependency review", "Warning", $"Could not read dependency-map.json: {ex.Message}", reportPath);
        }
    }

    private static void AppendGuidanceFromPackValidation(
        string outputDirectory,
        string? previewPath,
        Action<string, string, string, string?> add,
        ref bool requiresReview,
        Action<string?> promoteGate)
    {
        var reportPath = Path.Combine(outputDirectory, "armor-pack-validation.json");
        if (!File.Exists(reportPath))
        {
            return;
        }

        try
        {
            using var document = OpenJsonDocument(reportPath);
            var root = document.RootElement;
            var status = TryReadString(root, "PackReadinessStatus");
            var needsReviewCount = TryReadIntValue(root, "NeedsReviewCount") ?? 0;
            var highRiskCount = TryReadIntValue(root, "HighRiskCount") ?? 0;
            if (string.IsNullOrWhiteSpace(status))
            {
                return;
            }

            var normalizedStatus = NormalizeGateStatus(status);
            promoteGate(normalizedStatus);

            if (!string.Equals(normalizedStatus, "ready", StringComparison.OrdinalIgnoreCase) ||
                needsReviewCount > 0 ||
                highRiskCount > 0)
            {
                requiresReview = true;
            }

            add(
                "Packaging",
                string.Equals(normalizedStatus, "ready", StringComparison.OrdinalIgnoreCase) ? "Info" : "Warning",
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

    private static void AppendGuidanceFromFomodArtifacts(
        string outputDirectory,
        Action<string, string, string, string?> add)
    {
        var metaIniPath = ResolveExistingGuidancePath(outputDirectory, "meta.ini");
        if (metaIniPath is not null)
        {
            add(
                "Packaging assets",
                "Info",
                "Open meta.ini from Files to verify the generated Mod Organizer 2 package metadata before sharing the conversion output.",
                metaIniPath);
        }

        var moduleConfigPath = ResolveExistingGuidancePath(outputDirectory, Path.Combine("fomod", "ModuleConfig.xml"));
        if (moduleConfigPath is not null)
        {
            add(
                "Packaging assets",
                "Info",
                "Open fomod/ModuleConfig.xml from Files to verify the MO2/Vortex install mapping for meshes, plugins, CalienteTools, and staged support files.",
                moduleConfigPath);
        }

        var infoPath = ResolveExistingGuidancePath(outputDirectory, Path.Combine("fomod", "info.xml"));
        if (infoPath is not null)
        {
            add(
                "Packaging assets",
                "Info",
                "Open fomod/info.xml from Files to verify the generated package metadata and install notes before sharing the conversion output.",
                infoPath);
        }

        var sliderGroupsDirectory = Path.Combine(outputDirectory, "CalienteTools", "BodySlide", "SliderGroups");
        if (Directory.Exists(sliderGroupsDirectory))
        {
            var sliderGroupsPath = Directory
                .EnumerateFiles(sliderGroupsDirectory, "*.xml", SearchOption.TopDirectoryOnly)
                .FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(sliderGroupsPath))
            {
                add(
                    "BodySlide assets",
                    "Info",
                    "Open the generated BodySlide SliderGroups XML from Files to verify the converted sets will appear under the expected BodySlide batch-build groups.",
                    sliderGroupsPath);
            }
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
            using var document = OpenJsonDocument(patchPath);
            var root = document.RootElement;
            var patchSteps = TryReadArrayCount(root, "ProposedPatchSteps");
            var rewriteMappings = TryReadArrayCount(root, "RewriteMappings");
            if (patchSteps <= 0 && rewriteMappings <= 0)
            {
                return;
            }

            add(
                "Plugin patching",
                "Info",
                $"Open plugin-patches.json if the mod ships ESP/ESM/ESL files. Review {rewriteMappings} rewrite mapping(s) and {patchSteps} proposed patch step(s) in xEdit context before release.",
                ResolveGuidanceTargetPath(outputDirectory, previewPath, "plugin-rewrite-verification-warning", patchPath));

            if (TryGetProperty(root, "PluginInstallHints", out var pluginInstallHints) && pluginInstallHints.ValueKind == JsonValueKind.Array)
            {
                foreach (var hint in pluginInstallHints.EnumerateArray().Take(2))
                {
                    var sourcePlugin = TryReadString(hint, "SourcePlugin") ?? "plugin";
                    var generatedPatch = TryReadString(hint, "GeneratedPatchPlugin");
                    var manualReview = TryReadBoolValue(hint, "ManualReviewRequired");
                    var loadAfter = TryReadArray(hint, "RecommendedPluginLoadAfter");
                    var placement = TryReadString(hint, "RecommendedModManagerPlacement");
                    var notes = TryReadArray(hint, "Notes");

                    if (!string.IsNullOrWhiteSpace(generatedPatch))
                    {
                        var loadAfterLabel = !string.IsNullOrWhiteSpace(loadAfter) && !string.Equals(loadAfter, "None", StringComparison.OrdinalIgnoreCase)
                            ? loadAfter
                            : sourcePlugin;
                        add(
                            "Plugin install",
                            "Action",
                            $"{generatedPatch} should load after {loadAfterLabel}. {placement}",
                            patchPath);
                    }

                    if (manualReview == true)
                    {
                        requiresReview = true;
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

                    requiresReview = true;
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

    private static void AppendGuidanceFromConversionMatrixProof(
        string outputDirectory,
        string? previewPath,
        Action<string, string, string, string?> add,
        ref bool requiresReview)
    {
        AppendGuidanceFromConversionMatrixProofReport(
            Path.Combine(outputDirectory, "conversion-matrix-proof.json"),
            "Matrix proof",
            outputDirectory,
            previewPath,
            add,
            ref requiresReview);

        AppendGuidanceFromConversionMatrixProofReport(
            Path.Combine(outputDirectory, "conversion-matrix-pack-proof.json"),
            "Pack matrix proof",
            outputDirectory,
            previewPath,
            add,
            ref requiresReview);

        AppendGuidanceFromRemainingGapsChecklist(
            Path.Combine(outputDirectory, "remaining-gaps-checklist.json"),
            "Matrix gaps checklist",
            add,
            ref requiresReview);
        AppendGuidanceFromRemainingGapsChecklist(
            Path.Combine(outputDirectory, "remaining-gaps-pack-checklist.json"),
            "Pack gaps checklist",
            add,
            ref requiresReview);
    }

    private static void AppendGuidanceFromConversionMatrixProofReport(
        string reportPath,
        string area,
        string outputDirectory,
        string? previewPath,
        Action<string, string, string, string?> add,
        ref bool requiresReview)
    {
        if (!File.Exists(reportPath))
        {
            return;
        }

        try
        {
            using var document = OpenJsonDocument(reportPath);
            var root = document.RootElement;
            var proofCoverage = TryReadString(root, "ProofCoverage") ?? "unknown";
            var proofExecutionStatus = TryReadString(root, "ProofExecutionStatus") ?? "planned-only";
            var strictProofReady = TryReadBoolValue(root, "StrictProofReady") == true;
            var missingProofAxes = ReadArrayValues(root, "MissingProofAxes");
            var blockingGaps = ReadArrayValues(root, "BlockingGaps");
            var missingDimensions = ReadArrayValues(root, "MissingMatrixDimensions");
            var missingCombinations = ReadArrayValues(root, "MissingMatrixCombinations");
            var targetBody = TryReadString(root, "TargetBody") ?? "this output";
            var guidanceTarget = ResolveMatrixProofGuidanceTargetPath(
                outputDirectory,
                previewPath,
                reportPath,
                missingProofAxes);

            if (strictProofReady)
            {
                add(
                    area,
                    "Info",
                    $"{targetBody} is marked strict-proof-ready. Open {Path.GetFileName(reportPath)} if you want the full axis-by-axis proof summary before sharing.",
                    guidanceTarget);
                return;
            }

            requiresReview = true;

            var axisSummary = missingProofAxes.Count > 0
                ? $" Missing proof axes: {BuildListPreview(missingProofAxes)}."
                : string.Empty;
            var dimensionSummary = missingDimensions.Count > 0
                ? $" Missing matrix dimensions: {BuildListPreview(missingDimensions)}."
                : string.Empty;
            var combinationSummary = missingCombinations.Count > 0
                ? $" Missing matrix combinations: {BuildListPreview(missingCombinations)}."
                : string.Empty;

            add(
                area,
                "Warning",
                $"{targetBody} is not fully proven across the current conversion matrix yet ({proofCoverage}; proof execution: {proofExecutionStatus}).{axisSummary}{dimensionSummary}{combinationSummary}",
                guidanceTarget);
            add(
                $"{area} current capability",
                "Info",
                $"Conversion output is still generated and usable now for {targetBody}; this warning means universal/strict proof coverage is incomplete, not that conversion is blocked.",
                guidanceTarget);
            add(
                "Action card",
                "Action",
                $"Release-level universal proof is still incomplete for {targetBody}. This does not invalidate a single successful conversion; it means broader matrix evidence is still missing. Open {Path.GetFileName(reportPath)} and close missing proof axes/dimensions first.",
                guidanceTarget);

            if (!string.Equals(proofExecutionStatus, "executed-pass", StringComparison.OrdinalIgnoreCase))
            {
                var externalProofTarget = File.Exists(Path.Combine(outputDirectory, "proof-result-bundle.json"))
                    ? Path.Combine(outputDirectory, "proof-result-bundle.json")
                    : Path.Combine(outputDirectory, "proof-harness-bundle.json");
                var proofAction = string.Equals(proofExecutionStatus, "planned-only", StringComparison.OrdinalIgnoreCase)
                    ? "Open proof-harness-bundle.json, run the required external Windows/Desktop/runtime/live-game proof steps, then import proof-result-bundle.json so this output stops being plan-only."
                    : $"Open {Path.GetFileName(externalProofTarget)} and resolve the incomplete imported proof evidence before treating this output as release-ready.";
                add(
                    $"{area} proof handoff",
                    "Action",
                    proofAction,
                    externalProofTarget);
            }

            if (blockingGaps.Count > 0)
            {
                add(
                    $"{area} next step",
                    "Action",
                    $"Open {Path.GetFileName(reportPath)} and work through the top blocking gap first: {blockingGaps[0]}",
                    guidanceTarget);
            }

            foreach (var axis in missingProofAxes.Take(4))
            {
                add(
                    $"{area} gap",
                    "Action",
                    BuildMatrixProofAxisActionText(axis),
                    ResolveMatrixProofGuidanceTargetPath(outputDirectory, previewPath, reportPath, [axis]));
            }
        }
        catch (Exception ex)
        {
            requiresReview = true;
            add(area, "Warning", $"Could not read {Path.GetFileName(reportPath)}: {ex.Message}", reportPath);
        }
    }

    private static void AppendGuidanceFromRemainingGapsChecklist(
        string reportPath,
        string area,
        Action<string, string, string, string?> add,
        ref bool requiresReview)
    {
        if (!File.Exists(reportPath))
        {
            return;
        }

        try
        {
            using var document = OpenJsonDocument(reportPath);
            var root = document.RootElement;
            var strictProofReady = TryReadBoolValue(root, "StrictProofReady") == true;
            var targetBody = TryReadString(root, "TargetBody") ?? "this output";
            var sourceReport = TryReadString(root, "SourceReport") ?? "matrix proof report";
            var proofCoverage = TryReadString(root, "ProofCoverage") ?? "unknown";
            var gaps = new List<string>();
            if (TryGetProperty(root, "RemainingGaps", out var remainingGaps) && remainingGaps.ValueKind == JsonValueKind.Array)
            {
                gaps.AddRange(remainingGaps.EnumerateArray()
                    .Select(static item => TryReadString(item, "Description"))
                    .Where(static item => !string.IsNullOrWhiteSpace(item))
                    .Cast<string>());
            }

            if (strictProofReady || gaps.Count == 0)
            {
                add(
                    area,
                    "Info",
                    $"{targetBody} has no remaining strict/universal blockers in {Path.GetFileName(sourceReport)}.",
                    reportPath);
                return;
            }

            requiresReview = true;
            add(
                area,
                "Action",
                $"{targetBody} still has {gaps.Count} concrete blocker(s) ({proofCoverage}). Start with: {gaps[0]}",
                reportPath);
            if (gaps.Count > 1)
            {
                add(
                    $"{area} next blockers",
                    "Action",
                    $"Then work through: {BuildListPreview(gaps.Skip(1).Take(3).ToList())}",
                    reportPath);
            }
        }
        catch (Exception ex)
        {
            requiresReview = true;
            add(area, "Warning", $"Could not read {Path.GetFileName(reportPath)}: {ex.Message}", reportPath);
        }
    }

    private static void AppendGuidanceFromModStackCrossValidation(
        string outputDirectory,
        string? previewPath,
        Action<string, string, string, string?> add,
        ref bool requiresReview)
    {
        var reportPath = Path.Combine(outputDirectory, "mod-stack-cross-validation.json");
        if (!File.Exists(reportPath))
        {
            return;
        }

        try
        {
            using var document = OpenJsonDocument(reportPath);
            var root = document.RootElement;
            var targetBody = TryReadString(root, "TargetBody") ?? "target body";
            var requiresLoadOrderValidation = TryReadBoolValue(root, "RequiresLoadOrderValidation") == true;
            var requiresPluginPatchReview = TryReadBoolValue(root, "RequiresPluginPatchReview") == true;
            var pluginCount = TryReadIntValue(root, "ScannedPluginCount") ?? 0;
            var masterCount = TryReadIntValue(root, "DistinctDeclaredMasterCount") ?? 0;
            var meshFamilies = TryReadArray(root, "DistinctMeshFamilies");
            var runtimeScenarios = TryReadArray(root, "RecommendedRuntimeScenarios");
            var skeletonReliability = TryReadString(root, "SourceSkeletonReliability");

            if (requiresLoadOrderValidation)
            {
                requiresReview = true;
                add(
                    "Load order",
                    "High",
                    $"Large mixed-stack validation is still required for {targetBody}. Review {pluginCount} plugin(s), {masterCount} declared master chain(s), and the recommended runtime scenarios before release.",
                    ResolveGuidanceTargetPath(outputDirectory, previewPath, "runtime-validation-plan", reportPath));
            }

            if (requiresPluginPatchReview)
            {
                requiresReview = true;
                add(
                    "Plugin / race stack",
                    "Action",
                    $"Cross-check plugin-patches.json and mod-stack-cross-validation.json together before release. Mesh families: {meshFamilies ?? "Unknown"}. Runtime scenarios: {runtimeScenarios ?? "See report"}.",
                    ResolveGuidanceTargetPath(outputDirectory, previewPath, "plugin-rewrite-verification-warning", reportPath));
            }

            if (string.Equals(skeletonReliability, "provisional", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(skeletonReliability, "review", StringComparison.OrdinalIgnoreCase))
            {
                requiresReview = true;
                add(
                    "Custom skeleton stack",
                    string.Equals(skeletonReliability, "provisional", StringComparison.OrdinalIgnoreCase) ? "High" : "Action",
                    $"Source skeleton reliability is {skeletonReliability}. Validate the mixed load order against the real rig before treating this conversion as pack-ready.",
                    ResolveGuidanceTargetPath(outputDirectory, previewPath, "skeleton-compatibility-report", reportPath));
            }
        }
        catch (Exception ex)
        {
            requiresReview = true;
            add("Load order", "Warning", $"Could not read mod-stack-cross-validation.json: {ex.Message}", reportPath);
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
            using var document = OpenJsonDocument(reportPath);
            var root = document.RootElement;
            var mode = TryReadString(root, "Mode");
            var groundMeshAvailable = FormatBool(TryReadBoolValue(root, "GroundMeshAvailable"));
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

    private static void AppendGuidanceFromInGameValidation(
        string outputDirectory,
        string? previewPath,
        Action<string, string, string, string?> add,
        ref bool requiresReview)
    {
        var reportPath = Path.Combine(outputDirectory, "in-game-validation.json");
        if (!File.Exists(reportPath))
        {
            return;
        }

        try
        {
            using var stream = File.OpenRead(reportPath);
            var report = JsonSerializer.Deserialize<InGameValidationReport>(stream, ReportJsonOptions);
            if (report is null)
            {
                requiresReview = true;
                add("Runtime scenarios", "Warning", "Could not materialize in-game-validation.json. Open the report directly before release.", reportPath);
                return;
            }

            foreach (var entry in InGameValidationGuidance.BuildDesktopGuidanceEntries(report))
            {
                var priority = entry.Priority;
                if (string.Equals(priority, "High", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(priority, "Action", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(priority, "Warning", StringComparison.OrdinalIgnoreCase))
                {
                    requiresReview = true;
                }

                add(
                    entry.Area,
                    string.IsNullOrWhiteSpace(priority) ? "Info" : priority,
                    entry.Details,
                    ResolveInGameGuidanceTargetPath(outputDirectory, previewPath, entry.ArtifactPath, reportPath));
            }

            AppendHardCaseRegionGuidance(report, outputDirectory, previewPath, reportPath, add, ref requiresReview);
        }
        catch (Exception ex)
        {
            requiresReview = true;
            add("In-game validation", "Warning", $"Could not read in-game-validation.json: {ex.Message}", reportPath);
        }
    }

    private static void AppendHardCaseRegionGuidance(
        InGameValidationReport report,
        string outputDirectory,
        string? previewPath,
        string reportPath,
        Action<string, string, string, string?> add,
        ref bool requiresReview)
    {
        var regions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var region in report.CoreBodyRegions)
        {
            if (!string.IsNullOrWhiteSpace(region))
            {
                regions.Add(region);
            }
        }

        foreach (var region in report.SensitiveRegions)
        {
            if (!string.IsNullOrWhiteSpace(region))
            {
                regions.Add(region);
            }
        }

        if (report.TopologyCorrespondence?.FocusRegions is { Count: > 0 } topologyRegions)
        {
            foreach (var region in topologyRegions)
            {
                if (!string.IsNullOrWhiteSpace(region))
                {
                    regions.Add(region);
                }
            }
        }

        static bool MatchesRegion(IReadOnlyCollection<string> focusRegions, params string[] tokens) =>
            focusRegions.Any(region => tokens.Any(token => region.Contains(token, StringComparison.OrdinalIgnoreCase)));

        if (report.TopologyCorrespondence?.StrictTransferBlockers is { Count: > 0 } strictTransferBlockers)
        {
            requiresReview = true;
            add(
                "Topology transfer",
                "High",
                $"Strict topology transfer blockers were reported ({BuildListPreview(strictTransferBlockers)}). Keep conversion-quality.json and in-game-validation.json with the output and resolve these blockers before release.",
                ResolveGuidanceTargetPath(outputDirectory, previewPath, "topology-partition-review", reportPath));
        }

        if (MatchesRegion(regions, "heel", "foot", "toe", "ankle", "calf"))
        {
            requiresReview = true;
            add(
                "Footwear",
                "Action",
                "Hard-case footwear regions were flagged. Validate heel height, toe roll, floor contact, and weight-0/1 transitions in preview-workbench.html and world-physics.json before release.",
                ResolveGuidanceTargetPath(outputDirectory, previewPath, "heel-offset-review", reportPath));
        }

        if (MatchesRegion(regions, "head", "face", "jaw", "mouth", "oral", "tongue", "teeth"))
        {
            requiresReview = true;
            add(
                "Head / oral fit",
                "Action",
                "Head/oral regions were flagged for runtime checks. Verify jaw and mouth animation poses plus neck seam behavior using in-game-validation.json before publishing.",
                ResolveInGameGuidanceTargetPath(outputDirectory, previewPath, "in-game-validation.json", reportPath));
        }

        if (MatchesRegion(regions, "genital", "groin", "vagina", "penis", "anus", "schlong", "pelvis"))
        {
            requiresReview = true;
            add(
                "Genital fit",
                "Action",
                "Genital/groin regions were flagged. Run collision-heavy animation checks, verify partition assignments, and confirm no clipping/penetration regressions before release.",
                ResolveInGameGuidanceTargetPath(outputDirectory, previewPath, "in-game-validation.json", reportPath));
        }

        if (MatchesRegion(regions, "head", "oral", "mouth", "genital", "groin", "heel", "foot", "toe"))
        {
            var bodySlideTarget = ResolveBodySlideGuidanceTargetPath(outputDirectory) ?? reportPath;
            var hasSliderSets = ResolveExistingGuidancePath(outputDirectory, Path.Combine("CalienteTools", "BodySlide", "SliderSets")) is not null;
            var hasSliderGroups = ResolveExistingGuidancePath(outputDirectory, Path.Combine("CalienteTools", "BodySlide", "SliderGroups")) is not null;
            var hasShapeData = ResolveExistingGuidancePath(outputDirectory, Path.Combine("CalienteTools", "BodySlide", "ShapeData")) is not null;
            if (!hasSliderSets || !hasSliderGroups || !hasShapeData)
            {
                requiresReview = true;
            }

            add(
                "BodySlide placement",
                !hasSliderSets || !hasSliderGroups || !hasShapeData ? "Warning" : "Info",
                !hasSliderSets || !hasSliderGroups || !hasShapeData
                    ? "Hard-case regions were detected and one or more BodySlide artifacts are missing (SliderSets, SliderGroups, or ShapeData). Regenerate and verify OSP/XML/ShapeData placement before packaging."
                    : "Hard-case regions were detected. Verify generated OSP/XML/ShapeData placements and slider payload links so BodySlide shows and rebuilds the converted set correctly.",
                bodySlideTarget);
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

        if (normalized is "missing-source-partitions" or "missing-plugin-partitions" or "topology-partition-review")
        {
            return "Partition review";
        }

        if (normalized.StartsWith("plugin-", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("race-compatibility-warning", StringComparison.OrdinalIgnoreCase) ||
            normalized is "missing-plugin-patch-report" or "missing-xedit-script" or "missing-root-plugin")
        {
            return "Plugin review";
        }

        if (IsPackagingReviewGuidanceCode(normalized))
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

        if (normalized is "unsupported-bones")
        {
            return ResolveExistingGuidancePath(outputDirectory, "skeleton-compatibility.json") ?? fallbackPath;
        }

        if (normalized is "physics-profile-unsupported" or "physics-config-mismatch")
        {
            return ResolveExistingGuidancePath(outputDirectory, "skeleton-compatibility.json")
                ?? ResolveExistingGuidancePath(outputDirectory, "world-physics.json")
                ?? fallbackPath;
        }

        if (normalized is "missing-normal-maps")
        {
            return ResolveExistingGuidancePath(outputDirectory, "texture-summary.json") ?? fallbackPath;
        }

        if (normalized is "clipping-detected" or "voxel-penetration" or "pose-risk" or "auto-correction-applied")
        {
            return ResolveExistingGuidancePath(outputDirectory, "pose-simulation-report.json")
                ?? ResolveExistingGuidancePath(outputDirectory, "conversion-quality.json")
                ?? fallbackPath;
        }

        if (normalized is "heel-offset-review" or "missing-world-physics-report")
        {
            return ResolveExistingGuidancePath(outputDirectory, "world-physics.json")
                ?? ResolveExistingGuidancePath(outputDirectory, "conversion-quality.json")
                ?? fallbackPath;
        }

        if (normalized.Equals("race-compatibility-warning", StringComparison.OrdinalIgnoreCase))
        {
            return ResolveExistingGuidancePath(outputDirectory, "race-compatibility.json")
                ?? ResolveExistingGuidancePath(outputDirectory, "plugin-patches.json")
                ?? fallbackPath;
        }

        if (normalized.StartsWith("plugin-", StringComparison.OrdinalIgnoreCase))
        {
            return ResolveExistingGuidancePath(outputDirectory, "plugin-patches.json") ?? fallbackPath;
        }

        if (normalized is "missing-plugin-patch-report" or "missing-xedit-script")
        {
            return ResolveExistingGuidancePath(outputDirectory, "plugin-patches.json")
                ?? ResolveExistingGuidancePath(outputDirectory, "patch-armor.pas")
                ?? outputDirectory;
        }

        if (normalized is "missing-readme")
        {
            return ResolveExistingGuidancePath(outputDirectory, "README.txt") ?? outputDirectory;
        }

        if (normalized is "missing-dependency-map" or "missing-conversion-quality-report")
        {
            return ResolveExistingGuidancePath(outputDirectory, "dependency-map.json")
                ?? ResolveExistingGuidancePath(outputDirectory, "conversion-quality.json")
                ?? outputDirectory;
        }

        if (normalized is "missing-skeleton-compatibility-report")
        {
            return ResolveExistingGuidancePath(outputDirectory, "skeleton-compatibility.json") ?? outputDirectory;
        }

        if (normalized is "missing-pose-report")
        {
            return ResolveExistingGuidancePath(outputDirectory, "pose-simulation-report.json")
                ?? ResolveExistingGuidancePath(outputDirectory, "conversion-quality.json")
                ?? outputDirectory;
        }

        if (normalized is "missing-staged-cbpc-config")
        {
            return ResolveExistingGuidancePath(outputDirectory, Path.Combine("SKSE", "Plugins", "CBPCSystem", "cbpc-config.xml"))
                ?? ResolveExistingGuidancePath(outputDirectory, Path.Combine("SKSE", "Plugins", "CBPCSystem"))
                ?? outputDirectory;
        }

        if (normalized is "missing-staged-smp-config")
        {
            return ResolveExistingGuidancePath(outputDirectory, Path.Combine("SKSE", "Plugins", "hdtSMP64", "smp-config.xml"))
                ?? ResolveExistingGuidancePath(outputDirectory, Path.Combine("SKSE", "Plugins", "hdtSMP64"))
                ?? outputDirectory;
        }

        if (normalized is "bodyslide-incompatible" or "incomplete-source-fallback" ||
            normalized.StartsWith("missing-bodyslide-", StringComparison.OrdinalIgnoreCase))
        {
            return ResolveBodySlideGuidanceTargetPath(outputDirectory) ?? fallbackPath;
        }

        if (normalized.StartsWith("zip-missing-", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("missing-output-zip", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("invalid-output-zip", StringComparison.OrdinalIgnoreCase))
        {
            var expectedZipPath = outputDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + ".zip";
            if (File.Exists(expectedZipPath))
            {
                return expectedZipPath;
            }

            return Directory
                       .EnumerateFiles(outputDirectory, "*.zip", SearchOption.TopDirectoryOnly)
                       .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                       .FirstOrDefault()
                   ?? outputDirectory;
        }

        if (normalized.StartsWith("fomod-", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("missing-fomod-module-config", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("missing-fomod-info", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("missing-meta-ini", StringComparison.OrdinalIgnoreCase))
        {
            if (normalized.Equals("missing-meta-ini", StringComparison.OrdinalIgnoreCase))
            {
                return ResolveExistingGuidancePath(outputDirectory, "meta.ini")
                    ?? ResolveExistingGuidancePath(outputDirectory, Path.Combine("fomod", "ModuleConfig.xml"))
                    ?? ResolveExistingGuidancePath(outputDirectory, Path.Combine("fomod", "info.xml"))
                    ?? fallbackPath;
            }

            return ResolveExistingGuidancePath(outputDirectory, Path.Combine("fomod", "ModuleConfig.xml"))
                ?? ResolveExistingGuidancePath(outputDirectory, Path.Combine("fomod", "info.xml"))
                ?? ResolveExistingGuidancePath(outputDirectory, "fomod")
                ?? fallbackPath;
        }

        if (normalized is "missing-staged-mesh-output" or "zip-missing-staged-mesh-output")
        {
            return ResolveExistingGuidancePath(outputDirectory, Path.Combine("meshes", "slidesmith"))
                ?? ResolveExistingGuidancePath(outputDirectory, "meshes")
                ?? outputDirectory;
        }

        if (normalized is "missing-root-support-file")
        {
            return ResolveExistingGuidancePath(outputDirectory, "armor-pack-validation.json")
                ?? ResolveExistingGuidancePath(outputDirectory, "conversion-quality.json")
                ?? outputDirectory;
        }

        if (normalized is "missing-root-plugin")
        {
            return ResolveFirstExistingPath(outputDirectory, "*.esp", "*.esm", "*.esl") ?? outputDirectory;
        }

        if (IsPreviewDrivenGuidanceCode(normalized) &&
            !string.IsNullOrWhiteSpace(previewPath) &&
            File.Exists(previewPath))
        {
            return previewPath;
        }

        return fallbackPath;
    }

    private static string ResolveInGameGuidanceTargetPath(
        string outputDirectory,
        string? previewPath,
        string? artifactPath,
        string fallbackPath)
    {
        if (!string.IsNullOrWhiteSpace(artifactPath))
        {
            if (!string.IsNullOrWhiteSpace(previewPath) &&
                Path.GetFileName(artifactPath).Equals(Path.GetFileName(previewPath), StringComparison.OrdinalIgnoreCase) &&
                File.Exists(previewPath))
            {
                return previewPath;
            }

            if (Path.IsPathRooted(artifactPath) && (File.Exists(artifactPath) || Directory.Exists(artifactPath)))
            {
                return artifactPath;
            }

            if (ResolveExistingGuidancePath(outputDirectory, artifactPath) is { } resolvedArtifactPath)
            {
                return resolvedArtifactPath;
            }
        }

        return fallbackPath;
    }

    private static string ResolveMatrixProofGuidanceTargetPath(
        string outputDirectory,
        string? previewPath,
        string reportPath,
        IReadOnlyList<string> missingProofAxes)
    {
        foreach (var artifact in ConversionMatrixProofGuidance.GetPreferredArtifactsForAxes(missingProofAxes))
        {
            if (artifact.Equals("preview-workbench.html", StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrWhiteSpace(previewPath) && File.Exists(previewPath))
                {
                    return previewPath;
                }

                continue;
            }

            if (ResolveExistingGuidancePath(outputDirectory, artifact) is { } resolved)
            {
                return resolved;
            }
        }

        return reportPath;
    }

    private static string BuildMatrixProofAxisActionText(string? axis) =>
        ConversionMatrixProofGuidance.BuildAxisActionText(axis);

    private static string? NormalizeGateStatus(string? status)
    {
        var rank = ConversionValidationPresentation.GetGateRank(status);
        if (rank >= ConversionValidationPresentation.GetGateRank("high-risk"))
        {
            return "high-risk";
        }

        if (rank >= ConversionValidationPresentation.GetGateRank("needs-review"))
        {
            return "needs-review";
        }

        if (rank >= ConversionValidationPresentation.GetGateRank("ready"))
        {
            return "ready";
        }

        return null;
    }

    private static JsonDocument OpenJsonDocument(string path)
    {
        var json = File.ReadAllText(path);
        return JsonDocument.Parse(json, new JsonDocumentOptions
        {
            AllowTrailingCommas = ReportJsonOptions.AllowTrailingCommas,
            CommentHandling = ReportJsonOptions.ReadCommentHandling
        });
    }

    private static bool IsPreviewDrivenGuidanceCode(string code) =>
        code.Equals("low-detection-confidence", StringComparison.OrdinalIgnoreCase) ||
        code.Equals("low-body-match", StringComparison.OrdinalIgnoreCase) ||
        code.Equals("unsupported-nif-layout", StringComparison.OrdinalIgnoreCase) ||
        code.Equals("heuristic-nif-read", StringComparison.OrdinalIgnoreCase) ||
        code.Equals("synthetic-morph-fallback", StringComparison.OrdinalIgnoreCase) ||
        code.Equals("retargeted-morph-reuse", StringComparison.OrdinalIgnoreCase) ||
        code.Equals("topology-mismatch-risk", StringComparison.OrdinalIgnoreCase) ||
        code.Equals("missing-source-partitions", StringComparison.OrdinalIgnoreCase) ||
        code.Equals("missing-plugin-partitions", StringComparison.OrdinalIgnoreCase) ||
        code.Equals("topology-partition-review", StringComparison.OrdinalIgnoreCase) ||
        code.Equals("clipping-detected", StringComparison.OrdinalIgnoreCase) ||
        code.Equals("voxel-penetration", StringComparison.OrdinalIgnoreCase) ||
        code.Equals("pose-risk", StringComparison.OrdinalIgnoreCase) ||
        code.Equals("auto-correction-applied", StringComparison.OrdinalIgnoreCase) ||
        code.Equals("heel-offset-review", StringComparison.OrdinalIgnoreCase) ||
        code.Equals("plugin-link-unsupported-nif-layout", StringComparison.OrdinalIgnoreCase) ||
        code.Equals("missing-preview-workbench", StringComparison.OrdinalIgnoreCase) ||
        code.Equals("missing-preview-html", StringComparison.OrdinalIgnoreCase) ||
        code.Equals("missing-preview-svg", StringComparison.OrdinalIgnoreCase);

    private static bool IsPackagingReviewGuidanceCode(string code) =>
        code.StartsWith("zip-", StringComparison.OrdinalIgnoreCase) ||
        code.StartsWith("fomod-", StringComparison.OrdinalIgnoreCase) ||
        code.Equals("invalid-output-zip", StringComparison.OrdinalIgnoreCase) ||
        code.Equals("missing-readme", StringComparison.OrdinalIgnoreCase) ||
        code.Equals("missing-dependency-map", StringComparison.OrdinalIgnoreCase) ||
        code.Equals("missing-conversion-quality-report", StringComparison.OrdinalIgnoreCase) ||
        code.Equals("missing-staged-cbpc-config", StringComparison.OrdinalIgnoreCase) ||
        code.Equals("missing-staged-smp-config", StringComparison.OrdinalIgnoreCase) ||
        code.Equals("missing-staged-mesh-output", StringComparison.OrdinalIgnoreCase) ||
        code.Equals("missing-fomod-module-config", StringComparison.OrdinalIgnoreCase) ||
        code.Equals("missing-fomod-info", StringComparison.OrdinalIgnoreCase) ||
        code.Equals("missing-meta-ini", StringComparison.OrdinalIgnoreCase) ||
        code.Equals("missing-output-zip", StringComparison.OrdinalIgnoreCase) ||
        code.Equals("missing-root-support-file", StringComparison.OrdinalIgnoreCase);

    private static string? ResolveExistingGuidancePath(string outputDirectory, string relativePath)
    {
        var fullPath = Path.Combine(outputDirectory, relativePath);
        if (File.Exists(fullPath) || Directory.Exists(fullPath))
        {
            return fullPath;
        }

        return null;
    }

    private static string? ResolveBodySlideGuidanceTargetPath(string outputDirectory)
    {
        return ResolveExistingGuidancePath(outputDirectory, Path.Combine("CalienteTools", "BodySlide", "ShapeData"))
            ?? ResolveExistingGuidancePath(outputDirectory, Path.Combine("CalienteTools", "BodySlide", "SliderGroups"))
            ?? ResolveExistingGuidancePath(outputDirectory, Path.Combine("CalienteTools", "BodySlide", "SliderSets"))
            ?? ResolveExistingGuidancePath(outputDirectory, Path.Combine("CalienteTools", "BodySlide"))
            ?? ResolveExistingGuidancePath(outputDirectory, "conversion-quality.json")
            ?? outputDirectory;
    }

    private static string? ResolveFirstExistingPath(string outputDirectory, params string[] patterns)
    {
        foreach (var pattern in patterns)
        {
            var match = Directory
                .EnumerateFiles(outputDirectory, pattern, SearchOption.TopDirectoryOnly)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(match))
            {
                return match;
            }
        }

        return null;
    }

    private static string BuildGuidanceOverview(IReadOnlyList<GuidanceEntry> entries, bool requiresReview, string? gateStatus)
    {
        var highCount = entries.Count(static entry => entry.Priority.Equals("High", StringComparison.OrdinalIgnoreCase));
        var warningCount = entries.Count(static entry => entry.Priority.Equals("Warning", StringComparison.OrdinalIgnoreCase));
        var actionCount = entries.Count(static entry => entry.Priority.Equals("Action", StringComparison.OrdinalIgnoreCase));
        var actualGate = gateStatus is not null &&
                         ConversionValidationPresentation.GetGateRank(gateStatus) >= ConversionValidationPresentation.GetGateRank("ready")
            ? gateStatus
            : null;
        var effectiveGate = ConversionValidationPresentation.GetGateRank(gateStatus) >= ConversionValidationPresentation.GetGateRank("high-risk")
            ? "high-risk"
            : (ConversionValidationPresentation.GetGateRank(gateStatus) >= ConversionValidationPresentation.GetGateRank("needs-review") || requiresReview)
                ? "needs-review"
                : string.Equals(actualGate, "ready", StringComparison.OrdinalIgnoreCase)
                    ? "ready"
                    : "informational";
        if (string.Equals(effectiveGate, "high-risk", StringComparison.OrdinalIgnoreCase))
        {
            return $"{ConversionValidationPresentation.GetGateLabel("high-risk")} (loaded output only) — this tab explains why and what to do next. Do not install/share yet: {highCount} high-priority, {warningCount} warning, and {actionCount} action item(s). Start with Preview, then open linked reports.";
        }

        if (string.Equals(effectiveGate, "needs-review", StringComparison.OrdinalIgnoreCase))
        {
            return $"{ConversionValidationPresentation.GetGateLabel("needs-review")} (loaded output only) — this tab is a review checklist before install/share: {warningCount} warning and {actionCount} action item(s).";
        }

        if (!string.Equals(effectiveGate, "ready", StringComparison.OrdinalIgnoreCase))
        {
            return $"Checklist loaded — {highCount} high-priority, {warningCount} warning, and {actionCount} action item(s). Work top-down and open linked reports only for rows that apply.";
        }

        return $"{ConversionValidationPresentation.GetGateLabel("ready")} (loaded output only) — keep this tab as the final verification checklist, then do one final Preview pass and smoke test before install/share.";
    }

    private static string FormatGuidanceAreaLabel(string area, string priority)
    {
        var severity = GetGuidancePriorityRank(priority);
        var marker = severity >= 4 ? "[HIGH]"
            : severity >= 3 ? "[WARN]"
            : severity >= 2 ? "[ACTION]"
            : severity >= 1 ? "[INFO]"
            : "[NOTE]";
        return $"{marker} {area}";
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
            "meta-ini" => "meta.ini",
            "staged-mesh-output" => "the generated meshes/slidesmith output",
            "xedit-script" => "patch-armor.pas",
            "plugin-patch-report" => "plugin-patches.json",
            "output-zip" => "the final distributable zip",
            "bodyslide-osp" => "the generated BodySlide SliderSets .osp file",
            "bodyslide-slider-groups" => "the generated BodySlide SliderGroups XML",
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

    private static IReadOnlyList<string> ReadArrayValues(JsonElement element, string propertyName)
    {
        if (!TryGetProperty(element, propertyName, out var value) || value.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return value
            .EnumerateArray()
            .Select(FormatJsonValue)
            .Where(static item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyList<string> ReadDistinctArrayPropertyValues(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return element
            .EnumerateArray()
            .Select(item => TryReadString(item, propertyName))
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static int SumNestedArrayInt(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Array)
        {
            return 0;
        }

        return element
            .EnumerateArray()
            .Sum(item => TryReadArrayCount(item, propertyName));
    }

    private static string BuildListPreview(IReadOnlyList<string> values)
    {
        if (values.Count == 0)
        {
            return "none";
        }

        return values.Count <= 3
            ? string.Join(", ", values)
            : $"{string.Join(", ", values.Take(3))} (+{values.Count - 3} more)";
    }

    private static string CountNestedArray(JsonElement element, string propertyName) =>
        TryGetProperty(element, propertyName, out var value) && value.ValueKind == JsonValueKind.Array
            ? value.GetArrayLength().ToString()
            : "0";

    private static string? TryReadScenarioHighlights(JsonElement element)
    {
        if (!TryGetProperty(element, "ScenarioMatrix", out var value) || value.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var items = value
            .EnumerateArray()
            .Select(static scenario => new
            {
                Name = TryReadString(scenario, "Name"),
                Priority = TryReadString(scenario, "Priority")
            })
            .Where(static item => !string.IsNullOrWhiteSpace(item.Name))
            .OrderByDescending(static item => GetScenarioPriorityRank(item.Priority))
            .ThenBy(static item => item.Name, StringComparer.OrdinalIgnoreCase)
            .Select(static item =>
                string.IsNullOrWhiteSpace(item.Priority) ? item.Name : $"{item.Name} [{item.Priority}]")
            .Take(4)
            .ToArray();

        return items.Length == 0 ? null : string.Join("; ", items);
    }

    private static string CountScenarioPriorities(JsonElement element, params string[] priorities)
    {
        if (!TryGetProperty(element, "ScenarioMatrix", out var value) || value.ValueKind != JsonValueKind.Array)
        {
            return "0";
        }

        return value
            .EnumerateArray()
            .Count(scenario =>
            {
                var priority = TryReadString(scenario, "Priority");
                return !string.IsNullOrWhiteSpace(priority) &&
                       priorities.Contains(priority, StringComparer.OrdinalIgnoreCase);
            })
            .ToString();
    }

    private static string? TryReadExecutionHighlights(JsonElement element)
    {
        if (!TryGetProperty(element, "Steps", out var value) || value.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var items = value
            .EnumerateArray()
            .Select(static step =>
            {
               var phase = TryReadString(step, "Phase");
               var name = TryReadString(step, "Name");
               return string.IsNullOrWhiteSpace(name)
                   ? null
                   : string.IsNullOrWhiteSpace(phase) ? name : $"{phase}: {name}";
            })
            .Where(static item => !string.IsNullOrWhiteSpace(item))
            .Take(4)
            .ToArray();

        return items.Length == 0 ? null : string.Join("; ", items);
    }

    private static string? TryReadGuiFlowHighlights(JsonElement element)
    {
        if (!TryGetProperty(element, "SuggestedGuiFlow", out var value) || value.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var items = value
            .EnumerateArray()
            .Select(static step =>
            {
               var area = TryReadString(step, "Area");
               var action = TryReadString(step, "Action");
               return string.IsNullOrWhiteSpace(action)
                   ? null
                   : string.IsNullOrWhiteSpace(area) ? action : $"{area}: {action}";
            })
            .Where(static item => !string.IsNullOrWhiteSpace(item))
            .Take(4)
            .ToArray();

        return items.Length == 0 ? null : string.Join("; ", items);
    }

    private static string? TryReadWindowsUiStepHighlights(JsonElement element)
    {
        if (!TryGetProperty(element, "Steps", out var value) || value.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var items = value
            .EnumerateArray()
            .Select(static step =>
            {
               var area = TryReadString(step, "Area");
               var signal = TryReadString(step, "ExpectedSignal");
               return string.IsNullOrWhiteSpace(area)
                   ? signal
                   : string.IsNullOrWhiteSpace(signal) ? area : $"{area}: {signal}";
            })
            .Where(static item => !string.IsNullOrWhiteSpace(item))
            .Take(4)
            .ToArray();

        return items.Length == 0 ? null : string.Join("; ", items);
    }

    private static string? TryReadRemainingGapHighlights(JsonElement element)
    {
        if (!TryGetProperty(element, "RemainingGaps", out var value) || value.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var items = value
            .EnumerateArray()
            .Select(static item => TryReadString(item, "Description"))
            .Where(static item => !string.IsNullOrWhiteSpace(item))
            .Take(4)
            .ToArray();
        return items.Length == 0 ? null : string.Join("; ", items);
    }

    private static string CountObjectsWithBool(JsonElement element, string arrayPropertyName, string boolPropertyName, bool expected)
    {
        if (!TryGetProperty(element, arrayPropertyName, out var value) || value.ValueKind != JsonValueKind.Array)
        {
            return "0";
        }

        return value
            .EnumerateArray()
            .Count(item => TryReadBoolValue(item, boolPropertyName) == expected)
            .ToString();
    }

    private static string CountObjectsWithString(JsonElement element, string arrayPropertyName, string propertyName, string expected)
    {
        if (!TryGetProperty(element, arrayPropertyName, out var value) || value.ValueKind != JsonValueKind.Array)
        {
            return "0";
        }

        return value
            .EnumerateArray()
            .Count(item => string.Equals(TryReadString(item, propertyName), expected, StringComparison.OrdinalIgnoreCase))
            .ToString();
    }

    private static string DistinctNestedArrayValues(JsonElement element, string arrayPropertyName, string nestedPropertyName)
    {
        if (!TryGetProperty(element, arrayPropertyName, out var value) || value.ValueKind != JsonValueKind.Array)
        {
            return "None";
        }

        var values = value
            .EnumerateArray()
            .Select(item => TryReadString(item, nestedPropertyName))
            .Where(static entry => !string.IsNullOrWhiteSpace(entry))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static entry => entry, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return values.Length == 0 ? "None" : BuildListPreview(values);
    }

    private static int GetScenarioPriorityRank(string? priority) =>
        priority?.Trim() switch
        {
            var value when string.Equals(value, "high", StringComparison.OrdinalIgnoreCase) => 3,
            var value when string.Equals(value, "action", StringComparison.OrdinalIgnoreCase) => 2,
            var value when string.Equals(value, "warning", StringComparison.OrdinalIgnoreCase) => 2,
            var value when string.Equals(value, "info", StringComparison.OrdinalIgnoreCase) => 1,
            _ => 0
        };

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
