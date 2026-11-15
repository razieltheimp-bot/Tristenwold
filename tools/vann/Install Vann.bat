@echo off
rem Installs Vann's court (asks for administrator).
powershell -NoProfile -Command "Start-Process powershell -Verb RunAs -ArgumentList '-NoProfile -ExecutionPolicy Bypass -NoExit -File \"%~dp0install.ps1\"'"
