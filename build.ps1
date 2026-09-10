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

# 清理多余语言资源目录（WinUI 自包含默认输出 100+ 区域，仅保留中英文）
$outDir = Split-Path $out -Parent
$keep = @("en-us", "en-US", "en-GB", "zh-CN", "zh-Hans", "zh-Hant", "zh-TW")
Get-ChildItem $outDir -Directory | Where-Object {
    $_.Name -match "^[a-z]{2,3}(-[A-Za-z]{2,8}){0,2}$" -and $_.Name -notin $keep
} | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
Write-Host "已裁剪语言资源，保留: $($keep -join ', ')" -ForegroundColor DarkGray
