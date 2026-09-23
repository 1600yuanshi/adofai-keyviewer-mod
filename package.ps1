# ============================================================
#  ADOFAI Agent Key Viewer - 自动打包脚本
#  用法:
#    powershell -ExecutionPolicy Bypass -File package.ps1
#    powershell -ExecutionPolicy Bypass -File package.ps1 -SkipBuild
#    powershell -ExecutionPolicy Bypass -File package.ps1 -WithCtModule
#  产物: release/<Id>-v<Version>.zip  (UnityModManager 可安装包)
#  说明: -WithCtModule 会额外编译 CherryTools Sonnet 模块并打入 zip 的 CtModule/ 目录，
#        安装后需手动把该 dll 复制到 Mods/CheryTools/Modules/ 才能出现在 CT 面板中。
# ============================================================
param([switch]$SkipBuild, [switch]$WithCtModule)

$ErrorActionPreference = "Stop"
try { [Console]::OutputEncoding = [System.Text.Encoding]::UTF8 } catch { }

$Root    = Split-Path -Parent $MyInvocation.MyCommand.Path
$Release = Join-Path $Root "release"
$Staging = Join-Path $Root ("release\_staging_" + [Guid]::NewGuid().ToString("N"))

function Write-Step($msg) { Write-Host "`n==== $msg ====" -ForegroundColor Cyan }

try {
    Set-Location $Root

    # ---------- 1. 构建 Release ----------
    $coreDll  = "bin\Release\net48\AgentKeyViewer.Core.dll"
    $bootDll  = "bootstrap\bin\Release\net48\ADOFAI.AgentKeyViewer.dll"
    if ($SkipBuild) {
        Write-Step "跳过构建 (SkipBuild)"
        if (-not (Test-Path $coreDll)) {
            Write-Host "[ERROR] 未找到已构建的核心 dll，请先去掉 -SkipBuild" -ForegroundColor Red
            exit 1
        }
        if (-not (Test-Path $bootDll)) {
            Write-Host "[ERROR] 未找到已构建的引导器 dll，请先去掉 -SkipBuild" -ForegroundColor Red
            exit 1
        }
    } else {
        Write-Step "构建 Release（核心工程会一并构建引导器）"
        & dotnet build "AgentKeyViewer.Core.csproj" -c Release
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
    $zipName = "$id-v$ver.zip"
    Write-Host "Id=$id  Version=$ver  ->  $zipName"

    # ---------- 3. 准备暂存目录 (UMM 标准: zip 根目录含 Info.json + 引导器 dll + 核心 dll) ----------
    Write-Step "准备打包文件"
    New-Item -ItemType Directory -Path $Staging | Out-Null
    Copy-Item $bootDll                 $Staging
    Copy-Item $coreDll                 $Staging
    Copy-Item "Info.json"              $Staging
    if (Test-Path "Repository.json") { Copy-Item "Repository.json" $Staging }
    Write-Host "  引导器: $bootDll"
    Write-Host "  核心  : $coreDll（可热更新，无需重启游戏即可替换）"

    # ---------- 3b. 可选的 CT Sonnet 模块 ----------
    if ($WithCtModule) {
        Write-Step "构建 CherryTools Sonnet 模块"
        $moduleDll = "ctmodule\bin\Release\net48\CheryTools.AgentKeyViewer.dll"
        & dotnet build "ctmodule\AgentKeyViewer.CtModule.csproj" -c Release
        if ($LASTEXITCODE -eq 0 -and (Test-Path $moduleDll)) {
            $ctDir = Join-Path $Staging "CtModule"
            New-Item -ItemType Directory -Path $ctDir | Out-Null
            Copy-Item $moduleDll $ctDir
            Write-Host "  已打入 CtModule\CheryTools.AgentKeyViewer.dll（需手动复制到 Mods/CheryTools/Modules/）" -ForegroundColor Yellow
        } else {
            Write-Host "  [WARN] CT 模块构建失败（通常是因为未安装 CherryTools Sonnet），已跳过" -ForegroundColor Yellow
        }
    }

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
