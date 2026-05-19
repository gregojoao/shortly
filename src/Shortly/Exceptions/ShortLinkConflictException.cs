namespace Shortly.Exceptions;

public sealed class ShortLinkConflictException : ShortlyException
{
    public ShortLinkConflictException(string slug)
        : base($"A short link with slug '{slug}' already exists.")
    {
        Slug = slug;
    }

    public string Slug { get; }
}
