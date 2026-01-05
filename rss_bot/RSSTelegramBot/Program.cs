using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

using RSSTelegramBot;

var botClient = new TelegramBotClient(BotConfig.BotToken);
var apiClient = new ApiClient();

using var cts = new CancellationTokenSource();

var me = await botClient.GetMe(cts.Token);
Console.WriteLine($"Telegram bot started: @{me.Username} ({me.Id})");

var receiverOptions = new ReceiverOptions
{
    AllowedUpdates = new[] { UpdateType.Message }
};

botClient.StartReceiving(
    updateHandler: HandleUpdateAsync,
    errorHandler: HandleErrorAsync,
    receiverOptions: receiverOptions,
    cancellationToken: cts.Token
);

Console.WriteLine("Press Enter to stop...");
Console.ReadLine();
cts.Cancel();

static bool IsCommand(string text, string cmd)
{
    return text.StartsWith(cmd, StringComparison.OrdinalIgnoreCase) ||
           text.StartsWith(cmd + "@", StringComparison.OrdinalIgnoreCase);
}

static string GetCommandArg(string text, string cmd)
{
    if (string.IsNullOrWhiteSpace(text)) return "";

    var trimmed = text.Trim();
    var firstSpace = trimmed.IndexOf(' ');
    var firstToken = firstSpace < 0 ? trimmed : trimmed[..firstSpace];

    if (!firstToken.StartsWith(cmd, StringComparison.OrdinalIgnoreCase))
        return "";

    if (firstSpace < 0) return "";
    return trimmed[(firstSpace + 1)..].Trim();
}

async Task HandleUpdateAsync(ITelegramBotClient bot, Update update, CancellationToken ct)
{
    if (update.Message?.Type != MessageType.Text)
        return;

    var message = update.Message!;
    var text = (message.Text ?? "").Trim();
    var chatId = message.Chat.Id;

    if (string.IsNullOrWhiteSpace(text))
        return;

    try
    {
        if (IsCommand(text, "/start"))
        {
            await bot.SendMessage(
                chatId: chatId,
                text:
                    "👋 RSS Reader Bot\n\n" +
                    "Команды:\n" +
                    "/channels — список каналов\n" +
                    "/articles — последние статьи\n" +
                    "/search <текст> — поиск по описанию",
                cancellationToken: ct
            );
            return;
        }

        if (IsCommand(text, "/channels"))
        {
            var channels = await apiClient.GetChannelsAsync(ct);

            if (channels.Count == 0)
            {
                await bot.SendMessage(chatId, "Каналов пока нет.", cancellationToken: ct);
                return;
            }

            var msg =
                "📡 Каналы:\n\n" +
                string.Join("\n", channels.Select(c => $"• {c.Name}"));

            await bot.SendMessage(chatId, msg, cancellationToken: ct);
            return;
        }

        if (IsCommand(text, "/articles"))
        {
            var articles = await apiClient.GetArticlesAsync(ct);

            var latest = articles
                .OrderByDescending(a => a.PublishedAt)
                .ToList();

            if (latest.Count == 0)
            {
                await bot.SendMessage(chatId, "Статей пока нет.", cancellationToken: ct);
                return;
            }

            var msg =
                "📰 Последние статьи:\n\n" +
                string.Join("\n\n", latest.Select(a =>
                    $"🗓 {a.PublishedAt:dd.MM.yyyy}\n" +
                    $"📌 {a.RssChannel.Name}\n" +
                    $"📰 {a.Title}\n" +
                    $"🔗 {a.Url}"
                ));

            await bot.SendMessage(chatId, msg, cancellationToken: ct);
            return;
        }

        if (IsCommand(text, "/search"))
        {
            var query = GetCommandArg(text, "/search").Trim();

            if (string.IsNullOrWhiteSpace(query))
            {
                await bot.SendMessage(chatId, "Использование: /search <текст>", cancellationToken: ct);
                return;
            }

            var results = await apiClient.SearchArticlesAsync(query, ct);

            results = results
                .OrderByDescending(a => a.PublishedAt)
                .Take(10)
                .ToList();

            if (results.Count == 0)
            {
                await bot.SendMessage(chatId, "❌ Ничего не найдено.", cancellationToken: ct);
                return;
            }

            var msg =
                "🔍 Результаты поиска:\n\n" +
                string.Join("\n\n", results.Select(a =>
                    $"🗓 {a.PublishedAt:dd.MM.yyyy}\n" +
                    (a.RssChannel?.Name != null ? $"📌 {a.RssChannel.Name}\n" : "") +
                    $"📰 {a.Title}\n" +
                    $"🔗 {a.Url}"
                ));

            await bot.SendMessage(chatId, msg, cancellationToken: ct);
            return;
        }

        await bot.SendMessage(chatId, "Не понял команду. Напишите /start.", cancellationToken: ct);
    }
    catch (HttpRequestException ex)
    {
        Console.WriteLine($"HTTP error: {ex}");
        await bot.SendMessage(chatId, "⚠️ Ошибка связи с сервером API.", cancellationToken: ct);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Unhandled error: {ex}");
        await bot.SendMessage(chatId, "⚠️ Внутренняя ошибка бота.", cancellationToken: ct);
    }
}

Task HandleErrorAsync(ITelegramBotClient bot, Exception exception, CancellationToken ct)
{
    Console.WriteLine(exception.ToString());
    return Task.CompletedTask;
}
