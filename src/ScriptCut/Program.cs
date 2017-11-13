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

            string source = @"C:\SQL Scripts\data-script.sql"; // default
            if (args.Length > 0 && !string.IsNullOrWhiteSpace(args[1]))
            {
                source = args[0];
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
                                        sw?.Flush();
                                        sw?.Close();
                                        tableIndex++;
                                    }

                                    currentTable = potentiallyNewTableName;
                                    string fileName = $"{tableIndex:D3}.{currentTable}.sql";
                                    string targetFullPath = Path.Combine(targetsFolder, fileName);

                                    sqlFiles.Add(fileName);

                                    Console.WriteLine("Started extracting '{0}' table script into file", currentTable);

                                    sw = new StreamWriter(targetFullPath, append: false);
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
                                    currentTable = null;
                                    sw.Flush();
                                    sw.Close();
                                    sw = null;
                                    tableIndex++;
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
                    sw?.Flush();
                    sw?.Close();
                }
            }

            using (var batchFile = new StreamWriter(Path.Combine(targetsFolder, "insert_all.bat"), append: false))
            {
                foreach (var sqlFile in sqlFiles)
                {
                    string targetFullPath = Path.Combine(targetsFolder, sqlFile);
                    string outputFullPath = Path.Combine(targetsFolder, "output", $"{Path.GetFileNameWithoutExtension(sqlFile)}.txt");

                    batchFile.WriteLine($"sqlcmd -S .\\SQLEXPRESS -i \"{targetFullPath}\" -o \"{outputFullPath}\"");
                }
            }

            Console.WriteLine("DONE!");
        }
    }
}
