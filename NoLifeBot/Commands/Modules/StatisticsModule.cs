using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Disqord;
using Disqord.Bot;
using Disqord.Extensions.Interactivity.Menus.Paged;
using Microsoft.EntityFrameworkCore;
using NoLifeBot.Data;
using NoLifeBot.Extensions;
using Qmmands;

namespace NoLifeBot.Commands.Modules
{
    [Group("stats", "statistics")]
    public class StatisticsModule : DataModule
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

        [Command("channels", "channel")]
        public async Task<DiscordCommandResult> ChannelStatsAsync()
        {
            var histories = await GetVoiceHistoriesForGuildAsync();

            var totalTimesSpentInChannel = new Dictionary<Snowflake, TimeSpan>();
            
            foreach (var voiceHistory in histories)
            {
                foreach (var kvp in voiceHistory.TimesSpentInChannel)
                {
                    if (totalTimesSpentInChannel.TryGetValue(kvp.Key, out var value))
                        totalTimesSpentInChannel[kvp.Key] = value + kvp.Value;
                    else
                        totalTimesSpentInChannel.Add(kvp.Key, kvp.Value);
                }
            }

            var formattedTimesInChannels = totalTimesSpentInChannel.OrderByDescending(x => x.Value).Select(x => $"{Mention.Channel(x.Key)} - {x.Value.GetHumanReadableHours()}").ToArray();
            
            var pageProvider = new ArrayPageProvider<string>(formattedTimesInChannels, Utilities.Formatter<string>($"{Context.Guild.Name} - Time Spent In Channels"));
            return Pages(pageProvider);
        }

        [Command("periods", "period")]
        public async Task<DiscordCommandResult> PeriodsAsync()
            => Response($"I've recorded a total of {await _dbContext.VoicePeriods.CountAsync()} voice periods!");
        
        [Command("periods", "period")]
        public async Task<DiscordCommandResult> PeriodsAsync(IMember member)
            => Response($"I've recorded {await _dbContext.VoicePeriods.Where(x => x.UserId == member.Id).CountAsync()} voice periods for {member.Mention}!");
    }
}