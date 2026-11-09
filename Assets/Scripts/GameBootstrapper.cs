using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR
using UnityEditor;
#endif

public static class GameBootstrapper
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    public static void InitBinding()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Only run the battle bootstrapper in the actual Battle scene
        if (scene.name != "BattleScene") return;

        // Prevent duplicate initializations if we reload scripts
        if (GameObject.Find("GameWorld") != null) return;

        GameObject world = new GameObject("GameWorld");

        // Setup Camera
        GameObject camObj = new GameObject("Main Camera");
        camObj.tag = "MainCamera";
        camObj.transform.SetParent(world.transform);
        Camera c = camObj.AddComponent<Camera>();
        c.clearFlags = CameraClearFlags.SolidColor;
        c.backgroundColor = new Color(0.15f, 0.15f, 0.15f);
        camObj.AddComponent<AudioListener>();

        // Add BGM
#if UNITY_EDITOR
        AudioClip bgmClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Battle Music Pack DEMO/Battle Theme 1_demo.wav");
        if (bgmClip != null)
        {
            AudioSource bgmSource = camObj.AddComponent<AudioSource>();
            bgmSource.clip = bgmClip;
            bgmSource.loop = true;
            bgmSource.volume = 0.1f; // Lowered background volume
            bgmSource.playOnAwake = true;
            bgmSource.Play();
        }
#endif

        // Side-angled view: lower and further back, roughly 18° tilt
        camObj.transform.position = new Vector3(-4f, 1.5f, -4.5f);
        camObj.transform.rotation = Quaternion.Euler(14, 47, 0);
        camObj.AddComponent<BattleCamera>();   // presets (press C to cycle); applies the saved choice on Start

        // Light
        GameObject lightObj = new GameObject("Directional Light");
        lightObj.transform.SetParent(world.transform);
        Light l = lightObj.AddComponent<Light>();
        l.type = LightType.Directional;
        lightObj.transform.rotation = Quaternion.Euler(50, -30, 0);

        // Arena: dungeon built from Resources/Arenas/DungeonArena. If the theme asset is missing
        // or invalid, fall back to a plain floor so the battle is still playable.
        if (ArenaBuilder.Build(world.transform, l, c) == null)
        {
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.transform.SetParent(world.transform);
            floor.transform.position = Vector3.zero;
            floor.GetComponent<Renderer>().material.color = new Color(0.25f, 0.35f, 0.25f);
        }

        // Remove any default main camera so they don't clash
        Camera[] cams = GameObject.FindObjectsOfType<Camera>();
        foreach (var cam in cams)
        {
            if (cam.gameObject != camObj && cam.gameObject.name == "Main Camera")
            {
                GameObject.Destroy(cam.gameObject);
            }
        }

        // Get Managers
        var managerObj = GameObject.Find("Managers");
        if (managerObj == null)
        {
            managerObj = new GameObject("Managers");
            managerObj.transform.SetParent(world.transform);
        }

        // ── RunManager (persists across scene reloads) ────────────────────────
        RunManager runManager = GameObject.FindFirstObjectByType<RunManager>();
        if (runManager == null)
        {
            GameObject rmObj = new GameObject("RunManager");
            runManager = rmObj.AddComponent<RunManager>();
        }
        if (!runManager.IsRunActive)
            runManager.StartRun();

        BaseBattleManager battleManager = GameObject.FindFirstObjectByType<BaseBattleManager>();
        if (battleManager == null) battleManager = managerObj.AddComponent<BaseBattleManager>();

        EventSystem eventSystem = GameObject.FindFirstObjectByType<EventSystem>();
        if (eventSystem == null)
        {
            GameObject eventSystemObj = new GameObject("EventSystem");
            eventSystemObj.transform.SetParent(world.transform);
            eventSystem = eventSystemObj.AddComponent<EventSystem>();
            eventSystemObj.AddComponent<InputSystemUIInputModule>();
        }

        BattleUIManager uiManager = GameObject.FindFirstObjectByType<BattleUIManager>();
        if (uiManager == null) uiManager = managerObj.AddComponent<BattleUIManager>();

        uiManager.Initialize(battleManager);
        battleManager.Initialize(uiManager);
    }
}