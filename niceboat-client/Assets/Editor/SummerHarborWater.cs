using System.Collections.Generic;
using System.IO;
using NiceBoat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static partial class SummerHarborBuilder
{
    // This refresh only touches water assets/components, preserving scene art and layout.
    [MenuItem("NiceBoat/Refresh Harbor Water")]
    public static void RefreshWaterFromMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        RefreshWater();
    }

    public static void RefreshWater()
    {
        if (SceneManager.GetActiveScene().path != ScenePath)
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var ocean = Object.FindFirstObjectByType<OceanSurface>();
        var effects = Object.FindFirstObjectByType<WaterEffects>();
        var boat = Object.FindFirstObjectByType<BoatController>();
        if (ocean == null || effects == null || boat == null)
            throw new System.InvalidOperationException("Open or build the Summer Harbor scene first.");
        var material = ocean.waterRenderer.sharedMaterial;
        ConfigureHarborWater(material);
        BakeHarborWaterMap(material, ocean.transform.parent, ocean.seaLevel);
        ConfigureWaterInteraction(effects, boat.transform);
        ocean.RefreshSettings();
        EditorUtility.SetDirty(material);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        Debug.Log("HARBOR_WATER_REFRESHED");
    }

    static void ConfigureWaterInteraction(WaterEffects effects, Transform boat)
    {
        var field = effects.GetComponent<WaterInteractionField>();
        if (field == null) field = effects.gameObject.AddComponent<WaterInteractionField>();
        field.updateMaterial = SaveAsset(new Material(Shader.Find("NiceBoat/Water/Interaction Field")),
            AssetRoot + "/Materials/Water interaction field.mat");
        field.followTarget = boat;
        field.resolution = 256;
        field.worldSize = 64f;
        field.updatesPerSecond = 30f;
        field.foamLifetime = 5f;
        field.disturbanceLifetime = 2.4f;
        effects.interactionField = field;
    }

    static void BakeHarborWaterMap(Material material, Transform environment, float seaLevel)
    {
        const int size = 512;
        const float extent = 240f, depthRange = 12f, shoreRange = 16f;
        var origin = new Vector2(-120f, -100f);
        float cell = extent / size;
        var terrain = new List<Collider>();
        foreach (var collider in environment.GetComponentsInChildren<Collider>())
        {
            // Docks, roofs and moving objects are not seabed/shoreline.
            if (collider.name == "Palm island - solid shore" || collider.name == "Shore rock") terrain.Add(collider);
        }
        if (terrain.Count == 0) throw new System.InvalidOperationException("Harbor terrain colliders are missing.");
        Physics.SyncTransforms();
        var depth = new float[size * size];
        var distance = new float[size * size];
        for (int z = 0; z < size; z++)
        for (int x = 0; x < size; x++)
        {
            float wx = origin.x + (x + 0.5f) * cell, wz = origin.y + (z + 0.5f) * cell;
            var ray = new Ray(new Vector3(wx, seaLevel + 32f, wz), Vector3.down);
            float seabed = seaLevel - 7.5f;
            foreach (var collider in terrain)
            {
                var bounds = collider.bounds;
                if (wx < bounds.min.x || wx > bounds.max.x || wz < bounds.min.z || wz > bounds.max.z) continue;
                if (collider.Raycast(ray, out var hit, 64f)) seabed = Mathf.Max(seabed, hit.point.y);
            }
            int index = z * size + x;
            depth[index] = Mathf.Clamp(seaLevel - seabed, 0f, depthRange);
            distance[index] = depth[index] <= 0.01f ? 0f : shoreRange / cell;
        }

        // Two-pass chamfer distance in texels. Baked once, never evaluated per frame.
        for (int z = 0; z < size; z++) for (int x = 0; x < size; x++)
        {
            int i = z * size + x;
            if (x > 0) distance[i] = Mathf.Min(distance[i], distance[i - 1] + 1f);
            if (z > 0) distance[i] = Mathf.Min(distance[i], distance[i - size] + 1f);
            if (x > 0 && z > 0) distance[i] = Mathf.Min(distance[i], distance[i - size - 1] + 1.414214f);
            if (x + 1 < size && z > 0) distance[i] = Mathf.Min(distance[i], distance[i - size + 1] + 1.414214f);
        }
        for (int z = size - 1; z >= 0; z--) for (int x = size - 1; x >= 0; x--)
        {
            int i = z * size + x;
            if (x + 1 < size) distance[i] = Mathf.Min(distance[i], distance[i + 1] + 1f);
            if (z + 1 < size) distance[i] = Mathf.Min(distance[i], distance[i + size] + 1f);
            if (x + 1 < size && z + 1 < size) distance[i] = Mathf.Min(distance[i], distance[i + size + 1] + 1.414214f);
            if (x > 0 && z + 1 < size) distance[i] = Mathf.Min(distance[i], distance[i + size - 1] + 1.414214f);
        }
        var pixels = new Color[size * size];
        for (int z = 0; z < size; z++) for (int x = 0; x < size; x++)
        {
            int i = z * size + x;
            float wx = origin.x + (x + 0.5f) * cell, wz = origin.y + (z + 0.5f) * cell;
            // Art-directed sheltered basin, easing smoothly into exposed offshore water.
            float shelter = Mathf.Exp(-((wx + 2f) * (wx + 2f) / 650f + (wz - 7f) * (wz - 7f) / 520f));
            float exposure = Mathf.Lerp(0.3f, 1f, 1f - shelter) * Mathf.Lerp(0.55f, 1f, Mathf.Clamp01(depth[i] / 4f));
            float foam = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(depth[i] / 0.45f));
            pixels[i] = new Color(depth[i] / depthRange, Mathf.Clamp01(distance[i] * cell / shoreRange), exposure, foam);
        }
        EnsureFolder(AssetRoot + "/Textures");
        string path = AssetRoot + "/Textures/Harbor water regions.png";
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false, true);
        texture.SetPixels(pixels); texture.Apply();
        File.WriteAllBytes(path, texture.EncodeToPNG()); Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.sRGBTexture = false; importer.mipmapEnabled = true; importer.wrapMode = TextureWrapMode.Clamp;
        importer.alphaSource = TextureImporterAlphaSource.FromInput; importer.alphaIsTransparency = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.filterMode = FilterMode.Bilinear; importer.SaveAndReimport();
        material.SetTexture("_WorldWaterMask", AssetDatabase.LoadAssetAtPath<Texture2D>(path));
        material.SetVector("_WorldWaterBounds", new Vector4(origin.x, origin.y, 1f / extent, 1f / extent));
        material.SetFloat("_WorldDepthRange", depthRange);
        material.SetFloat("_WorldShoreRange", shoreRange);
        material.SetFloat("_WorldMaskStrength", 1f);
        Debug.Log($"HARBOR_WATER_MAP: {size}x{size}, {terrain.Count} terrain colliders");
    }
}
