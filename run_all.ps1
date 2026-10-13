# ==============================================================================
# SCRIPT KHỞI ĐỘNG HỆ THỐNG TIỆC CƯỚI RIVERSIDE PALACE
# Bao gồm: AI Service (Port 8000), Backend API (Port 7101), Frontend Web (Port 7102)
# ==============================================================================

Write-Host ">>> [1/4] Dang don dep cac tien trinh dotnet cu..." -ForegroundColor Cyan
Stop-Process -Name dotnet -ErrorAction SilentlyContinue

Write-Host ">>> [2/4] Khoi dong AI Microservice (FastAPI - Port 8000)..." -ForegroundColor Yellow
Start-Process powershell -ArgumentList "-NoExit", "-Command", "cd AI_Service; Write-Host '=== AI MICROSERVICE (PORT 8000) ===' -ForegroundColor Yellow; python -m uvicorn app.main:app --host 127.0.0.1 --port 8000 --reload"

Start-Sleep -Seconds 2

Write-Host ">>> [3/4] Khoi dong Backend API (ASP.NET Core - Port 7101)..." -ForegroundColor Green
Start-Process powershell -ArgumentList "-NoExit", "-Command", "cd Backend/HeThongDatTiecCuoi_API; Write-Host '=== BACKEND API (PORT 7101) ===' -ForegroundColor Green; dotnet run"

Start-Sleep -Seconds 3

Write-Host ">>> [4/4] Khoi dong Frontend Web (ASP.NET Core MVC - Port 7102)..." -ForegroundColor Magenta
Start-Process powershell -ArgumentList "-NoExit", "-Command", "cd Frontend/HeThongDatTiecCuoi_WEB; Write-Host '=== FRONTEND WEB (PORT 7102) ===' -ForegroundColor Magenta; dotnet run"

Write-Host "==============================================================================" -ForegroundColor Cyan
Write-Host " Toan bo 3 service dang duoc khoi dong tren cac cua so rieng biet!" -ForegroundColor Cyan
Write-Host " - Swagger AI:       http://127.0.0.1:8000/docs" -ForegroundColor Yellow
Write-Host " - Swagger Backend:  https://localhost:7101/swagger" -ForegroundColor Green
Write-Host " - Trang chu Web:    https://localhost:7102" -ForegroundColor Magenta
Write-Host "==============================================================================" -ForegroundColor Cyan