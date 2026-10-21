using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

public class NetworkBall : NetworkBehaviour
{
    public static NetworkBall Instance;

    public float speed = 9f;
    public readonly SyncVar<bool> InPlay = new SyncVar<bool>(false);

    private Vector3 _velocity;
    private Renderer _renderer;

    private void Awake()
    {
        Instance = this;
        _renderer = GetComponentInChildren<Renderer>();
        InPlay.OnChange += OnInPlayChanged;
        ApplyColor(InPlay.Value);
    }

    private void OnDestroy()
    {
        InPlay.OnChange -= OnInPlayChanged;
        if (Instance == this)
            Instance = null;
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        Instance = this;
        ResetToCenter();
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        Instance = this;
        ApplyColor(InPlay.Value);
    }

    private void OnInPlayChanged(bool prev, bool next, bool asServer)
    {
        ApplyColor(next);
    }

    private void ApplyColor(bool inPlay)
    {
        if (_renderer == null)
            _renderer = GetComponentInChildren<Renderer>();
        if (_renderer != null)
        {
            Color c = inPlay ? Color.white : new Color(0.55f, 0.55f, 0.55f);
            _renderer.material.color = c;
            if (_renderer.material.HasProperty("_BaseColor"))
                _renderer.material.SetColor("_BaseColor", c);
        }
    }

    public void Serve(int towardSide)
    {
        if (!IsServerInitialized) return;

        transform.position = new Vector3(0f, Court.BallY, 0f);
        float dirX = towardSide == 0 ? -1f : 1f;
        float dirZ = Random.Range(-0.45f, 0.45f);
        _velocity = new Vector3(dirX, 0f, dirZ).normalized * speed;
        InPlay.Value = true;

        GameState.Announce($"Serve toward {(towardSide == 0 ? "LEFT" : "RIGHT")}");
    }

    private void ResetToCenter()
    {
        transform.position = new Vector3(0f, Court.BallY, 0f);
        _velocity = Vector3.zero;
        InPlay.Value = false;
    }

    private void Update()
    {
        if (!IsServerInitialized) return;
        if (!InPlay.Value) return;

        Vector3 pos = transform.position + _velocity * Time.deltaTime;

        // Top and bottom walls
        float wallZ = Court.HalfHeight - Court.BallRadius;
        if (pos.z > wallZ)
        {
            pos.z = wallZ;
            _velocity.z = -Mathf.Abs(_velocity.z);
        }
        else if (pos.z < -wallZ)
        {
            pos.z = -wallZ;
            _velocity.z = Mathf.Abs(_velocity.z);
        }

        // Paddles (from PaddleController.All)
        for (int i = 0; i < PaddleController.All.Count; i++)
        {
            PaddleController paddle = PaddleController.All[i];
            if (paddle == null) continue;

            if (paddle.Side.Value == 0 && _velocity.x < 0f)
            {
                if (pos.x - Court.BallRadius <= -Court.PaddleX && pos.x >= -Court.PaddleX - 0.65f)
                {
                    float dz = pos.z - paddle.transform.position.z;
                    if (Mathf.Abs(dz) <= Court.PaddleHalfLength + Court.BallRadius)
                    {
                        pos.x = -Court.PaddleX + Court.BallRadius;
                        float bounceZ = Mathf.Clamp(dz / Court.PaddleHalfLength, -0.8f, 0.8f);
                        _velocity = new Vector3(1f, 0f, bounceZ).normalized * speed;
                    }
                }
            }
            else if (paddle.Side.Value == 1 && _velocity.x > 0f)
            {
                if (pos.x + Court.BallRadius >= Court.PaddleX && pos.x <= Court.PaddleX + 0.65f)
                {
                    float dz = pos.z - paddle.transform.position.z;
                    if (Mathf.Abs(dz) <= Court.PaddleHalfLength + Court.BallRadius)
                    {
                        pos.x = Court.PaddleX - Court.BallRadius;
                        float bounceZ = Mathf.Clamp(dz / Court.PaddleHalfLength, -0.8f, 0.8f);
                        _velocity = new Vector3(-1f, 0f, bounceZ).normalized * speed;
                    }
                }
            }
        }

        transform.position = pos;

        // Goal line check
        if (pos.x < -Court.HalfWidth)
        {
            ResetToCenter();
            if (GameState.Instance != null)
                GameState.Instance.Goal(1); // RIGHT scored
        }
        else if (pos.x > Court.HalfWidth)
        {
            ResetToCenter();
            if (GameState.Instance != null)
                GameState.Instance.Goal(0); // LEFT scored
        }
    }
}
