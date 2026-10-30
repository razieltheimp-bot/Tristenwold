@echo off
setlocal enabledelayedexpansion
rem Joins the downloaded pieces back into Tristinwold-Android.apk. Copes with accidental second downloads ("name (1).partN"): the first copy is used, the rest are deleted.
cd /d "%~dp0"
if not exist "Tristinwold-Android.apk.part0" for %%F in ("Tristinwold-Android (*).apk.part0" "Tristinwold-Android.apk (*).part0") do if not exist "Tristinwold-Android.apk.part0" ren "%%~F" "Tristinwold-Android.apk.part0"
for %%F in ("Tristinwold-Android (*).apk.part0" "Tristinwold-Android.apk (*).part0") do del "%%~F" 2>nul
if not exist "Tristinwold-Android.apk.part1" for %%F in ("Tristinwold-Android (*).apk.part1" "Tristinwold-Android.apk (*).part1") do if not exist "Tristinwold-Android.apk.part1" ren "%%~F" "Tristinwold-Android.apk.part1"
for %%F in ("Tristinwold-Android (*).apk.part1" "Tristinwold-Android.apk (*).part1") do del "%%~F" 2>nul
if not exist "Tristinwold-Android.apk.part2" for %%F in ("Tristinwold-Android (*).apk.part2" "Tristinwold-Android.apk (*).part2") do if not exist "Tristinwold-Android.apk.part2" ren "%%~F" "Tristinwold-Android.apk.part2"
for %%F in ("Tristinwold-Android (*).apk.part2" "Tristinwold-Android.apk (*).part2") do del "%%~F" 2>nul
set MISSING=
if not exist "Tristinwold-Android.apk.part0" set MISSING=1 if not exist "Tristinwold-Android.apk.part1" set MISSING=1 if not exist "Tristinwold-Android.apk.part2" set MISSING=1
if defined MISSING (echo A piece is missing: the .part0 to .part2 files must all be in this folder. & pause & exit /b 1)
if exist "Tristinwold-Android.apk" del "Tristinwold-Android.apk"
copy /b "Tristinwold-Android.apk.part0"+"Tristinwold-Android.apk.part1"+"Tristinwold-Android.apk.part2" "Tristinwold-Android.apk" >nul
if errorlevel 1 (echo Something went wrong joining the pieces. & pause & exit /b 1)
del "Tristinwold-Android.apk.part0"
del "Tristinwold-Android.apk.part1"
del "Tristinwold-Android.apk.part2"
for %%F in ("Tristinwold-Android (*).apk") do del "%%~F" 2>nul
echo Done: Tristinwold-Android.apk is ready. Copy it to the phone or tablet and open it there.
pause
