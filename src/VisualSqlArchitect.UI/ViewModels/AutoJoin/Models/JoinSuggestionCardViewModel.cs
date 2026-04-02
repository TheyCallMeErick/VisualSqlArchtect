using System.Windows.Input;
using Avalonia.Media;
using VisualSqlArchitect.Metadata;

namespace VisualSqlArchitect.UI.ViewModels;

public sealed class JoinSuggestionCardViewModel : ViewModelBase
{
    private bool _isAccepted;
    private bool _isDismissed;

    public JoinSuggestion Suggestion { get; }

    public string TablePair => $"{Suggestion.ExistingTable}  ↔  {Suggestion.NewTable}";
    public string OnClause => Suggestion.OnClause;
    public string JoinType => Suggestion.JoinType + " JOIN";
    public string Rationale => Suggestion.Rationale;
    public string ScoreLabel => $"{Suggestion.Score * 100:F0}%";
    public bool IsCatalogFk => Suggestion.Confidence >= JoinConfidence.CatalogDefinedFk;
    public double ScoreBarWidth => Math.Max(4, Suggestion.Score * 340);

    public Color ConfidenceColor =>
        Suggestion.Confidence switch
        {
            >= JoinConfidence.CatalogDefinedFk => Color.Parse("#4ADE80"),
            >= JoinConfidence.CatalogDefinedReverse => Color.Parse("#60A5FA"),
            >= JoinConfidence.HeuristicStrong => Color.Parse("#FBBF24"),
            _ => Color.Parse("#94A3B8"),
        };

    public SolidColorBrush ConfidenceBrush => new(ConfidenceColor);

    public string ConfidenceLabel =>
        Suggestion.Confidence switch
        {
            >= JoinConfidence.CatalogDefinedFk => "FK Constraint",
            >= JoinConfidence.CatalogDefinedReverse => "FK (Reverse)",
            >= JoinConfidence.HeuristicStrong => "Naming Match",
            _ => "Weak Match",
        };

    public bool IsAccepted
    {
        get => _isAccepted;
        private set => Set(ref _isAccepted, value);
    }

    public bool IsDismissed
    {
        get => _isDismissed;
        private set => Set(ref _isDismissed, value);
    }

    public bool IsVisible => !IsAccepted && !IsDismissed;

    public ICommand AcceptCommand { get; }
    public ICommand DismissCommand { get; }

    public event EventHandler<JoinSuggestion>? Accepted;
    public event EventHandler<JoinSuggestion>? Dismissed;

    public JoinSuggestionCardViewModel(JoinSuggestion suggestion)
    {
        Suggestion = suggestion;
        AcceptCommand = new RelayCommand(Accept);
        DismissCommand = new RelayCommand(Dismiss);
    }

    public void Accept()
    {
        IsAccepted = true;
        RaisePropertyChanged(nameof(IsVisible));
        Accepted?.Invoke(this, Suggestion);
    }

    public void Dismiss()
    {
        IsDismissed = true;
        RaisePropertyChanged(nameof(IsVisible));
        Dismissed?.Invoke(this, Suggestion);
    }
}
