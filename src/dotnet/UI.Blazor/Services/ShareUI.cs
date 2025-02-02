﻿namespace ActualChat.UI.Blazor.Services;

public sealed class ShareUI(IServiceProvider services) : IHasServices
{
    private ModalUI? _modalUI;

    public IServiceProvider Services { get; } = services;
    public ModalUI ModalUI => _modalUI ??= Services.GetRequiredService<ModalUI>();

    public Task<ModalRef> Share(ShareModalModel model)
        => ModalUI.Show(model);
    public Task<ModalRef> Share(ShareKind kind, string title, string targetTitle, ShareRequest request)
        => ModalUI.Show(new ShareModalModel(kind, title, targetTitle, request));
}
