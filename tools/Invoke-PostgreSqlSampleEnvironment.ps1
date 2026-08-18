[CmdletBinding()]
param(
    [ValidateSet('Initialize', 'Status', 'Remove')]
    [string] $Action = 'Initialize',

    [string] $Image = 'postgres:17-alpine',

    [string] $ContainerName = 'dbtools-postgres-samples',

    [string] $Password = 'dbtools_public_samples_only'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$environmentLabel = 'com.dbtools.sample-environment'
$environmentLabelValue = 'postgresql-public-samples'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$sampleRoot = Join-Path $repositoryRoot 'artifacts\sample-databases'
$configurationRoot = Join-Path $repositoryRoot 'artifacts\sample-configurations\postgresql'
$cliProject = Join-Path $repositoryRoot 'src\Anonymyzer\Anonymyzer.Console\Anonymyzer.Console.csproj'
$markerScript = Join-Path $PSScriptRoot 'markers\postgresql.sql'
$chinookScript = Join-Path $sampleRoot 'Chinook_PostgreSql.sql'
$pagilaArchive = Join-Path $sampleRoot 'pagila-v3.1.0.tar.gz'
$databaseNames = @('anonymyzer_chinook', 'anonymyzer_pagila')

function Invoke-Docker {
    & docker @args
    if ($LASTEXITCODE -ne 0) {
        throw "Docker command failed with exit code ${LASTEXITCODE}: docker $($args -join ' ')"
    }
}

function Get-ContainerName {
    $names = docker ps -a --filter "name=^/$ContainerName$" --format '{{.Names}}'
    $dockerExitCode = $LASTEXITCODE
    if ($dockerExitCode -ne 0) {
        throw 'Could not inspect Docker containers.'
    }

    return @($names | Where-Object { $_ -eq $ContainerName }) | Select-Object -First 1
}

function Assert-OwnedContainer {
    param([string] $Name)

    $labelJson = docker inspect --format '{{json .Config.Labels}}' $Name
    if ($LASTEXITCODE -ne 0) {
        throw "Could not inspect labels of container '$Name'."
    }

    $labels = $labelJson | ConvertFrom-Json
    $labelProperty = $labels.PSObject.Properties[$environmentLabel]
    $label = if ($null -eq $labelProperty) { $null } else { $labelProperty.Value }
    if ($label -ne $environmentLabelValue) {
        throw "Container '$Name' exists but is not owned by the DbTools PostgreSQL sample environment."
    }
}

function Wait-PostgreSql {
    foreach ($attempt in 1..30) {
        docker exec $ContainerName pg_isready -U postgres 2>$null | Out-Null
        if ($LASTEXITCODE -eq 0) {
            return
        }

        Start-Sleep -Seconds 1
    }

    throw "PostgreSQL container '$ContainerName' did not become ready."
}

function Get-PublishedPort {
    $publishedPort = docker port $ContainerName 5432/tcp | Select-Object -First 1
    $portMatch = [regex]::Match(([string]$publishedPort).Trim(), ':(?<Port>[0-9]+)$')
    if (-not $portMatch.Success) {
        throw "Could not parse the PostgreSQL host port from: $publishedPort"
    }

    return $portMatch.Groups['Port'].Value
}

function Test-DatabaseExists {
    param([string] $DatabaseName)

    $result = docker exec $ContainerName psql -U postgres -d postgres -Atc `
        "SELECT 1 FROM pg_database WHERE datname = '$DatabaseName';"
    if ($LASTEXITCODE -ne 0) {
        throw "Could not inspect PostgreSQL database '$DatabaseName'."
    }

    return $result -eq '1'
}

function Get-MarkerId {
    param([string] $DatabaseName)

    $marker = docker exec $ContainerName psql -U postgres -d $DatabaseName -Atc `
        'SELECT marker_id FROM public.__anonymyzer_detached_copy;'
    $parsedMarker = [Guid]::Empty
    if ($LASTEXITCODE -ne 0 -or -not [Guid]::TryParse($marker, [ref]$parsedMarker)) {
        throw "Database '$DatabaseName' does not contain a valid detached-copy marker."
    }

    return $marker
}

function Add-Marker {
    param(
        [string] $DatabaseName,
        [Guid] $MarkerId
    )

    Invoke-Docker cp $markerScript "${ContainerName}:/tmp/anonymyzer-marker.sql" | Out-Null
    Invoke-Docker exec $ContainerName psql -U postgres -d $DatabaseName `
        -v ON_ERROR_STOP=1 -v "marker_id=$($MarkerId.ToString('D'))" `
        -f /tmp/anonymyzer-marker.sql | Out-Null
}

function Import-Samples {
    if (-not (Test-Path -LiteralPath $chinookScript) -or -not (Test-Path -LiteralPath $pagilaArchive)) {
        & (Join-Path $PSScriptRoot 'Get-SampleDatabases.ps1') -Sample Chinook, Pagila
        if (-not (Test-Path -LiteralPath $chinookScript) -or -not (Test-Path -LiteralPath $pagilaArchive)) {
            throw 'Could not acquire the PostgreSQL sample files.'
        }
    }

    $temporaryRoot = Join-Path ([IO.Path]::GetTempPath()) `
        ('dbtools-pagila-' + [Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $temporaryRoot | Out-Null
    try {
        tar -xf $pagilaArchive -C $temporaryRoot
        if ($LASTEXITCODE -ne 0) {
            throw 'Could not extract the Pagila archive.'
        }

        Invoke-Docker cp $chinookScript "${ContainerName}:/tmp/chinook.sql" | Out-Null
        Invoke-Docker exec $ContainerName psql -U postgres -d postgres `
            -v ON_ERROR_STOP=1 -f /tmp/chinook.sql | Out-Null
        Invoke-Docker exec $ContainerName psql -U postgres -d postgres `
            -v ON_ERROR_STOP=1 -c 'ALTER DATABASE chinook RENAME TO anonymyzer_chinook;' | Out-Null

        $pagilaRoot = Join-Path $temporaryRoot 'pagila-pagila-v3.1.0'
        Invoke-Docker cp (Join-Path $pagilaRoot 'pagila-schema.sql') `
            "${ContainerName}:/tmp/pagila-schema.sql" | Out-Null
        Invoke-Docker cp (Join-Path $pagilaRoot 'pagila-data.sql') `
            "${ContainerName}:/tmp/pagila-data.sql" | Out-Null
        Invoke-Docker exec $ContainerName createdb -U postgres anonymyzer_pagila | Out-Null
        Invoke-Docker exec $ContainerName psql -U postgres -d anonymyzer_pagila `
            -v ON_ERROR_STOP=1 -f /tmp/pagila-schema.sql | Out-Null
        Invoke-Docker exec $ContainerName psql -U postgres -d anonymyzer_pagila `
            -v ON_ERROR_STOP=1 -f /tmp/pagila-data.sql | Out-Null

        foreach ($databaseName in $databaseNames) {
            Add-Marker $databaseName ([Guid]::NewGuid())
        }
    }
    finally {
        $resolvedTemporaryRoot = [IO.Path]::GetFullPath($temporaryRoot)
        $systemTemporaryRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
        if ((Test-Path -LiteralPath $resolvedTemporaryRoot) `
            -and $resolvedTemporaryRoot.StartsWith(
                $systemTemporaryRoot,
                [StringComparison]::OrdinalIgnoreCase)) {
            Remove-Item -LiteralPath $resolvedTemporaryRoot -Recurse -Force
        }
    }
}

function New-Configurations {
    $hostPort = Get-PublishedPort
    New-Item -ItemType Directory -Path $configurationRoot -Force | Out-Null

    dotnet build $cliProject --no-restore
    if ($LASTEXITCODE -ne 0) {
        throw 'Anonymyzer CLI build failed.'
    }

    $previousConnection = $env:ANONYMYZER_POSTGRES_SAMPLE_CONNECTION
    try {
        foreach ($databaseName in $databaseNames) {
            $markerId = Get-MarkerId $databaseName
            $env:ANONYMYZER_POSTGRES_SAMPLE_CONNECTION =
                "Host=127.0.0.1;Port=$hostPort;Username=postgres;Password=$Password;" +
                "Database=$databaseName;Pooling=false"
            $outputPath = Join-Path $configurationRoot "$databaseName.json"
            dotnet run --project $cliProject --no-build -- generate-config `
                --engine PostgreSql `
                --database $databaseName `
                --connection-env ANONYMYZER_POSTGRES_SAMPLE_CONNECTION `
                --marker-id $markerId `
                --output $outputPath `
                --force
            if ($LASTEXITCODE -ne 0) {
                throw "Configuration generation failed for '$databaseName'."
            }
        }
    }
    finally {
        $env:ANONYMYZER_POSTGRES_SAMPLE_CONNECTION = $previousConnection
    }
}

function Show-Status {
    $existingContainer = Get-ContainerName
    if (-not $existingContainer) {
        Write-Output "Container '$ContainerName' does not exist."
        return
    }

    Assert-OwnedContainer $existingContainer
    $state = docker inspect --format '{{.State.Status}}' $ContainerName
    $hostPort = if ($state -eq 'running') { Get-PublishedPort } else { $null }
    foreach ($databaseName in $databaseNames) {
        $exists = $state -eq 'running' -and (Test-DatabaseExists $databaseName)
        [pscustomobject]@{
            Container = $ContainerName
            State = $state
            Host = '127.0.0.1'
            Port = $hostPort
            Database = $databaseName
            Ready = $exists
            Configuration = Join-Path $configurationRoot "$databaseName.json"
        }
    }
}

if ($Action -eq 'Status') {
    Show-Status
    return
}

$existingContainer = Get-ContainerName
if ($Action -eq 'Remove') {
    if (-not $existingContainer) {
        Write-Output "Container '$ContainerName' is already absent."
        return
    }

    Assert-OwnedContainer $existingContainer
    Invoke-Docker rm -f $ContainerName | Out-Null
    foreach ($databaseName in $databaseNames) {
        $configurationPath = Join-Path $configurationRoot "$databaseName.json"
        if (Test-Path -LiteralPath $configurationPath) {
            Remove-Item -LiteralPath $configurationPath -Force
        }
    }

    Write-Output "Removed owned sample container '$ContainerName' and its generated configurations."
    return
}

$createdContainer = $false
try {
    if ($existingContainer) {
        Assert-OwnedContainer $existingContainer
        $state = docker inspect --format '{{.State.Status}}' $ContainerName
        if ($state -ne 'running') {
            Invoke-Docker start $ContainerName | Out-Null
        }
        Wait-PostgreSql
    }
    else {
        Invoke-Docker run -d --name $ContainerName `
            --label "$environmentLabel=$environmentLabelValue" `
            -e "POSTGRES_PASSWORD=$Password" `
            -p '127.0.0.1::5432' `
            $Image | Out-Null
        $createdContainer = $true
        Wait-PostgreSql
        Import-Samples
    }

    foreach ($databaseName in $databaseNames) {
        if (-not (Test-DatabaseExists $databaseName)) {
            throw "Owned container '$ContainerName' is missing database '$databaseName'."
        }
    }

    New-Configurations
    Show-Status
}
catch {
    if ($createdContainer) {
        docker rm -f $ContainerName 2>$null | Out-Null
    }

    throw
}
