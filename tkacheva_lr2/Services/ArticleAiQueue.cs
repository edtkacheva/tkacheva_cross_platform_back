using System.Threading.Channels;

namespace tkacheva_lr2.Services
{
    public interface IArticleAiQueue
    {
        ValueTask EnqueueAsync(int articleId);
        ValueTask<int> DequeueAsync(CancellationToken cancellationToken);
    }

    public class ArticleAiQueue : IArticleAiQueue
    {
        private readonly Channel<int> _queue = Channel.CreateUnbounded<int>();

        public ValueTask EnqueueAsync(int articleId)
        {
            return _queue.Writer.WriteAsync(articleId);
        }

        public ValueTask<int> DequeueAsync(CancellationToken cancellationToken)
        {
            return _queue.Reader.ReadAsync(cancellationToken);
        }
    }
}