using System;

namespace TimeGateService
{
    internal interface IRule
    {
        bool IsRuleBroken(DateTime snapshot);
        bool Matches(DateTime snapshot);
    }
}