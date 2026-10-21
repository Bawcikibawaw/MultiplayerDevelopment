using FishNet;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Transporting;
using UnityEngine;

public class ConnectionLogger : MonoBehaviour
{
    private NetworkManager _networkManager;

    private void Start()
    {
        _networkManager = InstanceFinder.NetworkManager;
        if (_networkManager == null) return;

        _networkManager.ServerManager.OnServerConnectionState += OnServerConnectionState;
        _networkManager.ServerManager.OnRemoteConnectionState += OnRemoteConnectionState;
        _networkManager.ClientManager.OnClientConnectionState += OnClientConnectionState;
    }

    private void OnDestroy()
    {
        if (_networkManager == null) return;

        if (_networkManager.ServerManager != null)
        {
            _networkManager.ServerManager.OnServerConnectionState -= OnServerConnectionState;
            _networkManager.ServerManager.OnRemoteConnectionState -= OnRemoteConnectionState;
        }
        if (_networkManager.ClientManager != null)
        {
            _networkManager.ClientManager.OnClientConnectionState -= OnClientConnectionState;
        }
    }

    private void OnServerConnectionState(ServerConnectionStateArgs args)
    {
        Debug.Log($"[Server] Server connection state: {args.ConnectionState}");
    }

    private void OnRemoteConnectionState(NetworkConnection conn, RemoteConnectionStateArgs args)
    {
        int count = _networkManager != null && _networkManager.ServerManager != null
            ? _networkManager.ServerManager.Clients.Count
            : 0;
        Debug.Log($"[Server] Client {conn.ClientId} {args.ConnectionState}. Total clients: {count}");

        if (args.ConnectionState == RemoteConnectionState.Started)
        {
            GameState.Announce($"Client {conn.ClientId} joined");
        }
        else if (args.ConnectionState == RemoteConnectionState.Stopped)
        {
            GameState.Announce($"Client {conn.ClientId} left");
        }
    }

    private void OnClientConnectionState(ClientConnectionStateArgs args)
    {
        Debug.Log($"[Client] Client connection state: {args.ConnectionState}");
    }
}
