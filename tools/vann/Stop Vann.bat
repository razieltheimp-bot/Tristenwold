@echo off
rem Stops Vann's court until Start Vann.bat (or a restart of the machine).
powershell -NoProfile -Command "Start-Process powershell -Verb RunAs -ArgumentList '-NoProfile -ExecutionPolicy Bypass -File \"%~dp0stop-start.ps1\" stop'"
