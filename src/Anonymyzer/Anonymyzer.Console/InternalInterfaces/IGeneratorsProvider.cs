namespace Anonymyzer.Console.InternalInterfaces;

using Anonymyzer.Base.Generation;

internal interface IGeneratorsProvider
{
    IEnumerable<IGenerator> GetAllGenerators();
}