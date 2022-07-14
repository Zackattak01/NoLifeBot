using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Disqord;
using Disqord.Bot;
using Disqord.Extensions.Interactivity.Menus.Paged;
using Microsoft.EntityFrameworkCore;
using NoLifeBot.Extensions;
using Qmmands;

namespace NoLifeBot.Commands.Modules
{
    [Group("stats", "statistics")]
    public class StatisticsModule : DataModule
    {
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

        [Command("time", "total")]
        public async Task<DiscordCommandResult> TotalTimeAsync()
        {
            var periods = await DbContext.VoicePeriods.ToListAsync();
            var totalHours = periods.Sum(x => ((x.EndedAt ?? DateTime.Now) - x.StartedAt).TotalHours);
            var totalGuildHours = periods.Where(x => x.GuildId == Context.GuildId).Sum(x => ((x.EndedAt ?? DateTime.Now) - x.StartedAt).TotalHours);
            var totalUserHours = periods.Where(x => x.UserId == Context.Author.Id).Sum(x => ((x.EndedAt ?? DateTime.Now) - x.StartedAt).TotalHours);
            var trackingStartedDate = periods.MinBy(x => x.StartedAt)?.StartedAt ?? DateTime.Now;
            return Response($"I've watched over {totalHours:F1} hours of voice activity total, {totalGuildHours:F1} hours in this guild, and {totalUserHours:F1} hours of your time since {Markdown.Timestamp(trackingStartedDate, Markdown.TimestampFormat.LongDate)}");
        }

        [Group("period", "periods")]
        public class PeriodModule : DataModule
        {
            [Command]
            public async Task<DiscordCommandResult> PeriodsAsync()
                => Response($"I've recorded a total of {Markdown.Code(await DbContext.VoicePeriods.CountAsync())} voice periods!");
        
            [Command]
            public async Task<DiscordCommandResult> PeriodsAsync(IMember member)
                => Response($"I've recorded {Markdown.Code(await DbContext.VoicePeriods.Where(x => x.UserId == member.Id).CountAsync())} voice periods for {member.Mention}!");
            
            [Command("active")]
            public async Task<DiscordCommandResult> ActiveAsync()
            {
                var activePeriods = await DbContext.VoicePeriods.Where(x => x.EndedAt == null).ToListAsync();
                return Response($"There are {Markdown.Code(activePeriods.Count(x => x.GuildId == Context.GuildId))} active voice periods in this guild and {Markdown.Code(activePeriods.Count)} total");
            }

            [Command("length")]
            public async Task<DiscordCommandResult> LengthAsync()
            {
                var periods = await DbContext.VoicePeriods.ToListAsync();
                var totalMinutes = periods.Sum(x => ((x.EndedAt ?? DateTime.Now) - x.StartedAt).TotalMinutes);
                var averageTimePerPeriod = totalMinutes / periods.Count;
                return Response($"The average length of the {Markdown.Code(periods.Count)} periods I've recorded is {averageTimePerPeriod:F1} hours per period");
            }
            
            [Command("length")]
            public async Task<DiscordCommandResult> LengthAsync(IMember user)
            {
                var periods = await DbContext.VoicePeriods.Where(x => x.UserId == user.Id).ToListAsync();
                var totalMinutes = periods.Sum(x => ((x.EndedAt ?? DateTime.Now) - x.StartedAt).TotalMinutes);
                var averageTimePerPeriod = totalMinutes / periods.Count;
                return Response($"The average length of the {Markdown.Code(periods.Count)} periods I've recorded for {user.Mention} is {averageTimePerPeriod:F1} hours per period");
            }
        }
    }
}