using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Disqord;
using Disqord.Bot;
using Disqord.Extensions.Interactivity.Menus.Paged;
using Disqord.Rest.Api;
using Microsoft.EntityFrameworkCore;
using NoLifeBot.Data;
using NoLifeBot.Extensions;
using NoLifeBot.Services;
using Qmmands;

namespace NoLifeBot.Commands.Modules
{
    [Group("leaderboard")]
    public class LeaderboardModule : DataModule
    {
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
            var totalTimesSpentInChannel = new Dictionary<Snowflake, TimeSpan>();

            foreach (var voiceHistory in histories)
            {
                var totalTimeExcludingAfk = voiceHistory.GetTotalTimeExcludingChannel(Context.Guild.AfkChannelId);
                if (mostTimeInVc.Value < totalTimeExcludingAfk)
                    mostTimeInVc = (voiceHistory.UserId, totalTimeExcludingAfk);

                if (mostTimeMuted.Value < voiceHistory.TimeSpentMuted)
                    mostTimeMuted = (voiceHistory.UserId, voiceHistory.TimeSpentMuted);

                if (mostTimeDeafened.Value < voiceHistory.TimeSpentDeafened)
                    mostTimeDeafened = (voiceHistory.UserId, voiceHistory.TimeSpentDeafened);

                if (mostTimeStreaming.Value < voiceHistory.TimeSpentStreaming)
                    mostTimeStreaming = (voiceHistory.UserId, voiceHistory.TimeSpentStreaming);

                if (Context.Guild.AfkChannelId is { } afkChannelId)
                {
                    voiceHistory.TimesSpentInChannel.TryGetValue(afkChannelId, out var timeSpentInAfk);
                    
                    if (mostTimeInAfk.Value < timeSpentInAfk)
                        mostTimeInAfk = (voiceHistory.UserId, timeSpentInAfk);
                }

                foreach (var kvp in voiceHistory.TimesSpentInChannel)
                {
                    if (totalTimesSpentInChannel.TryGetValue(kvp.Key, out var value))
                        totalTimesSpentInChannel[kvp.Key] = value + kvp.Value;
                    else
                        totalTimesSpentInChannel.Add(kvp.Key, kvp.Value);
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
            
            var mostPopularChannel = totalTimesSpentInChannel.OrderByDescending(x => x.Value).FirstOrDefault();
            if (mostPopularChannel.Value > TimeSpan.Zero)
                e.AddField("Most Popular Channel", $"{Mention.Channel(mostPopularChannel.Key)} - {mostPopularChannel.Value.GetHumanReadableHours()}");
            
            return Response(e);
        }

        [Command("vc", "voice")]
        public Task<DiscordCommandResult> TotalTimeVcLeaderboardAsync()
            => GetLeaderboardForStatisticAsync(x => x.GetTotalTimeExcludingChannel(Context.Guild.AfkChannelId), "Time Spent In Voice Leaderboard");

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
            }, "Time Spent In Afk Leaderboard");
        }

        private async Task<DiscordCommandResult> GetLeaderboardForStatisticAsync(Func<VoiceHistory, TimeSpan> statisticSelector, string title)
        {
            var histories = (await GetVoiceHistoriesForGuildAsync()).Where(x => statisticSelector(x) > TimeSpan.Zero).OrderByDescending(statisticSelector).ToArray();

            if (histories.Length == 0)
                return Response("No data found for this category!");

            var formattedHistories = histories.Select(x => $"{Mention.User(x.UserId)} - {statisticSelector(x).GetHumanReadableHours()}").ToArray();

            var pageProvider = new ArrayPageProvider<string>(formattedHistories, Utilities.Formatter<string>(title));
            return Pages(pageProvider);
        }
        

    }
}