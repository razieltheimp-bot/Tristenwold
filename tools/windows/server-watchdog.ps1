# Keeps the Tristinwold server running on a Windows PC. If the server closes, crashes or stops answering, this starts it
# again, keeping a copy of the world first. If a server is already running, it takes over watching that one, so nobody
# gets knocked off. To stop the server for good, close this window first, then the server's own.
param(
    [string]$Folder = (Join-Path $PSScriptRoot 'server'),
    [int]$Port = 7777,
    [int]$HttpPort = 7778,
    [int]$CheckSeconds = 15,
    [int]$StartupSeconds = 45
)

$exe = Join-Path $Folder 'Tristinwold.Server.exe'
$world = Join-Path $Folder 'tristinwold.json'
$passwordFile = Join-Path $Folder 'watchdog.password'
$logFile = Join-Path $Folder 'watchdog.log'
$backups = Join-Path $Folder 'world-backups'
$keepBackups = 20
$strikesToRestart = 4

function Say([string]$text) {
    $line = "[$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')] $text"
    Write-Host $line
    Add-Content -LiteralPath $logFile -Value $line
}

if (-not (Test-Path -LiteralPath $exe)) {
    Write-Host "Tristinwold.Server.exe wasn't found in $Folder."
    exit 1
}

# Only one watchdog per server: a second one would start a second server.
$mutex = New-Object System.Threading.Mutex($false, "Local\TristinwoldWatchdog$Port")
if (-not $mutex.WaitOne(0)) {
    Write-Host 'The watchdog is already running for this server.'
    exit 0
}

function Find-Server {
    Get-CimInstance Win32_Process -Filter "Name='Tristinwold.Server.exe'" |
        Where-Object { $_.ExecutablePath -eq $exe } | Select-Object -First 1
}

function Answers {
    $client = New-Object System.Net.Sockets.TcpClient
    try { return $client.ConnectAsync('127.0.0.1', $Port).Wait(3000) -and $client.Connected }
    catch { return $false }
    finally { $client.Close() }
}

# The password is kept scrambled so that only this Windows account can read it.
function Save-Password([string]$plain) {
    ConvertTo-SecureString $plain -AsPlainText -Force | ConvertFrom-SecureString | Set-Content -LiteralPath $passwordFile
}

function Get-Password {
    if (Test-Path -LiteralPath $passwordFile) {
        $text = Get-Content -LiteralPath $passwordFile -Raw
        if (-not $text.Trim()) { return '' }
        $secure = ConvertTo-SecureString $text.Trim()
        return [System.Net.NetworkCredential]::new('', $secure).Password
    }
    $running = Find-Server
    if ($running -and $running.CommandLine -match '--password\s+(?:"([^"]*)"|(\S+))') {
        $plain = if ($Matches[1]) { $Matches[1] } else { $Matches[2] }
        Save-Password $plain
        return $plain
    }
    if ($running) { Set-Content -LiteralPath $passwordFile -Value ''; return '' }
    $secure = Read-Host 'Pick a password for your server (friends will need it; Enter for none)' -AsSecureString
    $plain = [System.Net.NetworkCredential]::new('', $secure).Password
    if ($plain) { Save-Password $plain } else { Set-Content -LiteralPath $passwordFile -Value '' }
    return $plain
}

function Backup-World {
    if (-not (Test-Path -LiteralPath $world)) { return }
    New-Item -ItemType Directory -Force -Path $backups | Out-Null
    Copy-Item -LiteralPath $world -Destination (Join-Path $backups "tristinwold-$(Get-Date -Format 'yyyyMMdd-HHmmss').json")
    Get-ChildItem -LiteralPath $backups -Filter 'tristinwold-*.json' | Sort-Object Name -Descending |
        Select-Object -Skip $keepBackups | Remove-Item
}

function Start-Server {
    $arguments = @('--campaign', 'tristinwold.json')
    if ($password) { $arguments += @('--password', "`"$($password -replace '"', '\"')`"") }
    if ($Port -ne 7777) { $arguments += @('--port', $Port) }
    if ($HttpPort -ne 7778) { $arguments += @('--http-port', $HttpPort) }
    Start-Process -FilePath $exe -ArgumentList $arguments -WorkingDirectory $Folder -PassThru
}

$password = Get-Password
$quickRestarts = 0
Say 'The watchdog is on. Leave this window open.'

while ($true) {
    $found = Find-Server
    if ($found) {
        $server = Get-Process -Id $found.ProcessId
        Say "Watching the server that's already running (process $($server.Id))."
    }
    else {
        Backup-World
        $server = Start-Server
        Say "Started the server (process $($server.Id))."
        Start-Sleep -Seconds $StartupSeconds
    }

    $began = Get-Date
    $strikes = 0
    while (-not $server.HasExited) {
        Start-Sleep -Seconds $CheckSeconds
        if ($server.HasExited) { break }
        if (Answers) { $strikes = 0; continue }
        $strikes++
        Say "The server didn't answer ($strikes of $strikesToRestart)."
        if ($strikes -ge $strikesToRestart) {
            Say 'It has stopped answering. Closing it to start it again.'
            Stop-Process -Id $server.Id -Force -ErrorAction SilentlyContinue
            break
        }
    }

    # A server that keeps falling straight over gets a longer rest between tries, so this doesn't spin.
    if (((Get-Date) - $began).TotalMinutes -lt 2) { $quickRestarts++ } else { $quickRestarts = 0 }
    $rest = if ($quickRestarts -ge 5) { 60 } else { 5 }
    Say "The server stopped. Starting it again in $rest seconds."
    Start-Sleep -Seconds $rest
}
