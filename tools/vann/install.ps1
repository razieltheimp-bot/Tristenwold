# Installs Vann's court on this server: no windows, always on. Run "Install Vann.bat" (it asks for administrator).
#   1. Ollama (Vann's local brain) is found, or installed with winget if you say yes.
#   2. The keys go into this machine's encrypted store (set-keys.ps1).
#   3. A scheduled task, "Vann's court", runs vann-service.ps1 as SYSTEM at every start-up and keeps the court going.
# Run it again any time: it updates the task and leaves the keys as they are.
$ErrorActionPreference = 'Stop'
$here = $PSScriptRoot
$court = Join-Path $here 'court'
$settingsFile = Join-Path $court 'vann-settings.txt'
$admin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $admin) { Write-Host 'Run this as administrator (right-click Install Vann.bat > Run as administrator).'; exit 1 }
if (-not (Test-Path (Join-Path $here 'game\Tristinwold-Vann.exe'))) { Write-Host "The game folder is missing (game\Tristinwold-Vann.exe). Unzip the whole package first."; exit 1 }

function Set-Setting($name, $value) {
    $lines = @(Get-Content $settingsFile)
    $found = $false
    for ($i = 0; $i -lt $lines.Count; $i++) { if ($lines[$i] -match "^\s*$name\s*=") { $lines[$i] = "$name = $value"; $found = $true } }
    if (-not $found) { $lines += "$name = $value" }
    Set-Content -Path $settingsFile -Value $lines -Encoding UTF8
}

# 1. Ollama, where SYSTEM can find it.
Write-Host '1. Vann''s local brain (Ollama)'
$ollama = (Get-Command ollama -ErrorAction SilentlyContinue).Source
foreach ($c in @("$env:ProgramFiles\Ollama\ollama.exe", "$env:LOCALAPPDATA\Programs\Ollama\ollama.exe")) { if (-not $ollama -and (Test-Path $c)) { $ollama = $c } }
if (-not $ollama) {
    $yes = Read-Host '   Ollama isn''t installed. Install it now with winget (about 1 GB, from ollama.com)? [y/N]'
    if ($yes -match '^y') {
        & winget install --id Ollama.Ollama -e --accept-package-agreements --accept-source-agreements
        foreach ($c in @("$env:ProgramFiles\Ollama\ollama.exe", "$env:LOCALAPPDATA\Programs\Ollama\ollama.exe")) { if (-not $ollama -and (Test-Path $c)) { $ollama = $c } }
    }
}
if ($ollama) {
    Set-Setting 'ollama_path' $ollama
    Write-Host "   Using $ollama. Its model (about 1 GB) is fetched the first time Vann plays, into ollama-models here."
}
else { Write-Host '   No Ollama: Vann plays on Gemini and the simple brain. Install it later and run this again.' }

# 2. The keys.
Write-Host ''
Write-Host '2. Keys (stored encrypted to this machine; typing shows nothing)'
& (Join-Path $here 'set-keys.ps1')

# 3. The always-on task.
Write-Host ''
Write-Host '3. The always-on task'
$action = New-ScheduledTaskAction -Execute 'powershell.exe' -Argument "-NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -File `"$here\vann-service.ps1`"" -WorkingDirectory $here
$trigger = New-ScheduledTaskTrigger -AtStartup
$principal = New-ScheduledTaskPrincipal -UserId 'SYSTEM' -LogonType ServiceAccount -RunLevel Highest
$settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -StartWhenAvailable -ExecutionTimeLimit ([TimeSpan]::Zero) -RestartCount 999 -RestartInterval (New-TimeSpan -Minutes 1) -MultipleInstances IgnoreNew
Register-ScheduledTask -TaskName "Vann's court" -Action $action -Trigger $trigger -Principal $principal -Settings $settings -Force | Out-Null
Start-ScheduledTask -TaskName "Vann's court"
Write-Host "   Installed and started. It runs as SYSTEM, starts with the machine, and restarts itself."
Write-Host ''
Write-Host "Done. What it's doing: service.log here, and court\<name>\last-run.log for each of the court."
Write-Host "Stop Vann.bat / Start Vann.bat stop and start it. Update Vann.bat puts in a new game build."
