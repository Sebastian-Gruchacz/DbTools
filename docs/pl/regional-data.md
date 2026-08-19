# Dane regionalne

Pakiety `Polish` i `English` w wersji 1.1.0 używają ważonego losowania. Wagi są
liczbami wystąpień z oficjalnych zestawień, a ten sam seed nadal daje ten sam
ciąg wyników dla tej samej wersji pakietu.

## Polska (`pl-PL`)

- Imiona: pierwszych 10 imion żeńskich i męskich z zestawienia Ministerstwa
  Cyfryzacji za 2025 r. Wersja danych w kodzie: `PL-MC-2025`.
- Nazwiska: 11 czołowych rodzin nazwisk z opublikowanego rankingu Ministerstwa
  Cyfryzacji opartego na rejestrze PESEL. Formy żeńskie i męskie są związane w
  jeden rekord. Wersja danych w kodzie: `PL-PESEL-2022-published-ranking`.

Źródła:

- [imiona nadawane dzieciom w 2025 r.](https://www.gov.pl/web/cyfryzacja/najpopularniejsze-imiona-nadawane-dzieciom-w-drugiej-polowie-2025-roku),
- [ranking nazwisk z rejestru PESEL](https://www.gov.pl/web/cyfryzacja/poszukiwani---anna-nowak-i-piotr-kowalski),
- [pełny, corocznie aktualizowany zbiór nazwisk](https://dane.gov.pl/pl/dataset/1681,nazwis).

## Stany Zjednoczone (`en-US`)

- Imiona: pierwszych 16 imion żeńskich i męskich z tabeli Social Security
  Administration dla urodzeń w latach 2020-2025. Wersja danych w kodzie:
  `US-SSA-2020-2025`.
- Nazwiska: pierwszych 16 nazwisk ze spisu ludności z 2010 r. Wersja danych w
  kodzie: `US-Census-2010`.

Źródła:

- [SSA: imiona w latach 2020-2025](https://www.ssa.gov/oact/babynames/decades/names2020s.html),
- [U.S. Census Bureau: nazwiska ze spisu 2010](https://www.census.gov/topics/population/genealogy/data/2010_surnames.html).

## Ograniczenia

To małe wycinki, a nie modele całej populacji. Imiona dzieci nie odzwierciedlają
rozkładu wieku w istniejącej bazie, a ograniczenie do czołowych pozycji zawyża
ich udział po renormalizacji wag. Pakiety nadają się do realistycznie brzmiących
danych testowych, ale nie do analiz statystycznych ani symulacji demograficznych.

Zmiana tabel lub wag wymaga podniesienia wersji pakietu, ponieważ wpływa na wynik
dla istniejącego seeda. Dlatego ważone dane są związane z `PersonIdentity 1.1.0`;
profil wskazujący wersję 1.0.0 trzeba jawnie zastąpić profilem 1.1.0.
