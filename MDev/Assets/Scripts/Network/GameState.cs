using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

public class GameState : NetworkBehaviour
{
    public static GameState Instance;

    public readonly SyncVar<int> LeftScore = new SyncVar<int>(0);
    public readonly SyncVar<int> RightScore = new SyncVar<int>(0);
    public readonly SyncVar<int> ServingSide = new SyncVar<int>(0);

    public readonly SyncList<string> Feed = new SyncList<string>();

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        ConnectionHud.Panels.Add(() =>
            $"LEFT {LeftScore.Value} : {RightScore.Value} RIGHT\nServe: {(ServingSide.Value == 0 ? "LEFT" : "RIGHT")}");

        ConnectionHud.Panels.Add(() =>
            Feed.Count == 0 ? "Event Feed: (empty)" : "Event Feed:\n" + string.Join("\n", Feed));
    }

    public static void Announce(string line)
    {
        if (Instance == null || !Instance.IsServerInitialized) return;

        Instance.Feed.Add(line);
        while (Instance.Feed.Count > 6)
        {
            Instance.Feed.RemoveAt(0);
        }
    }

    public void Goal(int scorerSide)
    {
        if (!IsServerInitialized) return;

        if (scorerSide == 0)
            LeftScore.Value++;
        else
            RightScore.Value++;

        int concederSide = 1 - scorerSide;
        ServingSide.Value = concederSide;

        string scorerName = scorerSide == 0 ? "LEFT" : "RIGHT";
        Announce($"{scorerName} scored! {LeftScore.Value} : {RightScore.Value}");

        PaddleController scorerPaddle = null;
        PaddleController concederPaddle = null;
        PaddleController fallbackPaddle = null;

        for (int i = 0; i < PaddleController.All.Count; i++)
        {
            PaddleController p = PaddleController.All[i];
            if (p == null) continue;
            if (fallbackPaddle == null) fallbackPaddle = p;
            if (p.Side.Value == scorerSide) scorerPaddle = p;
            if (p.Side.Value == concederSide) concederPaddle = p;
        }

        PaddleController announcer = scorerPaddle != null ? scorerPaddle : fallbackPaddle;
        if (announcer != null)
        {
            announcer.AnnounceGoal(LeftScore.Value, RightScore.Value);
        }

        if (concederPaddle != null && concederPaddle.Owner != null && concederPaddle.Owner.IsValid)
        {
            concederPaddle.YouServe(concederPaddle.Owner);
        }
    }
}
