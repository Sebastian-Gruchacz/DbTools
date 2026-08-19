# Anonymyzer generator catalogue

[Wersja polska](../pl/generators.md)

Profiles are versioned parameter sets. Prefer creating them through
`Generators → Profiles... → Add` and editing them with `Configure...`; the panel
writes the same `Options` object consumed by the CLI. Field names below match JSON.

## Common concepts

- `Seed` provides a deterministic, repeatable sequence for the same configuration.
- `PreserveNulls = true` leaves source `NULL` values unchanged.
- `Locale` selects an active language pack, currently `pl-PL` or `en-US` where
  both are supported.
- `Original` reads the value before anonymization; `Generated` depends on an
  earlier generated result and affects plan ordering.
- `Row` works on the current row, `Column` needs a complete column scan, and
  `Relational` may read other tables.

## Multi-output generators

### PersonIdentity (`Row`)

Creates a coherent first name, last name, gender, and email. Use it through a
group and map any non-empty subset of `FirstName`, `LastName`, `Gender`, `Email`.

- `Locale`: `pl-PL` or `en-US`.
- `Seed`: deterministic person selection.
- `EmailPattern`: `NameBased` or `Opaque`.
- `EmailDomain`: output domain; the default `example.invalid` is non-deliverable.

### PostalAddress (`Row`)

Creates `Country`, `Region`, `City`, `Street`, and `PostalCode` together. City and
postal code come from one regional-provider record.

- `Locale`: `pl-PL` or `en-US`.
- `Seed`: deterministic address selection.

## Simple text generators

### FixedText (`Row`)

- `Value`: constant text, `REDACTED` by default.
- `PreserveNulls`.

### SequentialText (`Row`)

Creates values such as `anon-00000001`.

- `Prefix`, `Suffix`;
- `StartAt`: first number;
- `MinimumDigits`: minimum zero-padded width;
- `PreserveNulls`.

### Uuid (`Row`)

- `Seed`: text seed for deterministic UUIDs;
- `StartAt`: initial counter;
- `Format`: `Hyphenated`, `Compact`, `Braced`, or `Parenthesized`;
- `Uppercase`, `PreserveNulls`.

### EmailAddress (`Row`)

- `Pattern`: `Opaque` or `NameBased`;
- `Domain`: `example.invalid` by default;
- `OpaquePrefix`, `StartAt`, `MinimumDigits`: numbered opaque form;
- `FirstNameColumn`, `LastNameColumn`: dependencies for `NameBased`;
- `NameValueSource`: `Original` or `Generated`;
- `PreserveNulls`.

With `Generated`, the first-name and last-name columns must have active earlier
steps.

### AccountLogin (`Row`)

- `Pattern`: `Opaque` or `NameBased`;
- `OpaquePrefix`, `StartAt`, `MinimumDigits`;
- `FirstNameColumn`, `LastNameColumn`, `NameValueSource`;
- `Separator`: separator between login components;
- `PreserveNulls`.

### PhoneNumber (`Row`)

- `Locale`: `pl-PL` or `en-US`;
- `Format`: `National` or `International`;
- `Seed`, `PreserveNulls`.

The `en-US` variant uses the reserved fictional 555-0100–0199 range.

### CompanyName (`Row`)

- `Locale`;
- `SyntheticMarker`: required marker identifying test data;
- `IncludeLegalForm`: includes a regional legal form;
- `Seed`, `PreserveNulls`.

### TaxIdentifier (`Row`)

Supports Polish NIP and REGON with valid check digits.

- `Locale`: currently `pl-PL`;
- `Variant`: `NIP`, `REGON9`, or `REGON14`;
- `Format`: `DigitsOnly`, `Hyphenated`, or `International`;
- `Seed`, `PreserveNulls`.

REGON supports `DigitsOnly` only; the other formats apply to NIP.

### BankAccount (`Row`)

Creates mathematically valid, deliberately non-routable Polish IBAN/NRB values.

- `Locale`: currently `pl-PL`;
- `Format`: `IbanCompact`, `IbanGrouped`, or `DomesticNrb`;
- `Seed`, `PreserveNulls`.

## Person data

### BirthDate (`Row`)

- `MinimumDate`, `MaximumDate`: inclusive, `yyyy-MM-dd`;
- `Seed`, `PreserveNulls`.

Supports `Date` and `DateTime` columns.

### Gender (`Row`)

- `FemaleValue`, `MaleValue`: values written to the database;
- `FemalePercentage`: ratio from 0 to 100;
- `Seed`, `PreserveNulls`.

### NationalIdentifier (`Row`)

Creates a Polish PESEL or a safely unassigned US SSN.

- `Locale`: `pl-PL` or `en-US`;
- `MinimumBirthDate`, `MaximumBirthDate`;
- `Gender`: `Any`, `Female`, or `Male`;
- `BirthDateColumn`, `BirthDateValueSource`;
- `GenderColumn`, `GenderValueSource`;
- `FemaleValues`, `MaleValues`: source-value gender mapping;
- `Seed`, `PreserveNulls`.

Empty column names use the configured date range and `Gender` setting. A
`Generated` dependency must point to an active step in the same table.

## Generators using existing data

### JsonPathRedactor (`Row`)

Replaces selected JSON fragments while retaining the rest of the document.

- `Rules`: objects containing `Path` and `ReplacementJson`;
- `Path`: supported JSON path;
- `ReplacementJson`: valid JSON such as `null`, `"REDACTED"`, or `0`;
- `RequireEveryPath`: fail when any configured path is absent.

Supports text and PostgreSQL `json/jsonb`. Do not approve rules solely from a
truncated sample.

### TextShuffler (`Column`)

Deterministically permutes a complete column and preserves its exact multiset.

- `Seed`;
- `MinimumPopulation`: minimum population required for shuffling;
- `PreserveNulls`;
- `MaximumInMemoryBytes`: buffer limit;
- `OverflowStrategy`: `Fail` or `EncryptedTemporaryFiles`.

The file strategy encrypts content with an ephemeral key and cleans up after the
session, but it still needs sufficient temporary disk space.

### ReferencePseudonym (`Relational`)

Creates the same HMAC pseudonym for rows sharing a foreign key. It does not modify
PK/FK columns; it writes a separate text column.

- `ReferenceColumn`: FK in the target table;
- `LookupSchema`, `LookupTable`, `LookupKeyColumn`: allowed-key source;
- `Prefix`, `HashLength`;
- `KeyEnvironmentVariable`: environment variable containing an HMAC secret of at
  least 32 characters;
- `MaximumInMemoryBytes`;
- `OverflowStrategy`: `Fail` or `EncryptedTemporaryIndex`;
- `PreserveNulls`.

The editor prioritizes foreign-key relations found during scanning. A shorter
`HashLength` increases collision risk; a detected collision aborts execution.
