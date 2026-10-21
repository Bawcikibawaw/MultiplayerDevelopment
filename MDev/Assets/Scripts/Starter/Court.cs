using UnityEngine;

public class Court : MonoBehaviour
{
    public const float PaddleX = 7f;
    public const float HalfWidth = 8f;
    public const float HalfHeight = 5f;
    public const float BallY = 0.25f;
    public const float BallRadius = 0.25f;
    public const float PaddleHalfLength = 1f;

    [Tooltip("Enable single-player LocalBall when true. Untick for FishNet multiplayer (Step 7).")]
    public bool LocalBall = false;

    public GameObject LocalBallObject;

    private void Awake()
    {
        if (LocalBallObject != null)
        {
            LocalBallObject.SetActive(LocalBall);
        }
    }

    public static void ClampPaddle(Transform paddle)
    {
        if (paddle == null) return;
        Vector3 pos = paddle.position;
        float maxZ = HalfHeight - PaddleHalfLength;
        pos.z = Mathf.Clamp(pos.z, -maxZ, maxZ);
        paddle.position = pos;
    }
}
