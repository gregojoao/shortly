namespace Shortly.Exceptions;

public sealed class ShortlyValidationException : ShortlyException
{
    public ShortlyValidationException(string message) : base(message)
    {
    }
}
