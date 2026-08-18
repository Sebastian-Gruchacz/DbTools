# Publiczne bazy przykładowe

Pliki źródłowe i backupy są pobierane do ignorowanego przez Git katalogu
`artifacts/sample-databases`. Repozytorium przechowuje tylko skrypt z przypiętymi
wersjami, rozmiarami i sumami SHA-256:

```powershell
.\tools\Get-SampleDatabases.ps1
```

Domyślny zestaw zajmuje około 35 MiB. Cięższy WideWorldImporters można dołączyć
osobno:

```powershell
.\tools\Get-SampleDatabases.ps1 -IncludeLarge
```

Parametr `-Sample` ogranicza pobieranie, na przykład:

```powershell
.\tools\Get-SampleDatabases.ps1 -Sample Chinook,Pagila
```

Skrypt nie tworzy baz, nie łączy się z serwerem i nie uruchamia anonimizatora.
Istniejący plik o prawidłowej sumie jest tylko weryfikowany. Plik o innej sumie
nie jest nadpisywany bez jawnego `-Force`.

## Zestaw

| Próbka | Silnik | Po co ją mamy | Wersja i licencja |
| --- | --- | --- | --- |
| Chinook | SQL Server i PostgreSQL | Ten sam model po obu stronach: osoby, adresy, telefony, e-maile i faktury; dobry test zgodności detekcji providerów. | 1.4.5, MIT |
| Northwind | SQL Server | Mały model relacyjny oraz stare typy `ntext`, `image` i `money`; dobry test zgodności wstecznej i pól `MAX`/LOB. | commit `1ab31bc`, MIT |
| AdventureWorksLT | SQL Server | Lżejsza, nowocześniejsza baza osób, klientów, adresów i sprzedaży; backup łatwy do wielokrotnego odtworzenia. | SQL Server 2022, MIT |
| Pagila | PostgreSQL | Relacje wypożyczalni, triggery, full-text oraz osobny wariant JSONB; archiwum zawiera schematy i dane w kilku formatach. | 3.1.0, PostgreSQL License |
| WideWorldImporters | SQL Server | Większy model OLTP i trudniejsze cechy SQL Servera; przydatny później do wydajności, kolejności i ograniczeń. | 1.0 Standard, MIT |

Źródła: [Microsoft SQL Server Samples](https://github.com/microsoft/sql-server-samples),
[Chinook](https://github.com/lerocha/chinook-database/releases/tag/v1.4.5) oraz
[Pagila](https://github.com/devrimgunduz/pagila/releases/tag/pagila-v3.1.0).

Chinook i Pagila zostały próbnie zaimportowane do jednorazowego kontenera
PostgreSQL 17. Import zakończył się poprawnie; Chinook zawiera 59 klientów i 412
faktur, a Pagila 599 klientów i 16 044 wypożyczenia. Kontener po teście usunięto.
Backupy SQL Servera są pobrane i zweryfikowane sumami, ale nie zostały jeszcze
odtworzone — wymagają osobnego, jawnie wskazanego kontenera lub instancji.

## Lokalne środowisko PostgreSQL

Chinook i Pagila mogą działać równocześnie w dedykowanym kontenerze, powiązanym
z configami przez dwa niezależne markery:

```powershell
.\tools\Invoke-PostgreSqlSampleEnvironment.ps1 -Action Initialize
```

Skrypt importuje bazy `anonymyzer_chinook` i `anonymyzer_pagila`, publikuje losowy
port wyłącznie na `127.0.0.1` oraz generuje configi w ignorowanym katalogu
`artifacts/sample-configurations/postgresql`. Connection string jest ustawiany
tylko w procesie skryptu i nie trafia do plików JSON.

Ponowne `Initialize` wykorzystuje istniejący, oznaczony kontener i odświeża
configi. Obcy kontener o tej samej nazwie jest odrzucany. Stan i bezpieczne
usunięcie wyłącznie własnego kontenera obsługują jawne akcje:

```powershell
.\tools\Invoke-PostgreSqlSampleEnvironment.ps1 -Action Status
.\tools\Invoke-PostgreSqlSampleEnvironment.ps1 -Action Remove
```

`Remove` usuwa również dwa powiązane configi, aby nie pozostawić JSON-ów z
nieaktualnymi markerami. Hasło dotyczy wyłącznie publicznych danych testowych.
Po odczytaniu portu przez `Status` połączenie dla CLI lub podglądów UI można
ustawić w bieżącej sesji, zmieniając nazwę bazy zależnie od configu:

```powershell
$env:ANONYMYZER_POSTGRES_SAMPLE_CONNECTION =
  'Host=127.0.0.1;Port=<PORT>;Username=postgres;' +
  'Password=dbtools_public_samples_only;Database=anonymyzer_chinook;Pooling=false'
```

Aktualny baseline detektora po wygenerowaniu configów:

| Baza | Tabele | Kandydaci | Automatycznie włączone kolumny |
| --- | ---: | ---: | ---: |
| `anonymyzer_chinook` | 10 | 21 | 0 |
| `anonymyzer_pagila` | 22 | 13 | 0 |

Oba wygenerowane JSON-y przeszły `run --dry-run` ze swoimi markerami; żadne dane
nie zostały zmodyfikowane.

## Lokalne środowisko SQL Server

Chinook, Northwind, AdventureWorksLT i WideWorldImporters mogą działać w
oddzielnym kontenerze SQL Server 2022 Developer:

```powershell
.\tools\Invoke-SqlServerSampleEnvironment.ps1 -Action Initialize
.\tools\Invoke-SqlServerSampleEnvironment.ps1 -Action Status
.\tools\Invoke-SqlServerSampleEnvironment.ps1 -Action Remove
```

Środowisko używa wyłącznie oficjalnego obrazu
`mcr.microsoft.com/mssql/server:2022-latest`, publikuje losowy port na
`127.0.0.1` i rozpoznaje własny kontener po etykiecie. Configi trafiają do
`artifacts/sample-configurations/sqlserver`; connection string nie jest w nich
zapisywany. `Remove` usuwa własny kontener oraz powiązane configi.

Aktualny baseline detektora po wygenerowaniu configów:

- Chinook: 10 tabel, 21 kandydatów, 0 automatycznie włączonych kolumn;
- Northwind: 11 tabel, 24 kandydatów, 0 automatycznie włączonych kolumn;
- AdventureWorksLT: 12 tabel, 11 kandydatów, 0 automatycznie włączonych kolumn;
- WideWorldImporters: 48 tabel, 46 kandydatów, 0 automatycznie włączonych kolumn.

Wszystkie cztery JSON-y przeszły `run --dry-run` ze swoimi markerami; żadne dane
nie zostały zmodyfikowane. Kandydaci pozostają wyłączeni do decyzji operatora.

Przed restore WideWorldImporters skrypt wymaga co najmniej 4 GiB wolnego miejsca,
mapuje osobno `WWI_Primary`, `WWI_UserData` i `WWI_Log`, a po zakończeniu usuwa
kopię backupu z `/tmp`. Istniejące środowisko jest rozszerzane bez odtwarzania
pozostałych trzech baz.

## Zasady bezpieczeństwa

Pobrany plik jest seedem, a nie bazą przeznaczoną do bezpośredniej anonimizacji.
Scenariusz integracyjny powinien zawsze:

1. utworzyć lub odtworzyć bazę pod nową, jednoznaczną nazwą;
2. oznaczyć dopiero tę kopię markerem odłączonego klona;
3. wygenerować konfigurację dla kopii;
4. uruchomić `--dry-run`, a później `--execute` wyłącznie na tej kopii;
5. usunąć kopię po teście, pozostawiając seed bez zmian.

Skrypt środowiska tworzy docelową bazę Northwind przed importem historycznego
skryptu. Backupów AdventureWorksLT i WideWorldImporters nie odtwarza z
`WITH REPLACE` ani pod nazwą istniejącej bazy.

## Proponowana kolejność analizy

1. Chinook na obu silnikach: porównanie kandydatów i wygenerowanych konfiguracji.
2. Northwind: stare typy tekstowe, binarne zdjęcia i niejednoznaczne `Notes`.
3. Pagila: relacje, triggery i pola JSONB.
4. AdventureWorksLT: grupy osoba/adres oraz spójność danych między tabelami.
5. WideWorldImporters: wydajność, checkpointy i zachowanie przy większym modelu.
