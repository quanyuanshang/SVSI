param(
  [string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
  [string]$SpriteKey = "icon.mouse",
  [string]$Output = "web/public/generated/stardew-ui/icons/mouse.png",
  [int]$Scale = 2
)

$ErrorActionPreference = "Stop"

$manifestPath = Join-Path $RepoRoot "web/public/generated/stardew-ui/manifest.local.json"
if (-not (Test-Path -LiteralPath $manifestPath)) {
  $manifestPath = Join-Path $RepoRoot "web/src/stardew-ui/stardew-ui-manifest.seed.json"
}

if (-not (Test-Path -LiteralPath $manifestPath)) {
  throw "Stardew UI manifest was not found."
}

$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
$sprite = $manifest.sprites.$SpriteKey
if ($null -eq $sprite) {
  throw "Sprite key '$SpriteKey' was not found in $manifestPath."
}

if ($Scale -lt 1) {
  throw "Scale must be 1 or greater."
}

$assetPath = Join-Path $RepoRoot ("web/public/generated/stardew-ui/{0}.png" -f $sprite.asset)
if (-not (Test-Path -LiteralPath $assetPath)) {
  throw "Atlas image for '$SpriteKey' was not found: $assetPath"
}

$outputPath = Join-Path $RepoRoot $Output
$outputDir = Split-Path -Parent $outputPath
New-Item -ItemType Directory -Force -Path $outputDir | Out-Null

Add-Type -AssemblyName System.Drawing

$sourceBitmap = [System.Drawing.Bitmap]::new($assetPath)
$rect = [System.Drawing.Rectangle]::new(
  [int]$sprite.rect.x,
  [int]$sprite.rect.y,
  [int]$sprite.rect.w,
  [int]$sprite.rect.h
)
$croppedBitmap = $sourceBitmap.Clone($rect, $sourceBitmap.PixelFormat)

try {
  if ($Scale -eq 1) {
    $croppedBitmap.Save($outputPath, [System.Drawing.Imaging.ImageFormat]::Png)
  }
  else {
    $scaledBitmap = [System.Drawing.Bitmap]::new(
      $croppedBitmap.Width * $Scale,
      $croppedBitmap.Height * $Scale
    )
    $graphics = [System.Drawing.Graphics]::FromImage($scaledBitmap)

    try {
      $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
      $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
      $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::None
      $graphics.DrawImage(
        $croppedBitmap,
        [System.Drawing.Rectangle]::new(0, 0, $scaledBitmap.Width, $scaledBitmap.Height),
        [System.Drawing.Rectangle]::new(0, 0, $croppedBitmap.Width, $croppedBitmap.Height),
        [System.Drawing.GraphicsUnit]::Pixel
      )
      $scaledBitmap.Save($outputPath, [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
      $graphics.Dispose()
      $scaledBitmap.Dispose()
    }
  }
}
finally {
  $croppedBitmap.Dispose()
  $sourceBitmap.Dispose()
}

Write-Host "Cropped $SpriteKey to $outputPath at ${Scale}x"
