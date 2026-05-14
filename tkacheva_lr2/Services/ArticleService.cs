using Microsoft.EntityFrameworkCore;
using tkacheva_lr2.Data;
using tkacheva_lr2.Models;

namespace tkacheva_lr2.Services
{
    public class ArticleService
    {
        private readonly ApplicationDbContext _context;

        public ArticleService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<Article>> GetAllArticlesAsync(
            int page,
            int pageSize,
            string? search,
            List<ChannelCategoryFilterDto>? channelCategoryFilters,
            string? sortOrder,
            string? periodFilter)
        {
            page = NormalizePage(page);
            pageSize = NormalizePageSize(pageSize);

            var query = _context.Articles
                .Include(a => a.RSSChannel)
                .Include(a => a.Keywords)
                .Include(a => a.Categories)
                .AsQueryable();

            query = ApplyKeywordSearch(query, search);
            query = ApplyArticlePeriodFilter(query, periodFilter);
            query = await ApplyArticleChannelCategoryTreeFilterAsync(query, channelCategoryFilters);
            query = ApplyArticleSorting(query, sortOrder);

            return await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<List<Article>> GetAllArticlesAsync(
            int page,
            int pageSize,
            string? search,
            List<int>? channelIds,
            string? sortOrder,
            string? periodFilter)
        {
            var filters = ConvertChannelIdsToTreeFilters(channelIds);

            return await GetAllArticlesAsync(
                page,
                pageSize,
                search,
                filters,
                sortOrder,
                periodFilter
            );
        }

        public async Task<List<Article>> GetAllArticlesAsync(
            int page,
            int pageSize,
            string? search,
            List<int>? channelIds,
            List<string>? categoryNames,
            string? sortOrder,
            string? periodFilter)
        {
            page = NormalizePage(page);
            pageSize = NormalizePageSize(pageSize);

            var query = _context.Articles
                .Include(a => a.RSSChannel)
                .Include(a => a.Keywords)
                .Include(a => a.Categories)
                .AsQueryable();

            query = ApplyKeywordSearch(query, search);
            query = ApplyChannelFilter(query, channelIds);
            query = ApplyArticleCategoryFilter(query, categoryNames);
            query = ApplyArticlePeriodFilter(query, periodFilter);
            query = ApplyArticleSorting(query, sortOrder);

            return await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<Article?> GetArticleByTitleAsync(string title)
        {
            return await _context.Articles
                .Include(a => a.RSSChannel)
                .Include(a => a.Keywords)
                .Include(a => a.Categories)
                .AsNoTracking()
                .FirstOrDefaultAsync(a => EF.Functions.Like(a.Title, title));
        }

        public async Task<Article> CreateArticleAsync(Article article, string channelName)
        {
            var channel = await _context.RSSChannels
                .FirstOrDefaultAsync(r => r.Name.ToLower() == channelName.ToLower());

            if (channel == null)
                throw new ArgumentException("Channel not found");

            if (!article.IsValidUrl())
                throw new ArgumentException("Invalid URL");

            article.RSSChannelId = channel.Id;
            article.RSSChannel = channel;

            _context.Articles.Add(article);
            await _context.SaveChangesAsync();

            return article;
        }

        public async Task<Article?> UpdateArticleAsync(
            string title,
            string? newTitle,
            string? url,
            string? channelName,
            DateTime? publishedAt,
            string? description)
        {
            var article = await _context.Articles
                .Include(a => a.RSSChannel)
                .Include(a => a.Keywords)
                .Include(a => a.Categories)
                .FirstOrDefaultAsync(a => EF.Functions.Like(a.Title, title));

            if (article == null)
                return null;

            if (!string.IsNullOrWhiteSpace(channelName))
            {
                var channel = await _context.RSSChannels
                    .FirstOrDefaultAsync(c => c.Name.ToLower() == channelName.ToLower());

                if (channel == null)
                    throw new ArgumentException("Channel not found");

                article.RSSChannelId = channel.Id;
                article.RSSChannel = channel;
            }

            if (!string.IsNullOrWhiteSpace(newTitle))
                article.Title = newTitle;

            if (!string.IsNullOrWhiteSpace(url))
                article.Url = url;

            if (publishedAt != null)
                article.PublishedAt = publishedAt.Value;

            if (description != null)
                article.Description = description;

            await _context.SaveChangesAsync();

            return article;
        }

        public async Task<bool> DeleteArticleAsync(string title)
        {
            var article = await _context.Articles
                .FirstOrDefaultAsync(a => EF.Functions.Like(a.Title, title));

            if (article == null)
                return false;

            _context.Articles.Remove(article);
            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<List<Article>> SearchByDescriptionAsync(string text)
        {
            text = (text ?? "").Trim();

            if (string.IsNullOrWhiteSpace(text))
                return new List<Article>();

            return await _context.Articles
                .Include(a => a.RSSChannel)
                .Include(a => a.Keywords)
                .Include(a => a.Categories)
                .Where(a =>
                    a.Description != null &&
                    EF.Functions.Like(a.Description, $"%{text}%"))
                .OrderByDescending(a => a.PublishedAt)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<List<Article>> GetUnreadArticlesForUserAsync(string username)
        {
            var user = await _context.AppUsers
                .Include(u => u.SubscribedChannels)
                .FirstOrDefaultAsync(u => u.UserName.ToLower() == username.ToLower());

            if (user == null)
                return new List<Article>();

            var subscribedChannelIds = user.SubscribedChannels
                .Select(c => c.Id)
                .ToList();

            var states = await _context.UserArticleStates
                .Where(s =>
                    s.AppUserId == user.Id &&
                    !s.IsRead &&
                    s.Article != null &&
                    subscribedChannelIds.Contains(s.Article.RSSChannelId))
                .Include(s => s.Article)
                    .ThenInclude(a => a!.RSSChannel)
                .Include(s => s.Article)
                    .ThenInclude(a => a!.Keywords)
                .Include(s => s.Article)
                    .ThenInclude(a => a!.Categories)
                .OrderByDescending(s => s.Article!.PublishedAt)
                .AsNoTracking()
                .ToListAsync();

            return states
                .Where(s => s.Article != null)
                .Select(ToArticleWithState)
                .ToList();
        }

        public async Task<List<Article>> GetArticlesForUserAsync(
            int userId,
            bool isRead,
            int page,
            int pageSize,
            string? search,
            List<ChannelCategoryFilterDto>? channelCategoryFilters,
            string? sortOrder,
            string? periodFilter)
        {
            page = NormalizePage(page);
            pageSize = NormalizePageSize(pageSize);

            var user = await _context.AppUsers
                .Include(u => u.SubscribedChannels)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
                return new List<Article>();

            var subscribedChannelIds = user.SubscribedChannels
                .Select(c => c.Id)
                .ToList();

            var query = _context.UserArticleStates
                .Where(s =>
                    s.AppUserId == userId &&
                    s.IsRead == isRead &&
                    s.Article != null &&
                    subscribedChannelIds.Contains(s.Article.RSSChannelId))
                .Include(s => s.Article)
                    .ThenInclude(a => a!.RSSChannel)
                .Include(s => s.Article)
                    .ThenInclude(a => a!.Keywords)
                .Include(s => s.Article)
                    .ThenInclude(a => a!.Categories)
                .AsQueryable();

            query = ApplyUserStateKeywordSearch(query, search);
            query = ApplyUserStatePeriodFilter(query, periodFilter);
            query = await ApplyUserStateChannelCategoryTreeFilterAsync(query, channelCategoryFilters);
            query = ApplyUserStateSorting(query, sortOrder);

            var states = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .AsNoTracking()
                .ToListAsync();

            return states
                .Where(s => s.Article != null)
                .Select(ToArticleWithState)
                .ToList();
        }

        public async Task<List<Article>> GetArticlesForUserAsync(
            int userId,
            bool isRead,
            int page,
            int pageSize,
            string? search,
            List<int>? channelIds,
            string? sortOrder,
            string? periodFilter)
        {
            var filters = ConvertChannelIdsToTreeFilters(channelIds);

            return await GetArticlesForUserAsync(
                userId,
                isRead,
                page,
                pageSize,
                search,
                filters,
                sortOrder,
                periodFilter
            );
        }

        public async Task<List<Article>> GetArticlesForUserAsync(
            int userId,
            bool isRead,
            int page,
            int pageSize,
            string? search,
            List<int>? channelIds,
            List<string>? categoryNames,
            string? sortOrder,
            string? periodFilter)
        {
            page = NormalizePage(page);
            pageSize = NormalizePageSize(pageSize);

            var user = await _context.AppUsers
                .Include(u => u.SubscribedChannels)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
                return new List<Article>();

            var subscribedChannelIds = user.SubscribedChannels
                .Select(c => c.Id)
                .ToList();

            var query = _context.UserArticleStates
                .Where(s =>
                    s.AppUserId == userId &&
                    s.IsRead == isRead &&
                    s.Article != null &&
                    subscribedChannelIds.Contains(s.Article.RSSChannelId))
                .Include(s => s.Article)
                    .ThenInclude(a => a!.RSSChannel)
                .Include(s => s.Article)
                    .ThenInclude(a => a!.Keywords)
                .Include(s => s.Article)
                    .ThenInclude(a => a!.Categories)
                .AsQueryable();

            query = ApplyUserStateKeywordSearch(query, search);
            query = ApplyUserStateChannelFilter(query, channelIds);
            query = ApplyUserStateCategoryFilter(query, categoryNames);
            query = ApplyUserStatePeriodFilter(query, periodFilter);
            query = ApplyUserStateSorting(query, sortOrder);

            var states = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .AsNoTracking()
                .ToListAsync();

            return states
                .Where(s => s.Article != null)
                .Select(ToArticleWithState)
                .ToList();
        }

        public async Task<bool> MarkAsReadAsync(int userId, int articleId)
        {
            var state = await _context.UserArticleStates
                .FirstOrDefaultAsync(s =>
                    s.AppUserId == userId &&
                    s.ArticleId == articleId);

            if (state == null)
                return false;

            if (!state.IsRead)
            {
                state.IsRead = true;
                state.ReadAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }

            return true;
        }

        public async Task<bool> SetFavoriteAsync(int userId, int articleId, bool isFavorite)
        {
            var userExists = await _context.AppUsers
                .AnyAsync(u => u.Id == userId);

            if (!userExists)
                return false;

            var articleExists = await _context.Articles
                .AnyAsync(a => a.Id == articleId);

            if (!articleExists)
                return false;

            var state = await _context.UserArticleStates
                .FirstOrDefaultAsync(s =>
                    s.AppUserId == userId &&
                    s.ArticleId == articleId);

            if (state == null)
            {
                if (!isFavorite)
                    return true;

                state = new UserArticleState
                {
                    AppUserId = userId,
                    ArticleId = articleId,
                    IsRead = false,
                    IsFavorite = true,
                    AddedAt = DateTime.UtcNow
                };

                _context.UserArticleStates.Add(state);
            }
            else
            {
                state.IsFavorite = isFavorite;
            }

            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<List<Article>> GetFavoriteArticlesForUserAsync(string username)
        {
            var user = await _context.AppUsers
                .FirstOrDefaultAsync(u => u.UserName.ToLower() == username.ToLower());

            if (user == null)
                return new List<Article>();

            return await GetFavoriteArticlesForUserAsync(user.Id);
        }

        public async Task<List<Article>> GetFavoriteArticlesForUserAsync(int userId)
        {
            var userExists = await _context.AppUsers
                .AnyAsync(u => u.Id == userId);

            if (!userExists)
                return new List<Article>();

            var states = await _context.UserArticleStates
                .Where(s => s.AppUserId == userId && s.IsFavorite)
                .Include(s => s.Article)
                    .ThenInclude(a => a!.RSSChannel)
                .Include(s => s.Article)
                    .ThenInclude(a => a!.Keywords)
                .Include(s => s.Article)
                    .ThenInclude(a => a!.Categories)
                .OrderByDescending(s => s.Article!.PublishedAt)
                .AsNoTracking()
                .ToListAsync();

            return states
                .Where(s => s.Article != null)
                .Select(ToArticleWithState)
                .ToList();
        }

        public async Task<List<ArticleCategory>> GetAllCategoriesAsync()
        {
            var categories = await _context.ArticleCategories
                .AsNoTracking()
                .ToListAsync();

            return categories
                .Where(c => !string.IsNullOrWhiteSpace(c.NormalizedName))
                .GroupBy(c => c.NormalizedName)
                .Select(g => g.OrderBy(c => c.Name).First())
                .OrderBy(c => c.Name)
                .ToList();
        }

        public async Task<List<ArticleCategory>> GetCategoriesForUserAsync(int userId)
        {
            var user = await _context.AppUsers
                .Include(u => u.SubscribedChannels)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
                return new List<ArticleCategory>();

            var subscribedChannelIds = user.SubscribedChannels
                .Select(c => c.Id)
                .ToList();

            if (subscribedChannelIds.Count == 0)
                return new List<ArticleCategory>();

            var categories = await _context.ArticleCategories
                .Where(c =>
                    c.Article != null &&
                    subscribedChannelIds.Contains(c.Article.RSSChannelId))
                .AsNoTracking()
                .ToListAsync();

            return categories
                .Where(c => !string.IsNullOrWhiteSpace(c.NormalizedName))
                .GroupBy(c => c.NormalizedName)
                .Select(g => g.OrderBy(c => c.Name).First())
                .OrderBy(c => c.Name)
                .ToList();
        }

        public async Task<List<ChannelCategoryGroupDto>> GetChannelCategoryTreeAsync(
            int? userId,
            bool includeAllChannels)
        {
            List<RSSChannel> channels;

            if (includeAllChannels)
            {
                channels = await _context.RSSChannels
                    .OrderBy(c => c.Name)
                    .AsNoTracking()
                    .ToListAsync();
            }
            else
            {
                if (userId == null)
                    return new List<ChannelCategoryGroupDto>();

                var user = await _context.AppUsers
                    .Include(u => u.SubscribedChannels)
                    .FirstOrDefaultAsync(u => u.Id == userId.Value);

                if (user == null)
                    return new List<ChannelCategoryGroupDto>();

                channels = user.SubscribedChannels
                    .OrderBy(c => c.Name)
                    .ToList();
            }

            var channelIds = channels
                .Select(c => c.Id)
                .ToList();

            var rawCategories = await _context.ArticleCategories
                .Where(c =>
                    c.Article != null &&
                    channelIds.Contains(c.Article.RSSChannelId))
                .Select(c => new
                {
                    ChannelId = c.Article!.RSSChannelId,
                    c.Name,
                    c.NormalizedName
                })
                .AsNoTracking()
                .ToListAsync();

            var result = new List<ChannelCategoryGroupDto>();

            foreach (var channel in channels)
            {
                var categories = rawCategories
                    .Where(c =>
                        c.ChannelId == channel.Id &&
                        !string.IsNullOrWhiteSpace(c.NormalizedName))
                    .GroupBy(c => c.NormalizedName)
                    .Select(g => g
                        .OrderBy(c => c.Name)
                        .First())
                    .OrderBy(c => c.Name)
                    .Select(c => new ChannelCategoryOptionDto
                    {
                        Name = c.Name,
                        NormalizedName = c.NormalizedName
                    })
                    .ToList();

                result.Add(new ChannelCategoryGroupDto
                {
                    ChannelId = channel.Id,
                    ChannelName = channel.Name,
                    Categories = categories
                });
            }

            return result;
        }

        public async Task<List<Article>> GetRecommendationsForUserAsync(int userId)
        {
            var user = await _context.AppUsers
                .Include(u => u.SubscribedChannels)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
                return new List<Article>();

            var subscribedChannelIds = user.SubscribedChannels
                .Select(c => c.Id)
                .ToList();

            var userArticleIds = await _context.UserArticleStates
                .Where(s => s.AppUserId == userId)
                .Select(s => s.ArticleId)
                .ToListAsync();

            var favoriteArticleIds = await _context.UserArticleStates
                .Where(s => s.AppUserId == userId && s.IsFavorite)
                .Select(s => s.ArticleId)
                .ToListAsync();

            var subscriptionCategorySet = await _context.ArticleCategories
                .Where(c =>
                    c.Article != null &&
                    subscribedChannelIds.Contains(c.Article.RSSChannelId))
                .Select(c => c.NormalizedName)
                .Distinct()
                .ToListAsync();

            var favoriteCategorySet = await _context.ArticleCategories
                .Where(c => favoriteArticleIds.Contains(c.ArticleId))
                .Select(c => c.NormalizedName)
                .Distinct()
                .ToListAsync();

            var subscriptionKeywordSet = await _context.ArticleKeywords
                .Where(k =>
                    k.Article != null &&
                    subscribedChannelIds.Contains(k.Article.RSSChannelId))
                .Select(k => k.NormalizedText)
                .Distinct()
                .ToListAsync();

            var favoriteKeywordSet = await _context.ArticleKeywords
                .Where(k => favoriteArticleIds.Contains(k.ArticleId))
                .Select(k => k.NormalizedText)
                .Distinct()
                .ToListAsync();

            var subscriptionCategories = subscriptionCategorySet.ToHashSet();
            var favoriteCategories = favoriteCategorySet.ToHashSet();

            var subscriptionKeywords = subscriptionKeywordSet.ToHashSet();
            var favoriteKeywords = favoriteKeywordSet.ToHashSet();

            var candidates = await _context.Articles
                .Include(a => a.RSSChannel)
                .Include(a => a.Keywords)
                .Include(a => a.Categories)
                .Where(a =>
                    !subscribedChannelIds.Contains(a.RSSChannelId) &&
                    !userArticleIds.Contains(a.Id))
                .AsNoTracking()
                .ToListAsync();

            var now = DateTime.UtcNow;

            return candidates
                .Select(article =>
                {
                    var similarityScore = 0.0;

                    foreach (var category in article.Categories)
                    {
                        if (favoriteCategories.Contains(category.NormalizedName))
                            similarityScore += 6;

                        if (subscriptionCategories.Contains(category.NormalizedName))
                            similarityScore += 3;
                    }

                    foreach (var keyword in article.Keywords)
                    {
                        if (favoriteKeywords.Contains(keyword.NormalizedText))
                            similarityScore += 4 * keyword.Weight;

                        if (subscriptionKeywords.Contains(keyword.NormalizedText))
                            similarityScore += 1 * keyword.Weight;
                    }

                    if (similarityScore <= 0)
                    {
                        return new
                        {
                            Article = article,
                            Score = 0.0
                        };
                    }

                    var daysOld = Math.Max(0, (now - article.PublishedAt).TotalDays);
                    var recencyBonus = Math.Max(0, 2 - daysOld / 30.0);

                    return new
                    {
                        Article = article,
                        Score = similarityScore + recencyBonus
                    };
                })
                .Where(x => x.Score > 0)
                .OrderByDescending(x => x.Score)
                .ThenByDescending(x => x.Article.PublishedAt)
                .Select(x => x.Article)
                .ToList();
        }

        private static async Task<IQueryable<Article>> ApplyArticleChannelCategoryTreeFilterAsync(
            IQueryable<Article> query,
            List<ChannelCategoryFilterDto>? filters)
        {
            filters = NormalizeChannelCategoryFilters(filters);

            if (filters.Count == 0)
                return query;

            var allowedArticleIds = new List<int>();

            foreach (var filter in filters)
            {
                var partQuery = query.Where(a => a.RSSChannelId == filter.ChannelId);

                if (filter.CategoryNames.Count > 0)
                {
                    partQuery = partQuery.Where(a =>
                        a.Categories.Any(c =>
                            filter.CategoryNames.Contains(c.NormalizedName)
                        )
                    );
                }

                var ids = await partQuery
                    .Select(a => a.Id)
                    .ToListAsync();

                allowedArticleIds.AddRange(ids);
            }

            allowedArticleIds = allowedArticleIds
                .Distinct()
                .ToList();

            if (allowedArticleIds.Count == 0)
                return query.Where(a => false);

            return query.Where(a => allowedArticleIds.Contains(a.Id));
        }

        private static async Task<IQueryable<UserArticleState>> ApplyUserStateChannelCategoryTreeFilterAsync(
            IQueryable<UserArticleState> query,
            List<ChannelCategoryFilterDto>? filters)
        {
            filters = NormalizeChannelCategoryFilters(filters);

            if (filters.Count == 0)
                return query;

            var allowedStateIds = new List<int>();

            foreach (var filter in filters)
            {
                var partQuery = query.Where(s =>
                    s.Article != null &&
                    s.Article.RSSChannelId == filter.ChannelId
                );

                if (filter.CategoryNames.Count > 0)
                {
                    partQuery = partQuery.Where(s =>
                        s.Article != null &&
                        s.Article.Categories.Any(c =>
                            filter.CategoryNames.Contains(c.NormalizedName)
                        )
                    );
                }

                var ids = await partQuery
                    .Select(s => s.Id)
                    .ToListAsync();

                allowedStateIds.AddRange(ids);
            }

            allowedStateIds = allowedStateIds
                .Distinct()
                .ToList();

            if (allowedStateIds.Count == 0)
                return query.Where(s => false);

            return query.Where(s => allowedStateIds.Contains(s.Id));
        }

        private static Article ToArticleWithState(UserArticleState state)
        {
            var article = state.Article!;

            article.IsRead = state.IsRead;
            article.IsFavorite = state.IsFavorite;

            return article;
        }

        private static int NormalizePage(int page)
        {
            return page < 1 ? 1 : page;
        }

        private static int NormalizePageSize(int pageSize)
        {
            if (pageSize < 1)
                return 10;

            if (pageSize > 100)
                return 100;

            return pageSize;
        }

        private static IQueryable<Article> ApplyKeywordSearch(
            IQueryable<Article> query,
            string? search)
        {
            if (string.IsNullOrWhiteSpace(search))
                return query;

            var normalizedSearch = ArticleKeywordService.NormalizeForSearch(search);

            if (string.IsNullOrWhiteSpace(normalizedSearch))
                return query;

            return query.Where(a =>
                a.Keywords.Any(k =>
                    k.NormalizedText.Contains(normalizedSearch)
                )
            );
        }

        private static IQueryable<UserArticleState> ApplyUserStateKeywordSearch(
            IQueryable<UserArticleState> query,
            string? search)
        {
            if (string.IsNullOrWhiteSpace(search))
                return query;

            var normalizedSearch = ArticleKeywordService.NormalizeForSearch(search);

            if (string.IsNullOrWhiteSpace(normalizedSearch))
                return query;

            return query.Where(s =>
                s.Article != null &&
                s.Article.Keywords.Any(k =>
                    k.NormalizedText.Contains(normalizedSearch)
                )
            );
        }

        private static IQueryable<Article> ApplyChannelFilter(
            IQueryable<Article> query,
            List<int>? channelIds)
        {
            if (channelIds == null || channelIds.Count == 0)
                return query;

            return query.Where(a => channelIds.Contains(a.RSSChannelId));
        }

        private static IQueryable<UserArticleState> ApplyUserStateChannelFilter(
            IQueryable<UserArticleState> query,
            List<int>? channelIds)
        {
            if (channelIds == null || channelIds.Count == 0)
                return query;

            return query.Where(s =>
                s.Article != null &&
                channelIds.Contains(s.Article.RSSChannelId)
            );
        }

        private static IQueryable<Article> ApplyArticleCategoryFilter(
            IQueryable<Article> query,
            List<string>? categoryNames)
        {
            var normalizedCategories = NormalizeCategoryNames(categoryNames);

            if (normalizedCategories.Count == 0)
                return query;

            return query.Where(a =>
                a.Categories.Any(c =>
                    normalizedCategories.Contains(c.NormalizedName)
                )
            );
        }

        private static IQueryable<UserArticleState> ApplyUserStateCategoryFilter(
            IQueryable<UserArticleState> query,
            List<string>? categoryNames)
        {
            var normalizedCategories = NormalizeCategoryNames(categoryNames);

            if (normalizedCategories.Count == 0)
                return query;

            return query.Where(s =>
                s.Article != null &&
                s.Article.Categories.Any(c =>
                    normalizedCategories.Contains(c.NormalizedName)
                )
            );
        }

        private static IQueryable<Article> ApplyArticlePeriodFilter(
            IQueryable<Article> query,
            string? periodFilter)
        {
            var now = DateTime.UtcNow;

            if (periodFilter == "lastMonth")
            {
                var monthAgo = now.AddMonths(-1);
                return query.Where(a => a.PublishedAt >= monthAgo);
            }

            if (periodFilter == "lastYear")
            {
                var yearAgo = now.AddYears(-1);
                return query.Where(a => a.PublishedAt >= yearAgo);
            }

            if (periodFilter == "previousYear")
            {
                var previousYear = now.Year - 1;
                return query.Where(a => a.PublishedAt.Year == previousYear);
            }

            return query;
        }

        private static IQueryable<UserArticleState> ApplyUserStatePeriodFilter(
            IQueryable<UserArticleState> query,
            string? periodFilter)
        {
            var now = DateTime.UtcNow;

            if (periodFilter == "lastMonth")
            {
                var monthAgo = now.AddMonths(-1);
                return query.Where(s =>
                    s.Article != null &&
                    s.Article.PublishedAt >= monthAgo);
            }

            if (periodFilter == "lastYear")
            {
                var yearAgo = now.AddYears(-1);
                return query.Where(s =>
                    s.Article != null &&
                    s.Article.PublishedAt >= yearAgo);
            }

            if (periodFilter == "previousYear")
            {
                var previousYear = now.Year - 1;
                return query.Where(s =>
                    s.Article != null &&
                    s.Article.PublishedAt.Year == previousYear);
            }

            return query;
        }

        private static IOrderedQueryable<Article> ApplyArticleSorting(
            IQueryable<Article> query,
            string? sortOrder)
        {
            return sortOrder == "oldest"
                ? query.OrderBy(a => a.PublishedAt)
                : query.OrderByDescending(a => a.PublishedAt);
        }

        private static IOrderedQueryable<UserArticleState> ApplyUserStateSorting(
            IQueryable<UserArticleState> query,
            string? sortOrder)
        {
            return sortOrder == "oldest"
                ? query.OrderBy(s => s.Article!.PublishedAt)
                : query.OrderByDescending(s => s.Article!.PublishedAt);
        }

        private static List<ChannelCategoryFilterDto> ConvertChannelIdsToTreeFilters(
            List<int>? channelIds)
        {
            if (channelIds == null || channelIds.Count == 0)
                return new List<ChannelCategoryFilterDto>();

            return channelIds
                .Where(id => id > 0)
                .Distinct()
                .Select(id => new ChannelCategoryFilterDto
                {
                    ChannelId = id,
                    CategoryNames = new List<string>()
                })
                .ToList();
        }

        private static List<ChannelCategoryFilterDto> NormalizeChannelCategoryFilters(
            List<ChannelCategoryFilterDto>? filters)
        {
            if (filters == null || filters.Count == 0)
                return new List<ChannelCategoryFilterDto>();

            return filters
                .Where(f => f.ChannelId > 0)
                .Select(f => new ChannelCategoryFilterDto
                {
                    ChannelId = f.ChannelId,
                    CategoryNames = NormalizeCategoryNames(f.CategoryNames)
                })
                .GroupBy(f => f.ChannelId)
                .Select(g => new ChannelCategoryFilterDto
                {
                    ChannelId = g.Key,
                    CategoryNames = g
                        .SelectMany(x => x.CategoryNames)
                        .Distinct()
                        .ToList()
                })
                .ToList();
        }

        private static List<string> NormalizeCategoryNames(List<string>? categoryNames)
        {
            if (categoryNames == null || categoryNames.Count == 0)
                return new List<string>();

            return categoryNames
                .Select(NormalizeCategoryName)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct()
                .ToList();
        }

        private static string NormalizeCategoryName(string? value)
        {
            return (value ?? "")
                .Trim()
                .ToLowerInvariant();
        }
    }

    public class ChannelCategoryGroupDto
    {
        public int ChannelId { get; set; }

        public string ChannelName { get; set; } = "";

        public List<ChannelCategoryOptionDto> Categories { get; set; } = new();
    }

    public class ChannelCategoryOptionDto
    {
        public string Name { get; set; } = "";

        public string NormalizedName { get; set; } = "";
    }

    public class ChannelCategoryFilterDto
    {
        public int ChannelId { get; set; }

        public List<string> CategoryNames { get; set; } = new();
    }
}