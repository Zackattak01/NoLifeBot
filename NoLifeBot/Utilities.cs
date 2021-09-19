using System.Linq;
using Disqord;
using Disqord.Extensions.Interactivity.Menus.Paged;
using NoLifeBot.Extensions;

namespace NoLifeBot
{
    public static class Utilities
    {
        public static ArrayPageFormatter<T> Formatter<T>(string title)
            => (view, items) =>
            {
                var page = ArrayPageProvider<T>.DefaultFormatter(view, items);
                page.Embeds.First().WithDefaultColor().WithTitle(title);
                return page;
            };
    }
}