# Plano — MarkReader: busca utilizável, abas e legibilidade

## Contexto (o que o scout provou)

- App **Avalonia 11.2.7 / .NET 8**, MVVM com CommunityToolkit. Dois projetos:
  `MarkReader.Core` (serviços + `MainViewModel`) e `MarkReader.Desktop` (`MainWindow`).
  Build baseline: **verde, 0 avisos** (`dotnet build MarkReader.slnx`).
- **Renderização é `Markdown.Avalonia` 11.0.3**, que desenha texto com `CTextBlock` /
  `CRun` / `CSpan` do pacote **ColorTextBlock.Avalonia** — *não* com `TextBlock`/`Run`
  do Avalonia. Confirmado nos metadados de
  `D:\NuGetPackages\colortextblock.avalonia\11.0.3\lib\netstandard2.0\ColorTextBlock.Avalonia.dll`
  (tipos públicos `CTextBlock`, `CRun`, `CSpan`, membros `Content`, `Background`,
  `Foreground`, `TemporaryBackground`, `TemporaryForeground`).
- **Causa raiz do bug do highlight** (`MainWindow.axaml.cs:252-278`): `ApplyHighlights`
  varre `GetVisualDescendants().OfType<TextBlock>()` e monta `Run` do
  `Avalonia.Controls.Documents`. Como a árvore só contém `CTextBlock`, **a varredura não
  acha nada e nenhum destaque é pintado** — enquanto o contador vem do
  `SearchService`, que conta sobre o texto cru. Daí "encontrou N" sem marcação visível.
- Bug secundário do mesmo fluxo (`MainWindow.axaml.cs:156-176`): `OnScrollToSearchResult`
  faz `ScrollToEnd()` e depois estima a posição por `(linha / total de linhas) *
  Extent.Height`. A estimativa ignora altura real de blocos (código, tabela, imagem) —
  o scroll cai longe do resultado.
- `SearchService` (`SearchService.cs`) busca no **markdown cru**: conta ocorrências em
  URLs de link, cercas de código e marcadores (`**`, `#`). O número exibido nunca vai
  bater com o que o leitor vê renderizado.
- **Um documento só**: `MainViewModel` guarda `MarkdownContent`, `CurrentFilePath`,
  `FileName`, `FileSizeText` soltos; a XAML tem um único `MarkdownScrollViewer`
  (`MainWindow.axaml:217-227`). Não há estrutura de abas.
- **Duplicação de abertura**: `OpenFileAsync` (`MainViewModel.cs:66-98`) e o `OnDrop`
  (`MainWindow.axaml.cs:185-209`) implementam a mesma carga em dois lugares; o `OnDrop`
  ainda esquece de limpar a busca e faz `break` no primeiro arquivo.
- **Scroll não volta ao topo** ao trocar de arquivo: ninguém zera `ContentScrollViewer.Offset`
  quando `MarkdownContent` muda.
- **Tema/tipografia**: `App.axaml` só sobrescreve 3 brushes do FluentTheme no variant Dark
  (`#202020`, `#2B2B2B`, `#333333`); todo o resto é default do Fluent. A fonte **Inter é
  registrada** (`Program.cs:22`, `.WithInterFont()`) mas **nunca aplicada** — o app roda na
  fonte default do sistema. O corpo não tem `FontSize` nem entrelinha definidos e o
  `MaxWidth="860"` dá ~95-110 caracteres por linha (convenção de leitura longa: 60-75).
  Highlight fixo em `Brushes.Yellow`/`Brushes.Black`, sem token de tema.
- **O repositório não tem nada versionado**: `git ls-files` volta vazio, o único commit é
  vazio e não existe `.gitignore` — `publish/`, `.vs/`, `bin/`, `obj/` entrariam no commit.
- `MarkdownService.ConvertToHtml` + o pacote **Markdig** não são usados por ninguém
  (a renderização é do Markdown.Avalonia).

## Decisões tomadas no planejamento

- **Fonte**: Inter em UI e corpo (já embarcada), 16px / entrelinha 1.6, medida ~70
  caracteres. Decisão do usuário: seletor de fonte/tamanho **fica como feat futura**,
  registrada em Pendências.
- **Temas**: mantém apenas claro + escuro, afinados por tokens semânticos com contraste
  medido. Sem sépia (decisão do usuário).
- **A busca passa a operar sobre o texto renderizado**, não sobre o markdown cru — é a
  única forma de o contador ("3 de 12") bater com o que está destacado na tela. O
  `SearchService` atual deixa de alimentar a UI (fica coberto por testes ou é removido —
  ver Fase 2).
- **Fronteira MVVM**: a VM não conhece a árvore visual. Entra uma interface no Core
  (`IDocumentSearchView`: `int Highlight(string termo)`, `void GoTo(int indice)`,
  `void Clear()`) que a `MainWindow` implementa; a VM mantém contagem e índice atual.
- **Abas com viewer vivo por documento** (`TabStrip` de cabeçalhos + painel com um
  `MarkdownScrollViewer` por aba, alternado por `IsVisible`) em vez de `TabControl`
  puro. Motivo: `TabControl` recria o conteúdo a cada troca, o que re-parseia o markdown
  (custo real em arquivo grande) e perde a posição de leitura de cada aba.
- **Abrir arquivo já aberto ativa a aba existente**, não duplica.
- **Arrastar N arquivos abre N abas**; toda abertura passa por um único caminho na VM.
- **Busca é por documento ativo** (Ctrl+F busca na aba da frente), estado zerado ao
  trocar de aba.
- Alvo de contraste: **AA (4.5:1) como piso e AAA (7:1) para o corpo do texto**, medido e
  registrado na **Fase 6** (WCAG 2.2, critérios 1.4.3/1.4.6; entrelinha 1.5+ vem do 1.4.12).
- **Um visualizador vivo por aba é invariante que sustenta duas fases** (4 e 5): virtualizar
  a lista de abas descartaria os viewers de fundo e a posição de leitura sumiria junto.
  Fixado por teste (`Toda_aba_aberta_tem_seu_visualizador_realizado`).
- **O projeto é pessoal e vai para `github.com/PhilipiMagalhaes`, público.** A máquina tem
  identidade global corporativa (`philipi.magalhaes@vibetecnologia.com`), duas contas
  GitHub salvas (`PhilipiMagalhaes` = pessoal, `Philipi-Magalhaes` = a outra) e credenciais
  do Azure DevOps corporativo. Proteções decididas, todas locais ao repositório:
  identidade pessoal em `--local`, `core.hooksPath=.githooks` e um hook `pre-push`
  versionado que barra destino fora de `github.com/PhilipiMagalhaes/` e commits assinados
  com domínio corporativo. Repositório criado pelo usuário no navegador; nada é criado na
  conta dele por automação.

## Perguntas ao negócio

- [ ] Reabrir na próxima execução as abas que estavam abertas (persistir em
      `settings.json`)? Hoje o app abre vazio. (não bloqueia nenhuma fase; se sim, vira
      fase extra)
- [ ] O `Markdig` / `MarkdownService.ConvertToHtml` servem a algum plano futuro (exportar
      HTML/PDF)? Se não, saem na Fase 1. (bloqueia só a limpeza, não as entregas)

## Fases (1 fase = 1 /fatia, cada uma com commit verde)

- [x] **Fase 4.1 — Abrir arquivos por linha de comando** (fatia extra, 2026-08-12).
      Não estava no plano: entrou porque o gate visual da Fase 5 ficou sem como abrir
      arquivo. `MarkReader.Desktop.exe a.md b.md` abre uma aba por arquivo — e de quebra o
      duplo clique num `.md` no Explorer passa a funcionar. Achado do revisor, verificado
      empiricamente por ele em .NET 8: a checagem de duplicata rodava **fora** do
      `try/catch` e `Path.GetFullPath("")` lança, então um argumento vazio derrubava o app
      na inicialização a partir do lambda do dispatcher. `LoadFileAsync` virou total e os
      argumentos passam por um seletor (`FilePathArguments`) que descarta vazios e opções.

- [x] **Fase 1 — Base versionada e publicada na conta pessoal.** (feita em 2026-08-12 —
      repo `github.com/PhilipiMagalhaes/Galileo-Reader`, público, branch `main`.
      Achado da fase: o commit inicial estava assinado com o e-mail corporativo e foi
      pego pelo próprio hook; história refeita antes de publicar. O repo já existia com
      README, então o baseline entrou por rebase sobre `origin/main`.) `.gitignore` (.NET +
      `.vs/`, `publish/`, `error_log.txt`), identidade pessoal local, hook `pre-push` de
      guarda, commit do baseline e push para o repositório pessoal. Sem isso não existe
      commit verde nem rollback para as fases seguintes.
      *Pronto quando:* `git status` limpo salvo o esperado, `dotnet build` verde,
      `bin/obj/publish/.vs` fora do índice, `git log` mostrando autoria pessoal, hook
      barrando destino corporativo em teste, e o baseline visível no GitHub pessoal.

- [x] **Fase 2 — Highlight de busca que funciona.** (feita em 2026-08-12. Medições do
      arranjo headless: o documento de amostra rende **12 CTextBlock contra 1 TextBlock** —
      prova numérica de por que o código antigo não pintava nada. Cobertura: **8 de 9**
      ocorrências; a que escapa é a do **bloco de código cercado**, que o Markdown.Avalonia
      não renderiza como CTextBlock. Custo medido de `Highlight` em documento de 202 KB /
      2400 blocos: **~320-480 ms**, o que obrigou a entrar um debounce de 180 ms nesta
      mesma fatia. Gate visual nos 2 temas com pixels conferidos: destaque `#B8860B` no
      escuro, `#FFD54F` no claro, zero resíduo após Esc.) Reescrever `ApplyHighlights` /
      `ClearHighlights` sobre `CTextBlock.Content` (`CRun`/`CSpan` de
      ColorTextBlock.Avalonia): coletar os `CTextBlock` da árvore, achar as ocorrências no
      texto renderizado, quebrar os `CRun` nas fronteiras do termo e pintar
      `Background`/`Foreground` com tokens de tema (o token definitivo chega na Fase 5;
      aqui entra um provisório via `DynamicResource`). Introduzir `IDocumentSearchView`
      no Core e ligar a contagem da VM ao número real de destaques. Restaurar o conteúdo
      original ao limpar (snapshot por `CTextBlock`), sem acumular quebras a cada tecla.
      *Pronto quando:* digitar um termo pinta todas as ocorrências visíveis, o contador
      bate com o número de marcas, e apagar o termo devolve o texto ao original (sem
      resíduo de cor nem duplicação).

- [x] **Fase 3 — Navegação entre resultados.** (feita em 2026-08-12. `SearchHighlighter`
      passou a guardar a ocorrência — `CTextBlock` + `CRun` — em vez de só o bloco, e a
      paleta inteira entra numa `SearchPalette` única, para destacar e navegar nunca
      misturarem cores de temas diferentes. Achado do revisor corrigido: Enter dentro dos
      180 ms do debounce aplicava o passo **por cima** da busca que acabara de posicionar
      na 1ª ocorrência, pulando-a — exercitado no gate visual, hoje marca "1 de 10". A cor
      da corrente no claro virou `#C2410C` com texto branco: o `#FF8F00` anterior dava só
      **1,62:1** contra o âmbar e mantinha o mesmo texto, pista que some em
      protanopia/deuteranopia; agora são 3,70:1 de fundo e 5,22:1 de texto, com inversão
      de luminosidade como pista principal. Gate visual: a marca corrente percorre
      x=[128..254] → [186..226] → [424..462], incluindo duas ocorrências do mesmo
      parágrafo, sempre uma só por vez, e sobrevive à troca de tema.) Marca distinta para o resultado corrente
      (cor de destaque + foco), Enter/Shift+Enter e ◀/▶ percorrendo, e scroll levando o
      resultado à viewport pelo `CTextBlock` real (`TranslatePoint` para o conteúdo do
      `ScrollViewer` / `BringIntoView`), aposentando a estimativa por número de linha.
      *Pronto quando:* em arquivo com tabela e bloco de código, Enter percorre 1→N→1 e o
      resultado corrente aparece sempre dentro da área visível, destacado diferente dos
      demais.

- [x] **Fase 4 — Abas multi-arquivo.** (feita em 2026-08-12. Dois defeitos pegos só no
      gate visual, invisíveis para os testes: **Ctrl+Tab não funcionava** — o Avalonia
      consome o Tab para navegação de foco antes de o evento borbulhar até a janela, então
      passou a ser interceptado no **tunelamento**; e a barra de status **continuava
      falando da aba já fechada**, corrigido fazendo o status seguir sempre a aba da
      frente. Achados do revisor aplicados: `CloseDocument` agora escolhe a vizinha
      **antes** de remover — remover o item selecionado fazia o `ListBox` reeleger a
      seleção sozinho e o estado passava por um valor intermediário; `ActiveDocumentViewer`
      usa os containers realizados em vez de varrer a árvore inteira, senão cada busca
      descia pelas abas escondidas; evento `ActiveDocumentChanged` removido por ser um
      segundo canal que só repetia o que o `ResetSearch` já fazia. Gate visual cobriu
      também fechar aba de fundo pelo ✕ com busca ativa na aba da frente: seleção e busca
      sobrevivem.) `DocumentViewModel` (caminho, nome, conteúdo, tamanho) + `ObservableCollection<DocumentViewModel>` e
      `SelectedDocument` na `MainViewModel`; caminho único de abertura (diálogo, Ctrl+O e
      drag&drop convergindo nele, com N arquivos = N abas); cabeçalho com nome do arquivo
      + botão ✕ e tooltip do caminho completo; arquivo já aberto ativa a aba existente;
      fechar a última aba volta ao empty state; Ctrl+W fecha a aba, Ctrl+Tab alterna.
      Barra de status e busca passam a refletir o documento ativo.
      *Pronto quando:* abrir 3 arquivos dá 3 abas nomeadas, ✕ e Ctrl+W fecham a certa,
      reabrir um já aberto só troca de aba, e a busca opera sobre a aba da frente.

- [x] **Fase 5 — Volta ao topo / memória de posição.** (feita em 2026-08-12, **sem código
      de produção**. O teste-primeiro provou que o comportamento já cai do desenho da
      Fase 4 — um `ScrollViewer` vivo por aba, escondido em vez de destruído. O critério
      original desta fase ("offset zerado depois do layout, para o re-measure não restaurar
      a posição") descrevia o mundo de **um viewer compartilhado**, que a decisão da Fase 4
      aposentou; implementá-lo ao pé da letra teria **quebrado** a metade "restaura" do
      próprio critério. A entrega é a guarda de regressão: `ReadingPositionTests`, 6 testes
      sobre a `MainWindow` real em headless. O revisor mediu os dentes dela por mutação —
      trocar o `ItemsControl` por `ContentControl` derruba 2 testes; zerar o offset a cada
      troca de aba derruba 3 — e achou um ponto cego: com a regressão escrita via
      `Dispatcher.Post` a suíte ficava 100% verde. Corrigido drenando a fila do dispatcher
      no arranjo; reinjetei a mutação e ela agora derruba 3 testes.) Aba **nova** abre no topo
      (offset zerado depois do layout, para o re-measure não restaurar a posição
      anterior); voltar para uma aba já aberta **restaura** onde a leitura parou.
      *Pronto quando:* abrir um arquivo novo estando no meio de outro mostra o topo do
      novo documento; alternar entre duas abas preserva a posição de cada uma.

- [x] **Fase 6 — Paleta e tipografia de leitura.** (feita em 2026-08-12. Tokens semânticos
      por variant (`Reader*Brush`), Inter aplicada de fato (era registrada em `Program.cs` e
      nunca usada), corpo 16px / entrelinha 26 (1,6), escala de títulos, e `MaxWidth`
      860 → 680 pela medida de ~70 caracteres. **20 pares de contraste medidos por script
      WCAG, todos dentro do alvo** — corpo em AAA (13,2:1 escuro / 15,1:1 claro), demais
      textos em AA, separadores e distinção entre destaques ≥ 3:1.
      A medição pegou **dois defeitos em cores já commitadas nas fases 2 e 3**: no escuro o
      destaque de busca dava 3,25:1 (reprovava AA) e a marca corrente separava-se da normal
      por 1,51:1 — hoje 6,51:1 e 3,53:1.
      O revisor **desmontou o pacote** e derrubou minha hipótese sobre por que os estilos de
      `CCode`/`CHyperlink` não pegavam: não é valor local nem ordem de aplicação — o estilo
      embutido entra em `Styles[0]` e usa seletor **descendente-com-classe**
      (`.Markdown_Avalonia_MarkdownViewer CCode`). Trocada a forma do seletor, tudo passou a
      seguir o tema; confirmado por pixel nos dois temas, com o azul velho `#7272FF`
      (2,74:1) em **zero pixels**.
      O arranjo de captura passou a usar `PrintWindow`: o Windows recusa
      `SetForegroundWindow` depois de muita automação, e `CopyFromScreen` capturaria a
      janela de outro programa.) `ResourceDictionary` por variant com
      tokens semânticos (fundo, texto, texto secundário, borda, link, fundo de código,
      citação, destaque de busca, destaque corrente) substituindo os brushes soltos do
      `App.axaml`; `MarkdownStyle` cobrindo títulos, parágrafo, código, citação, tabela e
      link; Inter aplicada de fato, corpo 16px / entrelinha 1.6, escala de títulos, e
      `MaxWidth` reduzido para a medida de ~70 caracteres. Registrar no plano a **razão
      de contraste medida** de cada par texto/fundo (piso AA 4.5:1, corpo AAA 7:1) e
      trocar o amarelo puro do highlight pelos tokens dos dois temas.
      *Pronto quando:* claro e escuro têm todos os pares de texto medidos e dentro do
      alvo, código/tabela/citação legíveis nos dois, e a marcação de busca visível sem
      ofuscar em nenhum dos temas.

## Pendências / fios em aberto

- **Busca não alcança blocos de código cercados** (```): o Markdown.Avalonia os renderiza
  fora de `CTextBlock`. Termo que só existe num bloco de código dá "Sem resultados".
  Medido na Fase 2 (8 de 9 ocorrências do documento de amostra).
- **Termo que cruza fronteira de formatação não casa** (buscar `bold` em `**bo**ld`):
  são runs distintos. Coerente com o contador, mas é uma limitação real.
- **O scroll da navegação vai ao bloco que contém a ocorrência, não à ocorrência.**
  `TextPointer` não é público na raiz do ColorTextBlock.Avalonia, então a coordenada
  exata dentro do bloco não está acessível. Num parágrafo mais alto que a viewport, a
  ocorrência corrente pode ficar fora da área visível mesmo com o bloco à vista — o
  critério da Fase 3 ("sempre dentro da área visível") vale para blocos de altura normal.
- **Ritmo vertical dos títulos não entrou**: o estilo embutido dá a mesma margem a título
  e parágrafo, então um H1 de 28px respira como um parágrafo. Espaçamento pesa tanto quanto
  o corpo numa tela de leitura.
- **Pares do chrome fora do conjunto medido**: contorno do campo de busca e estados de
  hover ainda vêm de pinceis do Fluent. A tira de abas ganhou traço de acento na aba ativa
  (o degrau de fundo sozinho era 1,1:1), mas o resto do chrome não foi medido.
- **Abas não têm limite e cada uma segura um documento parseado e vivo** — é o preço
  explícito de não re-parsear ao trocar de aba (Fase 4). Com muitos arquivos grandes
  abertos isso pesa; não há teto nem descarte.
- `FileService`/`IFileService` não têm nenhum consumidor no repo, assim como o
  `Markdig`/`ConvertToHtml`. Limpeza pendente de resposta do negócio.
- **Feat futura (pedido do usuário):** seletor de fonte e tamanho de texto nas
  preferências, persistido em `settings.json` — fora do escopo deste plano.
- Persistir e restaurar abas abertas entre execuções (ver Perguntas ao negócio).
- Remover `Markdig` + `MarkdownService.ConvertToHtml` se não houver plano de exportação.
- `src/MarkReader.Tests` (xunit + Avalonia.Headless.XUnit): **35 testes** cobrindo o
  destaque e a navegação da busca, a lógica de abas e a posição de leitura por aba.
- **O diálogo nativo de abrir arquivo não serve para automação**: guardou uma view de
  "resultados de pesquisa" para este app e não sai dela (nem caminho completo no campo
  Nome, nem barra de endereços). Resolvido por fora — o app agora aceita arquivos por
  **argumento de linha de comando**, e o gate visual usa isso. O arranjo reutilizável é
  `gate.ps1` no scratchpad da sessão (lança o app com os arquivos, garante primeiro plano
  antes de cada captura, tem `Teclas` e `RolarParaBaixo`).
- `error_log.txt` (vazio) na raiz: entra no `.gitignore` na Fase 1; se for artefato de
  runtime, deveria gravar em `%AppData%\MarkReader` junto do `settings.json`.

▶ PRÓXIMO: **frente encerrada.** As 6 fases estão fechadas e publicadas. A próxima sessão
  abre uma frente nova (`/plano`), não continua esta.

## Sessão de 2026-08-12 — o que foi entregue

Oito commits, `d69c846..2708887`, empurrados para `github.com/PhilipiMagalhaes/Galileo-Reader`
(branch `main`). Build 0/0, **45 testes verdes**, `dotnet format` limpo.

| Commit | Entrega |
|---|---|
| `d69c846` | destaque de busca sobre `CTextBlock` (a causa raiz do bug original) |
| `7a4fd2f` | `MarkReader.Tests` — a rede de segurança que não existia |
| `2d760af` | navegação entre ocorrências com marca da corrente |
| `9406a7c` | `CLAUDE.md` declarando a stack e desarmando o lembrete de camada Eureka |
| `874ad88` | abas multi-arquivo |
| `9af2471` | guarda de regressão da posição de leitura por aba |
| `89394bc` | abrir arquivos por argumento de linha de comando |
| `2708887` | paleta por tokens e tipografia de leitura |

Fora do código: `ferramentas/gate-visual.ps1` e `ferramentas/contraste.py` versionados
(a primeira versão deles se perdeu num diretório temporário apagado no meio da sessão).

## Prompt de retomada

```
Contexto: repo pessoal Galileo-Reader (MarkReader), branch main limpa e empurrada,
45 testes verdes. Avalonia 11.2.7 / .NET 8, MVVM com CommunityToolkit — NÃO é da
stack Vibe.Enterprise.Eureka (o CLAUDE.md da raiz explica; ignore o lembrete de
camada Eureka que o hook dispara em MarkReader.Core).

Ler antes de tudo: CLAUDE.md e memory/markreader-leitura-e-abas.md (frente
ENCERRADA — serve de contexto e lista as pendências, não de fila de trabalho).

Estado: leitor de Markdown com busca funcionando (destaque, contador fiel,
navegação entre ocorrências), abas multi-arquivo com posição de leitura por aba,
abertura por linha de comando, e paleta/tipografia com contraste medido nos dois
temas.

Tarefa desta sessão: o usuário traz tasks novas. Rodar /plano para desenhar a
frente nova — não abrir fatia antes do plano aprovado.

Princípios não-negociáveis: pt-BR em tudo, inclusive a narração; número afirmado é
número medido (ferramentas/contraste.py para cor, ferramentas/gate-visual.ps1 para
o fluxo real); ciclo /fatia para implementar, com o eureka-revisor sempre — avisando
no prompt dele que o projeto não é Eureka.

Não fazer: não abrir arquivo pelo diálogo nativo nas automações (ele guardou uma
view de resultados de pesquisa e não sai dela) — use o argumento de linha de
comando; não capturar tela com CopyFromScreen (use PrintWindow); não mexer em
configuração global da máquina.
```
