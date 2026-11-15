# Stop Vann.bat / Start Vann.bat / Update Vann.bat.
#   stop    closes the court and its service; it stays closed until start (or the machine restarts)
#   start   starts the service (and so the court) again
#   update  stop, then put in the new game build from Vann-game.zip next to this script, then start
param([string]$what = 'start')
$here = $PSScriptRoot
$task = "Vann's court"
function Stop-Court {
    Stop-ScheduledTask -TaskName $task -ErrorAction SilentlyContinue
    $mine = @(Get-CimInstance Win32_Process | Where-Object {
        ($_.CommandLine -match 'vann-service\.ps1|run-vann\.ps1') -or ($_.Name -like 'Tristinwold-Vann*') })
    foreach ($p in $mine) { Stop-Process -Id $p.ProcessId -Force -ErrorAction SilentlyContinue }
    Start-Sleep 5
}
switch ($what) {
    'stop' { Stop-Court; Write-Host "Vann's court is stopped. Start Vann.bat brings it back." }
    'start' { Start-ScheduledTask -TaskName $task; Write-Host "Vann's court is starting." }
    'update' {
        $zip = Join-Path $here 'Vann-game.zip'
        if (-not (Test-Path $zip)) { Write-Host "Put Vann-game.zip next to Update Vann.bat first."; exit 1 }
        Stop-Court
        $game = Join-Path $here 'game'
        if (Test-Path "$game.previous") { Remove-Item "$game.previous" -Recurse -Force }
        if (Test-Path $game) { Rename-Item $game "$game.previous" }
        Expand-Archive -Path $zip -DestinationPath $game -Force
        Remove-Item $zip -Force
        Start-ScheduledTask -TaskName $task
        Write-Host "The new build is in (the old one is kept as game.previous), and Vann's court is starting."
    }
}
