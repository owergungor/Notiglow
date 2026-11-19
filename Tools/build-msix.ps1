param(
    [switch]$SkipPublish = $false,
    [switch]$SignPackage = $true
)

$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path "$PSScriptRoot\..").Path

$dotnetExe = Join-Path $env:LOCALAPPDATA "Microsoft\dotnet\dotnet.exe"
if (-not (Test-Path $dotnetExe)) {
    $dotnetExe = Get-Command "dotnet" -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source
}
if (-not $dotnetExe -or -not (Test-Path $dotnetExe)) {
    throw "dotnet.exe not found."
}

$makeAppxCandidates = @(
    "C:\Users\omer\.nuget\packages\microsoft.windows.sdk.buildtools\10.0.26100.1742\bin\10.0.26100.0\x64\makeappx.exe",
    "C:\Users\omer\AppData\Local\Programs\msvc\msvc\Windows Kits\10\bin\10.0.22621.0\x64\makeappx.exe"
)
$makeAppxPath = $makeAppxCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $makeAppxPath) {
    throw "makeappx.exe not found."
}

Write-Host "=== NotiGlow v4.1 MSIX Build Pipeline ===" -ForegroundColor Cyan

# 1. Publish self-contained x64 if requested
$publishDir = Join-Path $repoRoot "bin\Release\net9.0-windows10.0.19041.0\win-x64\publish"
if (-not $SkipPublish -or -not (Test-Path (Join-Path $publishDir "NotiGlow.exe"))) {
    Write-Host "[1/5] Publishing self-contained Release win-x64..." -ForegroundColor Yellow
    & $dotnetExe publish "$repoRoot\NotiGlow.csproj" -c Release -r win-x64 --self-contained true
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
$msixPath = Join-Path $outputDir "NotiGlow-v4.1-x64.msix"

Write-Host "[4/5] Packaging MSIX: $msixPath..." -ForegroundColor Yellow
& $makeAppxPath pack /v /h SHA256 /d $stagingDir /p $msixPath /o
if ($LASTEXITCODE -ne 0) {
    throw "makeappx pack failed with exit code $LASTEXITCODE"
}

# 5. Sign Package if signtool and publisher certificate are available
if ($SignPackage) {
    Write-Host "[5/5] Checking signing tools and publisher certificate..." -ForegroundColor Yellow
    $subject = "CN=2F9A5694-FADC-405A-AF51-BB9269D3FFF1"
    $cert = Get-ChildItem Cert:\CurrentUser\My -ErrorAction SilentlyContinue |
        Where-Object { $_.Subject -eq $subject -and $_.HasPrivateKey } |
        Select-Object -First 1

    $signtoolCandidates = @(
        (Get-Command "signtool.exe" -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source),
        "C:\Users\omer\.nuget\packages\microsoft.windows.sdk.buildtools\10.0.26100.1742\bin\10.0.26100.0\x64\signtool.exe",
        "C:\Users\omer\.nuget\packages\microsoft.windows.sdk.buildtools\10.0.22621.756\bin\10.0.22621.0\x64\signtool.exe"
    )
    $signtoolPath = $signtoolCandidates | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1

    if ($signtoolPath -and $cert) {
        Write-Host "Signing MSIX with $signtoolPath using cert $($cert.Thumbprint)..." -ForegroundColor Cyan
        & $signtoolPath sign /fd SHA256 /sha1 $cert.Thumbprint $msixPath
        if ($LASTEXITCODE -ne 0) {
            throw "signtool sign failed with exit code $LASTEXITCODE"
        }
    } else {
        Write-Host "Note: signtool or publisher certificate not found. Package remains unsigned." -ForegroundColor Yellow
    }
}

# Compute Hash & Size
$item = Get-Item $msixPath
$hash = (Get-FileHash -Path $msixPath -Algorithm SHA256).Hash
Write-Host "=== MSIX Build Complete ===" -ForegroundColor Green
Write-Host "Package Path: $msixPath"
Write-Host "Size: $($item.Length) bytes ($([math]::Round($item.Length / 1MB, 2)) MB)"
Write-Host "SHA256: $hash"
