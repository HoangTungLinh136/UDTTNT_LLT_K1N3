HRMS + AVA - HUONG DAN CHAY TREN MAY MOI
========================================

YEU CAU CAN CAI:
- Python 3.11 x64
- .NET 9 SDK
- SQL Server Express
- SQL Server Management Studio
- Ollama
- FFmpeg co ho tro filter whisper

========================================
1. RESTORE DATABASE
========================================

Mo SQL Server Management Studio.

Restore file:

database\HRManagementDB_RAG.bak

Ten database phai la:

HRManagementDB_RAG

SQL Server instance mac dinh:

.\SQLEXPRESS


========================================
2. CAI OLLAMA MODEL
========================================

Mo PowerShell:

ollama pull qwen3:4b-instruct
ollama pull qwen2.5vl:3b

Kiem tra:

ollama list

Phai thay qwen3:4b-instruct dung luong khoang 2.5 GB va qwen2.5vl:3b khoang 3.2 GB.
Neu ca hai tag chi hien 397 MB/cung mot ID thi do la model text 0.5B bi gan nhan sai.


========================================
3. SETUP PROJECT LAN DAU
========================================

Mo PowerShell tai thu muc HRMS_AVA_RELEASE.

Chay:

powershell -ExecutionPolicy Bypass -File ".\setup-first-run.ps1"

Script se:
- tao Python .venv
- pip install requirements.txt
- tao .env
- sinh AVA_INTERNAL_SECRET local
- dotnet restore
- dotnet build
- kiem tra Ollama / FFmpeg


========================================
4. CHAY HE THONG
========================================

Chay:

powershell -ExecutionPolicy Bypass -File ".\giao diện demo\run-all.ps1"

Neu PowerShell dang mo tai thu muc ben ngoai:

D:\Download\giao diện demo

Thi co the build va chay rieng HRMS bang lenh on dinh sau (script tu tim
dung ASP.NET project, khong can dem cac thu muc trung ten):

powershell -ExecutionPolicy Bypass -File ".\run-web.ps1"

Neu HRMS da chay tren cong 5249, script se bao trang thai thanh cong va khong
mo them instance trung. Muon build lai va khoi dong lai dung project nay:

powershell -ExecutionPolicy Bypass -File ".\run-web.ps1" -Restart

Hoac dung dotnet truc tiep voi day du duong dan:

dotnet run --project ".\giao diện demo\giao diện demo\giao diện demo\giao diện demo.csproj"

He thong su dung:

AVA:
http://127.0.0.1:8001

HRMS:
http://127.0.0.1:5249


========================================
5. NEU KHONG CHAY
========================================

Kiem tra log tai:

data\ava-final.stderr.log
data\web-final.stderr.log

Kiem tra port:

Test-NetConnection 127.0.0.1 -Port 8001
Test-NetConnection 127.0.0.1 -Port 5249


========================================
LUU Y
========================================

- Khong copy .venv tu may khac.
- Phai dung Python 3.11.
- Project ASP.NET dung .NET 9.
- Ollama phai dang chay.
- Database phai co ten HRManagementDB_RAG.
- Khong chia se file .env sau khi setup.
