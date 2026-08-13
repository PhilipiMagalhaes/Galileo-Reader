# Ferramentas de verificação

Nasceram durante o plano de busca/abas/legibilidade e estão versionadas porque **a sessão
que as criou perdeu a primeira versão delas** — viviam num diretório temporário que foi
apagado no meio do trabalho.

## `gate-visual.ps1`

Arranjo do gate visual: abre o app com arquivos de verdade e captura a janela.

```powershell
.\ferramentas\gate-visual.ps1 -Arquivos a.md,b.md -Passos { Capturar "antes.png"; Teclas "^f" ; Capturar "depois.png" }
```

Duas decisões que custaram caro para descobrir:

- **Abre por argumento de linha de comando, nunca pelo diálogo.** O diálogo nativo do
  Windows guardou uma view de "resultados de pesquisa" para este app e não sai dela — nem
  digitando o caminho completo no campo Nome, nem pela barra de endereços.
- **Captura com `PrintWindow`, não com `CopyFromScreen`.** Depois de bastante automação o
  Windows recusa `SetForegroundWindow`; com captura de tela, o resultado seria a janela de
  outro programa — aconteceu. `PrintWindow` desenha a janela mesmo em segundo plano.

O caminho do scratchpad está fixo no topo do arquivo; ajuste ao usar em outra sessão.
Para exercitar o tema claro, prefira alterar `%AppData%\MarkReader\settings.json` antes de
lançar, em vez de mandar `Ctrl+T` sem foco — sem foco, as teclas vão para outro programa.

## `contraste.py`

Calcula as razões de contraste WCAG 2.2 da paleta e compara com o alvo de cada par
(corpo AAA ≥ 7:1, demais textos AA ≥ 4,5:1, separadores e distinção entre destaques ≥ 3:1).

```bash
python ferramentas/contraste.py
```

As cores estão nos dicionários `CLARO`/`ESCURO` e devem espelhar os tokens de
`src/MarkReader.Desktop/App.axaml`. Foi ele que pegou dois pares reprovados que já estavam
commitados — vale rodar sempre que uma cor mudar.
