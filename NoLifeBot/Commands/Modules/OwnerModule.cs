using System;
using System.Threading.Tasks;
using Disqord.Bot;
using Microsoft.Extensions.Hosting;
using Qmmands;

namespace NoLifeBot.Commands.Modules
{
    [RequireBotOwner]
    public class OwnerModule : DiscordGuildModuleBase
    {
        private readonly IHostApplicationLifetime _lifetime;

        public OwnerModule(IHostApplicationLifetime lifetime)
        {
            _lifetime = lifetime;
        }
        
        [Command("shutdown", "stop", "die", "kill", "exit")]
        public async Task Shutdown()
        {
            await Response("Shutting down");
            _lifetime.StopApplication();
        }

        [Command("restart", "update")]
        public async Task Restart()
        {
            await Response("Restarting");
            Environment.ExitCode = 1;
            _lifetime.StopApplication();
        }
    }
}