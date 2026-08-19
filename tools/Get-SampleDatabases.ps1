[CmdletBinding()]
param(
    [ValidateSet('Chinook', 'Northwind', 'AdventureWorksLT', 'Pagila', 'WideWorldImporters')]
    [string[]] $Sample = @('Chinook', 'Northwind', 'AdventureWorksLT', 'Pagila'),

    [switch] $IncludeLarge,

    [switch] $Force,

    [string] $Destination = (Join-Path $PSScriptRoot '..\artifacts\sample-databases')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$catalog = @(
    [pscustomobject]@{
        Sample = 'Chinook'
        FileName = 'Chinook_SqlServer.sql'
        Uri = 'https://github.com/lerocha/chinook-database/releases/download/v1.4.5/Chinook_SqlServer.sql'
        Length = 601344L
        Sha256 = '5EA75C9E925EAD917D3FABEA6ED3CC8C1FF1D036B61E915C94631AAFA2B0802B'
    },
    [pscustomobject]@{
        Sample = 'Chinook'
        FileName = 'Chinook_PostgreSql.sql'
        Uri = 'https://github.com/lerocha/chinook-database/releases/download/v1.4.5/Chinook_PostgreSql.sql'
        Length = 600200L
        Sha256 = 'E3FDE5C1A5B51A2A91429A702C9CA6E69BA56E6C7F5E112724D70C3D03DB695E'
    },
    [pscustomobject]@{
        Sample = 'Northwind'
        FileName = 'Northwind-SqlServer.sql'
        Uri = 'https://raw.githubusercontent.com/microsoft/sql-server-samples/1ab31bc560415b570d57bb5ff9896f4698891321/samples/databases/northwind-pubs/instnwnd.sql'
        Length = 1049720L
        Sha256 = '3CC62B3FCA6D244A47DBDE698B809331E4F85988A0685B2B370717D431E94871'
    },
    [pscustomobject]@{
        Sample = 'AdventureWorksLT'
        FileName = 'AdventureWorksLT2022.bak'
        Uri = 'https://github.com/microsoft/sql-server-samples/releases/download/adventureworks/AdventureWorksLT2022.bak'
        Length = 8511488L
        Sha256 = '7E1AE09A08EE0342781BACFBA9DA79E91B67991AABCD1A42454A3B64EB60D626'
    },
    [pscustomobject]@{
        Sample = 'Pagila'
        FileName = 'pagila-v3.1.0.tar.gz'
        Uri = 'https://github.com/devrimgunduz/pagila/archive/refs/tags/pagila-v3.1.0.tar.gz'
        Length = 26695598L
        Sha256 = 'ED732E900089797162A53A3C3B34DECF2865631054552EF22D9416DF39605A62'
    },
    [pscustomobject]@{
        Sample = 'WideWorldImporters'
        FileName = 'WideWorldImporters-Standard.bak'
        Uri = 'https://github.com/microsoft/sql-server-samples/releases/download/wide-world-importers-v1.0/WideWorldImporters-Standard.bak'
        Length = 126951424L
        Sha256 = '066279A8CD28C8D85CBD8215EA71A5D672B420CFBC19756B635C27BD8027DADA'
    }
)

$selectedSamples = @($Sample)
if ($IncludeLarge -and 'WideWorldImporters' -notin $selectedSamples) {
    $selectedSamples += 'WideWorldImporters'
}

$downloads = @($catalog | Where-Object Sample -In $selectedSamples)
if ($downloads.Count -eq 0) {
    throw 'No sample databases were selected.'
}

$destinationPath = [System.IO.Path]::GetFullPath($Destination)
New-Item -ItemType Directory -Path $destinationPath -Force | Out-Null

foreach ($download in $downloads) {
    $targetPath = Join-Path $destinationPath $download.FileName
    if (Test-Path -LiteralPath $targetPath) {
        $actualHash = (Get-FileHash -LiteralPath $targetPath -Algorithm SHA256).Hash
        if ($actualHash -eq $download.Sha256) {
            Write-Host "Verified $($download.FileName); download skipped."
            continue
        }

        if (-not $Force) {
            throw "Existing file '$targetPath' has an unexpected SHA-256. Use -Force to replace this exact file."
        }
    }

    $partialPath = "$targetPath.partial"
    try {
        if (Test-Path -LiteralPath $partialPath) {
            Remove-Item -LiteralPath $partialPath -Force
        }

        Write-Host "Downloading $($download.FileName)..."
        & curl.exe --fail --location --retry 3 --silent --show-error `
            --output $partialPath $download.Uri
        if ($LASTEXITCODE -ne 0) {
            throw "Download failed with curl exit code $LASTEXITCODE."
        }

        $actualLength = (Get-Item -LiteralPath $partialPath).Length
        if ($actualLength -ne $download.Length) {
            throw "Unexpected length for $($download.FileName): $actualLength, expected $($download.Length)."
        }

        $actualHash = (Get-FileHash -LiteralPath $partialPath -Algorithm SHA256).Hash
        if ($actualHash -ne $download.Sha256) {
            throw "Unexpected SHA-256 for $($download.FileName): $actualHash."
        }

        Move-Item -LiteralPath $partialPath -Destination $targetPath -Force
    }
    finally {
        if (Test-Path -LiteralPath $partialPath) {
            Remove-Item -LiteralPath $partialPath -Force
        }
    }
}

$downloads | ForEach-Object {
    $path = Join-Path $destinationPath $_.FileName
    [pscustomobject]@{
        Sample = $_.Sample
        Path = $path
        Bytes = (Get-Item -LiteralPath $path).Length
        Sha256 = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
    }
}
