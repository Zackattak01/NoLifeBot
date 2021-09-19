using System;

namespace NoLifeBot.Extensions
{
    public static class TimeSpanExtensions
    {
        public static string GetHumanReadableHours(this TimeSpan ts)
            => ts.TotalHours.ToString("F1") + " hours";
        
    }
}