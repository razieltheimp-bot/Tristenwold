# The Rings of Tristinwold: downloads (October 31 update)

GitHub keeps files under 100 MB, so the big downloads are in pieces. Download every piece of what you
want into one folder (click a file, then the **Download raw file** button, the arrow icon at the top
right of the file view), then double-click the joiner, which makes the one file and deletes the pieces.

## A player's PC
1. `Tristinwold-Windows-Players.zip.part0`, `.part1`, `.part2`, `.part3` and `Join the game zip.bat`.
2. Run `Join the game zip.bat`. It makes `Tristinwold-Windows-Players.zip`.
3. Right-click the zip, Extract All, then run `Tristinwold.exe` in the folder it made.
4. In the game's Server box, paste the address the host gives you, and use the host's server password.

## The host's PC (the one that runs the server)
1. The same four pieces and `Join the game zip.bat`, plus `Tristinwold-Host-Extras.zip` and `Tristinwold-Server-Windows.zip`.
2. Run the joiner and extract the game zip as above.
3. Extract `Tristinwold-Host-Extras.zip` into that same folder (the host's batch files and how-to; say yes to replacing HOW-TO-PLAY.txt).
4. Extract `Tristinwold-Server-Windows.zip` into that same folder too, so its `server` folder sits beside `Start Server.bat`.
5. Run `Keep Server Running.bat` and leave its window open. It restarts the server if it stops, and writes the address for players away from home to `Outside Link.txt`.

## Android
The Android files here are from the last update. They can't join the shared world until a new app is built, because the server turns older versions away.

## A Linux or cloud server
`Tristinwold-Server-Linux-x64.tar.gz` for AMD or Intel machines, `Tristinwold-Server-Linux-arm64.tar.gz` for Arm machines.

Everyone, and the server, must be on this update: an older game is turned away with a version message.
