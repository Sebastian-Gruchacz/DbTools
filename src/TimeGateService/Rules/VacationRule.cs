using System;

namespace TimeGateService.Rules
{
    internal class VacationRule : IRule
    {
        private readonly TimeSpan MAX_TIME_ALLOWED = new TimeSpan(22, 40, 0);
        private readonly TimeSpan MIN_TIME_ALLOWED = new TimeSpan(7, 0, 0);

        public bool Matches(DateTime snapshot)
        {
            return snapshot.Month == 7 || snapshot.Month == 8;
        }

        public bool IsRuleBroken(DateTime snapshot)
        {
            var currentTimePart = snapshot.TimeOfDay;

            return (currentTimePart <= MIN_TIME_ALLOWED || currentTimePart >= MAX_TIME_ALLOWED);
        }
    }
}