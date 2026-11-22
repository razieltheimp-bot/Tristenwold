# Gets ffmpeg (free) so Vann's ad clips are saved as phone-friendly .mp4s and sent to you on Telegram.
# Without it the clips still save, as .avi files. Run "Install Clips.bat" once; restart the court after.
$ErrorActionPreference = 'Stop'
$links = Join-Path $env:LOCALAPPDATA 'Microsoft\WinGet\Links\ffmpeg.exe'
if ((Get-Command ffmpeg -ErrorAction SilentlyContinue) -or (Test-Path $links)) { Write-Host "ffmpeg is already on this PC. Nothing to do."; exit 0 }
if (-not (Get-Command winget -ErrorAction SilentlyContinue)) {
    Write-Host "winget isn't on this PC. Install 'App Installer' from the Microsoft Store, then run this again."
    exit 1
}
Write-Host "Installing ffmpeg with winget (a minute or two)..."
winget install --id Gyan.FFmpeg -e --accept-source-agreements --accept-package-agreements
if ($LASTEXITCODE -ne 0) { Write-Host "The install didn't finish (see above)."; exit 1 }
Write-Host ""
Write-Host "Done. Restart the court (send restart on Telegram) and the next clips come out as .mp4."
