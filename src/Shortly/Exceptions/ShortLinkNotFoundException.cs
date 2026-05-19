namespace Shortly.Exceptions;

public sealed class ShortLinkNotFoundException : ShortlyException
{
    public ShortLinkNotFoundException(string slug)
        : base($"Short link with slug '{slug}' was not found.")
    {
        Slug = slug;
    }

    public string Slug { get; }
}
