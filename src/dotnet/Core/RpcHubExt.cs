using ActualChat.Hosting;
using Stl.Rpc;
using Stl.Rpc.Infrastructure;

namespace ActualChat;

public static class RpcHubExt
{
    public static Task WhenClientPeerConnected(this RpcHub rpcHub, CancellationToken cancellationToken = default)
    {
        var hostInfo = rpcHub.Services.GetRequiredService<HostInfo>();
        if (!hostInfo.AppKind.IsClient())
            return Task.CompletedTask;

        var peer = rpcHub.GetClientPeer(RpcPeerRef.Default);
        return peer.ConnectionState.WhenConnected(cancellationToken);
    }
}
