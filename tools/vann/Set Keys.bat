@echo off
rem Changes Vann's keys (asks for administrator).
powershell -NoProfile -Command "Start-Process powershell -Verb RunAs -ArgumentList '-NoProfile -ExecutionPolicy Bypass -NoExit -File \"%~dp0set-keys.ps1\"'"
