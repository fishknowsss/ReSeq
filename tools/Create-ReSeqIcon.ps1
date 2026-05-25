param(
    [string]$OutputPath = "src/ReSeq/Assets/ReSeq.ico"
)

Add-Type -AssemblyName System.Drawing
Add-Type @"
using System;
using System.Runtime.InteropServices;

public static class NativeIcon
{
    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool DestroyIcon(IntPtr handle);
}
"@

$resolvedOutput = Join-Path (Get-Location) $OutputPath
$outputDir = Split-Path -Parent $resolvedOutput
New-Item -ItemType Directory -Force -Path $outputDir | Out-Null

$size = 256
$bitmap = [System.Drawing.Bitmap]::new($size, $size)
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$graphics.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit

$background = [System.Drawing.Drawing2D.LinearGradientBrush]::new(
    [System.Drawing.Rectangle]::new(0, 0, $size, $size),
    [System.Drawing.Color]::FromArgb(22, 28, 36),
    [System.Drawing.Color]::FromArgb(42, 55, 70),
    45
)
$graphics.FillRectangle($background, 0, 0, $size, $size)

$accent = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(59, 130, 246))
$softAccent = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(34, 197, 94))
$white = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(246, 248, 251))
$muted = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(116, 129, 148), 9)
$gridPen = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(90, 104, 124), 8)
$accentPen = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(59, 130, 246), 12)

$outer = [System.Drawing.Rectangle]::new(30, 34, 196, 188)
$path = [System.Drawing.Drawing2D.GraphicsPath]::new()
$radius = 34
$path.AddArc($outer.X, $outer.Y, $radius, $radius, 180, 90)
$path.AddArc($outer.Right - $radius, $outer.Y, $radius, $radius, 270, 90)
$path.AddArc($outer.Right - $radius, $outer.Bottom - $radius, $radius, $radius, 0, 90)
$path.AddArc($outer.X, $outer.Bottom - $radius, $radius, $radius, 90, 90)
$path.CloseFigure()
$graphics.FillPath([System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(242, 245, 249)), $path)

$graphics.DrawLine($gridPen, 82, 54, 82, 202)
$graphics.DrawLine($gridPen, 144, 54, 144, 202)
$graphics.DrawLine($gridPen, 50, 96, 206, 96)
$graphics.DrawLine($gridPen, 50, 150, 206, 150)

$graphics.FillRectangle($accent, 96, 110, 36, 26)
$graphics.FillRectangle($softAccent, 158, 164, 34, 26)
$graphics.DrawLine($accentPen, 50, 150, 206, 150)

$font = [System.Drawing.Font]::new("Segoe UI Semibold", 42, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
$graphics.DrawString("R", $font, $white, 68, 45)

$hIcon = $bitmap.GetHicon()
try {
    $icon = [System.Drawing.Icon]::FromHandle($hIcon)
    $stream = [System.IO.File]::Create($resolvedOutput)
    try {
        $icon.Save($stream)
    }
    finally {
        $stream.Dispose()
        $icon.Dispose()
    }
}
finally {
    [NativeIcon]::DestroyIcon($hIcon) | Out-Null
    $graphics.Dispose()
    $bitmap.Dispose()
}

Write-Host "Created $resolvedOutput"
