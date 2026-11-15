# Keeps Vann's court playing on this server, with no windows. The "Vann's court" scheduled task runs this as SYSTEM
# whenever the machine starts (install.ps1 sets that up).
#   - Starts the court (court\run-vann.ps1), and starts it again a minute after it stops for any reason.
#   - "stop" on Telegram: the court closes and this waits, reading Telegram, for "start" (or "restart").
#   - Keeps Vann's game up to date by itself: every release on GitHub carries his build (Tristinwold-Vann-Windows.zip).
#     Every half hour this looks; when there's a newer one it downloads it, has the court close, swaps the game folder
#     (the old one kept as game.previous) and starts him again. So when the server is updated, Vann follows on his own.
#   - The server moved to a newer version than Vann's game and no new build is out yet: he waits, looking every 10 minutes.
# What happened goes in service.log, next to this script.
$ErrorActionPreference = 'Continue'
$here = $PSScriptRoot
$court = Join-Path $here 'court'
$log = Join-Path $here 'service.log'
$mutex = New-Object System.Threading.Mutex($false, 'Global\VannCourtService')
if (-not $mutex.WaitOne(0)) { exit 0 } # one at a time
. (Join-Path $here 'keys.ps1')

function Note($text) {
    if ((Test-Path $log) -and (Get-Item $log).Length -gt 2MB) { Move-Item $log "$log.old" -Force }
    Add-Content -Path $log -Value ("{0}  {1}" -f (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'), $text)
}

$token = (Read-Vault)['telegram_token']
$offsetFile = Join-Path $court 'telegram-offset.txt'
$chatFile = Join-Path $court 'telegram-chat.txt'
function Say($text) {
    if (-not $token -or -not (Test-Path $chatFile)) { return }
    try { Invoke-RestMethod -Uri "https://api.telegram.org/bot$token/sendMessage" -Method Post -Body @{ chat_id = (Get-Content $chatFile -Raw).Trim(); text = $text } -TimeoutSec 15 | Out-Null } catch { }
}
function Wait-ForStart {
    if (-not $token) { Note 'No Telegram token: starting again in 10 minutes instead.'; Start-Sleep 600; return }
    while ($true) {
        $offset = 0
        if (Test-Path $offsetFile) { [long]::TryParse((Get-Content $offsetFile -Raw).Trim(), [ref]$offset) | Out-Null }
        try { $updates = Invoke-RestMethod -Uri "https://api.telegram.org/bot$token/getUpdates?timeout=25$(if ($offset -gt 0) { "&offset=$offset" })" -TimeoutSec 40 }
        catch { Start-Sleep 15; continue }
        foreach ($u in @($updates.result)) {
            $offset = [Math]::Max($offset, [long]$u.update_id + 1)
            Set-Content -Path $offsetFile -Value $offset -Encoding ascii
            $m = $u.message
            if (-not $m -or -not $m.text -or -not (Test-Path $chatFile) -or "$($m.chat.id)" -ne (Get-Content $chatFile -Raw).Trim()) { continue }
            $word = $m.text.Trim().TrimStart('/').Split(' ', '@')[0].ToLowerInvariant().TrimEnd('.', '!', '?')
            if ($word -in @('start', 'restart', 'reboot')) { Say "Starting Vann's court on the server."; Note 'Start asked for on Telegram.'; return }
            Say "Vann's court is stopped on the server. Send start to bring it back."
        }
    }
}

# ---- Vann's game, kept up to date from the GitHub release ------------------------------------------------------
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
$repo = 'razieltheimp-bot/Tristenwold'; $tag = 'october-31-update'; $asset = 'Tristinwold-Vann-Windows.zip'
$game = Join-Path $here 'game'
$builtFile = Join-Path $here 'game-build.txt'

# The newest build on the release, if it isn't the one in the game folder now (else nothing).
function Find-NewBuild {
    try { $rel = Invoke-RestMethod -Uri "https://api.github.com/repos/$repo/releases/tags/$tag" -Headers @{ 'User-Agent' = 'vann-service' } -TimeoutSec 30 }
    catch { return $null }
    $a = @($rel.assets) | Where-Object { $_.name -eq $asset } | Select-Object -First 1
    if (-not $a) { return $null }
    $stamp = "$($a.id) $($a.updated_at)"
    if ((Test-Path $builtFile) -and (Get-Content $builtFile -Raw).Trim() -eq $stamp) { return $null }
    return @{ Url = $a.browser_download_url; Stamp = $stamp; Size = [long]$a.size }
}

# Downloads a build and puts it in place of the game folder (keeping the old one as game.previous). Only while the court is closed.
function Install-Build($build) {
    $zip = Join-Path $here 'game-download.zip'
    $new = Join-Path $here 'game.new'
    try {
        Note "Downloading Vann's new build ($([math]::Round($build.Size / 1MB)) MB)..."
        Invoke-WebRequest -Uri $build.Url -OutFile $zip -UseBasicParsing -TimeoutSec 3600
        if ((Get-Item $zip).Length -ne $build.Size) { Note 'The download came out the wrong size: keeping the build there is.'; return }
        if (Test-Path $new) { Remove-Item $new -Recurse -Force }
        Expand-Archive -Path $zip -DestinationPath $new -Force
        if (-not (Test-Path (Join-Path $new 'Tristinwold-Vann.exe'))) { Note 'The new build has no game in it: keeping the build there is.'; return }
        if (Test-Path "$game.previous") { Remove-Item "$game.previous" -Recurse -Force }
        if (Test-Path $game) { Rename-Item $game "$game.previous" }
        Rename-Item $new $game
        Set-Content -Path $builtFile -Value $build.Stamp -Encoding ascii
        Note "Vann's game is now the newest build (version $((Get-Content (Join-Path $game 'protocol.txt') -ErrorAction SilentlyContinue))). The old one is in game.previous."
    }
    catch { Note "Couldn't put the new build in ($($_.Exception.Message)): keeping the build there is." }
    finally {
        Remove-Item $zip -Force -ErrorAction SilentlyContinue
        if (Test-Path $new) { Remove-Item $new -Recurse -Force -ErrorAction SilentlyContinue }
    }
}

Note 'The service is up.'
while ($true) {
    $build = Find-NewBuild
    if ($build) { Install-Build $build }
    Note 'Starting the court.'
    $p = Start-Process powershell.exe -ArgumentList @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', "`"$court\run-vann.ps1`"") `
        -WorkingDirectory $court -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $here 'court.out') -RedirectStandardError (Join-Path $here 'court.err')
    # While he plays: every half hour, look for a new build; when there is one, the court is asked to close (as a
    # "restart" on Telegram would), and it's put in before he starts again.
    $nextLook = (Get-Date).AddMinutes(30)
    $updating = $false
    while (-not $p.WaitForExit(60000)) {
        if ($updating -or (Get-Date) -lt $nextLook) { continue }
        $nextLook = (Get-Date).AddMinutes(30)
        if (Find-NewBuild) {
            Note "A new build of Vann's game is out: closing the court to put it in."
            New-Item -ItemType File -Path (Join-Path $court 'restart.flag') -Force | Out-Null
            $updating = $true
        }
    }
    switch ($p.ExitCode) {
        2 { Note 'Stopped on Telegram: waiting for start.'; Wait-ForStart }
        3 { if ($updating) { Note 'The court closed for the update.' } else { Note 'Restart asked for on Telegram.'; Start-Sleep 30 } }
        4 { Note "The server is on a newer version than Vann's game: looking for a new build every 10 minutes."; for ($i = 0; $i -lt 6 -and -not (Find-NewBuild); $i++) { Start-Sleep 600 } }
        default { Note "The court stopped (exit $($p.ExitCode); see court.out): starting it again in a minute."; Start-Sleep 60 }
    }
}
