using System;
using System.Collections.Generic;
using Disqord;
using NoLifeBot.Data.Entities;

namespace NoLifeBot
{
    public class VoiceHistory
    {
        public Snowflake UserId { get; }
        
        public TimeSpan TotalTimeSpentInVc { get; }
        
        public IReadOnlyDictionary<Snowflake, TimeSpan> TimesSpentInChannel { get; }
        
        public TimeSpan TimeSpentDeafened { get; }
        
        public TimeSpan TimeSpentMuted { get; }
        
        public TimeSpan TimeSpentStreaming { get; }
        
        public int VoicePeriodCount { get; }

        public VoiceHistory(IEnumerable<VoicePeriod> voicePeriods)
        {
            var timesSpentInChannel = new Dictionary<Snowflake, TimeSpan>();
            foreach (var voicePeriod in voicePeriods)
            {
                if (UserId == default)
                    UserId = voicePeriod.UserId;
                
                var length = (voicePeriod.EndedAt ?? DateTime.Now) - voicePeriod.StartedAt;

                TotalTimeSpentInVc += length;
                
                if (voicePeriod.WasDeafened)
                    TimeSpentDeafened += length;
                else if (voicePeriod.WasMuted)
                    TimeSpentMuted += length;

                if (voicePeriod.WasStreaming)
                    TimeSpentStreaming += length;

                if (timesSpentInChannel.TryGetValue(voicePeriod.ChannelId, out var timeSpentInChannel))
                    timesSpentInChannel[voicePeriod.ChannelId] = timeSpentInChannel + length;
                else
                    timesSpentInChannel.Add(voicePeriod.ChannelId, length);

                VoicePeriodCount++;
            }

            TimesSpentInChannel = timesSpentInChannel;
        }
    }
}