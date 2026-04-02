using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Controls.Templates;
using Avalonia.Media;
using Avalonia;
using VisualSqlArchitect.UI.Services.Localization;
using VisualSqlArchitect.UI.ViewModels;

namespace VisualSqlArchitect.UI.Controls;

public partial class ConnectionTabControl : UserControl
{
    public ICommand? ProfileActionCommand { get; private set; }
    private ConnectionManagerViewModel? _vm;

    public ConnectionTabControl()
    {
        InitializeComponent();
        this.DataContextChanged += (_, _) =>
        {
            if (DataContext is ConnectionManagerViewModel vm)
            {
                _vm = vm;

                // Create command to handle connect/disconnect
                ProfileActionCommand = new RelayCommand<ConnectionProfile?>(profile =>
                {
                    if (profile != null && vm != null)
                    {
                        // Check if this profile is already active
                        if (vm.ActiveProfileId == profile.Id)
                        {
                            // Disconnect
                            vm.DisconnectCommand.Execute(null);
                        }
                        else
                        {
                            // Connect
                            vm.SelectedProfile = profile;
                            if (vm.ConnectCommand.CanExecute(null))
                                vm.ConnectCommand.Execute(null);
                        }
                        UpdateButtonStates();
                    }
                });

                // Update button states when connection changes or connecting status changes
                vm.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName == nameof(ConnectionManagerViewModel.ActiveProfileId) ||
                        e.PropertyName == nameof(ConnectionManagerViewModel.IsConnecting))
                    {
                        UpdateButtonStates();
                    }
                };

                UpdateButtonStates();
            }
        };
    }

    private void UpdateButtonStates()
    {
        if (_vm == null) return;

        // Find the ItemsControl with profiles
        var itemsControl = this.FindControl<ItemsControl>("ProfilesItemsControl");
        if (itemsControl?.ItemsPanel == null) return;

        var panel = itemsControl.ItemsPanel as Panel;
        if (panel == null) return;

        foreach (var child in panel.Children)
        {
            if (child is Border border && border.Child is Grid grid)
            {
                // Try to get the profile from the data context
                if (border.DataContext is ConnectionProfile profile)
                {
                    var isActive = profile.Id == _vm.ActiveProfileId;

                    // Find button and status dot in this grid
                    var button = grid.Children.OfType<Button>().FirstOrDefault();
                    var dot = grid.Children.OfType<Ellipse>().FirstOrDefault();
                    SetClass(border, "active", isActive);

                    if (button != null)
                    {
                        button.IsEnabled = !_vm.IsConnecting;
                        button.Content = isActive
                            ? LocalizationService.Instance["connection.disconnect"]
                            : LocalizationService.Instance["connection.connect"];
                        SetClass(button, "danger", isActive);
                        SetClass(button, "secondary", !isActive);
                    }

                    if (dot != null)
                    {
                        dot.Fill = isActive
                            ? ResolveBrush("BtnSuccessFgBrush", "#DCFCE7")
                            : ResolveBrush("TextSecondaryBrush", "#8B95A8");
                    }
                }
            }
        }
    }

    private static void SetClass(StyledElement element, string className, bool enabled)
    {
        if (enabled)
        {
            if (!element.Classes.Contains(className))
                element.Classes.Add(className);
            return;
        }

        if (element.Classes.Contains(className))
            element.Classes.Remove(className);
    }

    private static IBrush ResolveBrush(string resourceKey, string fallbackHex)
    {
        if (Application.Current?.TryFindResource(resourceKey, out object? resource) == true && resource is IBrush brush)
            return brush;

        return new SolidColorBrush(Color.Parse(fallbackHex));
    }
}
