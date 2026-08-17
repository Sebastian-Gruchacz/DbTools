# DbTools

Zbiór małych narzędzi bazodanowych. Aktywną gałęzią roboczą jest obecnie
`anonymyzator`: zawiera działający `ScriptCut` oraz rozpoczęty anonimizator dla
SQL Servera i PostgreSQL.

## Wymagania

- .NET SDK 10 do budowania całego rozwiązania i uruchamiania testów;
- SQL Server i `sqlcmd` dostępny w `PATH` do uruchamiania wygenerowanych paczek;
- dostęp do pakietów NuGet;
- opcjonalnie Docker z obrazem PostgreSQL do testu integracyjnego providera.

Budowanie całego rozwiązania:

```powershell
dotnet restore .\src\DbTools.sln
dotnet build .\src\DbTools.sln
dotnet test .\src\DbTools.sln
```

Wszystkie aktywne projekty celują w `net10.0`. Plik `global.json` wybiera
Microsoft Testing Platform, wymagany przez testy xUnit v3 i SDK .NET 10.

## ScriptCut

`ScriptCut` dzieli duży skrypt danych wygenerowany dla SQL Servera na osobny
plik dla każdej tabeli. Początek części rozpoznaje po:

- `SET IDENTITY_INSERT [dbo].[Tabela] ON`, albo
- `INSERT [dbo].[Tabela] ...`.

Treść przed pierwszą rozpoznaną tabelą jest pomijana. Każda część otrzymuje
`USE`, wyłączenie triggerów przed danymi i ponowne włączenie triggerów po nich.
Obok części powstaje `insert_all.bat`, który wykonuje je kolejno przez
`sqlcmd`, zapisuje logi w `output` i zatrzymuje się po pierwszym błędzie.

### CLI

```text
ScriptCut <source.sql> [database] [server]
```

- `source.sql` — wymagany plik wejściowy;
- `database` — opcjonalna nazwa bazy, domyślnie `_database_`;
- `server` — opcjonalny argument `-S` dla `sqlcmd`, domyślnie `.\SQLEXPRESS`;
- `-h` lub `--help` — pomoc.

Przykład bez instalowania narzędzia:

```powershell
dotnet run --project .\src\ScriptCut\ScriptCut.csproj -- `
  "C:\SQL Scripts\scripted-data.sql" "MyDatabase" ".\SQLEXPRESS"
```

Wynik trafi do `C:\SQL Scripts\scripted-data.parts`:

```text
001.FirstTable.sql
002.SecondTable.sql
insert_all.bat
```

Uruchomienie importu:

```powershell
Set-Location "C:\SQL Scripts\scripted-data.parts"
.\insert_all.bat
```

Ograniczenia: parser jest celowo prosty, obsługuje tylko schemat `[dbo]` i
format nawiasów kwadratowych generowany przez SQL Server. Nie parsuje pełnego
dialektu T-SQL. Skrypt wyłącza triggery, więc należy go uruchamiać wyłącznie na
kontrolowanej kopii bazy i sprawdzić logi po imporcie.

Kod ma trzy krótkie odpowiedzialności: obsługa CLI, dzielenie wejścia i zapis
pliku BAT. Nie zależy od frameworka komend ani parsera SQL.

## Anonymyzer

Cel projektu opisuje szerzej [Kwerenda.txt](Kwerenda.txt): wydajna i
konfigurowalna anonimizacja dużych baz, z uwzględnieniem tabel bez prostego PK,
indeksów, relacji oraz generatorów korzystających z innych wartości.

Anonimizator docelowo **modyfikuje bazę w miejscu**, ale jego targetem musi być
odłączona, odtwarzalna kopia robocza — nigdy baza źródłowa ani produkcyjna.
Backup/restore jest osobnym procesem z osobnymi uprawnieniami. Anonimizator nie
powinien nigdy otrzymywać connection stringa produkcji; także konfigurację
generuje ze schematu odłączonej kopii.
Zakładany przepływ, zabezpieczenia komendy `run`, strategię testów oraz pakiety
językowe opisuje [docs/anonymyzer-design.md](docs/anonymyzer-design.md).

### Architektura

- `Anonymyzer.Base` — kontrakty silnika, metadanych i generatorów;
- `Anonymyzer.SqlServer` — połączenia i odczyt tabel, kolumn tekstowych oraz PK;
- `Anonymyzer.PostgreSql` — analogiczny provider oparty na Npgsql i
  `information_schema`;
- `Anonymyzer.PostgreSql.Tests` — testy buildera i opcjonalna integracja z bazą;
- `Anonymyzer.Generators.Simple` — rejestracja generatora `TextShuffler`;
- `Anonymyzer.Console` — DI, generowanie konfiguracji i przyszłe wykonanie.

### Co działa

- budowanie połączeń do SQL Servera i PostgreSQL;
- odczyt schematów, tabel, tekstowych kolumn, nullowalności i informacji o PK;
- generowanie pliku JSON z domyślnie wyłączonymi tabelami i kolumnami;
- rejestracja generatorów i eksport ich domyślnej konfiguracji;
- test integracyjny odczytu metadanych PostgreSQL 17 na tymczasowej bazie.

### Czego jeszcze nie ma

- publicznego CLI — `Program.cs` kończy się obecnie bez próby połączenia, aby
  nie dało się przypadkiem uruchomić prototypu na bazie;
- wykonania konfiguracji (`ProcessAnonymyzerCommand` jest szkieletem);
- implementacji `TextShuffler` (`BuildColumnWriter` zgłasza
  `NotImplementedException`);
- testów SQL Servera i bezpiecznego trybu `dry-run`;
- obsługi XML/JSON oraz zmian PK/FK;
- strategii wyłączania i odbudowy indeksów, constraintów i triggerów.

Kod jest na .NET 10. SQL Server używa `Microsoft.Data.SqlClient` 7.0.2, a
PostgreSQL używa Npgsql 10.0.3. Aktualny build przechodzi bez ostrzeżeń.

Nie ma więc obecnie wspieranego wywołania CLI anonimizatora. Uruchomienie
`Anonymyzer.Console` zwraca błąd konfiguracji i nie próbuje łączyć się z bazą.

Provider wybiera pole `DatabaseEngine`: obsługiwane wartości to `SqlServer` i
`PostgreSql`. Konfiguracja formatu `0.2.0` zapisuje nazwę schematu każdej tabeli.

Najprostsze bezpieczne wywołanie integracji tworzy osobny kontener z lokalnego
obrazu, ładuje fixture i zawsze usuwa kontener w bloku `finally`:

```powershell
.\tools\Test-PostgreSqlProvider.ps1
```

Można wskazać inny lokalny obraz przez `-Image`. Sam test korzysta ze zmiennej
`ANONYMYZER_POSTGRES_CONNECTION`; bez niej integracja jest pomijana, a testy
jednostkowe nadal się wykonują. Fixture znajduje się w
`tests/postgresql/init.sql`. Bieżąca implementacja generowania konfiguracji
tylko czyta metadane odłączonej kopii i nie zapisuje connection stringa w JSON.
Przyszła komenda `run` będzie modyfikowała dane w
odłączonej kopii. Testy wykonania muszą tworzyć nową bazę z fixture'a albo
odtwarzać backup/dump (np. Northwind) i nigdy nie mogą wskazywać istniejącej
bazy roboczej użytkownika.

## Stan gałęzi

| Gałąź | Zawartość | Ocena |
| --- | --- | --- |
| `master` | historyczna wersja `ScriptCut` | punkt bazowy, zastąpiony przez nowsze prace |
| `some_changes` | obsługa triggerów i BAT w `ScriptCut` | w całości jest przodkiem `anonymyzator`; można później usunąć ref |
| `anonymyzator` | `ScriptCut` oraz szkic anonimizatora | właściwa gałąź do dalszej pracy |
| `gateway` | historyczne źródło `TimeGateService` | wydzielone do osobnego repozytorium `J:\GIT\Gateway`; nie scalać do `main` |

Gateway został wydzielony do `J:\GIT\Gateway` wraz z dwoma historycznymi
commitami, osobnym rozwiązaniem, licencją i dokumentacją bezpieczeństwa. Ref
`gateway` w DbTools pozostaje jedynie źródłem historycznym i może zostać
skasowany po wypchnięciu lub zarchiwizowaniu nowego repozytorium.

## Proponowana kolejka

1. Dodać testy regresyjne `ScriptCut` dla wielu tabel, tabel bez
   `IDENTITY_INSERT`, pustego wejścia i znaków niedozwolonych w nazwie pliku.
2. Dodać jawne komendy `generate-config` i `run` z connection stringiem kopii
   podawanym tylko w runtime; przed `run` dodać obowiązkowy marker odłączonej
   kopii, kontrolę oczekiwanej nazwy/identyfikatora bazy i plan `dry-run`.
3. Dodać słowniki kandydatów angielskich i polskich. Mają podpowiadać pola,
   ale nie włączać ich automatycznie bez zatwierdzenia konfiguracji.
4. Ujednolicić historyczne nazwy `Anonymyzer` / `Anonymization` bez zmiany
   zachowania i dodać test integracyjny SQL Servera.
5. Zrealizować mały pionowy wycinek: jedna tabela, tekstowe kolumny spoza PK,
   deterministyczny generator, batche, `dry-run` i test na lokalnej bazie.
6. Dopiero po pomiarach dodać planowanie zależności FK, mapowanie zmienianych
   kluczy, indeksy, XML/JSON i generatory odwołujące się do innych wierszy.

Najbardziej opłacalny następny krok to punkt 2, a potem pionowy wycinek z punktu
5. Próba rozwiązania od razu zmian PK/FK i wszystkich wariantów indeksów
utrudniłaby zweryfikowanie podstawowego przepływu.
