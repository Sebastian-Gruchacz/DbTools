[CmdletBinding()]
param(
    [ValidateSet('Initialize', 'Status', 'Remove')]
    [string] $Action = 'Initialize',

    [string] $Image = 'mcr.microsoft.com/mssql/server:2022-latest',

    [string] $ContainerName = 'dbtools-sqlserver-samples',

    [string] $Password = 'DbTools_Public_Samples_2026!'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$environmentLabel = 'com.dbtools.sample-environment'
$environmentLabelValue = 'sqlserver-public-samples'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$sampleRoot = Join-Path $repositoryRoot 'artifacts\sample-databases'
$configurationRoot = Join-Path $repositoryRoot 'artifacts\sample-configurations\sqlserver'
$cliProject = Join-Path $repositoryRoot 'src\Anonymyzer\Anonymyzer.Console\Anonymyzer.Console.csproj'
$markerScript = Join-Path $PSScriptRoot 'markers\sqlserver.sql'
$chinookScript = Join-Path $sampleRoot 'Chinook_SqlServer.sql'
$northwindScript = Join-Path $sampleRoot 'Northwind-SqlServer.sql'
$adventureWorksBackup = Join-Path $sampleRoot 'AdventureWorksLT2022.bak'
$sqlCmd = '/opt/mssql-tools18/bin/sqlcmd'
$databaseNames = @(
    'anonymyzer_chinook_sqlserver',
    'anonymyzer_northwind',
    'anonymyzer_adventureworkslt'
)

function Invoke-Docker {
    & docker @args
    if ($LASTEXITCODE -ne 0) {
        throw "Docker command failed with exit code ${LASTEXITCODE}: docker $($args -join ' ')"
    }
}

function Invoke-SqlCmd {
    param(
        [string] $Database = 'master',
        [string] $Query,
        [string] $InputFile,
        [string[]] $Variables = @(),
        [switch] $Capture
    )

    $arguments = @(
        'exec',
        '-e', "SQLCMDPASSWORD=$Password",
        $ContainerName,
        $sqlCmd,
        '-S', 'localhost',
        '-U', 'sa',
        '-No',
        '-b',
        '-d', $Database
    )
    if ($Query) {
        $arguments += @('-Q', $Query)
    }
    if ($InputFile) {
        $arguments += @('-i', $InputFile)
    }
    foreach ($variable in $Variables) {
        $arguments += @('-v', $variable)
    }
    if ($Capture) {
        $arguments += @('-h', '-1', '-W')
    }

    $previousErrorActionPreference = $ErrorActionPreference
    try {
        $ErrorActionPreference = 'Continue'
        $output = & docker @arguments 2>&1
        $sqlCmdExitCode = $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $previousErrorActionPreference
    }
    if ($sqlCmdExitCode -ne 0) {
        $details = (@($output) -join [Environment]::NewLine).Trim()
        throw "sqlcmd failed for database '$Database' with exit code $sqlCmdExitCode.$([Environment]::NewLine)$details"
    }

    if ($Capture) {
        return $output
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
        throw "Container '$Name' exists but is not owned by the DbTools SQL Server sample environment."
    }
}

function Wait-SqlServer {
    foreach ($attempt in 1..60) {
        $readyExitCode = 1
        try {
            docker exec -e "SQLCMDPASSWORD=$Password" $ContainerName $sqlCmd `
                -S localhost -U sa -No -b -Q 'SELECT 1;' *> $null
            $readyExitCode = $LASTEXITCODE
        }
        catch {
            $readyExitCode = 1
        }

        if ($readyExitCode -eq 0) {
            return
        }

        Start-Sleep -Seconds 1
    }

    throw "SQL Server container '$ContainerName' did not become ready."
}

function Get-PublishedPort {
    $publishedPort = docker port $ContainerName 1433/tcp | Select-Object -First 1
    $portMatch = [regex]::Match(([string]$publishedPort).Trim(), ':(?<Port>[0-9]+)$')
    if (-not $portMatch.Success) {
        throw "Could not parse the SQL Server host port from: $publishedPort"
    }

    return $portMatch.Groups['Port'].Value
}

function Test-DatabaseExists {
    param([string] $DatabaseName)

    $result = Invoke-SqlCmd -Capture -Query `
        "SET NOCOUNT ON; SELECT CASE WHEN DB_ID(N'$DatabaseName') IS NULL THEN 0 ELSE 1 END;"
    return (@($result | ForEach-Object { $_.Trim() } | Where-Object { $_ }) | Select-Object -Last 1) -eq '1'
}

function Get-MarkerId {
    param([string] $DatabaseName)

    $result = Invoke-SqlCmd -Database $DatabaseName -Capture -Query `
        'SET NOCOUNT ON; SELECT CONVERT(varchar(36), MarkerId) FROM dbo.__AnonymyzerDetachedCopy;'
    $marker = @($result | ForEach-Object { $_.Trim() } | Where-Object { $_ }) | Select-Object -Last 1
    $parsedMarker = [Guid]::Empty
    if (-not [Guid]::TryParse($marker, [ref]$parsedMarker)) {
        throw "Database '$DatabaseName' does not contain a valid detached-copy marker."
    }

    return $marker
}

function Add-Marker {
    param(
        [string] $DatabaseName,
        [Guid] $MarkerId
    )

    Invoke-SqlCmd -Database $DatabaseName -InputFile '/tmp/anonymyzer-marker.sql' `
        -Variables @("MarkerId=$($MarkerId.ToString('D'))")
}

function Rename-Database {
    param(
        [string] $SourceName,
        [string] $TargetName
    )

    Invoke-SqlCmd -Query (
        "ALTER DATABASE [$SourceName] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; " +
        "ALTER DATABASE [$SourceName] MODIFY NAME = [$TargetName]; " +
        "ALTER DATABASE [$TargetName] SET MULTI_USER;"
    )
}

function Import-Samples {
    if (-not (Test-Path -LiteralPath $chinookScript) `
        -or -not (Test-Path -LiteralPath $northwindScript) `
        -or -not (Test-Path -LiteralPath $adventureWorksBackup)) {
        & (Join-Path $PSScriptRoot 'Get-SampleDatabases.ps1') `
            -Sample Chinook, Northwind, AdventureWorksLT
    }
    if (-not (Test-Path -LiteralPath $chinookScript) `
        -or -not (Test-Path -LiteralPath $northwindScript) `
        -or -not (Test-Path -LiteralPath $adventureWorksBackup)) {
        throw 'Could not acquire the SQL Server sample files.'
    }

    Invoke-Docker cp $chinookScript "${ContainerName}:/tmp/chinook.sql" | Out-Null
    Invoke-Docker cp $northwindScript "${ContainerName}:/tmp/northwind.sql" | Out-Null
    Invoke-Docker cp $adventureWorksBackup "${ContainerName}:/tmp/AdventureWorksLT2022.bak" | Out-Null
    Invoke-Docker cp $markerScript "${ContainerName}:/tmp/anonymyzer-marker.sql" | Out-Null

    Invoke-SqlCmd -InputFile '/tmp/chinook.sql'
    Rename-Database 'Chinook' 'anonymyzer_chinook_sqlserver'

    Invoke-SqlCmd -Query 'CREATE DATABASE [anonymyzer_northwind];'
    Invoke-SqlCmd -Database 'anonymyzer_northwind' -InputFile '/tmp/northwind.sql'

    Invoke-SqlCmd -Query (
        "RESTORE DATABASE [anonymyzer_adventureworkslt] " +
        "FROM DISK = N'/tmp/AdventureWorksLT2022.bak' WITH " +
        "MOVE N'AdventureWorksLT2022_Data' TO N'/var/opt/mssql/data/anonymyzer_adventureworkslt.mdf', " +
        "MOVE N'AdventureWorksLT2022_Log' TO N'/var/opt/mssql/data/anonymyzer_adventureworkslt_log.ldf', " +
        "RECOVERY, STATS = 10;"
    )

    foreach ($databaseName in $databaseNames) {
        Add-Marker $databaseName ([Guid]::NewGuid())
    }
}

function New-Configurations {
    $hostPort = Get-PublishedPort
    New-Item -ItemType Directory -Path $configurationRoot -Force | Out-Null

    dotnet build $cliProject --no-restore
    if ($LASTEXITCODE -ne 0) {
        throw 'Anonymyzer CLI build failed.'
    }

    $previousConnection = $env:ANONYMYZER_SQLSERVER_SAMPLE_CONNECTION
    try {
        foreach ($databaseName in $databaseNames) {
            $markerId = Get-MarkerId $databaseName
            $env:ANONYMYZER_SQLSERVER_SAMPLE_CONNECTION =
                "Server=127.0.0.1,$hostPort;User ID=sa;Password=$Password;" +
                "Database=$databaseName;Encrypt=false;Pooling=false"
            $outputPath = Join-Path $configurationRoot "$databaseName.json"
            dotnet run --project $cliProject --no-build -- generate-config `
                --engine SqlServer `
                --database $databaseName `
                --connection-env ANONYMYZER_SQLSERVER_SAMPLE_CONNECTION `
                --marker-id $markerId `
                --output $outputPath `
                --force
            if ($LASTEXITCODE -ne 0) {
                throw "Configuration generation failed for '$databaseName'."
            }
        }
    }
    finally {
        $env:ANONYMYZER_SQLSERVER_SAMPLE_CONNECTION = $previousConnection
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
        Wait-SqlServer
    }
    else {
        Invoke-Docker run -d --name $ContainerName `
            --hostname $ContainerName `
            --label "$environmentLabel=$environmentLabelValue" `
            -e 'ACCEPT_EULA=Y' `
            -e "MSSQL_SA_PASSWORD=$Password" `
            -e 'MSSQL_PID=Developer' `
            -p '127.0.0.1::1433' `
            $Image | Out-Null
        $createdContainer = $true
        Wait-SqlServer
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
