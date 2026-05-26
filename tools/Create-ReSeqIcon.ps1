param(
    [string]$OutputPath = "src/ReSeq/Assets/ReSeq.ico"
)

Add-Type -AssemblyName System.Drawing

$resolvedOutput = Join-Path (Get-Location) $OutputPath
$outputDir = Split-Path -Parent $resolvedOutput
New-Item -ItemType Directory -Force -Path $outputDir | Out-Null

function New-RoundedRectanglePath {
    param(
        [float]$X,
        [float]$Y,
        [float]$Width,
        [float]$Height,
        [float]$Radius
    )

    $path = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $diameter = $Radius * 2
    $path.AddArc($X, $Y, $diameter, $diameter, 180, 90)
    $path.AddArc($X + $Width - $diameter, $Y, $diameter, $diameter, 270, 90)
    $path.AddArc($X + $Width - $diameter, $Y + $Height - $diameter, $diameter, $diameter, 0, 90)
    $path.AddArc($X, $Y + $Height - $diameter, $diameter, $diameter, 90, 90)
    $path.CloseFigure()
    return $path
}

function ConvertTo-IconNumber {
    param([float]$Value, [float]$Scale)
    return [float]($Value * $Scale)
}

function New-ReSeqFramePng {
    param([int]$Size)

    $scale = $Size / 256.0
    $bitmap = [System.Drawing.Bitmap]::new($Size, $Size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality

    try {
        $outer = New-RoundedRectanglePath `
            (ConvertTo-IconNumber 14 $scale) `
            (ConvertTo-IconNumber 14 $scale) `
            (ConvertTo-IconNumber 228 $scale) `
            (ConvertTo-IconNumber 228 $scale) `
            (ConvertTo-IconNumber 46 $scale)

        $bgBrush = [System.Drawing.Drawing2D.LinearGradientBrush]::new(
            [System.Drawing.RectangleF]::new(0, 0, $Size, $Size),
            [System.Drawing.Color]::FromArgb(255, 17, 24, 39),
            [System.Drawing.Color]::FromArgb(255, 30, 64, 105),
            45)
        $graphics.FillPath($bgBrush, $outer)
        $graphics.DrawPath([System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(120, 179, 211, 255), [Math]::Max(1.2, 2.8 * $scale)), $outer)

        $cardBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(238, 248, 251, 255))
        $cardBorder = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(255, 214, 228, 245), [Math]::Max(1, 2.2 * $scale))
        $accentBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 47, 111, 237))
        $cyanBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 40, 194, 203))

        $cards = @(
            @(54, 58, 62, 46),
            @(128, 58, 62, 46),
            @(54, 124, 62, 46),
            @(128, 124, 62, 46)
        )

        foreach ($card in $cards) {
            $rect = New-RoundedRectanglePath `
                (ConvertTo-IconNumber $card[0] $scale) `
                (ConvertTo-IconNumber $card[1] $scale) `
                (ConvertTo-IconNumber $card[2] $scale) `
                (ConvertTo-IconNumber $card[3] $scale) `
                (ConvertTo-IconNumber 10 $scale)
            $graphics.FillPath($cardBrush, $rect)
            $graphics.DrawPath($cardBorder, $rect)
            $graphics.FillRectangle(
                $accentBrush,
                (ConvertTo-IconNumber ($card[0] + 8) $scale),
                (ConvertTo-IconNumber ($card[1] + 9) $scale),
                (ConvertTo-IconNumber 18 $scale),
                (ConvertTo-IconNumber 5 $scale))
        }

        $stack1 = New-RoundedRectanglePath (ConvertTo-IconNumber 110 $scale) (ConvertTo-IconNumber 160 $scale) (ConvertTo-IconNumber 78 $scale) (ConvertTo-IconNumber 42 $scale) (ConvertTo-IconNumber 10 $scale)
        $stack2 = New-RoundedRectanglePath (ConvertTo-IconNumber 122 $scale) (ConvertTo-IconNumber 174 $scale) (ConvertTo-IconNumber 78 $scale) (ConvertTo-IconNumber 42 $scale) (ConvertTo-IconNumber 10 $scale)
        $graphics.FillPath([System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(180, 65, 88, 122)), $stack1)
        $graphics.FillPath($cardBrush, $stack2)
        $graphics.DrawPath($cardBorder, $stack2)
        $graphics.FillEllipse($cyanBrush, (ConvertTo-IconNumber 138 $scale), (ConvertTo-IconNumber 188 $scale), (ConvertTo-IconNumber 8 $scale), (ConvertTo-IconNumber 8 $scale))
        $graphics.FillEllipse($cyanBrush, (ConvertTo-IconNumber 154 $scale), (ConvertTo-IconNumber 188 $scale), (ConvertTo-IconNumber 8 $scale), (ConvertTo-IconNumber 8 $scale))
        $graphics.FillEllipse($cyanBrush, (ConvertTo-IconNumber 170 $scale), (ConvertTo-IconNumber 188 $scale), (ConvertTo-IconNumber 8 $scale), (ConvertTo-IconNumber 8 $scale))

        $arrowPen = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(255, 87, 160, 255), [Math]::Max(2, 9 * $scale))
        $arrowPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
        $arrowPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
        $graphics.DrawLine($arrowPen, (ConvertTo-IconNumber 68 $scale), (ConvertTo-IconNumber 202 $scale), (ConvertTo-IconNumber 98 $scale), (ConvertTo-IconNumber 202 $scale))
        $graphics.DrawLine($arrowPen, (ConvertTo-IconNumber 92 $scale), (ConvertTo-IconNumber 190 $scale), (ConvertTo-IconNumber 108 $scale), (ConvertTo-IconNumber 202 $scale))
        $graphics.DrawLine($arrowPen, (ConvertTo-IconNumber 92 $scale), (ConvertTo-IconNumber 214 $scale), (ConvertTo-IconNumber 108 $scale), (ConvertTo-IconNumber 202 $scale))
    }
    finally {
        $graphics.Dispose()
    }

    $stream = [System.IO.MemoryStream]::new()
    try {
        $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
        return $stream.ToArray()
    }
    finally {
        $stream.Dispose()
        $bitmap.Dispose()
    }
}

$frames = @(16, 32, 48, 128, 256) | ForEach-Object {
    [pscustomobject]@{
        Size = $_
        Bytes = New-ReSeqFramePng $_
    }
}

$file = [System.IO.File]::Create($resolvedOutput)
$writer = [System.IO.BinaryWriter]::new($file)
try {
    $writer.Write([UInt16]0)
    $writer.Write([UInt16]1)
    $writer.Write([UInt16]$frames.Count)

    $offset = 6 + (16 * $frames.Count)
    foreach ($frame in $frames) {
        $sizeByte = if ($frame.Size -eq 256) { [byte]0 } else { [byte]$frame.Size }
        $writer.Write($sizeByte)
        $writer.Write($sizeByte)
        $writer.Write([byte]0)
        $writer.Write([byte]0)
        $writer.Write([UInt16]1)
        $writer.Write([UInt16]32)
        $writer.Write([UInt32]$frame.Bytes.Length)
        $writer.Write([UInt32]$offset)
        $offset += $frame.Bytes.Length
    }

    foreach ($frame in $frames) {
        $bytes = [byte[]]$frame.Bytes
        $writer.Write($bytes, 0, $bytes.Length)
    }
}
finally {
    $writer.Dispose()
    $file.Dispose()
}

Write-Host "Created $resolvedOutput"
