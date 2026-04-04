using System.Collections.ObjectModel;
using VisualSqlArchitect.UI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using VisualSqlArchitect.Core;
using VisualSqlArchitect.Metadata;
using VisualSqlArchitect.UI.Services;
using VisualSqlArchitect.UI.Services.ConnectionManager;
using VisualSqlArchitect.UI.Services.Localization;
using VisualSqlArchitect.UI.ViewModels.Canvas;

namespace VisualSqlArchitect.UI.ViewModels;

// ── ViewModel ─────────────────────────────────────────────────────────────────

public sealed class ConnectionManagerViewModel : ViewModelBase
{
    // Latency above this threshold is considered "Degraded" rather than "Online"
    private const double DegradedLatencyThresholdMs = 500.0;
    // How often the background health monitor pings the active connection (seconds)
    // Value is defined in AppConstants.HealthCheckIntervalSeconds

    // ── Services ───────────────────────────────────────────────────────────────

    private readonly DatabaseConnectionService _dbConnectionService;
    private readonly ILogger<ConnectionManagerViewModel> _logger;
    private readonly CredentialVaultStore _credentialVault;
    private readonly LocalizationService _loc = LocalizationService.Instance;
    private readonly IConnectionErrorMessageMapper _errorMessageMapper;
    private readonly IConnectionStatusPresenter _statusPresenter;
    private readonly IConnectionCanvasPromptCoordinator _canvasPromptCoordinator;
    private readonly IConnectionHealthMonitorService _healthMonitorService;
    private readonly IConnectionSessionOrchestrator _sessionOrchestrator;
    private readonly IConnectionProfileStore _profileStore;
    private readonly IConnectionTestExecutor _connectionTestExecutor;
    private readonly IConnectionProfileFormMapper _formMapper;
    private readonly IConnectionActivationWorkflow _activationWorkflow;
    private readonly IConnectionProfileLifecycleService _profileLifecycleService;
    private readonly IFireAndForgetSafetyExecutor _fireAndForgetSafetyExecutor;
    private readonly IConnectionHealthLifecycleCoordinator _healthLifecycleCoordinator;

    /// <summary>
    /// Reference to the canvas search menu where database tables will be loaded.
    /// Set by the CanvasViewModel after initialization.
    /// </summary>
    public SearchMenuViewModel? SearchMenu { get; set; }

    /// <summary>
    /// Reference to the canvas view model to reset/update when connecting to a new database.
    /// Set by the CanvasViewModel after initialization.
    /// </summary>
    public CanvasViewModel? Canvas { get; set; }

    // ── Visibility ────────────────────────────────────────────────────────────

    private bool _isVisible;
    public bool IsVisible
    {
        get => _isVisible;
        set => Set(ref _isVisible, value);
    }

    // ── Profile list ──────────────────────────────────────────────────────────

    public ObservableCollection<ConnectionProfile> Profiles { get; } = [];

    private ConnectionProfile? _selectedProfile;
    public ConnectionProfile? SelectedProfile
    {
        get => _selectedProfile;
        set
        {
            Set(ref _selectedProfile, value);
            if (value is not null) LoadProfileIntoForm(value);
            DeleteProfileCommand.NotifyCanExecuteChanged();
            ConnectCommand.NotifyCanExecuteChanged();
        }
    }

    // ── Active connection & health ────────────────────────────────────────────

    private string? _activeProfileId;
    public string? ActiveProfileId
    {
        get => _activeProfileId;
        set
        {
            Set(ref _activeProfileId, value);
            RaisePropertyChanged(nameof(ActiveConnectionLabel));
            ActiveHealthStatus = value is null
                ? EConnectionHealthStatus.Unknown
                : EConnectionHealthStatus.Online;
            RestartHealthMonitor();
        }
    }

    private bool _isConnecting;
    public bool IsConnecting
    {
        get => _isConnecting;
        set => Set(ref _isConnecting, value);
    }

    /// <summary>
    /// True when NOT connecting (for UI bindings that need the inverse).
    /// </summary>
    public bool IsNotConnecting => !IsConnecting;

    public string ActiveConnectionLabel
    {
        get
        {
            var p = Profiles.FirstOrDefault(x => x.Id == _activeProfileId);
            return p is null ? _loc["connection.none"] : $"{p.Provider} · {p.Database}";
        }
    }

    private EConnectionHealthStatus _activeHealthStatus = EConnectionHealthStatus.Unknown;
    public EConnectionHealthStatus ActiveHealthStatus
    {
        get => _activeHealthStatus;
        private set
        {
            Set(ref _activeHealthStatus, value);
            RaisePropertyChanged(nameof(ConnectionIndicatorColor));
            RaisePropertyChanged(nameof(ConnectionHealthLabel));
            RaisePropertyChanged(nameof(ConnectionHealthTooltip));
        }
    }

    public string ConnectionIndicatorColor => _activeHealthStatus switch
    {
        EConnectionHealthStatus.Online   => "#4ADE80",
        EConnectionHealthStatus.Degraded => "#FBBF24",
        EConnectionHealthStatus.Offline  => "#EF4444",
        _                               => "#4A5568",
    };

    public string ConnectionHealthLabel => _activeHealthStatus switch
    {
        EConnectionHealthStatus.Online   => _loc["connection.health.online"],
        EConnectionHealthStatus.Degraded => _loc["connection.health.degraded"],
        EConnectionHealthStatus.Offline  => _loc["connection.health.offline"],
        _                               => _loc["connection.none"],
    };

    public string ConnectionHealthTooltip
    {
        get
        {
            var p = Profiles.FirstOrDefault(x => x.Id == _activeProfileId);
            if (p is null) return _loc["connection.tooltip.none"];
            var label = ConnectionHealthLabel;
            return $"{p.Name} ({p.Provider} · {p.Host}:{p.Port}/{p.Database}) — {label}";
        }
    }

    // ── Edit form ─────────────────────────────────────────────────────────────

    private bool _isEditing;
    public bool IsEditing
    {
        get => _isEditing;
        set => Set(ref _isEditing, value);
    }

    private string _editId = Guid.NewGuid().ToString();

    private string _editName = "New Connection";
    public string EditName { get => _editName; set => Set(ref _editName, value); }

    private DatabaseProvider _editProvider = DatabaseProvider.Postgres;
    public DatabaseProvider EditProvider
    {
        get => _editProvider;
        set
        {
            var oldDefault = ConnectionProfile.DefaultPort(_editProvider);
            Set(ref _editProvider, value);
            if (EditPort == oldDefault)
                EditPort = ConnectionProfile.DefaultPort(value);
        }
    }

    private string _editHost = AppConstants.DefaultHost;
    public string EditHost { get => _editHost; set => Set(ref _editHost, value); }

    private int _editPort = 5432;
    public int EditPort { get => _editPort; set => Set(ref _editPort, value); }

    private string _editDatabase = "";
    public string EditDatabase { get => _editDatabase; set => Set(ref _editDatabase, value); }

    private string _editUsername = "";
    public string EditUsername { get => _editUsername; set => Set(ref _editUsername, value); }

    private string _editPassword = "";
    public string EditPassword { get => _editPassword; set => Set(ref _editPassword, value); }

    private bool _editUseIntegratedSecurity;
    public bool EditUseIntegratedSecurity
    {
        get => _editUseIntegratedSecurity;
        set => Set(ref _editUseIntegratedSecurity, value);
    }

    private int _editTimeout = 30;
    public int EditTimeout { get => _editTimeout; set => Set(ref _editTimeout, value); }

    // ── Test connection state ─────────────────────────────────────────────────

    private bool _isTesting;
    public bool IsTesting
    {
        get => _isTesting;
        set
        {
            Set(ref _isTesting, value);
            TestConnectionCommand.NotifyCanExecuteChanged();
        }
    }

    private string _testStatus = "";
    public string TestStatus { get => _testStatus; set => Set(ref _testStatus, value); }

    private string _testStatusColor = "#4A5568";
    public string TestStatusColor { get => _testStatusColor; set => Set(ref _testStatusColor, value); }

    // ── Post-connect canvas confirmation ────────────────────────────────────

    private bool _isClearCanvasPromptVisible;
    public bool IsClearCanvasPromptVisible
    {
        get => _isClearCanvasPromptVisible;
        private set => Set(ref _isClearCanvasPromptVisible, value);
    }

    private DbMetadata? _pendingLoadedMetadata;
    private ConnectionConfig? _pendingLoadedConfig;

    // ── Background health monitor ─────────────────────────────────────────────

    private CancellationTokenSource? _healthMonitorCts;
    private CancellationTokenSource? _connectCts;

    // ── Provider list for ComboBox ────────────────────────────────────────────

    public static IReadOnlyList<DatabaseProvider> AvailableProviders { get; } =
        Enum.GetValues<DatabaseProvider>();

    // ── Commands ──────────────────────────────────────────────────────────────

    public RelayCommand NewProfileCommand { get; }
    public RelayCommand SaveProfileCommand { get; }
    public RelayCommand DeleteProfileCommand { get; }
    public RelayCommand TestConnectionCommand { get; }
    public RelayCommand ConnectCommand { get; }
    public RelayCommand DisconnectCommand { get; }
    public RelayCommand CloseCommand { get; }
    public RelayCommand OpenNewProfileCommand { get; }
    public RelayCommand RefreshHealthCommand { get; }
    public RelayCommand ClearCanvasAfterConnectCommand { get; }
    public RelayCommand KeepCanvasAfterConnectCommand { get; }
    public RelayCommand CloseClearCanvasPromptCommand { get; }

    public event Action<ConnectionProfile>? ConnectionActivated;

    // ── Constructor ───────────────────────────────────────────────────────────

    public ConnectionManagerViewModel(
        IConnectionErrorMessageMapper? errorMessageMapper = null,
        IConnectionStatusPresenter? statusPresenter = null,
        IConnectionCanvasPromptCoordinator? canvasPromptCoordinator = null,
        IConnectionHealthMonitorService? healthMonitorService = null,
        IConnectionSessionOrchestrator? sessionOrchestrator = null,
        IConnectionProfileStore? profileStore = null,
        IConnectionTestExecutor? connectionTestExecutor = null,
        IConnectionProfileFormMapper? formMapper = null,
        IConnectionActivationWorkflow? activationWorkflow = null,
        IConnectionProfileLifecycleService? profileLifecycleService = null,
        IFireAndForgetSafetyExecutor? fireAndForgetSafetyExecutor = null,
        IConnectionHealthLifecycleCoordinator? healthLifecycleCoordinator = null)
    {
        _logger = NullLogger<ConnectionManagerViewModel>.Instance;
        _dbConnectionService = new DatabaseConnectionService();
        _credentialVault = new CredentialVaultStore();
        _errorMessageMapper = errorMessageMapper ?? new ConnectionErrorMessageMapper(_loc);
        _statusPresenter = statusPresenter ?? new ConnectionStatusPresenter(_loc);
        _canvasPromptCoordinator = canvasPromptCoordinator ?? new ConnectionCanvasPromptCoordinator();
        _healthMonitorService = healthMonitorService ?? new ConnectionHealthMonitorService();
        _sessionOrchestrator = sessionOrchestrator ?? new ConnectionSessionOrchestrator();
        _profileStore = profileStore ?? new ConnectionProfileStore();
        _connectionTestExecutor = connectionTestExecutor ?? new DbOrchestratorConnectionTestExecutor();
        _formMapper = formMapper ?? new ConnectionProfileFormMapper(_loc);
        _activationWorkflow = activationWorkflow ?? new ConnectionActivationWorkflow();
        _profileLifecycleService = profileLifecycleService ?? new ConnectionProfileLifecycleService();
        _fireAndForgetSafetyExecutor = fireAndForgetSafetyExecutor ?? new FireAndForgetSafetyExecutor(_logger);
        _healthLifecycleCoordinator = healthLifecycleCoordinator ?? new ConnectionHealthLifecycleCoordinator(_healthMonitorService);

        _loc.PropertyChanged += (_, _) =>
        {
            RaisePropertyChanged(nameof(ActiveConnectionLabel));
            RaisePropertyChanged(nameof(ConnectionHealthLabel));
            RaisePropertyChanged(nameof(ConnectionHealthTooltip));
        };

        NewProfileCommand     = new RelayCommand(BeginNewProfile);
        SaveProfileCommand    = new RelayCommand(SaveProfile);
        DeleteProfileCommand  = new RelayCommand(DeleteProfile, () => SelectedProfile is not null);
        TestConnectionCommand = new RelayCommand(StartTestConnectionSafe, () => !IsTesting && IsEditing);
        ConnectCommand        = new RelayCommand(Connect, () => SelectedProfile is not null);
        DisconnectCommand     = new RelayCommand(Disconnect, () => _activeProfileId is not null);
        CloseCommand          = new RelayCommand(() => IsVisible = false);
        OpenNewProfileCommand = new RelayCommand(() =>
        {
            Open();
            BeginNewProfile();
        });
        RefreshHealthCommand  = new RelayCommand(StartRefreshHealthSafe, () => _activeProfileId is not null);
        ClearCanvasAfterConnectCommand = new RelayCommand(ClearCanvasAfterConnect);
        KeepCanvasAfterConnectCommand = new RelayCommand(KeepCanvasAfterConnect);
        CloseClearCanvasPromptCommand = new RelayCommand(() => CloseClearCanvasPrompt(dismissedByUser: true));

        LoadProfiles();
    }

    public void Open() => IsVisible = true;

    // ── Form operations ───────────────────────────────────────────────────────

    private void BeginNewProfile()
    {
        _selectedProfile = null;
        RaisePropertyChanged(nameof(SelectedProfile));

        ApplyFormData(_formMapper.CreateNew());
        IsEditing = true;
        TestStatus = "";
        TestConnectionCommand.NotifyCanExecuteChanged();
    }

    private void LoadProfileIntoForm(ConnectionProfile p)
    {
        ApplyFormData(_formMapper.FromProfile(p));
        IsEditing = true;
        TestStatus = "";
        TestConnectionCommand.NotifyCanExecuteChanged();
    }

    private void SaveProfile()
    {
        ConnectionProfile profile = _formMapper.ToProfile(CaptureFormData());

        ConnectionProfileSaveResult save = _profileLifecycleService.Save(Profiles, profile, _activeProfileId);
        _selectedProfile = save.SelectedProfile;
        RaisePropertyChanged(nameof(SelectedProfile));

        if (save.ActiveProfileAffected)
        {
            RaisePropertyChanged(nameof(ActiveConnectionLabel));
            RaisePropertyChanged(nameof(ConnectionHealthTooltip));
        }

        PersistProfiles();
    }

    private void DeleteProfile()
    {
        ConnectionProfileDeleteResult deleted = _profileLifecycleService.Delete(Profiles, SelectedProfile, ActiveProfileId);
        if (!deleted.Deleted)
            return;

        if (!string.IsNullOrWhiteSpace(deleted.RemovedProfileId))
            _credentialVault.RemoveSecret(deleted.RemovedProfileId);

        ActiveProfileId = deleted.NextActiveProfileId;
        SelectedProfile = null;
        IsEditing = deleted.IsEditing;
        TestStatus = deleted.TestStatus;
        PersistProfiles();
    }

    private void Connect()
    {
        ConnectionConnectState state = _sessionOrchestrator.BeginConnect(SelectedProfile, _connectCts);
        if (!state.Started || state.Profile is null)
            return;

        _connectCts = state.ConnectCts;
        IsConnecting = state.IsConnecting;
        RaisePropertyChanged(nameof(IsNotConnecting));
        ApplyTestStatus(_statusPresenter.Connecting());
        ActiveProfileId = state.ActiveProfileId;
        // Run an immediate health check for the newly activated connection
        StartRefreshHealthSafe();
        // Load database tables into the search menu in the background
        StartLoadDatabaseTablesSafe(state.Profile, _connectCts!.Token);
    }

    private void Disconnect()
    {
        ConnectionDisconnectState state = _sessionOrchestrator.BeginDisconnect(_connectCts);
        _connectCts = state.ConnectCts;
        _dbConnectionService.Cancel();
        IsConnecting = state.IsConnecting;
        RaisePropertyChanged(nameof(IsNotConnecting));
        ActiveProfileId = state.ActiveProfileId;
        DisconnectCommand.NotifyCanExecuteChanged();
        ConnectCommand.NotifyCanExecuteChanged();
        RefreshHealthCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// Asynchronously loads database tables into the search menu.
    /// This executes the normal workflow after a successful database connection:
    /// 1. Fetches database schema and metadata
    /// 2. Converts to search menu format
    /// 3. Populates the search menu with available tables
    /// 4. Resets the canvas to show the new database
    /// </summary>
    private Task LoadDatabaseTablesAsync(ConnectionProfile profile)
        => LoadDatabaseTablesAsync(profile, CancellationToken.None);

    private async Task LoadDatabaseTablesAsync(ConnectionProfile profile, CancellationToken ct)
    {
        try
        {
            ConnectionActivationResult result = await _activationWorkflow.ExecuteAsync(
                profile,
                SearchMenu,
                Canvas,
                async (config, searchMenu, token) =>
                {
                    await _dbConnectionService.ConnectAndLoadAsync(config, searchMenu, token);
                    return _dbConnectionService.LoadedMetadata;
                },
                ct);

            switch (result.Outcome)
            {
                case EConnectionActivationOutcome.Connected:
                    if (result.ShouldOpenClearCanvasPrompt && result.Metadata is not null && result.Config is not null)
                        OpenClearCanvasPrompt(result.Metadata, result.Config);
                    else
                        CloseClearCanvasPrompt(dismissedByUser: false);

                    ApplyTestStatus(_statusPresenter.Connected());
                    IsVisible = false;
                    ConnectionActivated?.Invoke(profile);
                    break;

                case EConnectionActivationOutcome.SearchMenuUnavailable:
                    string reason = $"{result.FailureReason ?? L("connection.error.searchMenuNotInitialized", "search menu not initialized")}.";
                    ApplyTestStatus(_statusPresenter.FailedWithPrefix(reason));
                    break;

                case EConnectionActivationOutcome.MetadataUnavailable:
                    ApplyTestStatus(_statusPresenter.MetadataUnavailable());
                    break;

                case EConnectionActivationOutcome.Cancelled:
                    ApplyTestStatus(_statusPresenter.Cancelled());
                    break;

                case EConnectionActivationOutcome.Failed:
                    Exception ex = result.FailureException ?? new InvalidOperationException("Unknown connection activation failure.");
                    ApplyTestStatus(_statusPresenter.FailedWithPrefix(_errorMessageMapper.Map(ex, profile.Provider)));
                    _logger?.LogError(ex, "Failed to load database tables for connection {Profile}", profile.Name);
                    break;
            }
        }
        finally
        {
            IsConnecting = false;
            RaisePropertyChanged(nameof(IsNotConnecting));
        }
    }

    private async Task TestConnectionAsync()
    {
        IsTesting = true;
        ApplyTestStatus(_statusPresenter.Testing());

        try
        {
            var config = new ConnectionProfile
            {
                Provider              = EditProvider,
                Host                  = EditHost,
                Port                  = EditPort,
                Database              = EditDatabase,
                Username              = EditUsername,
                Password              = EditPassword,
                UseIntegratedSecurity = EditUseIntegratedSecurity,
                TimeoutSeconds        = EditTimeout,
            }.ToConnectionConfig();

            ConnectionTestResult result = await _connectionTestExecutor.ExecuteAsync(config, EditProvider, EditTimeout);

            if (result.Success)
            {
                ApplyTestStatus(_statusPresenter.TestSuccess(result.Latency, DegradedLatencyThresholdMs));
            }
            else
            {
                ApplyTestStatus(_statusPresenter.Failed(result.ErrorMessage ?? _loc["connection.status.failedPrefix"]));
            }
        }
        catch (Exception ex)
        {
            ApplyTestStatus(_statusPresenter.Failed(_errorMessageMapper.Map(ex, EditProvider)));
        }
        finally
        {
            IsTesting = false;
        }
    }

    // ── Background health monitor ─────────────────────────────────────────────

    private void RestartHealthMonitor()
    {
        _healthMonitorCts = _healthLifecycleCoordinator.Restart(
            _activeProfileId,
            _healthMonitorCts,
            StartHealthMonitorLoopSafe);
    }

    private void StartTestConnectionSafe() =>
        _ = _fireAndForgetSafetyExecutor.ExecuteSafeAsync(TestConnectionAsync, "test connection");

    private void StartRefreshHealthSafe() =>
        _ = _fireAndForgetSafetyExecutor.ExecuteSafeAsync(() => RunHealthCheckAsync(), "refresh health");

    private void StartLoadDatabaseTablesSafe(ConnectionProfile profile, CancellationToken ct) =>
        _ = _fireAndForgetSafetyExecutor.ExecuteSafeAsync(() => LoadDatabaseTablesAsync(profile, ct), "load database tables");

    private void StartHealthMonitorLoopSafe(CancellationToken token) =>
        _ = _fireAndForgetSafetyExecutor.ExecuteSafeAsync(() => HealthMonitorLoopAsync(token), "health monitor loop");

    private async Task HealthMonitorLoopAsync(CancellationToken ct)
    {
        await _healthMonitorService.HealthMonitorLoopAsync(ct, RunHealthCheckAsync);
    }

    private async Task RunHealthCheckAsync(CancellationToken ct = default)
    {
        ActiveHealthStatus = await _healthLifecycleCoordinator.EvaluateActiveStatusAsync(
            Profiles,
            _activeProfileId,
            _connectionTestExecutor.ExecuteAsync,
            DegradedLatencyThresholdMs,
            ct);
    }

    private string L(string key, string fallback)
    {
        string value = _loc[key];
        return string.Equals(value, key, StringComparison.Ordinal) ? fallback : value;
    }

    private void ApplyTestStatus(ConnectionStatusViewState state)
    {
        TestStatus = state.Message;
        TestStatusColor = state.Color;
    }

    private ConnectionProfileFormData CaptureFormData() =>
        new(
            Id: _editId,
            Name: EditName,
            Provider: EditProvider,
            Host: EditHost,
            Port: EditPort,
            Database: EditDatabase,
            Username: EditUsername,
            Password: EditPassword,
            UseIntegratedSecurity: EditUseIntegratedSecurity,
            TimeoutSeconds: EditTimeout);

    private void ApplyFormData(ConnectionProfileFormData formData)
    {
        _editId = formData.Id;
        EditName = formData.Name;
        EditProvider = formData.Provider;
        EditHost = formData.Host;
        EditPort = formData.Port;
        EditDatabase = formData.Database;
        EditUsername = formData.Username;
        EditPassword = formData.Password;
        EditUseIntegratedSecurity = formData.UseIntegratedSecurity;
        EditTimeout = formData.TimeoutSeconds;
    }

    private void OpenClearCanvasPrompt(DbMetadata metadata, ConnectionConfig config)
    {
        ConnectionCanvasPromptState state = _canvasPromptCoordinator.Open(metadata, config);
        _pendingLoadedMetadata = state.PendingMetadata;
        _pendingLoadedConfig = state.PendingConfig;
        IsClearCanvasPromptVisible = state.IsVisible;
    }

    private void CloseClearCanvasPrompt(bool dismissedByUser)
    {
        bool shouldAddWarning = _canvasPromptCoordinator.ShouldAddDismissWarning(
            dismissedByUser,
            IsClearCanvasPromptVisible,
            _pendingLoadedMetadata);
        if (shouldAddWarning && Canvas is not null)
        {
            Canvas.Diagnostics.AddWarning(
                area: L("diagnostics.area.connection", "Connection"),
                message: L("connection.warning.canvasMayContainOldTables", "The canvas may still contain tables from a previous connection."),
                recommendation: L("connection.warning.canvasMayContainOldTablesRecommendation", "Clear the canvas manually or reconnect and choose keep/clear again."),
                openPanel: false
            );
        }

        ConnectionCanvasPromptState state = _canvasPromptCoordinator.Close();
        IsClearCanvasPromptVisible = state.IsVisible;
        _pendingLoadedMetadata = state.PendingMetadata;
        _pendingLoadedConfig = state.PendingConfig;
    }

    private void ClearCanvasAfterConnect()
    {
        if (Canvas is not null && _pendingLoadedMetadata is not null)
            Canvas.SetDatabaseAndResetCanvas(_pendingLoadedMetadata, _pendingLoadedConfig);

        CloseClearCanvasPrompt(dismissedByUser: false);
    }

    private void KeepCanvasAfterConnect()
    {
        CloseClearCanvasPrompt(dismissedByUser: false);
    }

    // ── Persistence ───────────────────────────────────────────────────────────

    private void LoadProfiles()
    {
        Profiles.Clear();
        IReadOnlyList<ConnectionProfile> loaded = _profileStore.LoadProfiles(_credentialVault);
        foreach (ConnectionProfile profile in loaded)
        {
            Profiles.Add(profile);
        }
    }

    private void PersistProfiles()
    {
        _profileStore.PersistProfiles(Profiles, _credentialVault);
    }
}
