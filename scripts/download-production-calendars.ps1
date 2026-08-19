param(
    [int[]] $Years = @(),
    [string] $OutputPath = ".\ProductionCalendar",
    [string] $BaseUrl = "https://isdayoff.ru/"
)

$ErrorActionPreference = "Stop"

if ($Years.Count -eq 0) {
    $currentYear = (Get-Date).Year
    $Years = @($currentYear - 1, $currentYear, $currentYear + 1)
}

$resolvedOutputPath = [System.IO.Path]::GetFullPath($OutputPath)
New-Item -ItemType Directory -Force -Path $resolvedOutputPath | Out-Null

foreach ($year in ($Years | Sort-Object -Unique)) {
    if ($year -lt 2000 -or $year -gt 2100) {
        throw "Некорректный год: $year."
    }

    $requestParameters = @{
        Uri = "$($BaseUrl.TrimEnd('/'))/api/getdata?year=$year"
        UseBasicParsing = $true
        TimeoutSec = 30
    }
    Write-Host "Загрузка производственного календаря за $year год..."
    $response = Invoke-WebRequest @requestParameters
    $calendar = [System.Text.RegularExpressions.Regex]::Replace(
        [string] $response.Content,
        "\s",
        "")
    $expectedLength = if ([DateTime]::IsLeapYear($year)) { 366 } else { 365 }

    if ($calendar.Length -ne $expectedLength -or $calendar -notmatch '^[01248]+$') {
        throw "Сервис вернул некорректный календарь за $year год."
    }

    $destination = Join-Path $resolvedOutputPath "$year.txt"
    [System.IO.File]::WriteAllText(
        $destination,
        $calendar,
        [System.Text.Encoding]::ASCII)
    Write-Host "Сохранено: $destination"
}
