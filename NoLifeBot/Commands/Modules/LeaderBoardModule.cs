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
    [Group("leaderboard")]
    public class LeaderBoardModule : DiscordGuildModuleBase
    {
        private readonly NoLifeBotDbContext _dbContext;

        public LeaderBoardModule(NoLifeBotDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        [Command]
        public async Task<DiscordCommandResult> LeaderboardAsync()
        {
            //TODO: support relative scoring

            var histories = await GetVoiceHistoriesForGuildAsync();
            if (histories is null)
                return Response("No voice activity found in this guild!");
            
            (Snowflake UserId, TimeSpan Value) mostTimeInVc = (default, default);
            (Snowflake UserId, TimeSpan Value) mostTimeMuted = (default, default);
            (Snowflake UserId, TimeSpan Value) mostTimeDeafened = (default, default);
            (Snowflake UserId, TimeSpan Value) mostTimeStreaming = (default, default);
            (Snowflake UserId, TimeSpan Value) mostTimeInAfk = (default, default);

            foreach (var voiceHistory in histories)
            {
                if (mostTimeInVc.Value < voiceHistory.TotalTimeSpentInVc)
                {
                    mostTimeInVc = (voiceHistory.UserId, voiceHistory.TotalTimeSpentInVc);
                }

                if (mostTimeMuted.Value < voiceHistory.TimeSpentMuted)
                {
                    mostTimeMuted = (voiceHistory.UserId, voiceHistory.TimeSpentMuted);
                }

                if (mostTimeDeafened.Value < voiceHistory.TimeSpentDeafened)
                {
                    mostTimeDeafened = (voiceHistory.UserId, voiceHistory.TimeSpentDeafened);
                }

                if (mostTimeStreaming.Value < voiceHistory.TimeSpentStreaming)
                {
                    mostTimeStreaming = (voiceHistory.UserId, voiceHistory.TimeSpentStreaming);
                }

                if (Context.Guild.AfkChannelId is { } afkChannelId)
                {
                    voiceHistory.TimesSpentInChannel.TryGetValue(afkChannelId, out var timeSpentInAfk);
                    
                    if (mostTimeInAfk.Value < timeSpentInAfk)
                        mostTimeInAfk = (voiceHistory.UserId, timeSpentInAfk);
                }
            }

            var e = new LocalEmbed().WithTitle(Context.Guild.Name)
                .WithDefaultColor();
                
            if (mostTimeInVc.UserId != default)
                e.AddField("Most Time Spent In Voice", $"{Mention.User(mostTimeInVc.UserId)} - {mostTimeInVc.Value.GetHumanReadableHours()}");
            
            if (mostTimeMuted.UserId != default)
                e.AddField("Most Time Spent Muted", $"{Mention.User(mostTimeMuted.UserId)} - {mostTimeMuted.Value.GetHumanReadableHours()}");

            if (mostTimeDeafened.UserId != default)
                e.AddField("Most Time Spent Deafened", $"{Mention.User(mostTimeDeafened.UserId)} - {mostTimeDeafened.Value.GetHumanReadableHours()}");

            if (mostTimeStreaming.UserId != default)
                e.AddField("Most Time Spent Streaming", $"{Mention.User(mostTimeStreaming.UserId)} - {mostTimeStreaming.Value.GetHumanReadableHours()}");

            if (mostTimeInAfk.UserId != default)
                e.AddField("Most Time Spent In Afk", $"{Mention.User(mostTimeInAfk.UserId)} - {mostTimeInAfk.Value.GetHumanReadableHours()}");
            
            return Response(e);
        }

        [Command("vc", "voice")]
        public Task<DiscordCommandResult> TotalTimeVcLeaderboardAsync()
            => GetLeaderboardForStatisticAsync(x => x.TotalTimeSpentInVc, "Time Spent In Voice Leaderboard");

        [Command("muted", "mute")]
        public Task<DiscordCommandResult> MutedLeaderboardAsync()
            => GetLeaderboardForStatisticAsync(x => x.TimeSpentMuted, "Time Spent Muted Leaderboard");

        [Command("deafened", "deaf")]
        public Task<DiscordCommandResult> DeafenedLeaderboardAsync()
            => GetLeaderboardForStatisticAsync(x => x.TimeSpentDeafened, "Time Spent Deafened Leaderboard");

        [Command("streaming", "stream")]
        public Task<DiscordCommandResult> StreamingLeaderboardAsync()
            => GetLeaderboardForStatisticAsync(x => x.TimeSpentStreaming, "Time Spent Streaming Leaderboard");

        [Command("afk")]
        public Task<DiscordCommandResult> AfkLeaderboardAsync()
        {
            if (Context.Guild.AfkChannelId is null)
                return Task.FromResult(Response("Your guild has no afk channel") as DiscordCommandResult);
            return GetLeaderboardForStatisticAsync(x =>
            {
                x.TimesSpentInChannel.TryGetValue(Context.Guild.AfkChannelId.Value, out var value);
                return value;
            }, "Time in VC Leaderboard");
        }

        private async Task<DiscordCommandResult> GetLeaderboardForStatisticAsync(Func<VoiceHistory, TimeSpan> statisticSelector, string title)
        {
            const int entriesPerPage = 10;
            
            var histories = (await GetVoiceHistoriesForGuildAsync()).Where(x => statisticSelector(x) > TimeSpan.Zero).OrderByDescending(statisticSelector).ToArray();
            var pages = new List<Page>(histories.Length / 10 + 1);
            
            for (var i = 0; i < histories.Length;)
            {
                var embed = new LocalEmbed()
                    .WithDefaultColor()
                    .WithTitle(title)
                    .WithDescription(string.Join("\n", histories[i..Math.Min(i + entriesPerPage, histories.Length)].Select(x => $"{++i}. {Mention.User(x.UserId)} - {statisticSelector(x).GetHumanReadableHours()}")));

                var page = new Page().WithEmbeds(embed);
                pages.Add(page);
            }

            if (pages.Count > 0)
                return Pages(pages);
            else
                return Response("No data found for this category!");
        }
        
        private async Task<IEnumerable<VoiceHistory>> GetVoiceHistoriesForGuildAsync()
        {
            var voicePeriods = await _dbContext.VoicePeriods.Where(x => x.GuildId == Context.GuildId).ToListAsync();

            if (voicePeriods.Count == 0)
                return null;
            
            var uniqueUsers = new HashSet<Snowflake>();
            foreach (var voicePeriod in voicePeriods)
                uniqueUsers.Add(voicePeriod.UserId);
            
            return uniqueUsers.Select(x => new VoiceHistory(voicePeriods.Where(y => y.UserId == x)));
        }
    }
}