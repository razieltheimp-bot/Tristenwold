# Puts Vann's keys into this machine's encrypted store (keys.dat). Run as administrator (install.ps1 runs it the first
# time; run it again any time to change a key). Typing shows nothing, and nothing is printed back.
#   - keys.sealed (made on the owner's PC) is opened first with its passphrase, if it's here, and then deleted.
#   - Then each key can be replaced: press Enter to keep the one already there.
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'keys.ps1')
function Plain($secure) { [Runtime.InteropServices.Marshal]::PtrToStringBSTR([Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure)) }

$keys = Read-Vault
if (Test-Path $script:SealedFile) {
    for ($try = 1; $try -le 3; $try++) {
        $pass = Plain (Read-Host -AsSecureString 'Passphrase for keys.sealed (from the owner)')
        try { $sealed = Open-Sealed $pass; foreach ($k in $sealed.Keys) { if (-not $keys[$k]) { $keys[$k] = $sealed[$k] } }; Write-Host 'keys.sealed opened.'; break }
        catch { Write-Host $_.Exception.Message }
    }
}

$about = [ordered]@{
    gemini_key      = 'Gemini API key (the one made for this server, locked to its IP)'
    telegram_token  = 'Telegram bot token (Vann reports through it)'
    password        = "The court's account password (every Vanncourt account)"
    server_password = "The world's server password (blank if it has none)"
    top_key         = 'Top-brain key (only if vann-settings.txt sets top_url; usually blank)'
    discord_token   = "Discord bot token (Vann talks in the game's channel)"
    discord_channel = "Discord channel ID he talks in (Developer Mode on, right-click the channel, Copy Channel ID)"
}
foreach ($name in $about.Keys) {
    $state = if ($keys[$name]) { 'set; Enter keeps it' } else { 'not set; Enter leaves it blank' }
    $new = Plain (Read-Host -AsSecureString "$($about[$name]) [$state]")
    if ($new) { $keys[$name] = $new.Trim() }
}
Write-Vault $keys
if (Test-Path $script:SealedFile) { Remove-Item $script:SealedFile -Force }
Write-Host ''
Write-Host 'Saved, encrypted to this machine:'
foreach ($name in $about.Keys) { Write-Host ("  {0,-16} {1}" -f $name, $(if ($keys[$name]) { 'set' } else { '-' })) }
