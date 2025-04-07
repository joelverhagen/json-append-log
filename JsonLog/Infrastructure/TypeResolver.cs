using Spectre.Console;
using Spectre.Console.Cli;

namespace JsonLog.Infrastructure;

internal sealed class TypeResolver : ITypeResolver
{
    private readonly IServiceProvider _provider;

    public TypeResolver(IServiceProvider provider)
    {
        _provider = provider;
    }

    public object? Resolve(Type? type)
    {
        if (type == null)
        {
            return null;
        }

        try
        {
            return _provider.GetService(type);
        }
        catch (Exception ex)
        {
            AnsiConsole.WriteException(ex);
            throw new InvalidOperationException($"Failed to resolve type {type.FullName}.", ex);
        }
    }
}