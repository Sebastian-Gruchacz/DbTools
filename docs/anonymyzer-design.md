# Anonymyzer — kontrakt działania

## Model operacyjny

Anonimizator przekształca dane bezpośrednio w podłączonej bazie. Nie jest to
operacja tylko do odczytu ani generator kopii wynikowej. Bezpiecznym targetem
jest wyłącznie odłączona i odtwarzalna kopia bazy źródłowej, przygotowana np. z:

- backupu i restore dla SQL Servera;
- dumpu i restore albo nowego kontenera dla PostgreSQL;
- wersjonowanego fixture'a, takiego jak Northwind, w testach end-to-end.

Utrata kopii roboczej ma być akceptowalnym scenariuszem: w razie błędu usuwamy
ją i odtwarzamy ponownie. Nie opieramy bezpieczeństwa dużej operacji na jednej
transakcji obejmującej całą bazę.

## Zakładany przepływ

1. Operator tworzy odłączoną kopię poza anonimizatorem. Proces backupu/restore
   działa z osobnymi uprawnieniami i jest jedynym miejscem, które zna połączenie
   do bazy źródłowej.
2. Kopia dostaje marker targetu anonimizacji zawierający losowy identyfikator,
   datę utworzenia oraz opcjonalny fingerprint źródła.
3. `generate-config` czyta schemat kopii i proponuje kandydatów na podstawie
   typu, ograniczeń oraz pakietów językowych nazw.
4. Operator zatwierdza tabele, kolumny i generatory. Propozycje pozostają
   domyślnie wyłączone.
5. `run --dry-run` waliduje provider, marker, oczekiwaną nazwę i identyfikator
   bazy, konfigurację generatorów, relacje oraz kolejność operacji. Porównuje
   aktywne kolumny z bieżącym schematem klona i raportuje estymowaną liczbę
   wierszy oraz górne zużycie pamięci pełnych skanów.
6. `run --execute` w pierwszym wycinku przetwarza jedną tabelę batchami po PK;
   checkpointy i raport końcowy pozostają kolejnym etapem.
7. Walidacja po wykonaniu sprawdza constrainty, liczbę wierszy i spójność
   relacji. Kopia może dopiero wtedy zostać przekazana dalej.

## Niezmienniki bezpieczeństwa

- `run` odmawia pracy bez markera odłączonej kopii.
- Marker jest pojedynczym rekordem w tabeli `dbo.__AnonymyzerDetachedCopy` dla
  SQL Servera albo `public.__anonymyzer_detached_copy` dla PostgreSQL.
- Identyfikator podany przez operatora, zapisany w konfiguracji i odczytany z
  bazy musi być identyczny; musi też zgadzać się nazwa połączonej bazy.
- Anonimizator nigdy nie otrzymuje connection stringa bazy źródłowej ani
  produkcyjnej. Zarówno `generate-config`, jak i `run` łączą się wyłącznie z
  odłączoną kopią.
- Przed potwierdzeniem wypisywane są provider, nazwa i identyfikator targetu,
  ale nie connection string ani jego fragmenty.
- Marker ma identyfikator wymagany również w parametrach uruchomienia; sama
  nazwa w stylu `database_copy` nie jest wystarczającym zabezpieczeniem.
- Domyślny `generate-config` i inspekcja schematu nie modyfikują danych.
- Ręczny podgląd wartości w edytorze jest tylko do odczytu, wymaga ponownej
  walidacji nazwy i markera klona, ma limit 50 wierszy i 15 sekund na zapytanie.
  Pojedyncza wartość jest ucinana po 32 768 znakach. Wyniki nie są utrwalane;
  operator może jawnie skopiować wyświetlone dane do schowka.
- `run` nie tworzy automatycznie backupu źródła i nigdy nie przełącza się na
  inną bazę po błędzie połączenia.
- Tryb testowy nie używa istniejących baz użytkownika. Tworzy fixture od zera
  albo odtwarza nową kopię z backupu/dumpu.
- Plik konfiguracji zawiera tylko niesekretną identyfikację providera i bazy.
  Connection string kopii jest parametrem wyłącznie runtime i nie trafia do
  konfiguracji, logów ani raportu; docelowo powinien być przekazywany przez
  zmienną środowiskową lub bezpieczny provider sekretów.
- Klonowanie nie jest komendą ani biblioteką procesu anonimizatora. Dzięki temu
  jego konto nie potrzebuje dostępu do produkcji, a sekret produkcyjny nie może
  zostać przypadkiem użyty przez `run`.

Format konfiguracji `0.4.0` przechowuje niesekretny identyfikator markera.
Connection string jest pobierany z nazwanej zmiennej środowiskowej; CLI celowo
nie udostępnia argumentu pozwalającego wkleić sekret do historii powłoki.

## Strategia testów bazodanowych

Warstwy testów powinny rosnąć od najtańszych do najbardziej realistycznych:

1. Testy jednostkowe tokenizacji nazw, scoringu kandydatów i generatorów.
2. Małe, tworzone od zera fixture'y SQL Server/PostgreSQL dla metadanych,
   batchowania, nulli, limitów i PK/FK.
3. Odtworzenie Northwind do nowej bazy o losowej nazwie, uruchomienie pełnej
   anonimizacji i walidacja invariants po wykonaniu.
4. Kopia większej, reprezentatywnej bazy do pomiaru wydajności i checkpointów.

Northwind nadaje się na pierwszy scenariusz end-to-end: ma klientów, kontakty,
adresy, telefony i relacje zamówień. Nie pokrywa jednak polskich identyfikatorów,
diakrytyki, dużych tabel ani trudnych PK/FK, więc powinien być uzupełniony małym
fixture'em PL oraz osobnym zestawem wydajnościowym.

Skrypt `tools/Test-PostgreSqlProvider.ps1` realizuje już pierwszy bezpieczny
wzorzec integracyjny: uruchamia własny kontener, ładuje fixture i usuwa kontener
w `finally`. Nie korzysta z istniejących kontenerów ani baz.

Opcjonalne testy executora wymagają `ANONYMYZER_POSTGRES_CONNECTION` albo
`ANONYMYZER_SQLSERVER_CONNECTION`. Po walidacji markera tworzą w jawnie wskazanym
klonie tabelę o losowej, jednoznacznej nazwie, wykonują na trzech wierszach grupę
`PersonIdentity`, weryfikują wynik i usuwają dokładnie tę tabelę w `finally`.
Istniejące tabele klona nie są odczytywane ani modyfikowane przez ten scenariusz.

## Model generatorów i konfiguracji 0.4

Konfiguracja rozdziela typ generatora od jego nazwanego profilu. Profil zawiera
parametry i opcjonalne locale, np. `Email:Opaque`, `Email:NameBased.pl-PL` albo
`Address:WarsawOnly`. Kolumna wskazuje typ oraz profil, a lokalne `Options` są
wyłącznie nadpisaniem profilu.

Nie wszystkie dane wolno generować niezależnie kolumna po kolumnie. Grupa
generowania opisuje jedno wywołanie zwracające kilka spójnych wartości i mapuje
role wyjściowe na kolumny tabeli. Pierwsze zakładane grupy to:

- `PersonIdentity`: imię, nazwisko, płeć, data urodzenia, identyfikator i e-mail;
- `PostalAddress`: kraj, region, miasto, ulica i kod pocztowy;
- `CompanyIdentity`: nazwa firmy, identyfikatory podatkowe i adres.

Grupa eliminuje zależność od przypadkowej kolejności kolumn. Przykładowo e-mail
może użyć już wygenerowanego imienia i nazwiska, PESEL może być zgodny z datą
urodzenia, a kod pocztowy z miastem. Prosty `TextShuffler`, stała wartość czy
tekst sekwencyjny pozostają generatorami pojedynczej kolumny.

Pakiet regionalny nie jest providerem bazy danych. Dostarcza słowniki, dane
adresowe, reguły formatowania i walidatory dla locale, początkowo `pl-PL` oraz
wybranych wariantów angielskich. Logika generatora korzysta z tego kontraktu,
ale nie zawiera na sztywno danych konkretnego kraju.

Identyfikator „poprawny składniowo” nie zawsze oznacza „na pewno nieistniejący”.
Generator PESEL/NIP/SSN musi stosować właściwe cyfry kontrolne i ograniczenia,
ale także dokumentować gwarancje danego kraju, zapewniać unikalność w obrębie
kopii i preferować oficjalne zakresy testowe lub zastrzeżone, jeśli istnieją.

Edytor WPF operuje wyłącznie na niesekretnym JSON. Oznacza wykrytych kandydatów,
ale pokazuje również wszystkie pozostałe tabele i kolumny. Obsługuje pliki,
podstawowy grid, profile oraz mapowanie wyjść grup wielokolumnowych na kolumny.
Lista tabel używa osobnych pól o stałej szerokości na znacznik i licznik
kandydatów, aby nazwy z trafieniami i bez trafień zaczynały się w tej samej
kolumnie.
Filtr listy działa wyłącznie w pamięci UI, dopasowuje wszystkie wpisane fragmenty
do `schema.table` bez rozróżniania wielkości liter i opcjonalnie pokazuje tylko
tabele zawierające kandydatów. Nie zmienia kolejności ani zawartości JSON-a.

Rola semantyczna jest wybierana płaskim przyciskiem otwierającym dwupoziomowe
menu: najpierw kategorię, np. osoba, adres lub identyfikatory, a następnie
konkretną rolę. Kategorie są wyłącznie organizacją UI. JSON nadal przechowuje
stabilną wartość kanoniczną, np. `Address.City`; nieznane wartości z istniejących
konfiguracji pozostają dostępne w grupie `Custom / legacy`.

Edytor śledzi mutacje konfiguracji niezależnie od zmian czysto prezentacyjnych.
Edycja kolumn, ról, profili, grup oraz doładowanie metadanych ustawia flagę dirty
i gwiazdkę w tytule; filtrowanie, podgląd oraz ujawnienie kolumny już obecnej w
JSON nie zmieniają dokumentu. `New`, `Open` i zamknięcie aplikacji wymagają wtedy
decyzji `Save / Don't Save / Cancel`. Flaga jest czyszczona dopiero po udanym
zapisie albo załadowaniu innego dokumentu.

Podgląd generatorów `Row` uruchamia ich rzeczywistą sesję w pamięci, bez dostępu
do bazy. Obejmuje grupy wielokolumnowe i generatory `Row` przypisane bezpośrednio
do jednej kolumny; lokalne `Options` są nakładane na profil tak samo jak w
plannerze. Dla pojedynczej kolumny operator może otworzyć kilka niemodalnych okien
surowych wartości `non-null` z odłączonego klona.

Bezpośrednio przypisany `TextShuffler` ma ograniczony podgląd `Column`. Operator
wskazuje zmienną środowiskową połączenia i limit 2–50 wierszy. Reader ponownie
waliduje marker odłączonej kopii, pobiera próbkę razem z pozycjami `NULL`, a
następnie prawdziwa sesja shuffle działa wyłącznie w pamięci. Wynik jest jawnie
oznaczony jako próbka, a nie symulacja pełnego rozkładu kolumny; pojedyncza
wartość jest ograniczona w zapytaniu do 32 768 znaków. Generatory
`Relational`, wieloskanowe i `Column` użyte przez grupę nadal nie mają podglądu.
Żaden wariant podglądu nie może modyfikować bazy.

Grid pokazuje początkowo kandydatów oraz kolumny już skonfigurowane. `Add column`
rozwija pozostałe kolumny zapisane podczas analizy i ujawnia wybraną bez dostępu
do bazy. Ostatnia pozycja menu ponownie waliduje nazwę i marker klona, a następnie
pokazuje brakujące kolumny niebędące PK. Dodanie zmienia wyłącznie model
konfiguracji w pamięci; pola pozostają wyłączone i bez generatora aż do jawnej
decyzji operatora.

### Kontrakt pluginu generatora

Generator jest identyfikowany przez parę `Type` + `Version`; profil zapisuje oba
pola, aby aktualizacja biblioteki nie zmieniła po cichu znaczenia istniejącej
konfiguracji. Rdzeń generatora udostępnia:

- descriptor z nazwą, wersją, typem danych, zakresem wykonania oraz listą
  nazwanych wyjść z sugerowanymi rolami semantycznymi;
- własny codec: default, deserializacja JSON, walidacja i serializacja JSON;
- listę wymagań danych dla konkretnego bindingu;
- `PrepareAsync`, które może wykonać skan i zbudować stan;
- sesję z `ApplyAsync`, która generuje wartości dla kolejnych wierszy.

Rdzeń nie zależy od WPF. Dokładna wersja generatora może dostarczyć osobny
adapter `IGeneratorConfigurationEditorFactory`. Adapter otwiera dedykowany panel,
ale zapisuje ustawienia przez ten sam codec. Gdy panelu nie ma, edytor pozwala
zmienić należący do generatora obiekt `Options` jako surowy JSON.

### Zakresy wykonania

- `Row` korzysta wyłącznie z bieżącego wiersza. Tak mogą działać generatory
  imienia/nazwiska i e-maila, jeśli wszystkie zależności są w tej samej tabeli.
- `Column` deklaruje pełny skan kolumny i przygotowuje stan przed zapisem.
  `TextShuffler` 1.0.0 buforuje wartości i wykonuje deterministyczny Fisher-Yates,
  zachowując dokładny multizbiór zamiast jedynie przybliżać rozkład losowaniem.
- `Relational` deklaruje kolumny z innych tabel oraz czy potrzebuje ich wartości
  oryginalnych czy już wygenerowanych. Planner `dry-run` buduje z deklaracji
  `Generated` graf zależności, wykrywa brakujących producentów, podwójne zapisy
  i cykle oraz ustala deterministyczną kolejność wykonania.

Plan obejmuje wyłącznie włączone tabele i kolumny. Dla każdego kroku podaje
docelową tabelę, dokładny typ i wersję generatora, zakres `Row`/`Column`/
`Relational`, mapowanie wyjść, wymagania danych oraz proponowany batch 1000
wierszy. Lokalne `Options` kolumny są nakładane na profil przed walidacją.
Włączona kolumna bez jednoznacznego kroku jest błędem, a nie cichym pominięciem.
Planner nadal nie odczytuje liczby wierszy ani nie wykonuje sesji generatorów.
Liczbę wierszy i koszt pamięci uzupełnia osobny inspektor bieżącego schematu,
uruchamiany przez `run --dry-run` po zbudowaniu planu.

Ten sam inspektor zapisuje rzeczywiste kolumny PK. Walidator pierwszego wycinka
zapisu dopuszcza dokładnie jedną tabelę,
wyłącznie kroki `Row`, jeden niezmieniany klucz główny i wymagania danych z tego
samego wiersza. Pełny skan, scope `Column`/`Relational`, brak lub złożony PK,
zmiana PK albo odczyt innej tabeli daje w `dry-run` jawny status `not ready`.
`--execute` działa wyłącznie dla statusu `ready`: czyta kolejne batche keyset
pagingiem, utrzymuje sesje generatorów między batchami i zapisuje batch w jednej
transakcji. Aktualizacja innej liczby wierszy niż dokładnie jeden dla danego PK
powoduje rollback batcha. Nie ma jeszcze checkpointu ani automatycznej walidacji
constraintów po zakończeniu.

Reader przekazuje dane strumieniowo, natomiast generator decyduje, co buforuje.
Dokładny shuffle ma koszt pamięci `O(n)` i nie nadaje się bezpośrednio do każdej
wielkiej tabeli. Kolejne strategie powinny obejmować limit pamięci, spill do
pliku tymczasowego lub bazowej tabeli roboczej oraz opcjonalne losowanie ważone,
które zachowuje rozkład tylko statystycznie. Wybór musi być jawny w profilu.

Samo przestawienie wartości nie usuwa rzadkich danych z całej kopii, dlatego
`TextShuffler` nie jest właściwym generatorem dla silnie identyfikujących pól.

### Proste generatory Row 1.0.0

`FixedText` zastępuje wartość skonfigurowanym tekstem. `Value` może być pustym
łańcuchem, ale nie `null`; `PreserveNulls` decyduje, czy istniejące pozycje `NULL`
pozostają nienaruszone. Generator nie skraca wartości do długości kolumny — zbyt
długi tekst ma zostać wykryty przez walidację lub bazę, a nie cicho uszkodzony.

`SequentialText` tworzy wartości `Prefix + numer + Suffix`. Konfiguracja zawiera
`StartAt`, `MinimumDigits` oraz `PreserveNulls`. Pominięte pozycje `NULL` nie
zużywają numeru, więc sekwencja pozostaje gęsta. Unikalność jest gwarantowana
tylko w ramach jednej sesji generatora, a konkretna wartość zależy od kolejności
przetwarzania wierszy; nie jest to pseudonim stabilny względem klucza źródłowego.

Oba generatory nie deklarują odczytu danych i mają osobne panele WPF zapisujące
konfigurację przez ten sam wersjonowany codec JSON co wykonanie CLI. Menu
`Profiles → Add` buduje gotowy profil z domyślnej konfiguracji właściciela
generatora; pusty profil pozostaje dostępny dla zewnętrznych pluginów.

### Samodzielny EmailAddress 1.0.0

`EmailAddress` ma jedno wyjście `Value` sugerujące rolę `Contact.Email` i dwa
tryby. `Opaque` nie czyta danych osobowych i tworzy local-part z prefiksu oraz
licznika. `NameBased` czyta wskazane kolumny imienia i nazwiska z bieżącego
wiersza, normalizuje m.in. polskie znaki i dodaje licznik zapobiegający kolizjom.

Dla `NameBased` operator jawnie wybiera `Original` albo `Generated`. W drugim
wariancie wymaganie danych uczestniczy w grafie plannera, więc producenci imienia
i nazwiska muszą istnieć i wykonać się wcześniej. Brak wartości nie uruchamia
cichego fallbacku — jest błędem konfiguracji lub danych.

Domyślna domena `example.invalid` jest niedostarczalna. Codec waliduje składnię
ASCII DNS, długość domeny, długość local-part, licznik i wymagane nazwy kolumn.
Generator zachowuje `NULL` opcjonalnie, a pominięty wiersz nie zużywa numeru.
Unikalność, podobnie jak w `SequentialText`, dotyczy jednej sesji wykonawczej.

### AccountLogin 1.0.0

`AccountLogin` generuje tekst dla roli `Account.Login`. Tryb `Opaque` łączy
znormalizowany prefiks z licznikiem, a `NameBased` dodatkowo czyta wskazane
kolumny imienia i nazwiska. Źródło `Original`/`Generated` jest deklarowanym
wymaganiem danych, więc planner może uruchomić producenta nazw wcześniej.

Separator jest ograniczony do krótkich kombinacji `.`, `_` i `-`. Końcowy
licznik jest zawsze obecny i zapewnia unikalność w sesji; `StartAt`, minimalna
liczba cyfr i zachowanie `NULL` należą do konfiguracji generatora. Normalizacja
korzysta z tych samych zasad transliteracji co `EmailAddress`.

### BankAccount 1.0.0

`BankAccount` generuje tekst dla roli `Financial.BankAccount`. Pierwszy provider
obsługuje `pl-PL` i formaty zwartego IBAN-u, IBAN-u grupowanego spacjami oraz
krajowego NRB. Każda wartość przechodzi kontrolę modulo 97, a sekwencja jest
deterministyczna i nie powtarza wartości w ramach sesji.

Ośmiocyfrowy segment banku i oddziału ma stałą wartość `00000000`. Dzięki temu
dane zachowują strukturę i checksum wymaganą przez typową walidację formularza,
ale nie wskazują świadomie wybranego działającego banku. Nie wolno używać tych
wartości do wykonywania przelewów ani traktować ich jako potwierdzenia istnienia
rachunku. Profil wybiera locale, format, seed oraz zachowanie pozycji `NULL`.

### PhoneNumber 1.0.0

`PhoneNumber` jest generatorem `Row` z jednym wyjściem `Value` dla roli
`Contact.Phone`. Profil wybiera `Locale`, format krajowy lub międzynarodowy,
deterministyczny `Seed` i zachowanie pozycji `NULL`. Generator nie czyta danych
źródłowych; składanie numeru należy do providerów pakietów językowych. Provider
deklaruje pojemność własnego zakresu, a sesja tworzy wartości bez powtórzeń i
kończy się błędem przed ponownym użyciem numeru.

Provider `pl-PL` tworzy dziewięciocyfrowy numer w układzie `501 XXX XXX` i może
dodać prefiks `+48`. Jest to wartość syntetyczna o poprawnym kształcie, ale polski
plan numeracji nie udostępnia ogólnego zakresu fikcyjnego, więc nie należy jej
wybierać ani używać do wysyłki. Provider `en-US` używa zakresu `202-555-0100`–
`202-555-0199`, zastrzeżonego jako fikcyjny i niedziałający, oraz opcjonalnego
prefiksu `+1`. Format polski odpowiada [opisowi numeracji krajowej UKE](https://cik.uke.gov.pl/news/nie-oddzwaniaj%2C100.html),
a zakres amerykański [dokumentuje NANPA](https://nanpa.com/numbering/555-line-numbers).

Panel WPF udostępnia dokładnie parametry własnego codeca. Nieznane locale jest
poprawnym JSON-em pluginu, lecz przygotowanie sesji kończy się czytelnym błędem,
jeżeli odpowiadający provider nie został zainstalowany.

### Uuid 1.0.0

`Uuid` generuje tekstowe identyfikatory z markerem wersji 4 i prawidłowym
wariantem UUID. Nie jest to źródło losowych UUID v4. Profil
zawiera tekstowy `Seed`, początek sekwencji, format `Hyphenated`, `Compact`,
`Braced` albo `Parenthesized`, wielkość liter i zachowanie pozycji `NULL`.
Identyfikator powstaje z SHA-256 seeda oraz kolejnego numeru, dlatego ta sama
konfiguracja i kolejność wierszy dają ten sam wynik.

Generator nie używa losowości kryptograficznej i nie służy do tworzenia sekretów.
Kolizja skrótu jest skrajnie mało prawdopodobna, ale nie jest matematycznie
niemożliwa. Zachowany `NULL` nie zużywa numeru sekwencji, a po wygenerowaniu
wartości dla `Int64.MaxValue` następne wywołanie kończy się błędem.

### TaxIdentifier 1.0.0 i polski NIP

`TaxIdentifier` jest rozszerzalnym generatorem `Row` dla roli `Company.TaxId`.
Profil zawiera `Locale`, deterministyczny `Seed`, format oraz zachowanie `NULL`.
Provider regionalny deklaruje pojemność i odpowiada zarówno za cyfry kontrolne,
jak i prezentację wartości. Brak providera dla wybranego locale zatrzymuje
przygotowanie sesji.

Pierwszy provider `pl-PL` generuje dziesięciocyfrowy NIP. Dla pierwszych dziewięciu
cyfr stosuje wagi `6, 5, 7, 2, 3, 4, 5, 6, 7`; reszta z dzielenia sumy przez 11
jest cyfrą kontrolną, a kombinacje dające resztę 10 są pomijane. Generator oferuje
format `DigitsOnly`, `Hyphenated` (`XXX-XXX-XX-XX`) oraz `International`
(`PLXXXXXXXXXX`). Ministerstwo Finansów potwierdza, że urzędowa walidacja NIP
[sprawdza strukturę identyfikatora i algorytm cyfry kontrolnej](https://www.podatki.gov.pl/pytania-i-odpowiedzi/mikrorachunek/czy-i-w-jaki-sposob-generator-bedzie-weryfikowal-poprawnosc-pesel-i-nip).

Provider mapuje numer porządkowy na jeden z 810 milionów różnych wyników, więc
nie używa retry ani rosnącego bufora zajętych wartości. Poprawność cyfry kontrolnej
nie dowodzi, że NIP jest nieprzydzielony; są to dane syntetyczne przeznaczone
wyłącznie do odłączonej kopii bazy. Obecna wersja zapisuje tekst i nie obsługuje
jeszcze liczbowych kolumn z NIP-em.

Ten sam provider obsługuje warianty `REGON9` i `REGON14`, wyłącznie jako
`DigitsOnly`. REGON9 zawiera osiem cyfr porządkowych i checksum, natomiast
REGON14 składa się z poprawnego REGON9 jednostki nadrzędnej, czterech cyfr
jednostki lokalnej i końcowej cyfry kontrolnej. Tę strukturę potwierdza
[definicja GUS](https://stat.gov.pl/metainformacje/slownik-pojec/pojecia-stosowane-w-statystyce-publicznej/2963%2Cpojecie.html?pdf=1).
Wagi checksum są testowane osobno dla obu długości. Pole `Variant` ma domyślną
wartość `NIP`, więc konfiguracje utworzone przed dodaniem REGON zachowują
dotychczasowe działanie.

### NationalIdentifier 1.0.0: PESEL i bezpieczny SSN

`NationalIdentifier` generuje pojedynczą wartość dla roli `Person.NationalId`.
Profil określa locale, zakres dat urodzenia zapisany jako `yyyy-MM-dd`, płeć
`Any`/`Female`/`Male`, seed i zachowanie `NULL`. Provider `pl-PL` obsługuje pełny
zakres stuleci kodowanych przez PESEL, czyli lata 1800–2299.

Pierwsze sześć cyfr koduje datę, cztery następne numer porządkowy i płeć, a
ostatnia jest obliczana z wag `1, 3, 7, 9, 1, 3, 7, 9, 1, 3`. Reguły, parytet
płci i przesunięcia miesiąca dla stuleci odpowiadają [opisowi gov.pl](https://www.gov.pl/web/gov/czym-jest-numer-pesel).
Numer porządkowy jest mapowany bez powtórzeń; przy jednej dacie dostępne jest
10 000 wartości dla `Any` albo 5 000 dla wskazanej płci.

Poprawny strukturalnie PESEL może należeć do rzeczywistej osoby, dlatego wynik
wolno stosować wyłącznie w odłączonej kopii i nie należy używać go do zapytań
urzędowych. Domyślnie generator dobiera datę oraz płeć wewnętrznie. Opcjonalne
`BirthDateColumn` i `GenderColumn` pozwalają jednak czytać je z bieżącego wiersza,
osobno wskazując `Original` albo `Generated`; wymagania uczestniczą wtedy w grafie
plannera. Wartości płci są mapowane przez konfigurowalne listy żeńskie i męskie.
Sesja utrzymuje osobny licznik dla każdej pary data+płeć, dzięki czemu powtarzająca
się data nie powoduje kolizji przed wyczerpaniem 5 000 numerów danej płci.

Provider `en-US` generuje wyłącznie wartości w formacie `000-xx-xxxx`. Prefiks
`000` nie jest przydzielany: SSA zaleca go do przykładów, aby nie pokazać
przypadkiem prawdziwego SSN. Daje to milion unikalnych, deterministycznych
wartości. Data i płeć mogą nadal uczestniczyć w konfiguracji wiersza, ale nie
dzielą przestrzeni numerów na osobne liczniki, ponieważ SSN ich nie koduje.

To celowy kompromis bezpieczeństwa: walidator wymagający numeru możliwego do
przydzielenia odrzuci prefiks `000`. Generator nie próbuje obchodzić takiej
walidacji przez tworzenie numerów, które mogłyby należeć do rzeczywistych osób.
Format 3-2-4 odpowiada opisowi SSA, a zasada nieprzydzielonego prefiksu `000`
pochodzi z [instrukcji SSA](https://secure.ssa.gov/apps10/poms.nsf/links/0110201020).

### BirthDate 1.0.0

`BirthDate` jest jednowyjściowym generatorem `Row` dla roli `Person.BirthDate`.
Profil określa minimalną i maksymalną datę w formacie `yyyy-MM-dd`, seed oraz
zachowanie pozycji `NULL`. Wynik jest wartością `DateOnly`; descriptor dopuszcza
kolumny `Date` i `DateTime`.

Oddzielny generator zachowuje poprawne typowanie: `PersonIdentity` ma tekstowe
wyjścia, natomiast data nie jest do niego dokładana jako wyjątek. Kolumna
wygenerowana przez `BirthDate` może zostać wskazana w `NationalIdentifier` jako
`BirthDateColumn` ze źródłem `Generated`, dzięki czemu planner ustawi kolejność,
a PESEL zakoduje dokładnie tę samą datę.

### Gender 1.0.0

`Gender` generuje pojedynczą tekstową wartość dla roli `Person.Gender`. Profil
zawiera osobne wartości żeńską i męską, całkowity udział żeński od 0 do 100,
seed oraz zachowanie `NULL`. Pozwala to dopasować wynik do konwencji konkretnej
bazy, np. `Female`/`Male`, `K`/`M` albo `F`/`M`.

`NationalIdentifier` może czytać tę kolumnę jako `GenderColumn` ze źródłem
`Generated`. Wspólnie z `BirthDate` tworzy to trzy kroki, które planner układa
w kolejności data+płeć, a następnie zależny PESEL. Mapowania wartości pozostają
w profilu identyfikatora, więc generator płci nie zależy od formatu PESEL.

### Pierwszy generator Row: PersonIdentity 1.0.0

`PersonIdentity` wykonuje jedno atomowe wywołanie dla wiersza i może wystawić
wyjścia `FirstName`, `LastName`, `Gender` oraz `Email`. Binding decyduje, które
z nich trafią do kolumn. E-mail jest budowany z wartości wygenerowanych w tym
samym wywołaniu, więc nie zależy od kolejności kolumn.

Konfiguracja należąca do generatora zawiera `Seed`, `Locale`, `EmailPattern` i
`EmailDomain`. Dostępne są na razie schematy:

- `NameBased`: znormalizowane imię, nazwisko i licznik zapewniający unikalność;
- `Opaque`: sztuczny identyfikator bez imienia i nazwiska.

Domyślna domena `example.invalid` jest zastrzeżona do przykładów i nie prowadzi
do prawdziwej skrzynki. Pakiet `pl-PL` odpowiada za pary męskich/żeńskich form
nazwisk oraz transliterację polskich znaków w local-part e-maila.

Pakiet `en-US` udostępnia osobne listy imion żeńskich i męskich oraz wspólną
listę nazwisk. Normalizacja local-part usuwa znaki diakrytyczne i interpunkcję,
więc np. `José O'Connor` staje się `joseoconnor`. Oba pakiety są wybierane przez
to samo pole `Locale` bez rozgałęzień regionalnych w samym generatorze.

Aktualne pakiety zawierają małe, równomiernie losowane zestawy startowe. Nie należy
traktować ich jako modeli rozkładu populacji. Docelowy pakiet danych
powinien zawierać wersjonowane częstotliwości wraz z pochodzeniem danych i
testami jakości. Generator nie tworzy jeszcze PESEL ani NIP.

### PostalAddress 1.0.0

`PostalAddress` jest generatorem `Row` z opcjonalnymi wyjściami `Country`,
`Region`, `City`, `Street` i `PostalCode`. Provider regionalny najpierw wybiera
rekord lokalizacji, który wiąże region, miasto i kod pocztowy, a następnie ulicę
i numer domu. Dzięki temu operator może związać dowolny podzbiór kolumn, ale kod
nie powstaje niezależnie od miasta w tym samym wierszu.

Pierwsze providery `pl-PL` i `en-US` mają małe, równomierne zestawy startowe.
Kody są przypisane do miasta; zestaw nie gwarantuje jeszcze zgodności kodu z
konkretnym numerem budynku ani reprezentatywnego rozkładu geograficznego. Pełne
dane powinny później trafić do wersjonowanych zasobów pakietu językowego.

### CompanyName 1.0.0

`CompanyName` generuje pojedynczy tekst dla roli `Company.Name`. Provider
`pl-PL` albo `en-US` składa regionalnie brzmiący rdzeń, określenie branży oraz
opcjonalną formę prawną. Licznik sesji zapewnia unikalność, a seed powtarzalność.

Każdy profil musi zawierać widoczny `SyntheticMarker` (domyślnie `TEST`), który
jest częścią każdej nazwy obok sześciocyfrowego licznika. To świadomy bezpiecznik:
nawet jeśli losowe człony przypominają istniejącą firmę, wynik pozostaje jawnie
syntetyczny. Marker można dopasować do konwencji testowanej organizacji, ale nie
można go wyłączyć.

## Słowniki kandydatów

Pierwszy zakres językowy to angielski (`en`) i polski (`pl`). Kolejne języki
powinny być oddzielnymi, opcjonalnymi pakietami danych korzystającymi z tego
samego kontraktu; nie dokładamy ich do rdzenia bez osoby zdolnej zweryfikować
znaczenie i fałszywe dopasowania.

Słownik służy do wykrywania kandydatów, a nie do automatycznego włączania
anonimizacji. Wynik powinien zawierać kategorię semantyczną, język, dopasowaną
regułę, proponowany generator i score. Konfiguracja wynikowa nadal ma
`Enabled = false`, dopóki operator jej nie zatwierdzi.

### Normalizacja nazw

- rozdzielenie `snake_case`, `kebab-case`, spacji, `camelCase` i `PascalCase`;
- porównanie bez uwzględniania wielkości liter;
- zachowanie formy oryginalnej oraz formy bez polskich znaków;
- preferowanie dopasowania całej nazwy lub pełnych tokenów nad substringiem;
- możliwość użycia kontekstu nazwy tabeli, ale bez podnoszenia słabego
  dopasowania do stuprocentowej pewności.

### Początkowe kategorie EN/PL

| Kategoria | Angielskie kandydaty | Polskie kandydaty |
| --- | --- | --- |
| Imię | `first_name`, `firstname`, `given_name`, `forename` | `imie`, `imię`, `pierwsze_imie` |
| Nazwisko | `last_name`, `lastname`, `surname`, `family_name` | `nazwisko`, `nazwisko_rodowe` |
| Pełna nazwa osoby | `full_name`, `display_name`, `contact_name` | `pelne_imie`, `pełne_imie`, `nazwa_kontaktu` |
| E-mail | `email`, `email_address`, `e_mail`, `mail` | `email`, `e_mail`, `adres_email` |
| Telefon | `phone`, `phone_number`, `mobile`, `telephone` | `telefon`, `nr_telefonu`, `numer_telefonu`, `komorka`, `komórka` |
| Adres | `address`, `street`, `city`, `postal_code`, `zip_code` | `adres`, `ulica`, `miasto`, `kod_pocztowy` |
| Login | `login`, `username`, `user_name`, `screen_name` | `login`, `nazwa_uzytkownika`, `nazwa_użytkownika` |
| Data urodzenia | `birth_date`, `date_of_birth`, `dob` | `data_urodzenia`, `urodzony`, `urodzona` |
| Identyfikator osoby/podatkowy | `ssn`, `tax_id`, `national_id`, `identity_number` | `pesel`, `nip`, `regon`, `nr_dowodu`, `numer_dowodu` |
| Konto bankowe | `iban`, `bank_account`, `account_number` | `iban`, `rachunek_bankowy`, `numer_konta` |

Lista jest zalążkiem do testów i strojenia, nie zamkniętym słownikiem. Należy
uwzględnić także prefiksy/sufiksy techniczne (`customer_email`, `billing_city`)
oraz negatywne przypadki, np. `email_enabled`, `address_type` czy
`tax_id_required`, których wartości nie są danymi do anonimizacji.

Pierwszy zestaw jest zaimplementowany jako dwa niezależne providery reguł.
Wspólny detektor usuwa diakrytykę, rozpoznaje skróty w `PascalCase`, dopasowuje
pełne sekwencje tokenów i obniża score dla prefiksów technicznych. Tokeny takie
jak `enabled`, `required`, `type`, `status` i `flag` odrzucają dopasowanie.
Reguła ogólnego angielskiego `address` została celowo zastąpiona formami
`street_address`, `mailing_address` i `postal_address`, ponieważ na realnym
schemacie myliła adres pocztowy z adresem strony, sieci i nadawcy e-maila.

Generator konfiguracji pobiera wszystkie kolumny niebędące PK i klasyfikuje je
do przenośnych kategorii: tekst, liczba całkowita/dziesiętna, boolean, data/czas,
GUID, binaria, JSON, XML lub `Other`. Najpierw dopasowuje nazwę, a potem sprawdza
zgodność roli z typem. Liczbowe PESEL, NIP, telefon, kod pocztowy i rachunek nadal
są kandydatami, ale liczbowe imię, e-mail lub region oraz booleanowy NIP już nie.
Boolean pozostaje zgodny z płcią, a data z datą urodzenia. Dodatkowe tokeny
techniczne, np. `id`, `kontrola`, `blokada` i `bez`, odrzucają ustawienia oraz FK;
`source`/`zrodlo` odrzucają tylko typy liczbowe, daty, boolean, GUID i binaria,
aby nie ukryć właściwych wartości tekstowych, JSON/XML ani nieznanych.
Automatyczny `TextShuffler` jest przypisywany wyłącznie
tekstowi; pozostałe typy czekają na jawny, zgodny generator. Dla liczbowych
identyfikatorów generator musi uwzględnić utracone zera wiodące i ograniczenia
typu docelowego.
