# Arranjo de gate visual do MarkReader.
# Abre os arquivos por ARGUMENTO de linha de comando: o dialogo nativo do Windows
# ficou presa numa view de resultados de pesquisa e nao serve mais para automacao.
#
#   .\gate.ps1 -Arquivos a.md,b.md -Passos { param($g) ... }

param(
    [string[]] $Arquivos = @(),
    [scriptblock] $Passos
)

Add-Type -AssemblyName System.Windows.Forms, System.Drawing

$scratch = "C:\Users\Phili\AppData\Local\Temp\claude\C--Users-Phili-Documents-antigravity-proud-galileo\f6b871c2-db58-415f-8ece-8fbd896aeb87\scratchpad"
$exe     = "C:\Users\Phili\Documents\antigravity\proud-galileo\src\MarkReader.Desktop\bin\Debug\net8.0\MarkReader.Desktop.exe"

if (-not ("Gate" -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public class Gate {
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int X, int Y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint f, uint dx, uint dy, uint d, int extra);
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr hWnd, IntPtr hdcBlt, uint nFlags);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
}
'@
}

$caminhos = $Arquivos | ForEach-Object { Join-Path $scratch $_ }
$proc = if ($caminhos.Count -gt 0) { Start-Process $exe -ArgumentList $caminhos -PassThru }
        else { Start-Process $exe -PassThru }

Start-Sleep -Seconds 5
$proc.Refresh()
$script:h = $proc.MainWindowHandle

function AoFrente {
    for ($i = 0; $i -lt 6; $i++) {
        [void][Gate]::SetForegroundWindow($script:h)
        Start-Sleep -Milliseconds 400
        if ([Gate]::GetForegroundWindow() -eq $script:h) { return }
    }
    throw "MarkReader nao ficou em primeiro plano - gate abortado"
}

# PrintWindow desenha a janela direto, mesmo em segundo plano: o Windows recusa
# SetForegroundWindow depois de muita automacao, e ai CopyFromScreen pegaria a
# janela de outro programa.
function Capturar($nome) {
    $r = New-Object Gate+RECT
    [void][Gate]::GetWindowRect($script:h, [ref]$r)
    $bmp = New-Object System.Drawing.Bitmap ($r.Right-$r.Left), ($r.Bottom-$r.Top)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $hdc = $g.GetHdc()
    $ok = [Gate]::PrintWindow($script:h, $hdc, 2)   # PW_RENDERFULLCONTENT
    $g.ReleaseHdc($hdc)
    if (-not $ok) { $g.Dispose(); $bmp.Dispose(); throw "PrintWindow falhou para $nome" }
    $bmp.Save((Join-Path $scratch $nome), [System.Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose(); $bmp.Dispose()
    Write-Output "capturado: $nome"
}

function Teclas($sequencia, $esperaMs = 900) {
    try { AoFrente } catch { Write-Output "aviso: sem foco, teclas podem nao chegar" }
    [System.Windows.Forms.SendKeys]::SendWait($sequencia)
    Start-Sleep -Milliseconds $esperaMs
}

function RolarParaBaixo($voltas) {
    AoFrente
    $r = New-Object Gate+RECT
    [void][Gate]::GetWindowRect($script:h, [ref]$r)
    [void][Gate]::SetCursorPos(($r.Left + 550), ($r.Top + 400))
    Start-Sleep -Milliseconds 300
    for ($i = 0; $i -lt $voltas; $i++) {
        [Gate]::mouse_event(0x0800, 0, 0, [uint32]4294967176, 0)
        Start-Sleep -Milliseconds 80
    }
    Start-Sleep -Milliseconds 800
}

try {
    if ($Passos) { & $Passos }
} finally {
    $proc.Kill()
}
Write-Output "fim"
