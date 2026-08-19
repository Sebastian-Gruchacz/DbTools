# Regional data

The `Polish` and `English` language packs have used weighted selection since
version 1.1.0. Current version 1.2.0 adds full-name formatting while retaining
the same tables and weights. Weights are occurrence counts from official
summaries, and the same seed still produces the same name sequence.

## Poland (`pl-PL`)

- Given names: the top 10 female and male names from the Polish Ministry of
  Digital Affairs summary for 2025. Code data version: `PL-MC-2025`.
- Surnames: 11 leading surname families from the Ministry's published ranking
  based on the PESEL register. Female and male forms share one weighted record.
  Code data version: `PL-PESEL-2022-published-ranking`.

Sources:

- [names given to children in 2025](https://www.gov.pl/web/cyfryzacja/najpopularniejsze-imiona-nadawane-dzieciom-w-drugiej-polowie-2025-roku),
- [PESEL surname ranking](https://www.gov.pl/web/cyfryzacja/poszukiwani---anna-nowak-i-piotr-kowalski),
- [complete annually updated surname dataset](https://dane.gov.pl/pl/dataset/1681,nazwis).

## United States (`en-US`)

- Given names: the top 16 female and male names in the Social Security
  Administration table for births from 2020 through 2025. Code data version:
  `US-SSA-2020-2025`.
- Surnames: the top 16 surnames from the 2010 Census. Code data version:
  `US-Census-2010`.

Sources:

- [SSA names for 2020-2025](https://www.ssa.gov/oact/babynames/decades/names2020s.html),
- [U.S. Census Bureau 2010 surnames](https://www.census.gov/topics/population/genealogy/data/2010_surnames.html).

## Limitations

These are small extracts, not whole-population models. Baby names do not reflect
the age distribution in an existing database, and truncating the lists to the
top ranks inflates their normalized share. The packs are suitable for plausible
test data, but not for statistical analysis or demographic simulation.

Changing a table or its weights requires a language-pack version increase,
because it changes the output produced by an existing seed. Weighted data was
introduced by `PersonIdentity 1.1.0`; profile 1.2.0 retains its sequence and adds
the optional `FullName` output. An older profile must be explicitly replaced by
one matching the installed generator version.
