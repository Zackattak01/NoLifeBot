using System;
using Disqord;

namespace NoLifeBot.Extensions
{
    public static class VoiceHistoryExtensions
    {
        public static TimeSpan GetTotalTimeExcludingChannel(this VoiceHistory history, Snowflake? channelId)
        {
            if (channelId is null || !history.TimesSpentInChannel.TryGetValue(channelId.Value, out var timeSpentInChannel))
                return history.TotalTimeSpentInVc;

            return history.TotalTimeSpentInVc - timeSpentInChannel;
        }
    }
}