using DBWeaver.Core;
using DBWeaver.Ddl.SchemaAnalysis.Application.Processing;

namespace DBWeaver.Tests.Unit.Ddl.SchemaAnalysis;

public sealed class SchemaSqlDialectEscapingTests
{
    private readonly SchemaSqlDialectEscaping _escaping = new();

    [Fact]
    public void QuoteIdentifier_EscapesClosingBracket_ForSqlServer()
    {
        string quoted = _escaping.QuoteIdentifier(DatabaseProvider.SqlServer, "order]items");

        Assert.Equal("[order]]items]", quoted);
    }

    [Fact]
    public void QuoteIdentifier_EscapesDoubleQuote_ForPostgres()
    {
        string quoted = _escaping.QuoteIdentifier(DatabaseProvider.Postgres, "order\"items");

        Assert.Equal("\"order\"\"items\"", quoted);
    }

    [Fact]
    public void QuoteIdentifier_EscapesBacktick_ForMySql()
    {
        string quoted = _escaping.QuoteIdentifier(DatabaseProvider.MySql, "order`items");

        Assert.Equal("`order``items`", quoted);
    }

    [Fact]
    public void QuoteStringLiteral_DuplicatesSingleQuotes()
    {
        string quoted = _escaping.QuoteStringLiteral(DatabaseProvider.Postgres, "O'Brien");

        Assert.Equal("'O''Brien'", quoted);
    }

    [Fact]
    public void QuoteUnicodeStringLiteral_UsesUnicodePrefix_ForSqlServer()
    {
        string quoted = _escaping.QuoteUnicodeStringLiteral(DatabaseProvider.SqlServer, "Descrição");

        Assert.Equal("N'Descrição'", quoted);
    }

    [Fact]
    public void QuoteQualifiedName_UsesCanonicalSchema_WhenAvailable()
    {
        string quoted = _escaping.QuoteQualifiedName(DatabaseProvider.SqlServer, "dbo", "orders");

        Assert.Equal("[dbo].[orders]", quoted);
    }

    [Fact]
    public void QuoteQualifiedName_OmitsSchema_WhenCanonicalSchemaIsNull()
    {
        string quoted = _escaping.QuoteQualifiedName(DatabaseProvider.MySql, null, "orders");

        Assert.Equal("`orders`", quoted);
    }
}
