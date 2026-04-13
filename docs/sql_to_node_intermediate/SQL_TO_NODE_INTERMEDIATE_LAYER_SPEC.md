# SQL_TO_NODE_INTERMEDIATE_LAYER_SPEC

## 0. Metadados do Documento

- **Nome**: SQL Import Intermediate Layer Specification
- **Versão**: 2.1.0-normativa (freeze-ready)
- **Data**: 2026-04-13
- **Status**: Pronta para congelamento técnico (implementação incremental sob feature flags)
- **Escopo de implementação**: `src/DBWeaver`, `src/DBWeaver.UI`, `tests/VisualSqlArchitect.Tests`
- **Substitui**: versão 2.0.0-normativa

## 1. Linguagem Normativa

As palavras **DEVE**, **NÃO DEVE**, **É OBRIGATÓRIO**, **É PROIBIDO**, **PODE**, **SOMENTE PODE**, **DEVE FALHAR** e **DEVE GERAR DIAGNÓSTICO** têm significado normativo e vinculante.

Se houver conflito entre texto explicativo e regra normativa, a regra normativa prevalece.

## 2. Objetivo e Resultado Esperado

Esta especificação define, de forma determinística e auditável, o pipeline:

`SQL texto → AST → IR canônica → Graph (Nodes + Edges) → SQL Compiler`

### 2.1 Resultado obrigatório

Para recursos suportados e sem diagnósticos fatais:

1. O import **DEVE** produzir grafo semanticamente consistente com a consulta original.
2. O SQL recompilado **DEVE** atingir no mínimo `EQUIVALENT_TOLERANT` quando aplicável (Seção 15).
3. Toda perda de fidelidade **DEVE** ser explícita em diagnóstico categorizado.

### 2.2 Propriedades mandatórias

- O pipeline **NÃO DEVE** depender de regex como mecanismo semântico primário.
- AST/IR **DEVE** ser a fonte de verdade semântica.
- Fallback regex **SOMENTE PODE** ocorrer de forma controlada, auditada e com diagnóstico.

## 3. Escopo, Não-Escopo e Restrições Arquiteturais

### 3.1 Escopo desta versão

Inclui:

- contratos formais de camada (Parser AST, AST→IR, IR→Graph, Graph→SQL)
- IR versionada, tipada e rastreável
- symbol table formal com escopo e resolução determinística
- normalização de aliases textuais
- matriz de suporte SQL por camada
- política de falha/parcialidade/fallback
- overhaul `ValueMap` graph-first com compatibilidade legada
- observabilidade, catálogo de diagnósticos, estratégia de testes e fixtures normativas

### 3.2 Não-escopo desta versão

- DML (`INSERT`, `UPDATE`, `DELETE`, `MERGE`)
- modelagem física DDL completa
- otimizações de layout visual sem impacto semântico
- reescritas de plano de execução SQL sem contrato explícito de equivalência

### 3.3 Proibições arquiteturais

É PROIBIDO:

1. usar parsing heurístico como semântica primária
2. mascarar import parcial como sucesso total
3. descartar projeções suportadas silenciosamente
4. mover semântica `ON` para `WHERE` sem prova formal de equivalência
5. alterar contratos do SQL compiler fora do escopo desta spec
6. introduzir abstrações paralelas duplicando componentes existentes sem justificativa mensurável

## 4. Reuso Obrigatório de Componentes Existentes

Antes de criar novo serviço utilitário, a implementação **DEVE** demonstrar:

1. que o comportamento não cabe em componente existente, **ou**
2. que a extração reduz duplicação mensurável e aumenta coesão.

Reuso obrigatório:

- `Services/SqlImport/SqlParserService.cs`
- `Services/SqlImport/Validation/SqlImportSyntaxValidator.cs`
- `Services/SqlImport/Execution/SqlImportExecutionService.cs`
- `Services/SqlImport/Execution/Applying/*`
- `Services/SqlImport/Build/ImportModelToCanvasBuilder.cs`
- `Services/SqlImport/Build/ImportBuildUtilities.cs`
- `Services/SqlImport/SqlImportIdentifierNormalizer.cs`
- `ViewModels/Validation/Conventions/*`
- `src/DBWeaver/Nodes/NodeDefinition.cs`
- `src/DBWeaver/Nodes/LogicalPlan/LogicalPlanner.cs`
- `src/DBWeaver/Nodes/Compilers/*`
- `tests/VisualSqlArchitect.Tests/Integration/SqlImport*`

## 5. Arquitetura Normativa do Pipeline

### 5.1 Fases

1. Parser: SQL texto → AST
2. Mapper semântico: AST → IR (`SqlToNodeIR`)
3. Materialização: IR → Graph
4. Compilação: Graph → SQL
5. Classificação: equivalência e outcome

### 5.2 Contratos de camada (fronteiras rígidas)

#### 5.2.1 Parser AST

**DEVE**:
- validar sintaxe estrutural
- produzir AST com spans e fragmentos relevantes
- preservar aliases/literais/identificadores conforme texto de origem

**NÃO DEVE**:
- construir graph
- normalizar sem registrar metadado
- inferir semântica fora da AST

Saída: `ParseResult{Ast?, Diagnostics[], ParseMode, CorrelationId}`

#### 5.2.2 AST → IR

**DEVE**:
- produzir IR versionada, determinística e tipada
- manter symbol table única por escopo
- resolver bindings com status explícito
- classificar suporte por recurso

**NÃO DEVE**:
- acessar UI
- emitir SQL
- usar regex como semântica primária

Saída: `IrBuildResult{Ir?, Diagnostics[], ResolutionSummary}`

#### 5.2.3 IR → Graph

**DEVE**:
- materializar subgrafos semânticos
- preservar rastreabilidade SQL↔IR↔Graph
- marcar nós degradados/sintéticos formalmente

**NÃO DEVE**:
- reparsear SQL
- esconder parcialidade estrutural

Saída: `GraphBuildResult{Graph?, Diagnostics[], CoverageSummary, DegradationSummary}`

#### 5.2.4 Graph → SQL

**DEVE**:
- compilar somente a partir de graph + contratos de nó
- preservar semântica de joins/filtros/projeções

**NÃO DEVE**:
- aplicar heurística textual para reconstrução primária
- ignorar marcações de parcialidade/degradação

Saída: `SqlCompileResult{Sql?, Diagnostics[], CompileMode}`

## 6. Modelo Canônico da IR (`SqlToNodeIR`)

### 6.1 Versionamento e serialização

- IR **DEVE** conter `IrVersion` semântico.
- Mudança breaking **DEVE** incrementar major.
- Snapshots **DEVE(M)** serializar JSON canônico estável (Seção 27).

### 6.2 Estrutura raiz

```text
SqlToNodeIR
  - IrVersion: string (obrigatório)
  - QueryId: string (obrigatório)
  - SourceHash: string (obrigatório)
  - Dialect: SqlDialect (obrigatório)
  - FeatureFlags: string[] (obrigatório)
  - Query: QueryExpr (obrigatório)
  - SymbolTable: SymbolTableModel (obrigatório)
  - Diagnostics: IrDiagnostic[] (obrigatório)
  - Metrics: IrMetrics (obrigatório)
  - IdGenerationMeta: IdGenerationMeta (obrigatório)
```

### 6.3 Entidades e invariantes

#### 6.3.1 QueryExpr

- `SelectItems[]` (obrigatório, tamanho ≥ 1)
- `FromSource` (obrigatório)
- `Joins[]` (obrigatório, pode vazio)
- `WhereExpr?`
- `GroupBy[]`
- `HavingExpr?`
- `OrderBy[]`
- `LimitOrTop?`
- `SetOperations[]` (P2)

Invariantes:

1. `HavingExpr` só existe com agregação válida.
2. `OrderBy` por alias referencia projeção existente ou marca `Unresolved`.
3. Toda referência de coluna possui `ResolutionStatus` explícito.

#### 6.3.2 SourceExpr

Subtipos:

- `TableRef{database?, schema?, table, alias?, sourceId}`
- `SubqueryRef{query, alias, sourceId}`
- `CteRef{name, alias?, sourceId}`

Invariantes:

- `sourceId` único por escopo.
- `SubqueryRef` abre escopo novo.
- `CteRef` inválido gera erro se CTE não visível.

#### 6.3.3 JoinExpr

- `joinId` (obrigatório)
- `joinType` (`Inner|Left|Right|Full|Cross`)
- `rightSource` (obrigatório)
- `onExpr?`
- `ordinal` (obrigatório)

Invariantes:

1. `ordinal` preserva ordem textual.
2. `Cross` não possui `onExpr`.
3. join não-`Cross` sem `onExpr` é erro fatal.

#### 6.3.4 SelectItemExpr

- `selectItemId` (obrigatório)
- `expression` (obrigatório)
- `AliasMeta` (obrigatório)
- `ordinal` (obrigatório)
- `SemanticType` (obrigatório)
- `SourceSpan` (obrigatório)

#### 6.3.5 Expression hierarchy

- `LogicalExpr`
- `NotExpr`
- `ComparisonExpr`
- `InExpr`
- `BetweenExpr`
- `LikeExpr`
- `IsNullExpr`
- `CaseExpr`
- `FunctionExpr`
- `ColumnRefExpr`
- `LiteralExpr`
- `StarExpr`
- `CastExpr`

Toda expressão **DEVE** conter:

- `exprId`
- `SourceSpan?`
- `SemanticType`
- `ResolutionStatus`
- `TraceMeta`

#### 6.3.6 AliasMeta

- `OriginalAlias?`
- `NormalizedAlias`
- `DisplayAlias`
- `NormalizationRule`
- `NormalizationLossFlags[]`

Invariantes:

1. `NormalizedAlias` único no escopo de projeção.
2. Colisão resolve por sufixo determinístico `_<n>` + diagnóstico.

#### 6.3.7 SourceSpan

`{startLine,startColumn,endLine,endColumn,sourceFragmentHash}`.

### 6.4 Estado de resolução

- `Resolved`
- `Partial`
- `Ambiguous`
- `Unresolved`

Regras:

- `Ambiguous` em `WHERE`/`JOIN ON`/`HAVING` **DEVE FALHAR**.
- `Unresolved` em projeção **PODE** gerar parcial somente com subgrafo degradado marcado (Seção 13).

### 6.5 Geração de IDs estáveis (norma obrigatória)

IDs da IR **DEVE(M)** ser determinísticos para o mesmo SQL normalizado e mesma configuração de dialeto/feature flags.

É PROIBIDO usar GUID randômico para IDs semânticos.

#### 6.5.1 Função base de ID

`StableId = base32(lowercase(sha256(payload_canonico))).substring(0, 16)`

Onde `payload_canonico` é string UTF-8 com:

- tipo de entidade
- caminho estrutural
- ordinais
- spans normalizados
- hash de fragmento AST normalizado

#### 6.5.2 Regras por entidade

- `QueryId`: `Q|Dialect|SourceHash|FeatureFlagsSorted`
- `joinId`: `J|QueryId|JoinOrdinal|JoinType|RightSourceKey|OnExprAstHash`
- `selectItemId`: `S|QueryId|SelectOrdinal|ExprAstHash|NormalizedAlias`
- `exprId`: `E|QueryId|ExprPath|NodeKind|ExprAstHash|ParentExprId?`
- `sourceId`: `R|QueryId|ScopePath|SourceOrdinal|SourceSignature`
- `scopeId`: `SC|QueryId|ScopePath`

#### 6.5.3 Compatibilidade de snapshot

- Mesmo fixture + mesma versão de parser AST **DEVE** gerar IDs idênticos.
- Alteração de algoritmo de ID **DEVE** incrementar `IrVersion` major ou incluir `IdSchemeVersion` e migração explícita.

### 6.6 Nós sintéticos e spans derivados na IR

Quando uma entidade IR não tiver span direto textual, **DEVE** registrar:

- `SyntheticNode: true`
- `SyntheticOriginReason` (`Normalization`, `CompatibilityMigration`, `FallbackProjection`, `DerivedAggregation`, `LegacyUpgrade`, `Other:<code>`)
- `DerivedFromExprIds[]` (não vazio quando aplicável)
- `DerivedFromSpans[]` (quando múltiplas origens)

Nenhuma entidade semântica relevante **PODE** existir sem origem auditável.

## 7. Symbol Table Normativa

### 7.1 Responsabilidades

A symbol table **É OBRIGATÓRIA** e **DEVE**:

1. registrar aliases de fontes
2. registrar aliases de projeção
3. controlar escopos (`Root`, `Cte`, `Subquery`)
4. resolver colunas qualificadas e não qualificadas
5. reportar colisões, ambiguidades e shadowing

### 7.2 Modelo mínimo

```text
SymbolTableModel
  - Scopes[]
Scope
  - ScopeId
  - ScopeType
  - ParentScopeId?
  - SourceSymbols: map<normalizedKey, SourceSymbol[]>
  - ProjectionSymbols: map<normalizedKey, ProjectionSymbol[]>
```

### 7.3 Chaves de lookup

- qualificada: `qualifier.column`
- não qualificada: `column`
- normalização case-insensitive conforme dialeto

### 7.4 Regras de resolução

1. qualificada resolve somente no alias informado
2. não qualificada com múltiplas candidatas = `Ambiguous`
3. símbolo local tem precedência sobre ancestral
4. CTE local prevalece sobre objeto físico homônimo
5. subquery expõe somente suas projeções

### 7.5 Falhas obrigatórias

- alias de fonte duplicado no mesmo escopo
- coluna ambígua em contexto booleano estrutural
- referência a CTE inexistente

## 8. Normalização de Identificadores e Alias Textual

### 8.1 Regra obrigatória

Para alias textual, o import **DEVE** preencher:

1. `OriginalAlias`
2. `NormalizedAlias` pela convenção ativa
3. `DisplayAlias`

Sem convenção ativa resolvida, **DEVE** aplicar fallback `snake_case_ascii`.

### 8.2 Fallback `snake_case_ascii`

Passos obrigatórios:

1. remover aspas externas
2. remover diacríticos
3. mapear separadores para `_`
4. colapsar `_` repetidos
5. lowercase
6. prefixar `col_` se primeiro caractere não alfabético

### 8.3 Colisão

Colisão de alias normalizado **DEVE**:

- sufixar `_<n>` por ordem de projeção
- gerar diagnóstico `SQLIMP_0102_ALIAS_NORMALIZATION_COLLISION`

## 9. Tipagem Semântica e Inferência

### 9.1 Tipos canônicos

`Unknown`, `Null`, `Boolean`, `Integer`, `Decimal`, `Text`, `Date`, `DateTime`, `Time`, `Binary`, `Json`, `Guid`

### 9.2 Regras obrigatórias

1. `LiteralExpr.raw` preserva texto original.
2. `LiteralExpr.normalized` contém forma canônica quando inferível.
3. `NULL` literal = tipo `Null` + `isNullLiteral=true`.
4. comparação não textual não força coerção textual.
5. propagação de `Unknown` deve ser explícita.

### 9.3 Criação de nós `Value*`

IR→Graph **DEVE** criar `ValueBoolean`, `ValueNumber`, `ValueText`, `ValueDate`, `ValueDateTime`, `ValueNull` conforme inferência.

Se não inferível: `ValueGeneric` + diagnóstico `SQLIMP_0601_TYPE_INFERENCE_FALLBACK`.

### 9.4 Coerções

- coerção implícita somente quando regra do dialeto existir
- coerção aplicada deve ser registrada em metadados

## 10. Funções SQL: Classificação, Catálogo e Regras

### 10.1 Classes de função

- `Canonical`
- `DialectSpecific`
- `GenericPreserved`
- `Unsupported`

### 10.2 Regras gerais

1. `Canonical` **DEVE** mapear `canonicalName`.
2. `DialectSpecific` **DEVE** preservar nome original + classe.
3. `GenericPreserved` **SOMENTE PODE** ocorrer em contexto permitido (Seção 10.4).
4. `Unsupported` em contexto booleano estrutural **DEVE FALHAR**.

### 10.3 Catálogo inicial normativo de funções suportadas

| Função | Classe | Suporte | Comportamento esperado | Impacto IR | Impacto Graph | Equivalência esperada |
|---|---|---|---|---|---|---|
| `COALESCE` | Canonical | S | selecionar primeiro não-nulo | `FunctionExpr(canonicalName=COALESCE)` | `FunctionNode` tipado | Total/Tolerant |
| `ISNULL` | DialectSpecific (canon para COALESCE opcional) | P | preservar semântica SQL Server de 2 args | `FunctionExpr(name=ISNULL,class=DialectSpecific)` | `FunctionNode` tipado | Tolerant |
| `NULLIF` | Canonical | S | retornar null quando args iguais | `FunctionExpr(canonicalName=NULLIF)` | `FunctionNode` tipado | Total/Tolerant |
| `UPPER` | Canonical | S | transformação textual maiúscula | `FunctionExpr(canonicalName=UPPER)` | `FunctionNode` tipado | Total |
| `LOWER` | Canonical | S | transformação textual minúscula | `FunctionExpr(canonicalName=LOWER)` | `FunctionNode` tipado | Total |
| `LEN` | DialectSpecific | P | tamanho textual SQL Server | `FunctionExpr(name=LEN,class=DialectSpecific)` | `FunctionNode` tipado | Tolerant |
| `LENGTH` | Canonical | S | tamanho textual canônico | `FunctionExpr(canonicalName=LENGTH)` | `FunctionNode` tipado | Total |
| `COUNT` | Canonical agregada | S | agregação cardinalidade | `FunctionExpr(canonicalName=COUNT)` | `AggregateNode` ou função agregada | Total/Tolerant |
| `SUM` | Canonical agregada | S | soma numérica | idem | idem | Total/Tolerant |
| `AVG` | Canonical agregada | S | média numérica | idem | idem | Total/Tolerant |
| `MIN` | Canonical agregada | S | mínimo | idem | idem | Total/Tolerant |
| `MAX` | Canonical agregada | S | máximo | idem | idem | Total/Tolerant |
| `FORMAT` | DialectSpecific | P | formatação textual dependente de cultura/dialeto | `FunctionExpr(name=FORMAT,class=DialectSpecific)` | `FunctionNode` tipado ou preservado | Tolerant/Partial |

Legenda de suporte: `S` = suportado completo na fase; `P` = suportado parcialmente.

### 10.4 Limites rígidos de `GenericPreserved`

`GenericPreserved` **SOMENTE PODE** ser usado quando:

1. a função está em projeção (`SELECT`) sem papel estrutural em cardinalidade/filtragem/join
2. a expressão pode ser serializada no graph de forma rastreável
3. o import seja classificado como `Partial` ou, no máximo, `EquivalentTolerant` (nunca `EquivalentTotal`)

`GenericPreserved` **DEVE FALHAR** quando usada em:

- `WHERE`
- `JOIN ON`
- `HAVING`
- `GROUP BY` (quando altera agrupamento)
- `ORDER BY` que define requisito funcional de equivalência estrita da feature testada

SQL recompilado com `GenericPreserved`:

- **PODE** ser emitido se função estiver apenas em projeção e sem erro fatal
- **NÃO PODE** ser emitido quando a função genérica estiver em contexto booleano estrutural

Diagnósticos obrigatórios:

- `SQLIMP_0401_FUNCTION_GENERIC_PRESERVED` (parcial permitido)
- `SQLIMP_0403_FUNCTION_GENERIC_FORBIDDEN_CONTEXT` (erro fatal)

## 11. Regras Normativas de JOIN

1. ordem de joins preservada (`ordinal`)
2. tipo preservado exatamente
3. predicado `ON` preservado no join correspondente
4. proibido mover `ON` para `WHERE` sem prova formal
5. self-join exige aliases independentes
6. join com subquery exige escopo isolado
7. reconstrução de join não pode ser textual heurística primária

Falha fatal quando:

- tipo de join muda
- ordem de join com impacto muda
- predicado `ON` perde semântica

## 12. Matriz Normativa de Suporte SQL por Camada

### 12.1 Legenda por etapa

- `S`: suportado
- `P`: parcial
- `N`: não suportado na fase

Colunas:

- `AST`: parseável pela camada de parser
- `IR`: representável na IR canônica
- `Graph`: materializável no grafo sem perda estrutural crítica
- `SQL`: recompilável pelo compiler atual
- `Equiv`: classe máxima permitida (`Total`, `Tolerant`, `Partial`, `NotExpected`)

### 12.2 Matriz

| Recurso SQL | AST | IR | Graph | SQL | Equiv | Regra operacional |
|---|---|---|---|---|---|---|
| SELECT simples | S | S | S | S | Total | pipeline completo obrigatório |
| SELECT com expressão | S | S | S | S | Total/Tolerant | expressão vira subgrafo |
| Alias textual | S | S | S | S | Total/Tolerant | normalizar + auditar |
| CASE | S | S | S | S | Total/Tolerant | sem descarte de branch |
| Funções canônicas catálogo | S | S | S | S | Total/Tolerant | conforme Seção 10.3 |
| Funções dialeto catálogo | S | S | P | P | Tolerant/Partial | semântica dependente de dialeto |
| Função genérica desconhecida em projeção | S | S | P | P | Partial/Tolerant | permitido só em projeção |
| Função genérica em WHERE/JOIN/HAVING | S | P | N | N | NotExpected | erro fatal obrigatório |
| Subquery em IN | S | S | S | S | Total/Tolerant | escopo obrigatório |
| Subquery em FROM | S | S | S | S | Total/Tolerant | alias obrigatório no SQL de entrada |
| CTE não-recursiva | S | S | P | P | Tolerant/Partial | parcial para limitações avançadas |
| CTE recursiva | P | P | N | N | NotExpected | unsupported nesta fase |
| GROUP BY simples | S | S | P | P | Tolerant/Partial | expressão complexa pode degradar |
| HAVING | S | S | P | P | Tolerant/Partial | ambiguidade estrutural é fatal |
| ORDER BY por coluna | S | S | S | S | Total/Tolerant | ordenação preservada |
| ORDER BY por alias | S | S | P | P | Tolerant/Partial | alias não resolvido => parcial/falha |
| JOIN INNER/LEFT/RIGHT/FULL | S | S | S | S | Total/Tolerant | preservar tipo/ordem/ON |
| CROSS JOIN | S | S | S | S | Total/Tolerant | proibido ON |
| UNION/INTERSECT/EXCEPT | S | P | N | N | NotExpected | P2 |
| Window Functions | S | P | N | N | NotExpected | unsupported nesta fase |
| APPLY | P | P | N | N | NotExpected | unsupported nesta fase |
| SELECT `*` (não qualificado) | S | S | P | P | Tolerant/Partial | regras da Seção 12.3 |
| SELECT `t.*` (qualificado) | S | S | P | P | Tolerant/Partial | regras da Seção 12.3 |

### 12.3 Contrato fechado de `SELECT *`

#### 12.3.1 Regra geral

`SELECT *` **NÃO DEVE** ficar ambíguo entre expansão e preservação. A decisão é determinística pelas regras abaixo.

#### 12.3.2 Expansão obrigatória

`*` **DEVE** ser expandido para lista explícita de colunas quando, simultaneamente:

1. metadados de colunas de todas as fontes relevantes estão disponíveis e confiáveis
2. scope bindings estão resolvidos
3. não há bloqueio por feature flag de expansão

Resultado:

- IR representa itens explícitos (`SelectItemExpr[]` sem `StarExpr` efetivo)
- Graph persiste colunas explícitas
- SQL recompilado **NÃO DEVE** usar `*`
- Classe alvo: `EquivalentTotal` ou `EquivalentTolerant`

#### 12.3.3 Preservação controlada como `StarExpr`

`StarExpr` **PODE** ser preservado quando qualquer condição de expansão obrigatória falhar.

Resultado:

- IR mantém `StarExpr` com `ResolutionStatus=Partial`
- Graph **DEVE** marcar nó/projeção como degradado (Seção 13)
- SQL recompilado **PODE** reemitir `*` somente se origem também era `*`
- Classe máxima: `EquivalentTolerant` (nunca `EquivalentTotal`)

#### 12.3.4 Falha obrigatória para `*`

Import **DEVE FALHAR** quando:

- `*` aparece em contexto em que metadata ausente impede segurança mínima e também impede preservação rastreável
- `t.*` referencia alias não resolvido

#### 12.3.5 `t.*` em subquery/alias

- `t.*` **DEVE** vincular a um único source alias resolvido.
- Em subquery, `t.*` refere-se ao escopo interno da subquery, não ao externo.
- Se alias existir em múltiplos escopos visíveis, regra de escopo local prevalece; se ainda ambíguo, falha.

#### 12.3.6 Persistência no grafo

- Novos grafos **NÃO DEVEM** persistir `*` quando houve expansão possível.
- Persistência de `*` é permitida apenas com `SyntheticOriginReason=StarPreservedDueToMissingMetadata` e diagnóstico obrigatório.

## 13. Política Formal de Falha, Parcialidade e Fallback

### 13.1 Categorias

- `FATAL_ERROR`
- `PARTIAL_IMPORT`
- `WARNING`
- `UNSUPPORTED_FEATURE`
- `FALLBACK_ACTIVATED`
- `AMBIGUITY_UNRESOLVED`
- `NORMALIZATION_LOSS`

### 13.2 Matriz de decisão

| Categoria | Continua import? | Gera canvas? | Emite SQL? | Status final permitido |
|---|---|---|---|---|
| FATAL_ERROR | Não | Não | Não | Failed |
| AMBIGUITY_UNRESOLVED (booleano estrutural) | Não | Não | Não | Failed |
| UNSUPPORTED_FEATURE (somente projeção) | Sim | Sim | Sim | Partial |
| PARTIAL_IMPORT | Sim | Sim | Sim | Partial |
| FALLBACK_ACTIVATED | Sim | Sim | Sim | Partial/Tolerant |
| WARNING | Sim | Sim | Sim | Total/Tolerant/Partial |
| NORMALIZATION_LOSS | Sim | Sim | Sim | Tolerant/Partial |

### 13.3 Regras de fallback

1. fallback só para recurso marcado parcial
2. fallback sempre com diagnóstico
3. fallback sem diagnóstico = não conformidade
4. fallback regex não pode sobrescrever semântica AST/IR já resolvida

### 13.4 Contrato visual/estrutural de import parcial (obrigatório)

Quando o graph for parcial, o modelo **DEVE** marcar elementos degradados com:

- `NodeDegradationState`: `None|Partial|Unsupported|Fallback`
- `DegradationReasonCode`
- `RelatedDiagnosticCodes[]`
- `SyntheticNode` (quando aplicável)

Regras:

1. Nós degradados **PODEM** coexistir com nós normais.
2. Conexões parciais **DEVE(M)** ser marcadas por metadado equivalente.
3. UI/modelo **NÃO DEVE(M)** exibir grafo parcial como semanticamente completo.
4. Nó degradado em contexto crítico booleando estrutural **NÃO PODE** compilar SQL.
5. Nó degradado em projeção **PODE** compilar SQL parcial com diagnóstico.

## 14. Regras de Fallback Controlado

Fallback controlado **DEVE** incluir:

- `FallbackScope` (`Parsing`, `FunctionMapping`, `StarHandling`, `LegacyValueMap`, `Other`)
- `FallbackReasonCode`
- `FallbackInputFragmentHash`
- `FallbackOutputMarking`

Sem esses campos, fallback é inválido.

## 15. Equivalência Semântica (Definição Formal)

### 15.1 Classes

- `EQUIVALENT_TOTAL`
- `EQUIVALENT_TOLERANT`
- `PARTIAL`
- `NOT_EQUIVALENT`

### 15.2 Regras de classificação

#### 15.2.1 `EQUIVALENT_TOTAL`

Exige:

1. fontes, joins, filtros, projeções e limites semanticamente equivalentes
2. ausência de nós degradados
3. ausência de `GenericPreserved`
4. ausência de `StarExpr` preservada por falta de metadata

#### 15.2.2 `EQUIVALENT_TOLERANT`

Permite:

- diferença de formatação textual
- alias normalizado
- canonicalização de função
- `*` expandido para colunas equivalentes

Não permite:

- import parcial/degradado
- função genérica em contexto crítico

#### 15.2.3 `PARTIAL`

Aplica quando:

- há ao menos um elemento degradado
- há fallback com perda controlada
- há `GenericPreserved` em projeção
- há `StarExpr` preservada por metadata insuficiente

Import parcial **NUNCA PODE** ser classificado como `EQUIVALENT_TOTAL`.

#### 15.2.4 `NOT_EQUIVALENT`

Aplica quando:

- predicado essencial é perdido/alterado
- join perde tipo/ordem/ON
- ambiguidade crítica não resolvida
- função genérica/unsupported em contexto crítico
- subgrafo degradado crítico é tratado como completo

### 15.3 `ImportOutcome`

```text
ImportOutcome
  - Status: EquivalentTotal|EquivalentTolerant|Partial|Failed
  - EquivalenceClass: EQUIVALENT_TOTAL|EQUIVALENT_TOLERANT|PARTIAL|NOT_EQUIVALENT
  - HasDegradedGraph: bool
  - BlockingDiagnostics[]
  - NonBlockingDiagnostics[]
```

## 16. Mapeamento Normativo IR → Graph

### 16.1 Regras gerais

1. expressão suportada vira nó/subgrafo explícito
2. proibido colapsar expressão suportada em string opaca
3. nó genérico só em casos parciais permitidos

### 16.2 Mapeamento mínimo

| IR | Node alvo | Regras |
|---|---|---|
| `TableRef` | `TableSource` | pins inferidos com rastreabilidade |
| `JoinExpr` | `Join*` | tipo/ordem/ON obrigatórios |
| `ComparisonExpr` | `Comparison` | left/op/right explícitos |
| `LogicalExpr` | `And`/`Or` | variádico com ordem estável |
| `NotExpr` | `Not` | encapsulamento obrigatório |
| `InExpr` | `In` | lista literal ou subquery |
| `BetweenExpr` | `Between` | low/high explícitos |
| `LikeExpr` | `Like` | pattern/negate |
| `IsNullExpr` | `IsNull`/`IsNotNull` | conforme flag |
| `CaseExpr` | subgrafo condicional / `ValueMap` | sem descarte de branches |
| `FunctionExpr` | `FunctionNode`/agregado | conforme catálogo e classe |
| `LiteralExpr` | `Value*` | tipo inferido ou `ValueGeneric` parcial |
| `ColumnRefExpr` | `ColumnRef`/binding | resolução explícita |
| `StarExpr` | projeção estrela marcada | somente conforme Seção 12.3 |

### 16.3 Rastreabilidade obrigatória de nós

Todo nó relevante **DEVE** conter:

- `TraceQueryId`
- `TraceExprId?`
- `TraceSourceSpan?`
- `SyntheticNode`
- `SyntheticOriginReason?`
- `DerivedFromExprIds[]?`
- `NodeDegradationState`

## 17. Overhaul Normativo do `ValueMap` (Graph-First)

### 17.1 Objetivo vinculante

`ValueMap` compila primariamente por arestas de grafo, não por dicionário textual.

### 17.2 Família de nós

- `ValueMap(input, rules*, default?) -> result`
- `ValueMapRule(match, then) -> rule`
- `ValueMapDefault(value) -> default`
- `ValueMapNormalize(value:text) -> normalized` (P1)

### 17.3 Semântica de avaliação

1. ordenar por `priority`; empate por ordem de conexão
2. primeira regra válida vence
3. sem match usa `default`; ausente => `NULL` (a menos de política explícita)
4. `mode=Exact` não força coerção textual

### 17.4 Compatibilidade legada

Fase A:

- `ValueMap` legado lido
- compilador tenta graph-first; legado é fallback

Fase B:

- UI edita `ValueMapRule`
- validação estrutural sem `src/dst`

Fase C:

- novos grafos não persistem `src/dst`/`map_key_*`
- leitura legada mantém compatibilidade retroativa

### 17.5 Validação estrutural obrigatória

`ValueMap` válido requer:

1. `input` conectado
2. `rules` com pelo menos 1 regra
3. cada regra com `match` e `then`

Violação em novo grafo = erro fatal.

## 18. Invariantes Formais do Sistema

1. nenhuma projeção suportada é descartada silenciosamente
2. nenhum fallback sem diagnóstico
3. toda coluna possui status de resolução explícito
4. todo join preserva tipo, ordem e predicado
5. toda normalização é auditável
6. regex não substitui semântica primária
7. todo import possui `ImportOutcome`
8. diagnóstico sempre possui código estável e severidade
9. `QueryId` é preservado ao longo de todas as camadas
10. import parcial nunca é classificado como total
11. nó degradado crítico nunca é tratado como semanticamente fechado

## 19. Observabilidade, Telemetria e Diagnóstico

### 19.1 Métricas obrigatórias

- `parse_ms`
- `ast_to_ir_ms`
- `ir_to_graph_ms`
- `graph_to_sql_ms`
- `total_ms`
- `fallback_count`
- `unsupported_feature_count`
- `ambiguity_count`
- `normalized_alias_count`
- `degraded_node_count`
- `partial_item_count`
- `roundtrip_divergence_count`

### 19.2 Contexto mínimo de logs

- `correlation_id`
- `query_id`
- `import_mode` (`legacy_regex`, `ast_ir_primary`, `mixed_fallback`)
- `dialect`
- `feature_flags`
- `equivalence_class`

### 19.3 Payload mínimo de diagnóstico

```json
{
  "code": "SQLIMP_XXXX",
  "category": "FATAL_ERROR|PARTIAL_IMPORT|WARNING|UNSUPPORTED_FEATURE|FALLBACK_ACTIVATED|AMBIGUITY_UNRESOLVED|NORMALIZATION_LOSS",
  "severity": "Error|Warning|Info",
  "message": "texto técnico determinístico",
  "clause": "SELECT|FROM|JOIN|WHERE|GROUP_BY|HAVING|ORDER_BY|LIMIT|FUNCTION|VALUEMAP|STAR",
  "sourceSpan": { "startLine": 1, "startColumn": 1, "endLine": 1, "endColumn": 10 },
  "sqlFragment": "...",
  "action": "abort|continue_partial|fallback",
  "recommendedAction": "...",
  "queryId": "...",
  "correlationId": "..."
}
```

### 19.4 Catálogo inicial de diagnósticos

| Código | Categoria | Significado |
|---|---|---|
| `SQLIMP_0001_PARSE_FATAL` | FATAL_ERROR | parse fatal sem fallback válido |
| `SQLIMP_0002_AST_UNSUPPORTED` | UNSUPPORTED_FEATURE | constructo AST não suportado |
| `SQLIMP_0101_ALIAS_NORMALIZATION_LOSS` | NORMALIZATION_LOSS | perda na normalização de alias |
| `SQLIMP_0102_ALIAS_NORMALIZATION_COLLISION` | WARNING | colisão de alias normalizado |
| `SQLIMP_0201_COLUMN_AMBIGUOUS` | AMBIGUITY_UNRESOLVED | coluna ambígua |
| `SQLIMP_0202_COLUMN_UNRESOLVED` | PARTIAL_IMPORT | coluna não resolvida não-crítica |
| `SQLIMP_0301_JOIN_SEMANTIC_DRIFT` | FATAL_ERROR | drift semântico de join |
| `SQLIMP_0401_FUNCTION_GENERIC_PRESERVED` | PARTIAL_IMPORT | função genérica preservada em projeção |
| `SQLIMP_0402_FUNCTION_UNSUPPORTED` | UNSUPPORTED_FEATURE | função não suportada |
| `SQLIMP_0403_FUNCTION_GENERIC_FORBIDDEN_CONTEXT` | FATAL_ERROR | função genérica em contexto crítico |
| `SQLIMP_0501_FALLBACK_REGEX_USED` | FALLBACK_ACTIVATED | fallback regex ativado |
| `SQLIMP_0601_TYPE_INFERENCE_FALLBACK` | PARTIAL_IMPORT | fallback de tipagem |
| `SQLIMP_0701_PROJECTION_DROPPED_BLOCKED` | FATAL_ERROR | descarte bloqueado de projeção suportada |
| `SQLIMP_0801_VALUEMAP_LEGACY_COMPAT` | WARNING | compatibilidade legada ValueMap |
| `SQLIMP_0802_VALUEMAP_STRUCT_INVALID` | FATAL_ERROR | estrutura inválida ValueMap |
| `SQLIMP_0851_STAR_PRESERVED_MISSING_METADATA` | PARTIAL_IMPORT | `*` preservado por metadata insuficiente |
| `SQLIMP_0852_STAR_ALIAS_UNRESOLVED` | FATAL_ERROR | `t.*` sem alias resolvido |
| `SQLIMP_0901_ROUNDTRIP_NOT_EQUIVALENT` | FATAL_ERROR | round-trip não equivalente |

## 20. Estratégia de Testes (Normativa)

### 20.1 Tipos obrigatórios

1. unitários
2. integração
3. contrato (snapshot)
4. regressão

### 20.2 Cobertura mínima obrigatória

- aliases textuais e colisão
- `WHERE` lógico completo
- joins múltiplos/self/subquery
- `CASE`, funções e literais tipados
- cenários parciais e falhas esperadas
- fallback controlado
- `ValueMap` graph-first + legado
- `SELECT *` expandido e preservado

### 20.3 Gates por fase

- 100% verde nos testes da fase
- 0 regressão crítica aberta
- diagnósticos novos com teste dedicado
- snapshot churn revisado e justificado

## 21. Plano de Adoção e Rollout

### 21.1 Feature flags

- `SqlImport.AstIrPrimary`
- `SqlImport.RegexFallbackEnabled`
- `SqlImport.ValueMapGraphFirst`
- `SqlImport.RoundTripEquivalenceCheck`

### 21.2 Fases e gates

#### Fase 1 — Infra IR + Symbol Table

- contrato IR estável
- resolução `SELECT/FROM/JOIN/WHERE`

Gate:

- >=95% baseline P0 em `EquivalentTotal` ou `EquivalentTolerant`

#### Fase 2 — AST/IR primário

- ativação em dev
- comparação com legado

Gate:

- fallback regex <20% P0

#### Fase 3 — projeções complexas

- `CASE`, funções, alias textual

Gate:

- cobertura >=90% da suíte alvo

#### Fase 4 — joins hardening

- cadeia join via IR

Gate:

- `SQLIMP_0301` = 0 na suíte crítica

#### Fase 5 — ValueMap convergência

- graph-first primário
- legado somente leitura/fallback

Gate:

- novos grafos sem parâmetros legados

#### Fase 6 — legado fora do caminho primário

- regex apenas fallback explicitamente permitido

Estabilidade:

- fallback global <5% por 4 ciclos
- sem regressão crítica por 2 releases

## 22. Critérios de Aceite Final (Congelamento)

Para congelar:

1. invariantes da Seção 18 cumpridos
2. matriz por camada da Seção 12 cumprida
3. catálogo de diagnósticos ativo com testes
4. equivalência classificada corretamente por regra
5. `ValueMap` graph-first com compatibilidade legada validada
6. regex fora do caminho primário
7. ausência de lacuna normativa relevante em comportamentos críticos

## 23. Compatibilidade com Convenções de Código e Exceções

Implementação **DEVE** seguir:

- `docs/CODE_CONVENTIONS.md`
- `docs/EXCEPTION_HANDLING_STRATEGY.md`

Regras adicionais:

- `string.IsNullOrWhiteSpace` para entradas textuais livres
- nullable annotations explícitas
- evitar `!` sem justificativa
- logging estruturado com correlação

## 24. Apêndice A — Suporte Parcial

Quando recurso for parcial:

1. import só continua se segurança semântica mínima for preservada
2. outcome obrigatório: `Partial`
3. diagnóstico parcial/unsupported obrigatório
4. SQL parcial pode ser emitido apenas em contextos permitidos

## 25. Apêndice B — Fixtures Normativas Mínimas

A suíte **DEVE** conter ao menos uma fixture canônica por classe abaixo:

1. múltiplos joins encadeados com aliases (`INNER` + `LEFT` + filtros)
2. self-join com aliases distintos
3. subquery em `IN`
4. subquery em `FROM` com alias obrigatório
5. alias textual com colisão de normalização
6. `CASE WHEN ... ELSE ... END`
7. função dialeto-específica (`FORMAT`)
8. `SELECT *` com metadata disponível (expansão obrigatória)
9. `SELECT *` sem metadata suficiente (preservação parcial)
10. `SELECT t.*` com alias resolvido
11. `SELECT t.*` com alias não resolvido (falha)
12. ambiguidade fatal de coluna em `WHERE`/`JOIN ON`
13. função genérica preservada em projeção (parcial)
14. função genérica em `WHERE` (falha)
15. cenário legado `ValueMap` com migração em memória
16. `ValueMap` graph-first com múltiplas regras e fallback

## 26. Apêndice C — Snapshot Contract Endurecido

Snapshots de IR/Graph/Outcome **DEVE(M)**:

1. usar ordenação estável de objetos e arrays por chaves determinísticas
2. excluir campos não determinísticos (`timestamp`, `runtime ids`, `memory addresses`)
3. serializar IDs derivados estáveis conforme Seção 6.5
4. serializar diagnósticos ordenados por `severity > code > span > message`
5. serializar spans (`SourceSpan` e derivados) de forma completa
6. serializar estado de degradação (`NodeDegradationState`, `DegradationReasonCode`)
7. incluir `IrVersion` e `IdSchemeVersion`
8. manter compatibilidade explícita entre versões (snapshot migration notes quando necessário)

Qualquer alteração de snapshot sem justificativa de contrato é não conformidade.

## 27. Apêndice D — Política de Evolução desta Spec

- alteração normativa breaking requer incremento major
- alteração de catálogo de funções requer atualização de matriz por camada
- alteração de algoritmo de IDs requer atualização de `IdSchemeVersion`
- toda mudança requer atualização de testes de contrato e fixtures aplicáveis

---

**Resumo vinculante**: a implementação SQL import no DBWeaver **DEVE** operar com AST/IR como semântica primária, contracts de camada rígidos, equivalência formal, suporte por camada explícito, `SELECT *` totalmente fechado, `GenericPreserved` estritamente limitado, IDs estáveis determinísticos, rastreabilidade de nós sintéticos/degradados e `ValueMap` graph-first com compatibilidade retroativa controlada. Desvio dessas regras caracteriza não conformidade desta especificação.
