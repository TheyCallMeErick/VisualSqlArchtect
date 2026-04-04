# Testes - Estrutura e Organização

## Visão Geral

O projeto de testes está organizado em **categorias lógicas** com fixtures compartilhadas para evitar duplicação de código.

```
tests/VisualSqlArchitect.Tests/
├── Fixtures/                 # Fixtures compartilhadas
│   └── TestFixtures.cs       # Builders e helpers para testes
│
├── Unit/                     # Testes unitários
│   ├── Metadata/
│   │   └── MetadataServiceTests.cs
│   ├── Nodes/
│   │   └── NodeEmissionTests.cs
│   └── Queries/
│       └── SqlFunctionRegistryTests.cs
│
├── Integration/              # Testes de integração (futuro)
│
└── VisualSqlArchitect.Tests.csproj
```

## Namespaces

- **VisualSqlArchitect.Tests.Fixtures** - Fixtures compartilhadas
- **VisualSqlArchitect.Tests.Unit.Metadata** - Testes de metadados
- **VisualSqlArchitect.Tests.Unit.Nodes** - Testes de emissão de nós
- **VisualSqlArchitect.Tests.Unit.Queries** - Testes de queries

## Arquivos de Teste

### 1. `Unit/Nodes/NodeEmissionTests.cs`
Testes para compilação e emissão de nós SQL.

**Fixtures disponíveis em `TestFixtures.Node`:**
- `PostgresContext`, `MySqlContext`, `SqlServerContext` - EmitContext para cada provedor
- `Column()` - Cria expressões de coluna
- `OrderTotal`, `UserEmail`, `EventPayload` - Colunas pré-definidas

### 2. `Unit/Metadata/MetadataServiceTests.cs`
Testes para inspeção e detecção automática de junções.

**Fixtures disponíveis em `TestFixtures.Metadata`:**
- `Column()` - Cria metadados de coluna
- `ForeignKey()` - Cria relações FK
- `Table()` - Cria tabelas completas
- `CreateEcommerceSchema()` - Schema completo de e-commerce para testes

### 3. `Unit/Queries/SqlFunctionRegistryTests.cs`
Testes para o registro de funções SQL por provedor.

**Fixtures disponíveis em `TestFixtures`:**
- Contextos de emissão
- Registros de funções SQL

## Como Usar as Fixtures

### Exemplo 1: Testar emissão de nó em PostgreSQL
```csharp
public class MyNodeTests
{
    [Fact]
    public void MyTest()
    {
        var ctx = TestFixtures.Node.PostgresContext;
        var column = TestFixtures.Node.OrderTotal;
        
        // seu teste aqui
    }
}
```

### Exemplo 2: Testar metadados com schema e-commerce
```csharp
public class MyMetadataTests
{
    [Fact]
    public void MyTest()
    {
        var schema = TestFixtures.Metadata.CreateEcommerceSchema();
        var ordersTable = schema.FindTable("public", "orders");
        
        // seu teste aqui
    }
}
```

## Executar Testes

```bash
# Todos os testes
dotnet test

# Apenas categoria específica
dotnet test --filter "Namespace=VisualSqlArchitect.Tests.Unit.Nodes"

# Com saída verbosa
dotnet test --verbosity=detailed
```

## Futuro: Testes de Integração

A pasta `Integration/` está reservada para testes que:
- Conectam a bancos de dados reais (Docker)
- Testam fluxos completos end-to-end
- Validam comportamento de múltiplos componentes juntos

## Matriz de Cobertura - Hardening QueryPreview

### Cobertura por categoria

| Categoria | Regras cobertas | Arquivos de teste |
|---|---|---|
| Type mismatch | Compatibilidade de pinos e conexoes invalidas | `tests/VisualSqlArchitect.Tests/Unit/ViewModels/QueryPreview/QueryGraphBuilderConnectionValidationTests.cs` |
| Predicate | AND/OR/COMPILE WHERE redundante ou vazio; NOT sem condition (ativo) | `tests/VisualSqlArchitect.Tests/Unit/ViewModels/QueryPreview/QueryGraphBuilderLogicGateValidationTests.cs`; `tests/VisualSqlArchitect.Tests/Unit/ViewModels/QueryPreview/QueryGraphBuilderNotAndJsonValidationTests.cs` |
| Comparison | Inputs obrigatorios de comparacao; LIKE sem pattern | `tests/VisualSqlArchitect.Tests/Unit/ViewModels/QueryPreview/QueryGraphBuilderComparisonValidationTests.cs`; `tests/VisualSqlArchitect.Tests/Unit/ViewModels/QueryPreview/QueryGraphBuilderQualifyTests.cs` |
| Window | value/order/frame/offset/ntile validation | `tests/VisualSqlArchitect.Tests/Unit/ViewModels/QueryPreview/QueryGraphBuilderWindowFunctionTests.cs` |
| CTE | nome, escopo, from inference, recursive prefix, CTE source alias | `tests/VisualSqlArchitect.Tests/Unit/ViewModels/QueryPreview/QueryGraphBuilderCteTests.cs` |
| Join | Join explicito por tipo; join incompleto; fallback legado | `tests/VisualSqlArchitect.Tests/Unit/ViewModels/QueryPreview/QueryGraphBuilderJoinTests.cs` |
| Subquery | shape SQL, alias handling e validacoes de subquery nodes | `tests/VisualSqlArchitect.Tests/Unit/ViewModels/QueryPreview/QueryGraphBuilderSubqueryTests.cs` |
| Set operation | operador suportado, query obrigatoria, shape SELECT | `tests/VisualSqlArchitect.Tests/Unit/ViewModels/QueryPreview/QueryGraphBuilderSetOperationTests.cs` |
| Query hints | hints por provider e validacao de sintaxe | `tests/VisualSqlArchitect.Tests/Unit/ViewModels/QueryPreview/QueryGraphBuilderQueryHintsTests.cs` |
| Pivot | pivot/unpivot provider-aware e configuracao invalida | `tests/VisualSqlArchitect.Tests/Unit/ViewModels/QueryPreview/QueryGraphBuilderPivotTests.cs` |
| Diagnostics estruturados | severidade/categoria/code + compatibilidade com mensagens legadas | `tests/VisualSqlArchitect.Tests/Unit/ViewModels/QueryPreview/PreviewDiagnosticMapperTests.cs`; `tests/VisualSqlArchitect.Tests/Unit/ViewModels/QueryPreview/QueryGraphBuilderDiagnosticsTests.cs` |
| Paginacao | TOP/LIMIT <= 0; OFFSET sem ORDER BY deterministico | `tests/VisualSqlArchitect.Tests/Unit/ViewModels/QueryPreview/QueryGraphBuilderPaginationValidationTests.cs` |
| Alias ambiguo | duplicidade de alias no mesmo escopo; sem falso positivo cross-scope | `tests/VisualSqlArchitect.Tests/Unit/ViewModels/QueryPreview/QueryGraphBuilderAliasAmbiguityTests.cs` |

### Regras criticas de warning (minimo 1 teste por regra)

| Regra critica | Coberto por |
|---|---|
| Conexao de tipos incompativeis | `QueryGraphBuilderConnectionValidationTests` |
| Predicate vazio/redundante em WHERE/HAVING/QUALIFY | `QueryGraphBuilderLogicGateValidationTests` |
| Comparacao sem input obrigatorio | `QueryGraphBuilderComparisonValidationTests` |
| Window sem inputs obrigatorios/frame invalido | `QueryGraphBuilderWindowFunctionTests` |
| CTE mal configurada (nome/escopo/fonte) | `QueryGraphBuilderCteTests` |
| Join incompleto/tipo invalido | `QueryGraphBuilderJoinTests` |
| Subquery invalida (shape/alias) | `QueryGraphBuilderSubqueryTests` |
| Set operation invalida (operator/query) | `QueryGraphBuilderSetOperationTests` |
| Query hints invalidos por provider | `QueryGraphBuilderQueryHintsTests` |
| Pivot configuracao invalida/provider incompatível | `QueryGraphBuilderPivotTests` |
| NOT/JSON sem input ou path invalido (quando ativo) | `QueryGraphBuilderNotAndJsonValidationTests` |
| TOP/LIMIT <= 0 e OFFSET sem ORDER BY | `QueryGraphBuilderPaginationValidationTests` |
| Alias duplicado no mesmo escopo logico | `QueryGraphBuilderAliasAmbiguityTests` |
| Diagnostico estruturado com code/category/severity | `PreviewDiagnosticMapperTests`; `QueryGraphBuilderDiagnosticsTests` |

## Snapshot critico de diagnosticos

- Escopo: apenas codigos de diagnostico criticos do preview, evitando snapshot fragil de SQL completo.
- Arquivo snapshot: `tests/VisualSqlArchitect.Tests/Fixtures/Snapshots/querypreview-diagnostic-codes.snap`.
- Teste verificador: `tests/VisualSqlArchitect.Tests/Unit/ViewModels/QueryPreview/QueryPreviewDiagnosticSnapshotTests.cs`.

### Politica de atualizacao do snapshot

1. Atualize o snapshot somente quando houver mudanca intencional de codigos de diagnostico (ex.: nova regra de hardening ou renomeacao deliberada de codigo).
2. Nao atualize snapshot para mascarar regressao; primeiro confirme a causa e ajuste o codigo/teste de regra.
3. Em PR, descreva quais codigos mudaram e por que.
4. Mantenha foco em codigos/mensagens criticas; nao adicionar snapshot de SQL inteiro quando nao for estritamente necessario.
