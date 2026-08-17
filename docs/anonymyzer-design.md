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
   bazy, konfigurację generatorów, relacje oraz kolejność operacji.
6. `run` przetwarza dane batchami, zapisuje checkpointy i raport końcowy.
7. Walidacja po wykonaniu sprawdza constrainty, liczbę wierszy i spójność
   relacji. Kopia może dopiero wtedy zostać przekazana dalej.

## Niezmienniki bezpieczeństwa

- `run` odmawia pracy bez markera odłączonej kopii.
- Anonimizator nigdy nie otrzymuje connection stringa bazy źródłowej ani
  produkcyjnej. Zarówno `generate-config`, jak i `run` łączą się wyłącznie z
  odłączoną kopią.
- Przed potwierdzeniem wypisywane są provider, nazwa i identyfikator targetu,
  ale nie connection string ani jego fragmenty.
- Marker ma identyfikator wymagany również w parametrach uruchomienia; sama
  nazwa w stylu `database_copy` nie jest wystarczającym zabezpieczeniem.
- Domyślny `generate-config` i inspekcja schematu nie modyfikują danych.
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
