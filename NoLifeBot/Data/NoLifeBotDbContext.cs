using Disqord;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using NoLifeBot.Data.Entities;
using NoLifeBot.Extensions;

namespace NoLifeBot.Data
{
    public class NoLifeBotDbContext : DbContext
    {
        public DbSet<VoicePeriod> VoicePeriods { get; set; }

        public NoLifeBotDbContext(DbContextOptions<NoLifeBotDbContext> options)
            : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            var snowflakeConverter = new ValueConverter<Snowflake, ulong>(
                static snowflake => snowflake,
                static @ulong => new Snowflake(@ulong));

            modelBuilder.UseValueConverterForType<Snowflake>(snowflakeConverter);
        }
    }
}