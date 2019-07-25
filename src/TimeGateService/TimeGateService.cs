using System;
using System.ComponentModel;
using System.Management;
using System.ServiceProcess;

namespace TimeGateService
{
    public partial class TimeGateService : ServiceBase
    {
        private readonly BackgroundWorker _backgroundMonitor;
        private readonly TimeSpan MAX_TIME_ALLOWED = new TimeSpan(23,0,0);
        private readonly TimeSpan MIN_TIME_ALLOWED = new TimeSpan(7, 0, 0);

        private DateTime _lastSnapshot;

        public TimeGateService()
        {
            InitializeComponent();

            _lastSnapshot = DateTime.Now;

            this._backgroundMonitor = new BackgroundWorker();
            this._backgroundMonitor.DoWork += BackgroundMonitorOnDoWork;
        }

        private void BackgroundMonitorOnDoWork(object sender, DoWorkEventArgs e)
        {
            System.Threading.Thread.Sleep(1000);

            while (!this._backgroundMonitor.CancellationPending)
            {
                var snapshot = DateTime.Now;

                // tamper protect
                if ((_lastSnapshot - snapshot) > new TimeSpan(0, 0, 2))
                {
                    DoShutdown();
                    return;
                }

                // normal time-check
                var currentTimePart = snapshot.TimeOfDay;
                if (currentTimePart <= MIN_TIME_ALLOWED || currentTimePart >= MAX_TIME_ALLOWED)
                {
                    DoShutdown();
                    return;
                }

                _lastSnapshot = snapshot;

                System.Threading.Thread.Sleep(1000);
            }
        }

        private void DoShutdown()
        {
            ManagementBaseObject mboShutdown = null;
            ManagementClass mcWin32 = new ManagementClass("Win32_OperatingSystem");
            mcWin32.Get();

            // You can't shutdown without security privileges
            mcWin32.Scope.Options.EnablePrivileges = true;
            ManagementBaseObject mboShutdownParams = mcWin32.GetMethodParameters("Win32Shutdown");

            // Flag 1 means we want to shut down the system. Use "2" to reboot.
            mboShutdownParams["Flags"] = "12"; //Forced Power Off (8 + 4) 
            mboShutdownParams["Reserved"] = "0";
            foreach (ManagementObject manObj in mcWin32.GetInstances())
            {
                mboShutdown = manObj.InvokeMethod("Win32Shutdown", mboShutdownParams, null);
            }
        }

        protected override void OnStart(string[] args)
        {
            this._backgroundMonitor.RunWorkerAsync();
        }

        protected override void OnStop()
        {
            this._backgroundMonitor.CancelAsync();
        }
    }
}
