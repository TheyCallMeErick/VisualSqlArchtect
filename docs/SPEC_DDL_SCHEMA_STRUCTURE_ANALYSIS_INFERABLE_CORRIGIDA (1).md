# Especificação Normativa Executável
## Análise Estrutural no Modo DDL (Somente Inferível)

## 1. Objetivo
Esta especificação DEFINE, de forma normativa, determinística e congelável, o comportamento da feature de análise estrutural no modo DDL para implementação por IA.

A feature DEVE:
1. analisar schema e metadados estruturais;
2. emitir issues com evidências e confiança;
3. emitir sugestões textuais e SQL fix candidates quando permitido;
4. operar SOMENTE com inferência técnica permitida.

A feature NÃO DEVE:
1. inferir regra de negócio funcional;
2. inferir intenção semântica de domínio fora do schema;
3. executar alterações no banco;
4. depender de interpretação subjetiva do implementador.

---

## 2. Escopo

## 2.1 Escopo coberto
A implementação DEVE cobrir:
1. verificação de FK catalogada inconsistente;
2. detecção de `MISSING_FK` por naming + tipo + topologia;
3. validação de convenção de nomes;
4. detecção de nomes de baixa qualidade;
5. verificação de comentários obrigatórios ausentes;
6. indícios de 1FN/2FN/3FN com limitação inferencial;
7. geração de SQL candidate para regras permitidas;
8. execução com timeout, cancelamento e retorno parcial;
9. cache determinístico por fingerprint + profile + provider + versão da spec;
10. UI com estados, comandos e comportamento de seleção normatizados.

## 2.2 Escopo proibido
A implementação NÃO DEVE:
1. ler dados de linhas para inferência;
2. inferir regra funcional do negócio;
3. executar SQL candidate automaticamente;
4. emitir SQL para regra não autorizada;
5. elevar indícios de normalização a diagnóstico absoluto;
6. depender de heurísticas externas ao documento.

## 2.3 Limites inferenciais
1. A engine SOMENTE PODE usar `DbMetadata`, configurações e capacidades por provider.
2. Toda issue DEVE indicar evidências técnicas explícitas.
3. Toda hipótese fora do limite inferencial DEVE ser descartada.
4. Metadado ausente ou incompleto DEVE degradar a análise para diagnóstico e nunca para falso positivo.

---

## 3. Glossário normativo
1. **Issue**: problema detectado por uma regra canônica.
2. **Evidence**: fato técnico atômico usado para justificar issue.
3. **Confidence**: escore final normalizado em `[0.0000, 1.0000]`.
4. **Severity**: criticidade objetiva (`Info`, `Warning`, `Critical`) definida por matriz normativa.
5. **Suggestion**: recomendação textual vinculada a issue.
6. **SQL Fix Candidate**: SQL sugerido, nunca executado pela engine.
7. **Inferable**: dedutível exclusivamente por metadado estrutural disponível.
8. **Ambiguous Match**: múltiplos alvos válidos com diferença de score `< 0.0500`.
9. **Partial Result**: resultado incompleto por timeout, cancelamento ou falha de regra tolerada.
10. **Provider Capability**: suporte técnico disponível no provider para metadado ou SQL.
11. **Metadata Snapshot**: instância imutável de metadados de entrada para uma execução.
12. **Normalization Hint**: indício de 1FN, 2FN ou 3FN sem valor de diagnóstico definitivo.
13. **Destructive Suggestion**: SQL candidate com potencial de perda de dados ou alteração irreversível.
14. **Token principal**: primeiro token não estrutural após normalização de nome; se inexistente, o token principal é `null`.
15. **Comentário não branco**: string que, após `Trim()`, possui comprimento `> 0`.
16. **Schema name canônico**: valor normalizado por regra da seção 10.2.
17. **JSON canônico**: serialização JSON determinística definida na seção 19.4.
18. **Constraint equivalente**: FK já existente entre o mesmo child table, mesmo conjunto ordenado de child columns, mesmo parent table e mesmo conjunto ordenado de parent columns, independentemente do nome físico da constraint.
19. **Texto normalizado para hash**: string convertida para Unicode NFC, `Trim()`, colapso de whitespace contíguo para um único espaço, `ToLowerInvariant()`, com `null` representado pelo literal `∅`.

---

## 4. Arquitetura e responsabilidades por camada

## 4.1 Domain
Domain DEVE conter:
1. enums normativos;
2. contratos imutáveis de análise;
3. regras canônicas;
4. algoritmos de nomeação, score, severidade, deduplicação e geração de IDs determinísticos.

Domain NÃO DEVE conhecer:
1. UI, Avalonia, ViewModel;
2. persistência de arquivo;
3. DI container;
4. execução de SQL em banco.

## 4.2 Application
Application DEVE conter:
1. orquestração do pipeline;
2. timeout e cancelamento;
3. execução de regras;
4. agregação de resultado parcial e final;
5. cache hit e miss com invalidação normativa;
6. mapeamento entre diagnósticos de execução e `SchemaAnalysisStatus`.

Application NÃO DEVE:
1. redefinir lógica de regra;
2. conhecer controles visuais;
3. executar DDL no banco.

## 4.3 Infrastructure
Infrastructure DEVE conter:
1. integração com `DbMetadata`;
2. adapters por provider para SQL candidate;
3. normalização de quoting por provider;
4. persistência de cache em memória ou processo;
5. serialização canônica auxiliar para cálculo de hash.

Infrastructure NÃO DEVE:
1. decidir severidade;
2. decidir score;
3. aplicar filtros de UI.

## 4.4 Presentation
Presentation DEVE conter:
1. estados de tela;
2. comandos;
3. filtros e ordenação visual;
4. renderização de issues, suggestions e candidates;
5. ação de cópia de SQL;
6. ação de aplicação no canvas quando permitida;
7. seleção padrão, seleção vazia e limpeza de painéis derivada de filtro.

Presentation NÃO DEVE:
1. recalcular score ou severidade;
2. recalcular dedupe;
3. alterar o resultado da engine.

---

## 5. Integração com componentes existentes

## 5.1 `DbMetadata`
1. É a única fonte de entrada estrutural.
2. A engine DEVE tratar `DbMetadata` como snapshot imutável.
3. A engine NÃO DEVE acessar banco diretamente.
4. Caso `DbMetadata` não exponha algum metadado requerido por regra, a regra DEVE degradar conforme este documento.

## 5.2 `AutoJoinDetector`
1. `AutoJoinDetector` NÃO DEVE ser usado para emissão de issue.
2. Utilitários puros de tokenização e singularização PODEM ser compartilhados somente se extraídos para componente comum determinístico.
3. O componente compartilhado DEVE obedecer exatamente à seção 10.

## 5.3 `DdlSchemaImporter`
1. Pode ser usado SOMENTE pela ação de UI “Aplicar no canvas”.
2. Não participa de score, severidade ou emissão de issue.
3. Não altera resultado da análise.
4. Em caso de candidate não aplicável ao canvas, a UI DEVE manter a ação desabilitada.

## 5.4 `AppSettingsStore`
1. O profile DEVE ser carregado e salvo via `AppSettingsStore`.
2. Campos ausentes ou inválidos DEVEM aplicar defaults normativos.
3. Correções de validação DEVEM gerar diagnóstico `ANL-SETTINGS-CLAMPED`.

Acoplamentos proibidos:
1. regras acessando `AppSettingsStore` diretamente;
2. UI acessando regra para alterar algoritmo;
3. adapter de provider alterando score ou severity.

---

## 6. Contratos normativos de dados

## 6.1 Enums oficiais (lista fechada)
```csharp
public enum SchemaAnalysisStatus { Completed, CompletedWithWarnings, Partial, Cancelled, Failed }
public enum SchemaIssueSeverity { Info, Warning, Critical }

public enum SchemaRuleCode {
  FK_CATALOG_INCONSISTENT,
  MISSING_FK,
  NAMING_CONVENTION_VIOLATION,
  LOW_SEMANTIC_NAME,
  MISSING_REQUIRED_COMMENT,
  NF1_HINT_MULTI_VALUED,
  NF2_HINT_PARTIAL_DEPENDENCY,
  NF3_HINT_TRANSITIVE_DEPENDENCY
}

public enum SchemaTargetType { Schema, Table, Column, Constraint }

public enum EvidenceKind {
  MetadataFact,
  NamingMatch,
  TypeCompatibility,
  ConstraintTopology,
  PolicyRequirement,
  Ambiguity,
  ProviderLimitation,
  ThresholdDecision,
  ExecutionBoundary
}

public enum SqlCandidateSafety { NonDestructive, PotentiallyDestructive, Destructive }
public enum NamingConvention { SnakeCase, CamelCase, PascalCase, KebabCase, MixedAllowed }
public enum NormalizationStrictness { Conservative, Balanced, Aggressive }
public enum CandidateVisibility { Hidden, VisibleReadOnly, VisibleActionable }
public enum RuleExecutionState { NotStarted, Running, Completed, Skipped, Failed, TimedOut, Cancelled }
```

## 6.2 Contratos
```csharp
public sealed record SchemaEvidence(
  EvidenceKind Kind,
  string Key,
  string Value,
  double Weight,
  string? SourcePath = null
);

public sealed record SqlFixCandidate(
  string CandidateId,
  DatabaseProvider Provider,
  string Title,
  string Sql,
  IReadOnlyList<string> PreconditionsSql,
  SqlCandidateSafety Safety,
  CandidateVisibility Visibility,
  bool IsAutoApplicable,
  IReadOnlyList<string> Notes
);

public sealed record SchemaSuggestion(
  string SuggestionId,
  string Title,
  string Description,
  double Confidence,
  IReadOnlyList<SqlFixCandidate> SqlCandidates
);

public sealed record SchemaIssue(
  string IssueId,
  SchemaRuleCode RuleCode,
  SchemaIssueSeverity Severity,
  double Confidence,
  SchemaTargetType TargetType,
  string? SchemaName,
  string? TableName,
  string? ColumnName,
  string? ConstraintName,
  string Title,
  string Message,
  IReadOnlyList<SchemaEvidence> Evidence,
  IReadOnlyList<SchemaSuggestion> Suggestions,
  bool IsAmbiguous
);

public sealed record SchemaRuleExecutionDiagnostic(
  string Code,
  string Message,
  SchemaRuleCode? RuleCode,
  RuleExecutionState State,
  bool IsFatal
);

public sealed record SchemaAnalysisSummary(
  int TotalIssues,
  int InfoCount,
  int WarningCount,
  int CriticalCount,
  IReadOnlyDictionary<SchemaRuleCode, int> PerRuleCount,
  IReadOnlyDictionary<string, int> PerTableCount
);

public sealed record SchemaAnalysisPartialState(
  bool IsPartial,
  string ReasonCode,
  int CompletedRules,
  int TotalRules
);

public sealed record SchemaAnalysisResult(
  string AnalysisId,
  SchemaAnalysisStatus Status,
  DatabaseProvider Provider,
  string DatabaseName,
  DateTimeOffset StartedAtUtc,
  DateTimeOffset CompletedAtUtc,
  long DurationMs,
  string MetadataFingerprint,
  string ProfileContentHash,
  int ProfileVersion,
  SchemaAnalysisPartialState PartialState,
  IReadOnlyList<SchemaIssue> Issues,
  IReadOnlyList<SchemaRuleExecutionDiagnostic> Diagnostics,
  SchemaAnalysisSummary Summary
);

public sealed record SchemaRuleSetting(
  bool Enabled,
  double MinConfidence,
  int MaxIssues
);

public sealed record SchemaAnalysisProfile(
  int Version,
  bool Enabled,
  double MinConfidenceGlobal,
  int TimeoutMs,
  bool AllowPartialOnTimeout,
  bool AllowPartialOnRuleFailure,
  bool EnableParallelRules,
  int MaxDegreeOfParallelism,
  int MaxIssues,
  int MaxSuggestionsPerIssue,
  NamingConvention NamingConvention,
  NormalizationStrictness NormalizationStrictness,
  IReadOnlyList<string> RequiredCommentTargets,
  IReadOnlyList<string> LowQualityNameDenylist,
  IReadOnlyList<string> NameAllowlist,
  IReadOnlyList<IReadOnlyList<string>> SynonymGroups,
  IReadOnlyList<string> SemiStructuredPayloadAllowlist,
  bool DebugDiagnostics,
  IReadOnlyDictionary<SchemaRuleCode, SchemaRuleSetting> RuleSettings,
  int CacheTtlSeconds
);
```

## 6.3 Nulabilidade, cardinalidade e invariantes obrigatórios
1. `Evidence.Count >= 1`.
2. `Suggestions.Count <= MaxSuggestionsPerIssue` por issue.
3. `Issues.Count <= MaxIssues`.
4. `RuleSettings` DEVE conter todas as `SchemaRuleCode`.
5. `TargetType=Column` exige `TableName` e `ColumnName` não nulos.
6. `TargetType=Constraint` exige `ConstraintName` não nulo.
7. `Confidence` DEVE ser arredondada com `Math.Round(v, 4, MidpointRounding.ToEven)`.
8. `IsAutoApplicable=true` SOMENTE quando `Safety=NonDestructive` e capability permite.
9. `CandidateVisibility=VisibleActionable` SOMENTE quando `Safety=NonDestructive`.
10. `SchemaName`, `TableName`, `ColumnName` e `ConstraintName` DEVEM ser tratados como `null` quando vazios após `Trim()`.

## 6.4 Serialização
1. JSON em `camelCase`.
2. Enums por string.
3. Strings vazias para nomes de schema DEVEM ser normalizadas para `null` internamente.
4. `schemaName=null`, `schemaName=""` e schema default DEVEM convergir para schema canônico por provider.
5. Arrays com semântica de conjunto usados em hashes DEVEM ser serializados já ordenados deterministicamente.

## 6.5 Semântica operacional de `SchemaAnalysisStatus`
1. `Completed` DEVE ser usado quando a execução termina sem timeout, sem cancelamento, sem falha de regra não tolerada e sem qualquer diagnóstico classificado como warning operacional.
2. `CompletedWithWarnings` DEVE ser usado quando a execução termina completa, mas existe ao menos um diagnóstico não fatal dentre:
   - `ANL-SETTINGS-CLAMPED`
   - `ANL-SETTINGS-SYNONYM-CONFLICT`
   - `ANL-SETTINGS-VERSION-FALLBACK`
   - `ANL-METADATA-PARTIAL`
   - `ANL-RULE-DISABLED`
   - `ANL-RULE-MAX-ISSUES-TRUNCATED`
   - `ANL-GLOBAL-MAX-ISSUES-TRUNCATED`
   - `ANL-SETTINGS-ALLOWLIST-OVERRIDES-DENYLIST`
3. `Partial` DEVE ser usado quando a execução encerra com resultado parcial por timeout, cancelamento com issues já materializadas ou falha de regra tolerada.
4. `Cancelled` DEVE ser usado quando há cancelamento antes de qualquer issue materializada.
5. `Failed` DEVE ser usado quando timeout sem parcial permitido, falha fatal de regra, invalidação fatal de input ou cancelamento antes de qualquer materialização e sem política de parcial.

---

## 7. Taxonomia oficial de regras e códigos

| RuleCode | Nome canônico | Descrição | Escopo | Severidade default | Configurável | SQL | Evidência mínima |
|---|---|---|---|---|---|---|---|
| `FK_CATALOG_INCONSISTENT` | FK Catalog Inconsistent | FK existente com inconsistência estrutural | constraint | Critical | Sim | Não | 2 |
| `MISSING_FK` | Missing FK by Naming | Coluna candidata a FK sem constraint formal | column | Warning | Sim | Sim | 3 |
| `NAMING_CONVENTION_VIOLATION` | Naming Convention Violation | Nome fora da convenção configurada | table/column/constraint | Warning | Sim | Não | 1 |
| `LOW_SEMANTIC_NAME` | Low Semantic Name | Nome de baixa qualidade técnica | table/column | Info | Sim | Não | 1 |
| `MISSING_REQUIRED_COMMENT` | Missing Required Comment | Comentário obrigatório ausente | table/column | Warning | Sim | Sim | 2 |
| `NF1_HINT_MULTI_VALUED` | 1NF Hint Multi-valued | Indício de não atomicidade | column | Info | Sim | Não | 2 |
| `NF2_HINT_PARTIAL_DEPENDENCY` | 2NF Hint Partial Dependency | Indício de dependência parcial | column | Warning | Sim | Não | 2 |
| `NF3_HINT_TRANSITIVE_DEPENDENCY` | 3NF Hint Transitive Dependency | Indício de dependência transitiva | column | Warning | Sim | Não | 2 |

---

## 8. Catálogo normativo de diagnósticos

| Code | Mensagem padrão | IsFatal | Afeta status |
|---|---|---:|---|
| `ANL-SETTINGS-CLAMPED` | `Configuração fora da faixa normativa foi ajustada para valor válido.` | Não | `CompletedWithWarnings` |
| `ANL-SETTINGS-SYNONYM-CONFLICT` | `Um token pertence a múltiplos grupos de sinônimos; prevaleceu o primeiro grupo.` | Não | `CompletedWithWarnings` |
| `ANL-SETTINGS-VERSION-FALLBACK` | `A versão de configuração excede a versão suportada; foi usada a última versão suportada.` | Não | `CompletedWithWarnings` |
| `ANL-SETTINGS-ALLOWLIST-OVERRIDES-DENYLIST` | `A allowlist suprimiu um match da denylist.` | Não | `CompletedWithWarnings` |
| `ANL-METADATA-PARTIAL` | `Metadado necessário à regra não está disponível no snapshot.` | Não | `CompletedWithWarnings` |
| `ANL-RULE-FAILED` | `Uma regra falhou e a execução seguiu conforme política de parcial.` | Não | `Partial` |
| `ANL-TIMEOUT` | `A execução atingiu o timeout global configurado.` | Não | `Partial` ou `Failed` |
| `ANL-RULE-MAX-ISSUES-TRUNCATED` | `Uma regra excedeu o limite máximo de issues configurado.` | Não | `CompletedWithWarnings` |
| `ANL-GLOBAL-MAX-ISSUES-TRUNCATED` | `O resultado global excedeu o limite máximo de issues configurado.` | Não | `CompletedWithWarnings` |
| `ANL-RULE-DISABLED` | `A regra foi desabilitada por configuração.` | Não | `CompletedWithWarnings` somente se `DebugDiagnostics=true` |
| `ANL-CACHE-BYPASSED` | `O cache não pôde ser utilizado por ausência de chave válida ou TTL expirado.` | Não | Não altera |
| `ANL-CACHE-HIT` | `Resultado retornado do cache válido.` | Não | Não altera |

---

## 9. Pipeline normativo

## 9.1 Fluxo exato
1. Receber `MetadataSnapshot` e `SchemaAnalysisProfile`.
2. Validar e normalizar profile.
3. Calcular `MetadataFingerprint`.
4. Calcular `ProfileContentHash`.
5. Consultar cache por chave determinística.
6. Se cache hit válido, retornar resultado cacheado com novo `AnalysisId`, novo tempo de execução real e diagnóstico `ANL-CACHE-HIT`.
7. Pré-indexar metadados.
8. Executar regras na ordem da seção 14.1.
9. Aplicar limites por regra (`RuleSetting.MaxIssues`) antes do merge global.
10. Aplicar dedupe global.
11. Aplicar filtros de threshold.
12. Truncar em `MaxIssues` com diagnóstico.
13. Ordenar resultado final deterministicamente.
14. Gerar suggestions e SQL candidates.
15. Limitar suggestions por issue.
16. Gerar resumo e diagnósticos.
17. Persistir cache, se aplicável.
18. Calcular status final.
19. Retornar resultado.

## 9.2 Pré-indexações obrigatórias
1. `tableByFullName`.
2. `columnsByTable`.
3. `pkColumnsByTable`.
4. `uniqueConstraintsByTable`.
5. `fkByChildTable`.
6. `fkByParentTable`.
7. `normalizedNameIndex` por tabela, coluna e constraint.
8. `ruleExecutionState` inicial para todas as regras.
9. `constraintNamesBySchema`.
10. `tableKindsByFullName`.

## 9.3 Ordenação final obrigatória
1. `Severity` desc (`Critical`, `Warning`, `Info`).
2. `Confidence` desc.
3. `RuleCode` asc.
4. `SchemaName` asc (`null` por último).
5. `TableName` asc (`null` por último).
6. `ColumnName` asc (`null` por último).
7. `ConstraintName` asc (`null` por último).
8. `IssueId` asc.

## 9.4 Deduplicação obrigatória
Chave:
`ruleCode|targetType|schema|table|column|constraint|sha256(messageNormalized)`

Retenção:
1. maior `Confidence`;
2. maior `Severity`;
3. menor `IssueId`.

`messageNormalized` DEVE ser o `Message` normalizado pela definição de “Texto normalizado para hash”.

## 9.5 Timeout, cancelamento e parcial
1. Timeout global DEVE interromper criação de novas issues.
2. Se timeout ocorrer:
   - `AllowPartialOnTimeout=true`: `Status=Partial`.
   - `AllowPartialOnTimeout=false`: `Status=Failed`, `Issues=[]`.
3. Cancelamento em qualquer fase DEVE parar execução.
4. Cancelamento durante consolidação DEVE retornar `Partial` se houver issues intermediárias; caso contrário, `Cancelled`.
5. Falha de regra:
   - `AllowPartialOnRuleFailure=true`: registrar diagnóstico e continuar.
   - `false`: `Status=Failed`.
6. `PartialState.ReasonCode` DEVE ser um dentre `NONE`, `TIMEOUT`, `CANCELLED`, `RULE_FAILURE`.

## 9.6 Materialização de suggestions
1. Suggestions DEVEM ser construídas após a issue finalizada e após o threshold.
2. `MaxSuggestionsPerIssue` DEVE truncar por ordem de prioridade: maior `Confidence`, depois `Title` asc, depois `SuggestionId` asc.
3. SQL candidate DEVE ser omitido se não seguro ou não compatível.
4. Se não houver suggestion, o campo `Suggestions` DEVE ser lista vazia e nunca `null`.

---

## 10. Política de inferência

| Tipo | Permitido | Requisito | SQL permitido |
|---|---|---|---|
| Inferência estrutural direta | Sim | metadado explícito | Sim, conforme regra |
| Inferência por nomeação | Sim | score >= threshold | Sim, apenas `MISSING_FK` não ambígua |
| Inferência de domínio funcional | Não | proibido | Não |
| Normalização (1FN/2FN/3FN) | Sim, como hint | evidência objetiva mínima | Não |
| Caso ambíguo | Sim | marcar `IsAmbiguous=true` | Não |
| Metadado insuficiente | Sim, diagnóstico | não emitir falso positivo | Não |

Regra central: a engine NÃO DEVE extrapolar semântica funcional não representada tecnicamente.

---

## 11. Algoritmo de nomenclatura e matching

## 11.1 Tokenização e normalização
Ordem obrigatória:
1. Aplicar Unicode NFD.
2. Remover diacríticos.
3. Aplicar `ToLowerInvariant()`.
4. Substituir qualquer caractere não alfanumérico ASCII por separador lógico.
5. Split por `_`, `-`, `.`, espaço e pelos separadores lógicos gerados.
6. Split camel e pascal por transição minúscula→maiúscula antes do passo 3 quando o nome bruto possuir ASCII alfabético.
7. Preservar siglas contínuas reconhecidas no conjunto fechado `{id, cpf, cnpj, uuid, url}` como token único.
8. Remover tokens vazios.
9. Singularizar.
10. Aplicar sinônimos.
11. Separar tokens estruturais de tokens de entidade.

Se após o passo 11 não restarem tokens de entidade, o nome NÃO DEVE ser candidato válido para regras que exijam entidade.

## 11.2 Normalização de schema name
1. PostgreSQL default: `public`.
2. SQL Server default: `dbo`.
3. MySQL default: `null` (schema lógico = database).
4. SQLite default: `main`.

Regras:
1. `null`, vazio e whitespace DEVEM virar schema default do provider.
2. Comparações DEVEM usar schema canônico.
3. Para MySQL, o schema canônico permanece `null` e comparações DEVEM usar apenas database e table.

## 11.3 Tokens estruturais
Conjunto fechado: `id`, `fk`, `ref`, `code`.
1. Tokens estruturais não definem entidade.
2. `id_pessoa`, `pessoa_id`, `PessoaId`, `IdPessoa` DEVEM gerar entidade `pessoa` + marcador `id`.
3. Nomes contendo somente tokens estruturais NUNCA DEVEM ser considerados match de entidade.

## 11.4 Singularização
Por token:
1. `ies -> y`
2. `(ss|sh|ch|x|z)es -> remove es`
3. `([^s])ses -> $1s`
4. `s -> remove`, exceto final `ss` e `us`

## 11.5 Sinônimos
1. `SynonymGroups` define equivalência total dentro do grupo.
2. Conflito de token em múltiplos grupos: prevalece o primeiro grupo por ordem; registrar `ANL-SETTINGS-SYNONYM-CONFLICT`.
3. Matching por sinônimo DEVE ser aplicado após singularização.
4. Lista vazia de `SynonymGroups` é válida e significa ausência de matching por sinônimo.

## 11.6 Allowlist x Denylist
1. Se nome estiver em `NameAllowlist`, a regra `LOW_SEMANTIC_NAME` NÃO DEVE emitir, mesmo se denylist casar.
2. Precedência obrigatória: Allowlist > Denylist.
3. O override DEVE registrar `ANL-SETTINGS-ALLOWLIST-OVERRIDES-DENYLIST`.

## 11.7 Matching exato e por sinônimo
1. Exato: token entidade == token nome da tabela destino normalizado.
2. Sinônimo: token entidade no mesmo grupo de token destino.
3. Match sem token entidade DEVE ser inválido.
4. Constraint names DEVEM usar o mesmo pipeline da seção 11.1 para validação de naming, mas NÃO participam do algoritmo de `MISSING_FK`.

## 11.8 Regex normativas por convenção
1. `SnakeCase`: `^[a-z][a-z0-9]*(?:_[a-z0-9]+)*$`
2. `CamelCase`: `^[a-z][A-Za-z0-9]*$`
3. `PascalCase`: `^[A-Z][A-Za-z0-9]*$`
4. `KebabCase`: `^[a-z][a-z0-9]*(?:-[a-z0-9]+)*$`

Regras:
1. Nomes iniciados por dígito DEVEM ser inválidos em todas as convenções.
2. Após normalização ASCII, qualquer caractere residual fora de `[A-Za-z0-9_-]` DEVE invalidar o nome.
3. `MixedAllowed` desabilita a regra `NAMING_CONVENTION_VIOLATION`.

---

## 12. Compatibilidade de tipos

## 12.1 Categorias canônicas
Categorias fechadas:
1. `Integer`
2. `Decimal`
3. `String`
4. `DateTime`
5. `Boolean`
6. `Guid`
7. `Binary`
8. `JsonXml`
9. `Other`

## 12.2 Mapeamento mínimo
1. `int`, `integer`, `bigint`, `smallint`, `serial`, `bigserial`, `tinyint` -> `Integer`
2. `decimal`, `numeric`, `money`, `float`, `double`, `real` -> `Decimal`
3. `char`, `varchar`, `nvarchar`, `nchar`, `text`, `longtext` -> `String`
4. `date`, `time`, `timestamp`, `datetime`, `datetime2` -> `DateTime`
5. `bool`, `boolean`, `bit` -> `Boolean`
6. `uuid`, `uniqueidentifier` -> `Guid`
7. `blob`, `varbinary`, `binary`, `bytea` -> `Binary`
8. `json`, `jsonb`, `xml` -> `JsonXml`
9. Tipos não mapeados -> `Other`

## 12.3 Regras de compatibilidade
1. **Exata**: tipos normalizados iguais.
2. **Semântica forte**: mesma categoria canônica.
3. **Semântica fraca**: `Integer` com `Decimal`.
4. **Incompatível**: qualquer outra combinação.

## 12.4 Uso por regra
1. `MISSING_FK` PODE usar compatibilidade exata e semântica forte.
2. `MISSING_FK` com compatibilidade semântica fraca DEVE resultar em score zero.
3. `FK_CATALOG_INCONSISTENT` com compatibilidade semântica fraca DEVE emitir `Warning`.
4. `FK_CATALOG_INCONSISTENT` com incompatibilidade total DEVE emitir `Critical`.

---

## 13. Estratégia de score e confiança

## 13.1 Regras gerais
1. Score bruto clampado em `[0,1]`.
2. Arredondamento para 4 casas com `ToEven`.
3. Emissão exige `score >= max(minConfidenceGlobal, minConfidenceRule)`.
4. Score exatamente igual ao threshold DEVE emitir.

## 13.2 Fórmula fechada `MISSING_FK`
Variáveis:
- `E_exactEntityMatch`
- `E_synonymEntityMatch`
- `E_idPattern`
- `E_typeExact`
- `E_typeSemantic`
- `E_targetPk`
- `E_targetUq`
- `E_indexedSource`
- `P_ambiguousTargets`
- `P_genericName`

Fórmula:
```text
score =
+0.30*E_exactEntityMatch
+0.20*E_synonymEntityMatch
+0.15*E_idPattern
+0.20*E_typeExact
+0.10*E_typeSemantic*(1-E_typeExact)
+0.12*E_targetPk
+0.08*E_targetUq*(1-E_targetPk)
+0.05*E_indexedSource
-0.20*P_ambiguousTargets
-0.15*P_genericName
```

Cortes:
1. sem match entidade (`E_exactEntityMatch=0` e `E_synonymEntityMatch=0`) => score 0.
2. sem compatibilidade de tipo (`E_typeExact=0` e `E_typeSemantic=0`) => score 0.
3. `E_typeSemantic=1` só é permitido quando a compatibilidade for semântica forte.

## 13.3 Scores das demais regras
1. `FK_CATALOG_INCONSISTENT = 0.9500`
2. `NAMING_CONVENTION_VIOLATION = 0.9000`
3. `LOW_SEMANTIC_NAME = 0.7500`
4. `MISSING_REQUIRED_COMMENT = 0.8500` ou `0.6000` com limitação de provider
5. `NF1_HINT_MULTI_VALUED`: seção 14.6
6. `NF2_HINT_PARTIAL_DEPENDENCY`: seção 14.7
7. `NF3_HINT_TRANSITIVE_DEPENDENCY`: seção 14.8

## 13.4 Empates
1. maior score;
2. match exato > sinônimo;
3. alvo PK > UQ;
4. menor `tableFullName` lexicográfico.

---

## 14. Matriz de severidade (objetiva)

| Regra | Condição | Severidade |
|---|---|---|
| `FK_CATALOG_INCONSISTENT` | coluna referenciada inexistente ou inválida | Critical |
| `FK_CATALOG_INCONSISTENT` | compatibilidade semântica fraca | Warning |
| `MISSING_FK` | não ambígua e score >= 0.8500 | Critical |
| `MISSING_FK` | não ambígua e 0.6500..0.8499 | Warning |
| `MISSING_FK` | ambígua | Warning |
| `NAMING_CONVENTION_VIOLATION` | qualquer emissão | Warning |
| `LOW_SEMANTIC_NAME` | qualquer emissão | Info |
| `MISSING_REQUIRED_COMMENT` | provider com suporte confiável | Warning |
| `MISSING_REQUIRED_COMMENT` | provider sem suporte confiável | Info |
| `NF1`, `NF2`, `NF3` | score >= 0.8000 | Warning |
| `NF1`, `NF2`, `NF3` | score < 0.8000 | Info |

---

## 15. Regras de análise

## 15.1 Ordem fixa
1. `FK_CATALOG_INCONSISTENT`
2. `MISSING_FK`
3. `NAMING_CONVENTION_VIOLATION`
4. `LOW_SEMANTIC_NAME`
5. `MISSING_REQUIRED_COMMENT`
6. `NF1_HINT_MULTI_VALUED`
7. `NF2_HINT_PARTIAL_DEPENDENCY`
8. `NF3_HINT_TRANSITIVE_DEPENDENCY`

## 15.2 `FK_CATALOG_INCONSISTENT`
Entradas:
1. FKs catalogadas
2. colunas origem e destino
3. tipos origem e destino

Pré-condição:
1. FK existe no catálogo.

Emite quando:
1. coluna child não existe;
2. coluna parent não existe;
3. tipo incompatível;
4. tipo com compatibilidade semântica fraca.

Não emite quando:
1. todos os componentes existem;
2. tipo é compatível exato ou semântico forte.

Evidência mínima:
1. `MetadataFact`
2. `TypeCompatibility` ou `ConstraintTopology`

SQL:
1. proibido.

Metadado insuficiente:
1. emitir `ANL-METADATA-PARTIAL`;
2. não emitir issue.

## 15.3 `MISSING_FK`
Entradas:
1. coluna candidata;
2. destinos possíveis;
3. PK e UQ;
4. tipo;
5. índices da coluna origem.

Pré-condições:
1. coluna não-PK;
2. coluna não já-FK;
3. nome com entidade inferível;
4. coluna não composta somente de tokens estruturais.

Algoritmo:
1. aplicar seções 11, 12 e 13.2;
2. escolher maior score;
3. aplicar desempates;
4. marcar `IsAmbiguous=true` se diferença `< 0.0500` após desempates lógicos sem resolver unicidade.

Não emite quando:
1. sem candidato válido;
2. sem compatibilidade exata ou semântica forte;
3. score abaixo do threshold.

Evidência mínima:
1. `NamingMatch`
2. `TypeCompatibility`
3. `ConstraintTopology`

SQL:
1. permitido sob seção 18.

## 15.4 `NAMING_CONVENTION_VIOLATION`
Entradas:
1. nomes de tabela, coluna e constraint.

Pré-condição:
1. `NamingConvention != MixedAllowed`.

Emite quando:
1. regex normativa falha.

Não emite quando:
1. regex passa.

SQL:
1. proibido.

Evidência mínima:
1. `PolicyRequirement`

## 15.5 `LOW_SEMANTIC_NAME`
Entradas:
1. nome normalizado.

Emite quando:
1. token principal na denylist;
2. regex fraca `^(col|campo|field|tmp|test|data|valor|misc|x|y|z)[0-9_]*$`;
3. não existir allowlist correspondente.

Não emite quando:
1. nome em allowlist;
2. token principal é `null` e o nome é composto apenas por tokens estruturais; nesse caso o nome é ignorado por esta regra.

Escopo:
1. tabela
2. coluna
3. constraint names NÃO participam desta regra.

SQL:
1. proibido.

Evidência mínima:
1. `MetadataFact`

## 15.6 `MISSING_REQUIRED_COMMENT`
Alvos válidos em `RequiredCommentTargets`:
1. `Table`
2. `PrimaryKeyColumn`
3. `ForeignKeyColumn`
4. `UniqueColumn`
5. `AuditColumn`

Definições fechadas:
1. `PrimaryKeyColumn`: coluna pertencente a qualquer PK, simples ou composta.
2. `ForeignKeyColumn`: coluna pertencente a qualquer FK outbound catalogada.
3. `UniqueColumn`: coluna pertencente a constraint unique de coluna simples; unique composto NÃO conta.
4. `AuditColumn`: coluna cujo nome normalizado seja um dentre `created_at`, `updated_at`, `deleted_at`, `created_by`, `updated_by`, `deleted_by`.

Validação de comentário:
1. comentário ausente = `null`, vazio ou whitespace;
2. comentário igual ao nome do objeto, após normalização textual para hash, DEVE ser tratado como comentário inválido fraco e emitir a regra normalmente.

Emite quando:
1. alvo obrigatório não possui comentário válido.

Não emite quando:
1. `RequiredCommentTargets` é lista vazia;
2. provider não suporta comentário de forma confiável e a política decidir degradar para `Info`, ainda assim a issue PODE ser emitida como `Info` com `ProviderLimitation`.

SQL:
1. permitido apenas para PostgreSQL, SQL Server e MySQL;
2. proibido para SQLite.

Evidência mínima:
1. `PolicyRequirement`
2. `ProviderLimitation` ou `MetadataFact`

## 15.7 `NF1_HINT_MULTI_VALUED`
Score:
```text
0.40
+0.25 se token forte em {list,lista,itens,items,csv,tags,values,valores}
+0.20 se default textual contém ','
+0.15 se tipo json/xml e tabela possui >=1 FK outbound
-0.20 se coluna em semiStructuredPayloadAllowlist
```

Regras:
1. Emite no threshold.
2. Não emite sem dois sinais objetivos.
3. `SemiStructuredPayloadAllowlist` DEVE conter nomes completos normalizados `schema.table.column` ou `table.column`.
4. SQL é proibido.

## 15.8 `NF2_HINT_PARTIAL_DEPENDENCY`
Pré-condição:
1. PK composta com 2 ou mais colunas.

Definições fechadas:
1. **coluna descritiva**: coluna cujo token principal seja um dentre `name`, `nome`, `description`, `descricao`, `desc`, `status`, `label`, `title`, `titulo`.
2. **associação forte a 1 componente da PK**: compartilhamento exato ou por sinônimo do token de entidade com exatamente um componente da PK e com nenhum outro componente da PK.
3. **tabela de dimensão**: tabela referenciada por FK cujo PK é simples e cuja `EstimatedRowCount`, quando disponível, é estritamente menor que a tabela origem.
4. Se `EstimatedRowCount` indisponível, o critério de dimensão NÃO DEVE ser aplicado.

Score:
```text
0.35
+0.30 se associação forte a 1 componente da PK
+0.20 se coluna descritiva vinculada ao mesmo componente
-0.25 se coluna participa de índice único com PK completa
```

Regras:
1. Emite no threshold.
2. Não emite sem dois sinais.
3. SQL é proibido.

## 15.9 `NF3_HINT_TRANSITIVE_DEPENDENCY`
Definições fechadas:
1. padrão `X_id + X_name` usa o mesmo pipeline canônico da seção 11 para `X`.
2. `X_desc` e `X_status` usam o mesmo `X` do item 1.
3. `allowlist` desta regra é a mesma `SemiStructuredPayloadAllowlist` somente para supressão de colunas explicitamente permitidas.

Score:
```text
0.45
+0.25 se padrão X_id + X_name
+0.15 se X_desc ou X_status adicional
-0.20 se coluna descritiva em semiStructuredPayloadAllowlist
```

Regras:
1. Emite no threshold.
2. Não emite sem par chave + descritivo.
3. SQL é proibido.

---

## 16. Normalização 1FN, 2FN, 3FN (endurecimento)
1. Regras de NF são exclusivamente hints.
2. Não podem gerar severidade `Critical`.
3. Não podem gerar SQL candidate.
4. Não podem usar semântica de negócio.
5. Requerem no mínimo 2 evidências técnicas.
6. Sinal único DEVE ser insuficiente e descartado.

---

## 17. Configuração e profile

## 17.1 Contrato fechado e defaults
```json
{
  "version": 1,
  "enabled": true,
  "minConfidenceGlobal": 0.55,
  "timeoutMs": 15000,
  "allowPartialOnTimeout": true,
  "allowPartialOnRuleFailure": true,
  "enableParallelRules": true,
  "maxDegreeOfParallelism": 4,
  "maxIssues": 5000,
  "maxSuggestionsPerIssue": 3,
  "namingConvention": "SnakeCase",
  "normalizationStrictness": "Balanced",
  "requiredCommentTargets": ["Table", "PrimaryKeyColumn", "ForeignKeyColumn"],
  "lowQualityNameDenylist": ["tmp","teste","campo","valor","misc","foo","bar","x","y","z"],
  "nameAllowlist": [],
  "synonymGroups": [["person","pessoa"],["customer","cliente"],["user","usuario"]],
  "semiStructuredPayloadAllowlist": [],
  "debugDiagnostics": false,
  "ruleSettings": {
    "FK_CATALOG_INCONSISTENT": {"enabled": true, "minConfidence": 0.75, "maxIssues": 1000},
    "MISSING_FK": {"enabled": true, "minConfidence": 0.65, "maxIssues": 1000},
    "NAMING_CONVENTION_VIOLATION": {"enabled": true, "minConfidence": 0.70, "maxIssues": 1000},
    "LOW_SEMANTIC_NAME": {"enabled": true, "minConfidence": 0.60, "maxIssues": 1000},
    "MISSING_REQUIRED_COMMENT": {"enabled": true, "minConfidence": 0.70, "maxIssues": 1000},
    "NF1_HINT_MULTI_VALUED": {"enabled": true, "minConfidence": 0.60, "maxIssues": 500},
    "NF2_HINT_PARTIAL_DEPENDENCY": {"enabled": true, "minConfidence": 0.65, "maxIssues": 500},
    "NF3_HINT_TRANSITIVE_DEPENDENCY": {"enabled": true, "minConfidence": 0.65, "maxIssues": 500}
  },
  "cacheTtlSeconds": 300
}
```

## 17.2 Regras de validação
1. Ausente => default.
2. Fora da faixa => clamp + diagnóstico.
3. Enum inválido => default + diagnóstico.
4. `ruleSettings` faltante para alguma regra => inserir default dessa regra + diagnóstico.
5. `version` maior que suportada => usar última versão suportada + diagnóstico `ANL-SETTINGS-VERSION-FALLBACK`.
6. `CacheTtlSeconds=0` => cache desabilitado para escrita e leitura.
7. `EnableParallelRules=true` com `MaxDegreeOfParallelism=1` => comportamento efetivo serial.
8. `RequiredCommentTargets=[]` => a regra `MISSING_REQUIRED_COMMENT` NÃO emite.
9. `MaxSuggestionsPerIssue=1` => somente a suggestion mais prioritária por issue é mantida.

---

## 18. Comportamento cross-provider

## 18.1 Capacidades obrigatórias por provider
| Provider | FK catálogo | Comentário tabela | Comentário coluna | Quoting de identificador | SQL COMMENT |
|---|---|---|---|---|---|
| PostgreSQL | Sim | Sim | Sim | `"name"` | `COMMENT ON ...` |
| SQL Server | Sim | Sim (`MS_Description`) | Sim (`MS_Description`) | `[name]` | `sp_addextendedproperty` ou `sp_updateextendedproperty` |
| MySQL | Sim | Sim | Sim | `` `name` `` | `ALTER TABLE ... COMMENT`, `MODIFY COLUMN ... COMMENT` |
| SQLite | Sim (PRAGMA) | Não confiável | Não confiável | `"name"` | Não suportado de forma nativa robusta |

## 18.2 Degradação obrigatória
1. Provider sem comentário confiável => `MISSING_REQUIRED_COMMENT` emite no máximo `Info`, sem SQL.
2. Provider com metadata parcial => registrar `ANL-METADATA-PARTIAL`.
3. Ausência de FK catálogo no snapshot => regras dependentes de FK catálogo DEVEM considerar somente flags disponíveis.

## 18.3 Dialeto e escaping
1. SQL candidate DEVE ser provider-specific.
2. Identificadores DEVEM ser escapados por função de quoting do provider.
3. Nomes qualificados DEVEM usar `schema.table` quando schema canônico não nulo.
4. Caracteres de fechamento do quoting DEVEM ser escapados duplicando o mesmo delimitador:
   - PostgreSQL e SQLite: `"` -> `""`
   - SQL Server: `]` -> `]]`
   - MySQL: `` ` `` -> `` `` ``

---

## 19. SQL Fix Candidates

## 19.1 Permissão de geração
Permitido SOMENTE para:
1. `MISSING_FK` não ambígua.
2. `MISSING_REQUIRED_COMMENT` com capability de comentário.

## 19.2 Proibição de geração
1. regra não autorizada;
2. ambiguidade;
3. metadado insuficiente;
4. risco não determinável;
5. provider sem suporte;
6. impossibilidade de gerar `PreconditionsSql` de forma segura.

## 19.3 Nome de constraint sugerida (`MISSING_FK`)
Formato obrigatório:
`fk_<child_table>_<child_column>__<parent_table>_<parent_column>`

Regras:
1. minúsculo;
2. tokens não alfanuméricos convertidos para `_`;
3. tamanho máximo 63 chars;
4. se exceder, truncar para 55 chars + `_` + sufixo hash 7 chars;
5. se nome já existir, gerar o primeiro sufixo disponível `_v2`, `_v3`, ... até `_v99`; acima disso, candidate NÃO DEVE ser emitido.

## 19.4 Preconditions normativas
Ordem obrigatória:
1. existência de tabela child;
2. existência de tabela parent;
3. inexistência de constraint com mesmo nome;
4. inexistência de FK equivalente.

Templates:
1. PostgreSQL:
   - `SELECT 1 FROM information_schema.tables WHERE table_schema = '<schema>' AND table_name = '<table>'`
   - `SELECT 1 FROM information_schema.table_constraints WHERE constraint_schema = '<schema>' AND constraint_name = '<constraint>'`
2. SQL Server:
   - `SELECT 1 FROM sys.tables t JOIN sys.schemas s ON s.schema_id=t.schema_id WHERE s.name=N'<schema>' AND t.name=N'<table>'`
   - `SELECT 1 FROM sys.foreign_keys WHERE name=N'<constraint>'`
3. MySQL:
   - `SELECT 1 FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = '<table>'`
   - `SELECT 1 FROM information_schema.table_constraints WHERE constraint_schema = DATABASE() AND constraint_name = '<constraint>'`
4. SQLite:
   - não gerar `MISSING_FK` candidate no MVP.

Se preconditions não puderem ser geradas com segurança, o candidate NÃO DEVE ser emitido.

## 19.5 Constraint equivalente
Uma FK é equivalente quando:
1. o child table é o mesmo;
2. o parent table é o mesmo;
3. a lista ordenada de child columns é a mesma;
4. a lista ordenada de parent columns é a mesma.

O nome da constraint NÃO participa da equivalência.

## 19.6 Templates SQL
### 19.6.1 `MISSING_FK`
1. PostgreSQL:
   - `ALTER TABLE <child_qualified> ADD CONSTRAINT <constraint_name> FOREIGN KEY (<child_columns>) REFERENCES <parent_qualified> (<parent_columns>);`
2. SQL Server:
   - `ALTER TABLE <child_qualified> ADD CONSTRAINT <constraint_name> FOREIGN KEY (<child_columns>) REFERENCES <parent_qualified> (<parent_columns>);`
3. MySQL:
   - `ALTER TABLE <child_qualified> ADD CONSTRAINT <constraint_name> FOREIGN KEY (<child_columns>) REFERENCES <parent_qualified> (<parent_columns>);`
4. SQLite:
   - proibido no MVP.

### 19.6.2 `MISSING_REQUIRED_COMMENT`
1. PostgreSQL tabela:
   - `COMMENT ON TABLE <qualified_table> IS '<escaped_comment>';`
2. PostgreSQL coluna:
   - `COMMENT ON COLUMN <qualified_table>.<quoted_column> IS '<escaped_comment>';`
3. SQL Server tabela:
   - `EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'<escaped_comment>', @level0type=N'SCHEMA', @level0name=N'<schema>', @level1type=N'TABLE', @level1name=N'<table>';`
4. SQL Server coluna:
   - `EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'<escaped_comment>', @level0type=N'SCHEMA', @level0name=N'<schema>', @level1type=N'TABLE', @level1name=N'<table>', @level2type=N'COLUMN', @level2name=N'<column>';`
5. MySQL tabela:
   - `ALTER TABLE <qualified_table> COMMENT = '<escaped_comment>';`
6. MySQL coluna:
   - candidate SOMENTE PODE ser gerado se `DbMetadata` expuser a definição completa da coluna; caso contrário, NÃO DEVE ser emitido, porque `MODIFY COLUMN` exige redefinição completa da coluna.
7. SQLite:
   - proibido.

## 19.7 Transação
1. Candidate NÃO DEVE envolver `BEGIN`, `COMMIT` ou `ROLLBACK` no MVP.
2. Candidate DEVE incluir nota de risco quando `Safety != NonDestructive`.

## 19.8 Visibilidade e ação
1. `Safety=NonDestructive` => `VisibleActionable` quando capability permite.
2. `Safety=PotentiallyDestructive` => `VisibleReadOnly`.
3. `Safety=Destructive` => `Hidden` no MVP.

## 19.9 Notas de risco padronizadas
Ordem obrigatória de `Notes`:
1. mensagem de risco;
2. limitação de provider;
3. observação de preconditions;
4. observação de não autoaplicação.

Mensagens fixas:
1. `Este candidate altera o schema e requer revisão humana.`
2. `Este candidate depende de suporte específico do provider.`
3. `As preconditions devem ser avaliadas antes da execução manual.`
4. `Este candidate não é autoaplicável no MVP.`

---

## 20. UX e estados da UI

## 20.1 Layout lógico mínimo
UI DEVE conter blocos:
1. barra de comandos (`Run`, `Cancel`, filtros);
2. painel de resumo;
3. lista de issues;
4. painel de detalhes da issue selecionada;
5. lista de SQL candidates da suggestion selecionada;
6. painel de diagnósticos de execução.

## 20.2 Estados
Estados válidos:
1. `Idle`
2. `Loading`
3. `Completed`
4. `Partial`
5. `Cancelled`
6. `Failed`
7. `Empty`

Mensagens fixas:
1. `Nenhum problema estrutural inferível foi detectado.`
2. `Metadata indisponível para análise estrutural.`
3. `Análise finalizada parcialmente por timeout.`
4. `Análise cancelada pelo usuário.`
5. `Falha na análise estrutural.`
6. `Nenhuma issue corresponde aos filtros selecionados.`
7. `Nenhuma issue selecionada.`
8. `Nenhum SQL candidate disponível.`

## 20.3 Comandos
1. `RunAnalysis`: habilitado quando `profile.enabled=true` e metadata disponível.
2. `CancelAnalysis`: habilitado somente em `Loading`.
3. `CopySql`: habilitado quando candidate selecionado e visível.
4. `ApplyToCanvas`: habilitado somente para `VisibleActionable`.

Ação bloqueada DEVE exibir tooltip fixa:
`Ação indisponível para o nível de risco ou capacidade atual.`

## 20.4 Seleção e painéis
1. Ao concluir análise com `Issues.Count > 0`, a primeira issue da ordenação final DEVE ser selecionada automaticamente.
2. Se filtros removerem a issue selecionada, a primeira issue visível remanescente DEVE ser selecionada; se não houver, a seleção DEVE ser limpa.
3. Sem issue selecionada, o painel de detalhes DEVE exibir `Nenhuma issue selecionada.`.
4. Sem suggestion selecionada ou sem candidates, o painel de SQL DEVE exibir `Nenhum SQL candidate disponível.`.
5. O painel de diagnósticos DEVE sempre existir no layout; quando vazio, DEVE exibir lista vazia sem mensagem de erro.

## 20.5 Lista de issues (item mínimo)
Cada item DEVE mostrar:
1. severidade;
2. regra;
3. alvo (`schema.table.column` ou equivalente);
4. confidence;
5. título curto.

## 20.6 Detalhes da issue
DEVE mostrar:
1. mensagem completa;
2. evidências ordenadas por `Weight desc`, depois `Key asc`;
3. suggestions;
4. diagnósticos correlatos por `RuleCode`, quando houver.

## 20.7 Filtros
Obrigatórios:
1. severidade (multi-select);
2. regra (multi-select);
3. confidence mínima;
4. texto de tabela (`contains` case-insensitive invariável em `schema.table`).

Aplicação:
1. filtros cumulativos com lógica AND.

## 20.8 Distinção visual de severidade
1. `Info`: estilo informativo;
2. `Warning`: estilo de atenção;
3. `Critical`: estilo de risco alto.

A semântica visual DEVE ser consistente em lista, resumo e detalhes.

## 20.9 Ocultar regra na sessão
1. Ocultação DEVE atuar apenas na apresentação.
2. Ocultação NÃO DEVE alterar o resultado da engine.
3. Ocultação NÃO DEVE persistir em arquivo no MVP.

---

## 21. Performance, cache e execução

## 21.1 Classificação de tamanho de schema
1. Pequeno: `1..100` tabelas
2. Médio: `101..500`
3. Grande: `501..2000`
4. Muito grande: `>2000`

## 21.2 Objetivos de latência (p95)
1. Pequeno: `<= 1000 ms`
2. Médio: `<= 4000 ms`
3. Grande: `<= 15000 ms`
4. Muito grande: retorno parcial permitido por timeout

## 21.3 Cache
Chave obrigatória:
`sha256(metadataFingerprint + "|" + profileContentHash + "|" + provider + "|" + specVersion)`

Invalidação:
1. mudança em fingerprint;
2. mudança em profile hash;
3. expiração TTL;
4. mudança de `specVersion`.

## 21.4 JSON canônico para hashes
Regras:
1. propriedades JSON DEVEM ser ordenadas lexicograficamente por nome;
2. enums DEVEM ser serializados como string;
3. `null` DEVE ser serializado como `null`;
4. números decimais DEVEM usar ponto `.` e remoção de zeros à direita não significativos;
5. arrays com semântica de lista ordenada DEVEM preservar a ordem normativa;
6. arrays com semântica de conjunto DEVEM ser ordenados lexicograficamente pelos seus elementos canônicos;
7. strings DEVEM ser serializadas em Unicode NFC;
8. datas, quando presentes no metadata canônico, DEVEM usar ISO-8601 UTC.

## 21.5 Fórmulas de hash e IDs determinísticos
Todos DEVEM usar:
1. algoritmo `SHA-256`;
2. input UTF-8;
3. saída hexadecimal minúscula.

### 21.5.1 `MetadataFingerprint`
Payload canônico:
1. provider;
2. databaseName;
3. schemas ordenados;
4. tabelas ordenadas por `schema, table`;
5. colunas ordenadas por `ordinal, name`;
6. PKs, FKs, UQs e índices ordenados por nome.

### 21.5.2 `ProfileContentHash`
Payload canônico:
1. `SchemaAnalysisProfile` já validado, clamped e com defaults preenchidos;
2. `ruleSettings` ordenado por `SchemaRuleCode`;
3. listas de allowlist, denylist e synonym groups ordenadas deterministicamente.

### 21.5.3 `IssueId`
Payload:
1. `RuleCode`
2. `TargetType`
3. `SchemaName`
4. `TableName`
5. `ColumnName`
6. `ConstraintName`
7. `Title`
8. `Message`
9. `Confidence`
10. `IsAmbiguous`

Todos os campos textuais DEVEM usar “Texto normalizado para hash”.

### 21.5.4 `SuggestionId`
Payload:
1. `IssueId`
2. `Title`
3. `Description`
4. `Confidence`

### 21.5.5 `CandidateId`
Payload:
1. `SuggestionId`
2. `Provider`
3. `Title`
4. `Sql`
5. `Safety`

## 21.6 Determinismo com paralelismo
1. Paralelismo permitido NÃO PODE alterar ordenação final.
2. Coleções intermediárias DEVEM ser consolidadas por ordenação normativa antes de emissão final.
3. `EnableParallelRules=true` com `MaxDegreeOfParallelism=1` DEVE se comportar como execução serial.

## 21.7 Limites
1. `MaxIssues` global obrigatório.
2. `RuleSetting.MaxIssues` por regra obrigatório.
3. Excedente DEVE truncar e registrar diagnóstico:
   - `ANL-RULE-MAX-ISSUES-TRUNCATED`
   - `ANL-GLOBAL-MAX-ISSUES-TRUNCATED`

## 21.8 Progress reporting
1. Progresso por regra concluída.
2. Valor em `[0,100]`.
3. Fórmula: `floor((completedRules / totalRules) * 100)`.

---

## 22. Testes normativos

## 22.1 Unitários por regra
Para cada regra:
1. cenário positivo;
2. cenário negativo;
3. cenário ambíguo;
4. cenário metadado insuficiente;
5. validação de evidências mínimas.

## 22.2 Score e threshold
1. fronteiras: `0.5499`, `0.5500`, `0.6499`, `0.6500`, `0.8499`, `0.8500`;
2. arredondamento ToEven com 4 casas;
3. caso score acima global e abaixo da regra => NÃO emitir;
4. caso score abaixo global e acima da regra => NÃO emitir;
5. caso score exatamente igual ao threshold => emitir.

## 22.3 Nomeação
Casos obrigatórios:
1. `IdPessoa`, `PessoaId`, `pessoa_id`, `id_pessoa`
2. acento + sigla + plural (`ÓrgãosIDs`)
3. conflito de sinônimo em múltiplos grupos
4. allowlist sobrepondo denylist
5. nome iniciado com número
6. nome somente com tokens estruturais

## 22.4 Cross-provider
1. quoting por provider;
2. SQL candidate de comentário para PostgreSQL, SQL Server e MySQL;
3. ausência de SQL de comentário para SQLite;
4. fallback de metadata parcial;
5. `MODIFY COLUMN ... COMMENT` em MySQL somente com definição completa de coluna.

## 22.5 Pipeline e edge cases
1. schema sem tabelas => `Completed` + `Empty`
2. tabela sem PK
3. tabela sem colunas
4. tabela sem schema explícito
5. PK composta com nomes genéricos
6. timeout após algumas regras
7. cancelamento durante consolidação
8. regra desabilitada com e sem diagnóstico debug
9. cache hit com profile alterado => miss
10. truncamento por `RuleSetting.MaxIssues`
11. truncamento por `MaxIssues`
12. ordenação determinística entre runs iguais
13. `CacheTtlSeconds=0`
14. `RequiredCommentTargets=[]`
15. `SynonymGroups=[]`

## 22.6 Contratos
1. serialização e deserialização de todos os contratos;
2. validação de nulabilidade e invariantes;
3. validação de IDs determinísticos (`IssueId`, `SuggestionId`, `CandidateId`);
4. validação de `MetadataFingerprint` e `ProfileContentHash`.

## 22.7 UI e MVVM
1. transições de estado;
2. habilitação de comandos;
3. filtros cumulativos;
4. empty state, partial, failed;
5. visibilidade de ações por safety e capability;
6. seleção inicial automática;
7. limpeza de seleção após filtro sem resultados;
8. painéis vazios de detalhes, SQL e diagnósticos.

---

## 23. Critérios de aceitação
A implementação somente é aceita se:
1. todos os contratos e enums desta especificação forem implementados sem campos ad hoc;
2. todas as regras da taxonomia forem implementadas;
3. a fórmula de `MISSING_FK` for implementada exatamente;
4. a matriz de severidade for obedecida;
5. o fallback cross-provider estiver conforme seção 18;
6. SQL candidates seguirem a seção 19;
7. a UI seguir a seção 20;
8. cache, hashes e determinismo seguirem a seção 21;
9. a suíte de testes da seção 22 passar integralmente.

---

## 24. Anexos com exemplos canônicos

## 24.1 Exemplo A — `MISSING_FK` com candidate
Input:
- `public.orders.customer_id` int, sem FK
- `public.customers.id` int PK

Output:
1. issue `MISSING_FK`
2. `IsAmbiguous=false`
3. confidence `>=0.8500`
4. severidade `Critical`
5. evidências: naming + tipo + topologia
6. candidate SQL `ALTER TABLE ... ADD CONSTRAINT ... FOREIGN KEY ...`

## 24.2 Exemplo B — ambiguidade sem candidate
Input:
- `person_id` compatível com `people.id` e `persons.id` com diferença `<0.0500`

Output:
1. issue `MISSING_FK`
2. `IsAmbiguous=true`
3. severidade `Warning`
4. `SqlCandidates=[]`

## 24.3 Exemplo C — comentário em SQLite
Input:
- SQLite, coluna sem comentário obrigatório

Output:
1. issue `MISSING_REQUIRED_COMMENT`
2. severidade `Info`
3. evidência `ProviderLimitation`
4. sem SQL candidate

## 24.4 Exemplo D — violação de convenção
Input:
- convenção `SnakeCase`
- coluna `CustomerID`

Output:
1. issue `NAMING_CONVENTION_VIOLATION`
2. confidence `0.9000`
3. severidade `Warning`

## 24.5 Exemplo E — schema vazio
Input:
- `DbMetadata` com `Schemas=[]`

Output:
1. `Status=Completed`
2. `Issues=[]`
3. `Summary.TotalIssues=0`
4. estado UI `Empty`

## 24.6 Exemplo F — conflito allowlist e denylist
Input:
- `nameAllowlist=["valor"]`
- `lowQualityNameDenylist` contém `valor`
- coluna `valor`

Output:
1. `LOW_SEMANTIC_NAME` NÃO emitida
2. diagnóstico `ANL-SETTINGS-ALLOWLIST-OVERRIDES-DENYLIST`

## 24.7 Exemplo G — excedeu limite por regra
Input:
- regra `LOW_SEMANTIC_NAME` produz 1400 issues
- `RuleSetting.MaxIssues=1000`

Output:
1. somente 1000 issues dessa regra preservadas após ordenação da regra
2. diagnóstico `ANL-RULE-MAX-ISSUES-TRUNCATED`

## 24.8 Exemplo H — timeout parcial
Input:
- timeout em 15s
- 5 de 8 regras concluídas

Output:
1. `Status=Partial`
2. `PartialState.ReasonCode="TIMEOUT"`
3. `CompletedRules=5`
4. diagnóstico `ANL-TIMEOUT`
