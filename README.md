# DbTools

Zbiór małych narzędzi bazodanowych. Aktywną gałęzią roboczą jest obecnie
`anonymyzator`: zawiera działający `ScriptCut` oraz rozpoczęty anonimizator dla
SQL Servera.

## Wymagania

- .NET SDK 10 do budowania `ScriptCut`;
- SQL Server i `sqlcmd` dostępny w `PATH` do uruchamiania wygenerowanych paczek;
- dla starego kodu anonimizatora: .NET 6 SDK oraz dostęp do pakietów NuGet.

Budowanie całego rozwiązania:

```powershell
dotnet restore .\src\DbTools.sln
dotnet build .\src\DbTools.sln
```

Anonimizator nadal celuje w niewspierany już `net6.0`; jego migracja jest na
liście dalszych prac. `ScriptCut` celuje w `net10.0`.

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

### Architektura

- `Anonymyzer.Base` — kontrakty silnika, metadanych i generatorów;
- `Anonymyzer.SqlServer` — połączenia i odczyt tabel, kolumn tekstowych oraz PK;
- `Anonymyzer.Generators.Simple` — rejestracja generatora `TextShuffler`;
- `Anonymyzer.Console` — DI, generowanie konfiguracji i przyszłe wykonanie.

### Co działa

- budowanie połączenia do SQL Servera;
- odczyt tabel, tekstowych kolumn i informacji o kluczu głównym;
- generowanie pliku JSON z domyślnie wyłączonymi tabelami i kolumnami;
- rejestracja generatorów i eksport ich domyślnej konfiguracji.

### Czego jeszcze nie ma

- publicznego CLI — `Program.cs` zawiera na razie lokalne, wpisane na sztywno
  parametry komputera autora;
- wykonania konfiguracji (`ProcessAnonymyzerCommand` jest szkieletem);
- implementacji `TextShuffler` (`BuildColumnWriter` zgłasza
  `NotImplementedException`);
- testów automatycznych i bezpiecznego trybu `dry-run`;
- obsługi innych schematów danych, XML/JSON oraz zmian PK/FK;
- strategii wyłączania i odbudowy indeksów, constraintów i triggerów.

Aktualny build przechodzi, ale zgłasza dług techniczny: projekty anonimizatora
celują w EOL `net6.0`, `System.Data.SqlClient` 4.8.3 ma zgłoszone podatności, a
modele konfiguracji i `GeneratorBase` generują ostrzeżenia nullable.

Nie ma więc obecnie wspieranego wywołania CLI anonimizatora. Nie należy
uruchamiać `Anonymyzer.Console` bez przejrzenia `Program.cs`: program próbuje
połączyć się z wpisanym tam serwerem i nadpisać wskazany plik konfiguracji.

## Stan gałęzi

| Gałąź | Zawartość | Ocena |
| --- | --- | --- |
| `master` | historyczna wersja `ScriptCut` | punkt bazowy, zastąpiony przez nowsze prace |
| `some_changes` | obsługa triggerów i BAT w `ScriptCut` | w całości jest przodkiem `anonymyzator`; można później usunąć ref |
| `anonymyzator` | `ScriptCut` oraz szkic anonimizatora | właściwa gałąź do dalszej pracy |
| `gateway` | `TimeGateService` i ręczny `TestConsole` | osobny eksperyment Windows Service z 2019 r.; nie scalać z DbTools bez decyzji produktowej |

`gateway` instaluje usługę, która według reguł czasu może wymusić wyłączenie
komputera. Kod nie ma testów automatycznych, używa starego modelu projektu .NET
Framework i nie został zweryfikowany na obecnym środowisku. Jeśli jest nadal
potrzebny, lepiej wydzielić go do osobnego repozytorium; w przeciwnym razie
zachować branch jako archiwum. Historyczny `setup.bat` kopiuje EXE do
`C:\Program Files\Obscure` i wywołuje `InstallUtil`; jest to instalator usługi,
nie bezpieczne CLI diagnostyczne. `TestConsole` tylko ręcznie symuluje kontrolę
czasu. Dostępne reguły to domyślne godziny pracy, weekend i wakacje.

## Proponowana kolejka

1. Dodać testy regresyjne `ScriptCut` dla wielu tabel, tabel bez
   `IDENTITY_INSERT`, pustego wejścia i znaków niedozwolonych w nazwie pliku.
2. Zamienić wpisane na sztywno parametry `Anonymyzer.Console` na jawne komendy
   `generate-config` i `run`; dodać walidację bez łączenia z bazą.
3. Zmigrować anonimizator do .NET 10, przejść z przestarzałego
   `System.Data.SqlClient` na wspierany provider i usunąć ostrzeżenia nullable;
   ujednolicić nazwy `Anonymyzer` / `Anonymization` bez zmiany zachowania.
4. Zrealizować mały pionowy wycinek: jedna tabela, tekstowe kolumny spoza PK,
   deterministyczny generator, batche, `dry-run` i test na lokalnej bazie.
5. Dopiero po pomiarach dodać planowanie zależności FK, mapowanie zmienianych
   kluczy, indeksy, XML/JSON i generatory odwołujące się do innych wierszy.

Najbardziej opłacalny następny krok to punkt 2, a potem pionowy wycinek z punktu
4. Próba rozwiązania od razu zmian PK/FK i wszystkich wariantów indeksów
utrudniłaby zweryfikowanie podstawowego przepływu.
