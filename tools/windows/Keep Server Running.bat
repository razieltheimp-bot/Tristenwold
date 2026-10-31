@echo off
title Tristinwold server watchdog
rem Starts the server and keeps it running. Close this window first if you want the server to stay stopped.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0server-watchdog.ps1"
pause
