[CmdletBinding()]
param(
    [string] $Image = 'postgres:17-alpine'
)

$ErrorActionPreference = 'Stop'
$containerName = 'dbtools-postgres-' + [Guid]::NewGuid().ToString('N').Substring(0, 12)
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$fixturePath = Join-Path $repositoryRoot 'tests\postgresql\init.sql'
$testProject = Join-Path $repositoryRoot 'src\Anonymyzer\Anonymyzer.PostgreSql.Tests\Anonymyzer.PostgreSql.Tests.csproj'
$cliProject = Join-Path $repositoryRoot 'src\Anonymyzer\Anonymyzer.Console\Anonymyzer.Console.csproj'
$generatedConfig = Join-Path ([IO.Path]::GetTempPath()) ('dbtools-anonymyzer-' + [Guid]::NewGuid().ToString('N') + '.json')
$markerId = '11111111-2222-3333-4444-555555555555'
$previousConnection = $env:ANONYMYZER_POSTGRES_CONNECTION

try {
    docker run -d --name $containerName `
        -e POSTGRES_PASSWORD=dbtools_test_password `
        -e POSTGRES_DB=anonymyzer_test `
        -P $Image | Out-Null

    $ready = $false
    foreach ($attempt in 1..30) {
        docker exec $containerName pg_isready -U postgres -d anonymyzer_test | Out-Null
        if ($LASTEXITCODE -eq 0) {
            $ready = $true
            break
        }

        Start-Sleep -Seconds 1
    }

    if (-not $ready) {
        throw 'PostgreSQL test container did not become ready.'
    }

    docker cp $fixturePath "${containerName}:/tmp/init.sql" | Out-Null
    $fixtureLoaded = $false
    foreach ($attempt in 1..30) {
        $fixtureExitCode = 1
        try {
            docker exec $containerName psql -U postgres -d anonymyzer_test `
                --single-transaction -v ON_ERROR_STOP=1 -f /tmp/init.sql 2>$null | Out-Null
            $fixtureExitCode = $LASTEXITCODE
        }
        catch {
            $fixtureExitCode = 1
        }

        if ($fixtureExitCode -eq 0) {
            $fixtureLoaded = $true
            break
        }

        Start-Sleep -Seconds 1
    }

    if (-not $fixtureLoaded) {
        throw 'Could not load the PostgreSQL fixture after the server became ready.'
    }

    $publishedPort = docker port $containerName 5432 | Select-Object -First 1
    if ($publishedPort -notmatch ':(\d+)$') {
        throw "Could not determine the PostgreSQL host port from: $publishedPort"
    }

    $env:ANONYMYZER_POSTGRES_CONNECTION =
        "Host=127.0.0.1;Port=$($Matches[1]);Username=postgres;Password=dbtools_test_password;Database=anonymyzer_test;Pooling=false"

    dotnet build $testProject --no-restore
    if ($LASTEXITCODE -ne 0) {
        throw "PostgreSQL provider test build failed with exit code $LASTEXITCODE."
    }

    dotnet test $testProject --no-build --no-restore
    if ($LASTEXITCODE -ne 0) {
        throw "PostgreSQL provider tests failed with exit code $LASTEXITCODE."
    }

    dotnet run --project $cliProject --no-build -- generate-config `
        --engine PostgreSql `
        --database anonymyzer_test `
        --connection-env ANONYMYZER_POSTGRES_CONNECTION `
        --marker-id $markerId `
        --output $generatedConfig
    if ($LASTEXITCODE -ne 0) {
        throw "Anonymyzer generate-config failed with exit code $LASTEXITCODE."
    }

    $configuration = Get-Content -Raw -LiteralPath $generatedConfig | ConvertFrom-Json
    $customerData = $configuration.Tables | Where-Object {
        $_.SchemaName -eq 'public' -and $_.TableName -eq 'customer_data'
    }
    $displayName = $customerData.Columns | Where-Object { $_.ColumnName -eq 'display_name' }
    if (-not $displayName.Detection.IsCandidate `
        -or $displayName.Detection.SuggestedRole -ne 'Person.FullName' `
        -or $displayName.Enabled) {
        throw 'Generated config did not contain the expected disabled display_name candidate.'
    }
    if ($configuration.Tables.Enabled -contains $true `
        -or $configuration.Tables.Columns.Enabled -contains $true) {
        throw 'Candidate detection enabled a table or column without operator approval.'
    }

    dotnet run --project $cliProject --no-build -- run `
        --config $generatedConfig `
        --connection-env ANONYMYZER_POSTGRES_CONNECTION `
        --marker-id $markerId `
        --dry-run
    if ($LASTEXITCODE -ne 0) {
        throw "Anonymyzer dry-run failed with exit code $LASTEXITCODE."
    }
}
finally {
    $env:ANONYMYZER_POSTGRES_CONNECTION = $previousConnection
    if (Test-Path -LiteralPath $generatedConfig) {
        Remove-Item -LiteralPath $generatedConfig -Force
    }
    docker rm -f $containerName 2>$null | Out-Null
}
