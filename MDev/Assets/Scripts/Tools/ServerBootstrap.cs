using System;
using System.Linq;
using System.Reflection;
using FishNet;
using FishNet.Managing;
using UnityEngine;

public class ServerBootstrap : MonoBehaviour
{
    public bool autoStartServerForTaggedPlayer = true;

    private void Start()
    {
        NetworkManager nm = InstanceFinder.NetworkManager;
        if (nm == null) return;

        if (Application.isBatchMode)
        {
            nm.ServerManager.StartConnection();
            return;
        }

        if (autoStartServerForTaggedPlayer && HasServerMultiplayerTag())
        {
            nm.ServerManager.StartConnection();
        }
    }

    private static bool HasServerMultiplayerTag()
    {
        try
        {
            Type currentPlayerType = Type.GetType("Unity.Multiplayer.Playmode.CurrentPlayer, Unity.Multiplayer.Playmode");
            if (currentPlayerType == null) return false;

            MethodInfo readOnlyTagsMethod = currentPlayerType.GetMethod("ReadOnlyTags", BindingFlags.Public | BindingFlags.Static);
            if (readOnlyTagsMethod == null) return false;

            string[] tags = readOnlyTagsMethod.Invoke(null, null) as string[];
            return tags != null && tags.Contains("Server");
        }
        catch
        {
            return false;
        }
    }
}
