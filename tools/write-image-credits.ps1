# Turns seed-images/credits.json into docs/image-credits.md.
# Run after tools/fetch-seed-images.ps1.
param(
    [string]$JsonPath = "$PSScriptRoot\..\AgriConnect\wwwroot\seed-images\credits.json",
    [string]$OutFile     = "$PSScriptRoot\..\docs\image-credits.md"
)

$ErrorActionPreference = "Stop"
# ConvertFrom-Json hands back the array as a single pipeline object in Windows
# PowerShell, so pipe it through to get a countable array rather than one item.
$entries = @(Get-Content $JsonPath -Raw | ConvertFrom-Json | ForEach-Object { $_ })

$lines = @(
    "# Image credits"
    ""
    "Every photo in ``AgriConnect/wwwroot/seed-images`` comes from Wikimedia Commons"
    "under a free licence. They are demonstration data, not AgriConnect's own work."
    "CC BY and CC BY-SA require the author and licence to travel with the image, which"
    "is what this file is for. Replace them before using this application for real."
    ""
    "Each was centre-cropped to 4:3 and resized to 800x600. No other changes were made."
    ""
    "Regenerate with ``tools/fetch-seed-images.ps1`` then ``tools/write-image-credits.ps1``."
    ""
    "| File | Author | Licence | Source |"
    "|---|---|---|---|"
)

foreach ($e in ($entries | Sort-Object File)) {
    $author = if ($e.Author) { $e.Author } else { "see source page" }
    if ($author.Length -gt 48) { $author = $author.Substring(0, 48).TrimEnd() + "..." }
    $author = $author -replace '\|', '/'
    $lines += "| ``$($e.File)`` | $author | $($e.License) | [Commons page]($($e.CommonsPage)) |"
}

$lines += ""
$lines += "Licence texts: <https://creativecommons.org/licenses/>"
$lines += ""

New-Item -ItemType Directory -Force (Split-Path $OutFile) | Out-Null
$lines -join "`n" | Out-File $OutFile -Encoding utf8
Write-Host "wrote $OutFile ($($entries.Count) images)"
