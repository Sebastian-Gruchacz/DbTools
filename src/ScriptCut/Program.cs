using System;

namespace ScriptCut
{
    using System.IO;
    using System.Text.RegularExpressions;

    class Program
    {

        static Regex tableStartRegex = new Regex(@"SET IDENTITY_INSERT [dbo].[TypeFormOrdersLog] ON");
        static Regex tableEndRegex = new Regex(@"SET IDENTITY_INSERT [dbo].[TypeFormOrdersLog] OFF");

        static void Main(string[] args)
        {
            string source = @"C:\SQL Scripts\script.sql";
            FileInfo ifo = new FileInfo(source);
            if (!ifo.Exists)
            {
                Console.WriteLine("Source file does not exists!");
                return;
            }

            string targetsFolder = Path.Combine(ifo.DirectoryName, ifo.Name + ".parts");
            if (!Directory.Exists(targetsFolder))
            {
                Directory.CreateDirectory(targetsFolder);
            }

            string currentTable = null;
            using (var sourceStream = ifo.OpenText())
            {
                string line = null;
                while ((line = sourceStream.ReadLine()) != null)
                {
                    if (line)


                }



            }

            Console.WriteLine("Hello World!");
        }
    }
}
