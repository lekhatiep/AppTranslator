# Đóng gói AppTranslator thành bản chạy độc lập cho Windows 10/11 64-bit (máy khách không cần cài .NET).
# Cách dùng (từ thư mục gốc repo):  powershell -ExecutionPolicy Bypass -File scripts\publish.ps1
# Kết quả: publish\AppTranslator\ (thư mục chạy) và publish\AppTranslator-<ngày>.zip (gửi khách).

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$output = Join-Path $root 'publish\AppTranslator'
$zip = Join-Path $root ("publish\AppTranslator-{0}.zip" -f (Get-Date -Format 'yyyyMMdd'))

if (Test-Path $output) { Remove-Item $output -Recurse -Force }

dotnet publish (Join-Path $root 'src\AppTranslator.App\AppTranslator.App.csproj') `
    -c Release -r win-x64 --self-contained true -o $output -nologo
if ($LASTEXITCODE -ne 0) { throw "dotnet publish thất bại ($LASTEXITCODE)" }

Copy-Item (Join-Path $root 'docs\HuongDanCaiDat.md') (Join-Path $output 'HuongDanCaiDat.md')

# Kiểm các file bắt buộc: thiếu model OCR hay hồ sơ mẫu thì app chạy nhưng không đọc được chữ.
$required = @('AppTranslator.App.exe', 'models\v5\latin_PP-OCRv5_rec_mobile_infer.onnx', 'profiles\witcher3.json', 'HuongDanCaiDat.md')
foreach ($file in $required) {
    if (-not (Test-Path (Join-Path $output $file))) { throw "Bản đóng gói thiếu $file" }
}

if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path (Join-Path $output '*') -DestinationPath $zip
$sizeMb = [math]::Round((Get-Item $zip).Length / 1MB, 1)
Write-Host "Xong: $zip ($sizeMb MB)"
