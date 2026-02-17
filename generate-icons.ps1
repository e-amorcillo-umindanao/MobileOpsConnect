Add-Type -AssemblyName System.Drawing

function New-Icon($size, $outPath) {
    $bmp = New-Object System.Drawing.Bitmap($size, $size)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAlias
    $bg = [System.Drawing.Color]::FromArgb(15, 23, 42)
    $g.Clear($bg)
    $fontSize = [int]($size * 0.45)
    $font = New-Object System.Drawing.Font("Segoe UI", $fontSize, [System.Drawing.FontStyle]::Bold)
    $sf = New-Object System.Drawing.StringFormat
    $sf.Alignment = [System.Drawing.StringAlignment]::Center
    $sf.LineAlignment = [System.Drawing.StringAlignment]::Center
    $rect = New-Object System.Drawing.RectangleF(0, 0, $size, $size)
    $g.DrawString("M", $font, [System.Drawing.Brushes]::White, $rect, $sf)
    $g.Dispose()
    $font.Dispose()
    $bmp.Save($outPath, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    Write-Host "Created: $outPath"
}

$iconDir = "c:\Users\Evan\OneDrive\Desktop\IT15 Project\MobileOpsConnect\MobileOpsConnect\wwwroot\icons"
New-Icon 192 "$iconDir\icon-192.png"
New-Icon 512 "$iconDir\icon-512.png"
Write-Host "Done!"
