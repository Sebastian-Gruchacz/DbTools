# Anonymyzer — katalog generatorów

[English version](../en/generators.md)

Profile są wersjonowanymi zestawami parametrów. Najbezpieczniej tworzyć je przez
`Generators → Profiles... → Add` i edytować przez `Configure...`; panel zapisuje
ten sam obiekt `Options`, którego używa CLI. Poniższe nazwy pól odpowiadają JSON.

## Wspólne pojęcia

- `Seed` zapewnia deterministyczny, powtarzalny ciąg dla tej samej konfiguracji.
- `PreserveNulls = true` pozostawia źródłowe `NULL` bez zmian.
- `Locale` wskazuje aktywny pakiet językowy, obecnie `pl-PL` albo `en-US` tam,
  gdzie generator obsługuje oba warianty.
- `Original` czyta wartość sprzed anonimizacji, a `Generated` wymaga wyniku
  wcześniejszego kroku i wpływa na kolejność planu.
- `Row` działa na bieżącym wierszu, `Column` wymaga pełnego skanu kolumny, a
  `Relational` może czytać inne tabele.

## Generatory wielowyjściowe

### PersonIdentity (`Row`)

Generuje spójne imię, nazwisko, płeć i e-mail. Używaj przez grupę; można mapować
dowolny niepusty podzbiór wyjść `FirstName`, `LastName`, `Gender`, `Email`.

- `Locale`: `pl-PL` lub `en-US`.
- `Seed`: deterministyczny wybór osoby.
- `EmailPattern`: `NameBased` albo `Opaque`.
- `EmailDomain`: domena wynikowa; domyślne `example.invalid` jest niedostarczalne.

### PostalAddress (`Row`)

Generuje razem `Country`, `Region`, `City`, `Street` i `PostalCode`. Miasto i kod
pocztowy pochodzą z jednego rekordu providera regionalnego.

- `Locale`: `pl-PL` lub `en-US`.
- `Seed`: deterministyczny wybór adresu.

## Proste generatory tekstowe

### FixedText (`Row`)

- `Value`: stały tekst, domyślnie `REDACTED`.
- `PreserveNulls`.

### SequentialText (`Row`)

Tworzy wartości typu `anon-00000001`.

- `Prefix`, `Suffix`;
- `StartAt`: pierwszy numer;
- `MinimumDigits`: minimalna liczba cyfr z zerami wiodącymi;
- `PreserveNulls`.

### Uuid (`Row`)

- `Seed`: tekstowy seed deterministycznych UUID;
- `StartAt`: początkowy licznik;
- `Format`: `Hyphenated`, `Compact`, `Braced` albo `Parenthesized`;
- `Uppercase`, `PreserveNulls`.

### EmailAddress (`Row`)

- `Pattern`: `Opaque` albo `NameBased`;
- `Domain`: domyślnie `example.invalid`;
- `OpaquePrefix`, `StartAt`, `MinimumDigits`: numerowany wariant opaque;
- `FirstNameColumn`, `LastNameColumn`: zależności wariantu `NameBased`;
- `NameValueSource`: `Original` albo `Generated`;
- `PreserveNulls`.

Przy `Generated` kolumny imienia i nazwiska muszą mieć aktywne wcześniejsze kroki.

### AccountLogin (`Row`)

- `Pattern`: `Opaque` albo `NameBased`;
- `OpaquePrefix`, `StartAt`, `MinimumDigits`;
- `FirstNameColumn`, `LastNameColumn`, `NameValueSource`;
- `Separator`: separator części loginu;
- `PreserveNulls`.

### PhoneNumber (`Row`)

- `Locale`: `pl-PL` albo `en-US`;
- `Format`: `National` albo `International`;
- `Seed`, `PreserveNulls`.

Wariant `en-US` korzysta z zastrzeżonego zakresu fikcyjnego 555-0100–0199.

### CompanyName (`Row`)

- `Locale`;
- `SyntheticMarker`: obowiązkowy marker odróżniający dane testowe;
- `IncludeLegalForm`: dodaje lokalną formę prawną;
- `Seed`, `PreserveNulls`.

### TaxIdentifier (`Row`)

Obsługuje polski NIP i REGON z prawidłową cyfrą kontrolną.

- `Locale`: obecnie `pl-PL`;
- `Variant`: `NIP`, `REGON9` albo `REGON14`;
- `Format`: `DigitsOnly`, `Hyphenated` albo `International`;
- `Seed`, `PreserveNulls`.

REGON obsługuje wyłącznie `DigitsOnly`; pozostałe formaty dotyczą NIP.

### BankAccount (`Row`)

Generuje poprawną matematycznie, celowo nieroutowalną polską wartość IBAN/NRB.

- `Locale`: obecnie `pl-PL`;
- `Format`: `IbanCompact`, `IbanGrouped` albo `DomesticNrb`;
- `Seed`, `PreserveNulls`.

## Dane osoby

### BirthDate (`Row`)

- `MinimumDate`, `MaximumDate`: włącznie, format `yyyy-MM-dd`;
- `Seed`, `PreserveNulls`.

Obsługuje kolumny `Date` i `DateTime`.

### Gender (`Row`)

- `FemaleValue`, `MaleValue`: wartości zapisane w bazie;
- `FemalePercentage`: udział 0–100;
- `Seed`, `PreserveNulls`.

### NationalIdentifier (`Row`)

Generuje polski PESEL albo bezpiecznie nieprzydzielony amerykański SSN.

- `Locale`: `pl-PL` albo `en-US`;
- `MinimumBirthDate`, `MaximumBirthDate`;
- `Gender`: `Any`, `Female` albo `Male`;
- `BirthDateColumn`, `BirthDateValueSource`;
- `GenderColumn`, `GenderValueSource`;
- `FemaleValues`, `MaleValues`: mapowanie wartości źródłowej na płeć;
- `Seed`, `PreserveNulls`.

Puste nazwy kolumn oznaczają użycie skonfigurowanego zakresu dat i ustawienia
`Gender`. Zależności `Generated` muszą wskazywać aktywne kroki w tej samej tabeli.

## Generatory korzystające z istniejących danych

### JsonPathRedactor (`Row`)

Modyfikuje wskazane fragmenty JSON, zachowując resztę dokumentu.

- `Rules`: lista obiektów `Path` i `ReplacementJson`;
- `Path`: obsługiwana ścieżka JSON;
- `ReplacementJson`: poprawny fragment JSON, np. `null`, `"REDACTED"` albo `0`;
- `RequireEveryPath`: błąd, jeśli któregokolwiek path nie ma w dokumencie.

Obsługuje tekst oraz PostgreSQL `json/jsonb`. Nie używaj obciętej próbki jako
podstawy do zatwierdzenia reguł.

### TextShuffler (`Column`)

Deterministycznie permutuje całą kolumnę i zachowuje dokładny multizbiór wartości.

- `Seed`;
- `MinimumPopulation`: minimalna liczba wartości potrzebna do shuffle;
- `PreserveNulls`;
- `MaximumInMemoryBytes`: limit bufora;
- `OverflowStrategy`: `Fail` albo `EncryptedTemporaryFiles`.

Strategia plikowa szyfruje zawartość kluczem efemerycznym i sprząta pliki po
sesji, ale nadal wymaga odpowiedniej przestrzeni na dysku tymczasowym.

### ReferencePseudonym (`Relational`)

Tworzy ten sam pseudonim HMAC dla każdego wiersza z tym samym kluczem obcym. Nie
zmienia kolumn PK/FK; zapisuje osobną kolumnę tekstową.

- `ReferenceColumn`: FK w tabeli docelowej;
- `LookupSchema`, `LookupTable`, `LookupKeyColumn`: źródło dozwolonych kluczy;
- `Prefix`, `HashLength`;
- `KeyEnvironmentVariable`: nazwa zmiennej z sekretem HMAC długości min. 32 znaków;
- `MaximumInMemoryBytes`;
- `OverflowStrategy`: `Fail` albo `EncryptedTemporaryIndex`;
- `PreserveNulls`.

Edytor preferuje relacje FK znalezione podczas skanu. Krótszy `HashLength`
zwiększa ryzyko kolizji; wykryta kolizja przerywa wykonanie.
