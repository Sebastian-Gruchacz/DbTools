# Anonymyzer — instrukcja operatora

[English version](../en/anonymyzer-user-guide.md)

## Zasada bezpieczeństwa

Anonymyzer modyfikuje bazę w miejscu. Używaj wyłącznie odłączonego klona
utworzonego przez backup/restore albo równoważną procedurę. Connection string
produkcji nie powinien być dostępny na komputerze ani koncie uruchamiającym
Anonymyzer.

Każdy klon musi mieć marker z `tools/markers`. Nazwa bazy, identyfikator markera
podany przez operatora i marker zapisany w bazie muszą być zgodne. Connection
string jest przekazywany tylko przez zmienną środowiskową i nie trafia do JSON-a.

## Zalecany przebieg

1. Odtwórz odłączony klon i dodaj marker.
2. Wygeneruj konfigurację poleceniem `generate-config`.
3. Otwórz JSON w edytorze WPF i przejrzyj wszystkie tabele oraz kandydatów.
4. Skonfiguruj profile generatorów, kolumny i grupy.
5. Zapisz dokument i wykonaj `run --dry-run`.
6. Popraw każdy błąd; wykonuj zapis dopiero dla planu `write slice ready`.
7. Uruchom `run --execute` z raportem, a dla obsługiwanego planu także z
   checkpointem.
8. Sprawdź raport i walidację klona przed przekazaniem go dalej.

## Uruchomienie edytora

```powershell
dotnet run --project .\src\Anonymyzer\Anonymyzer.ConfigEditor\Anonymyzer.ConfigEditor.csproj
```

Edytor nie przechowuje connection stringa. Okna próbek i rescan proszą o nazwę
zmiennej środowiskowej, domyślnie `ANONYMYZER_CONNECTION`.

## Tabele i kolumny

- `●` oznacza automatycznego kandydata; nie włącza anonimizacji.
- `◆` oznacza jawną decyzję operatora.
- `⚠` oznacza element zachowany w konfiguracji, ale nieobecny w ostatnim skanie.
- `Anonymize table` włącza tabelę w planie. Bez tego jej kolumny nie zostaną
  wykonane.
- `Anonymize` w wierszu kolumny włącza zapis tej kolumny.
- `Semantic role` opisuje znaczenie danych i pomaga automatycznie mapować grupy.
- `Add column` pokazuje ukryte kolumny analizy albo doczytuje metadane z klona.
- `View...` otwiera niemodalny, tylko-odczytowy podgląd wartości `non-null`.

Tabela i każda wybrana kolumna muszą być włączone. Zawsze przejrzyj kolumny
liczbowe: PESEL, NIP czy telefon mogą być zapisane jako liczby i wykryte po nazwie.

## Profile generatorów

`Generators → Profiles...` zarządza wielokrotnie używanymi zestawami parametrów.

1. `Add` tworzy profil z poprawnymi wartościami domyślnymi.
2. Nadaj profilowi stabilny, opisowy identyfikator.
3. `Configure...` otwiera panel właściwy dla dokładnego typu i wersji generatora.
4. `Options JSON` jest awaryjną ścieżką dla generatora bez panelu.

Zmiana profilu wpływa na wszystkie kolumny i grupy, które się do niego odwołują.
Nie umieszczaj sekretów w opcjach. Generatory wymagające sekretu zapisują tylko
nazwę zmiennej środowiskowej.

Pełny katalog parametrów znajduje się w [generators.md](generators.md).

## Grupy spójnych danych

Grupa wykonuje generator raz dla wiersza i mapuje kilka jego wyjść na kolumny.
Zapobiega to niezależnemu losowaniu danych, które powinny być ze sobą zgodne.

Obecne generatory wielowyjściowe:

- `PersonIdentity`: `FirstName`, `LastName`, `Gender`, `Email`;
- `PostalAddress`: `Country`, `Region`, `City`, `Street`, `PostalCode`.

Sposób konfiguracji:

1. Najpierw utwórz i skonfiguruj profil generatora.
2. Wybierz tabelę i kliknij `Edit groups...`.
3. Kliknij `Add group`; identyfikator musi być unikalny w tabeli.
4. Wybierz profil. Lista wyjść zostanie ograniczona do tego generatora, a kolumny
   o zgodnych rolach semantycznych zostaną dopasowane automatycznie.
5. Dodaj, usuń lub popraw mapowania `Generator output → Table column`. Lista
   pokazuje wyjścia wymagane/opcjonalne oraz typ i stan aktywacji kolumny.
6. Opcjonalny `Locale override` zastępuje `Locale` z opcji profilu tylko dla tej
   grupy.
7. Zatwierdź okno i włącz `Anonymize` dla każdej związanej kolumny.
8. Użyj `Refresh sample`, aby sprawdzić jeden spójny wynik w pamięci.

Jedna kolumna nie może należeć do dwóch grup. Wszystkie wymagane wyjścia muszą być
zmapowane. Edytor odrzuca mapowanie do nieobsługiwanego typu i ostrzega przed
zapisem grupy z wyłączoną tabelą lub kolumną. Przypisanie kolumny do grupy usuwa
jej generator bezpośredni; po
usunięciu grupy trzeba świadomie wybrać nowy generator albo pozostawić kolumnę
wyłączoną.

## Próbki i rescan

`Refresh sample` nie zapisuje danych. Generatory syntetyczne `Row` działają w
pamięci. Generatory wymagające danych klona używają ograniczonego odczytu po
ponownej walidacji markera. Komunikat `requires cloned data` oznacza, że uczciwy
podgląd nie jest dostępny dla danego zakresu.

`File → Rescan detached clone...` odświeża metadane, kandydatów, PK i FK, ale nie
usuwa z konfiguracji brakujących obiektów i zachowuje wybory operatora. Rescan
zmienia dokument w pamięci — trzeba go zapisać.

## CLI

```powershell
dotnet run --project .\src\Anonymyzer\Anonymyzer.Console -- generate-config `
  --engine PostgreSql --database anonymyzer_clone `
  --connection-env ANONYMYZER_CONNECTION --marker-id $marker `
  --output .\anonymyzer-config.json

dotnet run --project .\src\Anonymyzer\Anonymyzer.Console -- run `
  --config .\anonymyzer-config.json `
  --connection-env ANONYMYZER_CONNECTION --marker-id $marker --dry-run

dotnet run --project .\src\Anonymyzer\Anonymyzer.Console -- run `
  --config .\anonymyzer-config.json `
  --connection-env ANONYMYZER_CONNECTION --marker-id $marker --execute `
  --report .\anonymyzer-execution-report.json
```

Użyj `--engine SqlServer` dla SQL Server. `--execute` nigdy nie zastępuje
wcześniejszego dry-run i kontroli operatora.
