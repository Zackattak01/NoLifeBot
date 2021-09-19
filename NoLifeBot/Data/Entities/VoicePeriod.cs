using System;
using System.ComponentModel.DataAnnotations;
using Disqord;
using Disqord.Gateway;

namespace NoLifeBot.Data.Entities
{
    public class VoicePeriod
    {
        [Key]
        public Guid Id { get; set; }
        public Snowflake UserId { get; set; }
        
        public Snowflake ChannelId { get; set; }
        
        public Snowflake GuildId { get; set; }
        
        public DateTime StartedAt { get; set; }
        
        public DateTime? EndedAt { get; set; }
        
        public bool WasMuted { get; set; }
        
        public bool WasDeafened { get; set; }
        
        public bool WasStreaming { get; set; }

        
        private VoicePeriod(Guid id, Snowflake userId, Snowflake channelId, Snowflake guildId, DateTime startedAt, DateTime? endedAt, bool wasMuted, bool wasDeafened, bool wasStreaming)
        {
            Id = id;
            UserId = userId;
            ChannelId = channelId;
            GuildId = guildId;
            StartedAt = startedAt;
            EndedAt = endedAt;
            WasMuted = wasMuted;
            WasDeafened = wasDeafened;
            WasStreaming = wasStreaming;
        }
        
        public VoicePeriod(Snowflake userId, Snowflake channelId, Snowflake guildId, DateTime startedAt, bool wasMuted = false, bool wasDeafened = false, bool wasStreaming = false)
            : this(Guid.NewGuid(), userId, channelId, guildId, startedAt, null, wasMuted, wasDeafened, wasStreaming)
        { }
        
        public VoicePeriod(IVoiceState voiceState)
            : this(voiceState.MemberId, voiceState.ChannelId!.Value, voiceState.GuildId, DateTime.Now, voiceState.IsSelfMuted && !voiceState.IsSelfDeafened, voiceState.IsSelfDeafened, voiceState.IsStreaming)
        { }

        public override string ToString()
        {
            return $"Id: {Id}\nUser: {UserId}\nGuild: {GuildId}\nChannel: {ChannelId}\nStarted: {StartedAt}\nEnded: {EndedAt}\nMuted: {WasMuted}\nDeafened: {WasDeafened}\nStreaming {WasStreaming}";
        }
    }
}