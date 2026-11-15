namespace WebApplication1.Models;

public sealed record FashionMagazineArticle(
    string Title,
    string Summary,
    string Source,
    Uri Url,
    Uri? ImageUrl,
    DateTimeOffset PublishedAt);

public sealed record FashionMagazineViewModel(
    IReadOnlyList<FashionMagazineArticle> Articles,
    DateTimeOffset? LastUpdatedAt,
    bool IsAvailable);
