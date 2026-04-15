using AkkornStudio.UI.Services.ConnectionManager;
using AkkornStudio.UI.Services.Benchmark;
using System.ComponentModel;
using AkkornStudio.Core;
using AkkornStudio.UI.Services.Localization;
using AkkornStudio.UI.ViewModels;
using Xunit;

namespace AkkornStudio.Tests.Unit.ViewModels;

public class ConnectionErrorMessageMapperTests
{
    [Fact]
    public void Map_TimeoutException_ReturnsTimeoutFriendlyMessage()
    {
        var mapper = new ConnectionErrorMessageMapper(new FakeLocalizationService());

        string message = mapper.Map(new TimeoutException("boom"), DatabaseProvider.Postgres);

        Assert.Contains("Connection timed out", message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Map_AuthenticationFailure_ContainsProviderName()
    {
        var mapper = new ConnectionErrorMessageMapper(new FakeLocalizationService());

        string message = mapper.Map(new Exception("authentication failed"), DatabaseProvider.MySql);

        Assert.Contains("Authentication failed", message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("MySql", message, StringComparison.Ordinal);
    }

    [Fact]
    public void Map_HostFailure_ReturnsHostNotFoundMessage()
    {
        var mapper = new ConnectionErrorMessageMapper(new FakeLocalizationService());

        string message = mapper.Map(new Exception("no such host"), DatabaseProvider.Postgres);

        Assert.Contains("Host not found", message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Map_UnknownLongMessage_TruncatesToUiSafeLength()
    {
        var mapper = new ConnectionErrorMessageMapper(new FakeLocalizationService());
        string source = new string('x', 220);

        string message = mapper.Map(new Exception(source), DatabaseProvider.Postgres);

        Assert.True(message.Length <= 163);
        Assert.EndsWith("...", message, StringComparison.Ordinal);
    }

    private sealed class FakeLocalizationService : ILocalizationService
    {
        public event PropertyChangedEventHandler? PropertyChanged
        {
            add { }
            remove { }
        }

        public string CurrentCulture => "en-US";
        public string CurrentLanguageLabel => "English";
        public string this[string key] => key;
        public bool ToggleCulture() => false;
        public bool SetCulture(string culture) => false;
    }
}


