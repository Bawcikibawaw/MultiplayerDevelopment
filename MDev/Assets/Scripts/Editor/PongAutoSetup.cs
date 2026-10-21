#if UNITY_EDITOR
using System.IO;
using System.Reflection;
using FishNet.Component.Spawning;
using FishNet.Component.Transforming;
using FishNet.Managing;
using FishNet.Managing.Client;
using FishNet.Managing.Observing;
using FishNet.Managing.Server;
using FishNet.Managing.Statistic;
using FishNet.Managing.Timing;
using FishNet.Managing.Transporting;
using FishNet.Object;
using FishNet.Transporting.Tugboat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class PongAutoSetup
{
    private const string SetupMarkerPath = "UserSettings/PongHomework2SetupDone_v4.txt";
    private const ulong PaddleAssetPathHash = 5269707828472649657UL;
    private const ulong BallAssetPathHash = 17141456174694237989UL;

    static PongAutoSetup()
    {
        EditorApplication.delayCall += TryAutoSetup;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            EditorApplication.delayCall += TryAutoSetup;
        }
        else if (state == PlayModeStateChange.ExitingEditMode)
        {
            EnsureSceneNetworkObjectIds(saveScene: false);
        }
    }

    private static void TryAutoSetup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            return;

        bool prefabsExist = File.Exists("Assets/Prefabs/Paddle.prefab") && File.Exists("Assets/Prefabs/Ball.prefab");
        bool markerExists = File.Exists(SetupMarkerPath);
        GameObject nmInScene = GameObject.Find("NetworkManager");
        bool hasUnsetSceneId = HasAnyUnsetSceneNetworkObjectId();

        if (!prefabsExist || !markerExists || nmInScene == null || hasUnsetSceneId)
        {
            RunFullSetup();
        }
    }

    private static bool HasAnyUnsetSceneNetworkObjectId()
    {
        foreach (NetworkObject nob in Object.FindObjectsByType<NetworkObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (nob != null && nob.gameObject.scene.IsValid() && !nob.IsSceneObject)
            {
                return true;
            }
        }
        return false;
    }

    [MenuItem("Pong Homework/Setup Scene & Prefabs (Homework 2)")]
    public static void RunFullSetup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[PongAutoSetup] Please exit Play Mode before running setup.");
            return;
        }

        EnsureDirectory("UserSettings");
        File.WriteAllText(SetupMarkerPath, "Homework 2 setup v4 completed.");

        EnsureDirectory("Assets/Prefabs");
        EnsureDirectory("Assets/Materials");
        EnsureDirectory("Assets/Scenes");

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Color");

        Material floorMat = GetOrCreateMaterial("Assets/Materials/CourtFloor.mat", shader, new Color(0.11f, 0.14f, 0.19f, 1f));
        Material wallMat = GetOrCreateMaterial("Assets/Materials/CourtWall.mat", shader, new Color(0.75f, 0.80f, 0.88f, 1f));
        Material lineMat = GetOrCreateMaterial("Assets/Materials/CourtLine.mat", shader, new Color(0.28f, 0.34f, 0.42f, 1f));
        Material paddleMat = GetOrCreateMaterial("Assets/Materials/PaddleMat.mat", shader, new Color(0.2f, 0.55f, 1f, 1f));
        Material ballMat = GetOrCreateMaterial("Assets/Materials/BallMat.mat", shader, new Color(0.55f, 0.55f, 0.55f, 1f));

        NetworkObject paddlePrefab = CreateOrUpdatePaddlePrefab("Assets/Prefabs/Paddle.prefab", paddleMat);
        NetworkObject ballPrefab = CreateOrUpdateBallPrefab("Assets/Prefabs/Ball.prefab", ballMat);

        FishNet.Editing.RefreshDefaultPrefabsMenu.RebuildDefaultPrefabs();

        PopulateCurrentScene(floorMat, wallMat, lineMat, paddlePrefab, ballPrefab);

        Scene activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(activeScene);
        EditorSceneManager.SaveScene(activeScene);

        // Now that the scene has been saved and objects have persistent file IDs,
        // serialize valid FishNet SceneIds on all scene NetworkObjects and save again.
        EnsureSceneNetworkObjectIds(saveScene: true);

        if (activeScene.path != "Assets/Scenes/Pong.unity")
        {
            EditorSceneManager.SaveScene(activeScene, "Assets/Scenes/Pong.unity", true);
        }

        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene("Assets/Scenes/SampleScene.unity", true),
            new EditorBuildSettingsScene("Assets/Scenes/Pong.unity", true)
        };

        AssetDatabase.SaveAssets();
        Debug.Log("[PongAutoSetup] Homework 1 + Homework 2 scene, NetworkObjects (Court & GameState SceneIds initialized), Paddle prefab (ClientAuthoritative=OFF), and Ball prefab (ClientAuthoritative=OFF) are ready!");
    }

    private static void EnsureSceneNetworkObjectIds(bool saveScene)
    {
        MethodInfo reserializeMethod = typeof(NetworkObject).GetMethod(
            "ReserializeEditorSetValues",
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        MethodInfo createSceneIdMethod = typeof(NetworkObject).GetMethod(
            "CreateSceneId",
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

        NetworkObject[] sceneNobs = Object.FindObjectsByType<NetworkObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        ulong nextFallbackId = 100001UL;

        foreach (NetworkObject nob in sceneNobs)
        {
            if (nob == null || !nob.gameObject.scene.IsValid())
                continue;

            nob.gameObject.SetActive(true);

            try
            {
                reserializeMethod?.Invoke(nob, new object[] { true, true });
                createSceneIdMethod?.Invoke(nob, new object[] { true });
            }
            catch
            {
                // Fallback to SerializedObject below if reflection invocation fails
            }

            ulong deterministicId = GetDeterministicSceneId(nob.gameObject.name, nextFallbackId++);

            SerializedObject nobSo = new SerializedObject(nob);
            SetBoolProp(nobSo, "WasActiveDuringEdit", true);
            SetBoolProp(nobSo, "WasActiveDuringEdit_Set1", true);

            SerializedProperty sceneIdProp = nobSo.FindProperty("SceneId");
            if (sceneIdProp != null && sceneIdProp.ulongValue == 0UL)
            {
                sceneIdProp.ulongValue = deterministicId;
            }

            nobSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(nob);
            EditorUtility.SetDirty(nob.gameObject);
        }

        if (saveScene)
        {
            Scene activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);
        }
    }

    private static ulong GetDeterministicSceneId(string objectName, ulong defaultId)
    {
        if (objectName == "Court") return 1319050668UL;
        if (objectName == "GameState") return 1405627999UL;
        return defaultId;
    }

    private static void EnsureDirectory(string path)
    {
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }
    }

    private static Material GetOrCreateMaterial(string path, Shader shader, Color color)
    {
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(shader);
            mat.color = color;
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", color);
            AssetDatabase.CreateAsset(mat, path);
        }
        else
        {
            if (shader != null) mat.shader = shader;
            mat.color = color;
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", color);
            EditorUtility.SetDirty(mat);
        }
        return mat;
    }

    private static NetworkObject CreateOrUpdatePaddlePrefab(string prefabPath, Material mat)
    {
        NetworkObject existing = AssetDatabase.LoadAssetAtPath<NetworkObject>(prefabPath);
        if (existing != null && existing.GetComponent<PaddleController>() != null && existing.GetComponent<NetworkTransform>() != null)
        {
            Renderer existingRend = existing.GetComponent<Renderer>();
            if (existingRend != null && mat != null) existingRend.sharedMaterial = mat;
            ConfigureNetworkTransform(existing.GetComponent<NetworkTransform>(), clientAuthoritative: false, interpolation: 2);
            SetPrefabAssetPathHash(existing, PaddleAssetPathHash);
            EditorUtility.SetDirty(existing.gameObject);
            return existing;
        }

        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "Paddle";
        go.transform.position = new Vector3(-Court.PaddleX, Court.BallY, 0f);
        go.transform.localScale = new Vector3(0.35f, 0.5f, Court.PaddleHalfLength * 2f);

        Collider col = go.GetComponent<Collider>();
        if (col != null) Object.DestroyImmediate(col);

        Renderer rend = go.GetComponent<Renderer>();
        if (rend != null && mat != null) rend.sharedMaterial = mat;

        NetworkObject nob = go.AddComponent<NetworkObject>();
        SetPrefabAssetPathHash(nob, PaddleAssetPathHash);

        PaddleController pc = go.AddComponent<PaddleController>();
        pc.speed = 8f;

        NetworkTransform nt = go.AddComponent<NetworkTransform>();
        ConfigureNetworkTransform(nt, clientAuthoritative: false, interpolation: 2);

        GameObject saved = PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
        Object.DestroyImmediate(go);

        return saved.GetComponent<NetworkObject>();
    }

    private static NetworkObject CreateOrUpdateBallPrefab(string prefabPath, Material mat)
    {
        NetworkObject existing = AssetDatabase.LoadAssetAtPath<NetworkObject>(prefabPath);
        if (existing != null && existing.GetComponent<NetworkBall>() != null && existing.GetComponent<NetworkTransform>() != null)
        {
            Renderer existingRend = existing.GetComponent<Renderer>();
            if (existingRend != null && mat != null) existingRend.sharedMaterial = mat;
            ConfigureNetworkTransform(existing.GetComponent<NetworkTransform>(), clientAuthoritative: false, interpolation: 2);
            SetPrefabAssetPathHash(existing, BallAssetPathHash);
            EditorUtility.SetDirty(existing.gameObject);
            return existing;
        }

        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "Ball";
        go.transform.position = new Vector3(0f, Court.BallY, 0f);
        float diameter = Court.BallRadius * 2f;
        go.transform.localScale = new Vector3(diameter, diameter, diameter);

        Collider col = go.GetComponent<Collider>();
        if (col != null) Object.DestroyImmediate(col);

        Renderer rend = go.GetComponent<Renderer>();
        if (rend != null && mat != null) rend.sharedMaterial = mat;

        NetworkObject nob = go.AddComponent<NetworkObject>();
        SetPrefabAssetPathHash(nob, BallAssetPathHash);

        NetworkBall nb = go.AddComponent<NetworkBall>();
        nb.speed = 9f;

        NetworkTransform nt = go.AddComponent<NetworkTransform>();
        ConfigureNetworkTransform(nt, clientAuthoritative: false, interpolation: 2);

        GameObject saved = PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
        Object.DestroyImmediate(go);

        return saved.GetComponent<NetworkObject>();
    }

    private static void SetPrefabAssetPathHash(NetworkObject nob, ulong hash)
    {
        SerializedObject so = new SerializedObject(nob);
        SerializedProperty hashProp = so.FindProperty("<AssetPathHash>k__BackingField");
        if (hashProp != null)
        {
            hashProp.ulongValue = hash;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static void ConfigureNetworkTransform(NetworkTransform nt, bool clientAuthoritative, ushort interpolation)
    {
        SerializedObject so = new SerializedObject(nt);
        SetBoolProp(so, "_clientAuthoritative", clientAuthoritative);
        SetBoolProp(so, "_sendToOwner", true);
        SetIntProp(so, "_interpolation", interpolation);
        SetBoolProp(so, "_synchronizePosition", true);
        SetBoolProp(so, "_synchronizeRotation", false);
        SetBoolProp(so, "_synchronizeScale", false);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void PopulateCurrentScene(
        Material floorMat,
        Material wallMat,
        Material lineMat,
        NetworkObject paddlePrefab,
        NetworkObject ballPrefab)
    {
        // Configure Main Camera for top-down view of the XZ Pong court
        Camera cam = Camera.main;
        if (cam == null)
        {
            GameObject camObj = new GameObject("Main Camera");
            camObj.tag = "MainCamera";
            cam = camObj.AddComponent<Camera>();
        }
        cam.transform.position = new Vector3(0f, 15f, 0f);
        cam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        cam.orthographic = true;
        cam.orthographicSize = 6.2f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.06f, 0.08f, 0.11f, 1f);

        // Remove any stray scene Paddle or Ball copies
        foreach (PaddleController strayPaddle in Object.FindObjectsByType<PaddleController>(FindObjectsSortMode.None))
        {
            Object.DestroyImmediate(strayPaddle.gameObject);
        }
        foreach (NetworkBall strayBall in Object.FindObjectsByType<NetworkBall>(FindObjectsSortMode.None))
        {
            Object.DestroyImmediate(strayBall.gameObject);
        }

        // 1. Court GameObject (NetworkObject + Court + BallSpawner)
        DestroyIfExists("Court");
        DestroyIfExists("CourtVisuals");
        GameObject courtGo = new GameObject("Court");
        courtGo.transform.position = Vector3.zero;
        courtGo.AddComponent<NetworkObject>();
        Court court = courtGo.AddComponent<Court>();
        court.LocalBall = false;
        BallSpawner ballSpawner = courtGo.AddComponent<BallSpawner>();
        ballSpawner.ballPrefab = ballPrefab;

        // Keep CourtVisuals on a non-networked GameObject so the court is always visible even before connecting
        GameObject visualsGo = new GameObject("CourtVisuals");
        visualsGo.transform.position = Vector3.zero;
        CreateVisualBox("Floor", visualsGo.transform, new Vector3(0f, -0.1f, 0f), new Vector3(Court.HalfWidth * 2f, 0.1f, Court.HalfHeight * 2f), floorMat);
        CreateVisualBox("TopWall", visualsGo.transform, new Vector3(0f, Court.BallY, Court.HalfHeight + 0.15f), new Vector3(Court.HalfWidth * 2f + 0.4f, 0.4f, 0.3f), wallMat);
        CreateVisualBox("BottomWall", visualsGo.transform, new Vector3(0f, Court.BallY, -Court.HalfHeight - 0.15f), new Vector3(Court.HalfWidth * 2f + 0.4f, 0.4f, 0.3f), wallMat);
        CreateVisualBox("CenterLine", visualsGo.transform, new Vector3(0f, -0.02f, 0f), new Vector3(0.08f, 0.1f, Court.HalfHeight * 2f), lineMat);

        // 2. NetworkManager GameObject
        DestroyIfExists("NetworkManager");
        GameObject nmGo = new GameObject("NetworkManager");
        NetworkManager nm = nmGo.AddComponent<NetworkManager>();
        ServerManager serverMgr = GetOrAdd<ServerManager>(nmGo);
        ClientManager clientMgr = GetOrAdd<ClientManager>(nmGo);
        TransportManager transportMgr = GetOrAdd<TransportManager>(nmGo);
        TimeManager timeMgr = GetOrAdd<TimeManager>(nmGo);
        FishNet.Managing.Scened.SceneManager sceneMgr = GetOrAdd<FishNet.Managing.Scened.SceneManager>(nmGo);
        ObserverManager observerMgr = GetOrAdd<ObserverManager>(nmGo);
        StatisticsManager statsMgr = GetOrAdd<StatisticsManager>(nmGo);
        Tugboat tugboat = GetOrAdd<Tugboat>(nmGo);
        PlayerSpawner playerSpawner = GetOrAdd<PlayerSpawner>(nmGo);
        GetOrAdd<ServerBootstrap>(nmGo);
        GetOrAdd<ConnectionLogger>(nmGo);

        // Configure Tugboat (Port 7770, Client Address localhost)
        SerializedObject tugSo = new SerializedObject(tugboat);
        SetIntProp(tugSo, "_port", 7770);
        SetStringProp(tugSo, "_clientAddress", "localhost");
        tugSo.ApplyModifiedPropertiesWithoutUndo();

        // Configure ServerManager (Start On Headless = false)
        SerializedObject srvSo = new SerializedObject(serverMgr);
        SetBoolProp(srvSo, "_startOnHeadless", false);
        srvSo.ApplyModifiedPropertiesWithoutUndo();

        // Configure TimeManager (Tick Rate = 30)
        SerializedObject timeSo = new SerializedObject(timeMgr);
        SetIntProp(timeSo, "_tickRate", 30);
        timeSo.ApplyModifiedPropertiesWithoutUndo();

        // Configure StatisticsManager (Enable Mode Development, Update Client + Update Server)
        SerializedObject statsSo = new SerializedObject(statsMgr);
        SerializedProperty trafficProp = statsSo.FindProperty("_networkTraffic");
        if (trafficProp != null)
        {
            SerializedProperty modeProp = trafficProp.FindPropertyRelative("_enableMode");
            if (modeProp != null) modeProp.enumValueIndex = 1; // Development
            SerializedProperty updClientProp = trafficProp.FindPropertyRelative("_updateClient");
            if (updClientProp != null) updClientProp.boolValue = true;
            SerializedProperty updServerProp = trafficProp.FindPropertyRelative("_updateServer");
            if (updServerProp != null) updServerProp.boolValue = true;
        }
        statsSo.ApplyModifiedPropertiesWithoutUndo();

        // Configure PlayerSpawner (Player Prefab = Paddle.prefab)
        playerSpawner.SetPlayerPrefab(paddlePrefab);
        SerializedObject psSo = new SerializedObject(playerSpawner);
        SerializedProperty prefabProp = psSo.FindProperty("_playerPrefab");
        if (prefabProp != null) prefabProp.objectReferenceValue = paddlePrefab;
        psSo.ApplyModifiedPropertiesWithoutUndo();

        // 3. GameState GameObject
        DestroyIfExists("GameState");
        GameObject gsGo = new GameObject("GameState");
        gsGo.AddComponent<NetworkObject>();
        gsGo.AddComponent<GameState>();

        // 4. Hud GameObject
        DestroyIfExists("Hud");
        GameObject hudGo = new GameObject("Hud");
        hudGo.AddComponent<ConnectionHud>();
    }

    private static void CreateVisualBox(string name, Transform parent, Vector3 localPos, Vector3 localScale, Material mat)
    {
        GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = name;
        box.transform.SetParent(parent, false);
        box.transform.localPosition = localPos;
        box.transform.localScale = localScale;
        Collider col = box.GetComponent<Collider>();
        if (col != null) Object.DestroyImmediate(col);
        Renderer rend = box.GetComponent<Renderer>();
        if (rend != null && mat != null) rend.sharedMaterial = mat;
    }

    private static void DestroyIfExists(string name)
    {
        GameObject existing = GameObject.Find(name);
        while (existing != null)
        {
            Object.DestroyImmediate(existing);
            existing = GameObject.Find(name);
        }
    }

    private static T GetOrAdd<T>(GameObject go) where T : Component
    {
        T comp = go.GetComponent<T>();
        if (comp == null) comp = go.AddComponent<T>();
        return comp;
    }

    private static void SetBoolProp(SerializedObject so, string name, bool value)
    {
        SerializedProperty p = so.FindProperty(name);
        if (p != null) p.boolValue = value;
    }

    private static void SetIntProp(SerializedObject so, string name, int value)
    {
        SerializedProperty p = so.FindProperty(name);
        if (p != null) p.intValue = value;
    }

    private static void SetStringProp(SerializedObject so, string name, string value)
    {
        SerializedProperty p = so.FindProperty(name);
        if (p != null) p.stringValue = value;
    }
}
#endif
