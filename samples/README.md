# Sample configurations

## SQL Server: Test_OWS_Anonymyzer

`sqlserver/Test_OWS_Anonymyzer.json` został wygenerowany z lokalnego,
odłączonego klona bazy `Test_OWS`.

- wszystkie tabele i kolumny są domyślnie wyłączone;
- 18 kolumn ma propozycje ról EN/PL widoczne w UI jako kandydaci; żadna z nich
  nie została automatycznie włączona;
- plik nie zawiera connection stringa ani wartości z wierszy bazy;
- marker wiąże konfigurację z aktualnym klonem `Test_OWS_Anonymyzer`;
- po ponownym utworzeniu klona należy wygenerować nowy plik z nowym markerem.

Plik można otworzyć bez połączenia z bazą w edytorze WPF:

```powershell
dotnet run --project .\src\Anonymyzer\Anonymyzer.ConfigEditor\Anonymyzer.ConfigEditor.csproj
```

## Publiczne bazy testowe

Powtarzalny downloader Chinook, Northwind, AdventureWorksLT, Pagila i opcjonalnego
WideWorldImporters opisuje [docs/sample-databases.md](../docs/sample-databases.md).
Pobrane pliki trafiają do ignorowanego katalogu `artifacts/sample-databases` i nie
są commitowane.
