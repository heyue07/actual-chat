using ActualChat.App.Server;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.Extensions.Configuration;

namespace ActualChat.Testing.Host;

public static class AppHostExt
{
    public static WebClientTester NewWebClientTester(this AppHost appHost)
        => new(appHost);

    public static PlaywrightTester NewPlaywrightTester(this AppHost appHost)
        => new(appHost);

    public static BlazorTester NewBlazorTester(this AppHost appHost)
        => new(appHost);

    public static HttpClient NewHttpClient(this AppHost appHost)
    {
        var urlMapper = appHost.Services.UrlMapper();
        return new() { BaseAddress = urlMapper.BaseUri };
    }

    public static IServer Server(this IServiceProvider services)
        => services.GetRequiredService<IServer>();

    public static IConfigurationBuilder AddInMemory(this IConfigurationBuilder builder, params (string Key, string? Value)[] values)
        => builder.AddInMemoryCollection(values.ToDictionary(x => x.Key, x => x.Value));
}
