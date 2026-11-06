using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class HauntedMapuaBuilder
{
    const string HallwayPrefab   = "Assets/Prefabs/hallway.prefab";
    const string RoomsPrefab     = "Assets/Prefabs/rooms1.prefab";
    const string AuditPrefab     = "Assets/Prefabs/audit.prefab";
    const string CharacterPrefab = "Assets/Prefabs/Character/Character.prefab";
    const string CharacterModel  = "Assets/Models/Characters (1).fbx";
    const string DebrisFolder    = "Assets/Prefabs/Debris/";

    const string ScenesFolder = "Assets/Scenes";
    const string MatFolder    = "Assets/Materials/Generated";

    const float PlayerHeight   = 1.8f;
    const float JumpHeight     = 1.6f;
    const float SafeStart      = 5f;
    const float SafeEnd        = 9f;
    const float MaxCourseLen   = 180f;
    const float TargetCourseLen = 60f;
    const float NeededHeadroom = 6.2f;
    const float MinInsideLen   = 18f;
    static readonly Vector3 PlatformSize = new Vector3(2.4f, 0.35f, 2.4f);

    static float K = 1f;

    class LevelDef
    {
        public int number; public string scene; public string prefab; public string title;
        public string objective; public string next; public bool final;
    }

    static readonly LevelDef[] Levels =
    {
        new LevelDef { number = 1, scene = "Level1_Hallway", prefab = HallwayPrefab, title = "The Hallway",
            objective = "Jump over the haunted mist. Backstab the ghost to unlock the exit.", next = "Level2_Rooms" },
        new LevelDef { number = 2, scene = "Level2_Rooms", prefab = RoomsPrefab, title = "The Classrooms",
            objective = "The desks are floating... Cross them and backstab the ghost.", next = "Level3_Auditorium" },
        new LevelDef { number = 3, scene = "Level3_Auditorium", prefab = AuditPrefab, title = "The Auditorium",
            objective = "Final level. Defeat the ghost on stage and escape Mapua.", next = "", final = true },
    };

    enum PType { Static, Lateral, Spin, Pulse, Orbit, Vertical }

    static readonly PType[][] Patterns =
    {
        new[] { PType.Static, PType.Lateral, PType.Static, PType.Spin, PType.Static, PType.Lateral, PType.Pulse, PType.Spin },
        new[] { PType.Static, PType.Spin, PType.Pulse, PType.Lateral, PType.Static, PType.Vertical, PType.Spin, PType.Lateral },
        new[] { PType.Static, PType.Orbit, PType.Lateral, PType.Vertical, PType.Spin, PType.Orbit, PType.Pulse, PType.Lateral },
    };
    static readonly float[] Heights = { 0.7f, 1.4f, 1.0f, 1.8f, 1.3f, 2.1f, 1.5f, 2.4f, 1.7f, 2.2f };

    [MenuItem("Haunted Mapua/Build Entire Game", priority = 0)]
    public static void BuildAll()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (!CheckAssets()) return;
        WarnIfNoTMP();
        PrepareCharacterImport();
        EnsureFolder(ScenesFolder);
        EnsureFolder(MatFolder);

        var report = new System.Text.StringBuilder();
        BuildMenuScene();
        foreach (LevelDef l in Levels) report.AppendLine(BuildLevelScene(l));
        SetBuildSettings();

        EditorSceneManager.OpenScene(ScenePath("MainMenu"));
        EditorUtility.ClearProgressBar();
        EditorUtility.DisplayDialog("Haunted Mapua",
            "Done! 4 scenes built and added to Build Settings.\n\n" + report +
            "\nPress PLAY in the MainMenu scene to test.", "OK");
    }

    [MenuItem("Haunted Mapua/Rebuild Level 1 (Hallway)", priority = 20)]
    static void RebuildL1() { RebuildOne(0); }
    [MenuItem("Haunted Mapua/Rebuild Level 2 (Rooms)", priority = 21)]
    static void RebuildL2() { RebuildOne(1); }
    [MenuItem("Haunted Mapua/Rebuild Level 3 (Auditorium)", priority = 22)]
    static void RebuildL3() { RebuildOne(2); }
    [MenuItem("Haunted Mapua/Rebuild Main Menu", priority = 23)]
    static void RebuildMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EnsureFolder(ScenesFolder);
        BuildMenuScene();
        SetBuildSettings();
    }

    static void RebuildOne(int i)
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (!CheckAssets()) return;
        PrepareCharacterImport();
        EnsureFolder(ScenesFolder);
        EnsureFolder(MatFolder);
        string r = BuildLevelScene(Levels[i]);
        SetBuildSettings();
        EditorUtility.ClearProgressBar();
        Debug.Log("[HauntedMapua] " + r);
    }

    static void BuildMenuScene()
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        GameObject camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        Camera cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.02f, 0.02f, 0.05f);
        camGo.AddComponent<AudioListener>();

        GameObject menu = new GameObject("MainMenu");
        MainMenu mm = menu.AddComponent<MainMenu>();
        mm.firstLevelScene = Levels[0].scene;
        mm.autoBuild = true;

        EditorSceneManager.SaveScene(scene, ScenePath("MainMenu"));
    }

    static string BuildLevelScene(LevelDef def)
    {
        EditorUtility.DisplayProgressBar("Haunted Mapua", "Building " + def.scene + "...", def.number / 4f);
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        K = MeasureCharacterScale();

        GameObject mapPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(def.prefab);
        GameObject map = (GameObject)PrefabUtility.InstantiatePrefab(mapPrefab);
        map.name = "Map_" + mapPrefab.name;
        AddMeshColliders amc = map.AddComponent<AddMeshColliders>();
        amc.addOnAwake = false;
        amc.AddNow();
        Physics.SyncTransforms();

        bool oldBack = Physics.queriesHitBackfaces;
        Physics.queriesHitBackfaces = true;

        Bounds rb = RobustBounds(map);
        bool hasFloor = MeasureInterior(rb, out float floorY, out float gap);
        if (!hasFloor) floorY = rb.min.y;

        float K0 = K;
        bool inside = FindPath(rb, floorY, out Vector3 pathStart, out Vector3 pathDir, out float pathLen,
                               out float halfWidth, out float clearHeight);
        if (!inside || pathLen / K < MinInsideLen)
        {
            K = K0 * 0.5f;
            inside = FindPath(rb, floorY, out pathStart, out pathDir, out pathLen, out halfWidth, out clearHeight);
            if (!inside || pathLen / K < MinInsideLen) K = K0;
        }
        if (inside && pathLen / K >= MinInsideLen)
        {
            float kFit = Mathf.Min(pathLen / TargetCourseLen, clearHeight / NeededHeadroom);
            kFit = Mathf.Clamp(kFit, K0 * 0.3f, K0 * 4f);
            if (Mathf.Abs(kFit - K) / K > 0.1f)
            {
                float oldK = K;
                K = kFit;
                bool ok2 = FindPath(rb, floorY, out Vector3 s2, out Vector3 d2, out float l2, out float hw2, out float ch2);
                if (ok2 && l2 / K >= MinInsideLen)
                {
                    pathStart = s2; pathDir = d2; pathLen = l2; halfWidth = hw2; clearHeight = ch2;
                }
                else K = oldK;
            }
        }
        string mode;
        if (!inside || pathLen / K < MinInsideLen)
        {
            inside = false;
            mode = "outside the building (night exterior)";
            bool alongX = rb.size.x >= rb.size.z;
            pathDir = alongX ? Vector3.right : Vector3.forward;
            Vector3 lateral = alongX ? Vector3.forward : Vector3.right;
            float lateralOffset = (alongX ? rb.extents.z : rb.extents.x) + 7f * K;
            pathLen = Mathf.Clamp(alongX ? rb.size.x : rb.size.z, 40f * K, 70f * K);
            Vector3 centre = new Vector3(rb.center.x, floorY, rb.center.z) + lateral * lateralOffset;
            pathStart = centre - pathDir * (pathLen * 0.5f);
            halfWidth = 5f * K;
            clearHeight = 30f * K;
        }
        else
        {
            mode = "inside the map";
            pathLen = Mathf.Min(pathLen, MaxCourseLen * K);
        }

        Physics.queriesHitBackfaces = oldBack;

        pathLen /= K;
        halfWidth /= K;
        clearHeight /= K;
        float safeStart = Mathf.Min(SafeStart, pathLen * 0.15f);
        float safeEnd = Mathf.Min(SafeEnd, pathLen * 0.3f);

        GameObject course = new GameObject("Course");
        course.transform.position = pathStart;
        course.transform.rotation = Quaternion.LookRotation(pathDir, Vector3.up);
        course.transform.localScale = Vector3.one * K;
        Transform C = course.transform;

        Material cursedMat = MakeTransparentMat("HauntedMist", new Color(0.45f, 0.12f, 0.7f, 0.35f));
        Material groundMat = MakeMat("StoneGround", new Color(0.12f, 0.12f, 0.14f), false);
        Material barrierMat = MakeMat("GhostBarrier", new Color(0.9f, 0.1f, 0.15f), true);
        Material portalMat = MakeMat("ExitPortal", new Color(0.2f, 1f, 0.6f), true);
        Material platformMat = MakeMat("PlatformFallback", new Color(0.35f, 0.33f, 0.3f), false);

        if (!inside)
        {
            MakeBlock(C, "Start Ground", new Vector3(0f, -0.25f, safeStart * 0.5f), new Vector3(10f, 0.5f, safeStart + 1f), groundMat, true);
            MakeBlock(C, "End Ground", new Vector3(0f, -0.25f, pathLen - safeEnd * 0.5f), new Vector3(10f, 0.5f, safeEnd + 1f), groundMat, true);
        }

        float killStart = safeStart, killEnd = pathLen - safeEnd;
        float width = Mathf.Clamp(halfWidth * 2f, 3f, 14f);

        GameObject kill = new GameObject("Haunted Mist (KillZone)");
        kill.transform.SetParent(C, false);
        kill.transform.localPosition = new Vector3(0f, 0f, (killStart + killEnd) * 0.5f);
        BoxCollider kc = kill.AddComponent<BoxCollider>();
        kc.isTrigger = true;
        kc.size = new Vector3(inside ? width : 60f, inside ? 0.9f : 4f, killEnd - killStart);
        kc.center = new Vector3(0f, inside ? 0f : -4f, 0f);
        KillZone kz = kill.AddComponent<KillZone>();
        kz.message = "THE MIST TOOK YOU!";
        if (inside)
        {
            MakeBlock(kill.transform, "Haunted Mist", new Vector3(0f, 0.15f, 0f),
                      new Vector3(width, 0.3f, killEnd - killStart), cursedMat, false);
        }

        float middle = killEnd - killStart;
        int n = Mathf.Clamp(Mathf.FloorToInt(middle / 4.2f), 4, 60);
        float spacing = middle / (n + 1);
        while (spacing > 4.6f && n < 60) { n++; spacing = middle / (n + 1); }
        Vector3 pSize = PlatformSize;
        if (spacing < 3.2f)
        {
            float side = Mathf.Clamp(spacing * 0.7f, 1.4f, 2f);
            pSize = new Vector3(side, 0.35f, side);
        }

        float maxTop = Mathf.Clamp(clearHeight - PlayerHeight - JumpHeight - 0.3f, 0.6f, 2.6f);
        string[] debrisNames = { "ceiling", "ceiling2", "ceiling3", "ceiling holder" };
        PType[] pattern = Patterns[(def.number - 1) % Patterns.Length];
        MatrixPlatform firstMoving = null;
        float lastTop = 0f;

        for (int i = 0; i < n; i++)
        {
            PType type = pattern[i % pattern.Length];
            float h = Mathf.Min(Heights[i % Heights.Length], maxTop);
            if (h - lastTop > JumpHeight - 0.5f) h = lastTop + JumpHeight - 0.5f;
            lastTop = h;
            float z = killStart + spacing * (i + 1);

            GameObject debris = AssetDatabase.LoadAssetAtPath<GameObject>(DebrisFolder + debrisNames[i % debrisNames.Length] + ".prefab");
            MatrixPlatform mp = MakePlatform(C, "Platform_" + (i + 1).ToString("00") + "_" + type,
                                             new Vector3(0f, h, z), pSize, debris, platformMat);
            mp.timeOffset = i * 0.37f;
            if (i > 0 && i % 8 == 0 && type == PType.Static)
            {
                GameObject cp = new GameObject("Checkpoint");
                cp.transform.SetParent(mp.transform, false);
                cp.transform.localPosition = new Vector3(0f, 0.05f, 0f);
                BoxCollider cpc = cp.AddComponent<BoxCollider>();
                cpc.isTrigger = true;
                cpc.size = new Vector3(pSize.x, 2f, pSize.z);
                cpc.center = new Vector3(0f, 1f, 0f);
                cp.AddComponent<Checkpoint>();
            }
            float lat = Mathf.Min(1.6f, width * 0.5f - pSize.x * 0.5f);

            switch (type)
            {
                case PType.Static:
                    mp.moveMode = MatrixPlatform.MoveMode.None;
                    break;
                case PType.Lateral:
                    mp.transform.localPosition = new Vector3(-lat, h, z);
                    mp.moveMode = MatrixPlatform.MoveMode.PingPong;
                    mp.moveOffset = new Vector3(lat * 2f, 0f, 0f);
                    mp.moveSpeed = 0.2f;
                    break;
                case PType.Vertical:
                    float baseH = Mathf.Max(0.6f, h - 0.6f);
                    mp.transform.localPosition = new Vector3(0f, baseH, z);
                    mp.moveMode = MatrixPlatform.MoveMode.PingPong;
                    mp.moveOffset = new Vector3(0f, Mathf.Min(1.2f, maxTop + 0.4f - baseH), 0f);
                    mp.moveSpeed = 0.2f;
                    break;
                case PType.Spin:
                    mp.moveMode = MatrixPlatform.MoveMode.None;
                    mp.spinDegreesPerSecond = new Vector3(0f, 35f, 0f);
                    break;
                case PType.Pulse:
                    mp.moveMode = MatrixPlatform.MoveMode.None;
                    mp.pulseScale = true;
                    mp.pulseAmount = 0.25f;
                    mp.pulseSpeed = 0.35f;
                    break;
                case PType.Orbit:
                    mp.moveMode = MatrixPlatform.MoveMode.Orbit;
                    mp.orbitRadius = Mathf.Min(1f, lat);
                    mp.orbitDegreesPerSecond = 30f;
                    break;
            }
            if (firstMoving == null && (type == PType.Lateral || type == PType.Orbit || type == PType.Vertical)) firstMoving = mp;
        }

        float exitZ = pathLen - 1.2f;
        GameObject exitRoot = new GameObject("Exit");
        exitRoot.transform.SetParent(C, false);
        exitRoot.transform.localPosition = new Vector3(0f, 0f, exitZ);

        GameObject barrier = MakeBlock(exitRoot.transform, "Ghost Barrier (locked)", new Vector3(0f, 1.4f, 0f), new Vector3(2.2f, 2.8f, 0.15f), barrierMat, false);
        AddPointLight(barrier.transform, Vector3.zero, new Color(1f, 0.15f, 0.2f), 3f, 5f, "Barrier Light");

        GameObject portal = new GameObject("Exit Portal (unlocked)");
        portal.transform.SetParent(exitRoot.transform, false);
        MakeBlock(portal.transform, "Portal Glow", new Vector3(0f, 1.4f, 0f), new Vector3(2.2f, 2.8f, 0.1f), portalMat, false);
        AddPointLight(portal.transform, new Vector3(0f, 1.5f, -0.5f), new Color(0.3f, 1f, 0.6f), 4f, 6f, "Portal Light");

        GameObject exitTrigger = new GameObject("Exit Trigger");
        exitTrigger.transform.SetParent(exitRoot.transform, false);
        exitTrigger.transform.localPosition = new Vector3(0f, 1.4f, -0.6f);
        BoxCollider et = exitTrigger.AddComponent<BoxCollider>();
        et.isTrigger = true;
        et.size = new Vector3(2.6f, 3f, 1.6f);
        exitTrigger.AddComponent<LevelExit>();

        GameObject clock = AssetDatabase.LoadAssetAtPath<GameObject>(DebrisFolder + "clock.prefab");
        if (clock != null)
        {
            GameObject cl = PlaceDecor(exitRoot.transform, clock, new Vector3(0f, Mathf.Min(3.4f, clearHeight - 0.6f), 0.2f), 0.9f);
            if (cl != null) cl.name = "Haunted Clock";
        }

        GameObject cab = AssetDatabase.LoadAssetAtPath<GameObject>(DebrisFolder + "book cab.prefab");
        if (cab != null)
        {
            float cx = Mathf.Max(1.5f, width * 0.5f - 0.6f);
            GameObject c1 = PlaceDecor(C, cab, new Vector3(-cx, 0f, 1.5f), 2.2f, true);
            GameObject c2 = PlaceDecor(C, cab, new Vector3(cx, 0f, 3.2f), 2.2f, true);
            foreach (GameObject c in new[] { c1, c2 })
            {
                if (c == null) continue;
                AddMeshColliders a = c.AddComponent<AddMeshColliders>();
                a.addOnAwake = false;
                a.AddNow();
            }
        }

        GameObject lightsPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DebrisFolder + "lights.prefab");
        GameObject lightsRoot = new GameObject("Haunted Lights");
        lightsRoot.transform.SetParent(C, false);
        float lampY = inside ? Mathf.Clamp(clearHeight - 0.5f, 2.6f, 8f) : 4.5f;
        float lampRange = Mathf.Max(9f, lampY * 1.6f);
        int li = 0;
        for (float z = 2f; z < pathLen; z += 7f, li++)
        {
            GameObject lamp = new GameObject("Lamp_" + li.ToString("00"));
            lamp.transform.SetParent(lightsRoot.transform, false);
            lamp.transform.localPosition = new Vector3(0f, lampY, z);
            List<Renderer> fixture = new List<Renderer>();
            if (lightsPrefab != null)
            {
                GameObject f = PlaceDecor(lamp.transform, lightsPrefab, new Vector3(0f, 0.05f, 0f), 1.4f);
                if (f != null) fixture.AddRange(f.GetComponentsInChildren<Renderer>());
            }
            Light l = AddPointLight(lamp.transform, new Vector3(0f, -0.3f, 0f), new Color(0.78f, 0.88f, 1f), 4f, lampRange, "Point Light");
            l.shadows = li % 2 == 0 ? LightShadows.Soft : LightShadows.None;
            if (li % 3 != 1)
            {
                FlickerLight fl = l.gameObject.AddComponent<FlickerLight>();
                fl.emissiveRenderers = fixture.ToArray();
                fl.minTimeBetweenDropouts = 1.5f + (li % 4) * 0.5f;
                fl.scareRadius = 5f * K;
            }
        }

        GameObject spawn = new GameObject("SpawnPoint");
        spawn.transform.SetParent(C, false);
        spawn.transform.localPosition = new Vector3(0f, 0.1f, 2.2f);
        spawn.transform.localRotation = Quaternion.identity;

        GameObject player = BuildPlayer(spawn.transform.position, spawn.transform.rotation);

        float ez = pathLen - safeEnd * 0.5f - 0.5f;
        float ex = Mathf.Clamp(width * 0.5f - 1f, 1.2f, 3.2f);
        GameObject wps = new GameObject("Ghost Waypoints");
        wps.transform.SetParent(C, false);
        Transform wA = new GameObject("Waypoint A").transform; wA.SetParent(wps.transform, false); wA.localPosition = new Vector3(-ex, 0f, ez);
        Transform wB = new GameObject("Waypoint B").transform; wB.SetParent(wps.transform, false); wB.localPosition = new Vector3(ex, 0f, ez + 1.2f);
        Transform wC = new GameObject("Waypoint C").transform; wC.SetParent(wps.transform, false); wC.localPosition = new Vector3(0f, 0f, pathLen - 2.6f);
        GameObject ghost = BuildGhost(C.TransformPoint(new Vector3(0f, 0f, ez)), new[] { wA, wB, wC });
        ghost.GetComponent<EnemyGhost>().patrolSpeed = (1.4f + 0.2f * def.number) * K;

        GameObject camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        Camera cam = camGo.AddComponent<Camera>();
        cam.nearClipPlane = 0.05f * K;
        cam.farClipPlane = 600f * K;
        camGo.AddComponent<AudioListener>();
        camGo.transform.position = player.transform.position - pathDir * 5f * K + Vector3.up * 2.5f * K;
        camGo.transform.LookAt(player.transform.position + Vector3.up * 1.5f * K);
        ThirdPersonCamera tpc = camGo.AddComponent<ThirdPersonCamera>();
        tpc.target = player.transform;
        tpc.distance = (inside ? Mathf.Clamp(clearHeight * 0.75f, 3.5f, 6f) : 6f) * K;
        tpc.pivotHeight = 1.5f * K;
        tpc.collisionRadius = 0.25f * K;
        player.GetComponent<PlayerController>().cameraTransform = camGo.transform;

        GameObject moon = new GameObject("Moonlight");
        Light ml = moon.AddComponent<Light>();
        ml.type = LightType.Directional;
        ml.shadows = LightShadows.Soft;

        GameObject vol = new GameObject("Global Volume");
        Volume v = vol.AddComponent<Volume>();
        v.isGlobal = true;

        GameObject gm = new GameObject("GameManager");
        LevelManager lm = gm.AddComponent<LevelManager>();
        lm.levelNumber = def.number;
        lm.levelName = def.title;
        lm.objective = def.objective;
        lm.spawnPoint = spawn.transform;
        lm.killY = floorY - 8f * K;
        lm.requireAllEnemiesDefeated = true;
        lm.hideWhenUnlocked = new[] { barrier };
        lm.showWhenUnlocked = new[] { portal };
        lm.nextSceneName = def.next;
        lm.isFinalLevel = def.final;
        lm.mainMenuSceneName = "MainMenu";

        GameHUD hud = gm.AddComponent<GameHUD>();
        hud.player = player.GetComponent<PlayerController>();
        hud.platformToDisplay = firstMoving;

        HauntedAtmosphere atm = gm.AddComponent<HauntedAtmosphere>();
        atm.moonLight = ml;
        atm.globalVolume = v;
        atm.autoAddFlickerToLights = false;
        atm.fogDensity = (inside ? (def.number == 3 ? 0.03f : 0.045f) : 0.02f) / K;
        if (!inside) atm.moonIntensity = 0.5f;

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath(def.scene));

        return def.scene + ": course " + pathLen.ToString("F0") + " m built " + mode + ", " + n +
               " platforms (map kept as-is, gameplay scale x" + K.ToString("0.##") + ")";
    }

    static GameObject BuildPlayer(Vector3 pos, Quaternion rot)
    {
        GameObject player = new GameObject("Player");
        player.tag = "Player";
        player.transform.SetPositionAndRotation(pos, rot);

        CharacterController cc = player.AddComponent<CharacterController>();
        cc.height = PlayerHeight * K;
        cc.radius = 0.35f * K;
        cc.center = new Vector3(0f, PlayerHeight * 0.5f * K, 0f);
        cc.stepOffset = 0.35f * K;
        cc.slopeLimit = 50f;
        cc.skinWidth = 0.04f * K;

        PlayerController pc = player.AddComponent<PlayerController>();
        pc.moveSpeed = 5f * K;
        pc.gravity = -20f * K;
        pc.jumpHeight = JumpHeight * K;
        pc.terminalVelocity = -40f * K;
        pc.groundCheckDistance = 0.15f * K;
        PlayerBackstab pb = player.AddComponent<PlayerBackstab>();
        pb.attackRange = 2f * K;
        AudioSource src = player.AddComponent<AudioSource>();
        src.playOnAwake = false;
        player.GetComponent<PlayerBackstab>().audioSource = src;

        AttachCharacterModel(player.transform, "Model");
        return player;
    }

    static GameObject BuildGhost(Vector3 pos, Transform[] waypoints)
    {
        GameObject ghost = new GameObject("Ghost (Enemy)");
        ghost.transform.position = pos;
        Vector3 toA = waypoints[0].position - pos; toA.y = 0f;
        if (toA.sqrMagnitude > 0.01f) ghost.transform.rotation = Quaternion.LookRotation(toA.normalized);

        Transform model = AttachCharacterModel(ghost.transform, "Model");
        EnemyGhost eg = ghost.AddComponent<EnemyGhost>();
        eg.model = model;
        eg.waypoints = waypoints;
        eg.tint = new Color(0.55f, 0.8f, 1f);
        eg.viewDistance = 6.5f * K;
        eg.viewAngle = 100f;
        eg.chaseSpeed = 2.8f * K;
        eg.catchDistance = 1.2f * K;
        eg.eyeHeight = 1.5f * K;
        eg.bobHeight = 0.12f * K;

        AddPointLight(ghost.transform, new Vector3(0f, 1.2f * K, 0f), new Color(0.4f, 0.7f, 1f), 2.5f, 4f, "Ghost Glow");
        return ghost;
    }

    static Transform AttachCharacterModel(Transform parent, string name)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterPrefab);
        GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        model.name = name;
        model.transform.localPosition = Vector3.zero;
        foreach (Collider c in model.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);

        CenterModelOnParent cm = model.AddComponent<CenterModelOnParent>();
        cm.fitHeight = true;
        cm.targetHeight = PlayerHeight * K;
        cm.yawCorrection = 0f;
        cm.CenterNow();

        float yaw = DetectFacingYaw(model, parent);
        if (Mathf.Abs(yaw) > 1f)
        {
            cm.yawCorrection = yaw;
            cm.CenterNow();
        }
        cm.centerOnAwake = false;
        return model.transform;
    }

    static float MeasureCharacterScale()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterPrefab);
        GameObject tmp = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        float h = 0f;
        bool has = false;
        Bounds b = new Bounds();
        foreach (Renderer r in tmp.GetComponentsInChildren<Renderer>())
        {
            if (!has) { b = r.bounds; has = true; } else b.Encapsulate(r.bounds);
        }
        if (has) h = b.size.y;
        Object.DestroyImmediate(tmp);
        if (h < 1e-4f) return 1f;
        return Mathf.Clamp(h / PlayerHeight, 0.01f, 1000f);
    }

    static float DetectFacingYaw(GameObject model, Transform space)
    {
        List<Vector3> pts = new List<Vector3>();
        foreach (MeshFilter mf in model.GetComponentsInChildren<MeshFilter>())
        {
            Mesh m = mf.sharedMesh;
            if (m == null) continue;
            Vector3[] verts;
            try { verts = m.vertices; } catch { continue; }
            if (verts == null || verts.Length == 0) continue;
            int step = Mathf.Max(1, verts.Length / 5000);
            for (int i = 0; i < verts.Length; i += step)
                pts.Add(space.InverseTransformPoint(mf.transform.TransformPoint(verts[i])));
        }
        if (pts.Count < 50) return 0f;

        float minY = float.MaxValue, maxY = float.MinValue;
        Vector3 all = Vector3.zero;
        foreach (Vector3 p in pts) { minY = Mathf.Min(minY, p.y); maxY = Mathf.Max(maxY, p.y); all += p; }
        all /= pts.Count;
        float cut = minY + (maxY - minY) * 0.06f;
        Vector3 feet = Vector3.zero; int fc = 0;
        foreach (Vector3 p in pts) if (p.y <= cut) { feet += p; fc++; }
        if (fc < 5) return 0f;
        feet /= fc;

        Vector3 dir = feet - all; dir.y = 0f;
        if (dir.magnitude < (maxY - minY) * 0.01f) return 0f;
        float toesYaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
        float correction = Mathf.Round(-toesYaw / 90f) * 90f;
        return Mathf.DeltaAngle(0f, correction);
    }

    static Bounds RobustBounds(GameObject map)
    {
        Renderer[] rs = map.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return new Bounds(map.transform.position, Vector3.one * 10f);
        float[][] c = { new float[rs.Length], new float[rs.Length], new float[rs.Length] };
        for (int i = 0; i < rs.Length; i++)
        {
            Vector3 cc = rs[i].bounds.center;
            c[0][i] = cc.x; c[1][i] = cc.y; c[2][i] = cc.z;
        }
        Vector3 lo, hi;
        lo.x = Percentile(c[0], 0.03f); hi.x = Percentile(c[0], 0.97f);
        lo.y = Percentile(c[1], 0.03f); hi.y = Percentile(c[1], 0.97f);
        lo.z = Percentile(c[2], 0.03f); hi.z = Percentile(c[2], 0.97f);
        Vector3 span = hi - lo;
        Vector3 pad = span * 0.3f + Vector3.one * 0.01f;
        lo -= pad; hi += pad;

        bool has = false;
        Bounds b = new Bounds();
        foreach (Renderer r in rs)
        {
            Bounds rb = r.bounds;
            Vector3 p = rb.center;
            if (p.x < lo.x || p.y < lo.y || p.z < lo.z || p.x > hi.x || p.y > hi.y || p.z > hi.z) continue;
            if (rb.size.x > span.x * 3f + 1f && rb.size.z > span.z * 3f + 1f) continue;
            if (!has) { b = rb; has = true; } else b.Encapsulate(rb);
        }
        if (!has) { b = rs[0].bounds; foreach (Renderer r in rs) b.Encapsulate(r.bounds); }
        return b;
    }

    static float Percentile(float[] a, float p)
    {
        float[] s = (float[])a.Clone();
        System.Array.Sort(s);
        int i = Mathf.Clamp(Mathf.RoundToInt(p * (s.Length - 1)), 0, s.Length - 1);
        return s[i];
    }

    static bool MeasureInterior(Bounds b, out float floorY, out float gap)
    {
        List<float> floors = new List<float>(), gaps = new List<float>();
        const int N = 18;
        for (int ix = 1; ix < N; ix++)
        for (int iz = 1; iz < N; iz++)
        {
            float x = Mathf.Lerp(b.min.x, b.max.x, ix / (float)N);
            float z = Mathf.Lerp(b.min.z, b.max.z, iz / (float)N);
            if (ProbeColumn(b, x, z, out float f, out float g)) { floors.Add(f); gaps.Add(g); }
        }
        floorY = b.min.y; gap = 0f;
        if (floors.Count < 3) return false;

        float bin = Mathf.Max(b.size.y / 40f, 1e-4f);
        Dictionary<int, int> hist = new Dictionary<int, int>();
        int best = 0, bestCount = 0;
        foreach (float f in floors)
        {
            int k = Mathf.FloorToInt(f / bin);
            hist.TryGetValue(k, out int cnt); cnt++; hist[k] = cnt;
            if (cnt > bestCount) { bestCount = cnt; best = k; }
        }
        List<float> fSel = new List<float>(), gSel = new List<float>();
        for (int i = 0; i < floors.Count; i++)
            if (Mathf.Abs(Mathf.FloorToInt(floors[i] / bin) - best) <= 1) { fSel.Add(floors[i]); gSel.Add(gaps[i]); }
        floorY = Percentile(fSel.ToArray(), 0.5f);
        gap = Percentile(gSel.ToArray(), 0.6f);
        return gap > 0f;
    }

    static bool ProbeColumn(Bounds b, float x, float z, out float floorY, out float gap)
    {
        floorY = 0f; gap = 0f;
        float top = b.max.y + 1f;
        float maxGap = b.size.y * 0.95f;
        float minGap = b.size.y * 0.02f;
        List<RaycastHit> hits = RaycastThroughAll(new Vector3(x, top, z), Vector3.down, b.size.y + 2f);
        if (hits.Count == 0) return false;

        bool found = false;
        float eps = Mathf.Max(b.size.y * 0.002f, 0.001f);
        foreach (RaycastHit h in hits)
        {
            if (Mathf.Abs(h.normal.y) < 0.7f) continue;
            Vector3 o = h.point + Vector3.up * eps;
            if (!Physics.Raycast(o, Vector3.up, out RaycastHit up, maxGap, ~0, QueryTriggerInteraction.Ignore)) continue;
            float g = up.point.y - h.point.y;
            if (g < minGap || g > maxGap) continue;
            if (g > gap) { gap = g; floorY = h.point.y; found = true; }
        }
        return found;
    }

    static List<RaycastHit> RaycastThroughAll(Vector3 origin, Vector3 dir, float maxDistance)
    {
        List<RaycastHit> list = new List<RaycastHit>();
        float eps = Mathf.Max(maxDistance * 0.00002f, 0.0005f);
        float travelled = 0f;
        Vector3 o = origin;
        for (int i = 0; i < 256 && travelled < maxDistance; i++)
        {
            if (!Physics.Raycast(o, dir, out RaycastHit h, maxDistance - travelled, ~0, QueryTriggerInteraction.Ignore)) break;
            list.Add(h);
            float step = h.distance + eps;
            travelled += step;
            o += dir * step;
        }
        return list;
    }

    static bool FindPath(Bounds b, float floorY, out Vector3 start, out Vector3 dir, out float length,
                         out float halfWidth, out float clearHeight)
    {
        start = Vector3.zero; dir = Vector3.forward; length = 0f; halfWidth = 2f * K; clearHeight = 3f * K;

        List<Vector2> pts = new List<Vector2>();
        const int N = 40;
        for (int ix = 0; ix <= N; ix++)
        for (int iz = 0; iz <= N; iz++)
        {
            float x = Mathf.Lerp(b.min.x, b.max.x, ix / (float)N);
            float z = Mathf.Lerp(b.min.z, b.max.z, iz / (float)N);
            if (Walkable(x, z, floorY, out _)) pts.Add(new Vector2(x, z));
        }
        if (pts.Count < 10) return false;

        Vector2 mean = Vector2.zero;
        foreach (Vector2 p in pts) mean += p;
        mean /= pts.Count;
        float sxx = 0, sxz = 0, szz = 0;
        foreach (Vector2 p in pts) { Vector2 d = p - mean; sxx += d.x * d.x; sxz += d.x * d.y; szz += d.y * d.y; }
        float angle = 0.5f * Mathf.Atan2(2f * sxz, sxx - szz);
        Vector2 major = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        Vector2 minor = new Vector2(-major.y, major.x);

        float minA = float.MaxValue, maxA = float.MinValue, minB = float.MaxValue, maxB = float.MinValue;
        foreach (Vector2 p in pts)
        {
            float a = Vector2.Dot(p - mean, major), c = Vector2.Dot(p - mean, minor);
            minA = Mathf.Min(minA, a); maxA = Mathf.Max(maxA, a);
            minB = Mathf.Min(minB, c); maxB = Mathf.Max(maxB, c);
        }

        float bestLen = 0f; Vector2 bestStart = Vector2.zero, bestDir = major;
        foreach (bool swap in new[] { false, true })
        {
            Vector2 ax = swap ? minor : major, side = swap ? major : minor;
            float a0 = swap ? minB : minA, a1 = swap ? maxB : maxA;
            float s0 = swap ? minA : minB, s1 = swap ? maxA : maxB;
            int lines = Mathf.Clamp(Mathf.CeilToInt((s1 - s0) / K), 1, 30);
            float step = 0.5f * K;
            for (int li = 0; li <= lines; li++)
            {
                float s = Mathf.Lerp(s0, s1, li / (float)lines);
                float runStart = 0f, run = 0f;
                for (float a = a0; a <= a1; a += step)
                {
                    Vector2 p = mean + ax * a + side * s;
                    if (Clear(p.x, p.y, floorY))
                    {
                        if (run == 0f) runStart = a;
                        run += step;

                        float score = run - Mathf.Abs(s - (s0 + s1) * 0.5f) * 0.05f;
                        if (score > bestLen) { bestLen = score; bestStart = mean + ax * runStart + side * s; bestDir = ax; }
                    }
                    else run = 0f;
                }
            }
        }
        if (bestLen < 4f * K) return false;

        length = bestLen;
        start = new Vector3(bestStart.x, floorY, bestStart.y);
        dir = new Vector3(bestDir.x, 0f, bestDir.y).normalized;

        Vector3 mid = start + dir * (length * 0.5f);
        Vector3 right = Vector3.Cross(Vector3.up, dir);
        float wl = 0f, wr = 0f;
        for (float w = 0.5f * K; w < 8f * K; w += 0.5f * K) { Vector3 p = mid + right * w; if (!Clear(p.x, p.z, floorY)) break; wr = w; }
        for (float w = 0.5f * K; w < 8f * K; w += 0.5f * K) { Vector3 p = mid - right * w; if (!Clear(p.x, p.z, floorY)) break; wl = w; }
        halfWidth = Mathf.Max(1.5f * K, Mathf.Min(wl, wr) + 0.5f * K);

        List<float> hs = new List<float>();
        for (float t = 0f; t <= length; t += 2f * K)
        {
            Vector3 p = start + dir * t;
            if (Walkable(p.x, p.z, floorY, out float h)) hs.Add(h);
        }
        clearHeight = hs.Count > 0 ? Percentile(hs.ToArray(), 0.2f) : 3f * K;
        return true;
    }

    static bool Walkable(float x, float z, float floorY, out float height)
    {
        height = 0f;
        Vector3 o = new Vector3(x, floorY + 1.0f * K, z);
        if (!Physics.Raycast(o, Vector3.down, out RaycastHit down, 1.6f * K, ~0, QueryTriggerInteraction.Ignore)) return false;
        if (Mathf.Abs(down.point.y - floorY) > 0.4f * K || Mathf.Abs(down.normal.y) < 0.7f) return false;
        if (Physics.Raycast(down.point + Vector3.up * 0.05f * K, Vector3.up, out RaycastHit up, 40f * K, ~0, QueryTriggerInteraction.Ignore))
            height = up.point.y - down.point.y;
        else height = 40f * K;
        return height >= 2.6f * K;
    }

    static bool Clear(float x, float z, float floorY)
    {
        if (!Walkable(x, z, floorY, out float h)) return false;
        Vector3 bottom = new Vector3(x, floorY + (0.45f + 0.9f) * K, z);
        Vector3 topP = new Vector3(x, floorY + Mathf.Min(h - 0.2f * K, 3.2f * K) - 0.9f * K, z);
        if (topP.y < bottom.y) topP = bottom;
        return !Physics.CheckCapsule(bottom, topP, 0.9f * K, ~0, QueryTriggerInteraction.Ignore);
    }

    static MatrixPlatform MakePlatform(Transform parent, string name, Vector3 localPos, Vector3 size, GameObject debris, Material fallback)
    {
        GameObject root = new GameObject(name);
        root.transform.SetParent(parent, false);
        root.transform.localPosition = localPos;
        root.transform.localRotation = Quaternion.identity;

        BoxCollider bc = root.AddComponent<BoxCollider>();
        bc.size = size;
        bc.center = new Vector3(0f, -size.y * 0.5f, 0f);

        GameObject vis = new GameObject("Visual");
        vis.transform.SetParent(root.transform, false);

        bool fitted = false;
        if (debris != null)
        {
            GameObject inst = (GameObject)PrefabUtility.InstantiatePrefab(debris, vis.transform);
            foreach (Collider c in inst.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
            fitted = FitInto(root.transform, vis.transform, inst, size);
            if (!fitted) Object.DestroyImmediate(inst);
        }
        if (!fitted)
        {
            GameObject cube = MakeBlock(vis.transform, "Block", new Vector3(0f, -size.y * 0.5f, 0f), size, fallback, false);
            cube.name = "Block";
        }

        return root.AddComponent<MatrixPlatform>();
    }

    static bool FitInto(Transform root, Transform container, GameObject inst, Vector3 size)
    {
        if (!LocalBounds(root, inst, out Bounds b)) return false;
        Vector3 s = new Vector3(
            b.size.x > 1e-5f ? size.x / b.size.x : 1f,
            b.size.y > 1e-5f ? size.y / b.size.y : 1f,
            b.size.z > 1e-5f ? size.z / b.size.z : 1f);

        container.localScale = s;
        if (!LocalBounds(root, inst, out b)) return false;
        Vector3 target = new Vector3(0f, -size.y * 0.5f, 0f);
        container.localPosition += target - b.center;
        return true;
    }

    static GameObject PlaceDecor(Transform parent, GameObject prefab, Vector3 localPos, float maxSize, bool onFloor = false)
    {
        GameObject holder = new GameObject(prefab.name);
        holder.transform.SetParent(parent, false);
        holder.transform.localPosition = localPos;
        GameObject inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, holder.transform);
        foreach (Collider c in inst.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
        if (!LocalBounds(holder.transform, inst, out Bounds b)) return holder;
        float biggest = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
        if (biggest > 1e-5f) inst.transform.localScale *= maxSize / biggest;
        LocalBounds(holder.transform, inst, out b);
        Vector3 target = onFloor ? new Vector3(0f, b.extents.y, 0f) : Vector3.zero;
        inst.transform.localPosition += target - b.center;
        return holder;
    }

    static bool LocalBounds(Transform space, GameObject go, out Bounds b)
    {
        b = new Bounds();
        bool has = false;
        foreach (MeshFilter mf in go.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf.sharedMesh == null) continue;
            Bounds mb = mf.sharedMesh.bounds;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = mb.center + Vector3.Scale(mb.extents,
                    new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                Vector3 p = space.InverseTransformPoint(mf.transform.TransformPoint(corner));
                if (!has) { b = new Bounds(p, Vector3.zero); has = true; } else b.Encapsulate(p);
            }
        }
        return has;
    }

    static GameObject MakeBlock(Transform parent, string name, Vector3 localPos, Vector3 size, Material mat, bool keepCollider)
    {
        GameObject g = GameObject.CreatePrimitive(PrimitiveType.Cube);
        g.name = name;
        g.transform.SetParent(parent, false);
        g.transform.localPosition = localPos;
        g.transform.localScale = size;
        if (!keepCollider) Object.DestroyImmediate(g.GetComponent<Collider>());
        if (mat != null) g.GetComponent<Renderer>().sharedMaterial = mat;
        return g;
    }

    static Light AddPointLight(Transform parent, Vector3 localPos, Color color, float intensity, float range, string name)
    {
        GameObject g = new GameObject(name);
        g.transform.SetParent(parent, false);
        g.transform.localPosition = localPos;
        Light l = g.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = color;
        l.intensity = intensity * K * K;
        l.range = range * K;
        l.shadows = LightShadows.None;
        return l;
    }

    static Material MakeTransparentMat(string name, Color color)
    {
        Material m = MakeMat(name, color, true);
        if (m.HasProperty("_Surface"))
        {
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
        }
        EditorUtility.SetDirty(m);
        return m;
    }

    static Material MakeMat(string name, Color color, bool unlit)
    {
        string path = MatFolder + "/" + name + ".mat";
        Shader sh = Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit");
        if (sh == null) sh = Shader.Find(unlit ? "Unlit/Color" : "Standard");
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(sh);
            AssetDatabase.CreateAsset(m, path);
        }
        else m.shader = sh;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
        if (m.HasProperty("_Color")) m.SetColor("_Color", color);
        EditorUtility.SetDirty(m);
        return m;
    }

    static bool CheckAssets()
    {
        string[] needed = { HallwayPrefab, RoomsPrefab, AuditPrefab, CharacterPrefab };
        List<string> missing = new List<string>();
        foreach (string p in needed) if (AssetDatabase.LoadAssetAtPath<GameObject>(p) == null) missing.Add(p);
        if (missing.Count == 0) return true;
        EditorUtility.DisplayDialog("Haunted Mapua", "These prefabs are missing:\n\n" + string.Join("\n", missing) +
            "\n\nPut them back in those folders (or edit the paths at the top of HauntedMapuaBuilder.cs).", "OK");
        return false;
    }

    static void PrepareCharacterImport()
    {
        ModelImporter mi = AssetImporter.GetAtPath(CharacterModel) as ModelImporter;
        if (mi != null && !mi.isReadable)
        {
            mi.isReadable = true;
            mi.SaveAndReimport();
        }
    }

    static void WarnIfNoTMP()
    {
        if (Resources.Load("TMP Settings") != null) return;
        EditorUtility.DisplayDialog("Haunted Mapua",
            "TextMeshPro Essentials are not imported yet, so the HUD/menu text won't show.\n\n" +
            "After this build finishes: Window > TextMeshPro > Import TMP Essential Resources.", "OK");
    }

    static void SetBuildSettings()
    {
        List<EditorBuildSettingsScene> list = new List<EditorBuildSettingsScene>();
        list.Add(new EditorBuildSettingsScene(ScenePath("MainMenu"), true));
        foreach (LevelDef l in Levels)
            if (File.Exists(ScenePath(l.scene))) list.Add(new EditorBuildSettingsScene(ScenePath(l.scene), true));
        EditorBuildSettings.scenes = list.ToArray();
    }

    static string ScenePath(string name) { return ScenesFolder + "/" + name + ".unity"; }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
