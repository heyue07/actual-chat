namespace ActualChat.UI.Blazor;

public interface IBlazorUIModule
{
    /// <summary>
    /// The lowercase name of the module in a webpack bundle <br/>
    /// Must be in sync with <c>src/nodejs/index.ts</c> import ); <br/>
    /// </summary>
    static abstract string ImportName { get; }
}
