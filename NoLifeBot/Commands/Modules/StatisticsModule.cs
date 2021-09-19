using System;
using System.Linq;
using System.Threading.Tasks;
using Disqord;
using Disqord.Bot;
using Microsoft.EntityFrameworkCore;
using NoLifeBot.Data;
using NoLifeBot.Extensions;
using Qmmands;

namespace NoLifeBot.Commands.Modules
{
    [Group("stats", "statistics")]
    public class StatisticsModule : DiscordGuildModuleBase
    {
        private readonly NoLifeBotDbContext _dbContext;

        public StatisticsModule(NoLifeBotDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        [Command]
        public async Task<DiscordCommandResult> StatsAsync(IMember member = null)
        {
            member ??= Context.Author;
            var voiceHistory = await GetVoiceHistoryForUserAsync(member);

            if (voiceHistory is null)
                return Response($"No voice data recorded for {member.Mention}");

            var favoriteChannel = voiceHistory.TimesSpentInChannel.OrderByDescending(x => x.Value).FirstOrDefault();

            var embed = new LocalEmbed()
                .WithDefaultColor()
                .WithTitle($"{Context.Guild.Name} - {member.Nick ?? member.Name}")
                .AddField("Time Spent In Voice", voiceHistory.TotalTimeSpentInVc.GetHumanReadableHours())
                .AddField("Time Spent Muted", voiceHistory.TimeSpentMuted.GetHumanReadableHours())
                .AddField("Time Spent Deafened", voiceHistory.TimeSpentDeafened.GetHumanReadableHours())
                .AddField("Time Spent Streaming", voiceHistory.TimeSpentStreaming.GetHumanReadableHours());
                

            if (Context.Guild.AfkChannelId is { } afkChannelId)
            {
                voiceHistory.TimesSpentInChannel.TryGetValue(afkChannelId, out var value);
                embed.AddField("Time Spent In Afk", value.GetHumanReadableHours());
            }

            embed.AddField("Favorite Channel", $"{Mention.Channel(favoriteChannel.Key)} - {favoriteChannel.Value.GetHumanReadableHours()}");
            
            return Response(embed);
        }

        [Command("periods", "period")]
        public async Task<DiscordCommandResult> PeriodsAsync()
            => Response($"I've recorded a total of {await _dbContext.VoicePeriods.CountAsync()} voice periods!");
        
        [Command("periods", "period")]
        public async Task<DiscordCommandResult> PeriodsAsync(IMember member)
            => Response($"I've recorded {await _dbContext.VoicePeriods.Where(x => x.UserId == member.Id).CountAsync()} voice periods for {member.Mention}!");
        
        private async Task<VoiceHistory> GetVoiceHistoryForUserAsync(IMember member)
        {
            var voicePeriods = await _dbContext.VoicePeriods.Where(x => x.UserId == member.Id && x.GuildId == Context.GuildId).ToListAsync();

            if (voicePeriods.Count == 0)
                return null;
            
            return new VoiceHistory(voicePeriods);
        }
    }
}