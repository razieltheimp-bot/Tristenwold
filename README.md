# Tristinwold server for Linux

The Tristinwold shared-world server, built for 64-bit Linux, with a script that installs it on a cloud
machine as a service. This branch holds only the server; the game and its source are on `main`.

On a fresh Ubuntu or Debian cloud machine (allow TCP ports 7777 and 7778 in the provider's firewall):

```
git clone --branch linux-server --depth 1 https://github.com/razieltheimp-bot/Tristenwold.git tristinwold-server
cd tristinwold-server
sudo sh setup.sh --password <a password for your friends>
```

The repository is private, so `git clone` asks for a username and password: give your GitHub username, and
for the password a personal access token (GitHub, Settings, Developer settings, Fine-grained tokens: give it
this repository only, with read access to Contents). Or skip git and copy the files up with `scp` instead.

It prints the address for the game's Server box (`<address>:7777`) and for Android phones
(`http://<address>:7778`). The server starts with the machine, restarts if it falls over, and saves the
world when it's stopped. The world lives in `/opt/tristinwold/tristinwold.json`; settings (the password,
and keys for Claude's voice and Telegram) in `/etc/tristinwold.env`.

To update: `git pull` in this folder, then `sudo sh setup.sh` again (the world is kept).

Built from `main` at the commit named in this branch's latest commit message.
