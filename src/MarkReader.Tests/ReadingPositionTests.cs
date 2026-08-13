using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MarkReader.Core.ViewModels;
using MarkReader.Desktop;
using Xunit;

namespace MarkReader.Tests;

/// <summary>
/// Posição de leitura por aba, exercitada na <see cref="MainWindow"/> de verdade — é layout,
/// não lógica de VM: só a janela montada responde onde cada <see cref="ScrollViewer"/> parou.
/// <para>
/// O comportamento não veio de código próprio: cai do desenho da Fase 4 (um visualizador vivo
/// por aba, escondido em vez de destruído). Estes testes existem para que ninguém o derrube
/// sem perceber.
/// </para>
/// </summary>
public class ReadingPositionTests : IDisposable
{
    private const double Largura = 900;
    private const double Altura = 700;

    private readonly PastaTemporaria _pasta = new("markreader-rolagem");
    private readonly List<Window> _janelas = new();

    public void Dispose()
    {
        foreach (var janela in _janelas) janela.Close();
        _pasta.Dispose();
        GC.SuppressFinalize(this);
    }

    [AvaloniaFact]
    public async Task Aba_nova_abre_no_topo_mesmo_com_a_anterior_rolada()
    {
        var janela = AbrirJanela(out var vm);

        await vm.LoadFileAsync(Documento("a.md", "A"));
        RolarAte(janela, 0.75);

        await vm.LoadFileAsync(Documento("b.md", "B"));
        Assentar(janela);

        Assert.Equal(0, PosicaoAtual(janela));
    }

    [AvaloniaFact]
    public async Task Voltar_para_uma_aba_restaura_onde_a_leitura_parou()
    {
        var janela = AbrirJanela(out var vm);

        await vm.LoadFileAsync(Documento("a.md", "A"));
        var posicaoDeA = RolarAte(janela, 0.75);
        var visualizadorDeA = VisualizadorAtivo(janela);

        await vm.LoadFileAsync(Documento("b.md", "B"));
        Assentar(janela);
        Assert.NotSame(visualizadorDeA, VisualizadorAtivo(janela));

        vm.SelectPreviousDocument();
        Assentar(janela);

        // Amarrado à identidade do documento: um visualizador único compartilhado também
        // devolveria 900 aqui, sem lembrar de nada.
        Assert.Equal("a.md", vm.SelectedDocument?.FileName);
        Assert.Same(visualizadorDeA, VisualizadorAtivo(janela));
        Assert.Equal(posicaoDeA, PosicaoAtual(janela));
    }

    [AvaloniaFact]
    public async Task Cada_aba_guarda_a_propria_posicao()
    {
        var janela = AbrirJanela(out var vm);

        await vm.LoadFileAsync(Documento("a.md", "A"));
        var posicaoDeA = RolarAte(janela, 0.25);

        await vm.LoadFileAsync(Documento("b.md", "B"));
        var posicaoDeB = RolarAte(janela, 0.75);

        vm.SelectPreviousDocument();
        Assentar(janela);
        Assert.Equal(posicaoDeA, PosicaoAtual(janela));

        vm.SelectNextDocument();
        Assentar(janela);
        Assert.Equal(posicaoDeB, PosicaoAtual(janela));

        Assert.NotEqual(posicaoDeA, posicaoDeB);
    }

    [AvaloniaFact]
    public async Task Reabrir_arquivo_ja_aberto_volta_para_onde_a_leitura_parou()
    {
        var janela = AbrirJanela(out var vm);

        var a = Documento("a.md", "A");
        await vm.LoadFileAsync(a);
        var posicaoDeA = RolarAte(janela, 0.75);

        await vm.LoadFileAsync(Documento("b.md", "B"));
        Assentar(janela);

        // Caminho distinto do Ctrl+Tab: aqui a aba é reativada pelo atalho de "já aberto".
        await vm.LoadFileAsync(a);
        Assentar(janela);

        Assert.Equal(posicaoDeA, PosicaoAtual(janela));
    }

    [AvaloniaFact]
    public async Task Fechar_a_aba_vizinha_nao_mexe_na_posicao_da_aba_da_frente()
    {
        var janela = AbrirJanela(out var vm);

        await vm.LoadFileAsync(Documento("a.md", "A"));
        await vm.LoadFileAsync(Documento("b.md", "B"));
        var posicaoDeB = RolarAte(janela, 0.75);

        vm.CloseDocument(vm.OpenDocuments[0]);
        Assentar(janela);

        Assert.Equal(posicaoDeB, PosicaoAtual(janela));
    }

    [AvaloniaFact]
    public async Task Toda_aba_aberta_tem_seu_visualizador_realizado()
    {
        var janela = AbrirJanela(out var vm);

        await vm.LoadFileAsync(Documento("a.md", "A"));
        await vm.LoadFileAsync(Documento("b.md", "B"));
        await vm.LoadFileAsync(Documento("c.md", "C"));
        Assentar(janela);

        // Invariante que sustenta esta fase inteira: virtualizar as abas (trocar o ItemsPanel
        // por um painel virtualizante) descartaria os visualizadores das abas de fundo e a
        // posição de leitura sumiria junto.
        var host = janela.GetVisualDescendants().OfType<ItemsControl>()
            .First(controle => controle.Name == "DocumentHost");

        Assert.Equal(vm.OpenDocuments.Count, host.GetRealizedContainers().Count());
    }

    // ── apoio ────────────────────────────────────────────────────────

    private MainWindow AbrirJanela(out MainViewModel vm)
    {
        vm = new MainViewModel(new MarkdownServiceFake(), new SettingsServiceFake());
        var janela = new MainWindow(vm) { Width = Largura, Height = Altura };

        _janelas.Add(janela);
        janela.Show();
        Assentar(janela);

        return janela;
    }

    /// <summary>
    /// Layout **e** fila do dispatcher. Sem drenar a fila, uma regressão que zerasse o offset
    /// via <c>Dispatcher.Post</c> — a forma mais provável de escrevê-la — passaria despercebida.
    /// </summary>
    private static void Assentar(Window janela)
    {
        janela.Measure(new Size(Largura, Altura));
        janela.Arrange(new Rect(0, 0, Largura, Altura));
        janela.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>
    /// O único visualizador rolável visível. O contador é proposital: dois roláveis aninhados
    /// significariam que a roda do mouse move um e o teste mede o outro.
    /// </summary>
    private static ScrollViewer VisualizadorAtivo(Window janela)
    {
        var candidatos = janela.GetVisualDescendants()
            .OfType<ScrollViewer>()
            .Where(v => v.IsEffectivelyVisible && v.Extent.Height > v.Viewport.Height)
            .ToList();

        Assert.Single(candidatos);
        return candidatos[0];
    }

    /// <summary>Rola até uma fração do total e devolve onde parou.</summary>
    private static double RolarAte(Window janela, double fracao)
    {
        Assentar(janela);

        var visualizador = VisualizadorAtivo(janela);
        var maximo = visualizador.ScrollBarMaximum.Y;
        Assert.True(maximo > 0, "o documento não ficou alto o suficiente para rolar");

        // SetCurrentValue e não atribuição direta: escrever valor local sobrescreveria uma
        // binding em Offset, e o teste ficaria verde por cima de uma binding quebrada.
        visualizador.SetCurrentValue(ScrollViewer.OffsetProperty, visualizador.Offset.WithY(maximo * fracao));
        Assentar(janela);

        return visualizador.Offset.Y;
    }

    private static double PosicaoAtual(Window janela) => VisualizadorAtivo(janela).Offset.Y;

    private string Documento(string nome, string marcador)
    {
        var linhas = Enumerable.Range(1, 60).SelectMany(i => new[]
        {
            $"## {marcador} secao {i}",
            string.Empty,
            $"Paragrafo {i} do documento {marcador}, com texto suficiente para ocupar altura.",
            string.Empty
        });

        return _pasta.Escrever(nome, string.Join(Environment.NewLine, linhas));
    }
}
