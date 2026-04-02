using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using System.ComponentModel;
using VisualSqlArchitect.UI.ViewModels;

namespace VisualSqlArchitect.UI.Controls;

public partial class SidebarControl : UserControl
{
    private bool _buttonsWired = false;
    private SidebarViewModel? _subscribedVm;
    private bool _isAnimatingTab;

    public SidebarControl()
    {
        InitializeComponent();

        // Wire up button click handlers when loaded
        this.Loaded += (_, _) => WireUpButtons();
    }

    private void WireUpButtons()
    {
        if (_buttonsWired || DataContext is not SidebarViewModel vm)
            return;

        _buttonsWired = true;

        var nodesButton = this.FindControl<Button>("NodesTabButton");
        var connectionButton = this.FindControl<Button>("ConnectionTabButton");
        var schemaButton = this.FindControl<Button>("SchemaTabButton");
        var diagnosticsButton = this.FindControl<Button>("DiagnosticsTabButton");

        if (nodesButton != null)
        {
            nodesButton.Click += (_, _) => vm.ActiveTab = SidebarTab.Nodes;
        }
        if (connectionButton != null)
        {
            connectionButton.Click += (_, _) => vm.ActiveTab = SidebarTab.Connection;
        }
        if (schemaButton != null)
        {
            schemaButton.Click += (_, _) => vm.ActiveTab = SidebarTab.Schema;
        }
        if (diagnosticsButton != null)
        {
            diagnosticsButton.Click += (_, _) =>
            {
                vm.ActiveTab = SidebarTab.Diagnostics;
                vm.Diagnostics.RunChecksCommand.Execute(null);
            };
        }

        // Set child control DataContexts
        var nodesControl = this.FindControl<NodesListControl>("NodesControl");
        var connectionControl = this.FindControl<ConnectionTabControl>("ConnectionControl");
        var schemaControl = this.FindControl<SchemaControl>("SchemaControl");
        var diagnosticsControl = this.FindControl<SidebarDiagnosticsControl>("DiagnosticsControl");

        if (nodesControl != null)
            nodesControl.DataContext = vm.NodesList;
        if (connectionControl != null)
            connectionControl.DataContext = vm.ConnectionManager;
        if (schemaControl != null)
            schemaControl.DataContext = vm.Schema;
        if (diagnosticsControl != null)
            diagnosticsControl.DataContext = vm.Diagnostics;

        AttachVmSubscriptions(vm);
        _ = AnimateActiveTabAsync(vm.ActiveTab);
    }

    private void AttachVmSubscriptions(SidebarViewModel vm)
    {
        if (ReferenceEquals(_subscribedVm, vm))
            return;

        if (_subscribedVm is not null)
        {
            _subscribedVm.PropertyChanged -= OnSidebarPropertyChanged;
            _subscribedVm.AddNodeRequested -= OnAddNodeRequested;
        }

        _subscribedVm = vm;
        _subscribedVm.PropertyChanged += OnSidebarPropertyChanged;
        _subscribedVm.AddNodeRequested += OnAddNodeRequested;
    }

    private void OnSidebarPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(SidebarViewModel.ActiveTab) || _subscribedVm is null)
            return;

        _ = AnimateActiveTabAsync(_subscribedVm.ActiveTab);
    }

    private void OnAddNodeRequested()
    {
        Dispatcher.UIThread.Post(() =>
        {
            TextBox? search = this.FindControl<TextBox>("NodesSearchBox");
            search?.Focus();
        }, DispatcherPriority.Background);
    }

    private async Task AnimateActiveTabAsync(SidebarTab tab)
    {
        if (_isAnimatingTab)
            return;

        Control? target = tab switch
        {
            SidebarTab.Nodes => this.FindControl<Control>("NodesControl"),
            SidebarTab.Connection => this.FindControl<Control>("ConnectionControl"),
            SidebarTab.Schema => this.FindControl<Control>("SchemaControl"),
            SidebarTab.Diagnostics => this.FindControl<Control>("DiagnosticsControl"),
            _ => null,
        };

        if (target is null)
            return;

        _isAnimatingTab = true;
        try
        {
            target.Opacity = 0;
            const int steps = 5;
            for (int i = 1; i <= steps; i++)
            {
                target.Opacity = i / (double)steps;
                await Task.Delay(18);
            }
        }
        finally
        {
            _isAnimatingTab = false;
        }
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        // Reset and re-wire if data context changes
        if (this.IsLoaded)
        {
            _buttonsWired = false;
            WireUpButtons();
        }
    }
}
