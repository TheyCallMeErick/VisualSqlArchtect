# Start Menu Inicial — Ideias e Tasks (Mescla dos 4 Concepts)

## Objetivo
Definir o novo ponto de entrada da aplicacao: ao abrir o Visual SQL Architect, o usuario entra no **Start Menu** (home). O **canvas deixa de ser a tela inicial** e passa a ser aberto apenas por acao explicita (ex.: "Novo Diagrama", abrir projeto recente, carregar template).

---

## Sintese dos 4 Concepts

### 1) Concept de tipografia (imagem 1)
- Header com identidade forte, compacta e tecnica.
- Hierarquia clara entre titulo da secao, subtitulo e metadados.
- Uso de peso tipografico para reforcar navegacao (nao depender so de cor).

### 2) Concept de menu inicial (imagem 2)
- Estrutura ideal para onboarding e retomada rapida.
- Blocos principais:
  - Continuar de onde parou
  - Conexoes salvas
  - Explorar templates
  - CTA principal para iniciar novo diagrama/consulta
- Barra superior com navegacao global (Inicio, Conexoes, Recentes, Templates, Ajuda, Configuracoes).

### 3) Concept de hierarquia de tela (imagem 3)
- Grade com prioridades bem definidas:
  - Area principal com tarefas de alto impacto
  - Area secundaria com contexto e status
- Separacao visual limpa entre navegacao, conteudo e utilitarios.

### 4) Concept de layout/estrutura (imagem 4)
- Boa proporcao entre cards e secoes.
- Arredondamentos consistentes e modernos.
- Layout com blocos modulares reutilizaveis.
- Densidade visual equilibrada (rico sem ficar poluido).

---

## Direcao Visual Recomendada

## Tom geral
- Dark premium com acentos turquesa/verde para estados ativos.
- Contraste alto para legibilidade, com fundo em camadas (nao chapado).

## Tokens de UI (proposta inicial)
- Radius:
  - `--radius-sm: 10`
  - `--radius-md: 14`
  - `--radius-lg: 20`
- Spacing base:
  - `--space-1: 4`
  - `--space-2: 8`
  - `--space-3: 12`
  - `--space-4: 16`
  - `--space-5: 24`
- Elevation:
  - Card base: sombra suave + borda de 1px com opacidade baixa
  - Card hover: leve aumento de brilho e contraste de borda

## Tipografia
- Titulo de pagina: forte e sem serifa, tracking levemente fechado.
- Titulos de card: medium/semi-bold.
- Metadados (provider, ultima edicao, status): menor tamanho, contraste medio.
- Evitar depender so de cor para estado; sempre combinar com icone/label.

---

## Arquitetura de Navegacao (Regra Principal)

## Regra de produto
- Startup sempre abre no Start Menu.
- Canvas abre apenas quando o usuario:
  - clicar em Novo Diagrama / Consulta SQL,
  - clicar em Projeto Recente,
  - clicar em Template e confirmar criacao,
  - abrir arquivo manualmente.

## Estrategia tecnica (alto nivel)
- Introduzir estado global de shell (`Start` vs `Canvas`).
- Carregar `CanvasViewModel` sob demanda (lazy) para reduzir custo no startup.
- Preservar sistema atual de tabs do canvas, mas somente dentro do modo `Canvas`.

---

## Estrutura de Tela (Start Menu)

## Barra superior
- Esquerda: logo + nome da aplicacao.
- Centro: navegacao primaria (`Inicio`, `Conexoes`, `Projetos Recentes`, `Explorar Templates`).
- Direita: `Ajuda`, `Configuracoes`, versao atual.

## Conteudo principal
1. **Continuar de onde parou**
- Lista horizontal de projetos recentes (cards).
- Cada card com:
  - nome do arquivo
  - ultima edicao relativa
  - provider
  - acao secundaria (menu de contexto)

2. **Conexoes salvas**
- Card lateral com status por provider (Conectado/Desconectado).
- Acao primaria: `+ Nova Conexao`.

3. **Explorar templates**
- Grid de templates com icone + nome.
- Clique cria novo canvas pre-configurado.

4. **CTA flutuante/fixo inferior direito**
- Botao principal: `Novo Diagrama / Consulta SQL`.
- Sempre visivel para reduzir tempo ate primeira acao.

---

## Backlog de Implementacao (Tasks)

## EPIC A — Shell e Startup
- [x] Criar `ShellViewModel` com estado de pagina inicial (`Start`, `Canvas`).
- [x] Mover responsabilidade de tela inicial para shell (nao para `CanvasViewModel`).
- [x] Alterar bootstrap para abrir em `Start` por padrao.
- [x] Garantir que `CanvasViewModel` e servicos pesados sejam inicializados apenas quando necessario.

Arquivos alvo sugeridos:
- `src/VisualSqlArchitect.UI/App.axaml.cs`
- `src/VisualSqlArchitect.UI/Views/Shell/MainWindow.axaml`
- `src/VisualSqlArchitect.UI/Views/Shell/MainWindow.axaml.cs`
- `src/VisualSqlArchitect.UI/ViewModels/Shell/ShellViewModel.cs` (novo)

## EPIC B — Start Menu ViewModel + Dados
- [x] Criar `StartMenuViewModel`.
- [x] Expor colecoes:
  - `RecentProjects`
  - `SavedConnections`
  - `TemplateCatalog`
- [x] Incluir comandos:
  - `CreateNewDiagramCommand`
  - `OpenRecentProjectCommand`
  - `OpenTemplateCommand`
  - `OpenConnectionsCommand`
- [x] Integrar com sessao/historico ja existente (sem duplicar fonte de verdade).

Arquivos alvo sugeridos:
- `src/VisualSqlArchitect.UI/ViewModels/Start/StartMenuViewModel.cs` (novo)
- `src/VisualSqlArchitect.UI/Services/SessionManagementService.cs`
- `src/VisualSqlArchitect.UI/ViewModels/ConnectionManagerViewModel.cs`

## EPIC C — Layout do Start Menu
- [x] Criar view dedicada para Start Menu com estrutura por secoes.
- [x] Implementar componentes de card reutilizaveis (Recente, Conexao, Template).
- [x] Aplicar sistema consistente de arredondamento e espacamento.
- [x] Implementar estados de hover/focus/pressed em todos os cards clicaveis.

Arquivos alvo sugeridos:
- `src/VisualSqlArchitect.UI/Views/Start/StartMenuView.axaml` (novo)
- `src/VisualSqlArchitect.UI/Views/Start/StartMenuView.axaml.cs` (novo)
- `src/VisualSqlArchitect.UI/Controls/Start/RecentProjectCard.axaml` (novo)
- `src/VisualSqlArchitect.UI/Controls/Start/ConnectionStatusCard.axaml` (novo)
- `src/VisualSqlArchitect.UI/Controls/Start/TemplateCard.axaml` (novo)
- `src/VisualSqlArchitect.UI/Assets/Themes/AppStyles.axaml`

## EPIC D — Tipografia e Design Tokens
- [x] Definir tokens de tipografia para:
  - pagina, secao, card, metadado, botao
- [x] Revisar escala de fonte para telas 1366+, 1920+ e notebook menor.
- [x] Garantir consistencia de pesos em toda a home.
- [x] Revisar contraste de texto para acessibilidade minima.

Arquivos alvo sugeridos:
- `src/VisualSqlArchitect.UI/Assets/Themes/DesignTokens.axaml`
- `src/VisualSqlArchitect.UI/Assets/Themes/AppStyles.axaml`

## EPIC E — Fluxos de Navegacao Start -> Canvas
- [x] Implementar transicoes:
  - Start -> Canvas (Novo)
  - Start -> Canvas (Recente)
  - Start -> Canvas (Template)
- [x] Implementar retorno opcional para Start sem perder estado do canvas (decisao de produto).
- [x] Garantir que abrir arquivo externo tambem force caminho Start -> Canvas corretamente.

Arquivos alvo sugeridos:
- `src/VisualSqlArchitect.UI/Views/Shell/MainWindow.axaml.cs`
- `src/VisualSqlArchitect.UI/Serialization/CanvasSerializer.cs`
- `src/VisualSqlArchitect.UI/Services/FileOperationsService.cs`

## EPIC F — Qualidade e Testes
- [x] Unit tests para `ShellViewModel` (transicoes de estado).
- [x] Unit tests para comandos do `StartMenuViewModel`.
- [x] Testes de regressao para garantir que startup nao abre canvas automaticamente.
- [x] Validar performance do startup (tempo de primeira renderizacao).

Arquivos alvo sugeridos:
- `tests/VisualSqlArchitect.Tests/Unit/ViewModels/ShellViewModelTests.cs` (novo)
- `tests/VisualSqlArchitect.Tests/Unit/ViewModels/StartMenuViewModelTests.cs` (novo)
- `tests/VisualSqlArchitect.Tests/Integration/StartupFlowTests.cs` (novo)

---

## Criterios de Aceite
- Ao iniciar o app, a primeira tela visivel e sempre o Start Menu.
- Nao existem elementos de canvas renderizados antes de o usuario iniciar uma acao de abertura.
- `Novo Diagrama`, `Recente` e `Template` abrem o canvas corretamente.
- Home responsiva entre resolucoes comuns de desktop (1366x768, 1600x900, 1920x1080).
- Hierarquia visual clara: usuario identifica em menos de 3 segundos a acao principal para comecar.

---

## Itens de Refinamento (Opcional Pos-MVP)
- [x] Animacao curta de entrada dos cards (stagger sutil).
- [x] Filtro e busca em projetos recentes.
- [x] Favoritar templates.
- [x] Painel de "Dicas rapidas" para onboarding.
- [x] Placeholder de ultimo snapshot visual do canvas nos cards recentes.

---

## Ordem Recomendada de Execucao
1. EPIC A (Shell/Startup)
2. EPIC B (ViewModel de Start)
3. EPIC C (Layout)
4. EPIC E (Navegacao Start -> Canvas)
5. EPIC D (Polimento tipografico/tokens)
6. EPIC F (Testes)

Essa ordem reduz risco de retrabalho, porque fixa primeiro a arquitetura de navegacao e so depois detalha acabamento visual.
