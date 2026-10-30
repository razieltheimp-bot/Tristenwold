@echo off
rem Joins the downloaded pieces back into the one game zip. Put every Tristinwold-Windows-Players.zip.part* file in this folder first.
cd /d "%~dp0"
if not exist "Tristinwold-Windows-Players.zip.part0" (echo The .part0 to .part3 files must be in this folder. & pause & exit /b 1)
copy /b "Tristinwold-Windows-Players.zip.part0"+"Tristinwold-Windows-Players.zip.part1"+"Tristinwold-Windows-Players.zip.part2"+"Tristinwold-Windows-Players.zip.part3" "Tristinwold-Windows-Players.zip" >nul
if errorlevel 1 (echo Something went wrong joining the pieces. & pause & exit /b 1)
del "Tristinwold-Windows-Players.zip.part0" "Tristinwold-Windows-Players.zip.part1" "Tristinwold-Windows-Players.zip.part2" "Tristinwold-Windows-Players.zip.part3"
echo Done: Tristinwold-Windows-Players.zip is ready. Right-click it and Extract All.
pause
