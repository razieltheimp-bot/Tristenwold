@echo off
rem Starts Vann's court again.
powershell -NoProfile -Command "Start-Process powershell -Verb RunAs -ArgumentList '-NoProfile -ExecutionPolicy Bypass -File \"%~dp0stop-start.ps1\" start'"
