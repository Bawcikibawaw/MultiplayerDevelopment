using FishNet.Object;
using UnityEngine;

public class BallSpawner : NetworkBehaviour
{
    public NetworkObject ballPrefab;

    public override void OnStartServer()
    {
        base.OnStartServer();
        if (ballPrefab == null) return;

        NetworkObject ball = Instantiate(ballPrefab, new Vector3(0f, Court.BallY, 0f), Quaternion.identity);
        Spawn(ball);
    }
}
