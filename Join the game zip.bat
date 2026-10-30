@echo off
setlocal enabledelayedexpansion
rem Joins the downloaded pieces back into Tristinwold-Windows-Players.zip. Copes with accidental second downloads ("name (1).partN"): the first copy is used, the rest are deleted.
cd /d "%~dp0"
if not exist "Tristinwold-Windows-Players.zip.part0" for %%F in ("Tristinwold-Windows-Players (*).zip.part0" "Tristinwold-Windows-Players.zip (*).part0") do if not exist "Tristinwold-Windows-Players.zip.part0" ren "%%~F" "Tristinwold-Windows-Players.zip.part0"
for %%F in ("Tristinwold-Windows-Players (*).zip.part0" "Tristinwold-Windows-Players.zip (*).part0") do del "%%~F" 2>nul
if not exist "Tristinwold-Windows-Players.zip.part1" for %%F in ("Tristinwold-Windows-Players (*).zip.part1" "Tristinwold-Windows-Players.zip (*).part1") do if not exist "Tristinwold-Windows-Players.zip.part1" ren "%%~F" "Tristinwold-Windows-Players.zip.part1"
for %%F in ("Tristinwold-Windows-Players (*).zip.part1" "Tristinwold-Windows-Players.zip (*).part1") do del "%%~F" 2>nul
if not exist "Tristinwold-Windows-Players.zip.part2" for %%F in ("Tristinwold-Windows-Players (*).zip.part2" "Tristinwold-Windows-Players.zip (*).part2") do if not exist "Tristinwold-Windows-Players.zip.part2" ren "%%~F" "Tristinwold-Windows-Players.zip.part2"
for %%F in ("Tristinwold-Windows-Players (*).zip.part2" "Tristinwold-Windows-Players.zip (*).part2") do del "%%~F" 2>nul
if not exist "Tristinwold-Windows-Players.zip.part3" for %%F in ("Tristinwold-Windows-Players (*).zip.part3" "Tristinwold-Windows-Players.zip (*).part3") do if not exist "Tristinwold-Windows-Players.zip.part3" ren "%%~F" "Tristinwold-Windows-Players.zip.part3"
for %%F in ("Tristinwold-Windows-Players (*).zip.part3" "Tristinwold-Windows-Players.zip (*).part3") do del "%%~F" 2>nul
set MISSING=
if not exist "Tristinwold-Windows-Players.zip.part0" set MISSING=1 if not exist "Tristinwold-Windows-Players.zip.part1" set MISSING=1 if not exist "Tristinwold-Windows-Players.zip.part2" set MISSING=1 if not exist "Tristinwold-Windows-Players.zip.part3" set MISSING=1
if defined MISSING (echo A piece is missing: the .part0 to .part3 files must all be in this folder. & pause & exit /b 1)
if exist "Tristinwold-Windows-Players.zip" del "Tristinwold-Windows-Players.zip"
copy /b "Tristinwold-Windows-Players.zip.part0"+"Tristinwold-Windows-Players.zip.part1"+"Tristinwold-Windows-Players.zip.part2"+"Tristinwold-Windows-Players.zip.part3" "Tristinwold-Windows-Players.zip" >nul
if errorlevel 1 (echo Something went wrong joining the pieces. & pause & exit /b 1)
del "Tristinwold-Windows-Players.zip.part0"
del "Tristinwold-Windows-Players.zip.part1"
del "Tristinwold-Windows-Players.zip.part2"
del "Tristinwold-Windows-Players.zip.part3"
for %%F in ("Tristinwold-Windows-Players (*).zip") do del "%%~F" 2>nul
echo Done: Tristinwold-Windows-Players.zip is ready. Right-click it and Extract All.
pause
