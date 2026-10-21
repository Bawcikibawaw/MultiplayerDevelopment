# Multiplayer Pong (FishNet) — Week 3 & Week 4 Homework

## Ownership & State Overview
Each connected player owns their spawned `Paddle` (`NetworkObject`), which allows that client to send input (`[ServerRpc] ServerSetAxis`) and serve requests (`[ServerRpc] RequestServe`) to the server. Nobody on the client side owns the `Ball` or `GameState`—the server alone simulates the ball (`NetworkBall`), moves the paddles on each tick (`OnTick`), validates and rate-limits client inputs (30 ms minimum gap), and updates scores. Everybody shares the replicated game state (`LeftScore`, `RightScore`, `ServingSide`, the `Feed` event log, and the server-authoritative `NetworkTransform` positions of the paddles and ball).

## How to Run Server and Client
1. Open `Assets/Scenes/Pong.unity` (or `Assets/Scenes/SampleScene.unity`) in Unity 6 (`6000.3.x`) and press **Play**.
2. In the top-left HUD, click **Start Server** on the server instance (or **Start Host (Server + Client)** to run both in one window).
3. On each client window (via **Window > Multiplayer > Multiplayer Play Mode**), set IP to `127.0.0.1` (Port `7770`) and click **Start Client**.

## Homework Deliverables
- **[Week03/README.md](Week03/README.md)** — Homework 1 checkpoints, design sheet, and RPC/tick-rate answers.
- **[Week04/README.md](Week04/README.md)** — Homework 2 server-authoritative paddle, input validation, 30 ms rate limit, and `C` / `X` / `T` cheat test answers.
