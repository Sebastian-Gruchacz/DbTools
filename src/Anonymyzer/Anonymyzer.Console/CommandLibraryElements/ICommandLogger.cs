namespace Anonymyzer.Console.CommandLibraryElements;

public interface ICommandLogger
{
    void Info(string message);

    void Error(string message);

    void Warning(string message);
}
