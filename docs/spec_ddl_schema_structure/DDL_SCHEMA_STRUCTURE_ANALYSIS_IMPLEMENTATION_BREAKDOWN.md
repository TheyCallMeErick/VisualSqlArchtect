# Implementation Breakdown — DDL Schema Structure Analysis (Inferable Mode)

**Documento derivado de:** Especificação Normativa Executável — Análise Estrutural no Modo DDL (Somente Inferível)  
**Objetivo deste arquivo:** decompor a especificação em tarefas menores, sequenciáveis, testáveis e adequadas para execução por IA.  
**Formato:** backlog normativo de implementação.  
**Base:** divisão por fundação, utilitários determinísticos, pipeline, regras, SQL candidates, cache, UI e testes.

---

## 1. Diretrizes de uso deste breakdown

1. Cada tarefa DEVE ser implementada isoladamente, respeitando suas dependências explícitas.
2. Nenhuma tarefa DEVE assumir comportamento implícito não descrito na especificação base.
3. Cada tarefa DEVE produzir saída verificável por testes.
4. Cada tarefa DEVE respeitar a separação por camada:
   - Domain
   - Application
   - Infrastructure
   - Presentation
5. A IA NÃO DEVE mesclar tarefas de múltiplas fases em um único passo quando isso reduzir verificabilidade.
6. Antes de iniciar qualquer tarefa, a IA DEVE validar se todas as dependências declaradas já foram concluídas.
7. Toda implementação DEVE preservar determinismo, rastreabilidade e aderência literal à spec base.
8. Quando houver conflito entre este breakdown e a especificação base, a especificação base DEVE prevalecer.
9. Este documento NÃO redefine regras de negócio; apenas organiza a execução da implementação.
10. Cada tarefa DEVE ser considerada concluída somente quando seus critérios de aceite e testes mínimos forem atendidos.

---

## 2. Estratégia geral de execução

A implementação DEVE seguir a seguinte ordem macro:

1. Fundação contratual
2. Normalização e utilitários determinísticos
3. Pipeline base de execução
4. Regras de análise
5. Suggestions e SQL candidates
6. Cache, hashes, performance e determinismo
7. UI / MVVM
8. Testes finais e aceite

A IA NÃO DEVE iniciar:
- regras de análise antes de os utilitários determinísticos existirem;
- SQL candidates antes da infraestrutura de quoting, preconditions e classificação de safety existir;
- UI final antes de o contrato do resultado estar estabilizado.

---

## 3. Convenção obrigatória de cada tarefa

Cada tarefa deste breakdown possui os seguintes campos:

1. **Objetivo**
2. **Escopo incluído**
3. **Escopo excluído**
4. **Entradas**
5. **Saídas**
6. **Regras obrigatórias**
7. **Critérios de aceite**
8. **Testes mínimos**
9. **Dependências**

A IA NÃO DEVE ignorar nenhum desses campos durante a implementação.

---

# FASE 1 — Fundação contratual

## Tarefa 1.1 — Implementar enums oficiais

### Objetivo
Implementar todos os enums normativos fechados definidos na especificação.

### Escopo incluído
1. `SchemaAnalysisStatus`
2. `SchemaIssueSeverity`
3. `SchemaRuleCode`
4. `SchemaTargetType`
5. `EvidenceKind`
6. `SqlCandidateSafety`
7. `NamingConvention`
8. `NormalizationStrictness`
9. `CandidateVisibility`
10. `RuleExecutionState`

### Escopo excluído
1. records
2. validações
3. serialização
4. lógica de negócio
5. UI

### Entradas
1. lista fechada de enums da especificação base

### Saídas
1. arquivo(s) contendo os enums oficiais
2. nomes exatamente iguais aos definidos na spec

### Regras obrigatórias
1. Os enums DEVEM ser implementados sem valores extras.
2. Os nomes DEVEM ser idênticos à spec.
3. A ordem dos membros DEVE ser preservada quando relevante para leitura e manutenção.
4. A camada Domain DEVE conter esses enums.

### Critérios de aceite
1. Todos os enums compilam.
2. Não existem enums ad hoc.
3. Nenhum enum foi omitido.
4. Nenhum valor divergente foi introduzido.

### Testes mínimos
1. Teste de compilação/referência para cada enum.
2. Teste simples de serialização textual futura compatível.

### Dependências
1. nenhuma

---

## Tarefa 1.2 — Implementar records contratuais oficiais

### Objetivo
Implementar todos os records normativos da seção de contratos.

### Escopo incluído
1. `SchemaEvidence`
2. `SqlFixCandidate`
3. `SchemaSuggestion`
4. `SchemaIssue`
5. `SchemaRuleExecutionDiagnostic`
6. `SchemaAnalysisSummary`
7. `SchemaAnalysisPartialState`
8. `SchemaAnalysisResult`
9. `SchemaRuleSetting`
10. `SchemaAnalysisProfile`

### Escopo excluído
1. validações profundas
2. builder/factory
3. regras de análise
4. UI
5. persistência

### Entradas
1. definição de records da spec

### Saídas
1. records imutáveis compiláveis
2. nulabilidade aderente

### Regras obrigatórias
1. Os campos DEVEM ter os mesmos nomes da spec.
2. A nulabilidade DEVE ser preservada.
3. Não DEVEM existir campos extras.
4. Domain DEVE permanecer sem dependência de Infrastructure e Presentation.

### Critérios de aceite
1. Todos os records compilam.
2. Os tipos batem com a especificação.
3. Não existem membros mutáveis fora do contrato esperado.

### Testes mínimos
1. Instanciação de cada record.
2. Verificação de nulabilidade básica.
3. Igualdade estrutural padrão de records.

### Dependências
1. 1.1

---

## Tarefa 1.3 — Implementar invariantes contratuais e validação estrutural

### Objetivo
Criar validação central para impedir materialização de payloads inválidos.

### Escopo incluído
1. `Evidence.Count >= 1`
2. `Suggestions.Count <= MaxSuggestionsPerIssue`
3. `Issues.Count <= MaxIssues`
4. regras por `TargetType`
5. arredondamento de `Confidence`
6. restrições de `IsAutoApplicable`
7. restrições de `CandidateVisibility`
8. `PreconditionsSql` obrigatório para candidate no MVP

### Escopo excluído
1. validação de negócio das regras
2. validação de provider
3. validação de UI

### Entradas
1. records oficiais
2. limites do profile

### Saídas
1. componente validador
2. falhas explícitas para contratos inválidos

### Regras obrigatórias
1. O validador DEVE ser determinístico.
2. O validador NÃO DEVE depender de UI.
3. O arredondamento DEVE usar `Math.Round(v, 4, MidpointRounding.ToEven)`.
4. `TargetType=Column` exige `TableName` e `ColumnName`.
5. `TargetType=Constraint` exige `ConstraintName`.

### Critérios de aceite
1. Payload inválido falha de forma explícita.
2. Payload válido é aceito sem mutações silenciosas indevidas.
3. Nenhum candidate inválido é emitível.

### Testes mínimos
1. Um teste por invariante.
2. Teste com `IsAutoApplicable=true` e `Safety!=NonDestructive`.
3. Teste com `CandidateVisibility=VisibleActionable` e `Safety!=NonDestructive`.
4. Teste com lista vazia de preconditions.

### Dependências
1. 1.2

---

## Tarefa 1.4 — Implementar serialização canônica

### Objetivo
Padronizar a serialização JSON necessária para hashing e consistência operacional.

### Escopo incluído
1. `camelCase`
2. enums por string
3. normalização de vazios para `null`
4. ordenação determinística de coleções relevantes
5. suporte a JSON canônico

### Escopo excluído
1. hashing
2. cache
3. leitura de arquivo
4. UI

### Entradas
1. contratos da fase 1
2. regras da seção de serialização

### Saídas
1. serializador canônico
2. testes de estabilidade

### Regras obrigatórias
1. JSON DEVE ser determinístico.
2. Arrays com semântica de conjunto DEVEM sair ordenados.
3. Arrays com semântica de lista DEVEM preservar ordem normativa.
4. Strings DEVEM ser serializadas em Unicode NFC quando aplicável ao hash.

### Critérios de aceite
1. Mesma entrada lógica produz o mesmo JSON.
2. Ordem de propriedades é estável.
3. Enums saem como string.

### Testes mínimos
1. Duas serializações consecutivas da mesma estrutura.
2. Teste de arrays de conjunto.
3. Teste de enum textual.

### Dependências
1. 1.2

---

# FASE 2 — Normalização e utilitários determinísticos

## Tarefa 2.1 — Implementar normalização de schema canônico por provider

**Status:** Concluída em 2026-04-13.

### Objetivo
Materializar a convergência de schema default por provider.

### Escopo incluído
1. PostgreSQL -> `public`
2. SQL Server -> `dbo`
3. MySQL -> `null`
4. SQLite -> `main`

### Escopo excluído
1. quoting
2. SQL generation
3. matching de nomes

### Entradas
1. provider
2. schema bruto

### Saídas
1. função de schema canônico

### Regras obrigatórias
1. `null`, vazio e whitespace DEVEM convergir para schema default.
2. Para MySQL, o schema canônico DEVE permanecer `null`.
3. Comparações DEVEM usar schema canônico.

### Critérios de aceite
1. Todos os providers retornam valor esperado.
2. Não há divergência entre `null`, vazio e whitespace.

### Testes mínimos
1. Um teste por provider.
2. Um teste com valor vazio.
3. Um teste com whitespace.

### Dependências
1. 1.1

---

## Tarefa 2.2 — Implementar tokenização e normalização de nomes

**Status:** Concluída em 2026-04-13.

### Objetivo
Implementar exatamente o pipeline canônico de tokenização.

### Escopo incluído
1. Unicode NFD
2. remoção de diacríticos
3. lowercase invariável
4. separadores lógicos
5. split por `_`, `-`, `.`, espaço
6. split camel/pascal
7. preservação de siglas reconhecidas
8. remoção de vazios
9. singularização
10. aplicação de sinônimos
11. separação entre tokens estruturais e tokens de entidade

### Escopo excluído
1. decisão de score
2. SQL generation
3. UI

### Entradas
1. nome bruto
2. profile com sinônimos

### Saídas
1. tokens estruturais
2. tokens de entidade
3. token principal quando aplicável

### Regras obrigatórias
1. A ordem normativa do pipeline DEVE ser preservada.
2. Tokens estruturais NÃO DEVEM definir entidade.
3. Nomes sem tokens de entidade DEVEM ser inválidos para regras que exijam entidade.
4. O conjunto de siglas preservadas DEVE ser fechado conforme spec.

### Critérios de aceite
1. Casos como `PessoaId`, `id_pessoa`, `ÓrgãosIDs` são tratados corretamente.
2. O pipeline é determinístico.
3. Resultados são reprodutíveis.

### Testes mínimos
1. Casos da seção de testes de nomeação.
2. Casos com diacríticos.
3. Casos camel/pascal.
4. Casos com apenas tokens estruturais.

### Dependências
1. 1.1

---

## Tarefa 2.3 — Implementar singularização normativa

**Status:** Concluída em 2026-04-13.

### Objetivo
Isolar e implementar a lógica fechada de singularização por token.

### Escopo incluído
1. `ies -> y`
2. `(ss|sh|ch|x|z)es -> remove es`
3. `([^s])ses -> $1s`
4. `s -> remove`, com exceções

### Escopo excluído
1. sinônimos
2. score
3. regex de naming

### Entradas
1. token singularizável

### Saídas
1. token singularizado

### Regras obrigatórias
1. As regras DEVEM ser aplicadas na ordem definida.
2. Exceções `ss` e `us` DEVEM ser preservadas.

### Critérios de aceite
1. Regras aplicadas corretamente.
2. Não há singularização fora da norma.

### Testes mínimos
1. um caso por regra
2. casos de exceção

### Dependências
1. 2.2

---

## Tarefa 2.4 — Implementar sinônimos, allowlist e denylist

**Status:** Concluída em 2026-04-13.

### Objetivo
Criar o motor de equivalência e precedência de tokens normativos.

### Escopo incluído
1. grupos de sinônimos
2. conflito por ordem
3. allowlist sobre denylist
4. diagnósticos de conflito e override

### Escopo excluído
1. score final
2. UI
3. SQL candidate

### Entradas
1. `SynonymGroups`
2. `NameAllowlist`
3. `LowQualityNameDenylist`

### Saídas
1. resolução de tokens equivalentes
2. diagnósticos agregáveis

### Regras obrigatórias
1. Conflito em múltiplos grupos DEVE prevalecer pelo primeiro grupo.
2. O conflito DEVE registrar `ANL-SETTINGS-SYNONYM-CONFLICT`.
3. Allowlist DEVE suprimir emissão da denylist.
4. O override DEVE registrar `ANL-SETTINGS-ALLOWLIST-OVERRIDES-DENYLIST`.

### Critérios de aceite
1. Conflitos se resolvem deterministicamente.
2. Override funciona.
3. Sem comportamento implícito fora da spec.

### Testes mínimos
1. conflito de sinônimo
2. override allowlist
3. ausência de grupos

### Dependências
1. 2.2

---

## Tarefa 2.5 — Implementar regex normativas de convenção de nomes

**Status:** Concluída em 2026-04-13.

### Objetivo
Fornecer validação por convenção configurada.

### Escopo incluído
1. `SnakeCase`
2. `CamelCase`
3. `PascalCase`
4. `KebabCase`
5. `MixedAllowed`

### Escopo excluído
1. naming de schema
2. low semantic name
3. score

### Entradas
1. nome bruto ou canônico
2. convenção configurada

### Saídas
1. resultado válido/inválido por convenção

### Regras obrigatórias
1. Nomes iniciados por dígito DEVEM ser inválidos em todas as convenções.
2. `MixedAllowed` DEVE desabilitar a regra de violação.
3. Caracteres residuais inválidos DEVEM reprovar o nome.

### Critérios de aceite
1. Regex funciona conforme a spec.
2. `MixedAllowed` não emite violação.

### Testes mínimos
1. um teste por convenção
2. um teste para nome iniciado por número
3. um teste para caractere inválido

### Dependências
1. 1.1

---

## Tarefa 2.6 — Implementar compatibilidade canônica de tipos

**Status:** Concluída em 2026-04-13.

### Objetivo
Materializar categorias canônicas e compatibilidade normativa.

### Escopo incluído
1. categorias canônicas
2. mapeamento mínimo
3. compatibilidade exata
4. compatibilidade semântica forte
5. compatibilidade semântica fraca
6. incompatibilidade

### Escopo excluído
1. scoring completo
2. UI
3. SQL candidate

### Entradas
1. `rawType` origem
2. `rawType` destino
3. provider quando necessário

### Saídas
1. categoria canônica
2. nível de compatibilidade

### Regras obrigatórias
1. O mapeamento DEVE obedecer a lista fechada da spec.
2. `MISSING_FK` NÃO DEVE usar compatibilidade fraca como score positivo.
3. `FK_CATALOG_INCONSISTENT` PODE classificar compatibilidade fraca como warning.

### Critérios de aceite
1. Tipos normativos mapeiam corretamente.
2. Casos ambíguos da spec são respeitados.
3. Casos não mapeados viram `Other`.

### Testes mínimos
1. um teste por categoria
2. teste de `varchar(36)` x `uuid`
3. teste de `tinyint(1)` em MySQL
4. teste de `bit` em SQL Server

### Dependências
1. 1.1

---

# FASE 3 — Pipeline base de execução

## Tarefa 3.1 — Implementar validação de `DbMetadata`

**Status:** Concluída em 2026-04-13.

### Objetivo
Garantir aderência ao contrato bipartido do metadata.

### Escopo incluído
1. metadados obrigatórios globais
2. metadados opcionais por regra
3. falha fatal em ausência obrigatória
4. degradação em ausência opcional

### Escopo excluído
1. execução de regra
2. cache
3. UI

### Entradas
1. `DbMetadata` bruto
2. provider

### Saídas
1. validação do snapshot
2. diagnósticos adequados

### Regras obrigatórias
1. Ausência de obrigatório global DEVE falhar a execução.
2. Ausência de opcional DEVE degradar apenas a regra dependente.
3. A validação NÃO DEVE inventar default sem respaldo da spec.

### Critérios de aceite
1. Metadado inválido é rejeitado.
2. Metadado parcial não gera falso positivo.
3. `ANL-METADATA-PARTIAL` é emitido de modo agregado por regra.

### Testes mínimos
1. um caso fatal por obrigatório ausente
2. um caso parcial por comentário ausente
3. um caso parcial por índice/UQ ausente

### Dependências
1. 1.2
2. 2.1

---

## Tarefa 3.2 — Implementar pré-indexações obrigatórias

**Status:** Concluída em 2026-04-13.

### Objetivo
Criar os índices internos necessários ao pipeline e às regras.

### Escopo incluído
1. `tableByFullName`
2. `columnsByTable`
3. `pkColumnsByTable`
4. `uniqueConstraintsByTable`
5. `fkByChildTable`
6. `fkByParentTable`
7. `normalizedNameIndex`
8. `ruleExecutionState`
9. `constraintNamesBySchema`
10. `tableKindsByFullName`

### Escopo excluído
1. regras de análise
2. cache
3. UI

### Entradas
1. metadata validado

### Saídas
1. índices imutáveis por execução

### Regras obrigatórias
1. Os índices DEVEM ser determinísticos.
2. A construção dos índices NÃO DEVE mutar o metadata de entrada.
3. Nomes completos DEVEM usar schema canônico.

### Critérios de aceite
1. Todos os índices existem.
2. O acesso aos elementos é consistente.
3. O mesmo metadata produz os mesmos índices.

### Testes mínimos
1. teste de indexação por tabela
2. teste de indexação por FK
3. teste de nomes normalizados

### Dependências
1. 3.1
2. 2.2
3. 2.1

---

## Tarefa 3.3 — Implementar normalização e validação do profile

**Status:** Concluída em 2026-04-13.

### Objetivo
Materializar defaults, clamps e diagnósticos do profile.

### Escopo incluído
1. defaults normativos
2. clamps
3. fallback de versão
4. inserção de `RuleSettings` faltantes
5. cache desabilitado em `CacheTtlSeconds=0`

### Escopo excluído
1. persistência de settings
2. UI
3. regras

### Entradas
1. profile bruto
2. versão suportada

### Saídas
1. profile validado
2. diagnósticos de configuração

### Regras obrigatórias
1. Ausente => default.
2. Fora da faixa => clamp + diagnóstico.
3. Enum inválido => default + diagnóstico.
4. `version` acima da suportada => fallback.
5. `ruleSettings` incompleto => completar.

### Critérios de aceite
1. O profile final fica completo e válido.
2. Diagnósticos corretos são emitidos.
3. O conteúdo fica apto a hash determinístico.

### Testes mínimos
1. campo ausente
2. enum inválido
3. versão acima da suportada
4. `RuleSettings` incompleto

### Dependências
1. 1.2
2. 1.3

---

## Tarefa 3.4 — Implementar orquestrador principal do pipeline

**Status:** Concluída em 2026-04-13.

### Objetivo
Materializar o fluxo exato da análise da seção de pipeline.

### Escopo incluído
1. receber snapshot e profile
2. validar profile
3. validar metadata
4. calcular fingerprint
5. calcular hash do profile
6. consultar cache
7. pré-indexar
8. executar regras na ordem fixa
9. aplicar limites por regra
10. aplicar dedupe global
11. aplicar thresholds
12. truncar em `MaxIssues`
13. ordenar resultado final
14. materializar suggestions
15. limitar suggestions
16. gerar resumo
17. persistir cache
18. calcular status final
19. retornar resultado

### Escopo excluído
1. UI
2. execução automática de SQL
3. acesso direto ao banco

### Entradas
1. metadata snapshot
2. profile validado

### Saídas
1. `SchemaAnalysisResult`

### Regras obrigatórias
1. A ordem do pipeline DEVE ser exatamente a normativa.
2. O orquestrador NÃO DEVE reimplementar lógica interna das regras.
3. O pipeline DEVE ser deterministicamente repetível.

### Critérios de aceite
1. O pipeline roda mesmo com regras inicialmente stubadas.
2. Um resultado vazio válido pode ser produzido.
3. O fluxo segue a ordem exigida.

### Testes mínimos
1. execução completa sem issues
2. execução com uma regra ativa
3. validação da ordem de chamada das regras

### Dependências
1. 3.2
2. 3.3
3. 1.4

---

## Tarefa 3.5 — Implementar timeout, cancelamento e parcial

**Status:** Concluída em 2026-04-13.

### Objetivo
Controlar encerramento não integral da execução conforme política normativa.

### Escopo incluído
1. timeout global
2. parcial por timeout
3. parcial por falha de regra
4. cancelamento em qualquer fase
5. distinção entre `Partial`, `Cancelled` e `Failed`

### Escopo excluído
1. UI de progresso
2. cache
3. regras específicas

### Entradas
1. token de cancelamento
2. timeout configurado
3. estado corrente do pipeline

### Saídas
1. status final correto
2. `PartialState`
3. diagnósticos operacionais

### Regras obrigatórias
1. Timeout DEVE interromper criação de novas issues.
2. `AllowPartialOnTimeout=false` DEVE falhar sem issues.
3. Cancelamento antes de materialização DEVE resultar em `Cancelled`.
4. Falha de regra tolerada DEVE registrar diagnóstico e seguir.

### Critérios de aceite
1. Status obedece precedência normativa.
2. `ReasonCode` fica correto.
3. Não há emissão inconsistente após timeout/cancelamento.

### Testes mínimos
1. timeout com parcial permitido
2. timeout sem parcial permitido
3. cancelamento com issues já materializadas
4. cancelamento sem materialização

### Dependências
1. 3.4

---

## Tarefa 3.6 — Implementar ordenação final e deduplicação global

**Status:** Concluída em 2026-04-13.

### Objetivo
Garantir resultado final deduplicado e deterministicamente ordenado.

### Escopo incluído
1. chave de dedupe normativa
2. normalização de mensagem para hash
3. retenção da melhor issue
4. ordenação final por severidade, confidence e alvo

### Escopo excluído
1. UI
2. hashing global de metadata/profile
3. cache

### Entradas
1. lista de issues intermediárias

### Saídas
1. lista final de issues deduplicada e ordenada

### Regras obrigatórias
1. A chave de dedupe DEVE seguir exatamente a spec.
2. A retenção DEVE obedecer confidence, severity e `IssueId`.
3. A ordenação final DEVE obedecer a seção normativa.

### Critérios de aceite
1. Duplicatas colapsam corretamente.
2. Execuções repetidas mantêm a mesma ordem.
3. Mensagens textualmente equivalentes deduplicam após normalização.

### Testes mínimos
1. duas issues equivalentes com confidence diferente
2. duas issues equivalentes com severity diferente
3. teste de ordenação final completa

### Dependências
1. 3.4

---

# FASE 4 — Regras de análise

## Tarefa 4.1 — Implementar `FK_CATALOG_INCONSISTENT`

**Status:** Concluída em 2026-04-13.

### Objetivo
Emitir issues quando FK catalogada estiver estruturalmente inconsistente.

### Escopo incluído
1. coluna child inexistente
2. coluna parent inexistente
3. tipo incompatível
4. compatibilidade semântica fraca
5. evidências mínimas
6. severidade objetiva

### Escopo excluído
1. SQL candidate
2. MISSING_FK
3. UI

### Entradas
1. FKs catalogadas
2. colunas origem e destino
3. tipos canônicos

### Saídas
1. issues dessa regra
2. diagnósticos de metadata parcial quando aplicável

### Regras obrigatórias
1. SQL é proibido para esta regra.
2. Sem metadado suficiente, NÃO DEVE emitir issue.
3. Compatibilidade fraca PODE resultar em warning.
4. Incompatibilidade total DEVE resultar em critical.

### Critérios de aceite
1. Casos inconsistentes geram issue.
2. Casos consistentes não geram.
3. Evidências mínimas são sempre preenchidas.

### Testes mínimos
1. child inexistente
2. parent inexistente
3. tipo incompatível
4. compatibilidade fraca
5. metadado insuficiente

### Dependências
1. 2.6
2. 3.2
3. 3.4

---

## Tarefa 4.2 — Implementar `MISSING_FK`

**Status:** Concluída em 2026-04-13.

### Objetivo
Detectar colunas candidatas a FK sem constraint formal usando naming, tipo e topologia.

### Escopo incluído
1. elegibilidade da coluna
2. inferência de entidade
3. busca de destinos possíveis
4. aplicação da fórmula fechada de score
5. desempates
6. ambiguidade
7. evidências mínimas
8. severidade objetiva

### Escopo excluído
1. emissão do SQL candidate em si
2. aplicação no canvas
3. UI

### Entradas
1. coluna candidata
2. destinos possíveis
3. PK e UQ
4. tipos
5. índices da origem
6. sinônimos e configuração

### Saídas
1. issues `MISSING_FK`

### Regras obrigatórias
1. A fórmula DEVE ser implementada exatamente.
2. Sem match de entidade => score 0.
3. Sem compatibilidade exata ou semântica forte => score 0.
4. Ambiguidade DEVE ser marcada quando diferença `< 0.0500`.
5. Casos ambíguos NÃO DEVEM permitir candidate SQL.

### Critérios de aceite
1. Casos fortes geram issue.
2. Casos abaixo do threshold não geram.
3. Ambiguidade é marcada corretamente.
4. Desempates seguem a ordem normativa.

### Testes mínimos
1. caso positivo forte
2. caso abaixo de threshold
3. caso sem entidade inferível
4. caso sem compatibilidade de tipo
5. caso ambíguo

### Dependências
1. 2.2
2. 2.4
3. 2.6
4. 3.2
5. 3.4

---

## Tarefa 4.3 — Implementar `NAMING_CONVENTION_VIOLATION`

**Status:** Concluída em 2026-04-13.

### Objetivo
Detectar violações de convenção de nomes.

### Escopo incluído
1. validação de tabelas
2. validação de colunas
3. validação de constraints

### Escopo excluído
1. schema
2. low semantic name
3. SQL

### Entradas
1. nomes
2. convenção configurada

### Saídas
1. issues de naming violation

### Regras obrigatórias
1. `MixedAllowed` desabilita a regra.
2. A regra NÃO DEVE se aplicar a schema.
3. A confidence DEVE ser fixa conforme spec.

### Critérios de aceite
1. nomes inválidos emitem issue
2. nomes válidos não emitem
3. `MixedAllowed` não emite

### Testes mínimos
1. tabela inválida
2. coluna inválida
3. constraint inválida
4. `MixedAllowed`

### Dependências
1. 2.5
2. 3.4

---

## Tarefa 4.4 — Implementar `LOW_SEMANTIC_NAME`

**Status:** Concluída em 2026-04-13.

### Objetivo
Detectar nomes de baixa qualidade semântica técnica.

### Escopo incluído
1. token principal na denylist
2. regex fraca normativa
3. allowlist
4. exclusão de constraint names
5. exclusão de nomes só com tokens estruturais

### Escopo excluído
1. naming convention violation
2. SQL
3. UI

### Entradas
1. nome normalizado
2. allowlist/denylist

### Saídas
1. issues de baixa qualidade semântica

### Regras obrigatórias
1. Constraint names NÃO participam.
2. Allowlist DEVE suprimir emissão.
3. Nome sem token de entidade e só estrutural DEVE ser ignorado.

### Critérios de aceite
1. nomes fracos emitem issue
2. nomes allowlisted não emitem
3. constraints não entram

### Testes mínimos
1. token principal na denylist
2. regex fraca
3. allowlist
4. nome só estrutural

### Dependências
1. 2.2
2. 2.4
3. 3.4

---

## Tarefa 4.5 — Implementar `MISSING_REQUIRED_COMMENT`

**Status:** Concluída em 2026-04-13.

### Objetivo
Detectar alvos obrigatórios sem comentário válido.

### Escopo incluído
1. definição de alvos válidos
2. comentário ausente
3. comentário equivalente ao nome do objeto
4. degradação por provider
5. evidência mínima

### Escopo excluído
1. geração do SQL candidate propriamente dita
2. UI
3. execução automática

### Entradas
1. metadata de comentários
2. `RequiredCommentTargets`
3. provider
4. metadados auxiliares de PK/FK/UQ

### Saídas
1. issues de comentário ausente

### Regras obrigatórias
1. `RequiredCommentTargets=[]` desabilita emissão.
2. Comentário igual ao nome do objeto DEVE ser inválido.
3. SQLite emite no máximo `Info` e sem SQL.
4. MySQL coluna só permite future candidate com definição completa disponível.

### Critérios de aceite
1. Alvos obrigatórios sem comentário geram issue.
2. Comentário branco ou equivalente ao nome conta como ausente.
3. Providers degradados respeitam severidade.

### Testes mínimos
1. tabela sem comentário
2. PK column sem comentário
3. audit column sem comentário
4. comentário igual ao nome
5. SQLite

### Dependências
1. 3.2
2. 3.4
3. 2.2

---

## Tarefa 4.6 — Implementar `NF1_HINT_MULTI_VALUED`

**Status:** Concluída em 2026-04-13.

### Objetivo
Emitir hint de não atomicidade baseado em sinais estruturais permitidos.

### Escopo incluído
1. tokens fortes
2. default textual contendo vírgula
3. tipo json/xml com contexto relacional
4. allowlist de payload semiestruturado
5. score normativo

### Escopo excluído
1. SQL
2. severidade critical
3. inferência de domínio

### Entradas
1. coluna
2. tipo
3. default textual
4. allowlist

### Saídas
1. issues de hint 1FN

### Regras obrigatórias
1. A regra exige no mínimo dois sinais objetivos.
2. `SemiStructuredPayloadAllowlist` DEVE obedecer precedência normativa.
3. SQL é proibido.

### Critérios de aceite
1. sinais suficientes emitem
2. sinal isolado não emite
3. allowlist reduz emissão quando aplicável

### Testes mínimos
1. token + default com vírgula
2. json/xml + contexto relacional
3. só um sinal
4. allowlist

### Dependências
1. 2.2
2. 3.4

---

## Tarefa 4.7 — Implementar `NF2_HINT_PARTIAL_DEPENDENCY`

**Status:** Concluída em 2026-04-13.

### Objetivo
Emitir hint de dependência parcial sobre PK composta.

### Escopo incluído
1. pré-condição de PK composta
2. coluna descritiva
3. associação forte a um componente da PK
4. critério opcional de dimensão
5. penalidade por índice único com PK completa
6. score normativo

### Escopo excluído
1. SQL
2. inferência de negócio
3. UI

### Entradas
1. PK composta
2. colunas candidatas
3. FKs
4. `EstimatedRowCount` quando houver

### Saídas
1. issues de hint 2FN

### Regras obrigatórias
1. Sem PK composta, a regra NÃO DEVE emitir.
2. Exige ao menos dois sinais.
3. Critério de dimensão só vale com `EstimatedRowCount` disponível.
4. SQL é proibido.

### Critérios de aceite
1. casos adequados emitem
2. ausência de PK composta não emite
3. sinal único não emite

### Testes mínimos
1. caso positivo com PK composta
2. caso sem PK composta
3. caso com índice único mitigando score

### Dependências
1. 2.2
2. 2.4
3. 3.2
4. 3.4

---

## Tarefa 4.8 — Implementar `NF3_HINT_TRANSITIVE_DEPENDENCY`

**Status:** Concluída em 2026-04-13.

### Objetivo
Emitir hint de dependência transitiva baseado em padrão chave + descritivo.

### Escopo incluído
1. padrão `X_id + X_name`
2. `X_desc`
3. `X_status`
4. allowlist compartilhada
5. score normativo

### Escopo excluído
1. SQL
2. inferência funcional
3. UI

### Entradas
1. colunas da tabela
2. nomes normalizados
3. allowlist

### Saídas
1. issues de hint 3FN

### Regras obrigatórias
1. Exige par chave + descritivo.
2. Matching de `X` DEVE usar o pipeline canônico de nomes.
3. SQL é proibido.

### Critérios de aceite
1. padrão válido emite
2. ausência do par não emite
3. allowlist reduz emissão quando aplicável

### Testes mínimos
1. `customer_id + customer_name`
2. `status` isolado
3. allowlist

### Dependências
1. 2.2
2. 3.4

---

# FASE 5 — Suggestions e SQL candidates

## Tarefa 5.1 — Implementar fábrica padronizada de evidências

**Status:** Concluída em 2026-04-13.

### Objetivo
Centralizar a criação de `SchemaEvidence`.

### Escopo incluído
1. builders/factories de evidência
2. pesos
3. chaves e valores
4. source path quando aplicável

### Escopo excluído
1. UI
2. ranking global de issues
3. SQL generation

### Entradas
1. fatos técnicos das regras

### Saídas
1. listas de evidência válidas

### Regras obrigatórias
1. Toda issue DEVE ter ao menos uma evidência.
2. A evidência DEVE ser explícita e técnica.
3. A factory NÃO DEVE inventar fatos não observados.

### Critérios de aceite
1. Todas as regras conseguem montar evidência mínima.
2. Peso e ordenação posterior ficam consistentes.

### Testes mínimos
1. criação de evidência por tipo
2. validação de issue com e sem evidência

### Dependências
1. fase 4

---

## Tarefa 5.2 — Implementar fábrica de suggestions

**Status:** Concluída em 2026-04-13.

### Objetivo
Gerar sugestões textuais após emissão definitiva da issue.

### Escopo incluído
1. criação de `SchemaSuggestion`
2. priorização
3. truncamento por `MaxSuggestionsPerIssue`
4. vinculação a candidates quando houver

### Escopo excluído
1. execução de SQL
2. UI
3. cache

### Entradas
1. issue final
2. provider
3. profile

### Saídas
1. lista de suggestions da issue

### Regras obrigatórias
1. Suggestion só DEVE ser criada após threshold e issue finalizada.
2. `Suggestions` DEVE ser lista vazia quando não houver.
3. O truncamento DEVE obedecer confidence, title e `SuggestionId`.

### Critérios de aceite
1. suggestions surgem no momento correto
2. truncamento funciona
3. sem `null`

### Testes mínimos
1. issue com uma suggestion
2. issue com múltiplas suggestions acima do limite
3. issue sem suggestion

### Dependências
1. fase 4
2. 1.3

---

## Tarefa 5.3 — Implementar gerador de nome de constraint para `MISSING_FK`

**Status:** Concluída em 2026-04-13.

### Objetivo
Gerar nome sugerido determinístico de FK.

### Escopo incluído
1. formato base
2. normalização para minúsculo
3. substituição de não alfanumérico por `_`
4. truncamento com hash
5. resolução de colisão `_v2` a `_v99`

### Escopo excluído
1. SQL completo
2. preconditions
3. execução

### Entradas
1. child table
2. child column
3. parent table
4. parent column
5. nomes existentes no schema

### Saídas
1. nome sugerido de constraint

### Regras obrigatórias
1. O formato DEVE ser exatamente o da spec.
2. O limite de 63 caracteres DEVE ser respeitado.
3. Acima de `_v99`, o candidate NÃO DEVE ser emitido.

### Critérios de aceite
1. nomes curtos saem completos
2. nomes longos truncam corretamente
3. colisões usam sufixos corretos

### Testes mínimos
1. nome simples
2. truncamento
3. colisão existente
4. esgotamento de sufixos

### Dependências
1. 4.2

---

## Tarefa 5.4 — Implementar gerador de `PreconditionsSql`

**Status:** Concluída em 2026-04-13.

### Objetivo
Gerar preconditions obrigatórias dos candidates no MVP.

### Escopo incluído
1. existência de tabela child
2. existência de tabela parent
3. inexistência de constraint com mesmo nome
4. inexistência de FK equivalente
5. templates por provider

### Escopo excluído
1. SQL principal do candidate
2. execução
3. UI

### Entradas
1. provider
2. nomes qualificados
3. nome de constraint
4. chave de equivalência

### Saídas
1. lista ordenada de `PreconditionsSql`

### Regras obrigatórias
1. A ordem das preconditions DEVE ser a normativa.
2. Se não puderem ser geradas com segurança, o candidate NÃO DEVE ser emitido.
3. SQLite NÃO DEVE gerar candidates que dependam de preconditions robustas não suportadas.

### Critérios de aceite
1. Providers suportados geram preconditions válidas.
2. SQLite é corretamente bloqueado quando aplicável.

### Testes mínimos
1. PostgreSQL
2. SQL Server
3. MySQL
4. SQLite bloqueado

### Dependências
1. 5.3

---

## Tarefa 5.5 — Implementar quoting e escaping por provider

**Status:** Concluída em 2026-04-13.

### Objetivo
Centralizar identificação quoted e escaping de identificadores e literais.

### Escopo incluído
1. quoting PostgreSQL
2. quoting SQL Server
3. quoting MySQL
4. quoting SQLite
5. escape de delimitadores
6. escape de aspas simples
7. `N'...'` em SQL Server para Unicode

### Escopo excluído
1. score
2. UI
3. execução

### Entradas
1. provider
2. identificador
3. literal

### Saídas
1. identificador quoted
2. literal escaped

### Regras obrigatórias
1. Identificadores DEVEM ser sempre quoted em SQL candidate.
2. Delimitadores de fechamento DEVEM ser escapados duplicando o delimitador.
3. Strings DEVEM duplicar aspas simples.

### Critérios de aceite
1. nomes especiais são escapados corretamente
2. literals Unicode ficam corretos no SQL Server

### Testes mínimos
1. identificador com `]`
2. identificador com `"`
3. identificador com crase
4. string com aspas simples

### Dependências
1. nenhuma além da spec e providers conhecidos

---

## Tarefa 5.6 — Implementar SQL candidate de `MISSING_FK`

**Status:** Concluída em 2026-04-13.

### Objetivo
Gerar SQL candidate seguro e normativo para casos elegíveis de `MISSING_FK`.

### Escopo incluído
1. providers suportados
2. uso de quoting
3. uso de preconditions
4. safety e visibility
5. notas padronizadas

### Escopo excluído
1. execução automática
2. SQLite no MVP
3. candidate em caso ambíguo

### Entradas
1. issue `MISSING_FK` não ambígua
2. provider
3. nome sugerido de constraint
4. preconditions

### Saídas
1. `SqlFixCandidate`

### Regras obrigatórias
1. Só PODE ser gerado quando a issue for não ambígua.
2. Precisa ter `PreconditionsSql`.
3. SQLite é proibido no MVP.
4. Visibility e safety DEVEM seguir a spec.

### Critérios de aceite
1. cases válidos geram candidate
2. ambiguidade bloqueia candidate
3. providers geram SQL correto

### Testes mínimos
1. PostgreSQL
2. SQL Server
3. MySQL
4. caso ambíguo
5. caso sem preconditions

### Dependências
1. 4.2
2. 5.3
3. 5.4
4. 5.5

---

## Tarefa 5.7 — Implementar SQL candidate de `MISSING_REQUIRED_COMMENT`

**Status:** Concluída em 2026-04-13.

### Objetivo
Gerar candidate SQL de comentário quando permitido pelo provider.

### Escopo incluído
1. PostgreSQL tabela/coluna
2. SQL Server tabela/coluna
3. MySQL tabela
4. MySQL coluna apenas com definição completa
5. SQLite proibido

### Escopo excluído
1. execução automática
2. alteração de dados
3. UI

### Entradas
1. issue de comentário ausente
2. provider
3. metadados do alvo
4. preconditions

### Saídas
1. `SqlFixCandidate` de comentário quando elegível

### Regras obrigatórias
1. SQLite NÃO DEVE gerar candidate.
2. MySQL coluna sem definição completa NÃO DEVE gerar candidate.
3. SQL Server DEVE suportar add/update via precondition.
4. Candidate sem preconditions é inválido.

### Critérios de aceite
1. providers suportados geram candidate apropriado
2. casos não suportados não geram candidate

### Testes mínimos
1. PostgreSQL table comment
2. PostgreSQL column comment
3. SQL Server add/update
4. MySQL table comment
5. MySQL column sem definição completa
6. SQLite

### Dependências
1. 4.5
2. 5.4
3. 5.5

---

# FASE 6 — Cache, hashes, performance e determinismo

## Tarefa 6.1 — Implementar `MetadataFingerprint`

**Status:** Concluída em 2026-04-13.

### Objetivo
Gerar hash canônico do metadata estrutural.

### Escopo incluído
1. payload canônico do metadata
2. ordenação normativa
3. SHA-256 UTF-8 hex minúsculo

### Escopo excluído
1. cache storage
2. profile hash
3. issue id

### Entradas
1. metadata validado

### Saídas
1. `MetadataFingerprint`

### Regras obrigatórias
1. O payload DEVE seguir a seção canônica da spec.
2. A ordenação DEVE ser determinística.
3. Mudança estrutural mínima DEVE alterar o hash.

### Critérios de aceite
1. mesmo metadata gera mesmo fingerprint
2. metadata diferente gera fingerprint diferente

### Testes mínimos
1. repetição idêntica
2. mudança em coluna
3. mudança em FK

### Dependências
1. 1.4
2. 3.2

---

## Tarefa 6.2 — Implementar `ProfileContentHash`

**Status:** Concluída em 2026-04-13.

### Objetivo
Gerar hash canônico do profile validado.

### Escopo incluído
1. profile já validado e clamped
2. `RuleSettings` ordenado
3. listas ordenadas deterministicamente
4. SHA-256 UTF-8 hex minúsculo

### Escopo excluído
1. cache storage
2. metadata fingerprint
3. UI

### Entradas
1. profile validado

### Saídas
1. `ProfileContentHash`

### Regras obrigatórias
1. O hash DEVE ser calculado sobre o profile final validado.
2. Defaults DEVEM estar preenchidos antes do hash.

### Critérios de aceite
1. mesmo profile validado gera mesmo hash
2. mudança de config altera o hash

### Testes mínimos
1. repetição idêntica
2. mudança em threshold
3. mudança em allowlist

### Dependências
1. 1.4
2. 3.3

---

## Tarefa 6.3 — Implementar IDs determinísticos

**Status:** Concluída em 2026-04-13.

### Objetivo
Gerar `IssueId`, `SuggestionId` e `CandidateId` de forma determinística.

### Escopo incluído
1. payloads canônicos
2. texto normalizado para hash
3. SHA-256 hex minúsculo

### Escopo excluído
1. cache key
2. serialização externa
3. UI

### Entradas
1. issue
2. suggestion
3. candidate

### Saídas
1. IDs determinísticos

### Regras obrigatórias
1. Todos os campos textuais DEVEM usar texto normalizado para hash.
2. O algoritmo DEVE ser SHA-256.
3. A saída DEVE ser hexadecimal minúscula.

### Critérios de aceite
1. objetos logicamente idênticos geram mesmos IDs
2. mudanças relevantes alteram o ID

### Testes mínimos
1. repeatability
2. mudança de message
3. mudança de confidence

### Dependências
1. 1.4
2. 5.2
3. 5.6
4. 5.7

---

## Tarefa 6.4 — Implementar cache lógico de análise

**Status:** Concluída em 2026-04-13.

### Objetivo
Persistir e recuperar resultados lógicos por chave determinística.

### Escopo incluído
1. chave de cache normativa
2. escrita em cache
3. leitura de cache
4. TTL
5. invalidação
6. semântica de cache hit

### Escopo excluído
1. cache distribuído
2. persistência externa complexa
3. UI

### Entradas
1. fingerprint
2. profile hash
3. provider
4. spec version

### Saídas
1. resultado cacheado ou miss

### Regras obrigatórias
1. `CacheTtlSeconds=0` desabilita leitura e escrita.
2. Cache hit DEVE preservar payload lógico.
3. Cache hit DEVE regenerar apenas metadados materiais.
4. Diagnósticos transitórios NÃO DEVEM poluir entradas persistidas.

### Critérios de aceite
1. hit e miss funcionam
2. TTL expira corretamente
3. mudança em spec/profile/metadata invalida

### Testes mínimos
1. cache hit
2. cache miss por TTL
3. cache miss por profile hash
4. cache miss por fingerprint

### Dependências
1. 6.1
2. 6.2
3. 3.4

---

## Tarefa 6.5 — Implementar paralelismo determinístico

**Status:** Concluída em 2026-04-13.
### Objetivo
Permitir execução paralela sem alterar o resultado lógico final.

### Escopo incluído
1. execução paralela opcional
2. consolidação ordenada
3. comportamento serial com `MaxDegreeOfParallelism=1`

### Escopo excluído
1. paralelismo de UI
2. paralelismo não determinístico
3. acesso direto ao banco

### Entradas
1. regras
2. profile
3. scheduler interno

### Saídas
1. execução paralela controlada

### Regras obrigatórias
1. Paralelismo NÃO PODE alterar ordenação final.
2. Coleções intermediárias DEVEM ser consolidadas antes de emitir resultado final.
3. `EnableParallelRules=true` e `MaxDegreeOfParallelism=1` DEVE equivaler a serial.

### Critérios de aceite
1. execução serial e paralela retornam mesmo resultado lógico
2. ordem final é estável

### Testes mínimos
1. serial vs paralelo
2. `MaxDegreeOfParallelism=1`
3. múltiplas execuções repetidas

### Dependências
1. 3.4
2. 6.4

---

# FASE 7 — UI / MVVM

## Tarefa 7.1 — Implementar estados de tela e ViewModel base

### Objetivo
Representar os estados normativos da análise na Presentation.

### Escopo incluído
1. `Idle`
2. `Loading`
3. `Completed`
4. `Partial`
5. `Cancelled`
6. `Failed`
7. `Empty`

### Escopo excluído
1. score
2. regra
3. cache

### Entradas
1. `SchemaAnalysisResult`
2. status operacional

### Saídas
1. ViewModel de estado da análise

### Regras obrigatórias
1. A Presentation NÃO DEVE recalcular score ou severidade.
2. Mensagens fixas DEVEM seguir a spec.
3. Estado `Empty` DEVE ser coerente com resultado sem issues.

### Critérios de aceite
1. transições funcionam
2. mensagens corretas aparecem
3. UI não altera resultado da engine

### Testes mínimos
1. transição Idle -> Loading
2. Loading -> Completed
3. Loading -> Partial
4. Loading -> Failed

### Dependências
1. 3.4

---

## Tarefa 7.2 — Implementar lista de issues e seleção automática

### Objetivo
Renderizar as issues e manter seleção coerente com filtros e estado.

### Escopo incluído
1. lista de issues
2. seleção inicial automática
3. limpeza de seleção
4. fallback para primeira visível após filtro

### Escopo excluído
1. SQL execution
2. cálculo de issue
3. cache

### Entradas
1. resultado final da engine
2. filtros aplicados

### Saídas
1. item selecionado
2. lista visível filtrada

### Regras obrigatórias
1. Primeira issue ordenada DEVE ser selecionada ao concluir.
2. Filtro que remove a seleção DEVE selecionar a primeira restante.
3. Sem issue visível, a seleção DEVE ser limpa imediatamente.

### Critérios de aceite
1. seleção automática funciona
2. limpeza funciona
3. detalhes vazios aparecem quando não há seleção

### Testes mínimos
1. seleção inicial
2. filtro removendo item selecionado
3. filtro removendo todas as issues

### Dependências
1. 7.1

---

## Tarefa 7.3 — Implementar painel de detalhes, evidências e suggestions

### Objetivo
Exibir detalhes da issue selecionada sem mutar o resultado.

### Escopo incluído
1. mensagem completa
2. evidências ordenadas
3. suggestions
4. diagnósticos correlatos por regra

### Escopo excluído
1. execução de SQL
2. score
3. dedupe

### Entradas
1. issue selecionada

### Saídas
1. painel de detalhes renderizado

### Regras obrigatórias
1. Evidências DEVEM ser ordenadas por `Weight desc`, depois `Key asc`.
2. Sem issue selecionada, a mensagem fixa DEVE ser exibida.
3. Presentation NÃO DEVE recalcular conteúdo lógico.

### Critérios de aceite
1. detalhes exibem corretamente
2. ordenação de evidências respeitada
3. empty state aparece quando necessário

### Testes mínimos
1. issue com evidências
2. issue sem suggestions
3. nenhuma issue selecionada

### Dependências
1. 5.2
2. 7.2

---

## Tarefa 7.4 — Implementar painel de SQL candidates e comandos associados

### Objetivo
Exibir candidates e habilitar ações conforme capability e safety.

### Escopo incluído
1. lista de candidates
2. `CopySql`
3. `ApplyToCanvas`
4. tooltips de indisponibilidade

### Escopo excluído
1. execução no banco
2. alteração da engine
3. score

### Entradas
1. suggestion selecionada
2. candidate selecionado
3. capability e visibility

### Saídas
1. comandos habilitados/desabilitados corretamente

### Regras obrigatórias
1. `CopySql` só habilita com candidate visível selecionado.
2. `ApplyToCanvas` só habilita para `VisibleActionable`.
3. Tooltip fixa DEVE ser usada para ação bloqueada.

### Critérios de aceite
1. habilitação obedece status e visibility
2. painéis vazios exibem mensagem correta

### Testes mínimos
1. candidate visível
2. candidate não acionável
3. ausência de candidate

### Dependências
1. 5.6
2. 5.7
3. 7.3

---

## Tarefa 7.5 — Implementar filtros visuais e resumo bruto/filtrado

### Objetivo
Permitir exploração do resultado na UI sem mutar o payload lógico.

### Escopo incluído
1. filtro por severidade
2. filtro por regra
3. confidence mínima
4. texto de tabela
5. resumo bruto
6. resumo filtrado

### Escopo excluído
1. persistência de sessão
2. alteração do resultado da engine
3. ocultação persistida

### Entradas
1. resultado original
2. filtros ativos

### Saídas
1. lista filtrada
2. contagens filtradas
3. contagens brutas preservadas

### Regras obrigatórias
1. Filtros DEVEM usar lógica AND.
2. Filtros NÃO DEVEM alterar o resumo bruto original.
3. Ocultação de regra na sessão NÃO DEVE alterar o resultado da engine.

### Critérios de aceite
1. filtros funcionam cumulativamente
2. resumo bruto e filtrado coexistem corretamente

### Testes mínimos
1. filtro por severidade
2. filtro por regra
3. filtro por confidence
4. filtro textual
5. combinação de filtros

### Dependências
1. 7.2
2. 7.3

---

# FASE 8 — Testes e aceite final

## Tarefa 8.1 — Implementar suíte unitária por regra

### Objetivo
Cobrir cada regra da taxonomia com cenários essenciais.

### Escopo incluído
1. cenário positivo
2. cenário negativo
3. cenário ambíguo quando aplicável
4. cenário de metadado insuficiente
5. validação de evidência mínima

### Escopo excluído
1. UI
2. cache
3. performance macro

### Entradas
1. regras implementadas

### Saídas
1. suíte de testes por regra

### Regras obrigatórias
1. Cada regra DEVE ter cobertura mínima normativa.
2. Regras de hint DEVEM cobrir ausência de segundo sinal.

### Critérios de aceite
1. suíte verde
2. cobertura mínima por regra concluída

### Testes mínimos
1. conforme o próprio objetivo

### Dependências
1. fase 4

---

## Tarefa 8.2 — Implementar suíte de score, threshold e arredondamento

### Objetivo
Cobrir fronteiras e comportamento de emissão por threshold.

### Escopo incluído
1. fronteiras `0.5499`, `0.5500`, `0.6499`, `0.6500`, `0.8499`, `0.8500`
2. `ToEven`
3. global x rule threshold
4. igualdade ao threshold

### Escopo excluído
1. UI
2. cache

### Entradas
1. score calculado
2. thresholds

### Saídas
1. suíte de fronteira e arredondamento

### Regras obrigatórias
1. Igualdade ao threshold DEVE emitir.
2. Acima do global e abaixo da regra NÃO DEVE emitir.
3. Abaixo do global e acima da regra NÃO DEVE emitir.

### Critérios de aceite
1. todos os cenários fronteira passam

### Testes mínimos
1. todos os valores listados na spec

### Dependências
1. 4.2
2. 1.3

---

## Tarefa 8.3 — Implementar suíte cross-provider

### Objetivo
Validar comportamento provider-specific e degradações.

### Escopo incluído
1. quoting por provider
2. comment SQL por provider
3. ausência de comment SQL no SQLite
4. metadata parcial
5. MySQL column comment com definição completa

### Escopo excluído
1. UI
2. cache

### Entradas
1. providers suportados

### Saídas
1. testes provider-specific

### Regras obrigatórias
1. Toda divergência entre providers DEVE ser coberta.
2. SQLite DEVE ter cobertura explícita de degradação.

### Critérios de aceite
1. testes verdes por provider

### Testes mínimos
1. PostgreSQL
2. SQL Server
3. MySQL
4. SQLite

### Dependências
1. fase 5

---

## Tarefa 8.4 — Implementar suíte de pipeline e edge cases

### Objetivo
Cobrir os cenários especiais do pipeline normativo.

### Escopo incluído
1. schema vazio
2. tabela sem PK
3. tabela sem colunas
4. tabela sem schema explícito
5. timeout
6. cancelamento
7. regra desabilitada
8. cache hit/miss
9. truncamentos
10. ordenação determinística
11. `RequiredCommentTargets=[]`
12. `SynonymGroups=[]`
13. `CompletedWithWarnings` com `Issues=[]`

### Escopo excluído
1. UI

### Entradas
1. pipeline completo

### Saídas
1. suíte de edge cases

### Regras obrigatórias
1. Casos da spec DEVEM ser cobertos literalmente quando possível.

### Critérios de aceite
1. edge cases passam
2. status finais corretos são preservados

### Testes mínimos
1. um teste por item listado

### Dependências
1. fases 3 e 6

---

## Tarefa 8.5 — Implementar suíte de UI/MVVM

### Objetivo
Cobrir estados, comandos, filtros e painéis vazios da Presentation.

### Escopo incluído
1. transições de estado
2. habilitação de comandos
3. filtros cumulativos
4. empty state
5. partial
6. failed
7. visibilidade de ações
8. seleção inicial
9. limpeza de seleção
10. resumo bruto + filtrado

### Escopo excluído
1. testes de regra
2. testes de hashing

### Entradas
1. ViewModels
2. resultados simulados

### Saídas
1. suíte de UI/MVVM

### Regras obrigatórias
1. UI NÃO DEVE alterar resultado lógico da engine.
2. Estados vazios DEVEM ter cobertura explícita.

### Critérios de aceite
1. interação básica funciona
2. estados vazios e comandos bloqueados funcionam

### Testes mínimos
1. um teste por item listado

### Dependências
1. fase 7

---

## Tarefa 8.6 — Checklist final de aceite

### Objetivo
Validar aderência integral da implementação à especificação base.

### Escopo incluído
1. contratos
2. regras
3. score de `MISSING_FK`
4. severidade
5. fallback cross-provider
6. SQL candidates
7. UI
8. cache e hashes
9. testes completos

### Escopo excluído
1. novas features
2. ajustes cosméticos fora do escopo

### Entradas
1. implementação consolidada
2. suíte de testes
3. spec base

### Saídas
1. checklist final preenchido
2. decisão de aceite/rejeição

### Regras obrigatórias
1. A implementação só PODE ser aceita se todos os critérios da seção de aceitação forem atendidos.
2. Falha em item normativo crítico DEVE bloquear aceite.

### Critérios de aceite
1. checklist integralmente verde

### Testes mínimos
1. execução do checklist final

### Dependências
1. todas as tarefas anteriores

---

# 9. Sequência recomendada de execução

A ordem prática recomendada é:

1. 1.1
2. 1.2
3. 1.3
4. 1.4
5. 2.1
6. 2.2
7. 2.3
8. 2.4
9. 2.5
10. 2.6
11. 3.1
12. 3.2
13. 3.3
14. 3.4
15. 3.5
16. 3.6
17. 4.1
18. 4.2
19. 4.3
20. 4.4
21. 4.5
22. 4.6
23. 4.7
24. 4.8
25. 5.1
26. 5.2
27. 5.3
28. 5.4
29. 5.5
30. 5.6
31. 5.7
32. 6.1
33. 6.2
34. 6.3
35. 6.4
36. 6.5
37. 7.1
38. 7.2
39. 7.3
40. 7.4
41. 7.5
42. 8.1
43. 8.2
44. 8.3
45. 8.4
46. 8.5
47. 8.6

---

# 10. Observações finais de implementação para IA

1. A IA DEVE preferir tarefas menores e completas em vez de grandes blocos de implementação.
2. A IA NÃO DEVE implementar UI junto com regra de domínio na mesma tarefa.
3. A IA DEVE sempre validar dependências antes de iniciar uma nova tarefa.
4. A IA DEVE usar testes como mecanismo obrigatório de validação de conclusão.
5. A IA NÃO DEVE introduzir heurísticas novas fora das explicitamente previstas na spec base.
6. A IA DEVE respeitar integralmente os limites inferenciais definidos no documento principal.
7. Toda divergência interpretativa DEVE ser tratada como bloqueio de implementação, e não como licença para improviso.
