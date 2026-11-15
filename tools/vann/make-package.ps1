# Builds Vann's court for the server, on the owner's PC: local-claude\Vann-Server.zip (everything, for the first
# install) and local-claude\Vann-game.zip (just the game, for Update Vann.bat after a code change).
#   powershell -ExecutionPolicy Bypass -File local-claude\vann-server-kit\make-package.ps1 [-From <checkout>] [-GameOnly]
# -SkipGame: keep the game already in vann-server\game (only the court, launcher, keys and kit are redone).
# -From: the checkout to build the game from (default: the council-work worktree beside source). Its code, plus the
# local-only bot (ClaudePlayer.cs, Directory.Build.props from source), is exported as a Windows game, headless-ready.
# The court's state is copied from local-claude\vann as it is now. The keys are sealed into keys.sealed with a new
# passphrase, written to local-claude\vann-server-passphrase.txt: give it to whoever installs, separately from the zip.
param([string]$From = '', [switch]$GameOnly, [switch]$SkipGame)
$ErrorActionPreference = 'Stop'
$kit = $PSScriptRoot
$local = Split-Path -Parent $kit
$source = Split-Path -Parent $local
if (-not $From) { $From = Join-Path (Split-Path -Parent $source) 'council-work' }
$godot = 'C:\Users\Admin\Tools\Godot\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe'
$tree = Join-Path $local 'vann-build'     # the game's code with the bot in it (never a git checkout)
$pkg = Join-Path $local 'vann-server'     # the package, before zipping
. (Join-Path $kit 'keys.ps1')
function Step($t) { Write-Host ("{0}  {1}" -f (Get-Date -Format 'HH:mm:ss'), $t) }

$out = Join-Path $pkg 'game'
$gameZip = Join-Path $local 'Vann-game.zip'
if (-not ($SkipGame -and (Test-Path (Join-Path $out 'Tristinwold-Vann.pck')))) {
# 1. The game, with the bot compiled in.
Step "Copying the game from $From (with the bot from source)..."
& robocopy $From $tree /MIR /XD .git build bin obj temp /XF *.log /NFL /NDL /NJH /NJS /NP | Out-Null
if ($LASTEXITCODE -ge 8) { throw "robocopy failed ($LASTEXITCODE)" }
Copy-Item "$source\game\scripts\ClaudePlayer.cs" "$tree\game\scripts\ClaudePlayer.cs" -Force
if (Test-Path "$source\game\scripts\ClaudePlayer.cs.uid") { Copy-Item "$source\game\scripts\ClaudePlayer.cs.uid" "$tree\game\scripts\" -Force }
Copy-Item "$source\game\Directory.Build.props" "$tree\game\Directory.Build.props" -Force
$out = Join-Path $pkg 'game'
if (Test-Path $out) { Remove-Item $out -Recurse -Force }
New-Item -ItemType Directory -Force -Path $out | Out-Null

Step 'Exporting the game (Windows, release) with the bot in it...'
$env:TRISTINWOLD_CLAUDE = '1'
$exe = Join-Path $out 'Tristinwold-Vann.exe'
$p = Start-Process -FilePath $godot -ArgumentList @('--headless', '--path', "`"$tree\game`"", '--export-release', '"Windows Desktop"', "`"$exe`"") -PassThru -NoNewWindow `
    -RedirectStandardOutput "$local\vann-export.out" -RedirectStandardError "$local\vann-export.log"
# Godot writes the export in a minute or so and can then hang at shutdown: done once the .pck stops growing.
$pck = Join-Path $out 'Tristinwold-Vann.pck'
$last = -1; $still = 0
for ($t = 0; $t -lt 10800 -and -not $p.HasExited; $t += 10) {
    Start-Sleep 10
    $size = if (Test-Path $pck) { (Get-Item $pck).Length } else { -1 }
    if ($size -gt 0 -and $size -eq $last) { $still++ } else { $still = 0 }
    $last = $size
    if ($still -ge 6) { break }
}
if (-not $p.HasExited) { Stop-Process -Id $p.Id -Force }
Remove-Item Env:TRISTINWOLD_CLAUDE
if (-not (Test-Path $exe) -or -not (Test-Path $pck)) { throw "The export didn't finish: see $local\vann-export.log" }
$dll = Get-ChildItem $out -Recurse -Filter 'Tristinwold.Game.dll' | Select-Object -First 1
if (-not $dll -or -not (Select-String -Path $dll.FullName -Pattern 'VANN_TELEGRAM_TOKEN' -Encoding Unicode -Quiet)) { throw "The exported game doesn't have the bot in it (was TRISTINWOLD_CLAUDE seen?)." }
if ((Get-Content "$tree\src\Tristinwold.Shared\Protocol.cs" -Raw) -match 'ProtocolVersion = (\d+)') { Set-Content (Join-Path $out 'protocol.txt') $matches[1] -Encoding ascii }
Step "Game exported: protocol $(Get-Content (Join-Path $out 'protocol.txt')), bot inside."
$gameZip = Join-Path $local 'Vann-game.zip'
if (Test-Path $gameZip) { Remove-Item $gameZip -Force }
Compress-Archive -Path "$out\*" -DestinationPath $gameZip
}
else { Step "Keeping the game already in $out." }
if ($GameOnly) { Step "Done: $gameZip (Update Vann.bat on the server)."; exit 0 }

# 2. The court, as it is on this PC, without anything secret in plain text.
Step "Copying the court's state..."
$vann = Join-Path $local 'vann'
$court = Join-Path $pkg 'court'
& robocopy $vann $court /MIR /XD stream voice /XF telegram-token.txt build.log last-run.log last-run.out restart.flag stop.flag run-vann.ps1 vann-watch.ps1 install-watcher.ps1 'Install Watcher.bat' /NFL /NDL /NJH /NJS /NP | Out-Null
if ($LASTEXITCODE -ge 8) { throw "robocopy failed ($LASTEXITCODE)" }
function Read-Settings($file) { $s = @{}; foreach ($l in Get-Content $file -ErrorAction SilentlyContinue) { if ($l -match '^\s*([a-z_]+)\s*=\s*(.*?)\s*$') { $s[$matches[1]] = $matches[2] } }; $s }
$vs = Read-Settings (Join-Path $vann 'vann-settings.txt')
$ash = Read-Settings (Join-Path $local 'settings.txt')
$server = if ($vs.server) { $vs.server } else { $ash.server }
$keys = @{
    gemini_key = $vs.gemini_key; top_key = $vs.top_key; password = $vs.password
    server_password = $(if ($vs.server_password) { $vs.server_password } else { $ash.server_password })
    telegram_token = $(if ($vs.telegram_token) { $vs.telegram_token } elseif (Test-Path "$vann\telegram-token.txt") { (Get-Content "$vann\telegram-token.txt" -Raw).Trim() } else { '' })
}
$secret = '^\s*(gemini_key|top_key|password|server_password|telegram_token)\s*='
$lines = foreach ($l in Get-Content (Join-Path $vann 'vann-settings.txt')) {
    if ($l -match $secret) { '# (on the server this one is kept in keys.dat: Set Keys.bat)'; "$($matches[1]) =" }
    elseif ($l -match '^\s*server\s*=') { "server = $server" }
    elseif ($l -match '^\s*(window|window_others)\s*=') { "$($matches[1]) = no" }
    else { $l }
}
$lines += '', '# Where Ollama is on the server (install.ps1 fills it in).', 'ollama_path ='
Set-Content -Path (Join-Path $court 'vann-settings.txt') -Value $lines -Encoding UTF8

# 3. The launcher, changed for the server: the exported game, no windows, keys from keys.dat, passwords off the
# command line, no building or git, and exit codes the service understands.
Step 'Writing the server launcher...'
$text = (Get-Content (Join-Path $vann 'run-vann.ps1') -Raw) -replace "`r`n", "`n"
function Swap([string]$old, [string]$new, [int]$times = 1) {
    $old = $old -replace "`r`n", "`n"; $new = $new -replace "`r`n", "`n"
    $n = ([regex]::Matches($script:text, [regex]::Escape($old))).Count
    if ($n -ne $times) { throw "run-vann.ps1 has changed: expected $times of [$old], found $n" }
    $script:text = $script:text.Replace($old, $new)
}
function SwapRange([string]$pattern, [string]$new) {
    $m = [regex]::Match($script:text, $pattern)
    if (-not $m.Success) { throw "run-vann.ps1 has changed: no match for [$pattern]" }
    $script:text = $script:text.Remove($m.Index, $m.Length).Insert($m.Index, ($new -replace "`r`n", "`n"))
}
Swap @'
$local = Split-Path -Parent $here
$source = Split-Path -Parent $local
$godot = 'C:\Users\Admin\Tools\Godot\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe'
'@ @'
$pkg = Split-Path -Parent $here
$local = $pkg
$source = $pkg
$godot = Join-Path $pkg 'game\Tristinwold-Vann.exe' # the game with the bot built in: on the server it always plays with no window
. (Join-Path $pkg 'keys.ps1')
$vault = Read-Vault
'@
Swap @'
if (-not $settings.server_password) { $settings.server_password = $ashgrip.server_password }
'@ @'
if (-not $settings.server_password) { $settings.server_password = $ashgrip.server_password }
# On the server the keys come from this machine's encrypted store (keys.dat), never from a file in plain text.
foreach ($k in @('gemini_key', 'top_key', 'password', 'server_password', 'telegram_token')) { if ($vault[$k]) { $settings[$k] = $vault[$k] } }
$settings.window = 'no'; $settings.window_others = 'no'
# The court's passwords reach its games in the environment, never on a command line others on this machine could read.
$env:TRISTINWOLD_SECRET = $settings.password
if ($settings.server_password) { $env:TRISTINWOLD_SERVER_PASSWORD = $settings.server_password }
'@
SwapRange '(?s)    Write-Host \("\{0\}  Restart asked for on Telegram: a fresh launcher.*?\n    exit 0\n' @'
    Write-Host ("{0}  Restart asked for on Telegram: the service starts the court again." -f (Get-Date -Format 'HH:mm'))
    foreach ($pp in @(@($procs.Values) + @($proc))) { if ($pp -and -not $pp.HasExited) { Stop-Process -Id $pp.Id -Force -ErrorAction SilentlyContinue } }
    exit 3

'@
SwapRange '(?s)function Get-BotVersion.*?(?=\n\$botExe = )' @'
function Get-BotVersion { [int](Get-Content (Join-Path $pkg 'game\protocol.txt') -Raw).Trim() }
$speak = $null
if (-not $Offline) {
    $serverVersion = Get-ServerVersion $settings.server
    $botVersion = Get-BotVersion
    if ($serverVersion -and $serverVersion -gt $botVersion) {
        Write-Host "The server is on version $serverVersion and Vann's game here on ${botVersion}: he waits for a new build."
        Tell "The server moved to version $serverVersion; Vann's game on the server is on $botVersion and needs a new build (Update Vann.bat). He waits till then."
        exit 4
    }
    if ($serverVersion -and $serverVersion -lt $botVersion) { Write-Host "The server is on $serverVersion, the game on ${botVersion}: the court speaks $serverVersion."; $speak = $serverVersion }
    elseif ($serverVersion) { Write-Host "Server and game are both on version $serverVersion." }
}
'@
SwapRange '(?s)Write-Host "Building the game with the bot and Vann''s court in it\.\.\."\n.*?\nRemove-Item Env:TRISTINWOLD_CLAUDE\n' @'
Tell "Vann's court is up on the server."

'@
Swap @'
    $show = if ($lord) { $settings.window -ne 'no' } elseif ($Together) { $false } else { $settings.window_others -ne 'no' }
'@ @'
    $show = $false # no windows on the server
'@
Swap @'
    $arguments += @('--path', "`"$source\game`"", '--')
'@ @'
    $arguments += @('--')
'@
Swap @'
(Account-Of $m), '--secret', $settings.password, '--company'
'@ @'
(Account-Of $m), '--company'
'@
Swap @'
        if ($settings.server_password) { $arguments += @('--password', $settings.server_password) }

'@ ''
# Telegram's "stop": exit 2, so the service waits for "start" instead of starting the court again.
$stops = [regex]::Matches($text, '(Tell "Vann''s court has stopped\. Send start to bring it back\."\s*;?\s*)exit 0')
if ($stops.Count -ne 3) { throw "run-vann.ps1 has changed: expected 3 stops, found $($stops.Count)" }
$text = [regex]::Replace($text, '(Tell "Vann''s court has stopped\. Send start to bring it back\."\s*;?\s*)exit 0', '${1}exit 2')
Swap @'
    if (-not $ollama) { Brain-Note "Ollama isn't installed: playing without the local brain."; return @() }
'@ @'
    if ($settings.ollama_path -and (Test-Path $settings.ollama_path)) { $ollama = $settings.ollama_path }
    if (-not $ollama) { Brain-Note "Ollama isn't installed: playing without the local brain."; return @() }
    $env:OLLAMA_MODELS = Join-Path $pkg 'ollama-models' # models kept with the court, wherever the service's account is
'@
SwapRange '(?s)\nif \(Others-Playing\) \{ Write-Host "Done\..*$' @'

exit 1 # the court stopped by itself: the service starts it again
'@
foreach ($left in @('--secret', 'dotnet build', 'git ', "'--path'")) { if ($text.Contains($left)) { throw "The server launcher still has $left in it." } }
Set-Content -Path (Join-Path $court 'run-vann.ps1') -Value ($text -replace "`n", "`r`n") -Encoding UTF8 -NoNewline

# 4. The keys, sealed for the trip, and the kit.
Step 'Sealing the keys...'
$words = 'amber anvil arrow ash badger birch bramble brass cinder cliff copper crow dagger ember falcon fern flint frost gale glade granite harbour hawk heath hollow iron ivy kestrel lantern ledger marsh meadow moss oak otter pike quill raven reed rowan rust sable salt slate sorrel spire stag storm thistle thorn tide timber vale wren yew'.Split(' ')
$rng = [Security.Cryptography.RandomNumberGenerator]::Create()
$pick = { $b = New-Object byte[] 4; $rng.GetBytes($b); $words[[BitConverter]::ToUInt32($b, 0) % $words.Count] }
$pass = (1..6 | ForEach-Object { & $pick }) -join '-'
Close-Sealed $keys $pass (Join-Path $pkg 'keys.sealed')
Set-Content -Path (Join-Path $local 'vann-server-passphrase.txt') -Value @(
    "Passphrase for keys.sealed in Vann-Server.zip (made $(Get-Date -Format 'yyyy-MM-dd HH:mm')):", $pass, '',
    'Give it to whoever installs, separately from the zip (not in the same message). Install Vann.bat asks for it once.') -Encoding UTF8
foreach ($f in @('keys.ps1', 'set-keys.ps1', 'vann-service.ps1', 'install.ps1', 'stop-start.ps1', 'README.txt', 'Install Vann.bat', 'Set Keys.bat', 'Stop Vann.bat', 'Start Vann.bat', 'Update Vann.bat')) {
    Copy-Item (Join-Path $kit $f) (Join-Path $pkg $f) -Force
}
$zip = Join-Path $local 'Vann-Server.zip'
if (Test-Path $zip) { Remove-Item $zip -Force }
Step 'Zipping...'
Compress-Archive -Path "$pkg\*" -DestinationPath $zip
Step "Done: $zip ($([math]::Round((Get-Item $zip).Length / 1MB)) MB), and $gameZip for updates. Passphrase: local-claude\vann-server-passphrase.txt"
