using AkkornStudio.Core;
using AkkornStudio.UI.Services.SqlEditor;

namespace AkkornStudio.Tests.Unit.Services.SqlEditor;

public sealed class SqlSignatureHelpServiceTests
{
    [Fact]
    public void TryResolve_WithKnownFunction_ReturnsSignatureAndActiveParameter()
    {
        var sut = new SqlSignatureHelpService();
        const string sql = "SELECT DATE_TRUNC('day', NOW())";
        int caretOffset = sql.IndexOf("NOW", StringComparison.Ordinal);

        SignatureHelpInfo? result = sut.TryResolve(sql, caretOffset, DatabaseProvider.Postgres);

        Assert.NotNull(result);
        Assert.Equal("DATE_TRUNC", result.Signature.Name);
        Assert.Equal(1, result.ActiveParameterIndex);
        Assert.Contains("[source: timestamp]", result.DisplayText, StringComparison.Ordinal);
    }

    [Fact]
    public void TryResolve_WithUnknownFunction_ReturnsNull()
    {
        var sut = new SqlSignatureHelpService();
        const string sql = "SELECT CUSTOM_FN(";

        SignatureHelpInfo? result = sut.TryResolve(sql, sql.Length, DatabaseProvider.Postgres);

        Assert.Null(result);
    }

    [Fact]
    public void TryResolve_WithoutCallContext_ReturnsNull()
    {
        var sut = new SqlSignatureHelpService();
        const string sql = "SELECT * FROM public.orders";

        SignatureHelpInfo? result = sut.TryResolve(sql, sql.Length, DatabaseProvider.Postgres);

        Assert.Null(result);
    }

    [Fact]
    public void TryResolve_WithLargePrefixAndNearbyFunctionCall_ReturnsSignature()
    {
        var sut = new SqlSignatureHelpService();
        string prefix = new string('x', 10_000);
        string sql = $"{prefix}\nSELECT DATE_TRUNC('day', NOW())";
        int caretOffset = sql.IndexOf("NOW", StringComparison.Ordinal);

        SignatureHelpInfo? result = sut.TryResolve(sql, caretOffset, DatabaseProvider.Postgres);

        Assert.NotNull(result);
        Assert.Equal("DATE_TRUNC", result.Signature.Name);
        Assert.Equal(1, result.ActiveParameterIndex);
    }
}
