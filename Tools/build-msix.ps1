param(
    [switch]$SkipPublish = $false,
    [switch]$SignPackage = $true
)

$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path "$PSScriptRoot\..").Path
$makeAppxPath = "C:\Users\omer\AppData\Local\Programs\msvc\msvc\Windows Kits\10\bin\10.0.22621.0\x64\makeappx.exe"

if (-not (Test-Path $makeAppxPath)) {
    throw "makeappx.exe not found at $makeAppxPath"
}

Write-Host "=== NotiGlow v3.0 MSIX Build Pipeline ===" -ForegroundColor Cyan

# 1. Publish self-contained x64 if requested
$publishDir = Join-Path $repoRoot "bin\Release\net9.0-windows10.0.19041.0\win-x64\publish"
if (-not $SkipPublish -or -not (Test-Path (Join-Path $publishDir "NotiGlow.exe"))) {
    Write-Host "[1/5] Publishing self-contained Release win-x64..." -ForegroundColor Yellow
    & dotnet publish "$repoRoot\NotiGlow.csproj" -c Release -r win-x64 --self-contained true
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed with exit code $LASTEXITCODE"
    }
} else {
    Write-Host "[1/5] Using existing publish output at $publishDir" -ForegroundColor Green
}

# 2. Setup Staging Directory
$stagingDir = Join-Path $repoRoot "bin\msix_staging"
if (Test-Path $stagingDir) {
    Remove-Item -Path $stagingDir -Recurse -Force
}
New-Item -ItemType Directory -Path $stagingDir -Force | Out-Null
Write-Host "[2/5] Copying published binaries to staging directory..." -ForegroundColor Yellow
Copy-Item -Path "$publishDir\*" -Destination $stagingDir -Recurse -Force

# 3. Copy Manifest and Assets
Write-Host "[3/5] Copying AppxManifest and Assets..." -ForegroundColor Yellow
Copy-Item -Path "$repoRoot\Package\AppxManifest.xml" -Destination "$stagingDir\AppxManifest.xml" -Force
$assetsTarget = Join-Path $stagingDir "Assets"
New-Item -ItemType Directory -Path $assetsTarget -Force | Out-Null
Copy-Item -Path "$repoRoot\Package\Assets\*" -Destination $assetsTarget -Recurse -Force

# 4. Pack MSIX using makeappx.exe
$outputDir = Join-Path $repoRoot "artifacts\store"
if (-not (Test-Path $outputDir)) {
    New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
}
$msixPath = Join-Path $outputDir "NotiGlow-v3.0-x64.msix"

Write-Host "[4/5] Packaging MSIX: $msixPath..." -ForegroundColor Yellow
& $makeAppxPath pack /v /h SHA256 /d $stagingDir /p $msixPath /o
if ($LASTEXITCODE -ne 0) {
    throw "makeappx pack failed with exit code $LASTEXITCODE"
}

# 5. Sign Package for local installation if signtool is available
if ($SignPackage) {
    Write-Host "[5/5] Checking signing tools..." -ForegroundColor Yellow
    $subject = "CN=2F9A5694-FADC-405A-AF51-BB9269D3FFF1"
    
    $signtool = Get-Command "signtool.exe" -ErrorAction SilentlyContinue
    if ($null -ne $signtool) {
        Write-Host "Found signtool at $($signtool.Source). Signing MSIX package..." -ForegroundColor Cyan
        & $signtool sign /fd SHA256 /a /n "crius" $msixPath
    } else {
        Write-Host "Note: signtool.exe not found on PATH. Package is ready for Microsoft Partner Center upload (Partner Center applies official Store digital signatures automatically)." -ForegroundColor Gray
    }
}

# Compute Hash & Size
$item = Get-Item $msixPath
$hash = (Get-FileHash -Path $msixPath -Algorithm SHA256).Hash
Write-Host "=== MSIX Build Complete ===" -ForegroundColor Green
Write-Host "Package Path: $msixPath"
Write-Host "Size: $($item.Length) bytes ($([math]::Round($item.Length / 1MB, 2)) MB)"
Write-Host "SHA256: $hash"
