using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Material.Icons;
using System.ComponentModel;
using VisualSqlArchitect.UI.Serialization;
using VisualSqlArchitect.UI.Controls;
using VisualSqlArchitect.UI.Controls.Shell;
using VisualSqlArchitect.UI.Services;
using VisualSqlArchitect.UI.Services.Connection;
using VisualSqlArchitect.UI.Services.Localization;
using VisualSqlArchitect.UI.ViewModels;

namespace VisualSqlArchitect.UI;

public partial class MainWindow : Window
{
    private sealed class QueryTabState
    {
        public required string FallbackTitle { get; init; }
        public string? SnapshotJson { get; set; }
        public string? CurrentFilePath { get; set; }
        public bool IsDirty { get; set; }
    }

    private ShellViewModel CurrentShell => DataContext as ShellViewModel
        ?? throw new InvalidOperationException("MainWindow DataContext must be a ShellViewModel.");

    private CanvasViewModel CurrentVm => CurrentShell.Canvas
        ?? throw new InvalidOperationException("CanvasViewModel was not initialized.");

    private bool _canvasInitialized;
    private ContextMenu? _titleMenu;
    private bool _sidebarActionsWired;
    private bool _connectionActivationWired;
    private ConnectionWorkspaceModule? _connectionModule;
    private GridLength _lastLeftSidebarWidth = new(320);
    private GridLength _lastRightSidebarWidth = new(320);
    private static readonly GridLength CollapsedRailWidth = new(34);

    private readonly List<QueryTabState> _queryTabs = [];
    private int _activeQueryTabIndex;
    private bool _isRestoringTab;

    // Services
    private MainWindowLayoutService? _layoutService;
    private SessionManagementService? _sessionService;
    private KeyboardInputHandler? _keyboardHandler;
    private FileOperationsService? _fileOps;
    private ExportService? _export;
    private PreviewService? _preview;
    private CommandPaletteFactory? _commandFactory;
    private PropertyChangedEventHandler? _windowTitleChangedHandler;
    private PropertyChangedEventHandler? _databaseMetadataChangedHandler;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = new ShellViewModel();

        WireHeaderMenus();
        WireMenuButtons();
        WireStartMenu();
        Title = "Visual SQL Architect";
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
                NewItem("Novo diagrama", MaterialIconKind.FileOutline, () =>
                {
                    EnterCanvasMode();
                    ResetCurrentCanvas();
                }),
                NewItem("Abrir arquivo", MaterialIconKind.FolderOpenOutline, () =>
                {
                    EnterCanvasMode();
                    _ = _fileOps?.OpenAsync();
                }),
                NewItem("Salvar", MaterialIconKind.ContentSave, () =>
                {
                    EnterCanvasMode();
                    _ = _fileOps?.SaveAsync();
                }),
                NewItem("Historico de arquivos", MaterialIconKind.History, () =>
                {
                    EnterCanvasMode();
                    CurrentVm.FileHistory.Open();
                }),
                NewSeparator(),
                NewItem("Atalhos de teclado", MaterialIconKind.Keyboard, () => new KeyboardShortcutsWindow().Show(this)),
                NewItem("Alternar idioma", MaterialIconKind.Translate, () => LocalizationService.Instance.ToggleCulture()),
                NewSeparator(),
                NewItem("Voltar para inicio", MaterialIconKind.Home, () =>
                {
                    if (!_canvasInitialized)
                        return;

                    CurrentShell.StartMenu.RefreshData(
                        CurrentVm.ConnectionManager.Profiles,
                        CurrentVm.ConnectionManager.ActiveProfileId
                    );
                    CurrentShell.ReturnToStart();
                    Title = "Visual SQL Architect";
                }),
            },
        };
    }

    private void EnsureCanvasInitialized()
    {
        if (_canvasInitialized)
            return;

        CanvasViewModel vm = CurrentShell.EnsureCanvas();
        vm.ConnectionManager.IsVisible = false;

        InitializeServices(vm);
        AttachCanvasHandlers(vm);
        WireConnectionActivation(vm.ConnectionManager);
        WireSidebarActions(vm.Sidebar);
        WireSearchMenu();
        InitializeQueryTabs();

        PreviewPanel.LiveSqlViewModel = vm.LiveSql;
        CurrentShell.StartMenu.RefreshData(vm.ConnectionManager.Profiles);
        Title = vm.WindowTitle;
        _canvasInitialized = true;
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
            CurrentVm.TogglePreviewCommand.Execute(null);
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
            activateConnectionSidebar: () => CurrentVm.Sidebar.ActiveTab = SidebarTab.Connection,
            enterCanvas: EnterCanvasMode
        );

        return _connectionModule;
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
        };

        _databaseMetadataChangedHandler = (_, e) =>
        {
            if (e.PropertyName == nameof(CanvasViewModel.DatabaseMetadata))
                UpdateSchemaTree();
        };

        vm.PropertyChanged += _windowTitleChangedHandler;
        vm.PropertyChanged += _databaseMetadataChangedHandler;
    }

    private void DetachCanvasHandlers(CanvasViewModel vm)
    {
        if (_windowTitleChangedHandler is not null)
            vm.PropertyChanged -= _windowTitleChangedHandler;
        if (_databaseMetadataChangedHandler is not null)
            vm.PropertyChanged -= _databaseMetadataChangedHandler;

        _windowTitleChangedHandler = null;
        _databaseMetadataChangedHandler = null;
    }

    private void InitializeServices(CanvasViewModel vm)
    {
        _layoutService = new MainWindowLayoutService(this, vm);
        _sessionService = new SessionManagementService(this, vm);
        _fileOps = new FileOperationsService(this, vm);
        _keyboardHandler = new KeyboardInputHandler(this, vm, _fileOps, CreateNewQueryTab);
        _export = new ExportService(this, vm);
        _preview = new PreviewService(this, vm);
        _commandFactory = new CommandPaletteFactory(
            this,
            vm,
            _fileOps,
            _export,
            _preview,
            CreateNewQueryTab
        );

        _layoutService.Wire();
        _sessionService.Wire();
        _sessionService.CheckForSession();
        _keyboardHandler.Wire();
        _preview.Wire();
        _commandFactory.RegisterAllCommands();
    }

    private void UpdateSchemaTree()
    {
        IBrush mutedBrush = ResourceBrush("TextMutedBrush", "#4A5568");
        IBrush secondaryBrush = ResourceBrush("TextSecondaryBrush", "#8B95A8");
        IBrush accentBrush = ResourceBrush("AccentBlueBrush", "#60A5FA");
        IBrush warningBrush = ResourceBrush("BtnWarningFgBrush", "#FBBF24");
        IBrush numberBrush = ResourceBrush("PinNumberBrush", "#4ADE80");

        var schemaTree = this.FindControl<TreeView>("SchemaTree");
        if (schemaTree is null || CurrentVm.DatabaseMetadata is null)
            return;

        schemaTree.Items.Clear();

        foreach (var schema in CurrentVm.DatabaseMetadata.Schemas)
        {
            // Create schema node
            var schemaItem = new TreeViewItem
            {
                Header = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 5,
                    Children =
                    {
                        new Material.Icons.Avalonia.MaterialIcon { Kind = Material.Icons.MaterialIconKind.Database, Width = 12, Height = 12, Foreground = mutedBrush },
                        new TextBlock { Text = schema.Name, FontWeight = FontWeight.Medium, Foreground = secondaryBrush, FontSize = 11 }
                    }
                },
                IsExpanded = true
            };

            // Add tables to schema
            foreach (var table in schema.Tables.OrderBy(t => t.Name))
            {
                var tableItem = new TreeViewItem
                {
                    Header = new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Spacing = 4,
                        Children =
                        {
                            new Material.Icons.Avalonia.MaterialIcon { Kind = Material.Icons.MaterialIconKind.Table, Width = 12, Height = 12, Foreground = accentBrush },
                            new TextBlock { Text = table.Name, FontSize = 11 }
                        }
                    }
                };

                // Add columns to table
                foreach (var column in table.Columns.OrderBy(c => c.OrdinalPosition))
                {
                    var columnItem = new TreeViewItem
                    {
                        Header = new StackPanel
                        {
                            Orientation = Orientation.Horizontal,
                            Spacing = 3,
                            Children =
                            {
                                new Material.Icons.Avalonia.MaterialIcon
                                {
                                    Kind = column.IsPrimaryKey ? Material.Icons.MaterialIconKind.Key : Material.Icons.MaterialIconKind.CircleSmall,
                                    Width = 10,
                                    Height = 10,
                                    Foreground = column.IsPrimaryKey ? warningBrush : mutedBrush
                                },
                                new TextBlock { Text = column.Name, FontFamily = new FontFamily("Consolas,monospace"), FontSize = 10 },
                                new TextBlock { Text = column.NativeType, Foreground = numberBrush, FontSize = 9 }
                            }
                        }
                    };

                    tableItem.Items.Add(columnItem);
                }

                schemaItem.Items.Add(tableItem);
            }

            schemaTree.Items.Add(schemaItem);
        }
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
        if (_isRestoringTab || _activeQueryTabIndex < 0 || _activeQueryTabIndex >= _queryTabs.Count)
            return;

        QueryTabState activeTab = _queryTabs[_activeQueryTabIndex];
        activeTab.SnapshotJson = CanvasSerializer.Serialize(CurrentVm);
        activeTab.CurrentFilePath = CurrentVm.CurrentFilePath;
        activeTab.IsDirty = CurrentVm.IsDirty;
    }

    private void SyncActiveTabMetadataFromCanvas()
    {
        if (_isRestoringTab || _activeQueryTabIndex < 0 || _activeQueryTabIndex >= _queryTabs.Count)
            return;

        QueryTabState activeTab = _queryTabs[_activeQueryTabIndex];
        activeTab.CurrentFilePath = CurrentVm.CurrentFilePath;
        activeTab.IsDirty = CurrentVm.IsDirty;
    }

    private void RestoreTabState(int tabIndex)
    {
        if (tabIndex < 0 || tabIndex >= _queryTabs.Count)
            return;

        QueryTabState tab = _queryTabs[tabIndex];
        string snapshot = tab.SnapshotJson ?? CreateFreshCanvasSnapshot();
        CanvasLoadResult result = CanvasSerializer.Deserialize(snapshot, CurrentVm);
        if (!result.Success)
        {
            CurrentVm.DataPreview.ShowError($"Failed to switch tab: {result.Error}", null);
            return;
        }

        CurrentVm.CurrentFilePath = tab.CurrentFilePath;
        CurrentVm.IsDirty = tab.IsDirty;

        this.FindControl<InfiniteCanvas>("TheCanvas")?.InvalidateWires();
        Title = CurrentVm.WindowTitle;
    }

    private void ActivateQueryTab(int tabIndex)
    {
        if (tabIndex < 0 || tabIndex >= _queryTabs.Count || tabIndex == _activeQueryTabIndex)
            return;

        CaptureActiveTabState();
        _activeQueryTabIndex = tabIndex;

        _isRestoringTab = true;
        try
        {
            RestoreTabState(tabIndex);
                    CurrentVm.ConnectionManager.IsVisible = false;
        }
        finally
        {
            _isRestoringTab = false;
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

        for (int i = 0; i < _queryTabs.Count; i++)
        {
            int tabIndex = i;
            QueryTabState tab = _queryTabs[i];
            bool isActive = i == _activeQueryTabIndex;

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
                Padding = new Thickness(12, 5),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Content = row,
            };
            button.Click += (_, _) => ActivateQueryTab(tabIndex);

            var container = new Border
            {
                Background = isActive ? tabActiveBg : tabInactiveBg,
                BorderBrush = isActive ? tabActiveBorder : tabInactiveBorder,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Child = button,
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

    private void InitializeQueryTabs()
    {
        _queryTabs.Clear();
        _activeQueryTabIndex = 0;
        _queryTabs.Add(
            new QueryTabState
            {
                FallbackTitle = "Consulta 1",
                SnapshotJson = CanvasSerializer.Serialize(CurrentVm),
                CurrentFilePath = CurrentVm.CurrentFilePath,
                IsDirty = CurrentVm.IsDirty,
            }
        );

        RefreshQueryTabsUi();
    }

    private void ResetCurrentCanvas()
    {
        if (_activeQueryTabIndex < 0 || _activeQueryTabIndex >= _queryTabs.Count)
            return;

        QueryTabState activeTab = _queryTabs[_activeQueryTabIndex];
        activeTab.SnapshotJson = CreateFreshCanvasSnapshot();
        activeTab.CurrentFilePath = null;
        activeTab.IsDirty = false;

        _isRestoringTab = true;
        try
        {
            RestoreTabState(_activeQueryTabIndex);
        }
        finally
        {
            _isRestoringTab = false;
        }

        RefreshQueryTabsUi();
    }

    private void CreateNewQueryTab()
    {
        CaptureActiveTabState();

        int tabNumber = _queryTabs.Count + 1;
        _queryTabs.Add(
            new QueryTabState
            {
                FallbackTitle = $"Consulta {tabNumber}",
                SnapshotJson = CreateFreshCanvasSnapshot(),
                CurrentFilePath = null,
                IsDirty = false,
            }
        );

        _activeQueryTabIndex = _queryTabs.Count - 1;

        _isRestoringTab = true;
        try
        {
            RestoreTabState(_activeQueryTabIndex);
        }
        finally
        {
            _isRestoringTab = false;
        }

        RefreshQueryTabsUi();
    }

    private void ToastDetailsBackdrop_PointerPressed(object? s, PointerPressedEventArgs e)
    {
        CurrentVm.Toasts.CloseDetailsCommand.Execute(null);
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
            Title = "Visual SQL Architect";
        });
        B("ShortcutsBtn", () => new KeyboardShortcutsWindow().Show(this));
        B("LanguageToggleBtn", () => LocalizationService.Instance.ToggleCulture());
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
            CurrentVm.TogglePreviewCommand.Execute(null);
        });
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

        if (CurrentVm.Toasts.IsDetailsOpen)
        {
            CurrentVm.Toasts.CloseDetailsCommand.Execute(null);
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
            return;
        }

        sidebarColumn.MinWidth = 220;
        sidebarColumn.MaxWidth = 500;
        sidebarColumn.Width = _lastLeftSidebarWidth.Value > 1 ? _lastLeftSidebarWidth : new GridLength(320);
        splitterColumn.Width = new GridLength(8);
        sidebarHost.IsVisible = true;
        splitter.IsVisible = true;
        if (rail is not null)
            rail.IsVisible = false;
        if (showBtn is not null)
            showBtn.IsVisible = false;
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
            return;
        }

        sidebarColumn.MinWidth = 240;
        sidebarColumn.MaxWidth = 640;
        sidebarColumn.Width = _lastRightSidebarWidth.Value > 1 ? _lastRightSidebarWidth : new GridLength(320);
        splitterColumn.Width = new GridLength(8);
        sidebarHost.IsVisible = true;
        splitter.IsVisible = true;
        if (rail is not null)
            rail.IsVisible = false;
        if (showBtn is not null)
            showBtn.IsVisible = false;
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
