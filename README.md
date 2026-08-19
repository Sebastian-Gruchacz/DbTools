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

- `Anonymyzer.Base` — kontrakty silnika, metadanych, sesji generatorów oraz
  wersjonowanych pakietów językowych
  `Row`/`Column`/`Relational`;
- `Anonymyzer.Configuration` — współdzielony, niesekretny model konfiguracji;
- `Anonymyzer.SqlServer` — połączenia i odczyt tabel, kolumn tekstowych oraz PK;
- `Anonymyzer.PostgreSql` — analogiczny provider oparty na Npgsql i
  `information_schema`;
- `Anonymyzer.PostgreSql.Tests` — testy buildera i opcjonalna integracja z bazą;
- `Anonymyzer.Generators.Simple` — `TextShuffler`, `FixedText`, `SequentialText`,
  `EmailAddress`, `AccountLogin`, `PhoneNumber`, `Uuid`, `CompanyName`,
  `TaxIdentifier` i `BankAccount`;
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
  dokładny multizbiór wartości oraz opcjonalnie pozycje `NULL`; profil określa
  limit pamięci i zachowanie `Fail` albo `EncryptedTemporaryFiles`;
- `FixedText` 1.0.0: stała wartość tekstowa z opcjonalnym zachowaniem `NULL`;
- `JsonPathRedactor` 1.0.0: selektywna zamiana wartości pod ścieżkami JSON
  w kolumnach tekstowych oraz natywnych PostgreSQL `json/jsonb`, bez ujawniania
  błędnej wartości źródłowej w komunikacie;
- `SequentialText` 1.0.0: unikalny w ramach sesji tekst z prefiksem, sufiksem,
  początkiem numeracji i konfigurowalnym dopełnieniem zerami;
- `EmailAddress` 1.0.0: tryb opaque albo adres oparty na kolumnach imienia i
  nazwiska, z zależnością od ich wartości oryginalnych lub wygenerowanych;
- `AccountLogin` 1.0.0: unikalny login opaque albo złożony z kolumn imienia i
  nazwiska, również ze źródłem `Original`/`Generated`;
- `PhoneNumber` 1.0.0: deterministyczne formaty `pl-PL` i `en-US`, krajowe albo
  międzynarodowe; wariant amerykański korzysta z zastrzeżonego zakresu 555-0100–0199;
- `Uuid` 1.0.0: deterministyczne tekstowe UUID w formacie hyphenated, compact,
  braced albo parenthesized, z wyborem wielkości liter;
- `CompanyName` 1.0.0: unikalne w sesji nazwy `pl-PL`/`en-US` z obowiązkowym
  markerem syntetycznym i opcjonalną lokalną formą prawną;
- `TaxIdentifier` 1.0.0: polskie NIP-y oraz REGON 9/14 z poprawnymi cyframi
  kontrolnymi i bez powtórzeń; brak `Variant` w starszym JSON-ie oznacza NIP;
- `BankAccount` 1.0.0: polski IBAN lub NRB z poprawną sumą modulo 97,
  deterministyczną numeracją i zerowym, celowo nieroutowalnym segmentem banku;
- dedykowane panele WPF parametrów wszystkich wbudowanych generatorów prostych;
- merge profili przy otwieraniu starszej konfiguracji: profile z pliku pozostają
  bez zmian, bieżące profile wbudowane są dodawane pod unikalnymi identyfikatorami,
  a kolumna `Origin` pokazuje ich pochodzenie;
- rozwijane `Profiles → Add` tworzące kompletny profil domyślny wybranego
  generatora także w starszej konfiguracji;
- `PersonIdentity` 1.0.0 w zakresie `Row`: spójne imię, nazwisko, rodzaj i e-mail
  na podstawie pakietu `pl-PL` albo `en-US`, bez dodatkowego skanu bazy;
- `BirthDate` 1.0.0: deterministyczna data z konfigurowalnego zakresu dla kolumn
  `Date` i `DateTime`, gotowa jako zależność `Generated` dla identyfikatora;
- `Gender` 1.0.0: konfigurowalne wartości żeńska/męska, proporcja i seed, gotowe
  jako druga zależność `Generated` dla identyfikatora;
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
  kolumny; generatory syntetyczne działają bez połączenia, a `JsonPathRedactor`
  pobiera wyłącznie małą próbkę po ponownej walidacji klona;
- podgląd bezpośrednio przypisanego `TextShuffler`: 2–50 wierszy odczytywanych
  tylko z ponownie zwalidowanego klona, po czym prawdziwa sesja generatora działa
  wyłącznie w pamięci; wartości są ograniczone do 32 768 znaków, a connection
  string pochodzi ze zmiennej środowiskowej;
- podgląd `JsonPathRedactor` na kompletnych, nieobciętych wartościach z tej samej
  bezpiecznej ścieżki próbkowania; dokumenty są zmieniane wyłącznie w pamięci;
- niemodalne, tylko-odczytowe okna wartości `non-null` dla dowolnej kolumny z
  konfiguracji: limit 1–50, wiele okien naraz, kopiowanie i ponowna walidacja
  nazwy oraz markera klona przed każdym odczytem;
- prezentacja tekstowego typu kolumny wraz z długością albo `MAX`;
- menu `Help` z legendą oznaczeń tabel i kolumn, dokumentacją oraz oknem `About`
  pokazującym wersję, autora i odnośniki do projektu oraz zgłoszeń;
- trwałe flagi `OperatorOverrides` dla ręcznej zmiany włączenia kolumny, roli,
  generatora/profilu i grupy; niebieski `◆` wyróżnia takie kolumny oraz tabele;
- wspólny kontrakt `ILanguagePack` i `LanguagePackCatalog`; wbudowane biblioteki
  EN/PL deklarują metadane, typy providerów i gotowe profile generatorów, z
  których korzystają zarówno CLI, generatory edytora, jak i analiza kandydatów;
- `Generators -> Language packs` instaluje zaufaną lokalną DLL oraz pozwala
  włączać i wyłączać także wbudowane pakiety EN/PL; biblioteki trafiają do
  `%LocalAppData%\Anonymyzer\LanguagePacks`, a zmiany obowiązują po restarcie;
- zainstalowany pakiet można zaplanować do bezpiecznego usunięcia przy następnym
  starcie; wbudowane pakiety można wyłączyć, ale nie można ich skasować;
- profile regionalne mają widoczne pochodzenie pakietu; edytor ostrzega przy
  otwarciu lub edycji konfiguracji, jeśli profil wymaga wyłączonego locale;
- niedestruktywny `File -> Rescan detached clone`, który ponownie waliduje nazwę
  i marker klona, odświeża metadane oraz detekcję, dodaje nowe obiekty i zachowuje
  decyzje operatora; niewidoczne już tabele i kolumny pozostają w pliku z czerwonym
  oznaczeniem `⚠` do ręcznego przeglądu;
- klasyfikacja typów SQL Server/PostgreSQL oraz kandydaci wykrywani po nazwie
  także dla pól liczbowych, np. PESEL, NIP i telefonów;
- zgodność roli z typem oraz negatywne tokeny odrzucające m.in. booleanowe
  ustawienia, liczbowe FK z dodatkowym `id` i pola kontrolne;
- rozwijane `Add column`, które pokazuje ukryte kolumny zapisane podczas analizy,
  a na końcu pozwala po walidacji klona wczytać z bazy brakujące kolumny
  niebędące PK; dodane pola są domyślnie wyłączone;
- CLI `generate-config`, `run --dry-run` i ograniczone `run --execute`, które pobierają connection string
  wyłącznie ze wskazanej zmiennej środowiskowej;
- potrójna walidacja markera odłączonej kopii: argument operatora, konfiguracja
  i pojedynczy rekord w bazie muszą wskazywać ten sam identyfikator;
- deterministyczny plan `dry-run`: aktywne grupy i kolumny, dokładne wersje oraz
  zakresy generatorów, mapowania wyjść, wymagane skany i batch po 1000 wierszy;
- porównanie aktywnego planu z bieżącym schematem klona przed wykonaniem,
  wraz z estymacją liczby wierszy i górnego zużycia pamięci pełnych skanów;
- ocena gotowości pierwszego wycinka zapisu: dokładnie jedna tabela, kroki
  `Row`/`Column`, jeden niezmieniany PK oraz brak skanów międzytabelowych;
- deterministyczny detektor kandydatów EN/PL: `snake_case`, `camelCase`,
  prefiksy techniczne, polskie znaki, score i negatywne flagi; propozycje nigdy
  nie ustawiają `Enabled`;
- sortowanie kroków po zależnościach od wartości `Generated`, wraz z odrzucaniem
  brakujących producentów, podwójnych zapisów i cykli;
- testy integracyjne metadanych PostgreSQL 17 na tymczasowej bazie oraz SQL
  Servera na jawnie wskazanym odłączonym klonie.

### Czego jeszcze nie ma

- wykonania planów wielotabelowych i `Relational`; `--execute` obsługuje obecnie
  jedną tabelę, kroki `Row`/`Column` i pojedynczy niezmieniany PK;
- checkpointów dla `Column`, generatorów zależnych od nadpisywanej wartości oraz
  planów wielotabelowych; bezpieczne plany wyłącznie `Row` można już wznawiać;
- pełnej walidacji indeksów, triggerów i constraintów spoza aktywnej tabeli;
- pozostałych generatorów grupowych;
- podglądu generatorów `Relational` oraz przyszłych generatorów `Column`
  wymagających wielu skanów lub przypisanych przez grupę;
- pełnych, ważonych zbiorów danych regionalnych;
- automatycznej analizy semantyki niestabilnych kolekcji, XML i tekstu swobodnego;
- obsługi XML oraz zmian PK/FK;
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

# Mutuje wyłącznie zwalidowany klon i tylko plan oznaczony przez dry-run jako ready:
dotnet run --project .\src\Anonymyzer\Anonymyzer.Console -- run `
  --config .\anonymyzer-config.json `
  --connection-env ANONYMYZER_CONNECTION --marker-id $marker --execute `
  --report .\anonymyzer-execution-report.json `
  --checkpoint .\anonymyzer-execution.checkpoint.json `
  --checkpoint-key-env ANONYMYZER_CHECKPOINT_KEY
```

Obie komendy sprawdzają nazwę bazy i marker. `generate-config` tylko czyta
metadane, pomija samą tabelę markera i zapisuje niesekretny JSON. `run --dry-run`
waliduje konfigurację, dokładne wersje generatorów i target, wypisuje kolejność
kroków, mapowania, wymagane pełne skany i proponowany batch. Dodatkowo odrzuca
zmiany schematu aktywnych kolumn oraz pokazuje szacowaną liczbę wierszy i pamięć
pełnych skanów. Dla nieograniczonego `text` pamięć pozostaje jawnie nieznana.
Komenda kończy bez zapisu danych. `--execute` wymaga jawnego trybu, ponawia te
same walidacje, odrzuca plan bez statusu `write slice ready`, czyta wiersze
keyset pagingiem po PK i zapisuje każdy batch w osobnej transakcji. Opcjonalny
`--report` zapisuje atomowo raport JSON z fingerprintem konfiguracji, markerem,
czasem, planem, liczbą zatwierdzonych batchy i wierszy oraz wynikiem walidacji
po zapisie. Raport nie zawiera
connection stringa, wartości rekordów ani ostatniego klucza. Wywołanie
bez `--dry-run` i `--execute` albo z obiema flagami jest odrzucane.

Przed zapisem CLI odmawia pracy, jeśli tabela już narusza constrainty. Po zapisie
ponownie sprawdza marker i aktywny schemat, porównuje dokładne
`COUNT(*)` sprzed i po wykonaniu oraz szuka naruszeń `CHECK` i FK tabeli docelowej.
SQL Server używa `DBCC CHECKCONSTRAINTS`, a PostgreSQL wykonuje tylko zapytania
odczytowe zbudowane z katalogów `pg_constraint`, wewnątrz transakcji `READ ONLY`.
Nieudana walidacja daje kod
błędu, raport ze statusem `ValidationFailed` i pozostawia checkpoint jako
nieukończony; klon nie powinien wtedy zostać przekazany dalej.

Opcjonalny `--checkpoint` działa tylko dla deterministycznych planów `Row`, które
można bezpiecznie odtworzyć od początku. Po każdym commicie zapisuje atomowo
liczniki oraz HMAC granicznego PK, nigdy sam klucz. Sekret HMAC jest pobierany
wyłącznie ze zmiennej wskazanej przez `--checkpoint-key-env` i nie trafia do
checkpointu ani konfiguracji; powinien być losowy i mieć co najmniej 32 znaki.
Wznowienie ponownie waliduje
konfigurację, marker, tabelę, PK, batch i hash granicy. Plany `Column` (w tym
`TextShuffler`) oraz generatory czytające nadpisywaną wartość są jawnie odrzucane.
Ukończony checkpoint chroni też przed przypadkowym ponownym wykonaniem; świadomy
nowy przebieg wymaga nowej ścieżki albo usunięcia starego pliku.

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

Po wczytaniu próbek zwijany panel profilu JSON pokazuje ścieżki, liczbę
dokumentów zawierających daną ścieżkę, liczbę wartości oraz obserwowane typy.
Niepełne próbki obcięte limitem są raportowane osobno, zamiast udawać uszkodzony
JSON. Dla odporności na patologiczne dokumenty profil kończy analizę na 16
poziomach, 200 różnych ścieżkach lub 10 000 wartościach na próbkę i jawnie
sygnalizuje osiągnięcie limitu. Profil służy wyłącznie operatorowi i nie jest
zapisywany w configu.

Profil może zostać ręcznie przełożony na reguły generatora `JsonPathRedactor`.
Generator przyjmuje te same ścieżki (`$/property`, `$/array[]/property`) i osobny
literał JSON dla każdej wartości zastępczej. Zachowuje wszystkie nieskonfigurowane
gałęzie, istniejący `NULL` bazy oraz typ literału; wynik zapisuje jako zwarty JSON.
Może ignorować brakujące ścieżki albo przerwać wiersz, gdy `RequireEveryPath` jest
włączone. Reguły zduplikowane i nakładające się są odrzucane, aby wynik nie zależał
od kolejności. Typ docelowy jest przenoszony przez plan wykonania; provider
PostgreSQL jawnie rzutuje parametr JSON podczas zapisu, dzięki czemu ten sam
generator obsługuje zarówno tekst, jak i natywne kolumny `json`/`jsonb`.

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
Testy executora są opt-in dla `ANONYMYZER_POSTGRES_CONNECTION` oraz
`ANONYMYZER_SQLSERVER_CONNECTION`. Po walidacji markera tworzą tabele o unikalnych
nazwach, uruchamiają `PersonIdentity`, a na PostgreSQL także pełny `TextShuffler`,
i usuwają wyłącznie te tabele w `finally`; nie modyfikują istniejących tabel
klona. Connection string nie trafia do logu ani konfiguracji.

Powtarzalne lokalne środowiska Chinook, Northwind, AdventureWorksLT i Pagila,
wraz z komendami pobrania, inicjalizacji i generowania configów, opisuje
[katalog przykładowych baz](docs/sample-databases.md).

## Stan gałęzi

| Gałąź | Zawartość | Ocena |
| --- | --- | --- |
| `master` | `ScriptCut` i pierwszy scalony pion anonimizatora (PR #1) | wspólna baza; bieżące prace nie są jeszcze scalone |
| `anon-generators` | generatory, bezpieczny executor, SQL Server/PostgreSQL, WPF, rescan i pakiety językowe | aktywna gałąź rozwojowa |
| `anonymyzator` | historyczna gałąź pierwszego pionu | scalona do `master`; ref można zarchiwizować |
| `some_changes` | historyczne zmiany `ScriptCut` | scalona pośrednio; ref można zarchiwizować |
| `gateway` | historyczne źródło `TimeGateService` | wydzielone do osobnego repozytorium `J:\GIT\Gateway`; nie scalać do `main` |

Gateway został wydzielony do `J:\GIT\Gateway` wraz z dwoma historycznymi
commitami, osobnym rozwiązaniem, licencją i dokumentacją bezpieczeństwa. Ref
`gateway` w DbTools pozostaje jedynie źródłem historycznym i może zostać
skasowany po wypchnięciu lub zarchiwizowaniu nowego repozytorium.

## Proponowana kolejka

1. Zaprojektować pierwszy generator `Relational` oraz planowanie zależności
   między tabelami bez zmiany PK/FK.
2. Rozszerzać checkpoint tylko wraz z trwałym, odtwarzalnym stanem generatorów
   `Column`/`Relational`.
3. Rozszerzyć pakiety regionalne o wersjonowane, ważone dane z opisanym źródłem.
4. Dopiero po pomiarach dodać mapowanie zmienianych kluczy, obsługę XML oraz
   strategię indeksów, constraintów i triggerów.
5. Ujednolicić historyczne nazwy `Anonymyzer` / `Anonymization` bez zmiany
   zachowania i usunąć nieaktualne refy dopiero po upewnieniu się, że są na GH.

Raport z walidacją, checkpoint dla bezpiecznych planów `Row` oraz ograniczony
pamięciowo `TextShuffler` są już dostępne. Następny opłacalny krok to pierwszy
generator `Relational` i planowanie jego zależności między tabelami. Pełny skan
shufflera pozostaje celowo wyłączony ze wznowienia.
