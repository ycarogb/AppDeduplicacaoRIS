@echo off
cd /d "%~dp0"
echo Abra http://127.0.0.1:5080 quando o servidor estiver pronto.
dotnet run --project DeduplicacaoCSharp.csproj
pause
