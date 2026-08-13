# MarkReader / Galileo-Reader

Leitor de Markdown desktop. Projeto **pessoal**, publicado em
`github.com/PhilipiMagalhaes/Galileo-Reader`.

## Stack

- **Avalonia 11.2.7 / .NET 8**, MVVM com **CommunityToolkit.Mvvm**.
- Renderização por **Markdown.Avalonia 11.0.3**, que desenha texto com
  `CTextBlock`/`CRun`/`CSpan` do **ColorTextBlock.Avalonia** — *não* com `TextBlock`/`Run`
  do Avalonia. Quem procurar `TextBlock` na árvore visual não acha o texto do documento.
- Projetos: `MarkReader.Core` (ViewModels + serviços), `MarkReader.Desktop` (janela,
  XAML, destaque de busca), `MarkReader.Tests` (xunit + Avalonia.Headless.XUnit).

> **Este projeto NÃO é da stack Vibe.Enterprise.Eureka.** Não existem camadas
> Domínio/Persistência/Api, SDK Eureka, analyzers VIBE nem `BlocoRegras`. O hook do
> plugin vibe-eureka casa `*.Core*` no caminho e pede a skill `eureka-dominio` ao editar
> `MarkReader.Core/` — aqui `.Core` é só a biblioteca compartilhada, então **ignore esse
> lembrete**; as convenções daquele SDK não se aplicam. O ciclo do `/fatia` (revisar,
> buildar, testar, memória, commit) continua valendo.

## Comandos

```bash
dotnet build MarkReader.slnx
dotnet test MarkReader.slnx
dotnet format style --verify-no-changes --no-restore MarkReader.slnx
```

O app é WinExe: se o build falhar com "arquivo bloqueado por MarkReader.Desktop", há uma
instância aberta (tipicamente da verificação visual) — encerre o processo e repita.

## Convenções

- Nomes de tipos e membros em **inglês**; textos de usuário, comentários e mensagens de
  commit em **pt-BR**. Commits no padrão `tipo(escopo): descrição`.
  **Exceção:** no projeto de testes os nomes são em pt-BR — o nome de um teste é a frase
  que descreve o comportamento, e vale mais legível do que uniforme.
- `MarkReader.Core` **não** referencia Avalonia. O que precisa da árvore visual entra por
  uma porta no Core implementada pelo Desktop — ver `IDocumentSearchView`.

## Verificação

Build verde não é verificação funcional. Mudança de tela fecha exercitando o fluxo real
**nos dois temas**, e afirmação sobre cor, custo ou cobertura sai de medição — ver
[ferramentas/LEIAME.md](ferramentas/LEIAME.md): `gate-visual.ps1` abre o app com arquivos
de verdade e captura a janela; `contraste.py` mede as razões WCAG da paleta.

## Estado do trabalho

O plano da frente atual vive em [memory/markreader-leitura-e-abas.md](memory/markreader-leitura-e-abas.md),
com as fases, as decisões já tomadas e o marcador `▶ PRÓXIMO`. Leia antes de começar.
