# 获取 Microsoft.WSL.Containers SDK 包到本地 NuGet 源。
# 该包仅随 microsoft/WSL 的 GitHub Release 发布，不在 nuget.org 上，
# 因此需要显式下载后由 nuget.config 中的 wsl-containers 本地源提供。
param(
    [string]$Version = "2.9.12"
)

$ErrorActionPreference = "Stop"

$root = $PSScriptRoot | Split-Path -Parent
$feed = Join-Path $root "nuget-packages"
$target = Join-Path $feed "Microsoft.WSL.Containers.$Version.nupkg"

if (Test-Path $target) {
    Write-Host "已存在：$target" -ForegroundColor DarkGray
    exit 0
}

New-Item -ItemType Directory -Path $feed -Force | Out-Null

$url = "https://github.com/microsoft/WSL/releases/download/$Version/Microsoft.WSL.Containers.$Version.nupkg"
Write-Host "==> 下载 Microsoft.WSL.Containers $Version"
Invoke-WebRequest -Uri $url -OutFile $target -TimeoutSec 180
Write-Host "已下载：$target" -ForegroundColor Green
