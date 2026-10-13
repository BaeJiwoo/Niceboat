using System.Collections.Generic;
using System.IO;
using NiceBoat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static partial class SummerHarborBuilder
{
    [MenuItem("NiceBoat/Refresh Training Monster")]
    public static void RefreshEnemyFromMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        RefreshEnemy();
    }

    public static void RefreshEnemy()
    {
        if (SceneManager.GetActiveScene().path != ScenePath) EditorSceneManager.OpenScene(ScenePath);
        var boat = Object.FindFirstObjectByType<BoatController>();
        if (boat == null) throw new System.InvalidOperationException("Build the harbor first.");
        var existing = GameObject.Find("Training sea monster");
        if (existing != null) Object.DestroyImmediate(existing);
        ConfigureDummyEnemy(boat, Object.FindFirstObjectByType<OceanSurface>(), Object.FindFirstObjectByType<WaterEffects>());
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        Debug.Log("HARBOR_ENEMY_REFRESHED");
    }

    static void ConfigureDummyEnemy(BoatController boat, OceanSurface ocean, WaterEffects effects)
    {
        var root = new GameObject("Training sea monster");
        root.transform.position = boat.transform.position + boat.transform.forward * 14f;
        root.transform.position = new Vector3(root.transform.position.x, ocean.seaLevel, root.transform.position.z);
        root.transform.rotation = Quaternion.LookRotation(-boat.transform.forward, Vector3.up);
        var body = root.AddComponent<Rigidbody>();
        body.isKinematic = true; body.useGravity = false; body.mass = 120f;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        var collider = root.AddComponent<CapsuleCollider>();
        collider.center = new Vector3(0f, 1.1f, 0f); collider.radius = 1.15f; collider.height = 3.1f;
        var enemy = root.AddComponent<HarborDummyEnemy>();
        enemy.player = boat; enemy.ocean = ocean; enemy.waterEffects = effects; enemy.burstMaterial = effects.sprayMaterial;
        ConfigureShadowWraith(enemy);
        ConfigureEnemyHealthBar(enemy);
        PrefabUtility.SaveAsPrefabAsset(root, AssetRoot + "/Prefabs/ShadowWraith.prefab");
    }

    static void ConfigureEnemyHealthBar(HarborDummyEnemy enemy)
    {
        string path = AssetRoot + "/Textures/Health pixel.png";
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        texture.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white }); texture.Apply();
        File.WriteAllBytes(path, texture.EncodeToPNG()); Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
        importer.mipmapEnabled = false; importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed; importer.SaveAndReimport();
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        var go = new GameObject("Monster health", typeof(RectTransform), typeof(Canvas));
        go.transform.SetParent(enemy.transform, false); go.transform.localPosition = new Vector3(0f, 4.3f, 0f);
        go.transform.localScale = Vector3.one * 0.015f;
        var rect = (RectTransform)go.transform; rect.sizeDelta = new Vector2(250f, 90f);
        enemy.healthCanvas = go.GetComponent<Canvas>(); enemy.healthCanvas.renderMode = RenderMode.WorldSpace;
        enemy.healthCanvas.worldCamera = Camera.main;
        EnemyBarImage("Bar border", rect, new Vector2(244f, 20f), Hex("10191C"), sprite, false);
        EnemyBarImage("Empty health", rect, new Vector2(236f, 12f), Hex("4A3032"), sprite, false);
        enemy.damageTrail = EnemyBarImage("Recent damage", rect, new Vector2(236f, 12f), Hex("EDBD6D"), sprite, true);
        enemy.healthFill = EnemyBarImage("Remaining health", rect, new Vector2(236f, 12f), Hex("D8493F"), sprite, true);
        enemy.healthText = EnemyBarText("Health value", rect, new Vector2(0f, 28f), 23, "WRAITH  100 / 100");
        enemy.damageText = EnemyBarText("Damage popup", rect, new Vector2(0f, 55f), 32, "");
        enemy.damageText.enabled = false;
    }

    static Image EnemyBarImage(string name, RectTransform parent, Vector2 size, Color color, Sprite sprite, bool filled)
    {
        var image = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
        image.transform.SetParent(parent, false); image.rectTransform.sizeDelta = size;
        image.color = color; image.sprite = sprite; image.raycastTarget = false;
        if (filled) { image.type = Image.Type.Filled; image.fillMethod = Image.FillMethod.Horizontal; image.fillOrigin = 0; }
        return image;
    }

    static Text EnemyBarText(string name, RectTransform parent, Vector2 position, int size, string value)
    {
        var label = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text)).GetComponent<Text>();
        label.transform.SetParent(parent, false); label.rectTransform.sizeDelta = new Vector2(250f, 38f);
        label.rectTransform.anchoredPosition = position;
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = size; label.fontStyle = FontStyle.Bold; label.alignment = TextAnchor.MiddleCenter;
        label.color = Hex("EFDFC1"); label.raycastTarget = false; label.text = value;
        return label;
    }

    static Mesh MonsterHornMesh(int side)
    {
        var vertices = new List<Vector3>(); var triangles = new List<int>();
        var tip = new Vector3(side * 0.34f, 0.9f, -0.2f);
        for (int i = 0; i < 6; i++)
        {
            float a = i * Mathf.PI / 3f, b = (i + 1) * Mathf.PI / 3f;
            var p = new Vector3(Mathf.Cos(a) * 0.27f, 0f, Mathf.Sin(a) * 0.27f);
            var q = new Vector3(Mathf.Cos(b) * 0.27f, 0f, Mathf.Sin(b) * 0.27f);
            Triangle(vertices, triangles, p, tip, q);
        }
        return MeshFrom(vertices, triangles);
    }

    static Mesh MonsterEyeMesh()
    {
        var vertices = new List<Vector3>(); var triangles = new List<int>();
        var a = new Vector3(-0.31f, -0.02f, 0f); var b = new Vector3(0.3f, 0.15f, 0f);
        var c = new Vector3(0.23f, -0.11f, 0f); var d = new Vector3(-0.26f, -0.2f, 0f);
        Triangle(vertices, triangles, a, b, c); Triangle(vertices, triangles, a, c, d);
        return MeshFrom(vertices, triangles);
    }
}
