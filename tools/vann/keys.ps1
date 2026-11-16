# Vann's keys on the server: kept encrypted by Windows (DPAPI, tied to this one machine) in keys.dat, which only the
# court's own account (read-only), SYSTEM and the administrators may read. Copied to any other machine, keys.dat can't be opened.
#   Read-Vault           the keys, for the launcher (they go to the games in environment variables, never on a command line)
#   Write-Vault $keys    saves them (set-keys.ps1)
#   Open-Sealed $pass    the keys sealed on the owner's PC for the trip here (keys.sealed), opened with its passphrase
# Nothing here prints a key.
Add-Type -AssemblyName System.Security
$script:VaultFile = Join-Path $PSScriptRoot 'keys.dat'
$script:SealedFile = Join-Path $PSScriptRoot 'keys.sealed'
$script:VaultEntropy = [Text.Encoding]::UTF8.GetBytes('Tristinwold/Vann/court/1')
$script:KeyNames = @('gemini_key', 'telegram_token', 'password', 'server_password', 'top_key')

function Read-Vault {
    $h = @{}
    if (-not (Test-Path $script:VaultFile)) { return $h }
    $bytes = [Security.Cryptography.ProtectedData]::Unprotect([IO.File]::ReadAllBytes($script:VaultFile), $script:VaultEntropy, 'LocalMachine')
    $json = [Text.Encoding]::UTF8.GetString($bytes) | ConvertFrom-Json
    foreach ($p in $json.PSObject.Properties) { if ($p.Value) { $h[$p.Name] = [string]$p.Value } }
    return $h
}

function Write-Vault($keys) {
    $json = ($keys | ConvertTo-Json -Compress)
    $bytes = [Security.Cryptography.ProtectedData]::Protect([Text.Encoding]::UTF8.GetBytes($json), $script:VaultEntropy, 'LocalMachine')
    [IO.File]::WriteAllBytes($script:VaultFile, $bytes)
    Set-VaultAccess
}

# The Windows account the court runs as (install.ps1 asks; service-account.txt keeps it). Blank: SYSTEM.
$script:AccountFile = Join-Path $PSScriptRoot 'service-account.txt'
function Get-ServiceAccount { if (Test-Path $script:AccountFile) { (Get-Content $script:AccountFile -Raw).Trim() } else { '' } }

# keys.dat: SYSTEM (S-1-5-18) and the Administrators group (S-1-5-32-544), plus read-only for the court's own account.
function Set-VaultAccess {
    if (-not (Test-Path $script:VaultFile)) { return }
    $grants = @('*S-1-5-18:F', '*S-1-5-32-544:F')
    $account = Get-ServiceAccount
    if ($account) { $grants += "${account}:R" }
    & icacls $script:VaultFile /inheritance:r /grant:r @grants | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Couldn't set who may read keys.dat (icacls exit $LASTEXITCODE)." }
}

# keys.sealed: AES-256 with a key stretched from a passphrase (PBKDF2-SHA256, 200,000 rounds), and an HMAC so a wrong
# passphrase or a damaged file is caught rather than read as rubbish.
function Get-SealKeys([string]$pass, [byte[]]$salt) {
    $kdf = New-Object Security.Cryptography.Rfc2898DeriveBytes($pass, $salt, 200000, [Security.Cryptography.HashAlgorithmName]::SHA256)
    return , $kdf.GetBytes(64) # the first 32 bytes encrypt, the last 32 sign
}

function Close-Sealed($keys, [string]$pass, [string]$file = $script:SealedFile) {
    $rng = [Security.Cryptography.RandomNumberGenerator]::Create()
    $salt = New-Object byte[] 16; $rng.GetBytes($salt)
    $k = Get-SealKeys $pass $salt
    $aes = [Security.Cryptography.Aes]::Create(); $aes.Key = [byte[]]$k[0..31]; $aes.GenerateIV()
    $plain = [Text.Encoding]::UTF8.GetBytes(($keys | ConvertTo-Json -Compress))
    $ct = $aes.CreateEncryptor().TransformFinalBlock($plain, 0, $plain.Length)
    $hmac = New-Object Security.Cryptography.HMACSHA256(, [byte[]]$k[32..63])
    $tag = $hmac.ComputeHash([byte[]]($aes.IV + $ct))
    @{ salt = [Convert]::ToBase64String($salt); iv = [Convert]::ToBase64String($aes.IV); data = [Convert]::ToBase64String($ct); mac = [Convert]::ToBase64String($tag) } |
        ConvertTo-Json -Compress | Set-Content -Path $file -Encoding ascii
}

function Open-Sealed([string]$pass, [string]$file = $script:SealedFile) {
    $s = Get-Content $file -Raw | ConvertFrom-Json
    $salt = [Convert]::FromBase64String($s.salt); $iv = [Convert]::FromBase64String($s.iv); $ct = [Convert]::FromBase64String($s.data)
    $k = Get-SealKeys $pass $salt
    $hmac = New-Object Security.Cryptography.HMACSHA256(, [byte[]]$k[32..63])
    $tag = $hmac.ComputeHash([byte[]]($iv + $ct))
    if ([Convert]::ToBase64String($tag) -ne $s.mac) { throw 'That passphrase does not open keys.sealed.' }
    $aes = [Security.Cryptography.Aes]::Create(); $aes.Key = [byte[]]$k[0..31]; $aes.IV = $iv
    $plain = $aes.CreateDecryptor().TransformFinalBlock($ct, 0, $ct.Length)
    $h = @{}
    foreach ($p in ([Text.Encoding]::UTF8.GetString($plain) | ConvertFrom-Json).PSObject.Properties) { if ($p.Value) { $h[$p.Name] = [string]$p.Value } }
    return $h
}
