param(
    [Parameter(Mandatory = $true)]
    [string] $SourceConnectionString,

    [Parameter(Mandatory = $true)]
    [string] $TargetConnectionString,

    [ValidatePattern('^[A-Za-z_][A-Za-z0-9_]*$')]
    [string] $SourceSchema = "public",

    [string] $Psql = "psql",

    [switch] $DryRun
)

$ErrorActionPreference = "Stop"

function Get-ConnectionValue {
    param(
        [hashtable] $Values,
        [string[]] $Aliases
    )

    foreach ($alias in $Aliases) {
        $normalizedAlias = $alias.Replace(" ", "").Replace("_", "").ToLowerInvariant()
        if ($Values.ContainsKey($normalizedAlias) -and -not [string]::IsNullOrWhiteSpace($Values[$normalizedAlias])) {
            return $Values[$normalizedAlias]
        }
    }

    return $null
}

function ConvertFrom-PostgresConnectionString {
    param(
        [string] $ConnectionString,
        [string] $Name
    )

    $values = @{}
    if ($ConnectionString.Contains(";")) {
        $builder = [System.Data.Common.DbConnectionStringBuilder]::new()
        # Windows PowerShell 5.1 can treat property assignment as a dictionary key.
        $builder.set_ConnectionString($ConnectionString)
        foreach ($key in $builder.Keys) {
            $normalizedKey = $key.ToString().Replace(" ", "").Replace("_", "").ToLowerInvariant()
            $values[$normalizedKey] = $builder[$key].ToString()
        }
    }
    else {
        $pattern = '(?<key>[A-Za-z_][A-Za-z0-9_]*)\s*=\s*(?:''(?<single>(?:\\.|[^''])*)''|"(?<double>(?:\\.|[^"])*)"|(?<plain>\S+))'
        foreach ($match in [regex]::Matches($ConnectionString, $pattern)) {
            $normalizedKey = $match.Groups["key"].Value.Replace("_", "").ToLowerInvariant()
            $value = if ($match.Groups["single"].Success) {
                $match.Groups["single"].Value
            }
            elseif ($match.Groups["double"].Success) {
                $match.Groups["double"].Value
            }
            else {
                $match.Groups["plain"].Value
            }
            $values[$normalizedKey] = $value
        }
    }

    $hostName = Get-ConnectionValue -Values $values -Aliases @("host", "server", "data source", "address", "addr", "network address")
    $database = Get-ConnectionValue -Values $values -Aliases @("database", "dbname", "initial catalog")
    $userName = Get-ConnectionValue -Values $values -Aliases @("username", "user id", "userid", "user", "uid")

    $missingKeys = @()
    if ([string]::IsNullOrWhiteSpace($hostName)) { $missingKeys += "Host" }
    if ([string]::IsNullOrWhiteSpace($database)) { $missingKeys += "Database" }
    if ([string]::IsNullOrWhiteSpace($userName)) { $missingKeys += "Username" }
    if ($missingKeys.Count -gt 0) {
        $recognizedKeys = @($values.Keys | Where-Object { $_ -notin @("password", "pwd") } | Sort-Object) -join ", "
        if ([string]::IsNullOrWhiteSpace($recognizedKeys)) {
            $recognizedKeys = "none"
        }
        throw "$Name connection string is missing: $($missingKeys -join ', '). Recognized keys: $recognizedKeys."
    }

    $port = Get-ConnectionValue -Values $values -Aliases @("port")
    if ([string]::IsNullOrWhiteSpace($port)) {
        $port = "5432"
    }

    $sslMode = Get-ConnectionValue -Values $values -Aliases @("sslmode", "ssl mode")
    if ([string]::IsNullOrWhiteSpace($sslMode)) {
        $sslMode = "prefer"
    }
    $sslMode = switch ($sslMode.Replace("-", "").ToLowerInvariant()) {
        "disable" { "disable" }
        "allow" { "allow" }
        "prefer" { "prefer" }
        "require" { "require" }
        "verifyca" { "verify-ca" }
        "verifyfull" { "verify-full" }
        default { throw "$Name connection string contains unsupported SSL mode: $sslMode" }
    }

    return [pscustomobject]@{
        Host = $hostName
        Port = $port
        Database = $database
        Username = $userName
        Password = Get-ConnectionValue -Values $values -Aliases @("password", "pwd")
        SslMode = $sslMode
    }
}

function ConvertTo-PlainText {
    param([Security.SecureString] $SecureValue)

    $pointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($SecureValue)
    try {
        return [Runtime.InteropServices.Marshal]::PtrToStringBSTR($pointer)
    }
    finally {
        [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($pointer)
    }
}

function Read-PasswordIfMissing {
    param(
        [string] $ExistingPassword,
        [string] $Prompt
    )

    if (-not [string]::IsNullOrEmpty($ExistingPassword)) {
        return $ExistingPassword
    }

    return ConvertTo-PlainText (Read-Host $Prompt -AsSecureString)
}

function ConvertTo-LibpqValue {
    param([string] $Value)

    return "'" + $Value.Replace("\", "\\").Replace("'", "\'") + "'"
}

function New-LibpqConnectionString {
    param($Connection)

    return @(
        "host=$(ConvertTo-LibpqValue $Connection.Host)"
        "port=$(ConvertTo-LibpqValue $Connection.Port)"
        "dbname=$(ConvertTo-LibpqValue $Connection.Database)"
        "user=$(ConvertTo-LibpqValue $Connection.Username)"
        "sslmode=$(ConvertTo-LibpqValue $Connection.SslMode)"
    ) -join " "
}

function Set-UInt32BigEndian {
    param(
        [byte[]] $Buffer,
        [int] $Offset,
        [uint32] $Value
    )

    $bytes = [BitConverter]::GetBytes($Value)
    if ([BitConverter]::IsLittleEndian) {
        [Array]::Reverse($bytes)
    }

    [Array]::Copy($bytes, 0, $Buffer, $Offset, 4)
}

function New-IdentityV3PasswordHash {
    param([string] $Password)

    $salt = [byte[]]::new(16)
    $random = [Security.Cryptography.RandomNumberGenerator]::Create()
    try {
        $random.GetBytes($salt)
    }
    finally {
        $random.Dispose()
    }

    $iterations = 100000
    $deriveBytes = [Security.Cryptography.Rfc2898DeriveBytes]::new(
        $Password,
        $salt,
        $iterations,
        [Security.Cryptography.HashAlgorithmName]::SHA512
    )
    try {
        $subkey = $deriveBytes.GetBytes(32)
    }
    finally {
        $deriveBytes.Dispose()
    }

    $output = [byte[]]::new(13 + $salt.Length + $subkey.Length)
    $output[0] = 1
    Set-UInt32BigEndian -Buffer $output -Offset 1 -Value 2
    Set-UInt32BigEndian -Buffer $output -Offset 5 -Value $iterations
    Set-UInt32BigEndian -Buffer $output -Offset 9 -Value $salt.Length
    [Array]::Copy($salt, 0, $output, 13, $salt.Length)
    [Array]::Copy($subkey, 0, $output, 13 + $salt.Length, $subkey.Length)

    return [Convert]::ToBase64String($output)
}

function Invoke-TargetPsql {
    param(
        [string] $Script,
        [string[]] $Variables = @()
    )

    $arguments = @(
        $script:targetLibpqConnectionString,
        "--set=ON_ERROR_STOP=1"
    )
    foreach ($variable in $Variables) {
        $arguments += "--set=$variable"
    }
    $arguments += "--file=$Script"

    & $script:Psql @arguments
    if ($LASTEXITCODE -ne 0) {
        throw "psql failed while running $Script."
    }
}

$setupScript = Join-Path $PSScriptRoot "prepare-legacy-foreign-schema.sql"
$importScript = Join-Path $PSScriptRoot "import-legacy-schema.sql"
$cleanupScript = Join-Path $PSScriptRoot "cleanup-legacy-foreign-schema.sql"

foreach ($requiredScript in @($setupScript, $importScript, $cleanupScript)) {
    if (-not (Test-Path $requiredScript)) {
        throw "Required SQL script not found: $requiredScript"
    }
}

$source = ConvertFrom-PostgresConnectionString -ConnectionString $SourceConnectionString -Name "Source"
$target = ConvertFrom-PostgresConnectionString -ConnectionString $TargetConnectionString -Name "Target"
Write-Host "Source: $($source.Host):$($source.Port)/$($source.Database), schema $SourceSchema"
Write-Host "Target: $($target.Host):$($target.Port)/$($target.Database), schema public"
$sourcePassword = Read-PasswordIfMissing -ExistingPassword $source.Password -Prompt "Source database password"
$targetPassword = Read-PasswordIfMissing -ExistingPassword $target.Password -Prompt "Target database password"

$temporaryPassword = ConvertTo-PlainText (Read-Host "Temporary password for imported application users" -AsSecureString)
if (
    $temporaryPassword.Length -lt 14 -or
    $temporaryPassword -cnotmatch '\p{Ll}' -or
    $temporaryPassword -cnotmatch '\p{Lu}' -or
    $temporaryPassword -notmatch '\d' -or
    $temporaryPassword -notmatch '[^\p{L}\p{N}]'
) {
    throw "The temporary password must contain at least 14 characters, lowercase and uppercase letters, a digit, and a special character."
}

$passwordHash = New-IdentityV3PasswordHash -Password $temporaryPassword
$temporaryPassword = $null
$runSuffix = [Guid]::NewGuid().ToString("N").Substring(0, 10)
$stagingSchema = "legacy_import_$runSuffix"
$foreignServer = "legacy_source_$runSuffix"
$commitImport = if ($DryRun) { "false" } else { "true" }
$targetLibpqConnectionString = New-LibpqConnectionString $target
$previousTargetPassword = $env:PGPASSWORD
$previousSourcePassword = $env:SURVEY_LEGACY_SOURCE_PASSWORD
$stagingAttempted = $false

try {
    $env:PGPASSWORD = $targetPassword
    $env:SURVEY_LEGACY_SOURCE_PASSWORD = $sourcePassword
    $stagingAttempted = $true

    Invoke-TargetPsql -Script $setupScript -Variables @(
        "source_host=$($source.Host)",
        "source_port=$($source.Port)",
        "source_database=$($source.Database)",
        "source_user=$($source.Username)",
        "source_schema=$SourceSchema",
        "source_sslmode=$($source.SslMode)",
        "staging_schema=$stagingSchema",
        "foreign_server=$foreignServer"
    )

    Invoke-TargetPsql -Script $importScript -Variables @(
        "legacy_schema=$stagingSchema",
        "legacy_password_hash=$passwordHash",
        "commit_import=$commitImport"
    )
}
finally {
    if ($stagingAttempted) {
        try {
            Invoke-TargetPsql -Script $cleanupScript -Variables @(
                "staging_schema=$stagingSchema",
                "foreign_server=$foreignServer"
            )
        }
        catch {
            Write-Warning "Temporary foreign schema cleanup failed: $($_.Exception.Message)"
        }
    }

    $env:PGPASSWORD = $previousTargetPassword
    $env:SURVEY_LEGACY_SOURCE_PASSWORD = $previousSourcePassword
    $sourcePassword = $null
    $targetPassword = $null
    $passwordHash = $null
}

if ($DryRun) {
    Write-Host "Validation completed. No target data was changed."
}
else {
    Write-Host "Database transfer completed."
}
