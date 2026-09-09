# WSLC 发布脚本：单目录自包含产物 + zip 分发包
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    [ValidateSet("win-x64", "win-arm64")]
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"

$project = Join-Path $PSScriptRoot "src\WSLCC.App\WSLCC.App.csproj"
$publishDir = Join-Path $PSScriptRoot "dist\publish"
$exe = Join-Path $publishDir "WSLCC.App.exe"

Write-Host "==> dotnet publish ($Configuration|$Runtime, self-contained)"
dotnet publish $project -c $Configuration -r $Runtime --self-contained true -o $publishDir -v:m
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

# 读取版本号
$version = "0.1.0"
try {
    $info = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($exe)
    if ($info.ProductVersion) { $version = $info.ProductVersion.Split('+')[0] }
} catch { }

$zip = Join-Path $PSScriptRoot "dist\WSLCC-$version-$Runtime.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path "$publishDir\*" -DestinationPath $zip -Force
Write-Host "发布完成：$zip" -ForegroundColor Green