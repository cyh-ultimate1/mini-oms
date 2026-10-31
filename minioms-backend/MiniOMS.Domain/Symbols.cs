namespace MiniOMS.Domain;

public static  class Symbols
{
    public static readonly IReadOnlyList<SymbolInfo> All = [
        new SymbolInfo("ACME", 100.00M, 0.01M), 
        new SymbolInfo("BOLT", 50.00M, 0.01M), 
        new SymbolInfo("CRUX", 25.00M, 0.01M)];
    /// <summary>
    /// use this lookup, else using lambda expression to loop will create new Closure objects every time.
    /// a lambda that captures an outside variable allocates on every call.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, SymbolInfo> BySymbol = All.ToDictionary(s => s.Symbol);
}

public record SymbolInfo(string Symbol, decimal ReferencePrice, decimal TickSize);
