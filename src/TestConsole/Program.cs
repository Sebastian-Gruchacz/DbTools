using System;

namespace TestConsole
{
    class Program
    {
        private static readonly TimeSpan MAX_TIME_ALLOWED = new TimeSpan(23, 0, 0);
        private static readonly TimeSpan MIN_TIME_ALLOWED = new TimeSpan(7, 0, 0);
        private static DateTime _lastSnapshot;


        static void Main(string[] args)
        {
            _lastSnapshot = DateTime.Now;
            System.Threading.Thread.Yield();

            while (true)
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

                System.Threading.Thread.Yield();
            }
        }

        private static void DoShutdown()
        {
            Console.WriteLine("!");
        }
    }
}
