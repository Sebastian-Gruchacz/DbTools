[CmdletBinding()]
param(
    [string] $Image = 'postgres:17-alpine'
)

$ErrorActionPreference = 'Stop'
$containerName = 'dbtools-postgres-' + [Guid]::NewGuid().ToString('N').Substring(0, 12)
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$fixturePath = Join-Path $repositoryRoot 'tests\postgresql\init.sql'
$testProject = Join-Path $repositoryRoot 'src\Anonymyzer\Anonymyzer.PostgreSql.Tests\Anonymyzer.PostgreSql.Tests.csproj'
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
    docker exec $containerName psql -U postgres -d anonymyzer_test `
        -v ON_ERROR_STOP=1 -f /tmp/init.sql | Out-Null

    $publishedPort = docker port $containerName 5432 | Select-Object -First 1
    if ($publishedPort -notmatch ':(\d+)$') {
        throw "Could not determine the PostgreSQL host port from: $publishedPort"
    }

    $env:ANONYMYZER_POSTGRES_CONNECTION =
        "Host=127.0.0.1;Port=$($Matches[1]);Username=postgres;Password=dbtools_test_password;Database=anonymyzer_test;Pooling=false"

    dotnet test $testProject
    if ($LASTEXITCODE -ne 0) {
        throw "PostgreSQL provider tests failed with exit code $LASTEXITCODE."
    }
}
finally {
    $env:ANONYMYZER_POSTGRES_CONNECTION = $previousConnection
    docker rm -f $containerName 2>$null | Out-Null
}
