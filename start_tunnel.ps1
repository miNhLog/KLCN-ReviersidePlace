# ==============================================================================
# SCRIPT MỞ ĐƯỜNG TRUYỀN INTERNET CHO ĐIỆN THOẠI QUÉT QR (CLOUDFLARE TUNNEL)
# ==============================================================================

$cloudflaredPath = "C:\Program Files (x86)\cloudflared\cloudflared.exe"
if (-not (Test-Path $cloudflaredPath)) {
    $cmd = Get-Command cloudflared -ErrorAction SilentlyContinue
    if ($cmd) {
        $cloudflaredPath = $cmd.Source
    } else {
        Write-Host "[Loi] Khong tim thay cloudflared.exe!" -ForegroundColor Red
        Write-Host "Hay chay: winget install --id Cloudflare.cloudflared" -ForegroundColor Yellow
        exit 1
    }
}

Write-Host "==============================================================================" -ForegroundColor Cyan
Write-Host " DANG KHOI TAO DUONG TRUYEN INTERNET CHO DIEN THOAI QUET MA QR" -ForegroundColor Cyan
Write-Host "==============================================================================" -ForegroundColor Cyan
Write-Host ">>> Luu y: HAY GIU CUA SO NAY MO trong suot qua trinh test tren dien thoai." -ForegroundColor Yellow
Write-Host ">>> Tim dong co dang: https://xxxx.trycloudflare.com de dan vao appsettings.json" -ForegroundColor Green
Write-Host "------------------------------------------------------------------------------" -ForegroundColor DarkGray

& "$cloudflaredPath" tunnel --url http://localhost:5102

