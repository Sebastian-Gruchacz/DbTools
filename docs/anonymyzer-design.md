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
6. `run` przetwarza dane batchami, zapisuje checkpointy i raport końcowy.
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

Opcjonalny test SQL Servera wymaga `ANONYMYZER_SQLSERVER_CONNECTION` i tylko
odczytuje metadane jawnie wskazanego klona. Fixture lub klon musi zawierać marker
`dbo.__AnonymyzerDetachedCopy`; test nie tworzy ani nie modyfikuje bazy.

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
losowy tekst pozostają generatorami pojedynczej kolumny.

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
Podgląd generatorów `Row` uruchamia ich rzeczywistą sesję w pamięci, bez dostępu
do bazy. Dla pojedynczej kolumny operator może otworzyć kilka niemodalnych okien
surowych wartości `non-null` z odłączonego klona. Jest to narzędzie inspekcyjne,
nie podgląd wyniku generatora `Column`: UI nadal nie symuluje shuffle ani
generatorów `Relational`. Podgląd nigdy nie może modyfikować bazy.

`Add column...` ponownie waliduje nazwę i marker klona, a następnie pokazuje
brakujące kolumny niebędące PK. Dodanie zmienia wyłącznie model konfiguracji w
pamięci; pola pozostają wyłączone i bez generatora aż do jawnej decyzji
operatora.

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

Reader przekazuje dane strumieniowo, natomiast generator decyduje, co buforuje.
Dokładny shuffle ma koszt pamięci `O(n)` i nie nadaje się bezpośrednio do każdej
wielkiej tabeli. Kolejne strategie powinny obejmować limit pamięci, spill do
pliku tymczasowego lub bazowej tabeli roboczej oraz opcjonalne losowanie ważone,
które zachowuje rozkład tylko statystycznie. Wybór musi być jawny w profilu.

Samo przestawienie wartości nie usuwa rzadkich danych z całej kopii, dlatego
`TextShuffler` nie jest właściwym generatorem dla silnie identyfikujących pól.

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

Aktualny pakiet zawiera mały, równomiernie losowany zestaw startowy. Nie należy
traktować go jako modelu rozkładu polskiej populacji. Docelowy pakiet danych
powinien zawierać wersjonowane częstotliwości wraz z pochodzeniem danych i
testami jakości. Generator nie tworzy jeszcze PESEL ani NIP.

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
