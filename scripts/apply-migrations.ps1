param(
    [Parameter(Mandatory = $true)]
    [string] $ConnectionString,

    [string] $Psql = "psql"
)

$ErrorActionPreference = "Stop"

$RepositoryRoot = Split-Path -Parent $PSScriptRoot
$MigrationScript = Join-Path $RepositoryRoot "db\migrations\000_apply_all.sql"

if (-not (Test-Path $MigrationScript)) {
    throw "Migration script not found: $MigrationScript"
}

& $Psql $ConnectionString --set ON_ERROR_STOP=1 --file $MigrationScript

if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}
