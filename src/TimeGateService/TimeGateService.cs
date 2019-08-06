using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Management;
using System.ServiceProcess;

using TimeGateService.Rules;

namespace TimeGateService
{
    public partial class TimeGateService : ServiceBase
    {
        private readonly BackgroundWorker _backgroundMonitor;
        private readonly TimeSpan TAMPER_LIMIT =  new TimeSpan(0, 0, 30);

        private DateTime _lastSnapshot;
        private readonly List<IRule> _rules = new List<IRule>();
        private readonly IRule _defaultRule = new DefaultRule();

        public TimeGateService()
        {
            InitializeComponent();

            _lastSnapshot = DateTime.Now;

            _rules.Add(new VacationRule());
            _rules.Add(new WeekendRule());
            // ...

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
                if ((_lastSnapshot - snapshot) > TAMPER_LIMIT)
                {
                    DoShutdown();
                    return;
                }

                // normal time-check
                var rule = GetRule(snapshot);
                if (rule.IsRuleBroken(snapshot))
                {
                    DoShutdown();
                    return;
                }

                _lastSnapshot = snapshot;

                System.Threading.Thread.Sleep(1000);
            }
        }

        private IRule GetRule(DateTime snapshot)
        {
            return _rules.FirstOrDefault(r => r.Matches(snapshot)) ?? _defaultRule;
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
