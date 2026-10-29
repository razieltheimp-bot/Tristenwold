#!/bin/sh
# Installs the Tristinwold server on a cloud machine (Ubuntu or Debian) as a service that starts on boot,
# restarts if it falls over, and saves the world when the machine stops it. Run it as root, from the folder
# the Linux server download was unpacked into (the Tristinwold.Server program sits next to this script):
#
#   sudo sh setup.sh                      # no password: anyone with the address can join
#   sudo sh setup.sh --password secret    # only friends who know the password
#
# Running it again updates the program and keeps the world. Settings live in /etc/tristinwold.env.
set -eu

here=$(cd "$(dirname "$0")" && pwd)
home=/opt/tristinwold
password=""
[ "${1:-}" = "--password" ] && password="${2:-}"

if [ ! -f "$here/Tristinwold.Server" ]; then
    echo "Tristinwold.Server isn't next to this script. Unpack the Linux server download and run setup.sh from there." >&2
    exit 1
fi

id tristinwold >/dev/null 2>&1 || useradd --system --home-dir "$home" --shell /usr/sbin/nologin tristinwold
mkdir -p "$home"
systemctl stop tristinwold 2>/dev/null || true
install -m 755 "$here/Tristinwold.Server" "$home/Tristinwold.Server"
chown -R tristinwold:tristinwold "$home"

# Settings: written once, then yours to edit (the password, and the keys for Claude's voice and Telegram).
if [ ! -f /etc/tristinwold.env ]; then
    cat > /etc/tristinwold.env <<SETTINGS
# Tristinwold server settings. After changing them: sudo systemctl restart tristinwold
TRISTINWOLD_PASSWORD=$password
# ANTHROPIC_API_KEY=        (Claude voices Vann)
# TELEGRAM_BOT_TOKEN=       (the server posts who joins and leaves to a Telegram chat)
# TELEGRAM_CHAT_ID=
SETTINGS
    chmod 600 /etc/tristinwold.env
elif [ -n "$password" ]; then
    sed -i "s/^TRISTINWOLD_PASSWORD=.*/TRISTINWOLD_PASSWORD=$password/" /etc/tristinwold.env
fi

install -m 644 "$here/tristinwold.service" /etc/systemd/system/tristinwold.service
systemctl daemon-reload
systemctl enable --now tristinwold

# Open the game's ports, if this machine has a firewall of its own (the cloud provider's may need them too).
if command -v ufw >/dev/null 2>&1 && ufw status | grep -q active; then
    ufw allow 7777/tcp >/dev/null
    ufw allow 7778/tcp >/dev/null
fi

address=$(curl -fsS --max-time 5 https://api.ipify.org 2>/dev/null || hostname -I | cut -d' ' -f1)
echo
echo "The Tristinwold server is running, and will start again whenever this machine does."
echo "  In the game's Server box, type:  $address:7777"
echo "  On an Android phone:             http://$address:7778"
echo "  Its log:     sudo journalctl -u tristinwold -f"
echo "  Restart it:  sudo systemctl restart tristinwold"
echo "  The world:   $home/tristinwold.json (copy it to back it up)"
