using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

using RSSTelegramBot;

var botClient = new TelegramBotClient(BotConfig.BotToken);
var apiClient = new ApiClient();

var createFlow = new Dictionary<long, CreateFlowState>();

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

static bool TryValidateUrl(string input, out string normalizedUrl, out string error)
{
    normalizedUrl = "";
    error = "";

    input = (input ?? "").Trim();

    if (string.IsNullOrWhiteSpace(input))
    {
        error = "пустая ссылка";
        return false;
    }

    if (!input.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
        !input.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
    {
        input = "https://" + input;
    }

    if (!Uri.TryCreate(input, UriKind.Absolute, out var uri))
    {
        error = "не удалось распознать URL";
        return false;
    }

    if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
    {
        error = "разрешены только http/https ссылки";
        return false;
    }

    if (string.IsNullOrWhiteSpace(uri.Host))
    {
        error = "у ссылки нет домена (host)";
        return false;
    }

    normalizedUrl = uri.ToString();
    return true;
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

    bool IsCancel()
        => IsCommand(text, "/cancel") ||
           string.Equals(text, "отмена", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(text, "отменить", StringComparison.OrdinalIgnoreCase);

    try
    {
        if (IsCancel())
        {
            createFlow.Remove(chatId);
            await bot.SendMessage(chatId, "❎ Ок, отменено.", cancellationToken: ct);
            return;
        }

        if (createFlow.TryGetValue(chatId, out var flow) && flow.Step != CreateFlowStep.None)
        {
            if (flow.Step == CreateFlowStep.AwaitName)
            {
                var name = text.Trim();

                if (name.Length < 2)
                {
                    await bot.SendMessage(chatId, "Название слишком короткое. Введите название ещё раз или /cancel.", cancellationToken: ct);
                    return;
                }

                createFlow[chatId] = new CreateFlowState(CreateFlowStep.AwaitUrl, name);
                await bot.SendMessage(chatId, "Введите ссылку RSS-канала (пример: https://site.com/feed.xml)\nОтмена: /cancel", cancellationToken: ct);
                return;
            }

            if (flow.Step == CreateFlowStep.AwaitUrl)
            {
                var urlText = text.Trim();

                if (!TryValidateUrl(urlText, out var normalizedUrl, out var error))
                {
                    await bot.SendMessage(chatId, $"❌ Некорректная ссылка: {error}\nВведите ссылку ещё раз или /cancel.", cancellationToken: ct);
                    return;
                }

                var created = await apiClient.CreateChannelAsync(flow.Name!, normalizedUrl, ct);
                createFlow.Remove(chatId);

                await bot.SendMessage(chatId, $"✅ Канал создан:\n• {created.Name}\n• {created.Url}", cancellationToken: ct);
                return;
            }
        }

        if (IsCommand(text, "/start"))
        {
            await bot.SendMessage(
                chatId: chatId,
                text:
                    "👋 RSS Reader Bot\n\n" +
                    "Команды:\n" +
                    "/channels — список каналов\n" +
                    "/articles — последние статьи\n" +
                    "/search <текст> — поиск по описанию\n" +
                    "/create — добавить RSS-канал\n" +
                    "/cancel — отмена",
                cancellationToken: ct
            );
            return;
        }

        if (IsCommand(text, "/create"))
        {
            createFlow[chatId] = new CreateFlowState(CreateFlowStep.AwaitName, null);
            await bot.SendMessage(chatId, "Введите название нового RSS-канала.\nОтмена: /cancel", cancellationToken: ct);
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
                    (a.RssChannel?.Name != null ? $"📌 {a.RssChannel.Name}\n" : "") +
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
        createFlow.Remove(chatId);
        await bot.SendMessage(chatId, "⚠️ Ошибка связи с сервером API.", cancellationToken: ct);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Unhandled error: {ex}");
        createFlow.Remove(chatId);
        await bot.SendMessage(chatId, "⚠️ Внутренняя ошибка бота.", cancellationToken: ct);
    }
}

Task HandleErrorAsync(ITelegramBotClient bot, Exception exception, CancellationToken ct)
{
    Console.WriteLine(exception.ToString());
    return Task.CompletedTask;
}
