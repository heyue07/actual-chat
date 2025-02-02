﻿using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Json;
using ActualChat.Hosting;
using ActualChat.UI.Blazor.Services;

namespace ActualChat.Chat.UI.Blazor.Services;

public class FileUploader(IServiceProvider services)
{
    private HostInfo? _hostInfo;
    private SessionTokens? _sessionTokens;
    private UrlMapper? _urlMapper;
    private IHttpClientFactory? _httpClientFactory;

    private SessionTokens SessionTokens => _sessionTokens ??= services.GetRequiredService<SessionTokens>();
    private HostInfo HostInfo => _hostInfo ??= services.GetRequiredService<HostInfo>();
    private UrlMapper UrlMapper => _urlMapper ??= services.GetRequiredService<UrlMapper>();
    private IHttpClientFactory HttpClientFactory
        => _httpClientFactory ??= services.GetRequiredService<IHttpClientFactory>();

    [RequiresUnreferencedCode("Uses ReadFromJsonAsync")]
    public async Task<MediaContent> Upload(ChatId chatId, Stream file, string? contentType, string? fileName, CancellationToken cancellationToken = default)
    {
        using var formData = new MultipartFormDataContent();
        var streamContent = new StreamContent(file);
        if (contentType != null)
            streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        formData.Add(streamContent, "file", fileName.NullIfEmpty() ?? "Upload");

        var httpClient = HttpClientFactory.CreateClient("UploadFile.Client");
        if (HostInfo.AppKind.IsClient()) {
            var sessionToken = await SessionTokens.Get(cancellationToken).ConfigureAwait(false);
            httpClient.DefaultRequestHeaders.Add(SessionTokens.HeaderName, sessionToken.Token);
        }

        var url = UrlMapper.ApiBaseUrl + "chat-media/"+ chatId + "/upload";
        try {
            var response = await httpClient.PostAsync(url, formData, cancellationToken)
                .ConfigureAwait(false);
            if (response.IsSuccessStatusCode) {
                var result = await response.Content
                    .ReadFromJsonAsync<MediaContent>(cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                return result!;
            }
            var error = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            throw new Exception(error);
        } catch(Exception) when (cancellationToken.IsCancellationRequested) {
            throw new TaskCanceledException();
        }
    }
}
