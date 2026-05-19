namespace Shortly.Exceptions;

public class ShortlyException : Exception
{
    public ShortlyException(string message) : base(message)
    {
    }

    public ShortlyException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
