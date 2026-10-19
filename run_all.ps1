# ==============================================================================
# SCRIPT KHỞI ĐỘNG HỆ THỐNG TIỆC CƯỚI RIVERSIDE PALACE
# Bao gồm: AI Service (Port 8000), Backend API (Port 7101), Frontend Web (Port 7102)
# ==============================================================================

$projectRoot = $PSScriptRoot
$aiDirectory = Join-Path $projectRoot "AI_Service"
$backendDirectory = Join-Path $projectRoot "Backend\HeThongDatTiecCuoi_API"
$frontendDirectory = Join-Path $projectRoot "Frontend\HeThongDatTiecCuoi_WEB"
$aiPython = Join-Path $aiDirectory ".venv\Scripts\python.exe"

if (-not (Test-Path -LiteralPath $aiPython)) {
    Write-Host "[Loi] Chua co moi truong Python tai AI_Service\.venv." -ForegroundColor Red
    Write-Host "Chay: py -3.10 -m venv AI_Service\.venv" -ForegroundColor Yellow
    Write-Host "Sau do: AI_Service\.venv\Scripts\python.exe -m pip install -r AI_Service\requirements.txt" -ForegroundColor Yellow
    exit 1
}

Write-Host ">>> [1/4] Dang don dep cac tien trinh dotnet cu..." -ForegroundColor Cyan
Stop-Process -Name dotnet -ErrorAction SilentlyContinue

Write-Host ">>> [2/4] Khoi dong AI Microservice (FastAPI - Port 8000)..." -ForegroundColor Yellow
if (-not (Test-Path -LiteralPath (Join-Path $aiDirectory ".env"))) {
    Write-Host "    [Canh bao] Chua co AI_Service\.env -> chatbot chay offline (rule-based), khong dung LLM." -ForegroundColor Yellow
    Write-Host "    Tao tu mau: Copy-Item AI_Service\.env.example AI_Service\.env roi dien OPENAI_API_KEY/GEMINI_API_KEY." -ForegroundColor DarkYellow
}
Start-Process powershell -WorkingDirectory $aiDirectory -ArgumentList "-NoExit", "-Command", "Write-Host '=== AI MICROSERVICE (PORT 8000) ===' -ForegroundColor Yellow; & '$aiPython' -m uvicorn app.main:app --host 127.0.0.1 --port 8000 --reload"

Start-Sleep -Seconds 2

Write-Host ">>> [3/4] Khoi dong Backend API (ASP.NET Core - Port 7101)..." -ForegroundColor Green
Start-Process powershell -WorkingDirectory $backendDirectory -ArgumentList "-NoExit", "-Command", "Write-Host '=== BACKEND API (PORT 7101) ===' -ForegroundColor Green; dotnet run"

Start-Sleep -Seconds 3

Write-Host ">>> [4/4] Khoi dong Frontend Web (ASP.NET Core MVC - Port 7102)..." -ForegroundColor Magenta
Start-Process powershell -WorkingDirectory $frontendDirectory -ArgumentList "-NoExit", "-Command", "Write-Host '=== FRONTEND WEB (PORT 7102) ===' -ForegroundColor Magenta; dotnet run"

Write-Host "==============================================================================" -ForegroundColor Cyan
Write-Host " Toan bo 3 service dang duoc khoi dong tren cac cua so rieng biet!" -ForegroundColor Cyan
Write-Host " - Swagger AI:       http://127.0.0.1:8000/docs" -ForegroundColor Yellow
Write-Host " - Swagger Backend:  https://localhost:7101/swagger" -ForegroundColor Green
Write-Host " - Trang chu Web:    https://localhost:7102" -ForegroundColor Magenta
Write-Host "==============================================================================" -ForegroundColor Cyan
