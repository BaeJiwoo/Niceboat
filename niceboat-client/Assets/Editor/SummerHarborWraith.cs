using System.Collections.Generic;
using System.IO;
using NiceBoat;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

public static partial class SummerHarborBuilder
{
    static void ConfigureShadowWraith(HarborDummyEnemy enemy)
    {
        var root = enemy.transform;
        var visual = new GameObject("Living shadow").transform; visual.SetParent(root, false);
        enemy.model = visual; enemy.skin = System.Array.Empty<Renderer>();
        var vfx = root.gameObject.AddComponent<ShadowWraithVfx>(); enemy.wraith = vfx;
        vfx.visualRoot = visual; vfx.ocean = enemy.ocean;
        var noise = WraithNoise();
        var coreMaterial = WraithMaterial("Wraith charcoal", "NiceBoat/Wraith/Core");
        coreMaterial.SetTexture("_Noise", noise); coreMaterial.SetColor("_BaseColor", Hex("11191E"));
        var smokeMaterial = WraithMaterial("Wraith smoke", "NiceBoat/Wraith/Smoke");
        smokeMaterial.SetTexture("_Noise", noise); smokeMaterial.EnableKeyword("_SOFTPARTICLES_ON");
        var eyeMaterial = WraithMaterial("Wraith red eyes", "NiceBoat/Wraith/Eye Glow");
        var haloMaterial = WraithMaterial("Wraith eye halos", "NiceBoat/Wraith/Eye Glow");
        haloMaterial.SetFloat("_SoftEdge", 1f); haloMaterial.SetFloat("_Intensity", 0.5f);
        var shadowMaterial = WraithMaterial("Wraith ground shadow", "NiceBoat/Wraith/Ground Shadow");
        foreach (var material in new[] { coreMaterial, smokeMaterial, eyeMaterial, haloMaterial, shadowMaterial }) EditorUtility.SetDirty(material);

        var core = new GameObject("Core animation").AddComponent<ShadowWraithCore>(); core.transform.SetParent(visual, false);
        vfx.core = core; core.lobes = new Transform[3];
        for (int i = 0; i < 3; i++)
        {
            var lobe = MeshObject("Condensed smoke " + i, SaveMesh(WraithLobe(i), "Wraith core " + i), coreMaterial, core.transform);
            lobe.transform.localPosition = i == 0 ? new Vector3(0f, 0.48f, 0f) : new Vector3(i == 1 ? -0.65f : 0.55f, i == 1 ? 0.7f : 1.1f, -0.15f);
            lobe.transform.localScale = Vector3.one * (i == 0 ? 0.85f : 0.6f);
            lobe.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            core.lobes[i] = lobe.transform;
        }
        var eyes = new GameObject("Stable red eyes").AddComponent<ShadowWraithEyes>(); eyes.transform.SetParent(visual, false);
        eyes.transform.localPosition = new Vector3(0f, 2.1f, 1.3f);
        eyes.eyes = new Renderer[2]; eyes.halos = new Renderer[2]; vfx.eyes = eyes;
        var eyeMesh = SaveMesh(MonsterEyeMesh(), "Wraith eye slit");
        var quad = SaveMesh(WraithQuad(), "Wraith soft quad");
        for (int i = 0; i < 2; i++)
        {
            float side = i == 0 ? -1f : 1f;
            var eye = MeshObject("Emissive eye " + i, eyeMesh, eyeMaterial, eyes.transform);
            eye.transform.localPosition = Vector3.right * side * 0.39f;
            eye.transform.localScale = new Vector3(side * 0.95f, 0.8f, 1f);
            eyes.eyes[i] = eye.GetComponent<Renderer>(); eyes.eyes[i].shadowCastingMode = ShadowCastingMode.Off;
            var halo = MeshObject("Red haze " + i, quad, haloMaterial, eyes.transform);
            halo.transform.localPosition = eye.transform.localPosition + Vector3.forward * 0.025f;
            halo.transform.localScale = new Vector3(1.15f, 0.65f, 1f);
            eyes.halos[i] = halo.GetComponent<Renderer>(); eyes.halos[i].shadowCastingMode = ShadowCastingMode.Off;
        }
        var shadow = MeshObject("Hover shadow", quad, shadowMaterial, root);
        shadow.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        vfx.groundShadow = shadow.GetComponent<Renderer>(); vfx.groundShadow.shadowCastingMode = ShadowCastingMode.Off;
        var particles = new GameObject("Smoke systems").AddComponent<ShadowWraithParticles>(); particles.transform.SetParent(visual, false);
        particles.mobilePipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>("Assets/Settings/Mobile_RPAsset.asset");
        vfx.smoke = particles;
        particles.mainSmoke = WraithSystem("Main smoke - rising", particles.transform, smokeMaterial, 40, 18f, 0.9f, 1.8f, 0.9f);
        particles.mainSmoke.transform.localPosition = Vector3.up * 1.4f;
        particles.outerSmoke = WraithSystem("Outer smoke - reconverging", particles.transform, smokeMaterial, 28, 12f, 1.3f, 2.1f, 1.25f);
        particles.outerSmoke.transform.localPosition = Vector3.up * 1.65f;
        var outerShape = particles.outerSmoke.shape; outerShape.radiusThickness = 0.05f;
        var outerFlow = particles.outerSmoke.velocityOverLifetime;
        outerFlow.radial = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.5f), new Keyframe(0.5f, 0f), new Keyframe(1f, -0.85f)));
        outerFlow.orbitalY = 0.35f;
        particles.fragments = WraithSystem("Shadow fragments", particles.transform, coreMaterial, 10, 3f, 1.3f, 2.3f, 1.3f);
        particles.fragments.transform.localPosition = Vector3.up * 1.6f;
        var fragmentMain = particles.fragments.main; fragmentMain.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.23f);
        fragmentMain.startRotation3D = true; fragmentMain.startRotationX = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        fragmentMain.startRotationY = fragmentMain.startRotationX; fragmentMain.startRotationZ = fragmentMain.startRotationX;
        var spin = particles.fragments.rotationOverLifetime; spin.enabled = true; spin.separateAxes = true;
        spin.x = 0.8f; spin.y = 1.3f; spin.z = 0.5f;
        var renderer = particles.fragments.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Mesh; renderer.mesh = SaveMesh(WraithLobe(7), "Wraith fragment");
        renderer.SetActiveVertexStreams(new List<ParticleSystemVertexStream> { ParticleSystemVertexStream.Position, ParticleSystemVertexStream.Normal, ParticleSystemVertexStream.Color, ParticleSystemVertexStream.UV });
        particles.bursts = WraithSystem("Pooled hit and death smoke", particles.transform, smokeMaterial, 48, 0f, 0.65f, 1.8f, 0.1f);
        var burstShape = particles.bursts.shape; burstShape.enabled = false;
        var burstEmission = particles.bursts.emission; burstEmission.enabled = false;
        var burstFlow = particles.bursts.velocityOverLifetime; burstFlow.enabled = false;
        particles.ApplySettings();
    }

    static Material WraithMaterial(string name, string shader)
    {
        return SaveAsset(new Material(Shader.Find(shader)) { name = name }, AssetRoot + "/Materials/" + name + ".mat");
    }

    static ParticleSystem WraithSystem(string name, Transform parent, Material material, int cap, float rate, float minLife, float maxLife, float radius)
    {
        var system = new GameObject(name).AddComponent<ParticleSystem>(); system.transform.SetParent(parent, false);
        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = system.main; main.playOnAwake = false; main.loop = true; main.duration = 3f;
        main.maxParticles = cap; main.startLifetime = new ParticleSystem.MinMaxCurve(minLife, maxLife);
        main.startSpeed = 0f; main.startSize = new ParticleSystem.MinMaxCurve(0.7f, 1.3f);
        main.startRotation = new ParticleSystem.MinMaxCurve(-Mathf.PI, Mathf.PI);
        main.simulationSpace = ParticleSystemSimulationSpace.World; main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
        var emission = system.emission; emission.rateOverTime = rate;
        var shape = system.shape; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = radius;
        var noise = system.noise; noise.enabled = true; noise.quality = ParticleSystemNoiseQuality.Low;
        noise.strength = 0.22f; noise.frequency = 0.65f; noise.scrollSpeed = 0.3f; noise.octaveCount = 1;
        var flow = system.velocityOverLifetime; flow.enabled = true; flow.space = ParticleSystemSimulationSpace.World; flow.y = 0.55f;
        var fade = system.colorOverLifetime; fade.enabled = true;
        var gradient = new Gradient(); gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.9f, 0.16f), new GradientAlphaKey(0.65f, 0.55f), new GradientAlphaKey(0f, 1f) }); fade.color = gradient;
        var size = system.sizeOverLifetime; size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.35f), new Keyframe(0.35f, 1f), new Keyframe(1f, 1.3f)));
        var renderer = system.GetComponent<ParticleSystemRenderer>(); renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
        renderer.renderMode = ParticleSystemRenderMode.Billboard; renderer.sortMode = ParticleSystemSortMode.Distance;
        renderer.minParticleSize = 0f; renderer.maxParticleSize = 0.25f;
        renderer.SetActiveVertexStreams(new List<ParticleSystemVertexStream> { ParticleSystemVertexStream.Position, ParticleSystemVertexStream.Color, ParticleSystemVertexStream.UV, ParticleSystemVertexStream.AgePercent });
        return system;
    }

    static Mesh WraithLobe(int seed)
    {
        var v = new List<Vector3>(); var indices = new List<int>();
        const int sides = 9, rings = 7;
        var points = new Vector3[rings, sides];
        float[] radii = { 0.035f, 0.38f, 0.88f, 1f, 0.83f, 0.48f, 0.025f };
        for (int y = 0; y < rings; y++)
        for (int x = 0; x < sides; x++)
        {
            float angle = (x + (y % 2) * 0.23f) * Mathf.PI * 2f / sides;
            float random = Mathf.PerlinNoise(x * 0.71f + seed * 3f, y * 0.63f + 12f);
            float r = radii[y] * (0.72f + random * 0.6f);
            points[y, x] = new Vector3(Mathf.Cos(angle) * r + Mathf.Sin(y * 1.2f + seed) * 0.16f,
                y * 0.41f + (random - 0.5f) * 0.2f, Mathf.Sin(angle) * r * 0.7f);
        }
        for (int y = 0; y < rings - 1; y++)
        for (int x = 0; x < sides; x++)
        {
            int next = (x + 1) % sides;
            Triangle(v, indices, points[y, x], points[y + 1, x], points[y + 1, next]);
            Triangle(v, indices, points[y, x], points[y + 1, next], points[y, next]);
        }
        for (int x = 1; x < sides - 1; x++)
        {
            Triangle(v, indices, points[0, 0], points[0, x], points[0, x + 1]);
            Triangle(v, indices, points[rings - 1, 0], points[rings - 1, x + 1], points[rings - 1, x]);
        }
        var mesh = MeshFrom(v, indices); var colors = new Color[v.Count];
        for (int i = 0; i < colors.Length; i++) colors[i] = Color.white;
        mesh.colors = colors; var bounds = mesh.bounds; bounds.Expand(0.6f); mesh.bounds = bounds;
        return mesh;
    }

    static Mesh WraithQuad()
    {
        var mesh = new Mesh { vertices = new[] { new Vector3(-0.5f,-0.5f,0), new Vector3(0.5f,-0.5f,0), new Vector3(0.5f,0.5f,0), new Vector3(-0.5f,0.5f,0) },
            uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up }, triangles = new[] { 0, 2, 1, 0, 3, 2 } };
        mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
    }

    static Texture2D WraithNoise()
    {
        EnsureFolder(AssetRoot + "/Textures");
        const int size = 128;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, true, true);
        var pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float u = x / (float)size, v = y / (float)size;
            float a = PeriodicCloud(u, v, 4f, 11f) * 0.65f + PeriodicCloud(u, v, 11f, 27f) * 0.35f;
            float b = PeriodicCloud(u, v, 7f, 39f);
            pixels[y * size + x] = new Color(a, b, 0f, 1f);
        }
        texture.SetPixels(pixels); texture.Apply();
        string path = AssetRoot + "/Textures/Wraith packed cloud.png";
        File.WriteAllBytes(path, texture.EncodeToPNG()); Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.sRGBTexture = false; importer.wrapMode = TextureWrapMode.Repeat; importer.filterMode = FilterMode.Bilinear;
        importer.mipmapEnabled = true; importer.textureCompression = TextureImporterCompression.Uncompressed; importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static float PeriodicCloud(float u, float v, float scale, float seed)
    {
        float a = Mathf.Lerp(Mathf.PerlinNoise(seed + u * scale, seed + v * scale), Mathf.PerlinNoise(seed + (u - 1f) * scale, seed + v * scale), u);
        float b = Mathf.Lerp(Mathf.PerlinNoise(seed + u * scale, seed + (v - 1f) * scale), Mathf.PerlinNoise(seed + (u - 1f) * scale, seed + (v - 1f) * scale), u);
        return Mathf.Lerp(a, b, v);
    }
}
