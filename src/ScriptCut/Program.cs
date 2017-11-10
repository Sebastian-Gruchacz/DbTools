namespace ScriptCut
{
    using System;
    using System.IO;
    using System.Text.RegularExpressions;

    public static class Program
    {
        private static readonly Regex TableStartRegex;
        private static readonly Regex TableEndRegex;

        static Program()
        {
            TableEndRegex = new Regex(@"SET IDENTITY_INSERT \[dbo\]\.\[([^\]]*)\] OFF", RegexOptions.Compiled | RegexOptions.IgnoreCase);
            TableStartRegex = new Regex(@"SET IDENTITY_INSERT \[dbo\]\.\[([^\]]*)\] ON", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        }

        static void Main(string[] args)
        {
            string source = @"J:\SQL Scripts\script.sql"; // default
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

            string currentTable = null;
            StreamWriter sw = null;

            using (var sourceStream = ifo.OpenText())
            {
                string line = null;

                while ((line = sourceStream.ReadLine()) != null)
                {
                    if (currentTable == null)
                    {
                        var match = TableStartRegex.Match(line);
                        if (match.Success)
                        {
                            currentTable = match.Groups[1].Value;
                            sw = new StreamWriter(Path.Combine(targetsFolder, currentTable + ".sql"));

                            Console.WriteLine("Started extracting '{0}' table script into file", currentTable);
                        }
                    }
                    
                    if (currentTable != null)
                    {
                        sw.WriteLine(line);

                        var match = TableEndRegex.Match(line);
                        if (match.Success)
                        {
                            currentTable = null;
                            sw.Flush();
                            sw.Close();
                            sw = null;
                        }
                    }
                }
            }

            Console.WriteLine("DONE!");
        }
    }
}
