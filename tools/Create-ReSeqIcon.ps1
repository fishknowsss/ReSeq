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

$background = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(20, 29, 41))
$graphics.FillRectangle($background, 0, 0, $size, $size)

$outer = [System.Drawing.Rectangle]::new(28, 28, 200, 200)
$path = [System.Drawing.Drawing2D.GraphicsPath]::new()
$radius = 34
$path.AddArc($outer.X, $outer.Y, $radius, $radius, 180, 90)
$path.AddArc($outer.Right - $radius, $outer.Y, $radius, $radius, 270, 90)
$path.AddArc($outer.Right - $radius, $outer.Bottom - $radius, $radius, $radius, 0, 90)
$path.AddArc($outer.X, $outer.Bottom - $radius, $radius, $radius, 90, 90)
$path.CloseFigure()
$graphics.DrawPath([System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(239, 244, 251), 10), $path)

$accentPen = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(83, 143, 255), 9)
$accentPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
$accentPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
$graphics.DrawLine($accentPen, 70, 184, 186, 184)

$font = [System.Drawing.Font]::new("Segoe UI Semibold", 66, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
$white = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(248, 251, 255))
$format = [System.Drawing.StringFormat]::new()
$format.Alignment = [System.Drawing.StringAlignment]::Center
$format.LineAlignment = [System.Drawing.StringAlignment]::Center
$graphics.DrawString("X-Y", $font, $white, [System.Drawing.RectangleF]::new(26, 45, 204, 126), $format)

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
