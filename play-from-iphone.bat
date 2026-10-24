@echo off
rem Plays Tristinwold on this PC and lets your iPhone (or any phone) play it in Safari, from anywhere.
rem The phone link is printed below, shown on screen, and sent to Telegram if TELEGRAM_BOT_TOKEN and
rem TELEGRAM_CHAT_ID are set. Needs Godot 4.7 .NET (set GODOT to its path if it isn't "godot") and cloudflared.
if "%GODOT%"=="" set GODOT=godot
"%GODOT%" --path "%~dp0game" -- --remote --tunnel %*
