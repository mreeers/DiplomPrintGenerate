# DiplomPrintGenerate build script
Write-Host "========================================================" -ForegroundColor Cyan
Write-Host "  DiplomPrintGenerate — Сборка приложения" -ForegroundColor Cyan
Write-Host "========================================================" -ForegroundColor Cyan

$msbuild = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\msbuild.exe"
$projDir = $PSScriptRoot
$projFile = Join-Path $projDir "DiplomPrintGenerate.csproj"

if (!(Test-Path $msbuild)) {
    Write-Host "[ОШИБКА] Не найден MSBuild: $msbuild" -ForegroundColor Red
    exit 1
}

Write-Host "`n[1/2] Компиляция проекта в конфигурации Release..." -ForegroundColor Yellow
& $msbuild $projFile /p:Configuration=Release /nologo /v:m

if ($LASTEXITCODE -ne 0) {
    Write-Host "[ОШИБКА] Сборка завершилась с ошибкой!" -ForegroundColor Red
    exit $LASTEXITCODE
}

Write-Host "`n[2/2] Очистка зависимостей рантайма и подготовка шаблонов..." -ForegroundColor Yellow
$binRelease = Join-Path $projDir "bin\Release"
Remove-Item (Join-Path $binRelease "mscorlib.dll") -ErrorAction SilentlyContinue
Remove-Item (Join-Path $binRelease "*.nlp") -ErrorAction SilentlyContinue
Remove-Item (Join-Path $binRelease "ru") -Recurse -Force -ErrorAction SilentlyContinue

$sourceTemplates = Join-Path $projDir "templates"
$destTemplates = Join-Path $binRelease "templates"
if (Test-Path $sourceTemplates) {
    if (!(Test-Path $destTemplates)) { New-Item -ItemType Directory -Path $destTemplates | Out-Null }
    Copy-Item (Join-Path $sourceTemplates "*") $destTemplates -Force -ErrorAction SilentlyContinue
}

$exePath = Join-Path $binRelease "DiplomPrintGenerate.exe"
if (Test-Path $exePath) {
    $size = (Get-Item $exePath).Length / 1024
    Write-Host "`n========================================================" -ForegroundColor Green
    Write-Host "  Сборка успешно завершена!" -ForegroundColor Green
    Write-Host "  Файл: $exePath ($([math]::Round($size, 1)) КБ)" -ForegroundColor Green
    Write-Host "========================================================" -ForegroundColor Green
} else {
    Write-Host "[ОШИБКА] Исполняемый файл не найден!" -ForegroundColor Red
}
