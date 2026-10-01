# WSLC 发布脚本：单目录产物 + zip 分发包
# 两种发布模式：
#   - 合并版（自包含，默认）：内置 .NET 运行时，开箱即用。产物 WSLCC-<版本>-win-x64.zip / WSLCC-Setup.exe
#   - 分离版（框架依赖）：不含运行时，需系统已安装 .NET 10 桌面运行时与 Windows App Runtime。
#     产物 WSLCC-<版本>-win-x64-framework.zip / WSLCC-Setup-framework.exe
# 用法：.\publish.ps1 [-Configuration Release] [-Runtime win-x64] [-Framework] [-InnoSetup]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    [ValidateSet("win-x64", "win-arm64")]
    [string]$Runtime = "win-x64",
    [switch]$Framework,
    [switch]$InnoSetup
)

$ErrorActionPreference = "Stop"

$project = Join-Path $PSScriptRoot "src\WSLCC.App\WSLCC.App.csproj"
$publishDir = Join-Path $PSScriptRoot "dist\publish"
$exe = Join-Path $publishDir "WSLCC.exe"

$selfContained = -not $Framework
$suffix = if ($Framework) { "-framework" } else { "" }
$mode = if ($Framework) { "框架依赖（分离版）" } else { "自包含（合并版）" }

# Microsoft.WSL.Containers 只随 microsoft/WSL 的 GitHub Release 发布，需先取到本地源
& (Join-Path $PSScriptRoot "scripts\fetch-wslc-sdk.ps1")

if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }
New-Item -ItemType Directory -Path $publishDir -Force | Out-Null

Write-Host "==> dotnet publish ($Configuration|$Runtime, $mode)"
dotnet publish $project -c $Configuration -r $Runtime --self-contained $selfContained `
    -p:Platform=x64 -p:WSLCCFrameworkDependent=$($Framework.ToString().ToLowerInvariant()) `
    -p:PublishTrimmed=false -p:PublishReadyToRun=false -o $publishDir -v:m
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

# 裁剪多余语言资源目录（仅保留中英文）
$keep = @("en-us", "en-US", "en-GB", "zh-CN", "zh-Hans", "zh-Hant", "zh-TW")
Get-ChildItem $publishDir -Directory | Where-Object {
    $_.Name -match "^[a-z]{2,3}(-[A-Za-z]{2,8}){0,2}$" -and $_.Name -notin $keep
} | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
Write-Host "已裁剪语言资源，保留: $($keep -join ', ')" -ForegroundColor DarkGray

# 读取版本号
$version = "0.1.0"
try {
    $info = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($exe)
    if ($info.ProductVersion) { $version = $info.ProductVersion.Split('+')[0] }
} catch { }

$zip = Join-Path $PSScriptRoot "dist\WSLCC-$version-$Runtime$suffix.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path "$publishDir\*" -DestinationPath $zip -Force
Write-Host "发布完成：$zip" -ForegroundColor Green

if ($InnoSetup) {
    $iscc = @(
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
        "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
        "C:\Program Files\Inno Setup 6\ISCC.exe"
    ) | Where-Object { Test-Path $_ } | Select-Object -First 1
    if (-not $iscc) { throw "未找到 Inno Setup 编译器（ISCC.exe），请先安装 Inno Setup 6" }
    $iss = Join-Path $PSScriptRoot "scripts\installer.iss"
    $dist = Join-Path $PSScriptRoot "dist"
    Write-Host "==> Inno Setup 安装器"
    & $iscc "/DSourceDir=$publishDir" "/DAppVersion=$version" "/DOutputDir=$dist" "/DOutputSuffix=$suffix" "/DFrameworkDependent=$($Framework.ToString().ToLowerInvariant())" $iss
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}
