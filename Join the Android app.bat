@echo off
rem Joins the downloaded pieces back into the Android app.
cd /d "%~dp0"
if not exist "Tristinwold-Android.apk.part0" (echo The .part0 to .part2 files must be in this folder. & pause & exit /b 1)
copy /b "Tristinwold-Android.apk.part0"+"Tristinwold-Android.apk.part1"+"Tristinwold-Android.apk.part2" "Tristinwold-Android.apk" >nul
if errorlevel 1 (echo Something went wrong joining the pieces. & pause & exit /b 1)
del "Tristinwold-Android.apk.part0" "Tristinwold-Android.apk.part1" "Tristinwold-Android.apk.part2"
echo Done: Tristinwold-Android.apk is ready. Copy it to the phone or tablet and open it there.
pause
