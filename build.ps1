# WSLC 容器管理器构建脚本
# 用法：.\build.ps1 [-Configuration Debug|Release] [-Platform x64|arm64]
# 说明：使用 dotnet build 构建（本机已无 Visual Studio MSBuild）。

param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug",
    [ValidateSet("x64", "arm64")]
    [string]$Platform = "x64"
)

$ErrorActionPreference = "Stop"

$project = Join-Path $PSScriptRoot "src\WSLCC.App\WSLCC.App.csproj"
Write-Host "==> dotnet build WSLCC ($Configuration|$Platform)"
dotnet build $project -c $Configuration -p:Platform=$Platform -p:RuntimeIdentifier=win-$Platform -v:m

if ($LASTEXITCODE -ne 0) {
    Write-Host "构建失败。退出码: $LASTEXITCODE" -ForegroundColor Red
    exit $LASTEXITCODE
}

$out = Join-Path $PSScriptRoot "src\WSLCC.App\bin\$Platform\$Configuration\net10.0-windows10.0.26100.0\win-$Platform\WSLCC.exe"
if (-not (Test-Path $out)) {
    $out = Join-Path $PSScriptRoot "src\WSLCC.App\bin\$Configuration\net10.0-windows10.0.26100.0\win-$Platform\WSLCC.exe"
}
Write-Host "构建成功。产物: $out" -ForegroundColor Green
