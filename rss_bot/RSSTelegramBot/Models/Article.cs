public class Article
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string Url { get; set; } = "";
    public DateTime PublishedAt { get; set; }

    public RssChannel? RssChannel { get; set; }
}
