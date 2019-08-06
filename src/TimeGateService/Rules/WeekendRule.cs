using System;

namespace TimeGateService.Rules
{
    internal class WeekendRule : IRule
    {
        private readonly TimeSpan MAX_TIME_ALLOWED = new TimeSpan(22, 0, 0);
        private readonly TimeSpan MIN_TIME_ALLOWED = new TimeSpan(7, 0, 0);

        public bool Matches(DateTime snapshot)
        {
            return snapshot.DayOfWeek == DayOfWeek.Friday || snapshot.DayOfWeek == DayOfWeek.Saturday;
        }

        public bool IsRuleBroken(DateTime snapshot)
        {
            var currentTimePart = snapshot.TimeOfDay;

            return (currentTimePart <= MIN_TIME_ALLOWED || currentTimePart >= MAX_TIME_ALLOWED);
        }
    }
}