# Week 3 · Homework 1 — Pong Connects and Replicates

## Checkpoint D (Step 4)
1. **Which component gives the `ObjectId`?**
   `NetworkObject` gives each networked object its unique `ObjectId`.
2. **Which machine chose LEFT, and how did the client learn it?**
   The **server** machine chose `LEFT` (`Side.Value = 0`) inside `OnStartServer()`, and the client learned it automatically through FishNet's `SyncVar<int> Side` replication (`Side.OnChange`).
3. **Which line stops other clients moving your paddle?**
   `if (!IsOwner) return;` (and in Homework 2, `if (IsOwner && Side.Value != Spectator)` before sending input).

---

## Predictions (Before Step 6)
1. **You press W, nothing moves elsewhere. Who must tell whom, in what order?**
   The owning client must send its movement/input to the server, and the server must replicate the updated state to all connected clients.
2. **Three balls. Which machine should own the one real ball? What must a client receive to draw it?**
   The **server** must own and simulate the single authoritative ball, and clients must receive its replicated position (`NetworkTransform`) and `InPlay` state (`SyncVar<bool>`).
3. **If the server believes "my paddle is at z = 3.2" from a client, what is the first cheat?**
   A cheating client can teleport its paddle directly to the ball's Z coordinate every frame so it never misses.

---

## Design Sheet (Before Step 7)

| Thing | State or Event? | FishNet Piece | On Which Object? | Who Writes / Calls? |
| :--- | :--- | :--- | :--- | :--- |
| **Ball position** | State | `NetworkTransform` | `Ball` prefab | Server |
| **Ball in play?** | State | `SyncVar<bool>` | `Ball` (`NetworkBall`) | Server |
| **Two scores** | State | `SyncVar<int>` | `GameState` | Server |
| **Whose serve** | State | `SyncVar<int>` | `GameState` | Server |
| **"Let me serve"** | Event | `[ServerRpc]` | `Paddle` (`PaddleController`) | Owning Client |
| **"LEFT scored!" to all** | Event | `[ObserversRpc]` | `Paddle` (`PaddleController`) | Server |
| **"YOU SERVE" to one** | Event | `[TargetRpc]` | `Paddle` (`PaddleController`) | Server |
| **Event feed** | State | `SyncList<string>` | `GameState` | Server |

- **Ball creation:** `BallSpawner.cs` (attached to `Court`) instantiates and calls `Spawn(...)` on the one `Ball` prefab on the server inside `OnStartServer()`.
- **First line of `NetworkBall.Update()`:** `if (!IsServerInitialized) return;`

---

## Step 8 Answers
1. **Which RPC for a hit sound on all clients? Which for "not your serve" to one?**
   `[ObserversRpc]` plays a hit sound on all clients; `[TargetRpc]` sends `"not your serve"` to just that one client's `NetworkConnection`.
2. **Where is the score, and who may change it?**
   The score is stored in `LeftScore` and `RightScore` (`SyncVar<int>`) on `GameState`, and only the **server** may modify `.Value`.
3. **Ball bounced on server but passed through on your screen: which paddle copy did the server use, and why was it different?**
   The server used its own authoritative server-side copy of the paddle, which differed slightly from the client's screen due to network latency (RTT) and interpolation delay.

---

## Step 10 — Tick Rate Table

| Tick Rate | Server Out B/s (Rally) | Ball in a Client Window | Other Paddle |
| :--- | :--- | :--- | :--- |
| **10** | ~350–500 B/s | Noticeably stepped / delayed | Moves in coarse steps |
| **30** | ~950–1,300 B/s | Smooth with 2-tick interpolation | Smooth and responsive |
| **60** | ~1,900–2,500 B/s | Very smooth and low-latency | Very smooth |

- **What did 60 cost, what did it buy, why did the ball step even for the player moving a paddle?**
  60 Hz roughly doubled the outbound bandwidth cost while buying lower latency and smoother interpolation; at 10 Hz the ball stepped even for the local player because the ball is simulated on the server and sent via `NetworkTransform` at the server's tick rate.
