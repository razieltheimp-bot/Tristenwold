# Installs Vann's court on this server: no windows, always on. Run "Install Vann.bat" (it asks for administrator).
#   1. The Windows account the court runs as: a standard account (recommended: it can't change the machine), or SYSTEM.
#   2. Ollama (Vann's local brain) is found, or installed with winget if you say yes.
#   3. The keys go into this machine's encrypted store (set-keys.ps1).
#   4. A scheduled task, "Vann's court", runs vann-service.ps1 as that account at every start-up and keeps the court going.
# Run it again any time: it updates the task and leaves the keys as they are.
$ErrorActionPreference = 'Stop'
$here = $PSScriptRoot
$court = Join-Path $here 'court'
$settingsFile = Join-Path $court 'vann-settings.txt'
$admin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $admin) { Write-Host 'Run this as administrator (right-click Install Vann.bat > Run as administrator).'; exit 1 }
if (-not (Test-Path (Join-Path $here 'game\Tristinwold-Vann.exe'))) { Write-Host "The game folder is missing (game\Tristinwold-Vann.exe). Unzip the whole package first."; exit 1 }
. (Join-Path $here 'keys.ps1')
function Plain($secure) { [Runtime.InteropServices.Marshal]::PtrToStringBSTR([Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure)) }

function Set-Setting($name, $value) {
    $lines = @(Get-Content $settingsFile)
    $found = $false
    for ($i = 0; $i -lt $lines.Count; $i++) { if ($lines[$i] -match "^\s*$name\s*=") { $lines[$i] = "$name = $value"; $found = $true } }
    if (-not $found) { $lines += "$name = $value" }
    Set-Content -Path $settingsFile -Value $lines -Encoding UTF8
}

# 1. The account.
Write-Host '1. The Windows account Vann runs as'
Write-Host '   A standard (non-administrator) account is best: the court can then only touch this folder, not the machine.'
Write-Host '   Make one first if you need to (e.g. "net user vann * /add" in an administrator prompt; it stays a plain user).'
$account = Get-ServiceAccount
$before = $account
$prompt = if ($account) { "   Account [$account; Enter keeps it, or type SYSTEM]" } else { '   Account (e.g. vann or SERVER1\vann; Enter for SYSTEM)' }
$typed = (Read-Host $prompt).Trim()
if ($typed) { $account = $typed }
if ($account -eq 'SYSTEM') { $account = '' }
$password = $null
if ($account) {
    if ($account -notmatch '\\') { $account = "$env:COMPUTERNAME\$account" }
    try { $sid = (New-Object Security.Principal.NTAccount($account)).Translate([Security.Principal.SecurityIdentifier]) }
    catch { Write-Host "   There's no account called $account on this machine. Make it, then run this again."; exit 1 }
    $admins = New-Object Security.Principal.SecurityIdentifier('S-1-5-32-544')
    $isAdmin = $false
    try { $isAdmin = @(Get-LocalGroupMember -SID $admins -ErrorAction Stop | Where-Object { $_.SID -eq $sid }).Count -gt 0 } catch { }
    if ($isAdmin) { Write-Host "   Note: $account is an administrator. A standard account is safer for an AI-driven client." }
    for ($try = 1; $try -le 3 -and -not $password; $try++) {
        $p1 = Plain (Read-Host -AsSecureString "   $account's Windows password (the task keeps it to start Vann at boot)")
        $p2 = Plain (Read-Host -AsSecureString '   Again, to be sure')
        if ($p1 -and $p1 -eq $p2) { $password = $p1 } else { Write-Host "   Those didn't match (or were blank)." }
    }
    if (-not $password) { Write-Host '   No password: nothing changed.'; exit 1 }
    Set-Content -Path $script:AccountFile -Value $account -Encoding ascii
    Write-Host "   Vann will run as $account."
}
else {
    if (Test-Path $script:AccountFile) { Remove-Item $script:AccountFile -Force }
    Write-Host '   Vann will run as SYSTEM.'
}

# Who may do what in this folder. Administrators and SYSTEM: everything. The court's account: change the court, the game
# and its own logs (it updates the game by itself), but only read the scripts and the kit, so it can't change what an
# administrator later runs, and only read keys.dat.
& icacls $here /inheritance:r /grant:r '*S-1-5-18:(OI)(CI)F' '*S-1-5-32-544:(OI)(CI)F' /T /C /Q | Out-Null
if ($before -and $before -ne $account) { & icacls $here /remove $before /T /C /Q | Out-Null } # the account it ran as before
if ($account) {
    & icacls $here /grant "${account}:(OI)(CI)M" /C /Q | Out-Null
    & icacls $here /grant "${account}:(OI)(CI)M" /T /C /Q | Out-Null
    $kitFiles = @('keys.ps1', 'set-keys.ps1', 'vann-service.ps1', 'install.ps1', 'stop-start.ps1', 'README.txt', 'service-account.txt',
        'Install Vann.bat', 'Set Keys.bat', 'Stop Vann.bat', 'Start Vann.bat', 'Update Vann.bat')
    foreach ($f in $kitFiles) {
        $path = Join-Path $here $f
        if (Test-Path $path) { & icacls $path /inheritance:r /grant:r '*S-1-5-18:F' '*S-1-5-32-544:F' "${account}:RX" /C /Q | Out-Null }
    }
}
Set-VaultAccess

# 2. Ollama, where the court's account can run it.
Write-Host ''
Write-Host '2. Vann''s local brain (Ollama)'
$ollama = (Get-Command ollama -ErrorAction SilentlyContinue).Source
foreach ($c in @("$env:ProgramFiles\Ollama\ollama.exe", "$env:LOCALAPPDATA\Programs\Ollama\ollama.exe")) { if (-not $ollama -and (Test-Path $c)) { $ollama = $c } }
if (-not $ollama) {
    $yes = Read-Host '   Ollama isn''t installed. Install it now with winget (about 1 GB, from ollama.com)? [y/N]'
    if ($yes -match '^y') {
        # Into Program Files, so any account on the machine can run it (by default it goes into one user's profile).
        & winget install --id Ollama.Ollama -e --location "$env:ProgramFiles\Ollama" --accept-package-agreements --accept-source-agreements
        foreach ($c in @("$env:ProgramFiles\Ollama\ollama.exe", "$env:LOCALAPPDATA\Programs\Ollama\ollama.exe")) { if (-not $ollama -and (Test-Path $c)) { $ollama = $c } }
    }
}
if ($ollama) {
    if ($account -and $ollama -like "$env:SystemDrive\Users\*") {
        # Installed inside someone's profile: let the court's account read and run just that folder.
        & icacls (Split-Path -Parent $ollama) /grant "${account}:(OI)(CI)RX" /T /C /Q | Out-Null
    }
    Set-Setting 'ollama_path' $ollama
    Write-Host "   Using $ollama. Its model (about 1 GB) is fetched the first time Vann plays, into ollama-models here."
}
else { Write-Host '   No Ollama: Vann plays on Gemini and the simple brain. Install it later and run this again.' }

# 3. The keys.
Write-Host ''
Write-Host '3. Keys (stored encrypted to this machine; typing shows nothing)'
& (Join-Path $here 'set-keys.ps1')

# 4. The always-on task.
Write-Host ''
Write-Host '4. The always-on task'
$action = New-ScheduledTaskAction -Execute 'powershell.exe' -Argument "-NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -File `"$here\vann-service.ps1`"" -WorkingDirectory $here
$trigger = New-ScheduledTaskTrigger -AtStartup
$settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -StartWhenAvailable -ExecutionTimeLimit ([TimeSpan]::Zero) -RestartCount 999 -RestartInterval (New-TimeSpan -Minutes 1) -MultipleInstances IgnoreNew
Unregister-ScheduledTask -TaskName "Vann's court" -Confirm:$false -ErrorAction SilentlyContinue
if ($account) {
    # Runs whether anyone is logged on or not, with no administrator rights (Task Scheduler gives the account the
    # "log on as a batch job" right it needs, unless a policy forbids it).
    Register-ScheduledTask -TaskName "Vann's court" -Action $action -Trigger $trigger -Settings $settings -User $account -Password $password -RunLevel Limited | Out-Null
    $password = $null
}
else {
    $principal = New-ScheduledTaskPrincipal -UserId 'SYSTEM' -LogonType ServiceAccount -RunLevel Highest
    Register-ScheduledTask -TaskName "Vann's court" -Action $action -Trigger $trigger -Principal $principal -Settings $settings | Out-Null
}
Start-ScheduledTask -TaskName "Vann's court"
Start-Sleep 8
$info = Get-ScheduledTaskInfo -TaskName "Vann's court"
$state = (Get-ScheduledTask -TaskName "Vann's court").State
if ($state -eq 'Running') { Write-Host "   Installed and running as $(if ($account) { $account } else { 'SYSTEM' }). It starts with the machine and restarts itself." }
else {
    Write-Host ("   The task didn't stay running (state $state, result 0x{0:X8})." -f $info.LastTaskResult)
    if ($info.LastTaskResult -eq 0x80070569) { Write-Host "   $account isn't allowed to log on as a batch job: add it under secpol.msc > Local Policies > User Rights Assignment > Log on as a batch job (or the group policy that sets it), then Start Vann.bat." }
    else { Write-Host '   See service.log here (if it got that far) and Task Scheduler''s history for "Vann''s court".' }
}
Write-Host ''
Write-Host "Done. What it's doing: service.log here, and court\<name>\last-run.log for each of the court."
Write-Host "Stop Vann.bat / Start Vann.bat stop and start it. Updates come by themselves (see README.txt)."
