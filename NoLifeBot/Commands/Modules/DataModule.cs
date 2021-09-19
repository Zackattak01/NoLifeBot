using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Disqord;
using Disqord.Bot;
using Microsoft.EntityFrameworkCore;
using NoLifeBot.Data;

namespace NoLifeBot.Commands.Modules
{
    public class DataModule : DiscordGuildModuleBase
    {
        public NoLifeBotDbContext DbContext { get; set; }
        
        protected async Task<VoiceHistory> GetVoiceHistoryForUserAsync(IMember member)
        {
            var voicePeriods = await DbContext.VoicePeriods.Where(x => x.UserId == member.Id && x.GuildId == member.GuildId).ToListAsync();

            if (voicePeriods.Count == 0)
                return null;
            
            return new VoiceHistory(voicePeriods);
        }
        
        protected async Task<IEnumerable<VoiceHistory>> GetVoiceHistoriesForGuildAsync()
        {
            var voicePeriods = await DbContext.VoicePeriods.Where(x => x.GuildId == Context.GuildId).ToListAsync();

            if (voicePeriods.Count == 0)
                return null;
            
            var uniqueUsers = new HashSet<Snowflake>();
            foreach (var voicePeriod in voicePeriods)
                uniqueUsers.Add(voicePeriod.UserId);
            
            return uniqueUsers.Select(x => new VoiceHistory(voicePeriods.Where(y => y.UserId == x)));
        }
    }
}