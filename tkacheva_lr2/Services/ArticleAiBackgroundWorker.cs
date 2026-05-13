using Microsoft.EntityFrameworkCore;
using tkacheva_lr2.Data;

namespace tkacheva_lr2.Services
{
    public class ArticleAiBackgroundWorker : BackgroundService
    {
        private readonly IArticleAiQueue _queue;
        private readonly IServiceScopeFactory _scopeFactory;

        public ArticleAiBackgroundWorker(
            IArticleAiQueue queue,
            IServiceScopeFactory scopeFactory)
        {
            _queue = queue;
            _scopeFactory = scopeFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            Console.WriteLine("[AI Background] Worker started.");
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var articleId = await _queue.DequeueAsync(stoppingToken);

                    using var scope = _scopeFactory.CreateScope();

                    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    var aiService = scope.ServiceProvider.GetRequiredService<AIService>();
                    var keywordService = scope.ServiceProvider.GetRequiredService<ArticleKeywordService>();

                    var article = await context.Articles
                        .AsNoTracking()
                        .FirstOrDefaultAsync(a => a.Id == articleId, stoppingToken);

                    if (article == null)
                        continue;

                    Console.WriteLine($"[AI Background] Анализ статьи #{article.Id}: {article.Title}");

                    var aiKeywords = await aiService.ExtractKeywordsAccurateAsync(article.Url);

                    await keywordService.ReplaceAiKeywordsAsync(article.Id, aiKeywords);

                    Console.WriteLine($"[AI Background] Ключевые слова добавлены для статьи #{article.Id}");
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[AI Background] Ошибка фоновой обработки статьи:");
                    Console.WriteLine(ex);
                }
            }
        }
    }
}