using System.ServiceProcess;

namespace TimeGateService
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        static void Main()
        {
            var servicesToRun = new ServiceBase[]
            {
                new TimeGateService()
            };

            ServiceBase.Run(servicesToRun);
        }
    }
}
