using System.Collections.Generic;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class PaddleController : NetworkBehaviour
{
    public const int Spectator = 2;

    public static readonly List<PaddleController> All = new List<PaddleController>();
    public static PaddleController Local;

    public float speed = 8f;

    // 0 = LEFT, 1 = RIGHT, 2 = Spectator
    public readonly SyncVar<int> Side = new SyncVar<int>(Spectator);

    // Homework 2 Step 2 — Server-only memory: the last input the server accepted
    private float _serverAxis;

    // Homework 2 Step 4 — Server-only rate-limit fields (30 ms minimum gap)
    private const double MinInputGap = 0.030;
    private double _lastInputTime = -1;
    public int RejectedInputs;

    // Homework 2 Step 4 Extra — 1-second rate limit on serve requests
    private const double MinServeGap = 1.0;
    private double _lastServeTime = -1;

    // Prevents OnTick from sending a duplicate 0 axis on the exact tick a debug key (C or X) already sent ServerSetAxis
    private bool _sentDebugInputThisTick;

    private Renderer _renderer;

    private void Awake()
    {
        _renderer = GetComponentInChildren<Renderer>();
        Side.OnChange += OnSideChanged;
    }

    private void OnDestroy()
    {
        Side.OnChange -= OnSideChanged;
    }

    public override void OnStartNetwork()
    {
        base.OnStartNetwork();
        TimeManager.OnTick += OnTick;
    }

    public override void OnStopNetwork()
    {
        if (TimeManager != null)
        {
            TimeManager.OnTick -= OnTick;
        }
        base.OnStopNetwork();
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        if (!All.Contains(this))
            All.Add(this);

        bool leftTaken = false;
        bool rightTaken = false;
        foreach (PaddleController p in All)
        {
            if (p == this) continue;
            if (p.Side.Value == 0) leftTaken = true;
            else if (p.Side.Value == 1) rightTaken = true;
        }

        if (!leftTaken)
            Side.Value = 0;
        else if (!rightTaken)
            Side.Value = 1;
        else
            Side.Value = Spectator;

        Place();
        Debug.Log($"[Server] Client {OwnerId} assigned to {SideToString(Side.Value)} (Side={Side.Value})");

        // Homework 2 Step 4 — Show rejected inputs count in the HUD
        int capturedOwnerId = OwnerId;
        ConnectionHud.Panels.Add(() => $"Rejected inputs (paddle {capturedOwnerId}): {RejectedInputs}");
    }

    public override void OnStopServer()
    {
        All.Remove(this);
        base.OnStopServer();
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        if (!All.Contains(this))
            All.Add(this);

        if (IsOwner)
        {
            Local = this;
            Debug.Log($"[Client] I am client {OwnerId}, side {SideToString(Side.Value)}");
        }

        Place();
    }

    public override void OnStopClient()
    {
        All.Remove(this);
        if (Local == this)
            Local = null;
        base.OnStopClient();
    }

    private void OnSideChanged(int prev, int next, bool asServer)
    {
        Place();
        if (!asServer && IsOwner)
        {
            Debug.Log($"[Client] I am client {OwnerId}, side {SideToString(next)}");
        }
    }

    public void Place()
    {
        if (_renderer == null)
            _renderer = GetComponentInChildren<Renderer>();

        if (Side.Value == 0)
        {
            transform.position = new Vector3(-Court.PaddleX, Court.BallY, transform.position.z);
            if (_renderer != null)
            {
                _renderer.enabled = true;
                Color c = new Color(0.2f, 0.55f, 1f); // Blue = LEFT
                _renderer.material.color = c;
                if (_renderer.material.HasProperty("_BaseColor"))
                    _renderer.material.SetColor("_BaseColor", c);
            }
        }
        else if (Side.Value == 1)
        {
            transform.position = new Vector3(Court.PaddleX, Court.BallY, transform.position.z);
            if (_renderer != null)
            {
                _renderer.enabled = true;
                Color c = new Color(1f, 0.55f, 0.15f); // Orange = RIGHT
                _renderer.material.color = c;
                if (_renderer.material.HasProperty("_BaseColor"))
                    _renderer.material.SetColor("_BaseColor", c);
            }
        }
        else
        {
            if (_renderer != null)
            {
                _renderer.enabled = false; // Spectator -> renderer off
            }
        }
    }

    private void Update()
    {
        if (!IsOwner) return;

        // Serve request on Space
        if (WasKeyPressedThisFrame(KeyCode.Space))
        {
            RequestServe();
        }

        // Homework 2 Step 5 — The cheating client debug keys
        // C (spam): a for loop that calls ServerSetAxis(1f) 20 times in one frame.
        if (WasKeyPressedThisFrame(KeyCode.C))
        {
            _sentDebugInputThisTick = true;
            for (int i = 0; i < 20; i++)
            {
                ServerSetAxis(1f);
            }
        }

        // X (speed hack): ServerSetAxis(50f) once.
        if (WasKeyPressedThisFrame(KeyCode.X))
        {
            _sentDebugInputThisTick = true;
            ServerSetAxis(50f);
        }

        // T (teleport): transform.position = NetworkBall.Instance.transform.position; on the client, no RPC.
        if (WasKeyPressedThisFrame(KeyCode.T))
        {
            if (NetworkBall.Instance != null)
            {
                transform.position = NetworkBall.Instance.transform.position;
            }
        }
    }

    private void OnTick()
    {
        // 1. Owner sends
        if (IsOwner && Side.Value != Spectator)
        {
            if (_sentDebugInputThisTick)
            {
                _sentDebugInputThisTick = false;
            }
            else
            {
                float axis = ReadAxis();
                ServerSetAxis(axis);
            }
        }

        // 2. Server moves
        if (IsServerInitialized)
        {
            transform.position += Vector3.forward * _serverAxis * speed * (float)TimeManager.TickDelta;
            Court.ClampPaddle(transform);
        }
    }

    [ServerRpc]
    private void ServerSetAxis(float axis)
    {
        // Homework 2 Step 3 — Check the input (well-formed? -> clamp -> allowed by game rule?)
        if (float.IsNaN(axis)) return;
        axis = Mathf.Clamp(axis, -1f, 1f);
        if (Side.Value == Spectator) return;

        // Homework 2 Step 4 — No second input inside 30 ms
        double now = TimeManager.TicksToTime(TimeManager.Tick);
        if (_lastInputTime >= 0 && now - _lastInputTime < MinInputGap)
        {
            RejectedInputs++;
            Debug.Log($"[Server] Rejected input from client {OwnerId}: too fast.");
            return;
        }
        _lastInputTime = now;

        _serverAxis = axis;
    }

    // Homework 1 Step 8 + Homework 2 Step 4 Extra (1-second rate limit on serve)
    [ServerRpc]
    public void RequestServe()
    {
        HandleServerRequestServe();
    }

    [ServerRpc]
    public void ServerRequestServe()
    {
        HandleServerRequestServe();
    }

    private void HandleServerRequestServe()
    {
        double now = TimeManager.TicksToTime(TimeManager.Tick);
        if (_lastServeTime >= 0 && now - _lastServeTime < MinServeGap)
        {
            if (Owner != null && Owner.IsValid)
                TooFastServe(Owner);
            return;
        }
        _lastServeTime = now;

        if (GameState.Instance == null || NetworkBall.Instance == null) return;
        if (Side.Value == Spectator) return;
        if (Side.Value != GameState.Instance.ServingSide.Value) return;
        if (NetworkBall.Instance.InPlay.Value) return;

        NetworkBall.Instance.Serve(1 - Side.Value);
    }

    [TargetRpc]
    private void TooFastServe(NetworkConnection target)
    {
        ConnectionHud.Flash("TOO FAST");
    }

    [ObserversRpc]
    public void AnnounceGoal(int left, int right)
    {
        string scorer = (GameState.Instance != null && GameState.Instance.ServingSide.Value == 1) ? "LEFT" : "RIGHT";
        ConnectionHud.Flash($"{scorer} scored! {left} : {right}");
    }

    [TargetRpc]
    public void YouServe(NetworkConnection target)
    {
        ConnectionHud.Flash("YOU SERVE");
    }

    private static string SideToString(int side)
    {
        if (side == 0) return "LEFT";
        if (side == 1) return "RIGHT";
        return "SPECTATOR";
    }

    private static float ReadAxis()
    {
        float axis = 0f;
        if (IsKeyHeld(KeyCode.W) || IsKeyHeld(KeyCode.UpArrow)) axis += 1f;
        if (IsKeyHeld(KeyCode.S) || IsKeyHeld(KeyCode.DownArrow)) axis -= 1f;
        return Mathf.Clamp(axis, -1f, 1f);
    }

    private static bool IsKeyHeld(KeyCode key)
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            switch (key)
            {
                case KeyCode.W: return Keyboard.current.wKey.isPressed;
                case KeyCode.S: return Keyboard.current.sKey.isPressed;
                case KeyCode.UpArrow: return Keyboard.current.upArrowKey.isPressed;
                case KeyCode.DownArrow: return Keyboard.current.downArrowKey.isPressed;
            }
        }
#endif
        return Input.GetKey(key);
    }

    private static bool WasKeyPressedThisFrame(KeyCode key)
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            switch (key)
            {
                case KeyCode.Space: return Keyboard.current.spaceKey.wasPressedThisFrame;
                case KeyCode.C: return Keyboard.current.cKey.wasPressedThisFrame;
                case KeyCode.X: return Keyboard.current.xKey.wasPressedThisFrame;
                case KeyCode.T: return Keyboard.current.tKey.wasPressedThisFrame;
            }
        }
#endif
        return Input.GetKeyDown(key);
    }
}
