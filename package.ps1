# ============================================================
#  ADOFAI Agent Key Viewer - 自动打包脚本
#  用法:
#    powershell -ExecutionPolicy Bypass -File package.ps1
#    powershell -ExecutionPolicy Bypass -File package.ps1 -SkipBuild
#  产物: release/<Id>-v<Version>.zip  (UnityModManager 可安装包)
# ============================================================
param([switch]$SkipBuild)

$ErrorActionPreference = "Stop"
try { [Console]::OutputEncoding = [System.Text.Encoding]::UTF8 } catch { }

$Root    = Split-Path -Parent $MyInvocation.MyCommand.Path
$Release = Join-Path $Root "release"
$Staging = Join-Path $Root ("release\_staging_" + [Guid]::NewGuid().ToString("N"))

function Write-Step($msg) { Write-Host "`n==== $msg ====" -ForegroundColor Cyan }

try {
    Set-Location $Root

    # ---------- 1. 构建 Release ----------
    if ($SkipBuild) {
        Write-Step "跳过构建 (SkipBuild)"
        if (-not (Test-Path "bin\Release\net48\ADOFAI.AgentKeyViewer.dll")) {
            Write-Host "[ERROR] 未找到已构建的 dll，请先去掉 -SkipBuild 或执行 dotnet build -c Release" -ForegroundColor Red
            exit 1
        }
    } else {
        Write-Step "构建 Release"
        & dotnet build -c Release
        if ($LASTEXITCODE -ne 0) {
            Write-Host "[ERROR] 构建失败" -ForegroundColor Red
            exit 1
        }
    }

    # ---------- 2. 读取版本信息 ----------
    Write-Step "读取 Info.json"
    $info    = Get-Content "Info.json" -Raw | ConvertFrom-Json
    $id      = $info.Id
    $ver     = $info.Version
    $dll     = "$id.dll"
    $zipName = "$id-v$ver.zip"
    Write-Host "Id=$id  Version=$ver  ->  $zipName"

    # ---------- 3. 准备暂存目录 (UMM 标准: zip 根目录含 Info.json + dll) ----------
    Write-Step "准备打包文件"
    New-Item -ItemType Directory -Path $Staging | Out-Null
    Copy-Item "bin\Release\net48\$dll" $Staging
    Copy-Item "Info.json"              $Staging
    if (Test-Path "Repository.json") { Copy-Item "Repository.json" $Staging }

    # ---------- 4. 打 zip ----------
    Write-Step "压缩为 zip"
    if (-not (Test-Path $Release)) { New-Item -ItemType Directory -Path $Release | Out-Null }
    $zipPath = Join-Path $Release $zipName
    if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
    Compress-Archive -Path "$Staging\*" -DestinationPath $zipPath -CompressionLevel Optimal

    # ---------- 5. 校验 zip 内容 ----------
    Write-Step "校验 zip 内容"
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $zip = [System.IO.Compression.ZipFile]::OpenRead($zipPath)
    try {
        foreach ($e in $zip.Entries) { Write-Host "  - $($e.FullName)" }
    } finally { $zip.Dispose() }

    Write-Host "`n[SUCCESS] 打包完成: $zipPath" -ForegroundColor Green
    Write-Host "将该 zip 放入游戏 Mods 目录即可安装 (UnityModManager 会自动解压)" -ForegroundColor Yellow

} finally {
    if (Test-Path $Staging) { Remove-Item $Staging -Recurse -Force }
}
