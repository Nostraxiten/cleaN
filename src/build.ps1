<#
    build.ps1 - compiles cleaN from source and drops the result into /release

    Requirements: .NET 8 SDK (https://dotnet.microsoft.com/download/dotnet/8.0) on Windows.
    Usage:        cd src ; ./build.ps1
                  ./build.ps1 -Runtime win-arm64      # build for ARM64 devices
                  ./build.ps1 -Output ../compilado    # send the binary somewhere else
#>
[CmdletBinding()]
param(
    [string]$Runtime = 'win-x64',
    [string]$Configuration = 'Release',
    [string]$Output = '../release'
)

$ErrorActionPreference = 'Stop'
Set-Location -Path $PSScriptRoot

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw 'The .NET SDK was not found. Install .NET 8 from https://dotnet.microsoft.com/download/dotnet/8.0 and try again.'
}

Write-Host "Building cleaN ($Configuration / $Runtime)..." -ForegroundColor Cyan

dotnet publish ./cleaN.csproj `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    -p:PublishSingleFile=true `
    -o $Output

if ($LASTEXITCODE -ne 0) {
    throw "The build failed with exit code $LASTEXITCODE."
}

$exe = Join-Path $Output 'cleaN.exe'
if (Test-Path $exe) {
    $sizeMb = [math]::Round((Get-Item $exe).Length / 1MB, 1)
    Write-Host "Build succeeded. Executable available at $exe ($sizeMb MB)." -ForegroundColor Green
} else {
    Write-Warning "The build reported success but $exe was not found."
}
