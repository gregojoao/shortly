namespace Shortly.Application.Ports;

public interface ISlugGenerator
{
    string Generate(int length);
}
