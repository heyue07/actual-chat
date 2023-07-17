﻿using ActualChat.UI.Blazor.Components;

namespace ActualChat.App.Maui.Services;

public class MauiShare : IMauiShare
{
    public Task ShareLink(string title, string link)
        => Share.Default.RequestAsync(new ShareTextRequest {
            Uri = link,
            Title = title,
        });

    public Task ShareText(string title, string text)
        => Share.Default.RequestAsync(new ShareTextRequest {
            Text = text,
            Title = title,
        });
}
