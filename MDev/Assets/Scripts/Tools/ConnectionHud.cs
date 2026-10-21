using System;
using System.Collections.Generic;
using FishNet;
using FishNet.Managing;
using UnityEngine;

public class ConnectionHud : MonoBehaviour
{
    public static readonly List<Func<string>> Panels = new List<Func<string>>();

    private static string _flashText = string.Empty;
    private static float _flashUntil = -1f;

    private string _address = "127.0.0.1";
    private ushort _port = 7770;

    public static void Flash(string message, float duration = 2.5f)
    {
        _flashText = message;
        _flashUntil = Time.unscaledTime + duration;
        Debug.Log($"[HUD Flash] {message}");
    }

    private void Awake()
    {
        Panels.Clear();
        _flashText = string.Empty;
        _flashUntil = -1f;
    }

    private void OnGUI()
    {
        NetworkManager nm = InstanceFinder.NetworkManager;
        if (nm == null)
        {
            GUI.Box(new Rect(10, 10, 280, 40), "NetworkManager not found in scene.");
            return;
        }

        GUILayout.BeginArea(new Rect(10, 10, 320, Screen.height - 20));
        GUILayout.BeginVertical("box");

        bool serverStarted = nm.ServerManager.Started;
        bool clientStarted = nm.ClientManager.Started;

        GUILayout.Label($"<b>FishNet Pong HUD</b> (Port {_port})");

        GUILayout.BeginHorizontal();
        GUILayout.Label("IP:", GUILayout.Width(28));
        _address = GUILayout.TextField(_address, GUILayout.Width(135));
        string portStr = GUILayout.TextField(_port.ToString(), GUILayout.Width(55));
        ushort.TryParse(portStr, out _port);
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (!serverStarted)
        {
            if (GUILayout.Button("Start Server"))
            {
                nm.TransportManager.Transport.SetPort(_port);
                nm.ServerManager.StartConnection();
            }
        }
        else
        {
            if (GUILayout.Button("Stop Server"))
            {
                nm.ServerManager.StopConnection(true);
            }
        }

        if (!clientStarted)
        {
            if (GUILayout.Button("Start Client"))
            {
                nm.TransportManager.Transport.SetClientAddress(_address);
                nm.TransportManager.Transport.SetPort(_port);
                nm.ClientManager.StartConnection();
            }
        }
        else
        {
            if (GUILayout.Button("Stop Client"))
            {
                nm.ClientManager.StopConnection();
            }
        }
        GUILayout.EndHorizontal();

        if (!serverStarted && !clientStarted)
        {
            if (GUILayout.Button("Start Host (Server + Client)"))
            {
                nm.TransportManager.Transport.SetPort(_port);
                nm.TransportManager.Transport.SetClientAddress(_address);
                nm.ServerManager.StartConnection();
                nm.ClientManager.StartConnection();
            }
        }

        long rtt = nm.TimeManager != null ? nm.TimeManager.RoundTripTime : 0;
        uint tick = nm.TimeManager != null ? nm.TimeManager.Tick : 0;
        ushort tickRate = nm.TimeManager != null ? nm.TimeManager.TickRate : (ushort)0;

        GUILayout.Label($"Server: {(serverStarted ? "Started" : "Stopped")} | Client: {(clientStarted ? "Started" : "Stopped")}");
        GUILayout.Label($"RTT: {rtt} ms | Tick: {tick} ({tickRate} Hz)");
        GUILayout.Label("Keys: W/S or Up/Down | Space: Serve | C/X/T: Cheats");
        GUILayout.EndVertical();

        for (int i = 0; i < Panels.Count; i++)
        {
            if (Panels[i] == null) continue;
            string content = Panels[i].Invoke();
            if (!string.IsNullOrEmpty(content))
            {
                GUILayout.BeginVertical("box");
                GUILayout.Label(content);
                GUILayout.EndVertical();
            }
        }

        GUILayout.EndArea();

        if (Time.unscaledTime < _flashUntil && !string.IsNullOrEmpty(_flashText))
        {
            GUIStyle flashStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = 26,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            flashStyle.normal.textColor = Color.yellow;
            Rect flashRect = new Rect((Screen.width - 420f) * 0.5f, 30f, 420f, 56f);
            GUI.Box(flashRect, _flashText, flashStyle);
        }
    }
}
