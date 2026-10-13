using NiceBoat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static partial class SummerHarborBuilder
{
    [MenuItem("NiceBoat/Refresh Boat Cannon")]
    public static void RefreshCannonFromMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        RefreshCannon();
    }

    public static void RefreshCannon()
    {
        if (SceneManager.GetActiveScene().path != ScenePath) EditorSceneManager.OpenScene(ScenePath);
        var boat = Object.FindFirstObjectByType<BoatController>();
        if (boat == null) throw new System.InvalidOperationException("Build the Summer Harbor scene first.");
        ConfigureBoatCannon(boat, Object.FindFirstObjectByType<WaterEffects>());
        PrefabUtility.SaveAsPrefabAsset(boat.gameObject, AssetRoot + "/Prefabs/PlayerBoat.prefab");
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        Debug.Log("HARBOR_CANNON_REFRESHED");
    }

    static void ConfigureBoatCannon(BoatController boat, WaterEffects effects)
    {
        var old = boat.transform.Find("Bow cannon");
        if (old != null) Object.DestroyImmediate(old.gameObject);
        var iron = AssetDatabase.LoadAssetAtPath<Material>(AssetRoot + "/Materials/Deep ink.mat");
        var timber = AssetDatabase.LoadAssetAtPath<Material>(AssetRoot + "/Materials/Honey teak.mat");
        var brass = AssetDatabase.LoadAssetAtPath<Material>(AssetRoot + "/Materials/Safety orange.mat");
        var root = new GameObject("Bow cannon").transform;
        root.SetParent(boat.transform, false);
        root.localPosition = new Vector3(0f, 0.61f, 1.5f);
        Primitive("Timber carriage", PrimitiveType.Cube, root, new Vector3(0f, 0.09f, 0f), new Vector3(0.68f, 0.18f, 0.7f), timber);
        for (int side = -1; side <= 1; side += 2)
        {
            Primitive("Carriage cheek", PrimitiveType.Cube, root, new Vector3(side * 0.27f, 0.26f, 0f), new Vector3(0.14f, 0.3f, 0.52f), timber);
            var pin = Primitive("Brass trunnion", PrimitiveType.Cylinder, root, new Vector3(side * 0.36f, 0.31f, 0f), new Vector3(0.18f, 0.055f, 0.18f), brass);
            pin.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        }
        var barrel = new GameObject("Recoiling barrel").transform;
        barrel.SetParent(root, false); barrel.localPosition = new Vector3(0f, 0.34f, 0f);
        barrel.localRotation = Quaternion.Euler(-8f, 0f, 0f);
        var mesh = SaveMesh(CannonBarrelMesh(), "Cannon barrel");
        MeshObject("Cast iron barrel", mesh, iron, barrel);
        var band = Primitive("Muzzle band", PrimitiveType.Cylinder, barrel, new Vector3(0f, 0f, 0.9f), new Vector3(0.35f, 0.055f, 0.35f), brass);
        band.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        // Dark inset in front of the band keeps the bore readable from the game camera.
        var bore = Primitive("Dark bore", PrimitiveType.Cylinder, barrel, new Vector3(0f, 0f, 0.96f), new Vector3(0.25f, 0.015f, 0.25f), iron);
        bore.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        var muzzle = new GameObject("Muzzle").transform;
        muzzle.SetParent(barrel, false); muzzle.localPosition = new Vector3(0f, 0f, 1.12f);
        var cannon = boat.GetComponent<BoatCannon>();
        if (cannon == null) cannon = boat.gameObject.AddComponent<BoatCannon>();
        cannon.barrel = barrel; cannon.muzzle = muzzle;
        cannon.waterEffects = effects;
        cannon.projectileMesh = SaveMesh(RockMesh(), "Cannonball");
        cannon.projectileMaterial = iron;
        cannon.effectMaterial = effects.sprayMaterial;
    }

    static Mesh CannonBarrelMesh()
    {
        var vertices = new System.Collections.Generic.List<Vector3>();
        var triangles = new System.Collections.Generic.List<int>();
        const int sides = 10;
        for (int i = 0; i < sides; i++)
        {
            float a = i * Mathf.PI * 2f / sides, b = (i + 1) * Mathf.PI * 2f / sides;
            var backA = new Vector3(Mathf.Cos(a) * 0.23f, Mathf.Sin(a) * 0.23f, -0.28f);
            var backB = new Vector3(Mathf.Cos(b) * 0.23f, Mathf.Sin(b) * 0.23f, -0.28f);
            var frontA = new Vector3(Mathf.Cos(a) * 0.15f, Mathf.Sin(a) * 0.15f, 0.94f);
            var frontB = new Vector3(Mathf.Cos(b) * 0.15f, Mathf.Sin(b) * 0.15f, 0.94f);
            Triangle(vertices, triangles, backA, backB, frontA);
            Triangle(vertices, triangles, backB, frontB, frontA);
            Triangle(vertices, triangles, new Vector3(0f, 0f, -0.28f), backB, backA);
        }
        return MeshFrom(vertices, triangles);
    }
}
