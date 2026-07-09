@echo off
setlocal

set "DINOAI_ROOT=%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%DINOAI_ROOT%dino.ps1" %*

endlocal
