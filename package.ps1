# Builds PostMortem and produces a Thunderstore-compatible package.
#
# Uses ZipArchive directly rather than Compress-Archive: the ZIP spec requires forward slashes
# as path separators, and Compress-Archive writes backslashes, which breaks extraction on
# non-Windows systems (and for some mod managers).

$ErrorActionPreference = "Stop"

$root = $PSScriptRoot
$dist = Join-Path $root "dist"
$zipPath = Join-Path $dist "PostMortem.zip"
$pluginRoot = "BepInEx/plugins/DeathRecap"

Write-Host "Building..."
dotnet build (Join-Path $root "DeathRecap.csproj") -c Release | Out-Null
if ($LASTEXITCODE -ne 0) { throw "Build failed" }

$dll = Join-Path $root "bin\Release\DeathRecap.dll"
if (-not (Test-Path $dll)) { $dll = Join-Path $root "bin\Debug\DeathRecap.dll" }
if (-not (Test-Path $dll)) { throw "Could not find DeathRecap.dll" }

if (-not (Test-Path $dist)) { New-Item -ItemType Directory $dist | Out-Null }
if (Test-Path $zipPath) { Remove-Item $zipPath -Force }

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

$zip = [System.IO.Compression.ZipFile]::Open($zipPath, [System.IO.Compression.ZipArchiveMode]::Create)
try {
    function Add-Entry($sourcePath, $entryName) {
        # Entry names must use forward slashes.
        $entryName = $entryName -replace '\\', '/'
        $entry = $zip.CreateEntry($entryName, [System.IO.Compression.CompressionLevel]::Optimal)
        $stream = $entry.Open()
        try {
            $bytes = [System.IO.File]::ReadAllBytes($sourcePath)
            $stream.Write($bytes, 0, $bytes.Length)
        } finally {
            $stream.Dispose()
        }
        Write-Host "  + $entryName"
    }

    # Thunderstore requires these four at the package root.
    Add-Entry (Join-Path $root "manifest.json") "manifest.json"
    Add-Entry (Join-Path $root "README.md")     "README.md"
    Add-Entry (Join-Path $root "CHANGELOG.md")  "CHANGELOG.md"
    Add-Entry (Join-Path $root "icon.png")      "icon.png"

    # The mod itself, laid out as it must sit in the game folder.
    Add-Entry $dll "$pluginRoot/DeathRecap.dll"

    Get-ChildItem (Join-Path $root "Lang") -Filter *.txt | ForEach-Object {
        Add-Entry $_.FullName "$pluginRoot/Lang/$($_.Name)"
    }
} finally {
    $zip.Dispose()
}

$sizeKb = [math]::Round((Get-Item $zipPath).Length / 1KB, 1)
Write-Host ""
Write-Host "Packaged: $zipPath ($sizeKb KB)"
