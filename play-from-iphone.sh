#!/bin/sh
# Plays Tristinwold on this computer and lets your iPhone (or any phone) play it in Safari, from anywhere.
# The phone link is printed below, shown on screen, and sent to Telegram if TELEGRAM_BOT_TOKEN and
# TELEGRAM_CHAT_ID are set. Needs Godot 4.7 .NET (set GODOT to its path if it isn't "godot") and cloudflared.
exec "${GODOT:-godot}" --path "$(dirname "$0")/game" -- --remote --tunnel "$@"
