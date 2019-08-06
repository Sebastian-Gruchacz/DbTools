using System;

namespace TimeGateService.Rules
{
    internal class DefaultRule : IRule
    {
        private readonly TimeSpan MAX_TIME_ALLOWED = new TimeSpan(21, 0, 0);
        private readonly TimeSpan MIN_TIME_ALLOWED = new TimeSpan(7, 0, 0);

        public bool Matches(DateTime snapshot)
        {
            return true;
        }

        public bool IsRuleBroken(DateTime snapshot)
        {
            var currentTimePart = snapshot.TimeOfDay;

            return (currentTimePart <= MIN_TIME_ALLOWED || currentTimePart >= MAX_TIME_ALLOWED);
        }
    }
}