using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Disqord;
using Disqord.Bot;
using Disqord.Bot.Hosting;
using Disqord.Gateway;
using Disqord.Rest;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NoLifeBot.Data;
using NoLifeBot.Data.Entities;

namespace NoLifeBot.Services
{
    public class VoiceTracker : DiscordBotService
    {
        private static readonly SemaphoreSlim EventSemaphore = new(1, 1);
        
        private readonly IServiceScopeFactory _scopeFactory;

        public VoiceTracker(ILogger<VoiceTracker> logger, DiscordBotBase bot, IServiceScopeFactory scopeFactory) : base(logger, bot)
        {
            _scopeFactory = scopeFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await EventSemaphore.WaitAsync(stoppingToken);

            await Bot.WaitUntilReadyAsync(stoppingToken);

            await RemoveOrphanedVoicePeriodsAsync();
            await DiscoverUnknownConnectionsAsync();

            EventSemaphore.Release();
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<NoLifeBotDbContext>();

            var voicePeriods = await dbContext.VoicePeriods.Where(x => x.EndedAt == null).ToListAsync(cancellationToken);

            foreach (var voicePeriod in voicePeriods)
            {
                voicePeriod.EndedAt = DateTime.Now;
                dbContext.Update(voicePeriod);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
        }

        private async Task DiscoverUnknownConnectionsAsync()
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<NoLifeBotDbContext>();

            var knownVoicePeriods = (await dbContext.VoicePeriods.Where(x => x.EndedAt == null).ToListAsync()).ToDictionary(x => x.UserId);
            
            var voiceStates = new Dictionary<Snowflake, IVoiceState>();

            foreach (var kvp in Bot.GetGuilds())
            {
                foreach (var voiceStatePair in kvp.Value.GetVoiceStates())
                {
                    var user = Client.GetUser(voiceStatePair.Value.MemberId) as IUser ?? await Client.FetchUserAsync(voiceStatePair.Value.MemberId);
                    if (user.IsBot)
                        continue;
                    
                    voiceStates.TryAdd(voiceStatePair.Key, voiceStatePair.Value);
                }
            }

            var ongoingConnectionsDiscovered = 0;
            foreach (var kvp in voiceStates)
            {
                if (!knownVoicePeriods.TryGetValue(kvp.Key, out _))
                {
                    ongoingConnectionsDiscovered++;
                    dbContext.Add(new VoicePeriod(kvp.Value));
                }
            }

            if (ongoingConnectionsDiscovered > 0)
            {
                Logger.LogInformation($"Discovered {ongoingConnectionsDiscovered} ongoing connections.");
                await dbContext.SaveChangesAsync();
            }
        }

        private async Task RemoveOrphanedVoicePeriodsAsync()
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<NoLifeBotDbContext>();
            
            var orphanedVoicePeriods = await dbContext.VoicePeriods.Where(x => x.EndedAt == null).OrderByDescending(x => x.StartedAt).ToListAsync();
            var removedOrphans = 0;
            var resumedPeriods = 0;
            
            foreach (var orphanedVoicePeriod in orphanedVoicePeriods)
            {
                var (succeeded, voiceState) = TryResumePeriod(orphanedVoicePeriod);
                if (!succeeded)
                {
                    dbContext.Remove(orphanedVoicePeriod);
                    removedOrphans++;
                    if (voiceState?.ChannelId is not null)
                        dbContext.Add(new VoicePeriod(voiceState));
                }
                else
                {
                    resumedPeriods++;
                    await EndCurrentVoicePeriodAsync(orphanedVoicePeriod.UserId, dbContext);
                    dbContext.Add(new VoicePeriod(voiceState));
                }
            }

            if(resumedPeriods > 0)
                Logger.LogInformation($"Resumed {resumedPeriods} voice periods");
            
            if (removedOrphans > 0)
                Logger.LogInformation($"Removing {removedOrphans} orphaned voice periods");
            
            await dbContext.SaveChangesAsync();
        }

        private (bool Succeeded, IVoiceState VoiceState) TryResumePeriod(VoicePeriod period)
        {
            var voiceState = Bot.GetGuild(period.GuildId).GetVoiceState(period.UserId);
            
            // if the period is older than 30 minutes just disregard it
            if (voiceState is null || DateTime.Now - period.StartedAt > TimeSpan.FromMinutes(30))
                return (false, voiceState);

            return (voiceState.ChannelId == period.ChannelId && voiceState.IsSelfDeafened == period.WasDeafened &&
                    (voiceState.IsSelfMuted && !voiceState.IsSelfDeafened) == period.WasMuted && voiceState.IsStreaming == period.WasStreaming, voiceState);
        }

        protected override async ValueTask OnVoiceStateUpdated(VoiceStateUpdatedEventArgs e)
        {
            if (e.Member.IsBot)
                return;

            await EventSemaphore.WaitAsync();
            
            if (e.OldVoiceState?.ChannelId is null && e.NewVoiceState.ChannelId is not null)
                await OnMemberConnectedAsync(e);
            else if (e.OldVoiceState?.ChannelId is not null && e.NewVoiceState.ChannelId is null)
                await OnMemberDisconnectedAsync(e);
            else if (e.OldVoiceState?.ChannelId != e.NewVoiceState.ChannelId)
            {
                await OnMemberDisconnectedAsync(e);
                await OnMemberConnectedAsync(e);
            }
            else
            {
                await OnMemberVoiceStateChanged(e);
            }

            EventSemaphore.Release();
        }

        private async Task EndCurrentVoicePeriodAsync(Snowflake userId, NoLifeBotDbContext dbContext)
        {
            // safeguard against any lurking double voice period bugs
            var activeVoicePeriods = await dbContext.VoicePeriods.Where(x => x.UserId == userId && x.EndedAt == null).ToListAsync();
            
            switch (activeVoicePeriods.Count)
            {
                case 0:
                    Logger.LogWarning($"Missing active voice period for user {userId}");
                    break;
                case > 1:
                {
                    Logger.LogError($"Multiple voice periods are active for user: {userId}! Removing!");

                    foreach (var activeVoicePeriod in activeVoicePeriods)
                    {
                        activeVoicePeriod.EndedAt = DateTime.Now;
                        dbContext.Update(activeVoicePeriod);
                    }

                    break;
                }
                default:
                {
                    var currentVoicePeriod = activeVoicePeriods.First();
                    currentVoicePeriod.EndedAt = DateTime.Now;
                    dbContext.Update(currentVoicePeriod);
                    break;
                }
            }
        }

        private async ValueTask OnMemberConnectedAsync(VoiceStateUpdatedEventArgs e)
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<NoLifeBotDbContext>();

            var voicePeriod = new VoicePeriod(e.NewVoiceState);

            dbContext.Add(voicePeriod);
            await dbContext.SaveChangesAsync();
        }

        private async ValueTask OnMemberDisconnectedAsync(VoiceStateUpdatedEventArgs e)
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<NoLifeBotDbContext>();

            await EndCurrentVoicePeriodAsync(e.MemberId, dbContext);
            
            await dbContext.SaveChangesAsync();
        }

        private async Task OnMemberVoiceStateChanged(VoiceStateUpdatedEventArgs e)
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<NoLifeBotDbContext>();

            await EndCurrentVoicePeriodAsync(e.MemberId, dbContext);

            var newVoicePeriod = new VoicePeriod(e.NewVoiceState);
            dbContext.Add(newVoicePeriod);
            await dbContext.SaveChangesAsync();
        }
    }
}