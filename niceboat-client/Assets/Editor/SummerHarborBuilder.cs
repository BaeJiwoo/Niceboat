using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using NiceBoat;
using Object = UnityEngine.Object;

public static partial class SummerHarborBuilder
{
    public const string ScenePath = "Assets/Scenes/SummerHarbor.unity";
    public const string AssetRoot = "Assets/SummerHarbor";
    static readonly Vector3 IslandCenter = new(8f, 0f, 17f);
    static Material cream, red, teal, navy, wood, orange, sand, grass, leaf, leafLight, stone;
    static Mesh rockMesh, ringMesh;

    [MenuItem("NiceBoat/Build Summer Harbor Scene")]
    public static void BuildFromMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null &&
            !EditorUtility.DisplayDialog("Rebuild Summer Harbor?", "This replaces the generated scene and its generated art assets.", "Rebuild", "Cancel")) return;
        Build();
    }

    public static void Build()
    {
        EnsureFolder(AssetRoot);
        EnsureFolder(AssetRoot + "/Materials");
        EnsureFolder(AssetRoot + "/Meshes");
        EnsureFolder(AssetRoot + "/Prefabs");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        scene.name = "SummerHarbor";
        cream = Lit("Warm ivory", Hex("D9CC99"));
        red = Lit("Vermilion", Hex("9C5137"));
        teal = Lit("Cabin turquoise", Hex("526D66"));
        navy = Lit("Deep ink", Hex("323E39"));
        wood = Lit("Honey teak", Hex("806345"));
        orange = Lit("Safety orange", Hex("BD8449"));
        sand = Lit("Coral sand", Hex("BAA367"));
        grass = Lit("Island meadow", Hex("9A984F"));
        leaf = Lit("Palm green", Hex("827B3F"));
        leafLight = Lit("Palm sunlight", Hex("BAB264"));
        leaf.SetFloat("_Cull", 0f); leafLight.SetFloat("_Cull", 0f);
        stone = Lit("Sea rocks", Hex("746B52"));
        PrepareHarborArt();
        rockMesh = SaveMesh(AngularRockMesh(), "Faceted rock");
        ringMesh = SaveMesh(Torus(0.48f, 0.11f), "Life ring");

        var world = new GameObject("Environment").transform;
        var waterMaterial = new Material(AssetDatabase.LoadAssetAtPath<Material>("Assets/Shaders/SummerOcean.mat"));
        waterMaterial.name = "Harbor water";
        ConfigureHarborWater(waterMaterial);
        waterMaterial = SaveAsset(waterMaterial, AssetRoot + "/Materials/Harbor water.mat");
        var oceanObject = MeshObject("Summer ocean", SaveMesh(Grid(192), "Ocean grid"), waterMaterial, world);
        var ocean = oceanObject.AddComponent<OceanSurface>();
        ocean.waterRenderer = oceanObject.GetComponent<Renderer>();
        ocean.RefreshSettings();
        oceanObject.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        var floor = Primitive("Sandy seabed", PrimitiveType.Cube, world, new Vector3(0f, -8f, 0f), new Vector3(270f, 1f, 270f), sand, true);

        var islandMesh = SaveMesh(IslandMesh(), "Island shore");
        var island = MeshObject("Palm island - solid shore", islandMesh, sand, world);
        island.transform.position = IslandCenter;
        island.GetComponent<MeshRenderer>().sharedMaterials = new[] { grass, sand, stone };
        var islandCollider = island.AddComponent<MeshCollider>();
        islandCollider.sharedMesh = islandMesh;
        Physics.SyncTransforms();
        foreach (var p in new[] { new Vector2(5f, 21f), new Vector2(13f, 23f), new Vector2(16f, 16f), new Vector2(3f, 15f), new Vector2(10f, 25f) })
            Palm(new Vector3(p.x, IslandHeight(islandCollider, p.x, p.y), p.y), world);
        foreach (var p in new[] { new Vector3(-3f, 0.3f, 9f), new Vector3(-4f, 0.4f, 17f), new Vector3(16.5f, 0.2f, 8f), new Vector3(19f, 0.5f, 19f) })
        {
            var rock = MeshObject("Shore rock", rockMesh, stone, world);
            rock.transform.position = p;
            rock.transform.localScale = new Vector3(1.7f, 1.3f, 1.4f);
            var collider = rock.AddComponent<MeshCollider>(); collider.sharedMesh = rockMesh; collider.convex = true;
        }
        HarborSettlement(world, islandCollider);
        BakeHarborWaterMap(waterMaterial, world, ocean.seaLevel);
        CombineEnvironmentMeshes(world);

        var effects = new GameObject("Water interaction effects").AddComponent<WaterEffects>();
        effects.ocean = ocean;
        effects.rippleMaterial = SaveAsset(new Material(Shader.Find("NiceBoat/Water/Ripple")), AssetRoot + "/Materials/Interaction ripples.mat");
        effects.sprayMaterial = SaveAsset(new Material(Shader.Find("NiceBoat/Water/Spray")), AssetRoot + "/Materials/Water spray.mat");
        var boat = Boat(ocean, effects);
        ConfigureBoatCannon(boat, effects);
        ConfigureHarborAudio(boat);
        ConfigureWaterInteraction(effects, boat.transform);
        boat.transform.SetPositionAndRotation(new Vector3(-5f, 0.3f, -9f), Quaternion.Euler(0f, 20f, 0f));
        PrefabUtility.SaveAsPrefabAsset(boat.gameObject, AssetRoot + "/Prefabs/PlayerBoat.prefab");
        var buoys = new GameObject("Navigation buoys").transform;
        Vector3[] positions = { new(-18, 0, -4), new(-8, 0, -4), new(-19, 0, 8), new(-11, 0, 14), new(-6, 0, -16), new(11, 0, -4) };
        for (int i = 0; i < positions.Length; i++) Buoy(positions[i], i, ocean, effects, buoys);

        var light = new GameObject("Summer sun").AddComponent<Light>();
        light.type = LightType.Directional;
        light.transform.rotation = Quaternion.Euler(48f, -38f, 0f);
        light.color = new Color(1f, 0.94f, 0.79f);
        light.intensity = 1.05f;
        light.shadows = LightShadows.Hard;
        light.shadowStrength = 0.72f;
        light.shadowBias = 0.08f;
        light.shadowNormalBias = 0.2f;
        light.GetUniversalAdditionalLightData().usePipelineSettings = false;
        RenderSettings.sun = light;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = Hex("A2AA94");
        RenderSettings.ambientEquatorColor = Hex("7D8772");
        RenderSettings.ambientGroundColor = Hex("56563D");
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = Hex("80897B");
        RenderSettings.fogStartDistance = 65f;
        RenderSettings.fogEndDistance = 170f;
        var sky = new Material(Shader.Find("Skybox/Procedural"));
        sky.SetColor("_SkyTint", new Color(0.47f, 0.67f, 0.77f));
        sky.SetColor("_GroundColor", new Color(0.35f, 0.69f, 0.73f));
        sky.SetFloat("_SunSize", 0.035f);
        sky.SetFloat("_Exposure", 1.1f);
        RenderSettings.skybox = SaveAsset(sky, AssetRoot + "/Materials/Summer sky.mat");
        var camera = new GameObject("Main Camera").AddComponent<Camera>();
        camera.tag = "MainCamera";
        camera.transform.position = new Vector3(15f, 35f, -32f);
        camera.transform.LookAt(new Vector3(2f, 0f, 8f));
        camera.fieldOfView = 36f;
        camera.nearClipPlane = 0.15f;
        camera.farClipPlane = 260f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = RenderSettings.fogColor;
        camera.allowHDR = true;
        camera.gameObject.AddComponent<AudioListener>();
        var cameraData = camera.GetUniversalAdditionalCameraData();
        cameraData.requiresDepthTexture = true;
        cameraData.requiresColorTexture = false;
        camera.gameObject.AddComponent<BoatFollowCamera>().target = boat.transform;
        new GameObject("Sailing HUD").AddComponent<OceanHud>().boat = boat;

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        var buildScenes = new List<EditorBuildSettingsScene> { new(ScenePath, true) };
        foreach (var existing in EditorBuildSettings.scenes)
            if (existing.path != ScenePath) buildScenes.Add(existing);
        EditorBuildSettings.scenes = buildScenes.ToArray();
        AssetDatabase.SaveAssets();
        Debug.Log("HARBOR_SCENE_CREATED: " + ScenePath);
    }

    static BoatController Boat(OceanSurface ocean, WaterEffects effects)
    {
        var root = new GameObject("Player Boat - WASD");
        var hullMesh = SaveMesh(HullMesh(), "Boat hull");
        MeshObject("Ivory hull", hullMesh, cream, root.transform);
        var stripe = MeshObject("Red gunwale", hullMesh, red, root.transform);
        stripe.transform.localScale = new Vector3(1.025f, 0.15f, 1.015f);
        stripe.transform.localPosition = new Vector3(0f, 0.43f, 0f);
        for (int i = 0; i < 8; i++)
            Primitive("Teak plank", PrimitiveType.Cube, root.transform, new Vector3(-0.63f + i * 0.18f, 0.56f, -0.25f), new Vector3(0.165f, 0.045f, 3f), wood);
        Primitive("Cabin", PrimitiveType.Cube, root.transform, new Vector3(0f, 1f, 0.1f), new Vector3(1.5f, 1f, 1.6f), cream);
        Primitive("Windshield", PrimitiveType.Cube, root.transform, new Vector3(0f, 1.16f, 0.91f), new Vector3(1.24f, 0.46f, 0.025f), navy);
        for (int side = -1; side <= 1; side += 2)
        {
            Primitive("Side window", PrimitiveType.Cube, root.transform, new Vector3(side * 0.758f, 1.16f, 0.14f), new Vector3(0.022f, 0.46f, 1.18f), navy);
            Primitive("Window mullion", PrimitiveType.Cube, root.transform, new Vector3(side * 0.78f, 1.15f, 0.2f), new Vector3(0.025f, 0.5f, 0.06f), cream);
            Primitive("Stern fender", PrimitiveType.Capsule, root.transform, new Vector3(side * 0.96f, 0.18f, -1.45f), new Vector3(0.22f, 0.32f, 0.22f), navy);
        }
        Primitive("Turquoise roof", PrimitiveType.Cube, root.transform, new Vector3(0f, 1.56f, 0.1f), new Vector3(1.8f, 0.16f, 1.95f), teal);
        Primitive("Roof trim", PrimitiveType.Cube, root.transform, new Vector3(0f, 1.65f, 0.1f), new Vector3(1.72f, 0.05f, 1.84f), cream);
        Primitive("Mast", PrimitiveType.Cylinder, root.transform, new Vector3(0f, 2.02f, -0.52f), new Vector3(0.045f, 0.52f, 0.045f), wood);
        var pennant = MeshObject("Summer pennant", SaveMesh(FlagMesh(), "Pennant"), orange, root.transform);
        pennant.transform.localPosition = new Vector3(0f, 2.44f, -0.52f);
        var ring = MeshObject("Life ring", ringMesh, orange, root.transform);
        ring.transform.localPosition = new Vector3(0.86f, 0.95f, -1.14f);
        ring.transform.localRotation = Quaternion.Euler(0, 0, 90);
        ring.transform.localScale = Vector3.one * 0.62f;
        Primitive("Outboard engine", PrimitiveType.Cube, root.transform, new Vector3(0f, 0.24f, -2.3f), new Vector3(0.62f, 0.85f, 0.55f), navy);
        Primitive("Engine cover", PrimitiveType.Cube, root.transform, new Vector3(0f, 0.7f, -2.3f), new Vector3(0.67f, 0.22f, 0.62f), red);
        var propeller = Primitive("Propeller", PrimitiveType.Cube, root.transform, new Vector3(0, -0.35f, -2.62f), new Vector3(0.7f, 0.12f, 0.1f), navy);
        var body = root.AddComponent<Rigidbody>();
        body.mass = 240f; body.linearDamping = 0.02f; body.angularDamping = 0.08f;
        var collider = root.AddComponent<MeshCollider>(); collider.sharedMesh = hullMesh; collider.convex = true;
        var cabinCollider = root.AddComponent<BoxCollider>(); cabinCollider.center = new Vector3(0, 1, 0.1f); cabinCollider.size = new Vector3(1.5f, 1, 1.6f);
        var floating = root.AddComponent<BuoyantBody>(); floating.ocean = ocean;
        var controller = root.AddComponent<BoatController>(); controller.propeller = propeller.transform;
        var interaction = root.AddComponent<WaterInteraction>(); interaction.effects = effects; interaction.boat = true;
        DressFishingBoat(root.transform);
        return controller;
    }

    static void Buoy(Vector3 position, int index, OceanSurface ocean, WaterEffects effects, Transform parent)
    {
        var root = new GameObject("Buoy " + (index + 1)); root.transform.SetParent(parent); root.transform.position = position;
        var color = index % 2 == 0 ? red : teal;
        Primitive("Weighted float", PrimitiveType.Sphere, root.transform, Vector3.zero, new Vector3(1.25f, 0.9f, 1.25f), color);
        Primitive("White band", PrimitiveType.Cylinder, root.transform, new Vector3(0, 0.16f, 0), new Vector3(1.18f, 0.095f, 1.18f), cream);
        Primitive("Marker", PrimitiveType.Cylinder, root.transform, new Vector3(0, 0.83f, 0), new Vector3(0.24f, 0.68f, 0.24f), color);
        Primitive("Light cap", PrimitiveType.Sphere, root.transform, new Vector3(0, 1.53f, 0), Vector3.one * 0.38f, orange);
        var loop = MeshObject("Float rim", ringMesh, navy, root.transform); loop.transform.localScale = Vector3.one * 1.22f;
        var body = root.AddComponent<Rigidbody>(); body.mass = 42f; body.angularDamping = 0.05f;
        root.AddComponent<SphereCollider>().radius = 0.64f;
        var pole = root.AddComponent<CapsuleCollider>(); pole.center = new Vector3(0, 0.85f, 0); pole.radius = 0.17f; pole.height = 1.6f;
        var floating = root.AddComponent<BuoyantBody>(); floating.ocean = ocean; floating.moored = true;
        floating.draft = 0.35f; floating.centerOfMass = new Vector3(0, -0.4f, 0); floating.waterDrag = 0.9f;
        floating.floatPoints = new[] { new Vector3(-0.36f, -0.32f, -0.36f), new Vector3(0.36f, -0.32f, -0.36f),
            new Vector3(-0.36f, -0.32f, 0.36f), new Vector3(0.36f, -0.32f, 0.36f) };
        var interaction = root.AddComponent<WaterInteraction>(); interaction.effects = effects;
    }

    static void Lighthouse(Vector3 point, Transform parent)
    {
        var root = new GameObject("Island beacon").transform; root.SetParent(parent); root.position = point;
        Primitive("Beacon foundation", PrimitiveType.Cylinder, root, new Vector3(0, 0.2f, 0), new Vector3(2.2f, 0.2f, 2.2f), stone, true);
        Primitive("Ivory tower", PrimitiveType.Cylinder, root, new Vector3(0, 1.6f, 0), new Vector3(1.45f, 1.4f, 1.45f), cream, true);
        Primitive("Red stripe", PrimitiveType.Cylinder, root, new Vector3(0, 2.1f, 0), new Vector3(1.47f, 0.22f, 1.47f), red);
        Primitive("Lantern glass", PrimitiveType.Cylinder, root, new Vector3(0, 3.25f, 0), new Vector3(1.08f, 0.28f, 1.08f), teal);
        Primitive("Lantern cap", PrimitiveType.Sphere, root, new Vector3(0, 3.65f, 0), new Vector3(1.9f, 0.65f, 1.9f), red);
        Primitive("Door", PrimitiveType.Cube, root, new Vector3(0, 0.72f, -0.72f), new Vector3(0.48f, 1.1f, 0.05f), navy);
    }

    static void Palm(Vector3 position, Transform parent)
    {
        var root = new GameObject("Palm").transform; root.SetParent(parent); root.position = position;
        float rotation = position.x * 31f;
        for (int i = 0; i < 5; i++)
        {
            var trunk = MeshObject("Angular trunk segment", postMesh, wood, root);
            trunk.transform.localPosition = new Vector3(i * 0.13f, 0.45f + i * 0.78f, 0);
            trunk.transform.localScale = new Vector3(0.35f - i * 0.025f, 0.86f, 0.35f - i * 0.025f);
            trunk.AddComponent<BoxCollider>().size = Vector3.one;
            trunk.transform.localRotation = Quaternion.Euler(0, 0, -9);
        }
        Mesh frond = SaveMesh(FrondMesh(), "Palm frond");
        for (int i = 0; i < 10; i++)
        {
            var crown = MeshObject("Palm frond", frond, i % 2 == 0 ? leaf : leafLight, root);
            crown.transform.localPosition = new Vector3(0.58f, 3.85f, 0);
            crown.transform.localRotation = Quaternion.Euler(Mathf.Sin(i * 4.2f) * 12f, rotation + i * 36f, 0);
            crown.transform.localScale = Vector3.one * (0.86f + 0.16f * Mathf.Sin(i * 5.6f));
        }
        Primitive("Coconuts", PrimitiveType.Sphere, root, new Vector3(0.5f, 3.6f, 0), Vector3.one * 0.55f, wood);
    }

    static void Dock(Transform parent)
    {
        var root = new GameObject("Little jetty").transform; root.SetParent(parent); root.position = new Vector3(1f, 0.65f, 3f);
        root.rotation = Quaternion.Euler(0, -12, 0);
        for (int i = 0; i < 14; i++) Primitive("Deck board", PrimitiveType.Cube, root, new Vector3(0, 0, i * 0.42f), new Vector3(2.4f, 0.18f, 0.39f), wood, true);
        for (int i = 0; i < 3; i++)
        for (int side = -1; side <= 1; side += 2)
            Primitive("Jetty piling", PrimitiveType.Cylinder, root, new Vector3(side * 1.1f, -0.8f, i * 2.5f), new Vector3(0.25f, 1.5f, 0.25f), wood, true);
    }

    static void Clouds(Transform parent)
    {
        var white = Lit("Cloud ivory", new Color(0.95f, 0.98f, 0.93f));
        for (int group = 0; group < 5; group++)
        for (int puff = 0; puff < 3; puff++)
        {
            var cloud = Primitive("Distant cloud", PrimitiveType.Sphere, parent, new Vector3(-75f + group * 36f + puff * 6f, 28f + puff % 2 * 2.2f, 100f + group % 2 * 15f), new Vector3(14, 5, 7), white);
            cloud.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        }
    }

    static float IslandHeight(MeshCollider collider, float x, float z)
    {
        return collider.Raycast(new Ray(new Vector3(x, 30, z), Vector3.down), out var hit, 50) ? hit.point.y : 1f;
    }

    static GameObject Primitive(string name, PrimitiveType type, Transform parent, Vector3 position, Vector3 scale, Material material, bool collision = false)
    {
        var go = GameObject.CreatePrimitive(type); go.name = name;
        go.transform.SetParent(parent, false); go.transform.localPosition = position; go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = material;
        if (!collision) Object.DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }

    static GameObject MeshObject(string name, Mesh mesh, Material material, Transform parent)
    {
        var go = new GameObject(name); go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = material;
        return go;
    }

    static Material Lit(string name, Color color)
    {
        var material = new Material(Shader.Find("NiceBoat/Harbor/Painted Lit")) { name = name, enableInstancing = true };
        material.SetColor("_BaseColor", color);
        return SaveAsset(material, AssetRoot + "/Materials/" + name + ".mat");
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        int split = path.LastIndexOf('/');
        AssetDatabase.CreateFolder(path.Substring(0, split), path.Substring(split + 1));
    }

    static T SaveAsset<T>(T asset, string path) where T : Object
    {
        var existing = AssetDatabase.LoadAssetAtPath<T>(path);
        if (existing != null)
        {
            if (asset is Mesh source && existing is Mesh destination)
            {
                destination.Clear(); destination.indexFormat = source.indexFormat;
                destination.vertices = source.vertices; destination.normals = source.normals;
                destination.uv = source.uv; destination.colors = source.colors; destination.subMeshCount = source.subMeshCount;
                for (int i = 0; i < source.subMeshCount; i++) destination.SetTriangles(source.GetTriangles(i), i);
                destination.bounds = source.bounds;
            }
            else EditorUtility.CopySerialized(asset, existing);
            EditorUtility.SetDirty(existing); Object.DestroyImmediate(asset); return existing;
        }
        AssetDatabase.CreateAsset(asset, path); return asset;
    }

    static Mesh SaveMesh(Mesh mesh, string name) { mesh.name = name; return SaveAsset(mesh, AssetRoot + "/Meshes/" + name + ".asset"); }

    static Mesh Grid(int segments)
    {
        var vertices = new Vector3[(segments + 1) * (segments + 1)];
        var uv = new Vector2[vertices.Length]; var triangles = new int[segments * segments * 6]; int cursor = 0;
        for (int z = 0; z <= segments; z++) for (int x = 0; x <= segments; x++)
        {
            int i = z * (segments + 1) + x; uv[i] = new Vector2((float)x / segments, (float)z / segments);
            // Dense near the playable area, sparse out to the horizon: same vertex budget.
            vertices[i] = new Vector3(OceanCoordinate(uv[i].x), 0, OceanCoordinate(uv[i].y));
        }
        for (int z = 0; z < segments; z++) for (int x = 0; x < segments; x++)
        {
            int i = z * (segments + 1) + x;
            triangles[cursor++] = i; triangles[cursor++] = i + segments + 1; triangles[cursor++] = i + 1;
            triangles[cursor++] = i + 1; triangles[cursor++] = i + segments + 1; triangles[cursor++] = i + segments + 2;
        }
        var mesh = new Mesh { vertices = vertices, uv = uv, triangles = triangles }; mesh.RecalculateNormals();
        mesh.bounds = new Bounds(Vector3.zero, new Vector3(960f, 4, 960f)); return mesh;
    }

    static float OceanCoordinate(float uv)
    {
        float n = uv * 2f - 1f, a = Mathf.Abs(n);
        float distance = a <= 0.7f ? a / 0.7f * 80f : 80f + Mathf.Pow((a - 0.7f) / 0.3f, 2f) * 400f;
        return Mathf.Sign(n) * distance;
    }

    static Mesh IslandMesh()
    {
        return SculptedIslandMesh();
    }

    static Mesh HullMesh()
    {
        float[] z = { -2.2f, -0.7f, 1.2f, 2.45f }; float[] w = { 0.82f, 1.08f, 0.82f, 0.025f };
        var rings = new List<Vector3>(); var vertices = new List<Vector3>(); var triangles = new List<int>();
        for (int i = 0; i < z.Length; i++) rings.AddRange(new[] { new Vector3(-w[i], 0.45f, z[i]), new Vector3(w[i], 0.45f, z[i]),
            new Vector3(w[i] * 0.88f, -0.15f, z[i]), new Vector3(w[i] * 0.45f, -0.6f, z[i]),
            new Vector3(-w[i] * 0.45f, -0.6f, z[i]), new Vector3(-w[i] * 0.88f, -0.15f, z[i]) });
        for (int ring = 0; ring < 3; ring++) for (int side = 0; side < 6; side++)
        {
            int a = ring * 6 + side, b = ring * 6 + (side + 1) % 6;
            Triangle(vertices, triangles, rings[a], rings[a + 6], rings[b]);
            Triangle(vertices, triangles, rings[b], rings[a + 6], rings[b + 6]);
        }
        for (int i = 1; i < 5; i++) { Triangle(vertices, triangles, rings[0], rings[i], rings[i + 1]); Triangle(vertices, triangles, rings[18], rings[18 + i + 1], rings[18 + i]); }
        return MeshFrom(vertices, triangles);
    }

    static Mesh Torus(float radius, float tube)
    {
        var v = new List<Vector3>(); var t = new List<int>(); const int segments = 24, sides = 6;
        for (int i = 0; i < segments; i++) for (int j = 0; j < sides; j++)
        {
            float a = i * Mathf.PI * 2 / segments, b = j * Mathf.PI * 2 / sides;
            v.Add(new Vector3(Mathf.Cos(a) * (radius + tube * Mathf.Cos(b)), tube * Mathf.Sin(b), Mathf.Sin(a) * (radius + tube * Mathf.Cos(b))));
        }
        for (int i = 0; i < segments; i++) for (int j = 0; j < sides; j++)
        {
            int a = i * sides + j, b = ((i + 1) % segments) * sides + j, c = i * sides + (j + 1) % sides, d = ((i + 1) % segments) * sides + (j + 1) % sides;
            t.Add(a); t.Add(c); t.Add(b); t.Add(c); t.Add(d); t.Add(b);
        }
        return MeshFrom(v, t);
    }

    static Mesh FrondMesh()
    {
        var v = new List<Vector3>(); var t = new List<int>();
        for (int i = 0; i < 5; i++)
        {
            float a = i / 5f, b = (i + 1) / 5f;
            Vector3 centerA = new(0, Mathf.Sin(a * Mathf.PI) * 0.8f - a * a * 1.4f, a * 3.4f);
            Vector3 centerB = new(0, Mathf.Sin(b * Mathf.PI) * 0.8f - b * b * 1.4f, b * 3.4f);
            float widthA = Mathf.Max(0.025f, Mathf.Sin(a * Mathf.PI) * 0.65f);
            float widthB = Mathf.Max(0.025f, Mathf.Sin(b * Mathf.PI) * 0.65f);
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 edgeA = centerA + new Vector3(side * widthA, -0.12f, 0);
                Vector3 edgeB = centerB + new Vector3(side * widthB, -0.12f, 0);
                if (side > 0) { Triangle(v, t, centerA, centerB, edgeA); Triangle(v, t, edgeA, centerB, edgeB); }
                else { Triangle(v, t, centerA, edgeA, centerB); Triangle(v, t, edgeA, edgeB, centerB); }
            }
        }
        return MeshFrom(v, t);
    }

    static Mesh FlagMesh()
    {
        var v = new List<Vector3>(); var t = new List<int>();
        Vector3 a = new(0, 0, 0), b = new(0, -0.4f, 0), c = new(0.7f, -0.12f, 0.12f);
        Triangle(v, t, a, b, c); Triangle(v, t, a, c, b); return MeshFrom(v, t);
    }

    static Mesh RockMesh()
    {
        float g = (1f + Mathf.Sqrt(5f)) / 2f;
        Vector3[] p = { new(-1,g,0),new(1,g,0),new(-1,-g,0),new(1,-g,0),new(0,-1,g),new(0,1,g),new(0,-1,-g),new(0,1,-g),new(g,0,-1),new(g,0,1),new(-g,0,-1),new(-g,0,1) };
        int[] faces = { 0,11,5,0,5,1,0,1,7,0,7,10,0,10,11,1,5,9,5,11,4,11,10,2,10,7,6,7,1,8,3,9,4,3,4,2,3,2,6,3,6,8,3,8,9,4,9,5,2,4,11,6,2,10,8,6,7,9,8,1 };
        var v = new List<Vector3>(); var t = new List<int>();
        for (int i = 0; i < faces.Length; i += 3) Triangle(v, t, p[faces[i]].normalized, p[faces[i+1]].normalized, p[faces[i+2]].normalized);
        return MeshFrom(v, t);
    }

    static void Triangle(List<Vector3> vertices, List<int> triangles, Vector3 a, Vector3 b, Vector3 c)
    {
        int index = vertices.Count; vertices.Add(a); vertices.Add(b); vertices.Add(c); triangles.Add(index); triangles.Add(index+1); triangles.Add(index+2);
    }
    static Mesh MeshFrom(List<Vector3> vertices, List<int> triangles)
    {
        var mesh = new Mesh(); mesh.SetVertices(vertices); mesh.SetTriangles(triangles,0); mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
    }
}
