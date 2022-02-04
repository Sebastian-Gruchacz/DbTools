namespace Anonymyzer.Console.CommandLibraryElements;

public interface ICommandLogger
{
    void Error(string message);

    void Warning(string message);
}