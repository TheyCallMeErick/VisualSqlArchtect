# Análise de Qualidade de Código — Visual SQL Architect

> Análise técnica baseada em leitura direta do código-fonte. Cada achado inclui arquivo,
> linha e trecho de código real. Nenhuma sugestão é genérica — cada recomendação aponta
> para um local específico e descreve exatamente o que mudar.
>
> **Escopo:** SOLID · 12-Factor App · Convenções C# · Cobertura de testes · Tratamento de
> erros · Arquitetura em camadas · Anti-patterns

---

## Índice

1. [God Classes](#1-god-classes)
2. [Princípio da Responsabilidade Única (SRP)](#2-srp---responsabilidade-única)
3. [Princípio Aberto/Fechado (OCP)](#3-ocp---abertofechado)
4. [Inversão de Dependência (DIP)](#4-dip---inversão-de-dependência)
5. [Segregação de Interface (ISP)](#5-isp---segregação-de-interface)
6. [12-Factor App](#6-12-factor-app)
7. [Convenções de Código C#](#7-convenções-de-código-c)
8. [Arquitetura em Camadas](#8-arquitetura-em-camadas)
9. [Tratamento de Erros](#9-tratamento-de-erros)
10. [Cobertura de Testes](#10-cobertura-de-testes)
11. [Anti-Patterns Adicionais](#11-anti-patterns-adicionais)
12. [Ranking de Severidade](#12-ranking-de-severidade)
13. [Plano de Remediação](#13-plano-de-remediação)

---

## 1. God Classes

### 1.1 `CanvasViewModel` — **Crítico** (1.294 linhas)

**Arquivo:** `src/VisualSqlArchitect.UI/ViewModels/CanvasViewModel.cs`

Esta é a violação mais grave do projeto. A classe age simultaneamente como:

- **Facade** de todo o canvas (orquestra 5 managers)
- **Dono** de 16 ViewModels filho
- **Container de estado** do canvas (zoom, pan, arquivo, metadados, CTE)
- **Dispatcher de comandos** (re-expõe 20+ comandos delegados dos managers)
- **Gerenciador de ciclo de vida** (implementa `IDisposable`, controla subscrição/remoção de eventos)

**Evidência — propriedades de ViewModels filho (linhas 42–57):**

```csharp
public SearchMenuViewModel SearchMenu { get; }
public CommandPaletteViewModel CommandPalette { get; } = new();
public DataPreviewViewModel DataPreview { get; } = new();
public ToastCenterViewModel Toasts { get; } = new();
public AppDiagnosticsViewModel Diagnostics { get; }
public PropertyPanelViewModel PropertyPanel { get; }
public LiveSqlBarViewModel LiveSql { get; set; }
public AutoJoinOverlayViewModel AutoJoin { get; set; }
public UndoRedoStack UndoRedo { get; }
public ConnectionManagerViewModel ConnectionManager { get; } = new();
public BenchmarkViewModel Benchmark { get; private set; } = null!;
public ExplainPlanViewModel ExplainPlan { get; private set; } = null!;
public SqlImporterViewModel SqlImporter { get; private set; } = null!;
public FlowVersionOverlayViewModel FlowVersions { get; private set; } = null!;
public FileVersionHistoryViewModel FileHistory { get; private set; } = null!;
public SidebarViewModel Sidebar { get; private set; } = null!;
```

**Evidência — managers instanciados no construtor (linhas 231–243):**

```csharp
public CanvasViewModel()
{
    UndoRedo = new UndoRedoStack(this);
    SearchMenu = new SearchMenuViewModel();
    PropertyPanel = new PropertyPanelViewModel(UndoRedo);
    Diagnostics = new AppDiagnosticsViewModel(this);

    _nodeManager   = new NodeManager(Nodes, Connections, UndoRedo, PropertyPanel, SearchMenu);
    _selectionManager = new SelectionManager(Nodes, PropertyPanel, UndoRedo);
    _layoutManager = new NodeLayoutManager(this, UndoRedo);
    _pinManager    = new PinManager(Nodes, Connections, UndoRedo);
    _validationManager = new ValidationManager(this);
```

**Evidência — 6 handlers de evento armazenados como campos para disposição manual (linhas 72–77):**

```csharp
private PropertyChangedEventHandler? _liveSqlPropertyChangedHandler;
private PropertyChangedEventHandler? _selfPropertyChangedHandler;
private PropertyChangedEventHandler? _layoutManagerPropertyChangedHandler;
private PropertyChangedEventHandler? _localizationPropertyChangedHandler;
private NotifyCollectionChangedEventHandler? _nodesCollectionChangedHandler;
private NotifyCollectionChangedEventHandler? _connectionsCollectionChangedHandler;
```

Seis campos de handler são um indicador claro de que a classe gerencia ciclo de vida de objetos
demais — responsabilidade que deveria pertencer a um container de DI.

**Evidência — 20+ comandos re-expostos como delegações (linhas 210–227):**

```csharp
// Delegated from SelectionManager
public RelayCommand SelectAllCommand    => _selectionManager.SelectAllCommand;
public RelayCommand AlignLeftCommand    => _selectionManager.AlignLeftCommand;
// ... 8 mais

// Delegated from NodeLayoutManager
public RelayCommand ZoomInCommand       => _layoutManager.ZoomInCommand;
public RelayCommand FitToScreenCommand  => _layoutManager.FitToScreenCommand;
// ... 5 mais
```

**Diagnóstico:** A class `CanvasViewModel` é a definição textual de uma God Class. Um ViewModel
deveria expor estado observável e comandos de *uma* responsabilidade. Esta classe expõe estado de
16 domínios diferentes e delega para 5 managers que ela mesma instancia.

---

### 1.2 `MainWindow.axaml.cs` — **Crítico** (1.134 linhas)

**Arquivo:** `src/VisualSqlArchitect.UI/Views/Shell/MainWindow.axaml.cs`

Code-behind com 1.134 linhas é, por si só, uma violação arquitetural. Em MVVM, o code-behind deve
ser quase vazio — limitado a interações que não são expressáveis em AXAML (drag-drop, foco de
teclado, animações imperativas). Esta classe faz muito mais:

**Responsabilidades identificadas:**

| Responsabilidade | Linhas | Comentário |
|---|---|---|
| Ciclo de vida da janela | 65–75 | Aceitável em code-behind |
| Construção imperativa de menu (UI) | 89–174 | **Violação** — deveria ser AXAML |
| Wiring de 7 serviços | 501–524 | **Violação** — pertence ao container de DI |
| Construção imperativa da árvore de schema | 526–607 | **Violação grave** — deveria ser binding |
| Gerenciamento de estado de tabs | 49–51, 609–796 | **Violação** — pertence ao ViewModel |
| Navegação start ↔ canvas | 176–195 | **Violação** — pertence ao NavigationService |
| Wiring de eventos de 5 subsistemas | 197–400 | **Violação** — acoplamento excessivo |

**Evidência — `UpdateSchemaTree()` construindo UI em C# (linhas 526–607):**

```csharp
private void UpdateSchemaTree()
{
    // ... resolve brushes de recursos do tema ...

    var schemaTree = this.FindControl<TreeView>("SchemaTree");
    schemaTree.Items.Clear();

    foreach (var schema in CurrentVm.DatabaseMetadata.Schemas)
    {
        var schemaItem = new TreeViewItem
        {
            Header = new StackPanel
            {
                Children =
                {
                    new Material.Icons.Avalonia.MaterialIcon { Kind = ..., Foreground = mutedBrush },
                    new TextBlock { Text = schema.Name, FontWeight = FontWeight.Medium }
                }
            }
        };

        foreach (var table in schema.Tables.OrderBy(t => t.Name))
        {
            var tableItem = new TreeViewItem { /* ... */ };
            // ... mais itens construídos imperativamente ...
        }

        schemaTree.Items.Add(schemaItem);
    }
}
```

Este método é a violação mais flagrante de MVVM no projeto. A construção de `TreeViewItem`s em
C# quebra completamente a separação View/ViewModel. Os dados (`DatabaseMetadata`) já existem
no ViewModel — falta apenas um `DataTemplate` em AXAML e binding correto.

> **Nota:** Existe um `SidebarViewModel` com tabs. A árvore de schema deveria ser renderizada via
> `ItemsControl` ou `TreeView` bindado em AXAML, com `HierarchicalDataTemplate`.

**Evidência — `InitializeServices()` instanciando serviços manualmente (linhas 501–524):**

```csharp
private void InitializeServices(CanvasViewModel vm)
{
    _layoutService   = new MainWindowLayoutService(this, vm);
    _sessionService  = new SessionManagementService(this, vm);
    _fileOps         = new FileOperationsService(this, vm);
    _keyboardHandler = new KeyboardInputHandler(this, vm, _fileOps, CreateNewQueryTab);
    _export          = new ExportService(this, vm);
    _preview         = new PreviewService(this, vm);
    _commandFactory  = new CommandPaletteFactory(this, vm, _fileOps, _export, _preview, CreateNewQueryTab);
    // ...
}
```

Sete serviços instanciados manualmente com `new`. Sem injeção de dependência.

**Evidência — estado de tab gerenciado no code-behind:**

```csharp
// Linhas 25–31 — modelo de dados privado DENTRO do code-behind
private sealed class QueryTabState
{
    public required string FallbackTitle { get; init; }
    public string? SnapshotJson { get; set; }
    public string? CurrentFilePath { get; set; }
    public bool IsDirty { get; set; }
}

private readonly List<QueryTabState> _queryTabs = [];  // linha 49
private int _activeQueryTabIndex;                        // linha 50
```

Um `List<QueryTabState>` no code-behind da janela é uma violação direta de MVVM. O estado de
sessão de tabs deveria viver em um `SessionViewModel` ou `TabManagerViewModel`.

**Evidência — strings de menu hardcoded em Português (linhas 132–171):**

```csharp
NewItem("Novo diagrama",       MaterialIconKind.FileOutline,        ...),
NewItem("Abrir arquivo",       MaterialIconKind.FolderOpenOutline,  ...),
NewItem("Salvar",              MaterialIconKind.ContentSave,        ...),
NewItem("Historico de arquivos", MaterialIconKind.History,          ...),
NewItem("Atalhos de teclado",  MaterialIconKind.Keyboard,           ...),
NewItem("Configurações",       MaterialIconKind.CogOutline,         ...),
NewItem("Voltar para inicio",  MaterialIconKind.Home,               ...),
```

Strings de UI hardcoded em C# não são localizáveis. Além disso, "Historico" deveria ser
"Histórico" e "inicio" deveria ser "início" — acentuação incorreta.

---

### 1.3 `QueryGeneratorService.cs` — **Moderado** (673 linhas)

**Arquivo:** `src/VisualSqlArchitect/QueryEngine/QueryGeneratorService.cs`

A classe tem uma responsabilidade central clara (gerar SQL a partir de `NodeGraph`), mas acumula
múltiplas transformações pós-compilação que deveriam ser estratégias separadas:

| Método | Linhas | Responsabilidade separável |
|---|---|---|
| `ApplyQueryHints()` | 189–203 | Aplicação de hints por provider |
| `ApplyPivotOperation()` | 227–244 | Transformação PIVOT/UNPIVOT |
| `ApplySetOperation()` | 148–167 | Combinação de operações SET |
| `ApplyQualifyClause()` | 169–187 | Emulação de QUALIFY |
| `OrderCtesByDependencies()` | 278–345 | Ordenação topológica de CTEs |
| `BuildDebugTree()` | 600–650 (aprox.) | Formatação de árvore de debug |

---

## 2. SRP — Responsabilidade Única

### 2.1 `CanvasViewModel` orquestra domínios demais

Ver seção 1.1. A classe mistura: estado de arquivo, estado de conexão, estado de canvas (zoom/pan),
validação, comandos de layout, CTE editor, e ainda delega para managers que ela mesma instancia.

**Separação ideal:**

```
CanvasStateViewModel       — zoom, pan, IsDirty, CurrentFilePath
CanvasSessionViewModel     — conexão ativa, DatabaseMetadata
CteEditorViewModel         — estado de sessão de edição de CTE
CanvasFacade               — composição dos acima (mantém a API atual)
```

### 2.2 `MainWindow.axaml.cs` mistura View, Navigation e Services

Ver seção 1.2. O code-behind acumula responsabilidades que pertencem a:

- `NavigationService` (transição start ↔ canvas)
- `SchemaTreeViewModel` (dados da árvore de schema)
- `TabManagerViewModel` (estado de tabs)
- Container de DI (instanciação de serviços)

### 2.3 `UpdateSchemaTree()` viola MVVM por completo

**Arquivo:** `MainWindow.axaml.cs`, linhas 526–607

Construir `TreeViewItem` em C# code-behind é a violação mais clara do padrão MVVM. A árvore
`Schema → Table → Column` já tem representação em `DbMetadata` / `SchemaMetadata` /
`TableMetadata` / `ColumnMetadata`. Basta um `HierarchicalDataTemplate` em AXAML:

```xml
<!-- Como deveria ser — zero linhas de C# necessárias -->
<TreeView ItemsSource="{Binding Sidebar.SchemaTree}">
    <TreeView.ItemTemplate>
        <HierarchicalDataTemplate DataType="{x:Type local:SchemaNodeVm}"
                                  ItemsSource="{Binding Tables}">
            <TextBlock Text="{Binding Name}" />
        </HierarchicalDataTemplate>
    </TreeView.ItemTemplate>
</TreeView>
```

---

## 3. OCP — Aberto/Fechado

### 3.1 `ConnectionConfig.BuildConnectionString()` — switch aberto a modificação

**Arquivo:** `src/VisualSqlArchitect/Core/IDbOrchestrator.cs`, linhas 42–50

```csharp
public string BuildConnectionString() =>
    Provider switch
    {
        DatabaseProvider.SqlServer => BuildSqlServerCs(),
        DatabaseProvider.MySql     => BuildMySqlCs(),
        DatabaseProvider.Postgres  => BuildPostgresCs(),
        DatabaseProvider.SQLite    => BuildSqliteCs(),
        _ => throw new NotSupportedException($"Provider {Provider} is not supported."),
    };
```

Adicionar suporte a um novo provider (DuckDB, Snowflake, Oracle) exige modificar esta classe —
violação direta do OCP.

**Solução:** extrair `IConnectionStringBuilder` como interface e registrar implementações por
provider. O `ConnectionConfig` chama `builder.Build(config)` sem saber qual provider é.

### 3.2 `DbOrchestratorFactory.Create()` — switch idêntico

**Arquivo:** `src/VisualSqlArchitect/ServiceRegistration.cs`, linhas 18–26

```csharp
public static IDbOrchestrator Create(ConnectionConfig config) =>
    config.Provider switch
    {
        DatabaseProvider.SqlServer => new SqlServerOrchestrator(config),
        DatabaseProvider.MySql     => new MySqlOrchestrator(config),
        DatabaseProvider.Postgres  => new PostgresOrchestrator(config),
        DatabaseProvider.SQLite    => new SqliteOrchestrator(config),
        _ => throw new NotSupportedException(...)
    };
```

Mesmo padrão. Para adicionar um provider, é necessário modificar esta factory.

**Solução:** um dicionário de factories registradas:

```csharp
// Extensível sem modificação da factory
private static readonly Dictionary<DatabaseProvider, Func<ConnectionConfig, IDbOrchestrator>>
    _registry = new()
    {
        [DatabaseProvider.SqlServer] = cfg => new SqlServerOrchestrator(cfg),
        [DatabaseProvider.Postgres]  = cfg => new PostgresOrchestrator(cfg),
        // ...
    };

public static void Register(DatabaseProvider p, Func<ConnectionConfig, IDbOrchestrator> factory)
    => _registry[p] = factory;
```

### 3.3 `QueryGeneratorService.ApplyQueryHints()` — switch por provider

**Arquivo:** `src/VisualSqlArchitect/QueryEngine/QueryGeneratorService.cs`, linhas 196–202

```csharp
return _provider switch
{
    DatabaseProvider.SqlServer => ApplySqlServerHints(baseSql, hints),
    DatabaseProvider.MySql     => ApplySelectCommentHints(baseSql, hints),
    DatabaseProvider.Postgres  => ApplySelectCommentHints(baseSql, hints),
    _ => baseSql,
};
```

Adicionar DuckDB (que usa `SET THREADS = 4` como hint) exige modificar este switch.

**Solução:** mover a lógica de hints para `ISqlDialect`:

```csharp
public interface ISqlDialect
{
    // ... métodos existentes ...
    string ApplyQueryHints(string sql, string hints);  // novo método
}
```

### 3.4 `NodeCompilerFactory` — **Conforme** ✓

**Arquivo:** `src/VisualSqlArchitect/Nodes/Compilers/NodeCompilerFactory.cs`

Este é o melhor exemplo do projeto de OCP correto. O factory usa Strategy pattern com array de
`INodeCompiler`:

```csharp
private readonly INodeCompiler[] _compilers =
[
    new DataSourceCompiler(),
    new StringTransformCompiler(),
    new MathTransformCompiler(),
    // ...
];

private INodeCompiler FindCompiler(NodeType nodeType) =>
    _compilers.FirstOrDefault(c => c.CanCompile(nodeType))
    ?? throw new NotSupportedException(...);
```

Adicionar um novo tipo de nó requer apenas criar um novo `INodeCompiler` e adicioná-lo ao array
— sem modificar o factory existente. **Padrão exemplar no projeto.**

---

## 4. DIP — Inversão de Dependência

### 4.1 Container de DI existe mas não é usado pela UI

**Arquivo:** `src/VisualSqlArchitect/ServiceRegistration.cs`

O projeto define um container de DI no core, mas a camada UI (`MainWindow`, `CanvasViewModel`)
não o usa. Todos os serviços são instanciados manualmente com `new`.

**Evidência — container de DI no core (não alcança a UI):**

```csharp
services.AddSingleton<ActiveConnectionContext>();
services.AddTransient<ISqlFunctionRegistry>(...);
services.AddTransient<QueryBuilderService>(...);
```

**Evidência — `App.axaml.cs` não inicializa DI:**

O ponto de entrada da aplicação (`App.axaml.cs`) não configura nenhum container. O
`MainWindow` recebe seus serviços por instanciação direta.

**Consequências:**
1. Impossível substituir implementações em testes
2. Ordem de inicialização depende de `null!` e inicializações tardias
3. `CanvasViewModel()` construtor sem parâmetros torna impossível a injeção de dependências

### 4.2 `CanvasViewModel()` — construtor sem parâmetros cria todas as dependências

**Arquivo:** `CanvasViewModel.cs`, linhas 231–243 (ver seção 1.1)

O construtor cria 10+ objetos com `new`. Não há como testar `CanvasViewModel` com dependências
mockadas.

### 4.3 `MainWindow` instancia 7 serviços manualmente

**Arquivo:** `MainWindow.axaml.cs`, linhas 501–524 (ver seção 1.2)

Não há como trocar `FileOperationsService` por um mock em testes.

### 4.4 `LocalizationService.Instance` — Service Locator anti-pattern

**Arquivo:** `CanvasViewModel.cs`, linha 281

```csharp
LocalizationService.Instance.PropertyChanged += _localizationPropertyChangedHandler;
```

`LocalizationService.Instance` é um Service Locator — uma das formas mais problemáticas de
acoplar dependências. É indistinguível de uma variável global. Dificulta testes e viola DIP.

**Solução:** injetar `ILocalizationService` via construtor.

---

## 5. ISP — Segregação de Interface

### 5.1 `IDbOrchestrator` — **Aceitável** com ressalva

**Arquivo:** `src/VisualSqlArchitect/Core/IDbOrchestrator.cs`, linhas 101–124

```csharp
public interface IDbOrchestrator : IAsyncDisposable
{
    DatabaseProvider Provider { get; }
    ConnectionConfig Config { get; }
    Task<ConnectionTestResult> TestConnectionAsync(...);
    Task<DatabaseSchema> GetSchemaAsync(...);
    Task<PreviewResult> ExecutePreviewAsync(...);
}
```

A interface é pequena (3 métodos + 2 propriedades) e todos os providers implementam todos os
métodos. Não é uma violação grave. Porém, contextos que só precisam testar conexão (health check)
são obrigados a depender de `GetSchemaAsync` e `ExecutePreviewAsync`.

**Refatoração opcional:**

```csharp
public interface IConnectionTester
{
    Task<ConnectionTestResult> TestConnectionAsync(CancellationToken ct = default);
}

public interface ISchemaProvider
{
    Task<DatabaseSchema> GetSchemaAsync(CancellationToken ct = default);
}

public interface IQueryExecutor
{
    Task<PreviewResult> ExecutePreviewAsync(string sql, int maxRows, CancellationToken ct = default);
}

// IDbOrchestrator continua como composição dos três
public interface IDbOrchestrator : IConnectionTester, ISchemaProvider, IQueryExecutor, IAsyncDisposable
{
    DatabaseProvider Provider { get; }
    ConnectionConfig Config { get; }
}
```

---

## 6. 12-Factor App

### Factor I — Codebase ✓

Um único repositório git, dois projetos (core + UI), uma suite de testes. Conforme.

### Factor III — Configuração ✗ (6 violações)

**Regra:** configuração que varia entre deploys deve estar no ambiente, não no código.

**Violação #1 — Nome da aplicação hardcoded em 6+ locais:**

| Arquivo | Linha | Valor hardcoded |
|---|---|---|
| `AppSettingsStore.cs` | 23 | `"VisualSqlArchitect"` |
| `FlowVersionStore.cs` | — | `"VisualSqlArchitect"` |
| `SnippetStore.cs` | 31 | `"VisualSqlArchitect"` |
| `CredentialProtector.cs` | — | `"VisualSqlArchitect"` |
| `RecentFilesStore.cs` | — | `"VisualSqlArchitect"` |
| `MainWindow.axaml.cs` | 73, 170 | `"Visual SQL Architect"` |

Se o nome do produto mudar (ex.: rebranding), são 6+ arquivos para editar.

**Solução:** centralizar em `AppConstants.cs` (já existe) e garantir que **todos** os stores
referenciem a constante:

```csharp
public static class AppConstants
{
    public const string AppName = "VisualSqlArchitect";
    public const string AppDisplayName = "Visual SQL Architect";
    public static string DataFolder =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            AppName
        );
}
```

**Violação #2 — Timeout hardcoded em `ConnectionConfig`:**

```csharp
public record ConnectionConfig(
    // ...
    int TimeoutSeconds = 30,  // hardcoded default
    // ...
)
```

O timeout padrão deveria ser configurável via arquivo de configuração, não baked-in no record.

### Factor IV — Backing Services ✓ (parcialmente)

Conexões de banco são tratadas como recursos conectáveis via `IDbOrchestrator`. A `ConnectionConfig`
é passada externamente. Conforme na teoria, mas a fábrica (`DbOrchestratorFactory`) não tem
ponto de extensão para registrar providers externos (ver seção 3.2).

### Factor XI — Logs ✗ (3 violações críticas)

**Regra:** tratar logs como fluxo de eventos. Nenhuma exceção deve ser engolida silenciosamente.

**Violação #1 — `AppSettingsStore.Load()` engole qualquer exceção:**

**Arquivo:** `src/VisualSqlArchitect.UI/Services/Settings/AppSettingsStore.cs`, linhas 27–41

```csharp
public static AppSettings Load()
{
    try
    {
        // lê arquivo de configurações
    }
    catch   // ← captura qualquer exceção, sem tipo, sem log
    {
        return new AppSettings();
    }
}
```

Se o arquivo de configurações estiver corrompido, o usuário perde todas as suas preferências sem
nenhuma notificação. Não há log, não há mensagem ao usuário.

**Violação #2 — `AppSettingsStore.Save()` descarta erros silenciosamente:**

```csharp
public static void Save(AppSettings settings)
{
    try
    {
        // salva arquivo
    }
    catch
    {
        // best effort  ← comentário que normaliza falha silenciosa
    }
}
```

"Best effort" não é uma política de erros — é ausência de política. O usuário muda o tema escuro,
um `IOException` é lançado (pasta sem permissão de escrita), nada acontece, a preferência é
perdida. Próxima abertura: tema padrão de volta.

**Violação #3 — `StartMenuViewModel.BuildSnapshotSummary()` engole parsing de JSON:**

**Arquivo:** `StartMenuViewModel.cs`, linhas 281–305

```csharp
private static string BuildSnapshotSummary(string filePath)
{
    try
    {
        // parse do JSON do arquivo
        return $"{nodes} nos • {conns} conexoes";
    }
    catch
    {
        // ← sem log
    }

    return "Snapshot indisponivel";
}
```

**Padrão correto:**

```csharp
catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
{
    _logger.LogWarning(ex, "Could not read snapshot summary from {FilePath}", filePath);
    return "Snapshot indisponível";
}
// exceções inesperadas propagam normalmente
```

### Factor XII — Admin Processes ✗

Não há mecanismo de migração de dados do usuário (arquivos `.vsa` de versões antigas). A migração
de v3→v4 do `CanvasSerializer` é corretamente versionada, mas não há processo de verificação/
migração automática ao iniciar o app.

---

## 7. Convenções de Código C#

### 7.1 Nomes incorretos em strings de UI

**Arquivo:** `MainWindow.axaml.cs`, linhas 132–171

```csharp
"Historico de arquivos"   // ← falta acento: "Histórico"
"Voltar para inicio"      // ← falta acento: "início"
"nos"                     // ← StartMenuViewModel.cs, "nós"
"conexoes"                // ← "conexões"
```

Strings em Português com acentuação incorreta em código de produção.

### 7.2 Inicialização com `null!` (null-forgiving sem garantia)

**Arquivo:** `CanvasViewModel.cs`, linhas 52–57

```csharp
public BenchmarkViewModel Benchmark     { get; private set; } = null!;
public ExplainPlanViewModel ExplainPlan { get; private set; } = null!;
public SqlImporterViewModel SqlImporter { get; private set; } = null!;
public FlowVersionOverlayViewModel FlowVersions { get; private set; } = null!;
public FileVersionHistoryViewModel FileHistory  { get; private set; } = null!;
public SidebarViewModel Sidebar         { get; private set; } = null!;
```

O operador `null!` suprime o aviso do compilador, mas não elimina o risco de
`NullReferenceException` em runtime. Seis propriedades nulas no momento da construção indicam
inicialização particionada — sintoma de acoplamento temporal. Quem garante que `Sidebar` é
inicializado antes de ser acessado?

### 7.3 Mistura de idiomas (Português e Inglês) no código

O projeto usa Inglês no código de negócio (correto) e Português nas strings de UI (aceitável
se intencional). O problema é a mistura dentro do mesmo arquivo e inconsistência na acentuação.

**Exemplos de inconsistência:**

```csharp
// Comentário em Inglês no NodeDefinition.cs:
/// <summary>Counts all rows</summary>

// Descrição em Português no mesmo arquivo:
"CAST explícito de coluna"
"Desempacota ColumnRef para expressão escalar"
"JOIN visual com dois RowSet + condição booleana"
"WHERE integrado ao RowSet"
"GROUP BY integrado ao RowSet"
```

Convenção de uma linha: todas as `Description` nos `NodeDefinition` em Inglês (já são maioria),
e strings de UI do usuário final em Português — separação clara.

### 7.4 Modelo de dados dentro do code-behind da View

**Arquivo:** `MainWindow.axaml.cs`, linhas 25–31

```csharp
private sealed class QueryTabState
{
    public required string FallbackTitle { get; init; }
    public string? SnapshotJson { get; set; }
    public string? CurrentFilePath { get; set; }
    public bool IsDirty { get; set; }
}
```

Uma classe que representa estado de sessão (`SnapshotJson`, `CurrentFilePath`, `IsDirty`) não
deveria existir dentro de um `Window`. Pertence ao ViewModel de sessão.

### 7.5 `SetOperation` de `NodeType` no `SelectOutput` — duplicação

**Arquivo:** `NodeDefinition.cs`, linhas 1507–1521

O nó `SelectOutput` tem parâmetros `set_operator` e `set_query`. Existe também um nó dedicado
`SetOperation`. Duas formas de fazer a mesma coisa.

---

## 8. Arquitetura em Camadas

### 8.1 Separação Core / UI — **Conforme** ✓

A camada Core (`VisualSqlArchitect`) não tem dependências de Avalonia nem da camada UI. O
`VisualSqlArchitect.csproj` referencia apenas:
- `SqlKata`
- `Microsoft.Extensions.DependencyInjection.Abstractions`
- `Microsoft.Extensions.Logging.Abstractions`

A direção de dependência é correta:

```
VisualSqlArchitect.UI  →  VisualSqlArchitect (Core)
VisualSqlArchitect.Tests → ambos
```

### 8.2 ViewModels chamando banco de dados diretamente — **Conforme** ✓

As operações de banco são corretamente delegadas a `IDbOrchestrator` via `ConnectionManagerViewModel`
e `DatabaseConnectionService`. Nenhum ViewModel faz acesso direto ao banco.

### 8.3 `DemoCatalog` no `NodeManager` — mistura de responsabilidades

**Arquivo:** `src/VisualSqlArchitect.UI/ViewModels/Canvas/NodeManager.cs`, linhas 31–93

```csharp
public static readonly IReadOnlyList<(
    string FullName,
    IReadOnlyList<(string Name, PinDataType Type)> Cols
)> DemoCatalog = [
    ("public.orders",    [("id", Number), ("customer_id", Number), ...]),
    ("public.customers", [("id", Number), ("name", Text), ...]),
    // ...
];
```

Dados de fixture de demonstração em um `Manager` de produção acoplam o sistema com dados de
teste. O `DemoCatalog` é acessado tanto pelo `QueryTemplateLibrary` (via
`CanvasViewModel.DemoCatalog`) quanto pelo `SearchMenuViewModel`. Deveria estar em uma classe
separada (ex.: `DemoCatalogFixture`, ou removido em favor de carregamento real do schema).

### 8.4 `CanvasViewModel.DemoCatalog` re-expõe dado estático do `NodeManager`

**Arquivo:** `CanvasViewModel.cs` (referenciado em `QueryTemplateLibrary.cs`)

```csharp
// Em QueryTemplateLibrary.cs:
CanvasViewModel.DemoCatalog.First(t => t.FullName == fullName)
```

O `QueryTemplateLibrary` acessa `CanvasViewModel.DemoCatalog` — o ViewModel expõe dados de
fixture como propriedade estática pública. Isso significa que o módulo de templates tem
dependência direta do ViewModel do canvas.

---

## 9. Tratamento de Erros

### 9.1 Catches sem tipo — **3 ocorrências críticas**

| Arquivo | Método | Impacto |
|---|---|---|
| `AppSettingsStore.cs` | `Load()` | Configuração perdida silenciosamente |
| `AppSettingsStore.cs` | `Save()` | Preferências não persistidas silenciosamente |
| `StartMenuViewModel.cs` | `BuildSnapshotSummary()` | Arquivo corrompido reportado incorretamente |

**Padrão correto:**

```csharp
// Ruim — captura qualquer coisa incluindo OutOfMemoryException
catch { return new AppSettings(); }

// Correto — captura apenas o que se espera e propaga o resto
catch (Exception ex) when (ex is IOException or JsonException)
{
    logger.LogWarning(ex, "Settings file unreadable, using defaults");
    return new AppSettings();
}
```

### 9.2 Exceções expostas apenas ao usuário, sem log estruturado

**Arquivo:** `src/VisualSqlArchitect.UI/Services/Session/FileOperationsService.cs`

```csharp
catch (Exception ex)
{
    _vm.DataPreview.ShowError($"Save failed: {ex.Message}", ex);
}
```

O erro é exibido ao usuário (bom), mas não é registrado no log estruturado (ruim). Em produção,
não há como diagnosticar falhas de salvamento reportadas por usuários.

### 9.3 Nenhuma política de retry ou circuit breaker

Operações de banco de dados (`TestConnectionAsync`, `ExecutePreviewAsync`) não têm política de
retry. Uma falha transiente de rede causa uma mensagem de erro imediata sem tentativa de reconexão.
Para uma ferramenta de desenvolvimento conectada a bancos remotos, isso é perceptível.

---

## 10. Cobertura de Testes

### 10.0 Atualização pós-Sprint 7 (2026-04-01)

Após a execução das Sprints 5, 6 e 7, os principais riscos de testabilidade e fluxo crítico
foram reduzidos com testes automatizados adicionais.

**Medição de cobertura focada (run em 2026-04-01):**

| Arquivo | Cobertura de statements |
|---|---|
| `CanvasViewModel.cs` | 74.2% (621/837) |
| `NodeGraphCompiler.cs` | 63.6% (103/162) |
| `QueryGeneratorService.cs` | 87.4% (366/419) |
| `QueryTemplateLibrary.cs` | 100.0% (876/876) |

Resultado: o caminho crítico (`template -> graph -> compiler -> SQL`) agora está coberto por
teste de integração dedicado e por testes unitários complementares.

### 10.1 Componentes críticos sem testes

| Componente | Linhas | Testado? | Risco |
|---|---|---|---|
| `CanvasViewModel` | 1.294 | ✗ | **Crítico** |
| `MainWindow.axaml.cs` | 1.134 | ✗ | Alto |
| `ConnectionManagerViewModel` | ~865 | ✗ | Alto |
| `PropertyPanelViewModel` | ~620 | ✗ | Médio |
| `FileOperationsService` | ~400 | ✗ | Alto |
| `SessionManagementService` | ~300 | ✗ | Médio |
| `KeyboardInputHandler` | ~200 | ✗ | Baixo |

### 10.2 O que existe

A suite de testes cobre bem:
- Compilação de nós (NodeCompilers)
- Emissão de SQL por provider (expressões)
- Serialização/deserialização de canvas (CanvasSerializer)
- Definições de nós (NodeDefinitionRegistry)
- Testes de controles Avalonia (rendering)
- Testes de performance (BenchmarkTests)

### 10.3 Por que `CanvasViewModel` não é testável hoje

**Status atual:** parcialmente resolvido na Sprint 5.

`CanvasViewModel` agora possui construtor com dependências injetáveis e contratos de manager
(`INodeManager`, `IPinManager`, `ISelectionManager`), além de suporte via `ILocalizationService`.
Isso permite evolução de testes sem depender exclusivamente de singleton/global state.

1. Construtor sem parâmetros (`public CanvasViewModel()`) instancia tudo com `new` — sem mock
2. Depende de `LocalizationService.Instance` (Service Locator)
3. Usa tipos Avalonia concretos (`Point`, `ObservableCollection`)

**Para torná-lo testável:**

```csharp
// Extrair interfaces para os managers:
public interface INodeManager { /* ... */ }
public interface IPinManager  { /* ... */ }

// Injetar via construtor:
public CanvasViewModel(
    INodeManager nodeManager,
    IPinManager pinManager,
    ILocalizationService localization
    // ...
) { ... }
```

### 10.4 Ausência de testes de integração para fluxo completo

**Status atual:** resolvido na Sprint 7.

Foi adicionado teste de integração end-to-end cobrindo:
`Template JOIN de três tabelas -> serialização NodeGraph -> NodeGraphCompiler -> QueryGeneratorService -> validação SQL por provider`.

Não existe teste que cubra o fluxo completo:
`Template carregado → nós adicionados → conexões criadas → SQL gerado → validado`

Este é o fluxo principal do produto. Um teste end-to-end em xUnit com o compilador real
(`NodeGraphCompiler → QueryGeneratorService`) seria de alto valor.

---

## 11. Anti-Patterns Adicionais

### 11.1 Service Locator via propriedade estática

**Arquivo:** `CanvasViewModel.cs`, linha 281

```csharp
LocalizationService.Instance.PropertyChanged += _localizationPropertyChangedHandler;
```

`LocalizationService.Instance` é equivalente a uma variável global. Viola DIP e torna a classe
impossível de testar sem afetar o estado global. Instância singleton deveria ser injetada.

### 11.2 `null!` como valor inicial de propriedades públicas

**Arquivo:** `CanvasViewModel.cs`, linhas 52–57 (6 ocorrências, ver seção 7.2)

Suprime avisos de nullable sem eliminar o risco. Indica acoplamento temporal — a classe precisa
ser inicializada em etapas.

### 11.3 `DataContext = new ShellViewModel()` no construtor da janela

**Arquivo:** `MainWindow.axaml.cs`, linha 68

```csharp
public MainWindow()
{
    InitializeComponent();
    DataContext = new ShellViewModel();  // ← ViewModel criado no code-behind
    // ...
}
```

Em um design MVVM adequado, o ViewModel é criado externamente e passado à View. Isso impede
testar a View com um ViewModel diferente.

### 11.4 Record `CteEditorSession` definido dentro de `CanvasViewModel`

**Arquivo:** `CanvasViewModel.cs`, linhas 28–33

```csharp
public sealed class CanvasViewModel : ViewModelBase, IDisposable
{
    private sealed record CteEditorSession(
        string ParentCanvasJson,
        string ParentCteNodeId,
        bool ParentWasDirty,
        string CteDisplayName
    );
```

Um record de sessão do CTE Editor definido como tipo privado dentro do ViewModel principal.
Isso indica que a funcionalidade de CTE Editor pertence à `CanvasViewModel`, quando deveria
ter seu próprio ViewModel.

### 11.5 `DemoCatalog` acessado de dentro do template library via `CanvasViewModel`

**Arquivo:** `QueryTemplateLibrary.cs`, linha 31

```csharp
CanvasViewModel.DemoCatalog.First(t => t.FullName == fullName);
```

`QueryTemplateLibrary` tem dependência de `CanvasViewModel` apenas para acessar dados de
demonstração estáticos. Violação de acoplamento — o catálogo de templates depende do ViewModel
do canvas.

---

## 12. Ranking de Severidade

| # | Achado | Severidade | Categoria |
|---|---|---|---|
| 1 | `CanvasViewModel` — God Class (1.294 linhas, 16 VMs filho, 5 managers) | 🔴 Crítico | SRP + DIP |
| 2 | `MainWindow.axaml.cs` — God Code-Behind (1.134 linhas, 7 serviços, UI imperativa) | 🔴 Crítico | SRP + MVVM |
| 3 | `UpdateSchemaTree()` constrói TreeView em C# em vez de data binding | 🔴 Crítico | MVVM |
| 4 | `AppSettingsStore.Load/Save()` engolem exceções silenciosamente | 🔴 Crítico | Error Handling |
| 5 | Ausência de DI na camada UI — `new` em todo lugar | 🔴 Crítico | DIP |
| 6 | `QueryTabState` e lógica de tab no code-behind da janela | 🟠 Alto | SRP + MVVM |
| 7 | `CanvasViewModel` não é testável (construtor sem parâmetros, Service Locator) | 🟠 Alto | Testabilidade |
| 8 | Switches em `ConnectionConfig`, `DbOrchestratorFactory`, `QueryGeneratorService` | 🟠 Alto | OCP |
| 9 | `LocalizationService.Instance` — Service Locator anti-pattern | 🟠 Alto | DIP |
| 10 | Nome do app hardcoded em 6+ arquivos | 🟡 Médio | Factor III |
| 11 | Nenhum teste de integração para fluxo completo | 🟡 Médio | Testabilidade |
| 12 | `null!` em 6 propriedades públicas do CanvasViewModel | 🟡 Médio | Null Safety |
| 13 | Strings de UI construídas em C# com acentuação incorreta | 🟡 Médio | Convenção |
| 14 | `DemoCatalog` em `NodeManager` (fixture acoplada ao produto) | 🟡 Médio | Arquitetura |
| 15 | `CteEditorSession` record privado dentro do CanvasViewModel | 🟡 Médio | SRP |
| 16 | Sem política de retry/circuit breaker em operações de banco | 🟢 Baixo | Resiliência |
| 17 | Sem testes de performance automáticos no CI | 🟢 Baixo | CI/CD |

---

## 13. Plano de Remediação

Os itens abaixo são ordenados por impacto vs. esforço. Cada um é independente e pode ser
implementado sem bloquear os demais.

### Sprint 1 — Erros críticos sem refatoração estrutural

**Status:** ✅ **Concluída em 2026-04-01**

- [x] 1.1 Corrigir tratamento de exceções no `AppSettingsStore`
- [x] 1.2 Centralizar nome/pasta do app em `AppConstants`
- [x] 1.3 Corrigir acentuação nas strings de UI mapeadas

**1.1 Corrigir tratamento de exceções no `AppSettingsStore`**

```csharp
// Antes:
catch { return new AppSettings(); }

// Depois:
catch (Exception ex) when (ex is IOException or JsonException or InvalidOperationException)
{
    _logger.LogWarning(ex, "Failed to load settings from {Path}, using defaults", SettingsPath);
    return new AppSettings();
}
```

**1.2 Centralizar nome do app em `AppConstants`**

Criar `AppConstants.AppDataFolder` e substituir todas as 6 ocorrências de
`Path.Combine(Environment.GetFolderPath(...), "VisualSqlArchitect", ...)` por referência à
constante.

**1.3 Corrigir acentuação nas strings de UI**

`"Historico"` → `"Histórico"`, `"inicio"` → `"início"`, `"nos"` → `"nós"`,
`"conexoes"` → `"conexões"`.

---

### Sprint 2 — Migrar `UpdateSchemaTree` para MVVM correto

**Status:** ✅ **Concluída em 2026-04-01**

- [x] Removida a montagem imperativa da TreeView em `MainWindow.axaml.cs`
- [x] Removido o wiring de atualização de schema no code-behind
- [x] Mantido fluxo por binding MVVM via `SchemaViewModel`/`SchemaControl`

Criar `SchemaTreeNodeViewModel` (com filhos `TableTreeNodeViewModel` / `ColumnTreeNodeViewModel`)
a partir do `DbMetadata` existente. Adicionar ao `SidebarViewModel`. Remover os 82 linhas de
`UpdateSchemaTree()` em `MainWindow.axaml.cs` e substituir por `HierarchicalDataTemplate` em AXAML.

**Impacto:** elimina a violação MVVM mais grave do projeto, sem quebrar funcionalidade.

---

### Sprint 3 — Extrair `TabManagerViewModel` do `MainWindow`

**Status:** ✅ **Concluída em 2026-04-01**

- [x] Estado de abas movido do code-behind para `QueryTabManagerViewModel`
- [x] `ShellViewModel` passou a expor `QueryTabs`
- [x] `MainWindow.axaml.cs` passou a consumir o estado de abas via ViewModel

Mover `QueryTabState`, `_queryTabs`, `_activeQueryTabIndex` para um `TabManagerViewModel`.
`MainWindow` passa a se ligar ao ViewModel via binding, sem manter estado próprio.

---

### Sprint 4 — Registrar DI para serviços da UI

**Status:** ✅ **Concluída em 2026-04-01**

- [x] `App.axaml.cs` passou a montar `IServiceProvider` e resolver `MainWindow` via DI
- [x] `MainWindow.axaml.cs` passou a receber `IServiceProvider`, `ShellViewModel` e `ThemeJsonSettingsService` por construtor
- [x] Inicialização de serviços de janela migrou para `ActivatorUtilities.CreateInstance(...)`
- [x] `VisualSqlArchitect.UI.csproj` atualizado com pacote de DI da Microsoft

Introduzir `IServiceCollection` / `IServiceProvider` em `App.axaml.cs`. Registrar os 7 serviços
de `InitializeServices()`. Injetar no `MainWindow` via construtor (usando `AppBuilderExtensions`
do Avalonia para DI).

---

### Sprint 5 — Tornar `CanvasViewModel` testável

**Status:** ✅ **Concluída em 2026-04-01**

- [x] Extraídas interfaces `INodeManager`, `IPinManager` e `ISelectionManager`
- [x] `CanvasViewModel` passou a depender das interfaces e ganhou construtor com parâmetros
- [x] Construtor sem parâmetros mantido como factory com defaults
- [x] `CanvasViewModel` passou a depender de `ILocalizationService` em vez de uso direto de singleton
- [x] Adicionado primeiro teste unitário de `CanvasViewModel.LoadTemplate()`

1. Extrair `INodeManager`, `IPinManager`, `ISelectionManager` como interfaces
2. Injetar `ILocalizationService` em vez de `LocalizationService.Instance`
3. Adicionar construtor com parâmetros (manter construtor sem parâmetros como factory)
4. Escrever primeiro teste unitário para `CanvasViewModel.LoadTemplate()`

---

### Sprint 6 — Switches OCP em factories de provider

**Status:** ✅ **Concluída em 2026-04-01**

- [x] `DbOrchestratorFactory` convertido para registry extensível (provider -> factory)
- [x] Adicionado suporte de registro/override por provider em `DbOrchestratorFactory.Register(...)`
- [x] Lógica de hints movida para `ISqlDialect.ApplyQueryHints(...)`
- [x] `QueryGeneratorService` passou a delegar hints para o dialeto em vez de switch local
- [x] `ProviderRegistry` passou a registrar SQLite por padrão
- [x] Testes adicionados para factory extensível e hints por dialeto

Converter `DbOrchestratorFactory` para registry extensível. Mover lógica de hints para
`ISqlDialect.ApplyQueryHints()`. Resultado: adicionar um novo provider = criar uma classe
nova, sem tocar nas existentes.

---

### Sprint 7 — Teste de integração end-to-end

**Status:** ✅ **Concluída em 2026-04-01**

- [x] Teste de integração criado para o template `JOIN de três tabelas`
- [x] Fluxo validado com serialização e desserialização de `NodeGraph`
- [x] Pipeline validado: `NodeGraphCompiler` + `QueryGeneratorService`
- [x] SQL validado para Postgres, MySQL, SQL Server e SQLite

Escrever um teste xUnit que:
1. Carrega o template "JOIN de três tabelas"
2. Serializa o `NodeGraph`
3. Passa pelo `NodeGraphCompiler`
4. Passa pelo `QueryGeneratorService`
5. Valida o SQL gerado para cada provider

Este único teste teria cobertura sobre o caminho crítico inteiro do produto.

---

### Sprint 8 — Cobertura incremental do compilador

**Status:** ✅ **Concluída em 2026-04-01**

- [x] Testes de regressão adicionados para ramos críticos de `NodeGraphCompiler`
- [x] Cobertos cenários de erro: construtor com argumentos nulos e ciclo no grafo
- [x] Coberto cenário de erro de `TableSource` sem `TableFullName`
- [x] Coberto preenchimento de coleções compiladas (`WHERE`, `HAVING`, `QUALIFY`, `ORDER BY`, `GROUP BY`)

Objetivo: reduzir risco em ramos de validação/erro e consolidar cobertura do núcleo de compilação sem alterar comportamento de runtime.

---

## Referências

- [CanvasViewModel.cs](../src/VisualSqlArchitect.UI/ViewModels/CanvasViewModel.cs) — God Class principal
- [MainWindow.axaml.cs](../src/VisualSqlArchitect.UI/Views/Shell/MainWindow.axaml.cs) — Code-behind excessivo
- [QueryGeneratorService.cs](../src/VisualSqlArchitect/QueryEngine/QueryGeneratorService.cs) — Switches OCP
- [NodeCompilerFactory.cs](../src/VisualSqlArchitect/Nodes/Compilers/NodeCompilerFactory.cs) — **Exemplo positivo** de Strategy + OCP
- [AppSettingsStore.cs](../src/VisualSqlArchitect.UI/Services/Settings/AppSettingsStore.cs) — Exceções engolidas
- [NodeManager.cs](../src/VisualSqlArchitect.UI/ViewModels/Canvas/NodeManager.cs) — DemoCatalog acoplado
- [ServiceRegistration.cs](../src/VisualSqlArchitect/ServiceRegistration.cs) — Factory com switch OCP
- [IDbOrchestrator.cs](../src/VisualSqlArchitect/Core/IDbOrchestrator.cs) — Switch em BuildConnectionString
