namespace ScriptCut
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text.RegularExpressions;

    public static class Program
    {
        private static readonly Regex TableStartRegex;
        private static readonly Regex TableEndRegex;
        private static readonly Regex KeepInsertingRegex;

        static Program()
        {
            TableEndRegex = new Regex(@"[\s]*SET IDENTITY_INSERT \[dbo\]\.\[([^\]]*)\] OFF", RegexOptions.Compiled | RegexOptions.IgnoreCase);
            TableStartRegex = new Regex(@"[\s]*SET IDENTITY_INSERT \[dbo\]\.\[([^\]]*)\] ON", RegexOptions.Compiled | RegexOptions.IgnoreCase);
            KeepInsertingRegex = new Regex(@"[\s]*INSERT \[dbo\]\.\[([^\]]*)\][\s\S]*", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        }

        static void Main(string[] args)
        {
            bool useEndRegex = false; // previous file ends at beginning of the previous one.

            // TODO: Use ObscureWare.Console.Command nugget when new one is available to process command line and use for flags and switches...

            string source = @"C:\SQL Scripts\scripted-data.sql"; // default
            string dbName = "_database_";
            if (args.Length > 0 && !string.IsNullOrWhiteSpace(args[0]))
            {
                source = args[0];
            }
            if (args.Length > 1 && !string.IsNullOrWhiteSpace(args[1]))
            {
                dbName = args[1];
            }

            FileInfo ifo;
            try
            {
                ifo = new FileInfo(source);
                if (!ifo.Exists)
                {
                    Console.WriteLine("Source file does not exists!");
                    return;
                }
            }
            catch
            {
                Console.WriteLine("'{0}' is not a valid file name.", source);
                return;
            }

            string targetsFolder = Path.Combine(ifo.DirectoryName, Path.GetFileNameWithoutExtension(ifo.Name) + ".parts");
            if (!Directory.Exists(targetsFolder))
            {
                Directory.CreateDirectory(targetsFolder);
            }

            int tableIndex = 1;
            string currentTable = null;
            StreamWriter sw = null;
            var sqlFiles = new List<string>();

            try
            {
                using (var sourceStream = ifo.OpenText())
                {
                    string line = null;

                    while ((line = sourceStream.ReadLine()) != null)
                    {
                        if (!useEndRegex || currentTable == null)
                        {
                            var match = TableStartRegex.Match(line);
                            if (!match.Success)
                            {
                                match = KeepInsertingRegex.Match(line);
                            }

                            if (match.Success)
                            {
                                string potentiallyNewTableName = match.Groups[1].Value;

                                if (potentiallyNewTableName != currentTable)
                                {
                                    if (!useEndRegex)
                                    {
                                        FinalizePartFile(ref sw, ref currentTable, ref tableIndex);
                                    }

                                    currentTable = potentiallyNewTableName;
                                    string fileName = $"{tableIndex:D3}.{currentTable}.sql";
                                    string targetFullPath = Path.Combine(targetsFolder, fileName);

                                    sqlFiles.Add(fileName);

                                    sw = StartNewPartFile(sw, targetFullPath, dbName, currentTable);
                                }
                            }
                        }

                        if (currentTable != null)
                        {
                            sw?.WriteLine(line);

                            if (useEndRegex)
                            {
                                var match = TableEndRegex.Match(line);
                                if (match.Success)
                                {
                                    FinalizePartFile(ref sw, ref currentTable, ref tableIndex);
                                }
                            }
                        }
                    }
                }
            }
            finally
            {
                if (!useEndRegex)
                {
                    FinalizePartFile(ref sw, ref currentTable, ref tableIndex);
                }
            }

            GenerateRunAllFile(targetsFolder, sqlFiles); // TODO: make switch-optional (default - YES)

            Console.WriteLine("DONE!");
        }

        private static void GenerateRunAllFile(string targetsFolder, List<string> sqlFiles)
        {
            using (var batchFile = new StreamWriter(Path.Combine(targetsFolder, "insert_all.bat"), append: false))
            {
                batchFile.WriteLine("md output");

                foreach (var sqlFile in sqlFiles)
                {
                    string targetFullPath = Path.Combine(targetsFolder, sqlFile);
                    string outputFullPath =
                        Path.Combine(targetsFolder, "output", $"{Path.GetFileNameWithoutExtension(sqlFile)}.txt");

                    batchFile.WriteLine($"sqlcmd -S .\\SQLEXPRESS -i \"{targetFullPath}\" -o \"{outputFullPath}\"");
                }
            }
        }

        private static void FinalizePartFile(ref StreamWriter sw, ref string currentTable, ref int tableIndex)
        {
            if (sw != null)
            {
                sw.WriteLine($"ENABLE TRIGGER ALL ON [dbo].[{currentTable}]");// TODO: triggers optional
                sw.Write("GO");

                sw.Flush();
                sw.Close();
                sw = null;
            }

            currentTable = null;
            tableIndex++;
        }

        private static StreamWriter StartNewPartFile(StreamWriter sw, string targetFullPath, string dbName, string currentTable)
        {
            Console.WriteLine("Started extracting '{0}' table script into file", currentTable);

            sw = new StreamWriter(targetFullPath, append: false);

            sw.WriteLine($"USE [{dbName}]");
            sw.Write("GO");
            sw.WriteLine($"DISABLE TRIGGER ALL ON [dbo].[{currentTable}]"); // TODO: triggers optional
            sw.Write("GO");
            return sw;
        }
    }
}
