using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Microsoft.Extensions.DependencyInjection;
using Material.Icons;
using System.ComponentModel;
using VisualSqlArchitect.Core;
using VisualSqlArchitect.Metadata;
using VisualSqlArchitect.UI.Serialization;
using VisualSqlArchitect.UI.Controls;
using VisualSqlArchitect.UI.Controls.Ddl;
using VisualSqlArchitect.UI.Controls.Shell;
using VisualSqlArchitect.UI.Services.Ddl;
using VisualSqlArchitect.UI.Services;
using VisualSqlArchitect.UI.Services.Connection;
using VisualSqlArchitect.UI.Services.Localization;
using VisualSqlArchitect.UI.Services.Settings;
using VisualSqlArchitect.UI.Services.Theming;
using VisualSqlArchitect.UI.ViewModels;
using VisualSqlArchitect.UI.ViewModels.Validation.Conventions;
using VisualSqlArchitect.UI.ViewModels.Validation.Conventions.Implementations;

namespace VisualSqlArchitect.UI;

public partial class MainWindow : Window
{
    private readonly IServiceProvider _services;

    private ShellViewModel CurrentShell => DataContext as ShellViewModel
        ?? throw new InvalidOperationException(
            L("error.mainWindow.invalidDataContext", "MainWindow DataContext must be a ShellViewModel.")
        );

    private CanvasViewModel CurrentVm => CurrentShell.Canvas
        ?? throw new InvalidOperationException(
            L("error.mainWindow.canvasNotInitialized", "CanvasViewModel was not initialized.")
        );

    private bool _canvasInitialized;
    private ContextMenu? _titleMenu;
    private bool _sidebarActionsWired;
    private bool _connectionActivationWired;
    private ConnectionWorkspaceModule? _connectionModule;
    private SettingsWorkspaceModule? _settingsModule;
    private GridLength _lastLeftSidebarWidth = new(280);
    private GridLength _lastRightSidebarWidth = new(280);
    private static readonly GridLength CollapsedRailWidth = new(24);

    private QueryTabManagerViewModel QueryTabs => CurrentShell.QueryTabs;

    // Services
    private MainWindowLayoutService? _layoutService;
    private SessionManagementService? _sessionService;
    private KeyboardInputHandler? _keyboardHandler;
    private FileOperationsService? _fileOps;
    private ExportService? _export;
    private PreviewService? _preview;
    private CommandPaletteFactory? _commandFactory;
    private ICommandPaletteService? _commandPaletteService;
    private PropertyChangedEventHandler? _windowTitleChangedHandler;
    private PropertyChangedEventHandler? _shellPropertyChangedHandler;
    private readonly ThemeJsonSettingsService _themeJsonSettings;

    public MainWindow()
        : this(
            new ServiceCollection()
                .AddVisualSqlArchitect()
                .AddSingleton<IAliasConvention, SnakeCaseConvention>()
                .AddSingleton<IAliasConvention, CamelCaseConvention>()
                .AddSingleton<IAliasConvention, PascalCaseConvention>()
                .AddSingleton<IAliasConvention, ScreamingSnakeCaseConvention>()
                .AddSingleton<IAliasConventionRegistry, AliasConventionRegistry>()
                .BuildServiceProvider(),
            new ShellViewModel(),
            new ThemeJsonSettingsService())
    {
    }

    public MainWindow(
        IServiceProvider services,
        ShellViewModel shell,
        ThemeJsonSettingsService themeJsonSettings)
    {
        _services = services;
        _themeJsonSettings = themeJsonSettings;

        InitializeComponent();
        DataContext = shell;

        WireHeaderMenus();
        WireMenuButtons();
        WireStartMenu();
        WireModeToggle();
        Title = AppConstants.AppDisplayName;
    }

    private void WireModeToggle()
    {
        _shellPropertyChangedHandler = (_, e) =>
        {
            if (e.PropertyName is nameof(ShellViewModel.ActiveMode) or nameof(ShellViewModel.IsQueryModeActive) or nameof(ShellViewModel.IsDdlModeActive) or nameof(ShellViewModel.ActiveCanvasContext))
            {
                SyncModeToggleState();
                SyncCanvasContext();
            }
        };

        CurrentShell.PropertyChanged += _shellPropertyChangedHandler;
        SyncModeToggleState();
    }

    private void SyncModeToggleState()
    {
        Button? queryModeBtn = this.FindControl<Button>("QueryModeBtn");
        Button? ddlModeBtn = this.FindControl<Button>("DdlModeBtn");

        if (queryModeBtn is not null)
        {
            queryModeBtn.IsEnabled = true;
            queryModeBtn.Classes.Set("active", CurrentShell.IsQueryModeActive);
        }

        if (ddlModeBtn is not null)
        {
            ddlModeBtn.IsEnabled = true;
            ddlModeBtn.Classes.Set("active", CurrentShell.IsDdlModeActive);
        }
    }

    private void QueryModeBtn_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        CurrentShell.SetActiveMode(ShellViewModel.AppMode.Query);
        SyncModeToggleState();
        e.Handled = true;
    }

    private void DdlModeBtn_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        CurrentShell.SetActiveMode(ShellViewModel.AppMode.Ddl);
        SyncModeToggleState();
        e.Handled = true;
    }

    private void WireHeaderMenus()
    {
        AppHeaderBar? canvasHeader = this.FindControl<AppHeaderBar>("CanvasHeader");
        if (canvasHeader is not null)
            canvasHeader.TitleMenuRequested += (_, _) => OpenTitleMenu(canvasHeader);
    }

    private void OpenTitleMenu(Control anchor)
    {
        _titleMenu ??= BuildTitleMenu();
        _titleMenu.Open(anchor);
    }

    private ContextMenu BuildTitleMenu()
    {
        MenuItem NewItem(string header, MaterialIconKind icon, Action onClick)
        {
            var item = new MenuItem
            {
                Header = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 8,
                    Children =
                    {
                        new Material.Icons.Avalonia.MaterialIcon
                        {
                            Kind = icon,
                            Width = 14,
                            Height = 14,
                        },
                        new TextBlock
                        {
                            Text = header,
                            VerticalAlignment = VerticalAlignment.Center,
                        },
                    },
                },
            };
            item.Classes.Add("app-title-menu-item");
            item.Click += (_, _) => onClick();
            return item;
        }

        Separator NewSeparator()
        {
            var separator = new Separator();
            separator.Classes.Add("app-title-menu-sep");
            return separator;
        }

        return new ContextMenu
        {
            Classes = { "app-title-menu" },
            ItemsSource = new object[]
            {
                NewItem(L("menu.newDiagram", "Novo diagrama"), MaterialIconKind.FileOutline, () =>
                {
                    EnterCanvasMode();
                    ResetCurrentCanvas();
                }),
                NewItem(L("menu.openFile", "Abrir arquivo"), MaterialIconKind.FolderOpenOutline, () =>
                {
                    EnterCanvasMode();
                    _ = _fileOps?.OpenAsync();
                }),
                NewItem(L("menu.save", "Salvar"), MaterialIconKind.ContentSave, () =>
                {
                    EnterCanvasMode();
                    _ = _fileOps?.SaveAsync();
                }),
                NewItem(L("menu.fileHistory", "Histórico de arquivos"), MaterialIconKind.History, () =>
                {
                    EnterCanvasMode();
                    CurrentVm.FileHistory.Open();
                }),
                NewSeparator(),
                NewItem(L("menu.shortcuts", "Atalhos de teclado"), MaterialIconKind.Keyboard, () => new KeyboardShortcutsWindow().Show(this)),
                NewItem(L("menu.settings", "Configurações"), MaterialIconKind.CogOutline, () =>
                {
                    PopulateSettingsThemeJsonEditor();
                    OpenSettings(keepStartVisible: false);
                }),
                NewItem(L("menu.importDdlSchema", "Importar Schema DDL"), MaterialIconKind.DatabaseImportOutline, () =>
                {
                    _ = ImportDdlSchemaSafeAsync();
                }),
                NewItem(L("menu.viewDdlSql", "Ver SQL DDL"), MaterialIconKind.CodeBraces, () =>
                {
                    _ = ViewDdlSqlSafeAsync();
                }),
                NewItem(L("menu.executeDdl", "Executar DDL"), MaterialIconKind.PlayCircleOutline, () =>
                {
                    _ = ExecuteDdlSafeAsync();
                }),
                NewSeparator(),
                NewItem(L("menu.backToStart", "Voltar para início"), MaterialIconKind.Home, () =>
                {
                    if (!_canvasInitialized)
                        return;

                    CurrentShell.StartMenu.RefreshData(
                        CurrentVm.ConnectionManager.Profiles,
                        CurrentVm.ConnectionManager.ActiveProfileId
                    );
                    CurrentShell.ReturnToStart();
                    Title = AppConstants.AppDisplayName;
                }),
            },
        };
    }

    private async Task ExecuteDdlSafeAsync()
    {
        try
        {
            await ExecuteDdlAsync();
        }
        catch (Exception ex)
        {
            CurrentShell.Toasts.ShowError(L("toast.ddlExecuteFailed", "Falha ao executar DDL."), ex.Message);
        }
    }

    private async Task ViewDdlSqlSafeAsync()
    {
        try
        {
            await ViewDdlSqlAsync();
        }
        catch (Exception ex)
        {
            CurrentShell.Toasts.ShowError(L("toast.ddlOpenFailed", "Falha ao abrir SQL DDL."), ex.Message);
        }
    }

    private async Task ImportDdlSchemaSafeAsync()
    {
        try
        {
            await ImportDdlSchemaAsync();
        }
        catch (Exception ex)
        {
            CurrentShell.Toasts.ShowError(L("toast.ddlImportFailed", "Falha ao importar schema para DDL."), ex.Message);
        }
    }

    private Task ImportDdlSchemaAsync()
    {
        DbMetadata? metadata = CurrentVm.DatabaseMetadata;
        if (metadata is null)
        {
            CurrentShell.Toasts.ShowWarning(L("toast.ddlConnectToImportSchema", "Conecte-se a um banco para importar schema no canvas DDL."));
            return Task.CompletedTask;
        }

        CanvasViewModel ddlCanvas = CurrentShell.EnsureDdlCanvas();
        var importer = new DdlSchemaImporter();
        DdlImportResult result = importer.Import(metadata, ddlCanvas);

        CurrentShell.SetActiveMode(ShellViewModel.AppMode.Ddl);
        SyncModeToggleState();

        if (result.TableCount == 0)
        {
            CurrentShell.Toasts.ShowWarning(L("toast.ddlNoTablesFound", "Nenhuma tabela encontrada para importar no modo DDL."));
            return Task.CompletedTask;
        }

        CurrentShell.Toasts.ShowSuccess(
            L("toast.ddlSchemaImported", "Schema importado para o canvas DDL."),
            BuildDdlImportSummary(result)
        );

        return Task.CompletedTask;
    }

    private static string BuildDdlImportSummary(DdlImportResult result)
    {
        string summary = LF(
            "toast.ddlImportSummary",
            "{0} tabela(s), {1} coluna(s), {2} FK(s), {3} indice(s) unicos.",
            result.TableCount,
            result.ColumnCount,
            result.ForeignKeyCount,
            result.IndexCount);

        if (result.Warnings is null || result.Warnings.Count == 0)
            return summary;

        return summary + "\n" + string.Join("\n", result.Warnings);
    }

    private void ImportSingleTableToDdl(TableMetadata table, Point position)
    {
        DbMetadata? metadata = CurrentVm.DatabaseMetadata;
        if (metadata is null)
        {
            CurrentShell.Toasts.ShowWarning(L("toast.ddlConnectToImportTable", "Conecte-se a um banco para importar tabelas no canvas DDL."));
            return;
        }

        CanvasViewModel ddlCanvas = CurrentShell.EnsureDdlCanvas();
        var importer = new DdlSchemaImporter();
        DdlPartialImportResult result = importer.ImportTable(metadata, table.FullName, ddlCanvas, position);

        CurrentShell.SetActiveMode(ShellViewModel.AppMode.Ddl);
        SyncModeToggleState();

        if (!result.TableAdded)
        {
            CurrentShell.Toasts.ShowWarning(LF("toast.ddlTableAlreadyExists", "A tabela '{0}' ja existe no canvas DDL.", table.FullName));
            return;
        }

        CurrentShell.Toasts.ShowSuccess(
            L("toast.ddlTableImported", "Tabela importada para o canvas DDL."),
            LF("toast.ddlTableImportSummary", "Nos: +{0}, conexoes: +{1}, FKs: +{2}.", result.AddedNodeCount, result.AddedConnectionCount, result.AddedForeignKeys)
        );
    }

    private async Task ExecuteDdlAsync()
    {
        ConnectionConfig? config = CurrentVm.ActiveConnectionConfig;
        if (config is null)
        {
            CurrentShell.Toasts.ShowWarning(L("toast.ddlNoActiveConnection", "Nenhuma conexão ativa para executar DDL."));
            return;
        }

        if (!TryBuildDdlSql(out string sql, out CanvasViewModel ddlCanvas))
            return;

        var dialogVm = new DdlExecuteDialogViewModel(sql);
        var dialog = new DdlExecuteDialogWindow(
            dialogVm,
            async (stopOnError, ct) =>
            {
                IDbOrchestratorFactory orchestratorFactory =
                    _services.GetRequiredService<IDbOrchestratorFactory>();
                await using IDbOrchestrator orchestrator = orchestratorFactory.Create(config);
                return await orchestrator.ExecuteDdlAsync(sql, stopOnError, ct);
            }
        );

        await dialog.ShowDialog(this);

        if (!dialogVm.HasResult)
            return;

        if (dialogVm.IsSuccess)
            CurrentShell.Toasts.ShowSuccess(L("toast.ddlExecutedSuccess", "DDL executado com sucesso."), dialogVm.ResultSummary);
        else
            CurrentShell.Toasts.ShowWarning(L("toast.ddlExecutedWithIssues", "DDL executado com falhas."), dialogVm.ResultDetails);
    }

    private async Task ViewDdlSqlAsync()
    {
        CanvasViewModel ddlCanvas = PrepareDdlPreviewCanvas();
        LiveDdlBarViewModel liveDdl = ddlCanvas.LiveDdl
            ?? throw new InvalidOperationException(
                L("error.mainWindow.ddlPreviewUnavailable", "DDL preview is unavailable for the current canvas.")
            );
        CurrentShell.OutputPreview.OpenForDdl(ddlCanvas, liveDdl, ddlCanvas.Provider.ToString());
        await Task.CompletedTask;
    }

    private CanvasViewModel PrepareDdlPreviewCanvas()
    {
        CanvasViewModel ddlCanvas = CurrentShell.EnsureDdlCanvas();
        DatabaseProvider provider = CurrentVm.ActiveConnectionConfig?.Provider ?? ddlCanvas.Provider;
        ddlCanvas.Provider = provider;
        LiveDdlBarViewModel liveDdl = ddlCanvas.LiveDdl
            ?? throw new InvalidOperationException(
                L("error.mainWindow.ddlPreviewUnavailable", "DDL preview is unavailable for the current canvas.")
            );
        liveDdl.Recompile();
        return ddlCanvas;
    }

    private bool TryBuildDdlSql(out string sql, out CanvasViewModel ddlCanvas)
    {
        sql = string.Empty;
        ddlCanvas = CurrentShell.EnsureDdlCanvas();

        if (!CurrentShell.IsDdlModeActive)
        {
            CurrentShell.Toasts.ShowWarning(L("toast.switchToDdl", "Alterne para o modo DDL para gerar SQL."));
            return false;
        }

        DatabaseProvider provider = CurrentVm.ActiveConnectionConfig?.Provider ?? ddlCanvas.Provider;
        ddlCanvas.Provider = provider;
        LiveDdlBarViewModel liveDdl = ddlCanvas.LiveDdl
            ?? throw new InvalidOperationException(
                L("error.mainWindow.ddlPreviewUnavailable", "DDL preview is unavailable for the current canvas.")
            );
        liveDdl.Recompile();

        if (!liveDdl.IsValid)
        {
            string details = liveDdl.CompileError ?? string.Join("\n", liveDdl.ErrorHints);
            CurrentShell.Toasts.ShowError(L("toast.ddlInvalid", "DDL inválido. Corrija os erros antes de continuar."), details);
            return false;
        }

        sql = liveDdl.RawSql;
        if (string.IsNullOrWhiteSpace(sql))
        {
            CurrentShell.Toasts.ShowWarning(L("toast.ddlNoStatements", "Nenhum statement DDL foi gerado no canvas."));
            return false;
        }

        return true;
    }

    private void EnsureCanvasInitialized()
    {
        if (_canvasInitialized)
            return;

        CanvasViewModel vm = CurrentShell.EnsureCanvas(
            isDdlModeActiveResolver: () => CurrentShell.IsDdlModeActive,
            importDdlTableAction: (table, position) => ImportSingleTableToDdl(table, position));
        vm.SetCanvasContext(CurrentShell.ActiveCanvasContext);
        vm.ConnectionManager.IsVisible = false;

        InitializeServices(vm);
        AttachCanvasHandlers(vm);
        WireConnectionActivation(vm.ConnectionManager);
        WireSidebarActions(vm.Sidebar);
        WireSearchMenu();
        InitializeQueryTabs();

        CurrentShell.StartMenu.RefreshData(vm.ConnectionManager.Profiles);
        Title = vm.WindowTitle;
        _canvasInitialized = true;
    }

    private void SyncCanvasContext()
    {
        if (!_canvasInitialized)
            return;

        if (CurrentShell.Canvas is not null)
            CurrentShell.Canvas.SetCanvasContext(CanvasContext.Query);

        if (CurrentShell.DdlCanvas is not null)
        {
            CanvasContext ddlContext = CurrentShell.ActiveCanvasContext == CanvasContext.ViewSubcanvas
                ? CanvasContext.ViewSubcanvas
                : CanvasContext.Ddl;

            CurrentShell.DdlCanvas.SetCanvasContext(ddlContext);
        }
    }

    private void WireSidebarActions(SidebarViewModel sidebar)
    {
        if (_sidebarActionsWired)
            return;

        sidebar.AddNodeRequested += () =>
        {
            EnterCanvasMode();
            OpenSearch();
        };

        sidebar.AddConnectionRequested += () => OpenConnectionsPanel(beginNewProfile: true, keepStartVisible: false);

        sidebar.TogglePreviewRequested += () =>
        {
            EnterCanvasMode();
            _ = OpenOutputPreviewForActiveModeSafeAsync();
        };

        _sidebarActionsWired = true;
    }

    private void WireStartMenu()
    {
        CurrentShell.StartMenu.CreateNewDiagramRequested += OnStartCreateNewDiagramRequested;
        CurrentShell.StartMenu.OpenConnectionsRequested += OnStartOpenConnectionsRequested;
        CurrentShell.StartMenu.OpenFromDiskRequested += OnStartOpenFromDiskRequested;
        CurrentShell.StartMenu.OpenSavedConnectionRequested += OnStartOpenSavedConnectionRequested;
        CurrentShell.StartMenu.OpenRecentProjectRequested += OnStartOpenRecentProjectRequested;
        CurrentShell.StartMenu.OpenTemplateRequested += OnStartOpenTemplateRequested;
        CurrentShell.StartMenu.OpenSettingsRequested += OnStartOpenSettingsRequested;
    }

    private void EnterCanvasMode()
    {
        EnsureCanvasInitialized();

        if (!CurrentShell.IsStartVisible)
            return;

        CurrentShell.EnterCanvas();
    }

    private void OnStartCreateNewDiagramRequested()
    {
        EnterCanvasMode();
        ResetCurrentCanvas();
    }

    private void OnStartOpenConnectionsRequested()
    {
        OpenConnectionsPanel(beginNewProfile: true, keepStartVisible: true);
    }

    private void SettingsBackdrop_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        GetSettingsModule().CloseSettings();
    }

    private void SettingsDialog_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        e.Handled = true;
    }

    private void SettingsCloseBtn_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        GetSettingsModule().CloseSettings();
        e.Handled = true;
    }

    private void OnStartOpenSettingsRequested()
    {
        PopulateSettingsThemeJsonEditor();
        OpenSettings(keepStartVisible: true);
    }

    private void OpenSettings(bool keepStartVisible)
    {
        GetSettingsModule().OpenSettings(keepStartVisible);
        SyncLanguageComboSelection();
    }

    private void SettingsNavBtn_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not string sectionRaw)
        {
            e.Handled = true;
            return;
        }

        if (Enum.TryParse<ShellViewModel.ESettingsSection>(sectionRaw, ignoreCase: true, out var section))
            CurrentShell.SelectSettingsSection(section);

        e.Handled = true;
    }

    private void SettingsThemeDarkBtn_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (Application.Current is not null)
            Application.Current.RequestedThemeVariant = ThemeVariant.Dark;

        AppSettingsStore.SaveThemeVariant("Dark");
        SetSettingsStatus(L("settings.status.darkApplied", "Tema escuro aplicado."), isError: false);

        e.Handled = true;
    }

    private void SettingsThemeLightBtn_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (Application.Current is not null)
            Application.Current.RequestedThemeVariant = ThemeVariant.Light;

        AppSettingsStore.SaveThemeVariant("Light");
        SetSettingsStatus(L("settings.status.lightApplied", "Tema claro aplicado."), isError: false);

        e.Handled = true;
    }

    private void SettingsToggleSnapBtn_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        EnsureCanvasInitialized();
        CurrentVm.ToggleSnapCommand.Execute(null);
        SetSettingsStatus(LF("settings.status.snapUpdated", "Snap atualizado: {0}.", CurrentVm.SnapToGridLabel), isError: false);
        e.Handled = true;
    }

    private void SettingsLanguageCombo_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is not ComboBox combo || combo.SelectedItem is not ComboBoxItem item || item.Tag is not string culture)
            return;

        if (LocalizationService.Instance.SetCulture(culture))
        {
            _titleMenu = null;
            SetSettingsStatus(LF("settings.status.languageSelected", "Idioma selecionado: {0}.", culture), isError: false);
        }

        e.Handled = true;
    }

    private void SyncLanguageComboSelection()
    {
        ComboBox? combo = this.FindControl<ComboBox>("SettingsLanguageCombo");
        if (combo is null)
            return;

        foreach (object? option in combo.Items)
        {
            if (option is ComboBoxItem item && item.Tag is string culture &&
                string.Equals(culture, LocalizationService.Instance.CurrentCulture, StringComparison.OrdinalIgnoreCase))
            {
                combo.SelectedItem = item;
                break;
            }
        }
    }

    private void SettingsApplyThemeJsonBtn_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        TextBox? jsonEditor = this.FindControl<TextBox>("SettingsThemeJsonTextBox");
        if (jsonEditor is null)
        {
            e.Handled = true;
            return;
        }

        string rawJson = jsonEditor.Text ?? string.Empty;
        ThemeJsonOperationResult result = _themeJsonSettings.ApplyAndPersist(rawJson);
        string warningSuffix = result.Warnings.Count > 0
            ? $" (avisos: {string.Join(" | ", result.Warnings)})"
            : string.Empty;
        SetSettingsStatus(result.Message + warningSuffix, isError: !result.Success);
        e.Handled = true;
    }

    private void SettingsRestoreDefaultThemeBtn_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        ThemeJsonOperationResult result = _themeJsonSettings.RestoreDefault();
        TextBox? jsonEditor = this.FindControl<TextBox>("SettingsThemeJsonTextBox");
        if (jsonEditor is not null)
            jsonEditor.Text = _themeJsonSettings.GetEditorJsonOrTemplate();

        SetSettingsStatus(result.Message, isError: !result.Success);
        e.Handled = true;
    }

    private void PopulateSettingsThemeJsonEditor()
    {
        TextBox? jsonEditor = this.FindControl<TextBox>("SettingsThemeJsonTextBox");
        if (jsonEditor is null)
            return;

        jsonEditor.Text = _themeJsonSettings.GetEditorJsonOrTemplate();
        SetSettingsStatus(L("settings.status.themeEditorReady", "Editor de tema pronto. Aplique para salvar e usar o tema."), isError: false);
    }

    private void SetSettingsStatus(string message, bool isError)
    {
        TextBlock? statusText = this.FindControl<TextBlock>("SettingsThemeStatusText");
        if (statusText is null)
            return;

        statusText.Text = message;
        statusText.Foreground = isError
            ? ResourceBrush("StatusErrorBrush", "#F87171")
            : ResourceBrush("StatusOkBrush", "#34D399");
    }

    private void OnStartOpenSavedConnectionRequested(StartSavedConnectionItem item)
    {
        if (GetConnectionModule().ConnectFromStartItem(item.Id))
            return;

        OpenConnectionsPanel(beginNewProfile: false, keepStartVisible: true);
    }

    private void OpenConnectionsPanel(bool beginNewProfile, bool keepStartVisible)
    {
        GetConnectionModule().OpenManager(beginNewProfile, keepStartVisible);
    }

    private ConnectionWorkspaceModule GetConnectionModule()
    {
        _connectionModule ??= new ConnectionWorkspaceModule(
            getConnectionManager: () =>
            {
                EnsureCanvasInitialized();
                return CurrentVm.ConnectionManager;
            },
            activateConnectionSidebar: () => CurrentVm.Sidebar.ActiveTab = ESidebarTab.Connection,
            enterCanvas: EnterCanvasMode
        );

        return _connectionModule;
    }

    private SettingsWorkspaceModule GetSettingsModule()
    {
        _settingsModule ??= new SettingsWorkspaceModule(
            getShell: () => CurrentShell,
            enterCanvas: EnterCanvasMode
        );

        return _settingsModule;
    }

    private void WireConnectionActivation(ConnectionManagerViewModel connectionManager)
    {
        if (_connectionActivationWired)
            return;

        connectionManager.ConnectionActivated += _ =>
        {
            if (CurrentShell.IsStartVisible)
                EnterCanvasMode();
        };

        _connectionActivationWired = true;
    }

    private void OnStartOpenFromDiskRequested()
    {
        EnterCanvasMode();
        _ = _fileOps?.OpenAsync();
    }

    private void OnStartOpenRecentProjectRequested(StartRecentProjectItem recent)
    {
        EnterCanvasMode();
        if (!string.IsNullOrWhiteSpace(recent.FilePath))
        {
            _ = _fileOps?.OpenPathAsync(recent.FilePath);
            return;
        }

        _ = _fileOps?.OpenAsync();
    }

    private void OnStartOpenTemplateRequested(StartTemplateItem item)
    {
        QueryTemplate? template = QueryTemplateLibrary.All.FirstOrDefault(t =>
            string.Equals(t.Name, item.Name, StringComparison.OrdinalIgnoreCase)
        );
        if (template is null)
            return;

        EnterCanvasMode();
        CurrentVm.LoadTemplate(template);
        this.FindControl<InfiniteCanvas>("TheCanvas")?.InvalidateWires();
    }

    private void AttachCanvasHandlers(CanvasViewModel vm)
    {
        _windowTitleChangedHandler = (_, e) =>
        {
            if (e.PropertyName == nameof(CanvasViewModel.WindowTitle))
                Title = vm.WindowTitle;

            if (
                e.PropertyName is nameof(CanvasViewModel.IsDirty)
                    or nameof(CanvasViewModel.CurrentFilePath)
            )
            {
                SyncActiveTabMetadataFromCanvas();
                RefreshQueryTabsUi();
            }

            if (e.PropertyName == nameof(CanvasViewModel.IsInViewEditor))
                CurrentShell.SetViewSubcanvasActive(vm.IsInViewEditor);
        };

        vm.PropertyChanged += _windowTitleChangedHandler;
    }

    private void DetachCanvasHandlers(CanvasViewModel vm)
    {
        if (_windowTitleChangedHandler is not null)
            vm.PropertyChanged -= _windowTitleChangedHandler;

        _windowTitleChangedHandler = null;
    }

    private void InitializeServices(CanvasViewModel vm)
    {
        CanvasViewModel ddlVm = CurrentShell.EnsureDdlCanvas();

        _layoutService = ActivatorUtilities.CreateInstance<MainWindowLayoutService>(_services, this, vm);
        _sessionService = ActivatorUtilities.CreateInstance<SessionManagementService>(_services, this, vm, ddlVm);
        _fileOps = ActivatorUtilities.CreateInstance<FileOperationsService>(_services, this, vm, ddlVm);
        _export = ActivatorUtilities.CreateInstance<ExportService>(_services, this, vm);
        _preview = ActivatorUtilities.CreateInstance<PreviewService>(_services, this, vm);
        _commandFactory = new CommandPaletteFactory(
            this,
            () => CurrentShell.ActiveCanvas ?? CurrentVm,
            () => CurrentShell,
            _fileOps,
            _export,
            _preview,
            CreateNewQueryTab
        );
        _commandPaletteService = new CommandPaletteService(_commandFactory);
        _commandPaletteService.Refresh();
        CurrentShell.SetCommandPalette(_commandPaletteService.ViewModel);
        var canvasProvider = new ActiveCanvasProvider(
            () => CurrentShell.ActiveCanvas ?? vm
        );
        _keyboardHandler = new KeyboardInputHandler(
            this,
            canvasProvider,
            _fileOps,
            CurrentShell.CommandPalette,
            CreateNewQueryTab
        );

        _layoutService.Wire();
        _sessionService.Wire();
        _sessionService.CheckForSession();
        _keyboardHandler.Wire();
        _preview.Wire();
    }

    private static string CreateFreshCanvasSnapshot()
    {
        using var vm = new CanvasViewModel();
        return CanvasSerializer.Serialize(vm);
    }

    private string GetTabTitle(QueryTabState tab)
    {
        if (!string.IsNullOrWhiteSpace(tab.CurrentFilePath))
            return System.IO.Path.GetFileNameWithoutExtension(tab.CurrentFilePath);

        return tab.FallbackTitle;
    }

    private void CaptureActiveTabState()
    {
        if (QueryTabs.IsRestoringTab)
            return;

        QueryTabs.CaptureActive(
            CanvasSerializer.Serialize(CurrentVm),
            CurrentVm.CurrentFilePath,
            CurrentVm.IsDirty,
            CurrentVm.ExplainPlan.ExportHistoryState()
        );
    }

    private void SyncActiveTabMetadataFromCanvas()
    {
        if (QueryTabs.IsRestoringTab)
            return;

        QueryTabs.SyncActiveMetadata(CurrentVm.CurrentFilePath, CurrentVm.IsDirty);
    }

    private void RestoreTabState(int tabIndex)
    {
        QueryTabState? tab = QueryTabs.GetTab(tabIndex);
        if (tab is null)
            return;

        string snapshot = tab.SnapshotJson ?? CreateFreshCanvasSnapshot();
        CanvasLoadResult result = CanvasSerializer.Deserialize(snapshot, CurrentVm);
        if (!result.Success)
        {
            CurrentVm.DataPreview.ShowError(LF("tab.switchFailed", "Falha ao alternar aba: {0}", result.Error ?? string.Empty), null);
            return;
        }

        CurrentVm.CurrentFilePath = tab.CurrentFilePath;
        CurrentVm.IsDirty = tab.IsDirty;
        CurrentVm.ExplainPlan.ImportHistoryState(tab.ExplainHistory);

        this.FindControl<InfiniteCanvas>("TheCanvas")?.InvalidateWires();
        Title = CurrentVm.WindowTitle;
    }

    private void ActivateQueryTab(int tabIndex)
    {
        if (tabIndex < 0 || tabIndex >= QueryTabs.Tabs.Count || tabIndex == QueryTabs.ActiveTabIndex)
            return;

        CaptureActiveTabState();

        if (!QueryTabs.TryActivate(tabIndex))
            return;

        QueryTabs.IsRestoringTab = true;
        try
        {
            RestoreTabState(tabIndex);
            CurrentVm.ConnectionManager.IsVisible = false;
        }
        finally
        {
            QueryTabs.IsRestoringTab = false;
        }

        RefreshQueryTabsUi();
    }

    private void CloseQueryTab(int tabIndex)
    {
        if (QueryTabs.Tabs.Count <= 1)
            return;

        bool closingActive = tabIndex == QueryTabs.ActiveTabIndex;

        if (closingActive)
            CaptureActiveTabState();

        int newActiveIndex = QueryTabs.CloseTab(tabIndex);

        if (closingActive)
        {
            QueryTabs.IsRestoringTab = true;
            try
            {
                RestoreTabState(newActiveIndex);
                CurrentVm.ConnectionManager.IsVisible = false;
            }
            finally
            {
                QueryTabs.IsRestoringTab = false;
            }
        }

        RefreshQueryTabsUi();
    }

    private void RefreshQueryTabsUi()
    {
        IBrush titleActiveBrush = ResourceBrush("TextPrimaryBrush", "#E8EAED");
        IBrush titleInactiveBrush = ResourceBrush("TextSecondaryBrush", "#8B95A8");
        IBrush dotActiveBrush = ResourceBrush("AccentBlueBrush", "#3B82F6");
        IBrush dotInactiveBrush = ResourceBrush("TextMutedBrush", "#4A5568");
        IBrush dirtyBrush = ResourceBrush("BtnWarningFgBrush", "#FBBF24");
        IBrush tabActiveBg = ResourceBrush("Surface1Brush", "#171B26");
        IBrush tabInactiveBg = ResourceBrush("MacroBg1Brush", "#11151F");
        IBrush tabActiveBorder = ResourceBrush("BorderBrush", "#252C3F");
        IBrush tabInactiveBorder = ResourceBrush("MacroBorderSubtleBrush", "#1E2335");

        StackPanel? host = this.FindControl<StackPanel>("QueryTabsHost");
        if (host is null)
            return;

        host.Children.Clear();

        for (int i = 0; i < QueryTabs.Tabs.Count; i++)
        {
            int tabIndex = i;
            QueryTabState tab = QueryTabs.Tabs[i];
            bool isActive = i == QueryTabs.ActiveTabIndex;

            var title = new TextBlock
            {
                Text = GetTabTitle(tab),
                FontSize = 11,
                Foreground = isActive ? titleActiveBrush : titleInactiveBrush,
                VerticalAlignment = VerticalAlignment.Center,
            };

            var dot = new Ellipse
            {
                Width = 5,
                Height = 5,
                Fill = isActive ? dotActiveBrush : dotInactiveBrush,
                VerticalAlignment = VerticalAlignment.Center,
            };

            var row = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 5,
                VerticalAlignment = VerticalAlignment.Center,
            };

            row.Children.Add(dot);
            row.Children.Add(title);

            if (tab.IsDirty)
            {
                row.Children.Add(
                    new TextBlock
                    {
                        Text = "•",
                        FontSize = 12,
                        Foreground = dirtyBrush,
                        VerticalAlignment = VerticalAlignment.Center,
                    }
                );
            }

            var button = new Button
            {
                Classes = { "tb" },
                Padding = new Thickness(10, 5, 4, 5),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Content = row,
            };
            button.Click += (_, _) => ActivateQueryTab(tabIndex);

            var closeBtn = new Button
            {
                Classes = { "tb" },
                Width = 18,
                Height = 18,
                Padding = new Thickness(0),
                Margin = new Thickness(0, 0, 4, 0),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                IsVisible = QueryTabs.Tabs.Count > 1,
                Content = new Material.Icons.Avalonia.MaterialIcon
                {
                    Kind = Material.Icons.MaterialIconKind.Close,
                    Width = 10,
                    Height = 10,
                },
            };
            closeBtn.Click += (_, e) =>
            {
                e.Handled = true;
                CloseQueryTab(tabIndex);
            };

            var wrapper = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Stretch,
            };
            wrapper.Children.Add(button);
            wrapper.Children.Add(closeBtn);

            var container = new Border
            {
                Background = isActive ? tabActiveBg : tabInactiveBg,
                BorderBrush = isActive ? tabActiveBorder : tabInactiveBorder,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Child = wrapper,
            };

            host.Children.Add(container);
        }
    }

    private static IBrush ResourceBrush(string key, string fallbackHex)
    {
        if (Application.Current?.TryFindResource(key, out object? resource) == true && resource is IBrush brush)
            return brush;

        return new SolidColorBrush(Color.Parse(fallbackHex));
    }

    private static string L(string key, string fallback)
    {
        string value = LocalizationService.Instance[key];
        return string.Equals(value, key, StringComparison.Ordinal) ? fallback : value;
    }

    private static string LF(string key, string fallbackFormat, params object[] args)
    {
        return string.Format(L(key, fallbackFormat), args);
    }

    private void InitializeQueryTabs()
    {
        QueryTabs.Initialize(
            CanvasSerializer.Serialize(CurrentVm),
            CurrentVm.CurrentFilePath,
            CurrentVm.IsDirty,
            CurrentVm.ExplainPlan.ExportHistoryState()
        );

        RefreshQueryTabsUi();
    }

    private void ResetCurrentCanvas()
    {
        QueryTabs.ResetActive(CreateFreshCanvasSnapshot());

        QueryTabs.IsRestoringTab = true;
        try
        {
            RestoreTabState(QueryTabs.ActiveTabIndex);
        }
        finally
        {
            QueryTabs.IsRestoringTab = false;
        }

        RefreshQueryTabsUi();
    }

    private void CreateNewQueryTab()
    {
        CaptureActiveTabState();

        QueryTabs.AddNewTab(CreateFreshCanvasSnapshot(), []);

        QueryTabs.IsRestoringTab = true;
        try
        {
            RestoreTabState(QueryTabs.ActiveTabIndex);
        }
        finally
        {
            QueryTabs.IsRestoringTab = false;
        }

        RefreshQueryTabsUi();
    }

    private void ToastDetailsBackdrop_PointerPressed(object? s, PointerPressedEventArgs e)
    {
        CurrentShell.Toasts.CloseDetailsCommand.Execute(null);
        e.Handled = true;
    }

    private void ToastDetailsDialog_PointerPressed(object? s, PointerPressedEventArgs e)
    {
        e.Handled = true;
    }

    private void ClearCanvasPromptBackdrop_PointerPressed(object? s, PointerPressedEventArgs e)
    {
        CurrentVm.ConnectionManager.CloseClearCanvasPromptCommand.Execute(null);
        e.Handled = true;
    }

    private void ClearCanvasPromptDialog_PointerPressed(object? s, PointerPressedEventArgs e)
    {
        e.Handled = true;
    }

    private void WireMenuButtons()
    {
        void B(string name, Action a)
        {
            Button? btn = this.FindControl<Button>(name);
            if (btn is not null)
                btn.Click += (_, _) => a();
        }
        B(
            "NewBtn",
            () =>
            {
                EnterCanvasMode();
                ResetCurrentCanvas();
            }
        );
        B(
            "NewTabBtn",
            () =>
            {
                EnterCanvasMode();
                CreateNewQueryTab();
            }
        );
        B("OpenSearchBtn", () =>
        {
            EnterCanvasMode();
            OpenSearch();
        });
        B("SaveBtn", () =>
        {
            EnterCanvasMode();
            _ = _fileOps?.SaveAsync();
        });
        B("FileHistoryBtn", () =>
        {
            EnterCanvasMode();
            CurrentVm.FileHistory.Open();
        });
        B("OpenBtn", () =>
        {
            EnterCanvasMode();
            _ = _fileOps?.OpenAsync();
        });
        B("HomeBtn", () =>
        {
            if (!_canvasInitialized)
                return;
            CurrentVm.ConnectionManager.IsVisible = false;

            CurrentShell.StartMenu.RefreshData(
                CurrentVm.ConnectionManager.Profiles,
                CurrentVm.ConnectionManager.ActiveProfileId
            );
            CurrentShell.ReturnToStart();
            Title = AppConstants.AppDisplayName;
        });
        B("ShortcutsBtn", () => new KeyboardShortcutsWindow().Show(this));
        B("ZoomInBtn", () =>
        {
            EnterCanvasMode();
            CurrentVm.ZoomInCommand.Execute(null);
        });
        B("ZoomOutBtn", () =>
        {
            EnterCanvasMode();
            CurrentVm.ZoomOutCommand.Execute(null);
        });
        B("FitBtn", () =>
        {
            EnterCanvasMode();
            CurrentVm.FitToScreenCommand.Execute(null);
        });
        B("TogglePreviewBtn", () =>
        {
            EnterCanvasMode();
            _ = OpenOutputPreviewForActiveModeSafeAsync();
        });
    }

    private async Task OpenOutputPreviewForActiveModeSafeAsync()
    {
        try
        {
            await OpenOutputPreviewForActiveModeAsync();
        }
        catch (Exception ex)
        {
            CurrentShell.Toasts.ShowError(L("toast.previewOpenFailed", "Falha ao abrir preview."), ex.Message);
        }
    }

    private async Task OpenOutputPreviewForActiveModeAsync()
    {
        if (CurrentShell.IsDdlModeActive)
        {
            CanvasViewModel ddlCanvas = PrepareDdlPreviewCanvas();
            LiveDdlBarViewModel liveDdl = ddlCanvas.LiveDdl
                ?? throw new InvalidOperationException(
                    L("error.mainWindow.ddlPreviewUnavailable", "DDL preview is unavailable for the current canvas.")
                );
            CurrentShell.OutputPreview.OpenForDdl(ddlCanvas, liveDdl, ddlCanvas.Provider.ToString());
            return;
        }

        CanvasViewModel queryCanvas = CurrentShell.EnsureCanvas(
            isDdlModeActiveResolver: () => CurrentShell.IsDdlModeActive,
            importDdlTableAction: (table, position) => ImportSingleTableToDdl(table, position));

        queryCanvas.DataPreview.IsVisible = true;
        CurrentShell.OutputPreview.OpenForQuery(queryCanvas);
        await Task.CompletedTask;
    }

    private void WireSearchMenu()
    {
        SearchMenuControl? overlay = this.FindControl<SearchMenuControl>("SearchOverlay");
        if (overlay is null)
            return;
        overlay.SpawnRequested += (_, def) =>
        {
            CurrentVm.SpawnNode(def, CurrentVm.SearchMenu.SpawnPosition);
            this.FindControl<InfiniteCanvas>("TheCanvas")?.InvalidateWires();
        };
        overlay.SpawnTableRequested += (_, args) =>
        {
            CurrentVm.SpawnTableNode(
                args.FullName,
                args.Cols.Select(c => (c.Name, c.Type)),
                CurrentVm.SearchMenu.SpawnPosition
            );
            this.FindControl<InfiniteCanvas>("TheCanvas")?.InvalidateWires();
            // Trigger join analysis after the node is added
            CurrentVm.TriggerAutoJoinAnalysis(args.FullName);
        };
        overlay.SnippetRequested += (_, snippet) =>
        {
            CurrentVm.InsertSnippet(snippet, CurrentVm.SearchMenu.SpawnPosition);
            this.FindControl<InfiniteCanvas>("TheCanvas")?.InvalidateWires();
        };
    }

    private void OpenSearch()
    {
        InfiniteCanvas? canvas = this.FindControl<InfiniteCanvas>("TheCanvas");
        Point ctr = canvas is not null
            ? new Point(canvas.Bounds.Width / 2, canvas.Bounds.Height / 2)
            : new Point(400, 300);
        CurrentVm.SearchMenu.Open(ctr);
    }

    private bool TryCloseTopModalOnEscape()
    {
        if (CurrentVm.ConnectionManager.IsClearCanvasPromptVisible)
        {
            CurrentVm.ConnectionManager.CloseClearCanvasPromptCommand.Execute(null);
            return true;
        }

        if (CurrentShell.Toasts.IsDetailsOpen)
        {
            CurrentShell.Toasts.CloseDetailsCommand.Execute(null);
            return true;
        }

        return false;
    }

    private void LeftSidebarHideBtn_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        SetLeftSidebarCollapsed(true);
        e.Handled = true;
    }

    private void LeftSidebarShowBtn_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        SetLeftSidebarCollapsed(false);
        e.Handled = true;
    }

    private void RightSidebarHideBtn_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        SetRightSidebarCollapsed(true);
        e.Handled = true;
    }

    private void RightSidebarShowBtn_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        SetRightSidebarCollapsed(false);
        e.Handled = true;
    }

    private void SetLeftSidebarCollapsed(bool collapsed)
    {
        Grid? bodyGrid = this.FindControl<Grid>("BodyGrid");
        if (bodyGrid is null || bodyGrid.ColumnDefinitions.Count < 5)
            return;

        ColumnDefinition sidebarColumn = bodyGrid.ColumnDefinitions[0];
        ColumnDefinition splitterColumn = bodyGrid.ColumnDefinitions[1];
        Border? sidebarHost = this.FindControl<Border>("LeftSidebarHost");
        Border? rail = this.FindControl<Border>("LeftSidebarRail");
        GridSplitter? splitter = this.FindControl<GridSplitter>("LeftSplitter");
        Button? showBtn = this.FindControl<Button>("LeftSidebarShowBtn");
        Button? hideBtn = this.FindControl<Button>("LeftSidebarHideBtn");

        if (sidebarHost is null || splitter is null)
            return;

        if (collapsed)
        {
            if (sidebarColumn.Width.IsAbsolute && sidebarColumn.Width.Value > 1)
                _lastLeftSidebarWidth = sidebarColumn.Width;

            sidebarColumn.MinWidth = CollapsedRailWidth.Value;
            sidebarColumn.MaxWidth = CollapsedRailWidth.Value;
            sidebarColumn.Width = CollapsedRailWidth;
            splitterColumn.Width = new GridLength(0);
            sidebarHost.IsVisible = false;
            splitter.IsVisible = false;
            if (rail is not null)
                rail.IsVisible = true;
            if (showBtn is not null)
                showBtn.IsVisible = true;
            if (hideBtn is not null)
                hideBtn.IsVisible = false;
            return;
        }

        sidebarColumn.MinWidth = 200;
        sidebarColumn.MaxWidth = 420;
        sidebarColumn.Width = _lastLeftSidebarWidth.Value > 1 ? _lastLeftSidebarWidth : new GridLength(280);
        splitterColumn.Width = new GridLength(4);
        sidebarHost.IsVisible = true;
        splitter.IsVisible = true;
        if (rail is not null)
            rail.IsVisible = false;
        if (showBtn is not null)
            showBtn.IsVisible = false;
        if (hideBtn is not null)
            hideBtn.IsVisible = true;
    }

    private void SetRightSidebarCollapsed(bool collapsed)
    {
        Grid? bodyGrid = this.FindControl<Grid>("BodyGrid");
        if (bodyGrid is null || bodyGrid.ColumnDefinitions.Count < 5)
            return;

        ColumnDefinition splitterColumn = bodyGrid.ColumnDefinitions[3];
        ColumnDefinition sidebarColumn = bodyGrid.ColumnDefinitions[4];
        Border? sidebarHost = this.FindControl<Border>("RightSidebarHost");
        Border? rail = this.FindControl<Border>("RightSidebarRail");
        GridSplitter? splitter = this.FindControl<GridSplitter>("RightSplitter");
        Button? showBtn = this.FindControl<Button>("RightSidebarShowBtn");
        Button? hideBtn = this.FindControl<Button>("RightSidebarHideBtn");

        if (sidebarHost is null || splitter is null)
            return;

        if (collapsed)
        {
            if (sidebarColumn.Width.IsAbsolute && sidebarColumn.Width.Value > 1)
                _lastRightSidebarWidth = sidebarColumn.Width;

            sidebarColumn.MinWidth = CollapsedRailWidth.Value;
            sidebarColumn.MaxWidth = CollapsedRailWidth.Value;
            sidebarColumn.Width = CollapsedRailWidth;
            splitterColumn.Width = new GridLength(0);
            sidebarHost.IsVisible = false;
            splitter.IsVisible = false;
            if (rail is not null)
                rail.IsVisible = true;
            if (showBtn is not null)
                showBtn.IsVisible = true;
            if (hideBtn is not null)
                hideBtn.IsVisible = false;
            return;
        }

        sidebarColumn.MinWidth = 220;
        sidebarColumn.MaxWidth = 500;
        sidebarColumn.Width = _lastRightSidebarWidth.Value > 1 ? _lastRightSidebarWidth : new GridLength(280);
        splitterColumn.Width = new GridLength(4);
        sidebarHost.IsVisible = true;
        splitter.IsVisible = true;
        if (rail is not null)
            rail.IsVisible = false;
        if (showBtn is not null)
            showBtn.IsVisible = false;
        if (hideBtn is not null)
            hideBtn.IsVisible = true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape && TryCloseTopModalOnEscape())
        {
            e.Handled = true;
            return;
        }

        base.OnKeyDown(e);
        _keyboardHandler?.OnKeyDown(this, e);
    }
}
