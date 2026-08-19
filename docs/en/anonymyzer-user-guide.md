# Anonymyzer operator guide

[Wersja polska](../pl/anonymyzer-user-guide.md)

## Safety rule

Anonymyzer modifies a database in place. Use only a detached clone created by a
backup/restore or an equivalent procedure. The production connection string
should not be available to the computer or account running Anonymyzer.

Every clone must contain a marker from `tools/markers`. The database name, marker
identifier supplied by the operator, and marker stored in the database must
match. A connection string is passed only through an environment variable and is
never stored in the JSON configuration.

## Recommended workflow

1. Restore a detached clone and add its marker.
2. Create a configuration with `generate-config`.
3. Open the JSON in the WPF editor and review every table and candidate.
4. Configure generator profiles, columns, and coherent groups.
5. Save the document and run `run --dry-run`.
6. Resolve every error; execute only a plan reported as `write slice ready`.
7. Run `run --execute` with a report and, for a supported plan, a checkpoint.
8. Review the report and clone validation before releasing the clone.

Checkpoint format 2 supports deterministic `Row` plans and
`ReferencePseudonym` reading an unchanged lookup table. The file contains HMACs
of the primary-key boundary and environment dependencies, but never their
secrets. Changing the pseudonymization secret between runs refuses resume before
any write. `Column`, complete scans of the target table, and reads of overwritten
values remain excluded from resume.

## Starting the editor

```powershell
dotnet run --project .\src\Anonymyzer\Anonymyzer.ConfigEditor\Anonymyzer.ConfigEditor.csproj
```

The editor does not store connection strings. Sample and rescan windows ask for
an environment-variable name, `ANONYMYZER_CONNECTION` by default.

## Tables and columns

- `●` is an automatic candidate; it does not enable anonymization.
- `◆` is an explicit operator decision.
- `⚠` is an item retained in the configuration but absent from the latest scan.
- `Anonymize table` includes the table in the plan. Its columns will not run
  without it.
- `Anonymize` in a column row enables writes to that column.
- `Semantic role` describes the data and supports automatic group mappings.
- `Add column` reveals saved analysis columns or reloads metadata from the clone.
- `View...` opens a non-modal, read-only view of non-null values.

Both the table and every selected column must be enabled. Always review numeric
columns: national IDs, tax IDs, or phone numbers may be stored as numbers and
detected by name.

## Generator profiles

`Generators → Profiles...` manages reusable parameter sets.

1. `Add` creates a profile with valid defaults.
2. Give the profile a stable, descriptive identifier.
3. `Configure...` opens the panel supplied by the exact generator version.
4. `Options JSON` is the fallback for a generator without a dedicated panel.

Changing a profile affects every column and group that references it. Never put
secrets in options. A generator that needs a secret stores only the environment
variable name.

See [generators.md](generators.md) for the complete parameter catalogue.

## Coherent generation groups

A group invokes a generator once per row and maps several outputs to columns.
This prevents values that must agree from being generated independently.

Current multi-output generators:

- `PersonIdentity`: `FirstName`, `LastName`, `FullName`, `Gender`, `Email`;
- `PostalAddress`: `Country`, `Region`, `City`, `Street`, `PostalCode`.

To configure a group:

1. Create and configure its generator profile first.
2. Select a table and click `Edit groups...`.
3. Click `Add group`; its identifier must be unique within the table.
4. Select a profile. Outputs are restricted to that generator, and columns with
   matching semantic roles are mapped automatically.
5. Add, remove, or correct `Generator output → Table column` mappings. The lists
   show required/optional outputs and each column's type and activation state.
6. Optional `Locale override` replaces the profile option named `Locale` for
   this group only.
7. Accept the dialog and enable `Anonymize` for every bound column.
8. Use `Refresh sample` to inspect one coherent in-memory result.

A column cannot belong to two groups. Every required output must be mapped.
The editor rejects mappings to unsupported types and warns before saving a group
with a disabled table or column. Assigning a column to a group clears its direct
generator; after removing the
group, deliberately select another generator or leave the column disabled.

## Samples and rescan

`Refresh sample` never writes data. Synthetic `Row` generators run in memory.
Generators that need clone data use a bounded read after validating the marker
again. `requires cloned data` means that an honest preview is not available for
that execution scope.

`File → Rescan detached clone...` refreshes metadata, candidates, primary keys,
and foreign keys. It retains missing objects and operator decisions. A rescan
changes the in-memory document and must be saved explicitly.

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

Use `--engine SqlServer` for SQL Server. `--execute` never replaces an earlier
dry-run and operator review.
