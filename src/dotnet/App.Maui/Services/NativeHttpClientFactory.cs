using System.Net;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Options;

namespace ActualChat.App.Maui.Services;

public partial class NativeHttpClientFactory : IHttpClientFactory, IHttpMessageHandlerFactory
{
    private static readonly Tracer Tracer = Tracer.Default[nameof(NativeHttpClientFactory)];
    private readonly ConcurrentDictionary<string, HttpMessageHandler> _messageHandlers = new (StringComparer.Ordinal);

    private IServiceProvider Services { get; }
    private IOptionsSnapshot<HttpClientFactoryOptions> Options { get; }
    private List<IHttpMessageHandlerBuilderFilter> Filters { get; }

    public NativeHttpClientFactory(IServiceProvider services)
    {
        Services = services;
        Options = services.GetRequiredService<IOptionsSnapshot<HttpClientFactoryOptions>>();
        Filters = services.GetRequiredService<IEnumerable<IHttpMessageHandlerBuilderFilter>>().ToList();
    }

    public HttpClient CreateClient(string name)
        // Each call to CreateClient(String) is guaranteed to return a new HttpClient instance.
        // https://learn.microsoft.com/en-us/dotnet/api/system.net.http.ihttpclientfactory.createclient?view=dotnet-plat-ext-6.0#remarks
        => ConfigureClient(new HttpClient(CreateHandler(name), false) {
                DefaultRequestVersion = HttpVersion.Version30,
                DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower,
            },
            name);

    public HttpMessageHandler CreateHandler(string name)
        => _messageHandlers.GetOrAdd(name,
            static (name1, this1) => {
                using var _ = Tracer.Region($"{nameof(CreateHandler)}: '{name1}'");

                var handler = this1.CreatePlatformMessageHandler();
                if (handler == null)
                    throw StandardError.NotSupported<NativeHttpClientFactory>(
                        $"{nameof(CreatePlatformMessageHandler)} should not return null on all supported platforms except Windows.");

                return this1.ConfigureMessageHandler(handler, name1);
            }, this);

    private partial HttpMessageHandler? CreatePlatformMessageHandler();

    private HttpClient ConfigureClient(HttpClient client, string name)
    {
        var options = Options.Get(name);
        foreach (var httpClientAction in options.HttpClientActions)
            httpClientAction(client);

        return client;
    }

    private HttpMessageHandler ConfigureMessageHandler(HttpMessageHandler handler, string name)
    {
        var options = Options.Get(name);
        var builder = Services.GetRequiredService<HttpMessageHandlerBuilder>();
        builder.PrimaryHandler = handler;
        builder.Name = name;

        // This is similar to the initialization pattern in:
        // https://github.com/aspnet/Hosting/blob/e892ed8bbdcd25a0dafc1850033398dc57f65fe1/src/Microsoft.AspNetCore.Hosting/Internal/WebHost.cs#L188
        Action<HttpMessageHandlerBuilder> configure = Configure;
        for (int i = Filters.Count - 1; i >= 0; i--)
            configure = Filters[i].Configure(configure);

        configure(builder);

        void Configure(HttpMessageHandlerBuilder b)
        {
            for (int i = 0; i < options?.HttpMessageHandlerBuilderActions.Count; i++)
                options.HttpMessageHandlerBuilderActions[i](b);
        }

        return builder.Build();
    }
}
