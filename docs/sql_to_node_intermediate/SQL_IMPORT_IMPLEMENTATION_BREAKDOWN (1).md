# Implementation Breakdown — SQL Import Intermediate Layer

## 0. Metadados do Documento

- **Nome**: SQL Import Implementation Breakdown
- **Versão**: 1.0.0
- **Data**: 2026-04-13
- **Status**: Pronto para execução incremental
- **Base normativa**: `SQL_TO_NODE_INTERMEDIATE_LAYER_SPEC` v2.1.0-normativa
- **Escopo de implementação**: `src/DBWeaver`, `src/DBWeaver.UI`, `tests/VisualSqlArchitect.Tests`

---

## 1. Objetivo Operacional

Este documento converte a especificação normativa da camada intermediária de importação SQL em um plano de implementação executável, incremental e controlado por feature flags.

O objetivo é decompor a spec em entregas reais de engenharia, com:

- ordem recomendada de execução
- épicos e milestones
- arquivos prováveis a tocar
- dependências entre etapas
- riscos por fase
- critérios objetivos de pronto
- backlog por prioridade
- primeiros passos práticos

Este documento **NÃO substitui** a especificação normativa. Ele existe para operacionalizar sua implementação.

---

## 2. Premissas Vinculantes

Toda implementação derivada deste breakdown **DEVE** respeitar integralmente a spec normativa base.

Em particular:

1. AST/IR **DEVE** ser a fonte semântica primária.
2. Regex **NÃO DEVE** ser mecanismo semântico primário.
3. Todo fallback **DEVE** ser controlado, auditado e diagnosticado.
4. Toda perda semântica **DEVE** ser explicitamente classificada.
5. Import parcial **NUNCA PODE** ser promovido a equivalência total.
6. `ValueMap` **DEVE** convergir para fluxo graph-first com compatibilidade retroativa controlada.
7. IDs semânticos **DEVE(M)** ser determinísticos.
8. Snapshots **DEVE(M)** ser estáveis e auditáveis.

---

## 3. Estratégia Geral de Execução

A ordem ideal de implementação é a seguinte:

1. infraestrutura transversal
2. IR + IDs + diagnósticos
3. symbol table + resolução semântica
4. AST → IR para recursos P0
5. IR → Graph para recursos P0
6. equivalência e outcome
7. alias textual e normalização
8. `SELECT *` e `t.*`
9. funções SQL
10. projeções complexas (`CASE`, etc.)
11. join hardening
12. `ValueMap` graph-first
13. observabilidade completa
14. testes de contrato, regressão e hardening final

Esta ordem existe para evitar que o time implemente comportamento de alto nível antes de fechar os contratos base.

---

## 4. Macro-Fases do Programa de Implementação

### 4.1 Fase 1 — Fundação Semântica

Objetivo:
- criar contratos básicos, IR, IDs e symbol table

Resultado esperado:
- base confiável para AST → IR e IR → Graph

### 4.2 Fase 2 — Pipeline P0 End-to-End

Objetivo:
- suportar `SELECT/FROM/JOIN/WHERE` com rastreabilidade e classificação

Resultado esperado:
- queries P0 entram em AST, saem em IR, materializam graph e recebem `ImportOutcome`

### 4.3 Fase 3 — Semântica Avançada de Projeção

Objetivo:
- fechar alias textual, `SELECT *`, funções e `CASE`

Resultado esperado:
- projeções complexas deixam de depender de heurística ou descarte silencioso

### 4.4 Fase 4 — Hardening Estrutural

Objetivo:
- eliminar drift semântico de join e endurecer equivalência

Resultado esperado:
- join chain confiável e classificação madura

### 4.5 Fase 5 — Convergência de Produto

Objetivo:
- introduzir `ValueMap` graph-first, observabilidade plena e suíte normativa completa

Resultado esperado:
- pipeline pronto para uso incremental sob feature flags, com telemetria e contrato estável

---

## 5. Épicos de Implementação

## Épico A — Fundação Transversal

### Objetivo

Criar os contratos e tipos base reutilizados por todas as camadas.

### Entregas

- tipos compartilhados de diagnóstico
- tipos compartilhados de outcome
- enums de severidade e categoria
- enums de resolução
- enums de degradação
- contrato de `TraceMeta`
- contrato de `SourceSpan`
- contrato de `IdGenerationMeta`
- helper central de geração de IDs estáveis
- helpers de serialização canônica para snapshot

### Arquivos prováveis a tocar

- `src/DBWeaver/SqlImport/Contracts/*`
- `src/DBWeaver/SqlImport/Diagnostics/*`
- `src/DBWeaver/SqlImport/Tracing/*`
- `src/DBWeaver/SqlImport/Ids/*`
- `tests/VisualSqlArchitect.Tests/Contract/*`

### Dependências

- nenhuma

### Riscos principais

- espalhar contratos base em múltiplos namespaces
- duplicar enums entre core e UI
- usar GUID randômico por conveniência

### Critério de pronto

- contratos base existem e estão centralizados
- tipos podem ser consumidos por AST → IR, IR → Graph e testes
- helper de ID estável possui testes repetidos e determinísticos

---

## Épico B — IR Canônica Versionada

### Objetivo

Materializar `SqlToNodeIR` e seus subtipos segundo a spec.

### Entregas

- `SqlToNodeIR`
- `QueryExpr`
- `SourceExpr`
- `JoinExpr`
- `SelectItemExpr`
- hierarquia de expressões
- `AliasMeta`
- `IrMetrics`
- `IdGenerationMeta`
- suporte a nós sintéticos e origem derivada

### Arquivos prováveis a tocar

- `src/DBWeaver/SqlImport/IR/SqlToNodeIR.cs`
- `src/DBWeaver/SqlImport/IR/Expressions/*`
- `src/DBWeaver/SqlImport/IR/Sources/*`
- `src/DBWeaver/SqlImport/IR/Metadata/*`

### Dependências

- Épico A

### Riscos principais

- IR permissiva demais
- entidades sem spans ou rastreabilidade
- mistura de estado de resolução com texto livre de diagnóstico

### Critério de pronto

- IR serializa em JSON canônico
- invariantes básicos são validáveis
- suporte a `SyntheticNode`, `SyntheticOriginReason` e `DerivedFromExprIds[]` está presente

---

## Épico C — Symbol Table Formal

### Objetivo

Implementar resolução determinística de fontes, aliases, colunas, escopos e shadowing.

### Entregas

- `SymbolTableModel`
- `Scope`
- `SourceSymbol`
- `ProjectionSymbol`
- serviço de resolução
- regras de lookup qualificado e não qualificado
- detecção de ambiguidade crítica
- resolução de CTE e subquery

### Arquivos prováveis a tocar

- `src/DBWeaver/SqlImport/Semantics/SymbolTable/*`
- `src/DBWeaver/SqlImport/Semantics/Resolvers/*`

### Dependências

- Épico B

### Riscos principais

- permitir ambiguidade silenciosa
- resolver colunas no escopo errado
- ignorar precedência de CTE local

### Critério de pronto

- testes cobrindo coluna ambígua, alias duplicado, self join, subquery isolada e CTE inexistente
- contextos críticos falham conforme a spec

---

## Épico D — AST → IR P0

### Objetivo

Implementar o caminho semântico primário para os recursos P0.

### Escopo P0

- `SELECT`
- `FROM`
- `JOIN`
- `WHERE`
- alias textual
- literais
- operadores lógicos
- `IN`
- `BETWEEN`
- `LIKE`
- `IS NULL`

### Entregas

- mapper AST → IR
- preenchimento de spans
- preenchimento de `ResolutionStatus`
- classificação inicial de suporte
- geração de diagnósticos por cláusula

### Arquivos prováveis a tocar

- `Services/SqlImport/SqlParserService.cs`
- `Services/SqlImport/Execution/SqlImportExecutionService.cs`
- `src/DBWeaver/SqlImport/Mappers/AstToIr/*`

### Dependências

- Épicos A, B, C

### Riscos principais

- reaproveitar regex dentro do mapper AST → IR
- “corrigir” semântica no mapper em vez de apenas representá-la
- deixar fallback sem marcação formal

### Critério de pronto

- queries P0 geram IR válida
- ambiguidades críticas falham
- diagnósticos saem com códigos estáveis
- `QueryId` é preservado ao longo do pipeline

---

## Épico E — IR → Graph P0

### Objetivo

Materializar grafo semântico a partir da IR para os recursos críticos de base.

### Escopo

- `TableRef`
- `JoinExpr`
- `ComparisonExpr`
- `LogicalExpr`
- `NotExpr`
- `InExpr`
- `BetweenExpr`
- `LikeExpr`
- `IsNullExpr`
- `LiteralExpr`
- `ColumnRefExpr`

### Entregas

- graph builder por expressão
- marcação de nós degradados
- rastreabilidade SQL ↔ IR ↔ Graph
- bindings explícitos de coluna
- suporte a `Value*`

### Arquivos prováveis a tocar

- `Services/SqlImport/Build/ImportModelToCanvasBuilder.cs`
- `Services/SqlImport/Build/ImportBuildUtilities.cs`
- `src/DBWeaver/SqlImport/GraphBuilders/*`

### Dependências

- Épico D

### Riscos principais

- materializar grafo aproximado demais
- perder `TraceExprId`
- criar nós genéricos onde a spec exige nós específicos

### Critério de pronto

- mapeamentos mínimos P0 implementados
- nós saem com `NodeDegradationState`
- round-trip básico passa nos casos suportados

---

## Épico F — Outcome, Equivalência e Classificação

### Objetivo

Implementar a camada que classifica formalmente o resultado do import.

### Entregas

- `ImportOutcome`
- classificador `EquivalentTotal`
- classificador `EquivalentTolerant`
- classificador `Partial`
- classificador `Failed`
- integração entre diagnósticos, degradação e resultado final

### Arquivos prováveis a tocar

- `src/DBWeaver/SqlImport/Equivalence/*`
- `src/DBWeaver/SqlImport/Outcome/*`

### Dependências

- Épicos D e E

### Riscos principais

- classificar parcial como tolerante
- ignorar `GenericPreserved`
- ignorar estrela preservada por falta de metadata

### Critério de pronto

- classificações respeitam a spec
- existe teste cobrindo promoção proibida de parcial para total

---

## Épico G — Alias Textual e Normalização

### Objetivo

Fechar naming normativo de projeções, exibição e colisão.

### Entregas

- uso da convenção ativa
- fallback `snake_case_ascii`
- `DisplayAlias`
- `NormalizationLossFlags`
- sufixação determinística em colisão

### Arquivos prováveis a tocar

- `Services/SqlImport/SqlImportIdentifierNormalizer.cs`
- `ViewModels/Validation/Conventions/*`
- `src/DBWeaver/SqlImport/Normalization/*`

### Dependências

- Épicos B e D

### Riscos principais

- normalizar em mais de um ponto
- divergência entre import e compilação
- colisão sem diagnóstico obrigatório

### Critério de pronto

- aliases com acento, espaços e colisão passam com resultado determinístico
- SQL recompilado usa alias conforme contrato

---

## Épico H — `SELECT *` e `t.*`

### Objetivo

Implementar o contrato fechado de estrela.

### Entregas

- detecção de metadata disponível
- expansão obrigatória quando possível
- preservação controlada como `StarExpr`
- falha para `t.*` não resolvido
- marcação degradada quando estrela for preservada
- reemissão controlada de `*`

### Arquivos prováveis a tocar

- `src/DBWeaver/SqlImport/StarHandling/*`
- `src/DBWeaver/SqlImport/Mappers/AstToIr/SelectMapper.cs`
- `src/DBWeaver/Nodes/Compilers/*`

### Dependências

- Épicos C, D, E, F

### Riscos principais

- expandir com metadata incompleta
- persistir `*` em grafos novos sem necessidade
- classificar estrela preservada como total

### Critério de pronto

- fixtures cobrindo expansão, preservação, `t.*` resolvido e `t.*` fatal
- diagnósticos específicos estão corretos

---

## Épico I — Funções SQL

### Objetivo

Implementar o catálogo inicial normativo de funções e os limites de `GenericPreserved`.

### Entregas

- classificação por função
- catálogo inicial
- nodes tipados para funções canônicas
- suporte parcial para funções dialeto-específicas
- política rígida para `GenericPreserved`

### Arquivos prováveis a tocar

- `src/DBWeaver/SqlImport/Functions/*`
- `src/DBWeaver/Nodes/Compilers/Functions/*`
- `src/DBWeaver/Nodes/NodeDefinition.cs`

### Dependências

- Épicos B, D, E, F

### Riscos principais

- função desconhecida passar como segura em `WHERE`
- não diferenciar agregadas de escalares
- reclassificar função dialeto-específica como equivalência total indevida

### Critério de pronto

- catálogo mínimo implementado
- `GenericPreserved` só existe em projeção
- contexto booleano estrutural fatal está coberto por teste

---

## Épico J — Projeções Complexas e `CASE`

### Objetivo

Materializar projeções avançadas sem descarte silencioso.

### Entregas

- `CaseExpr`
- subgrafos condicionais
- integração inicial com `ValueMap` quando aplicável
- prevenção de descarte de branches
- tratamento de fallback de projeção

### Arquivos prováveis a tocar

- `src/DBWeaver/SqlImport/Mappers/AstToIr/ProjectionMapper.cs`
- `src/DBWeaver/SqlImport/GraphBuilders/Projection/*`
- `src/DBWeaver/Nodes/Compilers/ConditionalCompiler.cs`

### Dependências

- Épicos D, E, I

### Riscos principais

- reduzir `CASE` a string opaca
- perder `ELSE`
- introduzir `ValueMap` cedo demais sem contrato estável

### Critério de pronto

- fixture de `CASE WHEN ... ELSE ... END`
- nenhum descarte silencioso de branch
- equivalência ao menos tolerante quando aplicável

---

## Épico K — Join Hardening

### Objetivo

Eliminar drift semântico em joins.

### Entregas

- preservação rígida de tipo, ordem e `ON`
- self join com bindings independentes
- join com subquery
- bloqueio de reconstrução textual heurística como caminho primário

### Arquivos prováveis a tocar

- `src/DBWeaver/SqlImport/Mappers/AstToIr/JoinMapper.cs`
- `src/DBWeaver/SqlImport/Semantics/JoinSemantics/*`
- `src/DBWeaver/Nodes/Compilers/Join*`

### Dependências

- Épicos C, D, E

### Riscos principais

- mover semântica de `ON` para `WHERE`
- colapsar aliases em self join
- reordenar joins acidentalmente

### Critério de pronto

- drift semântico de join zera na suíte crítica

---

## Épico L — `ValueMap` Graph-First

### Objetivo

Migrar `ValueMap` para conectividade real de grafo com compatibilidade retroativa controlada.

### Entregas

- novos node types:
  - `ValueMap`
  - `ValueMapRule`
  - `ValueMapDefault`
  - `ValueMapNormalize` em P1
- compilação primária por arestas
- compat legada em memória
- validação estrutural
- persistência nova sem `src/dst` e `map_key_*`

### Arquivos prováveis a tocar

- `src/DBWeaver/Nodes/NodeDefinition.cs`
- `src/DBWeaver/Nodes/NodeTaxonomy.cs`
- `src/DBWeaver/Nodes/Compilers/ConditionalCompiler.cs`
- `src/DBWeaver.UI/Services/Validation/GraphValidator.cs`
- `src/DBWeaver.UI/ViewModels/PropertyPanel/*`

### Dependências

- Épicos E e J

### Riscos principais

- manter lógica principal nos parâmetros legados
- persistir grafo novo no formato antigo
- não respeitar precedência das regras

### Critério de pronto

- grafos novos usam apenas fluxo graph-first
- grafos antigos continuam carregando com equivalência preservada
- validação estrutural falha quando o grafo novo está inválido

---

## Épico M — Observabilidade e Diagnósticos

### Objetivo

Fechar rastreabilidade operacional do pipeline.

### Entregas

- métricas obrigatórias
- payload de diagnóstico normativo
- `correlation_id`
- `query_id`
- contagem de nós degradados
- eventos de fallback
- catálogo de diagnósticos implementado

### Arquivos prováveis a tocar

- `src/DBWeaver/SqlImport/Diagnostics/*`
- `src/DBWeaver/SqlImport/Telemetry/*`
- pontos de log nos serviços de execução

### Dependências

- transversal a todos os épicos

### Riscos principais

- log insuficiente
- códigos instáveis
- fallback sem instrumentação

### Critério de pronto

- payload mínimo completo
- todos os diagnósticos usados possuem código estável e teste dedicado

---

## Épico N — Testes de Contrato, Regressão e Fixtures

### Objetivo

Amarrar a spec à suíte de teste e impedir regressão semântica.

### Entregas

- snapshots IR
- snapshots Graph
- snapshots Outcome
- fixtures normativas mínimas
- ordenação estável e exclusão de campos não determinísticos

### Arquivos prováveis a tocar

- `tests/VisualSqlArchitect.Tests/Contract/*`
- `tests/VisualSqlArchitect.Tests/Integration/*`
- `tests/VisualSqlArchitect.Tests/Regression/*`
- fixtures SQL dedicadas

### Dependências

- todos os épicos anteriores

### Riscos principais

- snapshot frágil
- teste cobrindo só caso feliz
- IDs instáveis causando ruído

### Critério de pronto

- todas as fixtures mínimas da spec estão presentes
- contrato de snapshot endurecido é respeitado

---

## 6. Milestones Recomendados

## Milestone 1 — Base Semântica

Inclui:
- Épico A
- Épico B
- Épico C

### Resultado esperado

Infraestrutura pronta para IR, IDs, spans, diagnósticos e symbol table.

### Não inclui ainda

- `ValueMap`
- funções avançadas
- `SELECT *`
- equivalência completa

### Critério de saída

- IR serializa deterministicamente
- IDs estáveis passam em teste repetido
- symbol table resolve casos básicos e falha nos críticos

---

## Milestone 2 — Pipeline P0 End-to-End

Inclui:
- Épico D
- Épico E
- Épico F
- Épico G

### Resultado esperado

Query P0 simples entra em AST, sai em IR, vira Graph e recebe classificação correta.

### Critério de saída

- queries P0 suportadas classificadas como `EquivalentTotal` ou `EquivalentTolerant`
- ambiguidades críticas falham corretamente
- aliases textuais já seguem contrato normativo

---

## Milestone 3 — Semântica Avançada de Projeção

Inclui:
- Épico H
- Épico I
- Épico J

### Resultado esperado

Projeções complexas e estrela deixam de ser zonas ambíguas do pipeline.

### Critério de saída

- `SELECT *` está fechado
- catálogo mínimo de funções está implementado
- `CASE` não sofre descarte silencioso

---

## Milestone 4 — Hardening Estrutural

Inclui:
- Épico K
- reforço do Épico F

### Resultado esperado

Join semantics confiável e equivalência madura para cenários reais.

### Critério de saída

- joins encadeados reais não apresentam drift
- diagnósticos de equivalência estão consistentes

---

## Milestone 5 — Convergência de Produto

Inclui:
- Épico L
- Épico M
- Épico N

### Resultado esperado

`ValueMap` novo, observabilidade completa e suíte normativa consolidada.

### Critério de saída

- `ValueMap` graph-first é o padrão
- métricas e diagnósticos estão completos
- suíte normativa mínima está integralmente verde

---

## 7. Backlog Técnico por Prioridade

## P0

- contratos base
- IR versionada
- IDs estáveis
- symbol table
- AST → IR P0
- IR → Graph P0
- classificação de outcome
- aliases textuais
- join semantics
- diagnósticos estáveis

## P1

- `SELECT *`
- catálogo inicial de funções
- projeções complexas
- `ValueMapNormalize`
- CTE não-recursiva melhorada
- `GROUP BY` e `HAVING` mais robustos

## P2

- `UNION`
- `INTERSECT`
- `EXCEPT`
- window functions
- `APPLY`
- CTE recursiva

---

## 8. Tarefas Concretas por Camada

## 8.1 Parser

- expor spans
- expor fragment hash
- garantir preservação de alias/literal
- identificar limite do parse principal vs fallback

## 8.2 AST → IR

- criar mappers por cláusula
- criar factory de `exprId`
- preencher `ResolutionStatus`
- popular `AliasMeta`
- popular `SyntheticOriginReason`

## 8.3 Semântica

- implementar resolver qualificado
- implementar resolver não qualificado
- implementar shadowing
- implementar bindings de self join
- implementar escopo de subquery

## 8.4 Graph

- builders por expressão
- builders por projeção
- builders por join
- metadados de degradação
- `Trace*` em todo nó relevante

## 8.5 Compiler

- respeitar `NodeDegradationState`
- tratar estrela preservada
- tratar `GenericPreserved`
- tratar `ValueMap` graph-first

## 8.6 Tests

- snapshots canônicos
- helpers de comparação
- normalização de ordem
- fixtures SQL mínimas
- testes de regressão histórica

---

## 9. Critério de Pronto por Milestone

## 9.1 Milestone 1 pronto quando

- IR serializa deterministicamente
- IDs estáveis são reproduzíveis
- symbol table resolve o básico e falha nos críticos

## 9.2 Milestone 2 pronto quando

- pipeline P0 roda end-to-end
- queries básicas são `EquivalentTotal` ou `EquivalentTolerant`
- ambiguidades críticas falham corretamente

## 9.3 Milestone 3 pronto quando

- `SELECT *` está normativamente implementado
- funções do catálogo mínimo passam
- `CASE` não é descartado

## 9.4 Milestone 4 pronto quando

- joins encadeados não apresentam drift semântico
- classificador de equivalência está confiável

## 9.5 Milestone 5 pronto quando

- `ValueMap` novo é o padrão
- observabilidade está completa
- suíte normativa mínima está toda verde

---

## 10. Mapa de Riscos

## 10.1 Riscos altos

- drift semântico em joins
- `GenericPreserved` escapar para contexto crítico
- classificação errada de parcial como tolerante
- estrela tratada sem metadata suficiente

## 10.2 Riscos médios

- snapshot instável
- colisão de alias normalizado mal resolvida
- nós sintéticos sem origem auditável
- compatibilidade legada do `ValueMap`

## 10.3 Riscos baixos

- naming interno
- organização de namespaces
- formato de logs, desde que campos normativos existam

---

## 11. Estratégia de Branching Recomendada

Sugestão de branches:

- `feature/sqlimport-foundation`
- `feature/sqlimport-ir`
- `feature/sqlimport-symbol-table`
- `feature/sqlimport-p0-pipeline`
- `feature/sqlimport-equivalence`
- `feature/sqlimport-star-handling`
- `feature/sqlimport-functions`
- `feature/sqlimport-projections`
- `feature/sqlimport-join-hardening`
- `feature/sqlimport-valuemap-graph-first`
- `feature/sqlimport-observability`
- `feature/sqlimport-contract-tests`

---

## 12. Formato Ideal de Cada PR

Cada PR **DEVE** informar:

1. escopo explícito
2. arquivos tocados
3. diagnósticos novos, se houver
4. fixtures novas, se houver
5. snapshots atualizados
6. decisão de feature flag
7. critério de pronto da etapa

---

## 13. Primeiras 10 Tarefas Recomendadas

1. Criar contratos base de diagnóstico, outcome, resolução e degradação.
2. Implementar helper central de ID estável.
3. Criar estrutura `SqlToNodeIR`.
4. Criar `SymbolTableModel` e `Scope`.
5. Implementar resolução qualificada e não qualificada.
6. Implementar mapper AST → IR para `SELECT/FROM`.
7. Implementar mapper AST → IR para `JOIN/WHERE`.
8. Implementar IR → Graph para comparações, lógicos e literais.
9. Implementar `ImportOutcome` e classificador inicial.
10. Criar primeira suíte de fixtures P0 com snapshots canônicos.

---

## 14. Sequência Recomendada de Entrega

### Sprint técnica 1

- Épico A
- início do Épico B

### Sprint técnica 2

- conclusão do Épico B
- Épico C

### Sprint técnica 3

- Épico D
- início do Épico E

### Sprint técnica 4

- conclusão do Épico E
- Épico F
- Épico G

### Sprint técnica 5

- Épico H
- Épico I

### Sprint técnica 6

- Épico J
- Épico K

### Sprint técnica 7

- Épico L
- Épico M

### Sprint técnica 8

- Épico N
- hardening final

---

## 15. Critério de Encerramento do Programa

O programa de implementação deste breakdown somente pode ser considerado encerrado quando:

1. todos os milestones estiverem concluídos
2. a suíte normativa mínima estiver integralmente verde
3. o pipeline AST/IR estiver como caminho primário
4. regex estiver fora do caminho primário, restrito a fallback permitido
5. `ValueMap` graph-first for o padrão de novos grafos
6. snapshots estiverem estáveis
7. observabilidade obrigatória estiver ativa
8. diagnósticos estiverem íntegros e rastreáveis

---

## 16. Resumo Executivo Final

Este documento define a implementação da camada intermediária SQL Import como um programa incremental estruturado em cinco grandes blocos:

1. fundação semântica
2. pipeline P0 end-to-end
3. semântica avançada de projeção
4. hardening estrutural
5. convergência de produto

O objetivo não é apenas “fazer importar SQL”, mas construir uma base confiável, auditável e suficientemente determinística para que o DBWeaver passe a operar com:

- AST/IR como verdade semântica primária
- equivalência formal
- graph com rastreabilidade total
- partial import explicitamente marcado
- `ValueMap` moderno e graph-first
- contrato forte de testes e regressão

---

**Status final deste documento**: pronto para uso como plano operacional de implementação da spec normativa congelada.
