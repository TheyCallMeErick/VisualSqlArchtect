# Roadmap: Módulo Visual DDL

> Módulo de criação e alteração de schema baseado em nós, com canvas dedicado acessível por
> toggle no header. O módulo gera `CREATE TABLE`, `ALTER TABLE`, `CREATE INDEX` e statements
> auxiliares com suporte completo a SQL Server, PostgreSQL, MySQL e SQLite.
>
> Leia este documento junto com [PIN_TYPES_REFERENCE.md](PIN_TYPES_REFERENCE.md) e
> [TYPE_SYSTEM_ROADMAP.md](TYPE_SYSTEM_ROADMAP.md), que definem o sistema visual de pinos
> sobre o qual o módulo DDL é construído.

---

## 1. Visão Geral e Filosofia

### 1.1 O que é o módulo DDL

O módulo DDL é uma segunda **camada de canvas** dentro da aplicação existente. Enquanto o
canvas atual (modo Query) gera `SELECT`, o canvas DDL gera statements de definição de schema.
Ambos os modos compartilham:

- O controle `InfiniteCanvas` — pan, zoom, rubber-band, snap-to-grid
- A hierarquia `NodeViewModel` / `PinViewModel` / `ConnectionViewModel`
- O `SelectionManager`, `NodeLayoutManager` e `UndoRedoStack`
- O `DbMetadata` do banco conectado (read-only, para referenciar tabelas existentes)
- O `CanvasSerializer` (estrutura base de `SavedCanvas`)

O que é **exclusivo** do módulo DDL:

- Cinco novos `PinDataType`s com gramática visual própria
- Quinze novos `NodeType`s na categoria `NodeCategory.Ddl`
- O compilador `DdlGraphCompiler` e a hierarquia `IDdlExpression`
- O `DdlGeneratorService` com emissão multi-dialeto
- A barra de preview `LiveDdlBarViewModel`
- O importador `DdlSchemaImporter` (schema → canvas)
- O `DdlCanvasViewModel` (equivalente ao `CanvasViewModel`)

### 1.2 Princípios de design

1. **Mesma gramática visual, vocabulário novo** — os pinos DDL seguem as regras de forma +
   cor do sistema de tipos existente (ver seção 3). O usuário que conhece o modo Query
   reconhece a linguagem visual imediatamente.

2. **Grafo como fonte de verdade** — o DDL gerado é sempre derivado do grafo. O usuário
   nunca edita SQL diretamente; ele edita nós e parâmetros.

3. **Não-destrutivo por padrão** — o canvas DDL não executa nada automaticamente. O botão
   "Executar DDL" exige confirmação explícita com preview do SQL antes da execução.

4. **Dialeto-agnóstico até o momento de emissão** — o grafo é armazenado sem dialeto. O
   `DdlGeneratorService` recebe o `DatabaseProvider` e emite o SQL adequado.

5. **Import ≠ execução reversa** — o importador lê o `DbMetadata` para popular o canvas,
   mas o grafo resultante representa a *intenção* atual do usuário, não um diff do schema.

---

## 2. Navegação: Toggle Query ↔ DDL

### 2.1 Estado atual de navegação

Hoje o `ShellViewModel` controla a transição Start Menu ↔ Canvas via `IsStartVisible`.
Dentro do canvas, não há sub-navegação — há apenas um modo (query).

```
ShellViewModel
  ├── IsStartVisible : bool
  ├── StartMenu      : StartMenuViewModel
  └── Canvas         : CanvasViewModel?   ← único modo hoje
```

### 2.2 Estrutura proposta

```
ShellViewModel
  ├── IsStartVisible : bool
  ├── StartMenu      : StartMenuViewModel
  ├── QueryCanvas    : QueryCanvasViewModel?   ← renomear CanvasViewModel atual
  ├── DdlCanvas      : DdlCanvasViewModel?     ← novo
  └── ActiveMode     : AppMode                 ← enum: Start | Query | Ddl
```

```csharp
// src/VisualSqlArchitect.UI/ViewModels/Shell/ShellViewModel.cs  (modificar)
public enum AppMode { Start, Query, Ddl }

public AppMode ActiveMode { get; private set; } = AppMode.Start;

public void EnterQueryMode() { EnsureQueryCanvas(); ActiveMode = AppMode.Query; }
public void EnterDdlMode()   { EnsureDdlCanvas();   ActiveMode = AppMode.Ddl;   }
public void ReturnToStart()  {                       ActiveMode = AppMode.Start; }
```

O `MainWindow.axaml` usa um `ContentControl` cujo `DataTemplate` é selecionado por
`ActiveMode`. A transição é instantânea — ambos os canvases mantêm estado em memória.

### 2.3 Toggle no header

O toggle fica no header da janela principal, à direita do nome do arquivo:

```
┌─────────────────────────────────────────────────────────────────┐
│  VisualSqlArchitect   orders_report.vsa   [ Query │ DDL ]   ⚙  │
└─────────────────────────────────────────────────────────────────┘
```

Implementação: `SegmentedControl` (ou dois `ToggleButton` mutuamente exclusivos) bindado
em `ShellViewModel.ActiveMode`. O controle é desabilitado quando não há conexão ativa, pois
o canvas DDL requer schema para ser útil.

### 2.4 Persistência dos dois canvases no .vsa

O arquivo `.vsa` existente (gerenciado por `CanvasSerializer`) ganha um envelope:

```json
{
  "AppVersion": "1.2.0",
  "QueryCanvas": { /* SavedCanvas atual */ },
  "DdlCanvas":   { /* SavedCanvas DDL  */ }
}
```

Arquivos v1/v2/v3 existentes são lidos como `QueryCanvas` com `DdlCanvas: null` (migração
automática). O campo `CanvasType` dentro de cada `SavedCanvas` distingue os dois.

---

## 3. Novos Tipos de Pino DDL

Os pinos DDL formam uma família semântica coesa. Seguem as mesmas regras de forma/cor do
sistema atual (losangos = estrutural, círculos = escalar), mas com identidade própria para
o domínio de definição de schema.

### 3.1 Adições ao enum `PinDataType`

```csharp
// src/VisualSqlArchitect/Nodes/NodeDefinition.cs  (modificar)
public enum PinDataType
{
    // ── Existentes (query) ─────────────────────────────────
    Text, Integer, Decimal, Number, Boolean, DateTime, Json,
    ColumnRef, ColumnSet, RowSet, Expression,

    // ── Novos (DDL) ────────────────────────────────────────
    TableDef,     // Definição de uma tabela (nova ou referenciada)
    ColumnDef,    // Definição de uma coluna com todos os atributos
    Constraint,   // PK, FK, UNIQUE ou CHECK constraint
    IndexDef,     // Definição de um índice
    AlterOp,      // Uma operação ALTER TABLE atômica
}
```

### 3.2 Gramática visual dos novos tipos

Segue os mesmos princípios do documento [PIN_TYPES_REFERENCE.md](PIN_TYPES_REFERENCE.md):
forma = família semântica, cor = tipo específico.

| Tipo | Forma | Cor | Hex | Label | Estilo de fio |
|---|---|---|---|---|---|
| `TableDef` | Quadrado arredondado ▪ | Azul-aço | `#60A5FA` → `#2563EB` | `TBL` | Sólido, 2.5 px |
| `ColumnDef` | Círculo duplo ⊙ | Verde-musgo | `#4ADE80` → `#16A34A` | `COL` | Sólido, 2.0 px |
| `Constraint` | Losango sólido ◆ | Violeta | `#A78BFA` | `CON` | Traço médio `6 3`, 2.2 px |
| `IndexDef` | Triângulo ▲ | Cinza-azul | `#93C5FD` | `IDX` | Traço curto `4 4`, 1.8 px |
| `AlterOp` | Seta arredondada → | Âmbar-escuro | `#F59E0B` | `ALT` | Traço `8 3`, 2.2 px |

**Racionalização de cores:**

- `TableDef` usa azul-aço porque "tabela" é a unidade de mais alto nível — o azul contrasta
  com o rosa do `RowSet` query (que também representa tabelas, mas em runtime).
- `ColumnDef` usa verde porque "coluna" é análoga ao `ColumnRef` (laranja, query), mas com
  verde para indicar que é *definição*, não *referência a dado existente*.
- `Constraint` usa violeta — sem análogo no modo query, cor nova para indicar regra/restrição.
- `IndexDef` usa cinza-azul — acessório/auxiliar, menor hierarquia visual.
- `AlterOp` usa âmbar porque representa uma *operação com impacto* — cor de atenção
  (mesmo âmbar semântico do `Boolean`, mas escuro para diferenciação).

> **Regra de coexistência:** pinos DDL nunca aparecem no canvas Query e vice-versa. O
> `PinViewModel.CanAccept` bloqueia conexões entre pinos de modos diferentes.

### 3.3 Implementação visual

```csharp
// src/VisualSqlArchitect.UI/ViewModels/Canvas/PinViewModel.cs  (modificar)
// Adicionar cases no switch de PinColor e no switch de DashKind

public Color PinColor => DataType switch
{
    // ... existentes ...
    PinDataType.TableDef   => Color.Parse("#2563EB"),
    PinDataType.ColumnDef  => Color.Parse("#16A34A"),
    PinDataType.Constraint => Color.Parse("#A78BFA"),
    PinDataType.IndexDef   => Color.Parse("#93C5FD"),
    PinDataType.AlterOp    => Color.Parse("#F59E0B"),
    _ => Color.Parse("#6B7280"),
};

public WireDashKind DashKind => DataType switch
{
    // ... existentes ...
    PinDataType.Constraint => WireDashKind.MediumDash,   // novo dash kind
    PinDataType.IndexDef   => WireDashKind.ShortDash,    // novo dash kind
    PinDataType.AlterOp    => WireDashKind.LongDash,
    _                      => WireDashKind.Solid,
};

public string DataTypeLabel => DataType switch
{
    // ... existentes ...
    PinDataType.TableDef   => "TBL",
    PinDataType.ColumnDef  => "COL",
    PinDataType.Constraint => "CON",
    PinDataType.IndexDef   => "IDX",
    PinDataType.AlterOp    => "ALT",
    _                      => "?",
};
```

### 3.4 Regras de compatibilidade DDL

```
TableDef  ──── possui ────▶  ColumnDef  ──── pode ter ────▶  Constraint
                                                 └──────────────▶  AlterOp
TableDef  ──────────────────────────────────────▶  Constraint
TableDef  ──────────────────────────────────────▶  IndexDef
```

| De | Para | Permitido? |
|---|---|---|
| `TableDef` | `TableDef` | Sim (FK referencia outra tabela) |
| `ColumnDef` | qualquer DDL | Não (ColumnDef é folha, só sai de TableDef) |
| `Constraint` | `Constraint` | Não |
| Qualquer DDL | Qualquer query | Não |

---

## 4. Catálogo de Nós DDL

Todos os nós abaixo pertencem à nova categoria `NodeCategory.Ddl`. Eles **não aparecem**
no buscador de nós do canvas Query — o `SearchMenuViewModel` filtra por `CanvasMode`.

### 4.1 Nós de Definição de Estrutura

---

#### `TableDefinition`
**Papel:** Nó central do canvas DDL. Define uma tabela nova ou em processo de alteração.
Agrega colunas e constraints e emite um `TableDef`.

```
                   ┌──────────────────────────┐
                   │  ▪ Table Definition       │
                   │  schema.table_name        │
  col_1 ● ────────▷│ col_1  (ColumnDef)       │─────────▶ ▪ table (TableDef)
  col_2 ● ────────▷│ col_2  (ColumnDef)       │
  ...              │ ...                       │
  pk    ◆ ────────▷│ pk     (Constraint)       │
  fk_1  ◆ ────────▷│ fk_1   (Constraint)       │
  uq_1  ◆ ────────▷│ uq_1   (Constraint)       │
                   └──────────────────────────┘
```

**Pins:**

| Nome | Direção | Tipo | `IsRequired` | `AllowMultiple` |
|---|---|---|---|---|
| `column` | Input | `ColumnDef` | false | true |
| `constraint` | Input | `Constraint` | false | true |
| `table` | Output | `TableDef` | — | — |

**Parâmetros (NodeParameter):**

| Nome | Kind | Default | Descrição |
|---|---|---|---|
| `TableName` | `Text` | `"new_table"` | Nome da tabela |
| `Schema` | `Text` | `""` | Schema (blank = default do provider) |
| `IfNotExists` | `Boolean` | `"false"` | Emite `IF NOT EXISTS` |
| `Temporary` | `Boolean` | `"false"` | Tabela temporária (`#` no SQL Server, `TEMP` no Postgres/MySQL) |

**Renderização especial:** o nó `TableDefinition` usa um template visual alternativo (ERD
card) no modo DDL. Ao invés de apenas listar pins, ele exibe as colunas como linhas
expandíveis com ícones de tipo de dado. Ver seção 6.2.

---

#### `ColumnDefinition`
**Papel:** Define uma coluna — nome, tipo, nullabilidade, identity e default.

```
                   ┌─────────────────────────┐
  default ─────────▷│ default (Expression)    │─────────▶ ⊙ col (ColumnDef)
                   │                         │
                   │  [Nome]   [Tipo]         │
                   │  [Nullable] [Identity]   │
                   └─────────────────────────┘
```

**Pins:**

| Nome | Direção | Tipo | `IsRequired` | Descrição |
|---|---|---|---|---|
| `default_value` | Input | `Expression` | false | Expressão de valor default |
| `col` | Output | `ColumnDef` | — | Definição da coluna |

**Parâmetros:**

| Nome | Kind | Default | Descrição |
|---|---|---|---|
| `ColumnName` | `Text` | `"column_name"` | Nome da coluna |
| `DataType` | `Enum` | `"INT"` | Tipo de dado (ver seção 4.5) |
| `Length` | `Number` | `""` | Comprimento (para VARCHAR, CHAR, NVARCHAR) |
| `Precision` | `Number` | `""` | Precisão total (DECIMAL/NUMERIC) |
| `Scale` | `Number` | `""` | Casas decimais (DECIMAL/NUMERIC) |
| `IsNullable` | `Boolean` | `"true"` | NULL ou NOT NULL |
| `IsIdentity` | `Boolean` | `"false"` | IDENTITY/SERIAL/AUTO_INCREMENT |
| `IdentitySeed` | `Number` | `"1"` | Valor inicial da sequência (SQL Server) |
| `IdentityIncrement` | `Number` | `"1"` | Incremento da sequência (SQL Server) |
| `Collation` | `Text` | `""` | Collation override (leave blank for default) |
| `ComputedExpression` | `Text` | `""` | Coluna computada — emite `AS (expr)` |
| `IsPersisted` | `Boolean` | `"false"` | PERSISTED para colunas computadas (SQL Server) |

> **Nota de dialeto para Identity:**
> - SQL Server: `INT IDENTITY(1,1) NOT NULL`
> - PostgreSQL: `SERIAL` (atalho) ou `INT GENERATED ALWAYS AS IDENTITY`
> - MySQL: `INT AUTO_INCREMENT`
> - SQLite: `INTEGER PRIMARY KEY` implica autoincrement; `AUTOINCREMENT` explícito é opcional

---

#### `SchemaReference`
**Papel:** Referencia uma tabela **já existente** no banco (lida do `DbMetadata`). Emite
um `TableDef` apontando para uma tabela real — usado principalmente como alvo de `ForeignKeyConstraint`.

```
                   ┌─────────────────────────┐
                   │  ▪ Schema Reference      │─────────▶ ▪ table (TableDef)
                   │  [dbo.customers]         │
                   └─────────────────────────┘
```

**Pins:** apenas `table` (Output, `TableDef`).

**Parâmetros:**

| Nome | Kind | Default | Descrição |
|---|---|---|---|
| `TableFullName` | `Text` | `""` | Nome completo da tabela (schema.table) |

Na UI, ao clicar no nó, o painel de propriedades exibe um dropdown populado com todas as
tabelas do `DbMetadata` conectado. O nó renderiza as colunas da tabela referenciada como
pinos de output `ColumnDef` (read-only) para que possam ser conectados a FKs.

---

### 4.2 Nós de Constraint

---

#### `PrimaryKeyConstraint`
**Papel:** Define a PK de uma tabela. Aceita uma ou mais colunas (PK composta).

```
  col_1 ⊙ ────────▷│ PK Constraint          │─────────▶ ◆ pk (Constraint)
  col_2 ⊙ ────────▷│ [ConstraintName]        │
```

**Pins:**

| Nome | Direção | Tipo | `IsRequired` | `AllowMultiple` |
|---|---|---|---|---|
| `column` | Input | `ColumnDef` | true | true |
| `pk` | Output | `Constraint` | — | — |

**Parâmetros:**

| Nome | Kind | Default | Descrição |
|---|---|---|---|
| `ConstraintName` | `Text` | `""` | Nome da constraint (gerado se em branco) |
| `IsClustered` | `Boolean` | `"true"` | CLUSTERED vs NONCLUSTERED (SQL Server) |

---

#### `ForeignKeyConstraint`
**Papel:** Define uma FK entre uma coluna local e uma coluna de tabela referenciada.
Suporta FKs compostas e ações referenciais.

```
  child_col  ⊙ ────────▷│ FK Constraint          │─────────▶ ◆ fk (Constraint)
  parent_col ⊙ ────────▷│ [ConstraintName]        │
                         │ [OnDelete] [OnUpdate]   │
```

**Pins:**

| Nome | Direção | Tipo | `IsRequired` | `AllowMultiple` | Descrição |
|---|---|---|---|---|---|
| `child_column` | Input | `ColumnDef` | true | true | Coluna(s) da tabela filha |
| `parent_column` | Input | `ColumnDef` | true | true | Coluna(s) referenciada(s) |
| `fk` | Output | `Constraint` | — | — | — |

**Parâmetros:**

| Nome | Kind | Valores | Default | Descrição |
|---|---|---|---|---|
| `ConstraintName` | `Text` | — | `""` | Gerado automaticamente se em branco |
| `OnDelete` | `Enum` | `NO ACTION`, `CASCADE`, `SET NULL`, `SET DEFAULT`, `RESTRICT` | `NO ACTION` | Ação referencial no delete |
| `OnUpdate` | `Enum` | idem | `NO ACTION` | Ação referencial no update |

> **Regra de validação:** o número de colunas em `child_column` deve ser igual ao de
> `parent_column`. O compilador valida por posição de conexão.

---

#### `UniqueConstraint`
**Papel:** Define uma constraint UNIQUE em uma ou mais colunas.

**Pins:**

| Nome | Direção | Tipo | `IsRequired` | `AllowMultiple` |
|---|---|---|---|---|
| `column` | Input | `ColumnDef` | true | true |
| `uq` | Output | `Constraint` | — | — |

**Parâmetros:**

| Nome | Kind | Default | Descrição |
|---|---|---|---|
| `ConstraintName` | `Text` | `""` | Nome da constraint |
| `IsFiltered` | `Boolean` | `"false"` | Filtered unique index (SQL Server: `WHERE col IS NOT NULL`) |
| `FilterExpression` | `Text` | `""` | Expressão do filtro (apenas SQL Server) |

---

#### `CheckConstraint`
**Papel:** Define uma constraint CHECK com expressão textual livre.

**Pins:**

| Nome | Direção | Tipo | `IsRequired` |
|---|---|---|---|
| `ck` | Output | `Constraint` | — |

**Parâmetros:**

| Nome | Kind | Default | Descrição |
|---|---|---|---|
| `ConstraintName` | `Text` | `""` | Nome da constraint |
| `Expression` | `Text` | `""` | Expressão booleana (ex: `age > 0 AND age < 150`) |

---

#### `DefaultConstraint`
**Papel:** Define um DEFAULT nomeado para uma coluna. Em SQL Server, defaults podem ter
nome; em outros providers, o `DEFAULT` é inline na definição da coluna.

**Pins:**

| Nome | Direção | Tipo | `IsRequired` |
|---|---|---|---|
| `column` | Input | `ColumnDef` | true |
| `dc` | Output | `Constraint` | — |

**Parâmetros:**

| Nome | Kind | Default | Descrição |
|---|---|---|---|
| `ConstraintName` | `Text` | `""` | Nome (SQL Server only; outros providers ignoram) |
| `DefaultValue` | `Text` | `""` | Valor ou expressão default (ex: `GETDATE()`, `NOW()`, `0`) |

---

### 4.3 Nós de Índice

---

#### `IndexDefinition`
**Papel:** Gera um `CREATE INDEX` separado. Suporta índices simples, únicos, compostos e
(parcialmente) filtered.

```
  table  ▪ ─────────▷│ Index Definition       │─────────▶ ▲ idx (IndexDef)
  col_1  ⊙ ─────────▷│ [IndexName]            │
  col_2  ⊙ ─────────▷│ [IsUnique] [IsClustered]│
```

**Pins:**

| Nome | Direção | Tipo | `IsRequired` | `AllowMultiple` |
|---|---|---|---|---|
| `table` | Input | `TableDef` | true | false |
| `column` | Input | `ColumnDef` | true | true |
| `include_column` | Input | `ColumnDef` | false | true |
| `idx` | Output | `IndexDef` | — | — |

**Parâmetros:**

| Nome | Kind | Default | Descrição |
|---|---|---|---|
| `IndexName` | `Text` | `""` | Nome do índice |
| `IsUnique` | `Boolean` | `"false"` | UNIQUE |
| `IsClustered` | `Boolean` | `"false"` | CLUSTERED (SQL Server / SQL Server Compat) |
| `SortOrder` | `Enum` | `ASC`, `DESC` | `ASC` | Ordem padrão de todos os campos |
| `FilterExpression` | `Text` | `""` | WHERE clause (SQL Server filtered index) |
| `IndexType` | `Enum` | `BTREE`, `HASH`, `GIN`, `GIST`, `BRIN` | `BTREE` | Tipo de índice (Postgres) |
| `FillFactor` | `Number` | `""` | Fill factor em % (SQL Server / Postgres) |

---

### 4.4 Nós de Output DDL

---

#### `CreateTableOutput`
**Papel:** Nó terminal. Recebe um `TableDef` completo e gera o statement `CREATE TABLE`.
É o equivalente ao `ResultOutput` no modo query.

```
  table ▪ ──────────▷│ ★ Create Table Output  │
                      │  [gerado em tempo real] │
```

**Pins:**

| Nome | Direção | Tipo | `IsRequired` |
|---|---|---|---|
| `table` | Input | `TableDef` | true |

Sem parâmetros. O compilador lê o grafo inteiro a partir deste nó.

---

#### `AlterTableOutput`
**Papel:** Nó terminal para statements `ALTER TABLE`. Aceita múltiplas operações atômicas
(`AlterOp`) e as agrupa em um bloco coerente de ALTER.

**Pins:**

| Nome | Direção | Tipo | `IsRequired` | `AllowMultiple` |
|---|---|---|---|---|
| `table` | Input | `TableDef` | true | false |
| `operation` | Input | `AlterOp` | true | true |

**Parâmetros:**

| Nome | Kind | Default | Descrição |
|---|---|---|---|
| `EmitSeparateStatements` | `Boolean` | `"true"` | Um ALTER por operação vs. bloco único |

---

#### `CreateIndexOutput`
**Papel:** Nó terminal para `CREATE INDEX`. Recebe um `IndexDef`.

**Pins:**

| Nome | Direção | Tipo | `IsRequired` |
|---|---|---|---|
| `index` | Input | `IndexDef` | true |

---

### 4.5 Nós de Operação ALTER

Cada operação ALTER é um nó separado que emite um `AlterOp`. Eles são conectados ao
`AlterTableOutput` pela porta `operation`.

---

#### `AddColumnOp`
Emite `ALTER TABLE ... ADD COLUMN ...`

**Pins:** `column` (Input, `ColumnDef`, required) → `op` (Output, `AlterOp`)

**Parâmetros:** nenhum (tudo vem do `ColumnDef` conectado).

---

#### `DropColumnOp`
Emite `ALTER TABLE ... DROP COLUMN ...`

**Pins:** `op` (Output, `AlterOp`)

**Parâmetros:**

| Nome | Kind | Descrição |
|---|---|---|
| `ColumnName` | `Text` | Nome da coluna a remover |
| `IfExists` | `Boolean` | `IF EXISTS` (Postgres / MySQL 8+; não suportado em SQL Server) |

---

#### `RenameColumnOp`
Emite `ALTER TABLE ... RENAME COLUMN ... TO ...` (Postgres/MySQL) ou
`sp_rename` (SQL Server).

**Pins:** `op` (Output, `AlterOp`)

**Parâmetros:**

| Nome | Kind | Descrição |
|---|---|---|
| `OldName` | `Text` | Nome atual da coluna |
| `NewName` | `Text` | Novo nome da coluna |

---

#### `AlterColumnTypeOp`
Emite `ALTER TABLE ... ALTER COLUMN ... TYPE ...` (Postgres) /
`ALTER TABLE ... MODIFY COLUMN ...` (MySQL) /
`ALTER TABLE ... ALTER COLUMN ... [nova definição]` (SQL Server).

**Pins:**
- `new_column` (Input, `ColumnDef`, required) → `op` (Output, `AlterOp`)

O `ColumnDef` conectado define o novo tipo. O compilador não gera o bloco `DEFAULT` e
`IDENTITY` para este nó (apenas type + nullability).

---

#### `AddConstraintOp`
Emite `ALTER TABLE ... ADD CONSTRAINT ...`

**Pins:** `constraint` (Input, `Constraint`, required) → `op` (Output, `AlterOp`)

---

#### `DropConstraintOp`
Emite `ALTER TABLE ... DROP CONSTRAINT ...`

**Pins:** `op` (Output, `AlterOp`)

**Parâmetros:**

| Nome | Kind | Descrição |
|---|---|---|
| `ConstraintName` | `Text` | Nome da constraint |
| `IfExists` | `Boolean` | `IF EXISTS` (não suportado em SQL Server) |

---

### 4.6 Tipos de dado suportados em `ColumnDefinition`

O parâmetro `DataType` do `ColumnDefinition` expõe um enum fixo com os tipos canônicos.
O `DdlGeneratorService` mapeia cada tipo para a sintaxe do provider ativo.

| Tipo Canônico | SQL Server | PostgreSQL | MySQL | SQLite |
|---|---|---|---|---|
| `TINYINT` | `TINYINT` | `SMALLINT` | `TINYINT` | `INTEGER` |
| `SMALLINT` | `SMALLINT` | `SMALLINT` | `SMALLINT` | `INTEGER` |
| `INT` | `INT` | `INTEGER` | `INT` | `INTEGER` |
| `BIGINT` | `BIGINT` | `BIGINT` | `BIGINT` | `INTEGER` |
| `DECIMAL(p,s)` | `DECIMAL(p,s)` | `NUMERIC(p,s)` | `DECIMAL(p,s)` | `REAL` |
| `FLOAT` | `FLOAT` | `DOUBLE PRECISION` | `DOUBLE` | `REAL` |
| `BOOLEAN` | `BIT` | `BOOLEAN` | `TINYINT(1)` | `INTEGER` |
| `CHAR(n)` | `CHAR(n)` | `CHAR(n)` | `CHAR(n)` | `TEXT` |
| `VARCHAR(n)` | `VARCHAR(n)` | `VARCHAR(n)` | `VARCHAR(n)` | `TEXT` |
| `NVARCHAR(n)` | `NVARCHAR(n)` | `VARCHAR(n)` | `VARCHAR(n) CHARSET utf8mb4` | `TEXT` |
| `TEXT` | `NVARCHAR(MAX)` | `TEXT` | `TEXT` | `TEXT` |
| `UUID` | `UNIQUEIDENTIFIER` | `UUID` | `CHAR(36)` | `TEXT` |
| `DATE` | `DATE` | `DATE` | `DATE` | `TEXT` |
| `TIME` | `TIME` | `TIME` | `TIME` | `TEXT` |
| `DATETIME` | `DATETIME2` | `TIMESTAMP` | `DATETIME` | `TEXT` |
| `TIMESTAMP_TZ` | `DATETIMEOFFSET` | `TIMESTAMPTZ` | `DATETIME` ¹ | `TEXT` |
| `JSON` | `NVARCHAR(MAX)` ² | `JSONB` | `JSON` | `TEXT` |
| `BLOB` | `VARBINARY(MAX)` | `BYTEA` | `BLOB` | `BLOB` |

¹ MySQL não tem timezone-aware timestamp nativo; o compilador emite um aviso.
² SQL Server sem suporte a tipo JSON nativo até SQL Server 2022 (JSON_VALUE apenas).

O compilador emite avisos de compatibilidade no painel de diagnóstico do canvas DDL quando
o provider ativo não suporta o tipo escolhido de forma nativa.

---

## 5. Pipeline de Compilação DDL

### 5.1 Arquitetura geral

O pipeline DDL é análogo ao pipeline de query, mas separado — não reutiliza
`NodeGraphCompiler` nem `ISqlExpression`.

```
DdlCanvasViewModel (NodeViewModel + ConnectionViewModel)
  │
  ▼  [serializar grafo]
DdlNodeGraph (mesma estrutura de NodeGraph, novos NodeTypes)
  │
  ▼  DdlGraphCompiler.Compile()
CompiledDdlGraph (CreateTableStmt[], AlterTableStmt[], CreateIndexStmt[])
  │
  ▼  DdlGeneratorService.Generate(provider)
DdlGeneratedScript (Sql string, Warnings[], StatementCount)
```

### 5.2 `IDdlExpression` — hierarquia de expressões

```csharp
// src/VisualSqlArchitect/Ddl/Expressions/IDdlExpression.cs  (novo)
public interface IDdlExpression
{
    string Emit(DdlEmitContext ctx);
}

// src/VisualSqlArchitect/Ddl/Expressions/DdlEmitContext.cs  (novo)
public sealed record DdlEmitContext(
    DatabaseProvider Provider,
    ISqlDialect Dialect
)
{
    public string Q(string identifier) => Dialect.QuoteIdentifier(identifier);
}
```

**Hierarquia completa:**

```
IDdlExpression
│
├── CreateTableExpr
│     TableName, SchemaName, IfNotExists, IsTemporary
│     Columns     : IReadOnlyList<ColumnDefExpr>
│     Constraints : IReadOnlyList<IDdlConstraintExpr>
│
├── ColumnDefExpr
│     Name, CanonicalType, Length?, Precision?, Scale?
│     IsNullable, IsIdentity, IdentitySeed, IdentityIncrement
│     DefaultExpr?, CollationOverride?, ComputedExpr?, IsPersisted
│     → Emit() delega ao provider para tradução de tipo e identity
│
├── IDdlConstraintExpr  (interface)
│     ├── PrimaryKeyExpr
│     │     Name?, Columns[], IsClustered
│     ├── ForeignKeyExpr
│     │     Name?, ChildColumns[], ParentTable, ParentSchema, ParentColumns[]
│     │     OnDelete, OnUpdate  (ReferentialAction enum)
│     ├── UniqueExpr
│     │     Name?, Columns[], IsFiltered, FilterExpression?
│     ├── CheckExpr
│     │     Name?, Expression
│     └── DefaultExpr          ← apenas SQL Server (outros são inline em ColumnDefExpr)
│           Name, Column, Value
│
├── CreateIndexExpr
│     Name, TableName, SchemaName, IsUnique, IsClustered
│     Columns        : IReadOnlyList<IndexColumnExpr>
│     IncludeColumns : IReadOnlyList<string>
│     FilterExpr?    : string
│     IndexType?     : string   (BTREE, HASH, GIN, etc.)
│     FillFactor?    : int
│
├── IndexColumnExpr
│     ColumnName, SortDescending
│
└── AlterTableExpr
      TableName, SchemaName
      Operations : IReadOnlyList<IAlterOpExpr>

IAlterOpExpr  (interface)
  ├── AddColumnOp    (ColumnDefExpr)
  ├── DropColumnOp   (ColumnName, IfExists)
  ├── RenameColumnOp (OldName, NewName)
  ├── AlterColumnOp  (ColumnName, NewType, IsNullable)
  ├── AddConstraintOp (IDdlConstraintExpr)
  └── DropConstraintOp (ConstraintName, IfExists)
```

### 5.3 `DdlGraphCompiler`

```csharp
// src/VisualSqlArchitect/Ddl/DdlGraphCompiler.cs  (novo)
public sealed class DdlGraphCompiler
{
    public CompiledDdlGraph Compile(DdlNodeGraph graph);
}

public sealed record CompiledDdlGraph(
    IReadOnlyList<CreateTableExpr> CreateStatements,
    IReadOnlyList<AlterTableExpr>  AlterStatements,
    IReadOnlyList<CreateIndexExpr> IndexStatements,
    IReadOnlyList<DdlDiagnostic>   Diagnostics     // avisos e erros de validação
);

public sealed record DdlDiagnostic(
    DdlDiagnosticSeverity Severity,  // Warning | Error
    string NodeId,
    string Message
);
```

**Processo de compilação:**

1. Localiza todos os nós `CreateTableOutput`, `AlterTableOutput`, `CreateIndexOutput`
   (nós terminais — pontos de entrada do compilador).
2. Caminha o grafo para trás a partir de cada terminal (depth-first), resolvendo
   `TableDef` → `ColumnDef[]` + `Constraint[]`.
3. Para cada `ColumnDef`, resolve os parâmetros e o pin `default_value` (se conectado).
4. Para cada `ForeignKeyConstraint`, valida que `child_column` e `parent_column` têm
   o mesmo número de conexões (PK composta).
5. Ordena os `CreateTableExpr` topologicamente por dependência de FK — tabelas
   referenciadas devem ser criadas antes das que referenciam.
6. Coleta todos os `CreateIndexExpr` (independentes da ordem).
7. Retorna `CompiledDdlGraph` com a lista ordenada e os diagnósticos.

### 5.4 `DdlGeneratorService`

```csharp
// src/VisualSqlArchitect/Ddl/DdlGeneratorService.cs  (novo)
public sealed class DdlGeneratorService
{
    public static DdlGeneratorService Create(DatabaseProvider provider);
    public DdlGeneratedScript Generate(CompiledDdlGraph compiled);
}

public sealed record DdlGeneratedScript(
    string Sql,
    IReadOnlyList<DdlDiagnostic> Warnings,
    int StatementCount,
    IReadOnlyList<string> StatementLabels   // ex: "CREATE TABLE dbo.orders"
);
```

**Emissão por provider — exemplos de `CREATE TABLE`:**

```sql
-- SQL Server
CREATE TABLE [dbo].[orders] (
    [id]          INT           NOT NULL IDENTITY(1,1),
    [customer_id] INT           NOT NULL,
    [total]       DECIMAL(10,2) NOT NULL,
    [created_at]  DATETIME2     NOT NULL DEFAULT GETDATE(),
    CONSTRAINT [PK_orders] PRIMARY KEY CLUSTERED ([id]),
    CONSTRAINT [FK_orders_customer] FOREIGN KEY ([customer_id])
        REFERENCES [dbo].[customers] ([id])
        ON DELETE NO ACTION ON UPDATE NO ACTION
);

-- PostgreSQL
CREATE TABLE "public"."orders" (
    "id"          SERIAL        NOT NULL,
    "customer_id" INTEGER       NOT NULL,
    "total"       NUMERIC(10,2) NOT NULL,
    "created_at"  TIMESTAMP     NOT NULL DEFAULT NOW(),
    CONSTRAINT "PK_orders" PRIMARY KEY ("id"),
    CONSTRAINT "FK_orders_customer" FOREIGN KEY ("customer_id")
        REFERENCES "public"."customers" ("id")
        ON DELETE NO ACTION ON UPDATE NO ACTION
);

-- MySQL
CREATE TABLE `orders` (
    `id`          INT            NOT NULL AUTO_INCREMENT,
    `customer_id` INT            NOT NULL,
    `total`       DECIMAL(10,2)  NOT NULL,
    `created_at`  DATETIME       NOT NULL DEFAULT (NOW()),
    PRIMARY KEY (`id`),
    CONSTRAINT `FK_orders_customer` FOREIGN KEY (`customer_id`)
        REFERENCES `customers` (`id`)
        ON DELETE NO ACTION ON UPDATE NO ACTION
);

-- SQLite
CREATE TABLE "orders" (
    "id"          INTEGER       NOT NULL,
    "customer_id" INTEGER       NOT NULL,
    "total"       REAL          NOT NULL,
    "created_at"  TEXT          NOT NULL DEFAULT (datetime('now')),
    PRIMARY KEY ("id"),
    FOREIGN KEY ("customer_id") REFERENCES "customers" ("id")
);
```

**Emissão por provider — exemplos de `ALTER TABLE`:**

```sql
-- SQL Server
ALTER TABLE [dbo].[orders] ADD [notes] NVARCHAR(500) NULL;
ALTER TABLE [dbo].[orders] DROP COLUMN [legacy_field];
EXEC sp_rename 'dbo.orders.old_name', 'new_name', 'COLUMN';
ALTER TABLE [dbo].[orders] ALTER COLUMN [total] DECIMAL(12,2) NOT NULL;

-- PostgreSQL
ALTER TABLE "public"."orders" ADD COLUMN "notes" VARCHAR(500);
ALTER TABLE "public"."orders" DROP COLUMN IF EXISTS "legacy_field";
ALTER TABLE "public"."orders" RENAME COLUMN "old_name" TO "new_name";
ALTER TABLE "public"."orders" ALTER COLUMN "total" TYPE NUMERIC(12,2);

-- MySQL
ALTER TABLE `orders` ADD COLUMN `notes` VARCHAR(500);
ALTER TABLE `orders` DROP COLUMN `legacy_field`;
ALTER TABLE `orders` RENAME COLUMN `old_name` TO `new_name`;
ALTER TABLE `orders` MODIFY COLUMN `total` DECIMAL(12,2) NOT NULL;

-- SQLite  (limitações: DROP COLUMN desde 3.35, RENAME COLUMN desde 3.25)
ALTER TABLE "orders" ADD COLUMN "notes" TEXT;
ALTER TABLE "orders" DROP COLUMN "legacy_field";
ALTER TABLE "orders" RENAME COLUMN "old_name" TO "new_name";
-- AlterColumnTypeOp emite aviso: SQLite não suporta ALTER COLUMN TYPE.
-- Alternativa gerada como comentário: recreate table pattern.
```

### 5.5 Extensão do `ISqlDialect`

O `ISqlDialect` existente precisa de métodos DDL:

```csharp
// src/VisualSqlArchitect/Providers/Dialects/ISqlDialect.cs  (modificar)
public interface ISqlDialect
{
    // ── existentes ──
    string GetTablesQuery();
    string GetColumnsQuery();
    string GetPrimaryKeysQuery();
    string GetForeignKeysQuery();
    string WrapWithPreviewLimit(string baseQuery, int maxRows);
    string FormatPagination(int? limit, int? offset);
    string QuoteIdentifier(string identifier);

    // ── novos DDL ──
    string FormatColumnType(string canonicalType, int? length, int? precision, int? scale);
    string FormatIdentity(int seed, int increment);          // IDENTITY, SERIAL, AUTO_INCREMENT
    string FormatDefaultNow();                               // GETDATE(), NOW(), datetime('now')
    bool   SupportsDropColumnIfExists   { get; }
    bool   SupportsAlterColumnType      { get; }
    bool   SupportsNamedDefaultConstraint { get; }
    bool   SupportsFilteredIndex        { get; }
    bool   SupportsClustered            { get; }
    string FormatRenameColumn(string table, string schema, string oldName, string newName);
}
```

---

## 6. Interface Visual do Canvas DDL

### 6.1 Layout geral

O canvas DDL usa o mesmo `InfiniteCanvas`, com diferenças no header lateral:

```
┌────────────────────────────────────────────────────────────────────┐
│  [ Query │ ▪ DDL ]                              [Import Schema] ⚙ │
├──────────┬─────────────────────────────────────────────────────────┤
│ Schema   │                                                         │
│ Sidebar  │   ┌──────────────────┐      ┌────────────────────────┐ │
│          │   │ ▪ TableDefinition│      │ ▪ TableDefinition      │ │
│ ▶ Tables │   │ dbo.orders       │      │ dbo.customers          │ │
│   orders │   ├──────────────────┤      ├────────────────────────┤ │
│   cust.. │   │⊙ id   INT  PK   ●──┐   │⊙ id   INT  PK        ●│ │
│   ...    │   │⊙ cust INT  FK   ●──┼───▷│⊙ name TEXT           ●│ │
│          │   │⊙ total DEC      ●  │   │⊙ email TEXT          ●│ │
│ ▶ Node   │   │⊙ creat DT       ●  │   └────────────────────────┘ │
│   Picker │   └──────────────────┘  │                              │
│          │                         └──▶ ◆ FK_orders_customer      │
│          │                              ◆ ──────────────────────  │
└──────────┴───────────────────────────────────────────────────────┘
```

### 6.2 Template visual do nó `TableDefinition` (ERD Card)

O nó `TableDefinition` usa um template AXAML diferente do nó query padrão.
Em vez de listar pinos como rows genéricos, ele renderiza como um **ERD card** com:

- **Header:** nome da tabela (schema.table) com ícone de tabela
- **Área de colunas:** cada `ColumnDef` conectado aparece como uma linha
  com ícone de tipo, nome, tipo de dado, e indicadores de PK/FK/NN
- **Área de pinos:** abaixo das colunas, os pinos de `Constraint` de entrada
  ficam agrupados
- **Output pin** `table` fica à direita do header

```
┌─────────────────────────────────────────────┐
│ ▪  dbo.orders                         ▪──── │  ← TableDef output
├─────────────────────────────────────────────┤
│ ⊙──●  🔑 id           INT  NOT NULL        │  ← ColumnDef outputs individuais
│ ⊙──●  🔗 customer_id  INT  NOT NULL        │
│ ⊙──●     total        DEC  NOT NULL        │
│ ⊙──●     created_at   DT   NOT NULL        │
│ ⊙──●     notes        TXT  NULL            │
├─────────────────────────────────────────────┤
│ ◆──▷  constraints...                        │  ← Constraint inputs
└─────────────────────────────────────────────┘
```

**Ícones de coluna:**
- `🔑` — Primary Key
- `🔗` — Foreign Key
- `◈` — Unique
- `⚡` — Identity/AutoIncrement
- `#` — Computed column
- *(sem ícone)* — coluna normal

### 6.3 Barra de Preview DDL (`LiveDdlBarViewModel`)

Análoga ao `LiveSqlBarViewModel` do modo query. Fica no rodapé do canvas DDL:

```
┌────────────────────────────────────────────────────────────────────┐
│ DDL Preview  [2 statements | 0 erros | 1 aviso]    [Copiar] [▶ Executar] │
├────────────────────────────────────────────────────────────────────┤
│  CREATE TABLE [dbo].[orders] (                                     │
│      [id] INT NOT NULL IDENTITY(1,1),                              │
│      ...                                                           │
│  );                                                                │
└────────────────────────────────────────────────────────────────────┘
```

```csharp
// src/VisualSqlArchitect.UI/ViewModels/Ddl/LiveDdlBarViewModel.cs  (novo)
public sealed class LiveDdlBarViewModel : ViewModelBase
{
    public string DdlText          { get; private set; }
    public int    StatementCount   { get; private set; }
    public int    ErrorCount       { get; private set; }
    public int    WarningCount     { get; private set; }
    public bool   HasErrors        { get; private set; }
    public IReadOnlyList<DdlDiagnostic> Diagnostics { get; private set; }

    public ICommand CopyDdlCommand    { get; }
    public ICommand ExecuteDdlCommand { get; }  // abre diálogo de confirmação
}
```

**Atualização em tempo real:** igual ao `LiveSqlBarViewModel` — debounce de 300ms após
qualquer mudança no grafo (nó adicionado, parâmetro alterado, conexão criada/removida).

### 6.4 Diálogo de Execução DDL

O botão "Executar" abre um diálogo modal de confirmação antes de executar qualquer
statement DDL. DDL é irreversível por natureza.

```
┌─────────────────────────────────────────────────┐
│  ⚠  Executar DDL                                │
│                                                 │
│  Os seguintes statements serão executados:      │
│                                                 │
│  1. CREATE TABLE [dbo].[orders]                 │
│  2. CREATE TABLE [dbo].[customers]              │
│  3. CREATE INDEX [IX_orders_customer_id]...     │
│                                                 │
│  Esta operação não pode ser desfeita.           │
│  Banco: SqlServer — production-server           │
│                                                 │
│            [Cancelar]   [Executar]              │
└─────────────────────────────────────────────────┘
```

**Execução:** `IDbOrchestrator` ganha um novo método:

```csharp
// src/VisualSqlArchitect/Core/IDbOrchestrator.cs  (modificar)
/// <summary>
/// Executes DDL statements sequentially. Each statement is run in its own
/// implicit transaction (DDL is auto-committed in most providers).
/// Returns a result per statement.
/// </summary>
Task<DdlExecutionResult> ExecuteDdlAsync(
    string sql,
    CancellationToken ct = default
);

public sealed record DdlExecutionResult(
    bool Success,
    IReadOnlyList<DdlStatementResult> Statements,
    string? ErrorMessage = null
);

public sealed record DdlStatementResult(
    string Statement,
    bool   Success,
    string? ErrorMessage,
    TimeSpan ExecutionTime
);
```

---

## 7. Import Automático: Schema → Canvas DDL

### 7.1 Propósito

O `DdlSchemaImporter` lê o `DbMetadata` do banco conectado e popula o canvas DDL
automaticamente. O resultado é um grafo visual representando o schema atual — pronto
para edição incremental.

### 7.2 Processo de importação

```csharp
// src/VisualSqlArchitect.UI/Services/Ddl/DdlSchemaImporter.cs  (novo)
public sealed class DdlSchemaImporter
{
    public ImportResult Import(DbMetadata metadata, DdlCanvasViewModel canvas);
}
```

**Algoritmo:**

1. **Por tabela** → cria um `TableDefinition` node com os parâmetros `TableName` e
   `Schema` preenchidos.

2. **Por coluna** de cada tabela → cria um `ColumnDefinition` node com todos os
   parâmetros preenchidos a partir do `ColumnMetadata` (tipo, nullabilidade, default,
   identity detectado via `NativeType`, maxlength, precision, scale).

3. **Por `ForeignKeyRelation`** → cria um `ForeignKeyConstraint` node. Se a FK é
   composta (múltiplas colunas), conecta todas as colunas na ordem `OrdinalPosition`.

4. **PKs** → cria um `PrimaryKeyConstraint` por tabela, conectando as colunas marcadas
   como `IsPrimaryKey`.

5. **Unique indexes** → cria um `UniqueConstraint` para cada `IndexMetadata` com
   `IsUnique = true` e `IsPrimaryKey = false`.

6. **Posicionamento** → usa o `NodeLayoutManager` com o algoritmo de Sugiyama para
   posicionar os nós. Tabelas sem FKs ficam na borda esquerda; tabelas com muitas
   dependências ficam no centro.

7. **Conecta as portas** dos `CreateTableOutput` automaticamente — um por tabela.

### 7.3 Mapeamento `ColumnMetadata` → `ColumnDefinition`

```
ColumnMetadata.NativeType  → ColumnDefinition.DataType (tipo canônico)
ColumnMetadata.MaxLength    → ColumnDefinition.Length
ColumnMetadata.Precision    → ColumnDefinition.Precision
ColumnMetadata.Scale        → ColumnDefinition.Scale
ColumnMetadata.IsNullable   → ColumnDefinition.IsNullable
ColumnMetadata.DefaultValue → ColumnDefinition parâmetro (default_value pin)
ColumnMetadata.NativeType contains "identity" / "serial" / "auto_increment"
                            → ColumnDefinition.IsIdentity = true
```

O mapeamento de `NativeType` para tipo canônico reutiliza a lógica de
`ColumnMetadata.InferSemanticType` como base, mas é mais granular — distingue
`INT` de `BIGINT`, `VARCHAR` de `NVARCHAR`, etc.

### 7.4 Importação parcial (tabelas selecionadas)

Além do import completo, o sidebar do canvas DDL permite arrastar uma tabela do
schema tree para o canvas — comportamento análogo ao drag de `TableSource` no modo
query. O resultado é um único `TableDefinition` com todas as suas `ColumnDefinition`
e `PrimaryKeyConstraint`.

---

## 8. Serialização e Persistência

### 8.1 Estrutura do arquivo .vsa com DDL

O `CanvasSerializer` existente salva um `SavedCanvas` como JSON. A nova estrutura:

```json
{
  "AppVersion": "1.2.0",
  "FileVersion": 4,
  "QueryCanvas": {
    "CanvasType": "Query",
    "CreatedAt": "2025-01-01T00:00:00Z",
    "Description": "",
    "Nodes": [...],
    "Connections": [...]
  },
  "DdlCanvas": {
    "CanvasType": "Ddl",
    "CreatedAt": "2025-01-01T00:00:00Z",
    "Description": "",
    "Nodes": [...],
    "Connections": [...]
  }
}
```

Os nós DDL serializam igual aos nós query: `NodeId`, `NodeType`, `Position`,
`Parameters`, `PinLiterals`. A distinção de canvas (Query vs DDL) é dada pelo campo
`CanvasType` e pelo `NodeType` enum — nós DDL nunca aparecem em um `QueryCanvas`.

### 8.2 Migração de versão

Arquivos v1–v3 (formato atual) são lidos como `QueryCanvas` com `DdlCanvas: null`.
O `CanvasSerializer.Migrate()` produz o envelope v4 na primeira abertura/salvamento.

```csharp
// src/VisualSqlArchitect.UI/Serialization/Canvas/CanvasSerializer.cs  (modificar)
private static SavedFile MigrateV3ToV4(SavedCanvas legacyCanvas) =>
    new SavedFile(
        AppVersion: legacyCanvas.AppVersion,
        FileVersion: 4,
        QueryCanvas: legacyCanvas with { CanvasType = "Query" },
        DdlCanvas: null
    );
```

---

## 9. Validação e Diagnósticos

### 9.1 Validações estáticas do compilador DDL

O `DdlGraphCompiler` coleta erros e avisos antes de emitir qualquer SQL.

| Regra | Severidade | Mensagem |
|---|---|---|
| `CreateTableOutput` sem `TableDef` conectado | Error | "Nó de saída não conectado a nenhuma tabela" |
| `TableDefinition` sem nenhuma `ColumnDef` | Error | "Tabela sem colunas definidas" |
| `ForeignKeyConstraint` com contagem de colunas assimétricas | Error | "FK composta: número de colunas filho ≠ colunas pai" |
| `PrimaryKeyConstraint` duplicada na mesma tabela | Error | "Tabela não pode ter mais de uma PK" |
| Ciclo de FKs (A→B→A) | Warning | "Dependência circular de FK detectada — verifique a ordem de criação" |
| `ColumnDefinition` sem `ColumnName` | Error | "Nome de coluna não pode estar em branco" |
| `AlterColumnTypeOp` com SQLite ativo | Warning | "SQLite não suporta ALTER COLUMN TYPE — considere recriar a tabela" |
| `DropColumnOp` com `IfExists=true` em SQL Server | Warning | "SQL Server não suporta IF EXISTS em DROP COLUMN — será removido" |
| Tipo de dado com `Length` não informado (ex: `VARCHAR` sem tamanho) | Warning | "VARCHAR sem tamanho — padrão será aplicado (1 no SQL Server, sem limite no Postgres)" |
| `IsIdentity=true` em coluna não inteira | Error | "Identity/AutoIncrement só é válido em colunas inteiras" |
| Tabela com mesmo nome já existe em `SchemaReference` no grafo | Warning | "Nome de tabela duplicado no canvas" |

### 9.2 Painel de diagnósticos DDL

Análogo ao painel de erros do canvas query — lista os diagnósticos com severidade, ID do
nó e mensagem. Clicar no item seleciona e faz pan até o nó no canvas.

---

## 10. Estrutura de Arquivos Novos e Modificados

### 10.1 Arquivos novos (Core)

```
src/VisualSqlArchitect/
└── Ddl/
    ├── Expressions/
    │   ├── IDdlExpression.cs
    │   ├── DdlEmitContext.cs
    │   ├── CreateTableExpr.cs
    │   ├── ColumnDefExpr.cs
    │   ├── IDdlConstraintExpr.cs
    │   ├── PrimaryKeyExpr.cs
    │   ├── ForeignKeyExpr.cs
    │   ├── UniqueExpr.cs
    │   ├── CheckExpr.cs
    │   ├── DefaultExpr.cs
    │   ├── CreateIndexExpr.cs
    │   ├── IndexColumnExpr.cs
    │   ├── AlterTableExpr.cs
    │   └── AlterOps/
    │       ├── IAlterOpExpr.cs
    │       ├── AddColumnOp.cs
    │       ├── DropColumnOp.cs
    │       ├── RenameColumnOp.cs
    │       ├── AlterColumnOp.cs
    │       ├── AddConstraintOp.cs
    │       └── DropConstraintOp.cs
    ├── DdlNodeGraph.cs          ← alias/specialização de NodeGraph com validação DDL
    ├── DdlGraphCompiler.cs
    ├── DdlGeneratorService.cs
    └── DdlTypeMapper.cs         ← canonical type → provider-specific SQL type string
```

### 10.2 Arquivos novos (UI)

```
src/VisualSqlArchitect.UI/
├── ViewModels/
│   └── Ddl/
│       ├── DdlCanvasViewModel.cs
│       ├── LiveDdlBarViewModel.cs
│       ├── DdlDiagnosticsPanelViewModel.cs
│       └── DdlExecuteDialogViewModel.cs
├── Services/
│   └── Ddl/
│       └── DdlSchemaImporter.cs
└── Views/
    └── Ddl/
        ├── DdlCanvasView.axaml
        ├── DdlCanvasView.axaml.cs
        ├── DdlExecuteDialog.axaml
        └── DdlExecuteDialog.axaml.cs
```

### 10.3 Arquivos modificados

| Arquivo | O que muda |
|---|---|
| `Nodes/NodeDefinition.cs` | `NodeCategory.Ddl`, 15 novos `NodeType`, 5 novos `PinDataType` |
| `Nodes/Definitions/` | Novo `DdlDefinitions.cs` com as `NodeDefinition` dos 15 nós DDL |
| `Providers/Dialects/ISqlDialect.cs` | Novos métodos DDL (seção 5.5) |
| `Providers/Dialects/SqlServerDialect.cs` | Implementar novos métodos DDL |
| `Providers/Dialects/PostgresDialect.cs` | Implementar novos métodos DDL |
| `Providers/Dialects/MySqlDialect.cs` | Implementar novos métodos DDL |
| `Providers/Dialects/SqliteDialect.cs` | Implementar novos métodos DDL |
| `Core/IDbOrchestrator.cs` | Novo método `ExecuteDdlAsync` |
| `Providers/SqlServerOrchestrator.cs` | Implementar `ExecuteDdlAsync` |
| `Providers/PostgresOrchestrator.cs` | Implementar `ExecuteDdlAsync` |
| `Providers/MySqlOrchestrator.cs` | Implementar `ExecuteDdlAsync` |
| `Providers/SqliteOrchestrator.cs` | Implementar `ExecuteDdlAsync` |
| `ViewModels/Shell/ShellViewModel.cs` | `AppMode` enum, `DdlCanvas`, `ActiveMode` |
| `ViewModels/Canvas/PinViewModel.cs` | Novos cases de cor, dash e label para tipos DDL |
| `Serialization/Canvas/CanvasSerializer.cs` | Envelope v4 com `QueryCanvas` + `DdlCanvas` |
| `Views/Shell/MainWindow.axaml` | Toggle Query/DDL no header |

---

## 11. Plano de Implementação por Fases

### Fase 1 — Fundação do tipo e navegação
*Sem funcionalidade visível ao usuário — infraestrutura.*

- [ ] Adicionar `NodeCategory.Ddl` e os 15 `NodeType` DDL ao enum `NodeDefinition.cs`
- [ ] Adicionar os 5 `PinDataType` DDL ao enum
- [ ] Implementar `DdlDefinitions.cs` (definições estáticas dos 15 nós)
- [ ] Adicionar `AppMode` ao `ShellViewModel` e criar o toggle no header
- [ ] Criar `DdlCanvasViewModel` como stub (canvas vazio, sem compiler)

### Fase 2 — Visual dos pinos DDL
*Todos os novos pinos renderizam corretamente no canvas.*

- [ ] Implementar cores, formas e labels dos 5 novos `PinDataType` em `PinViewModel.cs`
- [ ] Adicionar `WireDashKind.MediumDash` e `WireDashKind.ShortDash` se ausentes
- [ ] Garantir que pinos DDL bloqueiam conexão com pinos query em `CanAccept`
- [ ] Testar renderização dos novos tipos em `PinShapeControl`

### Fase 3 — Template ERD para `TableDefinition`
*O nó `TableDefinition` tem visual diferenciado.*

- [ ] Criar DataTemplate alternativo em AXAML para `NodeType.TableDefinition`
- [ ] Implementar renderização de linhas de coluna com ícones de PK/FK/Unique
- [ ] Integrar com `PropertyPanelViewModel` para os parâmetros de tabela e coluna

### Fase 4 — Compilador e gerador DDL básico
*`CREATE TABLE` funciona para o provider ativo.*

- [ ] Implementar hierarquia `IDdlExpression` e `DdlEmitContext`
- [ ] Implementar `DdlGraphCompiler.Compile()` para `CreateTableOutput`
- [ ] Implementar `DdlGeneratorService` para SQL Server (provider prioritário)
- [ ] Adicionar métodos DDL ao `ISqlDialect` e implementar em `SqlServerDialect`
- [ ] Ligar `LiveDdlBarViewModel` ao compilador (preview em tempo real)

### Fase 5 — Multi-dialeto e índices
*Suporte completo a todos os providers e índices.*

- [ ] Implementar dialect DDL em `PostgresDialect`, `MySqlDialect`, `SqliteDialect`
- [ ] Implementar `CreateIndexExpr` e `CreateIndexOutput`
- [ ] Adicionar avisos de compatibilidade de provider ao `DdlGraphCompiler`

### Fase 6 — ALTER TABLE
*Operações de alteração de schema.*

- [ ] Implementar os 6 nós `*Op` e a hierarquia `IAlterOpExpr`
- [ ] Implementar `AlterTableOutput` no compilador e gerador
- [ ] Testar diferenças de dialeto (sp_rename SQL Server, MODIFY MySQL, RENAME COLUMN Postgres)

### Fase 7 — Execução DDL
*O botão "Executar" funciona com confirmação.*

- [ ] Adicionar `ExecuteDdlAsync` à interface `IDbOrchestrator`
- [ ] Implementar nos quatro orchestrators
- [ ] Implementar `DdlExecuteDialogViewModel` e a view de confirmação
- [ ] Tratar erros de execução por statement

### Fase 8 — Import de schema
*O schema existente vira nós no canvas DDL automaticamente.*

- [ ] Implementar `DdlSchemaImporter`
- [ ] Adicionar botão "Import Schema" no header do canvas DDL
- [ ] Implementar importação parcial (drag de tabela do sidebar)
- [ ] Integrar com `NodeLayoutManager` para posicionamento automático

### Fase 9 — Serialização e migração
*O estado do canvas DDL persiste corretamente.*

- [ ] Implementar envelope `SavedFile` v4 no `CanvasSerializer`
- [ ] Implementar migração v3 → v4
- [ ] Garantir que arquivos `.vsa` abrem com Query e DDL separados

### Fase 10 — Polimento e diagnósticos
*Todas as validações implementadas; UX completa.*

- [ ] Implementar `DdlDiagnosticsPanelViewModel` com lista clicável
- [ ] Implementar todas as regras de validação da seção 9.1
- [ ] Testes unitários para `DdlGraphCompiler` e `DdlGeneratorService`
- [ ] Testes de integração para cada provider (DDL + rollback)

---

## 12. Não-Objetivos (Fora de Escopo)

- **DROP TABLE** — operação destrutiva demais para ter representação visual como nó.
  Fica fora do módulo DDL desta versão.
- **CREATE VIEW** — diferente de `CREATE TABLE`; requer integração com o canvas query
  para definir o SELECT. Candidato a versão futura (um `SelectOutput` pode ser fonte
  de um `CreateViewOutput`).
- **Stored Procedures / Functions** — fora do escopo de DDL tabular.
- **Migrations geradas (Flyway, Liquibase, EF Migrations)** — possível integração futura
  como exportador (`MigrationExport`), mas não faz parte do compilador base.
- **Round-trip (DDL editado → grafo atualizado)** — o fluxo é grafo → DDL, nunca o
  contrário. Edições textuais no preview não retroalimentam o grafo.
- **Execução com rollback automático** — DDL é auto-commit por natureza na maioria dos
  providers. O botão "Executar" é explícito e irreversível.
- **Suporte a `ENUM` do MySQL** — tipo não-padrão; pode ser adicionado como extensão
  futura do `DdlTypeMapper`.

---

## 13. Referências

- [PIN_TYPES_REFERENCE.md](PIN_TYPES_REFERENCE.md) — sistema visual de pinos (formas, cores, fios)
- [TYPE_SYSTEM_ROADMAP.md](TYPE_SYSTEM_ROADMAP.md) — fundamentos do sistema de tipos do canvas
- [NodeDefinition.cs](../src/VisualSqlArchitect/Nodes/NodeDefinition.cs) — `NodeType`, `PinDataType`, `NodeCategory`
- [DbMetadata.cs](../src/VisualSqlArchitect/Metadata/DbMetadata.cs) — `ColumnMetadata`, `ForeignKeyRelation`, `IndexMetadata`
- [ISqlDialect.cs](../src/VisualSqlArchitect/Providers/Dialects/ISqlDialect.cs) — interface de dialeto a ser estendida
- [IDbOrchestrator.cs](../src/VisualSqlArchitect/Core/IDbOrchestrator.cs) — `ExecuteDdlAsync` será adicionado aqui
- [CanvasSerializer.cs](../src/VisualSqlArchitect.UI/Serialization/Canvas/CanvasSerializer.cs) — migração v4
- [ShellViewModel.cs](../src/VisualSqlArchitect.UI/ViewModels/Shell/ShellViewModel.cs) — `AppMode` e toggle
