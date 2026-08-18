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

- `Anonymyzer.Base` — kontrakty silnika, metadanych oraz sesji generatorów
  `Row`/`Column`/`Relational`;
- `Anonymyzer.Configuration` — współdzielony, niesekretny model konfiguracji;
- `Anonymyzer.SqlServer` — połączenia i odczyt tabel, kolumn tekstowych oraz PK;
- `Anonymyzer.PostgreSql` — analogiczny provider oparty na Npgsql i
  `information_schema`;
- `Anonymyzer.PostgreSql.Tests` — testy buildera i opcjonalna integracja z bazą;
- `Anonymyzer.Generators.Simple` — `TextShuffler`, `FixedText`, `SequentialText`,
  `EmailAddress`, `PhoneNumber`, `Uuid` i `TaxIdentifier`;
- `Anonymyzer.Generators.Person` — generatory spójnej tożsamości oraz krajowego
  identyfikatora osoby;
- `Anonymyzer.Generators.Address` — grupowy generator spójnego adresu pocztowego;
- `Anonymyzer.ConfigEditor.Abstractions` — kontrakt opcjonalnych paneli WPF;
- `Anonymyzer.Generators.Simple.Wpf` — panele konfiguracji generatorów prostych;
- `Anonymyzer.Generators.Person.Wpf` — panel konfiguracji `PersonIdentity` 1.0.0;
- `Anonymyzer.Generators.Address.Wpf` — panel konfiguracji `PostalAddress` 1.0.0;
- `Anonymyzer.LanguagePack.Polish` — dane i reguły regionalne `pl-PL`;
- `Anonymyzer.LanguagePack.English` — reguły `en-US` oraz bezpieczne numery
  telefonów i SSN;
- `Anonymyzer.ConfigEditor` — edytor konfiguracji WPF;
- `Anonymyzer.Console` — DI, generowanie konfiguracji i przyszłe wykonanie.

### Co działa

- budowanie połączeń do SQL Servera i PostgreSQL;
- odczyt schematów, tabel, wszystkich kolumn, nullowalności, ordinali i informacji o PK;
- generowanie pliku JSON z domyślnie wyłączonymi tabelami i kolumnami;
- rejestracja generatorów i eksport ich domyślnej konfiguracji;
- model konfiguracji `0.4.0`: marker odłączonej kopii, role semantyczne,
  wykryci kandydaci, profile
  generatorów oraz grupy wiążące kilka kolumn;
- edytor WPF: New/Open/Save/Save As, wybór tabeli, grid kolumn, dwupoziomowe
  menu ról semantycznych, edycja profili oraz grup wielokolumnowych z mapowaniem
  wyjść generatora na kolumny;
- śledzenie niezapisanych zmian: gwiazdka w tytule oraz `Save / Don't Save /
  Cancel` przed `New`, `Open` i zamknięciem aplikacji;
- wersjonowany kontrakt generatora: własny codec JSON, walidacja, deklaracja
  wymagań danych, przygotowanie stanu i sesja wykonawcza;
- `TextShuffler` 1.0.0: deterministyczna permutacja całej kolumny zachowująca
  dokładny multizbiór wartości oraz opcjonalnie pozycje `NULL`;
- `FixedText` 1.0.0: stała wartość tekstowa z opcjonalnym zachowaniem `NULL`;
- `SequentialText` 1.0.0: unikalny w ramach sesji tekst z prefiksem, sufiksem,
  początkiem numeracji i konfigurowalnym dopełnieniem zerami;
- `EmailAddress` 1.0.0: tryb opaque albo adres oparty na kolumnach imienia i
  nazwiska, z zależnością od ich wartości oryginalnych lub wygenerowanych;
- `PhoneNumber` 1.0.0: deterministyczne formaty `pl-PL` i `en-US`, krajowe albo
  międzynarodowe; wariant amerykański korzysta z zastrzeżonego zakresu 555-0100–0199;
- `Uuid` 1.0.0: deterministyczne tekstowe UUID w formacie hyphenated, compact,
  braced albo parenthesized, z wyborem wielkości liter;
- `TaxIdentifier` 1.0.0: polskie NIP-y oraz REGON 9/14 z poprawnymi cyframi
  kontrolnymi i bez powtórzeń; brak `Variant` w starszym JSON-ie oznacza NIP;
- dedykowane panele WPF parametrów wszystkich siedmiu generatorów prostych;
- rozwijane `Profiles → Add` tworzące kompletny profil domyślny wybranego
  generatora także w starszej konfiguracji;
- `PersonIdentity` 1.0.0 w zakresie `Row`: spójne imię, nazwisko, rodzaj i e-mail
  na podstawie pakietu `pl-PL` albo `en-US`, bez dodatkowego skanu bazy;
- `BirthDate` 1.0.0: deterministyczna data z konfigurowalnego zakresu dla kolumn
  `Date` i `DateTime`, gotowa jako zależność `Generated` dla identyfikatora;
- `NationalIdentifier` 1.0.0: polski PESEL z prawidłową datą, płcią i checksum
  albo bezpiecznie nieprzydzielony amerykański SSN z prefiksem `000`; generator
  obsługuje konfigurowalny zakres dat i seed oraz jawne zależności od kolumn daty
  urodzenia i płci (`Original`/`Generated`);
- `PostalAddress` 1.0.0: atomowo generowane kraj, region, miasto, ulica i kod
  pocztowy dla `pl-PL` albo `en-US`; kod jest wybierany razem z miastem, a nie
  losowany niezależnie;
- dwa schematy e-mail: oparty na imieniu i nazwisku oraz opaque; domyślna domena
  `example.invalid` jest celowo niedostarczalna;
- dedykowany panel WPF konfiguracji `PersonIdentity`;
- bezpieczny podgląd generatorów `Row`, także przypisanych bezpośrednio do jednej
  kolumny, wykonywany w pamięci bez połączenia z bazą;
- niemodalne, tylko-odczytowe okna wartości `non-null` dla dowolnej kolumny z
  konfiguracji: limit 1–50, wiele okien naraz, kopiowanie i ponowna walidacja
  nazwy oraz markera klona przed każdym odczytem;
- prezentacja tekstowego typu kolumny wraz z długością albo `MAX`;
- klasyfikacja typów SQL Server/PostgreSQL oraz kandydaci wykrywani po nazwie
  także dla pól liczbowych, np. PESEL, NIP i telefonów;
- zgodność roli z typem oraz negatywne tokeny odrzucające m.in. booleanowe
  ustawienia, liczbowe FK z dodatkowym `id` i pola kontrolne;
- rozwijane `Add column`, które pokazuje ukryte kolumny zapisane podczas analizy,
  a na końcu pozwala po walidacji klona wczytać z bazy brakujące kolumny
  niebędące PK; dodane pola są domyślnie wyłączone;
- CLI `generate-config` i `run --dry-run`, które pobiera connection string
  wyłącznie ze wskazanej zmiennej środowiskowej;
- potrójna walidacja markera odłączonej kopii: argument operatora, konfiguracja
  i pojedynczy rekord w bazie muszą wskazywać ten sam identyfikator;
- deterministyczny plan `dry-run`: aktywne grupy i kolumny, dokładne wersje oraz
  zakresy generatorów, mapowania wyjść, wymagane skany i batch po 1000 wierszy;
- porównanie aktywnego planu z bieżącym schematem klona przed wykonaniem,
  wraz z estymacją liczby wierszy i górnego zużycia pamięci pełnych skanów;
- deterministyczny detektor kandydatów EN/PL: `snake_case`, `camelCase`,
  prefiksy techniczne, polskie znaki, score i negatywne flagi; propozycje nigdy
  nie ustawiają `Enabled`;
- sortowanie kroków po zależnościach od wartości `Generated`, wraz z odrzucaniem
  brakujących producentów, podwójnych zapisów i cykli;
- testy integracyjne metadanych PostgreSQL 17 na tymczasowej bazie oraz SQL
  Servera na jawnie wskazanym odłączonym klonie.

### Czego jeszcze nie ma

- wykonania konfiguracji modyfikującego dane — `run` przyjmuje obecnie wyłącznie
  `--dry-run` i kończy pracę po walidacji bezpieczeństwa oraz generatorów;
- wykonawcy planu, który dostarczy generatorom strumienie danych i zapisze wynik
  ich sesji do bazy;
- pozostałych generatorów grupowych;
- podglądu generatorów `Column` i `Relational`, które wymagają odczytu danych
  z odłączonego klona;
- pełnych, ważonych zbiorów danych regionalnych;
- automatycznej analizy niestabilnych kolekcji, JSON, XML i tekstu swobodnego;
- obsługi XML/JSON oraz zmian PK/FK;
- strategii wyłączania i odbudowy indeksów, constraintów i triggerów.

Kod jest na .NET 10. SQL Server używa `Microsoft.Data.SqlClient` 7.0.2, a
PostgreSQL używa Npgsql 10.0.3. Aktualny build przechodzi bez ostrzeżeń.

Provider wybiera pole `DatabaseEngine`: obsługiwane wartości to `SqlServer` i
`PostgreSql`. Konfiguracja formatu `0.4.0` zapisuje marker klona, nazwę schematu,
role kolumn, profile generatorów i grupy spójnych danych. Nadal nie zawiera
connection stringa. Edytor celowo odrzuca starsze formaty, zamiast po cichu
utracić pola przy zapisie; konfigurację należy obecnie wygenerować ponownie.

### CLI anonimizatora

Marker należy utworzyć dopiero po odtworzeniu odłączonej kopii. Skrypty są w
`tools/markers`; celowo odmawiają nadpisania istniejącej tabeli markera:

```powershell
$marker = [Guid]::NewGuid()
psql $env:ANONYMYZER_CONNECTION -v marker_id=$marker -f .\tools\markers\postgresql.sql
# albo:
sqlcmd -S .\SQLEXPRESS -d DetachedClone -v MarkerId=$marker -i .\tools\markers\sqlserver.sql
```

Connection string jest przekazywany wyłącznie przez zmienną środowiskową, a CLI
otrzymuje jej nazwę. Nie istnieje argument `--connection-string`:

```powershell
dotnet run --project .\src\Anonymyzer\Anonymyzer.Console -- generate-config `
  --engine PostgreSql --database anonymyzer_clone `
  --connection-env ANONYMYZER_CONNECTION --marker-id $marker `
  --output .\anonymyzer-config.json

dotnet run --project .\src\Anonymyzer\Anonymyzer.Console -- run `
  --config .\anonymyzer-config.json `
  --connection-env ANONYMYZER_CONNECTION --marker-id $marker --dry-run
```

Obie komendy sprawdzają nazwę bazy i marker. `generate-config` tylko czyta
metadane, pomija samą tabelę markera i zapisuje niesekretny JSON. `run --dry-run`
waliduje konfigurację, dokładne wersje generatorów i target, wypisuje kolejność
kroków, mapowania, wymagane pełne skany i proponowany batch. Dodatkowo odrzuca
zmiany schematu aktywnych kolumn oraz pokazuje szacowaną liczbę wierszy i pamięć
pełnych skanów. Dla nieograniczonego `text` pamięć pozostaje jawnie nieznana.
Komenda kończy bez zapisu danych. Wywołanie `run` bez `--dry-run` jest obecnie
odrzucane.

Edytor konfiguracji można uruchomić poleceniem:

```powershell
dotnet run --project .\src\Anonymyzer\Anonymyzer.ConfigEditor\Anonymyzer.ConfigEditor.csproj
```

Kropka `●` przy tabeli lub kolumnie oznacza propozycję automatu, nie zgodę na
anonimizację. Lista tabel pokazuje obok kropki liczbę kandydatów; oba pola mają
stałą szerokość, więc kwalifikowane nazwy tabel pozostają wyrównane. Wszystkie
tabele pozostają dostępne do ręcznej kontroli. Filtr wyszukuje bez rozróżniania
wielkości liter po `schema.table`, obsługuje kilka fragmentów rozdzielonych
spacjami i może ograniczyć listę do tabel z kandydatami. Przycisk
`Edit groups...` tworzy grupy wielokolumnowe i mapuje deklarowane wyjścia
generatora na kolumny. `Refresh sample` uruchamia prawdziwą sesję generatora
`Row` wyłącznie w pamięci. Dla generatorów `Column`, takich jak `TextShuffler`,
UI jawnie pokazuje `requires cloned data`, ponieważ uczciwy podgląd wymaga
odczytu całej kolumny z odłączonego klona. W oknie profili `Configure...`
otwiera panel dostarczony przez dokładną wersję generatora; bez panelu nadal
można edytować jego własny fragment `Options` jako JSON.

Przycisk `View...` w wierszu kolumny otwiera niemodalne okno surowych wartości
`non-null`. Connection string jest pobierany z podanej w oknie zmiennej
środowiskowej (domyślnie `ANONYMYZER_CONNECTION`). Przed zapytaniem edytor
sprawdza nazwę i marker odłączonego klona. Można otworzyć kilka takich okien i
kopiować dane, ale sample nie trafiają do konfiguracji, logów ani plików. Jedno
zapytanie trwa najwyżej 15 sekund, a pojedyncza wyświetlana wartość jest obcinana
po 32 768 znakach i oznaczana jako skrócona.

`Add column` rozwija najpierw kolumny zapisane w konfiguracji podczas analizy,
które nie są kandydatami ani nie zostały jeszcze skonfigurowane. Wybranie pozycji
pokazuje ją w gridzie bez ponownego połączenia z bazą. Ostatnia pozycja menu używa
tego samego bezpiecznego połączenia i walidacji markera, aby pokazać kolumny
istniejące w wybranej tabeli klona, których nie ma w konfiguracji, z pominięciem
klucza głównego. Dodane pola są wyłączone i nie dostają generatora automatycznie;
operator wybiera rolę i profil jawnie.

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
2. Podłączyć podgląd generatorów `Column` do bezpiecznego, tylko-odczytowego
   źródła danych z odłączonego klona.
3. Ujednolicić historyczne nazwy `Anonymyzer` / `Anonymization` bez zmiany
   zachowania.
4. Zrealizować mały pionowy wycinek: jedna tabela, grupa `PersonIdentity`,
   deterministyczny seed, batche, `dry-run` i test na lokalnej bazie.
5. Rozszerzyć pakiety regionalne o ważone dane.
6. Dopiero po pomiarach dodać planowanie zależności FK, mapowanie zmienianych
   kluczy, indeksy, XML/JSON i generatory odwołujące się do innych wierszy.

Najbardziej opłacalny następny krok to punkt 2, a potem pionowy wycinek z punktu
4. Próba rozwiązania od razu zmian PK/FK i wszystkich wariantów indeksów
utrudniłaby zweryfikowanie podstawowego przepływu.
