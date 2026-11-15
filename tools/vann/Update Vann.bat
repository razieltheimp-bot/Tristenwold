@echo off
rem Puts in a new game build: put Vann-game.zip next to this file first.
powershell -NoProfile -Command "Start-Process powershell -Verb RunAs -ArgumentList '-NoProfile -ExecutionPolicy Bypass -NoExit -File \"%~dp0stop-start.ps1\" update'"
