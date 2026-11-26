# Starts Vann's court: builds the game with the local-only bot compiled in, starts Vann, and keeps starting whoever he
# raises (Vann-Uno, Vann-Dos, Vann-Tres, then his agents), one at a time, switching as he says. Close the window of
# whoever's playing to stop the court.
# Run with:  powershell -ExecutionPolicy Bypass -File local-claude\vann\run-vann.ps1   (add -Offline to try it alone)
param([switch]$Offline, [switch]$Restarted)

$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$pkg = Split-Path -Parent $here
$local = $pkg
$source = $pkg
$godot = Join-Path $pkg 'game\Tristinwold-Vann.exe' # the game with the bot built in: on the server it always plays with no window
. (Join-Path $pkg 'keys.ps1')
$vault = Read-Vault
$host.UI.RawUI.WindowTitle = "Vann's court"

function Read-Settings($file) {
    $s = @{}
    foreach ($line in Get-Content $file -ErrorAction SilentlyContinue) {
        if ($line -match '^\s*([a-z_]+)\s*=\s*(.*?)\s*$') { $s[$matches[1]] = $matches[2] }
    }
    return $s
}
$settings = Read-Settings (Join-Path $here 'vann-settings.txt')
$ashgrip = Read-Settings (Join-Path $local 'settings.txt')
if (-not $settings.server) { $settings.server = $ashgrip.server }
if (-not $settings.server_password) { $settings.server_password = $ashgrip.server_password }
# On the server the keys come from this machine's encrypted store (keys.dat), never from a file in plain text.
foreach ($k in @('gemini_key', 'top_key', 'password', 'server_password', 'telegram_token')) { if ($vault[$k]) { $settings[$k] = $vault[$k] } }
$settings.window = 'no'; $settings.window_others = 'no'
# The court's passwords reach its games in the environment, never on a command line others on this machine could read.
$env:TRISTINWOLD_SECRET = $settings.password
if ($settings.server_password) { $env:TRISTINWOLD_SERVER_PASSWORD = $settings.server_password }
function Num($v, $d) { if ($v -match '^\d+(\.\d+)?$') { [double]$v } else { $d } }
$daily = [int](Num $settings.gemini_daily 900)
$window = Num $settings.court_hours 4      # hours a day of Gemini the court shares, split as Vann says
$reserve = Num $settings.reserve_minutes 2 # minutes each PC keeps back to fight back if a player strikes it
$relog = Num $settings.relog_minutes 60  # a fresh login this often, to clear the game's graphics leak in long sessions
$dayStarts = Num $settings.day_starts 6   # the hour the court's day turns over (a late night counts against the day it began)
function Court-Today { (Get-Date).AddHours(-$dayStarts).ToString('yyyy-MM-dd') }

# The court's own Gemini key, handed to its games only (Ashgrip keeps his).
$key = $settings.gemini_key
if (-not $key) { $key = [Environment]::GetEnvironmentVariable('VANN_GEMINI_API_KEY', 'User') }
if (-not $key) { $key = [Environment]::GetEnvironmentVariable('GEMINI_API_KEY', 'User') }
if ($key) { $env:GEMINI_API_KEY = $key; $env:GOOGLE_API_KEY = $null; Write-Host "Gemini runs the court ($daily free calls a day, shared out)." }
else { Write-Host "No Gemini key: the court plays with the simple brain, and Vann raises only his three lieutenants." }
$env:ANTHROPIC_API_KEY = $null # the court never spends Claude money
if ($settings.top_url) {
    # A top brain other than Gemini (top_url in vann-settings.txt): its key goes to the games, never on the command line.
    $env:TOP_BRAIN_KEY = $settings.top_key
    Write-Host "Top brain: $($settings.top_model) at $($settings.top_url)."
}
# Telegram (telegram_token in vann-settings.txt): Vann's games talk to you through the bot; the token goes to them in an
# environment variable, never on the command line. The chat is learned the first time you message the bot.
$tg = $settings.telegram_token
$tgFile = Join-Path $here 'telegram-token.txt'
if (-not $tg -and (Test-Path $tgFile)) { $tg = (Get-Content $tgFile -Raw).Trim() }
if ($tg) { $env:VANN_TELEGRAM_TOKEN = $tg; Write-Host "Telegram: Vann reports to you through the bot." }
# Discord (discord_token and discord_channel in the key store): Vann answers people in the game's channel and posts the
# court's big moments there. The token goes to the games in an environment variable, never on the command line.
if ($vault['discord_token'] -and $vault['discord_channel']) {
    $env:VANN_DISCORD_TOKEN = $vault['discord_token']; $env:VANN_DISCORD_CHANNEL = $vault['discord_channel']
    Write-Host "Discord: Vann talks in the game's channel."
}
function Tell($text) {
    $chatFile = Join-Path $here 'telegram-chat.txt'
    if (-not $env:VANN_TELEGRAM_TOKEN -or -not (Test-Path $chatFile)) { return }
    try {
        $chat = (Get-Content $chatFile -Raw).Trim()
        Invoke-RestMethod -Uri "https://api.telegram.org/bot$($env:VANN_TELEGRAM_TOKEN)/sendMessage" -Method Post -Body @{ chat_id = $chat; text = $text } -TimeoutSec 15 | Out-Null
    } catch { }
}
# A restart asked for on Telegram ("restart"): the game leaves restart.flag here; the launcher starts a fresh copy of itself,
# which closes this court, rebuilds the game with the latest code and starts the court again.
$restartFlag = Join-Path $here 'restart.flag'
$stopFlag = Join-Path $here 'stop.flag' # "stop" on Telegram: the court closes, and stays closed until "start" (the watcher)
function Check-Restart {
    if (-not (Test-Path $restartFlag)) { return }
    Remove-Item $restartFlag -Force -ErrorAction SilentlyContinue
    Write-Host ("{0}  Restart asked for on Telegram: the service starts the court again." -f (Get-Date -Format 'HH:mm'))
    foreach ($pp in @(@($procs.Values) + @($proc))) { if ($pp -and -not $pp.HasExited) { Stop-Process -Id $pp.Id -Force -ErrorAction SilentlyContinue } }
    exit 3
}
if (-not $settings.password -or $settings.password.Length -lt 6) { Write-Host "Put a password (6 or more characters) in vann-settings.txt first."; exit 1 }
if (-not $Offline -and -not $settings.server) { Write-Host "No server: fill in server in vann-settings.txt (or Ashgrip's settings.txt)."; exit 1 }

# The same version as the server, as Ashgrip's launcher does it.
function Get-ServerVersion($server) {
    $url = $server.TrimEnd('/')
    if ($url -notmatch '^https?://') { $url = "http://$($url -replace ':7777$', ':7778')" }
    $text = ''
    try {
        $text = (Invoke-WebRequest -Method Post -Uri "$url/v1/join" -ContentType 'application/json' -TimeoutSec 20 -UseBasicParsing `
            -Body '{"type":"hello","player":"version-check","password":"","protocolVersion":0,"party":[]}').Content
    } catch {
        if ($_.ErrorDetails.Message) { $text = $_.ErrorDetails.Message }
        else { Write-Host "Couldn't reach the server to check its version: $($_.Exception.Message)" }
    }
    if ($text -match 'the server on (\d+)') { return [int]$matches[1] }
    return $null
}
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
$botExe = [IO.Path]::GetFileNameWithoutExtension($godot)
# A court already running (an older launcher and its game): closed first, so this one takes over cleanly.
$older = @(Get-CimInstance Win32_Process -ErrorAction SilentlyContinue | Where-Object { $_.ProcessId -ne $PID -and $_.CommandLine -match 'run-vann\.ps1' })
$courtGames = @(Get-CimInstance Win32_Process -ErrorAction SilentlyContinue | Where-Object { $_.Name -like "$botExe*" -and $_.CommandLine -match '--vann' })
if ($older.Count -gt 0 -or $courtGames.Count -gt 0) {
    Write-Host "Vann's court is already running: closing it so this one can take over..."
    foreach ($o in $older) { Stop-Process -Id $o.ProcessId -Force -ErrorAction SilentlyContinue }
    foreach ($g in $courtGames) { $gp = Get-Process -Id $g.ProcessId -ErrorAction SilentlyContinue; if ($gp) { try { $gp.CloseMainWindow() | Out-Null } catch { }; if (-not $gp.WaitForExit(20000)) { Stop-Process -Id $gp.Id -Force -ErrorAction SilentlyContinue } } }
    Start-Sleep 3
}
# Asked to stay stopped (stop.flag left from Telegram's "stop", or set while restarting): the old court is closed above; don't start a new one.
# Only when this launcher was started by a restart: a stop note left lying about never keeps Start Vann from starting.
if ($Restarted -and (Test-Path $stopFlag)) { Remove-Item $stopFlag -Force -ErrorAction SilentlyContinue; Tell "Vann's court has stopped. Send start to bring it back."; exit 2 }
Remove-Item $stopFlag -Force -ErrorAction SilentlyContinue
function Others-Playing { @(Get-Process -Name $botExe -ErrorAction SilentlyContinue).Count -gt 0 }
if (Others-Playing) { Write-Host "Another copy of the game is running (Ashgrip?). If the build below fails, close it and start Vann again." }
Tell "Vann's court is up on the server."
Remove-Item $restartFlag -Force -ErrorAction SilentlyContinue # a request from before this start is done with
Remove-Item $stopFlag -Force -ErrorAction SilentlyContinue
if ($Restarted) { Tell "Vann's court rebuilt with the latest code and is starting again." }

# Vann's own brain: a small model in Ollama (as MoJo and Jarvis use it), between Gemini and the simple brain. Checked at
# every login: Ollama started if it isn't running (or restarted if it's stuck), the model fetched the first time. If it
# can't be had, that character plays without it. What happened is written to brain\brain-start.log.
$brainDir = Join-Path $here 'brain'
$brainLog = Join-Path $brainDir 'brain-start.log'
function Brain-Note($text) { $line = "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')  $text"; Write-Host "  [brain] $text"; Add-Content -Path $brainLog -Value $line -ErrorAction SilentlyContinue }
function Ensure-Brain {
    if (-not (Test-Path (Join-Path $brainDir 'config.json'))) { return @() }
    try { $bcfg = Get-Content (Join-Path $brainDir 'config.json') -Raw | ConvertFrom-Json } catch { Brain-Note "config.json couldn't be read: $($_.Exception.Message)"; return @() }
    if ($bcfg.enabled -eq $false) { return @() }
    $ollama = (Get-Command ollama -ErrorAction SilentlyContinue).Source
    if (-not $ollama) { $c = Join-Path $env:LOCALAPPDATA 'Programs\Ollama\ollama.exe'; if (Test-Path $c) { $ollama = $c } }
    if ($settings.ollama_path -and (Test-Path $settings.ollama_path)) { $ollama = $settings.ollama_path }
    if (-not $ollama) { Brain-Note "Ollama isn't installed: playing without the local brain."; return @() }
    $env:OLLAMA_MODELS = Join-Path $pkg 'ollama-models' # models kept with the court, wherever the service's account is
    $base = if ($bcfg.ollama_base_url) { $bcfg.ollama_base_url.TrimEnd('/') } else { 'http://localhost:11434' }
    $model = if ($bcfg.model) { $bcfg.model } else { 'qwen2.5:3b' }
    function Tags { try { return ((Invoke-WebRequest "$base/api/tags" -UseBasicParsing -TimeoutSec 8).Content | ConvertFrom-Json) } catch { return $null } }
    function Has($t) { $t -and ($t.models | Where-Object { $_.name -eq $model -or $_.name -eq "${model}:latest" }) }
    $tags = Tags
    if (-not $tags) {
        # Not answering: if it's running but stuck, it's closed first; then started fresh.
        $stuck = @(Get-Process -Name 'ollama', 'ollama app', 'ollama_llama_server' -ErrorAction SilentlyContinue)
        if ($stuck.Count -gt 0) { Brain-Note "Ollama is running but not answering: restarting it."; $stuck | Stop-Process -Force -ErrorAction SilentlyContinue; Start-Sleep 3 }
        else { Brain-Note "Starting Ollama." }
        Start-Process $ollama -ArgumentList 'serve' -WindowStyle Hidden
        for ($i = 0; $i -lt 30 -and -not $tags; $i++) { Start-Sleep 2; $tags = Tags }
        if (-not $tags) { Brain-Note "Ollama didn't answer at $base within a minute: playing without the local brain this login."; return @() }
    }
    if (-not (Has $tags)) {
        Brain-Note "Fetching $model (about 2 GB, the first time only)..."
        & $ollama pull $model 2>&1 | Select-Object -Last 3 | ForEach-Object { Brain-Note "  ollama: $_" }
        $tags = Tags
        if (-not (Has $tags)) { Brain-Note "Couldn't get $model into Ollama: playing without the local brain this login."; return @() }
    }
    return @('--brain', "`"$brainDir`"")
}

# The court file: Vann's alone to write once he's playing; the launcher only starts it.
$courtFile = Join-Path $here 'court.json'
$lordClass = if ($settings.lord_class) { $settings.lord_class } else { 'Sorcerer' }
$lordName = if ($settings.lord_name) { $settings.lord_name } else { 'Vann-Magus' }
$prefix = if ($settings.account_prefix) { $settings.account_prefix } else { 'Vanncourt' }
# A court under another Vann (the old Vann/Vannuno/... accounts): kept aside, and a new court begun.
if ((Test-Path $courtFile) -and -not ((Get-Content $courtFile -Raw) -match ('"Name":\s*"' + [regex]::Escape($lordName) + '"'))) {
    Move-Item $courtFile (Join-Path $here 'court-old.json') -Force
    Write-Host "A new court for $lordName (the old one is kept as court-old.json)."
}
# Each one's account: Vanncourt for Vann, Vanncourt-Uno for Vann-Uno, and so on.
function Account-Of($m) { if ($m.Role -eq 'lord') { $prefix } else { "$prefix-" + ($m.Name -replace '^Vann-', '') } }
if (-not (Test-Path $courtFile)) {
    @{ Decree = ''; Frontier = ''; Seat = ''; SeatX = 0; SeatZ = 0; Updated = ''
       Members = @(@{ Name = $lordName; Class = $lordClass; Role = 'lord'; Purpose = 'Vann himself'; Raised = (Get-Date -Format 'yyyy-MM-dd') }) } |
        ConvertTo-Json -Depth 5 | Set-Content $courtFile -Encoding UTF8
}
function Read-Court {
    try { return (Get-Content $courtFile -Raw -ErrorAction Stop | ConvertFrom-Json) } catch { return $null }
}

# Gemini's free calls left today on the court's key, all of them together (each keeps its own count, gemini-used.txt).
function Gemini-Left {
    $today = Get-Date -Format 'yyyy-MM-dd'
    $used = 0
    foreach ($f in Get-ChildItem -Path $here -Filter 'gemini-used.txt' -Recurse -ErrorAction SilentlyContinue) {
        $lines = @(Get-Content $f.FullName -ErrorAction SilentlyContinue)
        if ($lines.Count -gt 0 -and $lines[0] -eq $today) {
            foreach ($l in $lines | Select-Object -Skip 1) { if ($l -match '^(\S+) (\d+)$' -and $matches[1] -ne 'seconds') { $used += [int]$matches[2] } }
        }
    }
    return [Math]::Max(0, $daily - $used)
}

# Who Vann has asked to be played next (switch.json, written by whichever of the court is playing).
$switchFile = Join-Path $here 'switch.json'
function Read-Switch { try { return (Get-Content $switchFile -Raw -ErrorAction Stop | ConvertFrom-Json) } catch { return $null } }

function Start-Member($m, [switch]$Together) {
    $home_ = Join-Path $here $m.Name
    New-Item -ItemType Directory -Force -Path $home_ | Out-Null
    $lord = $m.Role -eq 'lord'
    # One at a time: whoever's playing may use what's left of the key's free calls today, a call every 6.5 s at most.
    $cap = [int](Gemini-Left)
    if ($Together) { $cap = [int]($cap / [Math]::Max(1, @((Read-Court).Members).Count)) } # all at once: the free calls shared out
    $arguments = @()
    $show = $false # no windows on the server
    if (-not $show) { $arguments += '--headless' }
    $arguments += @('--')
    if ($Offline) { $arguments += @('--server', 'off', '--company', $m.Name) }
    else {
        $arguments += @('--server', $settings.server, '--account', (Account-Of $m), '--company', $m.Name)
    }
    $arguments += @('--look', $m.Class)
    if ($settings.look) { $arguments += @('--avatar', "`"$($settings.look)`"") }
    $arguments += @('--claude', '--claude-minutes', '0', '--claude-home', "`"$home_`"", '--vann', $m.Role, '--court', "`"$here`"")
    if ($Together) { $arguments += @('--court-together', 'yes') }
    $arguments += @('--gemini-cap', [Math]::Max(1, $cap), '--gemini-every', '6.5', '--court-raise', $(if ($settings.raise -eq 'no') { 'no' } else { 'yes' }))
    $arguments += @('--court-most', [int](Num $settings.most_pcs 8), '--court-gap', [int](Num $settings.raise_gap 20))
    $arguments += @('--court-day-starts', $dayStarts.ToString([Globalization.CultureInfo]::InvariantCulture))
    $arguments += @('--court-hours', $window.ToString([Globalization.CultureInfo]::InvariantCulture), '--court-reserve', $reserve.ToString([Globalization.CultureInfo]::InvariantCulture))
    if ($settings.gemini_checkin) { $arguments += @('--gemini-checkin', $settings.gemini_checkin) }
    if ($settings.top_url) { $arguments += @('--top-url', $settings.top_url, '--top-model', "`"$($settings.top_model)`"") }
    $arguments += @('--graphics', $(if ($settings.graphics) { $settings.graphics } else { 'Medium' }))
    if ($settings.hours) { $arguments += @('--play-hours', $settings.hours) }
    if ($speak) { $arguments += @('--speak-protocol', $speak) }
    $brainArgs = Ensure-Brain
    if ($brainArgs.Count -gt 0) { Write-Host "  Vann's brain: on." }
    $arguments += $brainArgs
    $style = if ($show) { 'Normal' } else { 'Hidden' }
    $p = Start-Process -FilePath $godot -ArgumentList $arguments -WindowStyle $style -PassThru `
        -RedirectStandardError "$home_\last-run.log" -RedirectStandardOutput "$home_\last-run.out"
    Write-Host ("{0}  {1} ({2}, {3}) logs in." -f (Get-Date -Format 'HH:mm'), $m.Name, $m.Role, $m.Class)
    return $p
}

# One of the court in the world at a time, for as long as you leave it running: Vann first, then whoever he switches to.
# Each one has Gemini (his mind) for its share of the day's Gemini hours, and plays on by its last orders after that.
# A character that hands over (Vann's choice, or its Gemini hours spent) logs out and the next logs in; close the window
# of whoever's playing to stop the court.
$procs = @{}
if ($settings.together -eq 'yes') {
    # Vann on screen the whole time; at most background_max of the rest in the background (no windows), taking turns:
    # every cycle_minutes the one that's been in longest logs out and the next waiting logs in. Each plays on its own
    # share of the Gemini hours. Closing Vann's window stops the whole court.
    $bgMax = [int](Num $settings.background_max 2)
    $cycle = Num $settings.cycle_minutes 30
    Write-Host "Vann's court is playing together: Vann on screen, $bgMax more in the background, taking turns every $cycle minutes. Close Vann's window to stop the court. Logs: local-claude\vann\<name>\last-run.log"
    $started = @{}
    $fails = @{}
    $queue = New-Object System.Collections.ArrayList   # the background characters, next to log in first
    $lastSwap = Get-Date
    $stop = $false
    while (-not $stop) {
        Check-Restart
        if (Test-Path $stopFlag) {
            Remove-Item $stopFlag -Force -ErrorAction SilentlyContinue
            Write-Host ("{0}  Stop asked for on Telegram: closing the court." -f (Get-Date -Format 'HH:mm'))
            foreach ($pp in @(@($procs.Values) + @($proc))) { if ($pp -and -not $pp.HasExited) { try { $pp.CloseMainWindow() | Out-Null } catch { }; if (-not $pp.WaitForExit(20000)) { Stop-Process -Id $pp.Id -Force -ErrorAction SilentlyContinue } } }
            Tell "Vann's court has stopped. Send start to bring it back."
            exit 2
        }
        $court = Read-Court
        $members = if ($court) { @($court.Members) } else { @() }
        if ($members.Count -eq 0) { $members = @([pscustomobject]@{ Name = $lordName; Class = $lordClass; Role = 'lord' }) }
        $lord = $members | Where-Object { $_.Role -eq 'lord' } | Select-Object -First 1
        $others = @($members | Where-Object { $_.Role -ne 'lord' })
        foreach ($o in $others) { if (-not $queue.Contains($o.Name)) { [void]$queue.Add($o.Name) } }

        # Whoever has dropped out: a refused login waits; Vann's window closed stops the court.
        foreach ($name in @($procs.Keys)) {
            $p = $procs[$name]
            if (-not $p.HasExited) { continue }
            $procs.Remove($name)
            $busy = Select-String -Path (Join-Path (Join-Path $here $name) 'last-run.log') -Pattern "429 Too Many Requests" -ErrorAction SilentlyContinue | Select-Object -Last 1
            if ($busy) {
                Write-Host ("{0}  {1}: the server says too many logins just now; trying again in 3 minutes." -f (Get-Date -Format 'HH:mm'), $name)
                $fails[$name] = 0
                Start-Sleep 180
                continue
            }
            if ($lord -and $name -eq $lord.Name) {
                $why = Select-String -Path (Join-Path (Join-Path $here $name) 'last-run.log') -Pattern "Couldn't log in" -ErrorAction SilentlyContinue | Select-Object -Last 1
                if ($why) { Write-Host "$name couldn't get into the server: $($why.Line -replace '^.*?Couldn''t', 'Couldn''t')" }
                else { Write-Host "Vann's window was closed: the court stops." }
                $stop = $true
                break
            }
            $short = $started[$name] -and ((Get-Date) - $started[$name]).TotalMinutes -lt 3
            $fails[$name] = if ($short) { [int]$fails[$name] + 1 } else { 0 }
            Write-Host ("{0}  {1} dropped out{2}" -f (Get-Date -Format 'HH:mm'), $name, $(if ($fails[$name] -ge 3) { ': three times in a row, so it sits out (check its last-run.log).' } else { '.' }))
        }
        if ($stop) { break }

        # Vann first, always.
        if ($lord -and -not $procs[$lord.Name]) {
            $procs[$lord.Name] = Start-Member $lord -Together
            $started[$lord.Name] = Get-Date
            Start-Sleep 45
            continue
        }

        # Hourly fresh logins, to clear the graphics leak.
        foreach ($name in @($procs.Keys)) {
            if ($relog -gt 0 -and ((Get-Date) - $started[$name]).TotalMinutes -ge $relog) {
                Write-Host ("{0}  {1}: a fresh login (every {2} minutes)." -f (Get-Date -Format 'HH:mm'), $name, $relog)
                $p = $procs[$name]
                try { $p.CloseMainWindow() | Out-Null } catch { }
                if (-not $p.WaitForExit(20000)) { Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue }
                $procs.Remove($name)
                if ($lord -and $name -ne $lord.Name) { $queue.Remove($name); [void]$queue.Insert(0, $name) } # straight back in
                Start-Sleep 25 # long enough for the server to let go of the old login ("Rejected: already connected")
            }
        }

        # Taking turns: with more waiting than there's room for, the one in longest logs out every cycle_minutes.
        $inBg = @($procs.Keys | Where-Object { -not $lord -or $_ -ne $lord.Name })
        $waiting = @($queue | Where-Object { $inBg -notcontains $_ -and [int]$fails[$_] -lt 3 })
        if ($cycle -gt 0 -and $waiting.Count -gt 0 -and $inBg.Count -ge $bgMax -and ((Get-Date) - $lastSwap).TotalMinutes -ge $cycle) {
            $out = $inBg | Sort-Object { $started[$_] } | Select-Object -First 1
            Write-Host ("{0}  {1} takes a rest; {2} takes a turn." -f (Get-Date -Format 'HH:mm'), $out, $waiting[0])
            $p = $procs[$out]
            try { $p.CloseMainWindow() | Out-Null } catch { }
            if (-not $p.WaitForExit(20000)) { Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue }
            $procs.Remove($out)
            $lastSwap = Get-Date
            Start-Sleep 5
            $inBg = @($procs.Keys | Where-Object { -not $lord -or $_ -ne $lord.Name })
        }

        # A free place in the background: the next waiting one logs in (and goes to the back of the line).
        if ($inBg.Count -lt $bgMax) {
            $next = $queue | Where-Object { $inBg -notcontains $_ -and [int]$fails[$_] -lt 3 } | Select-Object -First 1
            $m = $others | Where-Object { $_.Name -eq $next } | Select-Object -First 1
            if ($m) {
                $procs[$m.Name] = Start-Member $m -Together
                $started[$m.Name] = Get-Date
                $queue.Remove($m.Name); [void]$queue.Add($m.Name)
                if ($inBg.Count + 1 -ge $bgMax) { $lastSwap = Get-Date }
                Start-Sleep 45
                continue
            }
        }
        Start-Sleep 5
    }
}
else {
Write-Host "Vann's court is playing, one character at a time. Close the window of whoever's playing to stop it. Logs: local-claude\vann\<name>\last-run.log"
$proc = $null
$current = $null
$again = $null # the same character, logging straight back in after its hourly fresh start
$startedAt = Get-Date
while ($true) {
    Check-Restart
    if (Test-Path $stopFlag) {
        Remove-Item $stopFlag -Force -ErrorAction SilentlyContinue
        Write-Host ("{0}  Stop asked for on Telegram: closing the court." -f (Get-Date -Format 'HH:mm'))
        foreach ($pp in @(@($procs.Values) + @($proc))) { if ($pp -and -not $pp.HasExited) { try { $pp.CloseMainWindow() | Out-Null } catch { }; if (-not $pp.WaitForExit(20000)) { Stop-Process -Id $pp.Id -Force -ErrorAction SilentlyContinue } } }
        Tell "Vann's court has stopped. Send start to bring it back."
        exit 2
    }
    $court = Read-Court
    $members = if ($court) { @($court.Members) } else { @() }
    if ($members.Count -eq 0) { $members = @([pscustomobject]@{ Name = $lordName; Class = $lordClass; Role = 'lord' }) }
    if (-not $proc) {
        $sw = Read-Switch
        $next = if ($again) { $again } elseif (-not $current) { $lordName } elseif ($sw -and ($members | Where-Object { $_.Name -eq $sw.To })) { $sw.To } else { $current }
        $again = $null
        $m = $members | Where-Object { $_.Name -eq $next } | Select-Object -First 1
        if (-not $m) { $m = $members[0] }
        $startedAt = Get-Date
        $proc = Start-Member $m
        $current = $m.Name
    }
    elseif (-not $proc.HasExited -and $relog -gt 0 -and ((Get-Date) - $startedAt).TotalMinutes -ge $relog) {
        # An hour in: log out and straight back in, which clears the graphics the game leaks over a long session.
        Write-Host ("{0}  {1}: a fresh login (every {2} minutes, to keep the game from running out of graphics memory)." -f (Get-Date -Format 'HH:mm'), $current, $relog)
        try { $proc.CloseMainWindow() | Out-Null } catch { }
        if (-not $proc.WaitForExit(20000)) { Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue }
        $again = $current
        $proc = $null
        Start-Sleep 25 # long enough for the server to let go of the old login ("Rejected: already connected")
        continue
    }
    elseif ($proc.HasExited) {
        $sw = Read-Switch
        $handover = $sw -and $sw.By -eq $current -and $sw.To -ne $current -and ([datetime]$sw.At) -gt $startedAt.AddSeconds(-5)
        if ($handover) {
            Write-Host ("{0}  {1} logs out: {2}" -f (Get-Date -Format 'HH:mm'), $current, $sw.Why)
            $proc = $null
            Start-Sleep 5
            continue
        }
        if (Select-String -Path (Join-Path (Join-Path $here $current) 'last-run.log') -Pattern "429 Too Many Requests" -ErrorAction SilentlyContinue) {
            Write-Host ("{0}  {1}: the server says too many logins just now; trying again in 3 minutes." -f (Get-Date -Format 'HH:mm'), $current)
            Start-Sleep 180
            $again = $current
            $proc = $null
            continue
        }
        $why = Select-String -Path (Join-Path (Join-Path $here $current) 'last-run.log') -Pattern "Couldn't log in" -ErrorAction SilentlyContinue | Select-Object -Last 1
        if ($why) { Write-Host "$current couldn't get into the server: $($why.Line -replace '^.*?Couldn''t', 'Couldn''t')"; Write-Host "Check the password in vann-settings.txt matches the account." }
        else { Write-Host "$current's window was closed: the court stops." }
        break
    }
    Start-Sleep 5
}

if ($proc) { $procs[$current] = $proc }
}

# Everyone else saves every 20 seconds anyway; give them a moment, then stop them.
foreach ($name in @($procs.Keys)) {
    $p = $procs[$name]
    if ($p -and -not $p.HasExited) { try { $p.CloseMainWindow() | Out-Null } catch { } }
}
Start-Sleep 8
foreach ($name in @($procs.Keys)) { $p = $procs[$name]; if ($p -and -not $p.HasExited) { Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue } }

exit 1 # the court stopped by itself: the service starts it again