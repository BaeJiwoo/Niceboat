using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

// Authored proportions and deterministic variation keep rebuilds reproducible.
public static partial class SummerHarborBuilder
{
    static Material timberDark, timberLight, thatch, plaster, pathPaint;
    static Mesh postMesh, roofMesh, barrelMesh;
    static Color Hex(string value) { ColorUtility.TryParseHtmlString("#" + value, out var c); return c; }

    static void PrepareHarborArt()
    {
        timberDark = Lit("Weathered dark timber", Hex("574332"));
        timberLight = Lit("Sunbleached timber", Hex("9C8157"));
        thatch = Lit("Ochre thatch", Hex("71502F"));
        plaster = Lit("Ochre plaster", Hex("C6AB76"));
        pathPaint = Lit("Footpath sand", Hex("CCB87B"));
        postMesh = SaveMesh(TaperedPostMesh(7), "Seven sided timber");
        roofMesh = SaveMesh(ThatchedRoofMesh(), "Hand cut thatch roof");
        barrelMesh = SaveMesh(BarrelMesh(), "Coopered barrel");
    }

    static void ConfigureHarborWater(Material material)
    {
        material.SetTexture("_PackedSurface", CreateHarborSurface());
        material.SetFloat("_WaveAmplitude", 0.13f);
        material.SetFloat("_WaveLength", 18f);
        material.SetFloat("_WaveSpeed", 0.8f);
        material.SetFloat("_WorldUVScale", 0.055f);
        material.SetVector("_SurfaceScroll", new Vector4(0.006f, 0.004f, 0, 0));
        material.SetFloat("_DepthDistance", 9f);
        material.SetFloat("_ShallowOpacity", 0.73f);
        material.SetFloat("_DeepOpacity", 1f);
        material.SetFloat("_FoamWidth", 0.16f);
        material.SetFloat("_FoamBreakup", 0.9f);
        material.SetFloat("_RippleStrength", 0.24f);
        material.SetFloat("_RippleThreshold", 0.36f);
        material.SetFloat("_RippleNormalStrength", 0.035f);
        material.SetFloat("_SpecularIntensity", 0.12f);
        material.SetFloat("_SpecularBreakup", 1f);
        material.SetFloat("_SpecularSurfaceMask", 1f);
        material.SetFloat("_ReceiveWaterShadows", 1f);
        material.EnableKeyword("_WATER_SHADOWS");
        material.SetFloat("_MidtoneBrightness", 0.96f);
        material.SetFloat("_SunColorInfluence", 0.06f);
        material.SetFloat("_LightThreshold", 0.65f);
        material.SetColor("_ShallowColor", Hex("99AD99"));
        material.SetColor("_DeepColor", Hex("4E655D"));
        material.SetColor("_ShadowColor", Hex("43564D"));
        material.SetColor("_RippleColor", Hex("B9C3A9"));
        material.SetColor("_FoamColor", Hex("DADCC0"));
        material.SetColor("_SpecularColor", Hex("C7C8A6"));
    }

    static Texture2D CreateHarborSurface()
    {
        EnsureFolder(AssetRoot + "/Textures");
        const int size = 512;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false, true);
        var pixels = new Color[size * size];
        for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
        {
            float u = x * Mathf.PI * 2f / size, v = y * Mathf.PI * 2f / size;
            float noise = 0.5f + 0.19f * Mathf.Sin(u * 3 + v * 2) + 0.14f * Mathf.Cos(v * 5 - u * 2) + 0.1f * Mathf.Sin(u * 7 + v * 9);
            pixels[y * size + x] = new Color(0.5f + Mathf.Cos(u * 3 + v * 2) * 0.22f,
                0.5f + Mathf.Sin(v * 5 - u * 2) * 0.22f, 0, noise);
        }
        var random = new System.Random(381);
        // Sparse tapered brush strokes, with periodic addressing on every edge.
        for (int i = 0; i < 155; i++)
        {
            int cx = random.Next(size), cy = random.Next(size);
            float length = 4f + (float)random.NextDouble() * 13f;
            for (int dy = -5; dy <= 5; dy++) for (int dx = -20; dx <= 20; dx++)
            {
                float along = dx / length;
                float width = Mathf.Clamp01(1f - along * along) * 1.05f;
                float across = Mathf.Abs(dy - dx * 0.16f - 0.3f * Mathf.Sin(dx * 0.6f));
                float stroke = Mathf.Clamp01((width - across) * 1.8f) * Mathf.Clamp01((1f - Mathf.Abs(along)) * 7f);
                int index = ((cy + dy + size) % size) * size + (cx + dx + size) % size;
                var c = pixels[index]; c.b = Mathf.Max(c.b, stroke); pixels[index] = c;
            }
        }
        texture.SetPixels(pixels); texture.Apply();
        string path = AssetRoot + "/Textures/Harbor surface.png";
        File.WriteAllBytes(path, texture.EncodeToPNG()); Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.sRGBTexture = false; importer.mipmapEnabled = true; importer.wrapMode = TextureWrapMode.Repeat;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.filterMode = FilterMode.Trilinear; importer.anisoLevel = 2; importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static Mesh SculptedIslandMesh()
    {
        const int sides = 22;
        float[] radii = { 0.001f, 0.35f, 0.67f, 0.82f, 1.0f, 1.17f, 1.48f, 2.1f };
        float[] heights = { 2.8f, 2.65f, 2.25f, 1.2f, 0.2f, -1.25f, -4.8f, -8f };
        var rings = new Vector3[radii.Length, sides];
        var vertices = new List<Vector3>(); var colors = new List<Color>();
        var faces = new[] { new List<int>(), new List<int>(), new List<int>() };
        for (int r = 0; r < radii.Length; r++) for (int s = 0; s < sides; s++)
        {
            float angle = s * 2f * Mathf.PI / sides;
            float edge = 1f + 0.13f * Mathf.Sin(angle * 3f + 0.7f) + 0.07f * Mathf.Cos(angle * 7f);
            float shelf = s >= 2 && s <= 9 && r == 3 ? 0.9f : 0f;
            rings[r,s] = new Vector3(Mathf.Cos(angle) * radii[r] * 13f * edge,
                heights[r] + Mathf.Sin(angle * 3f) * 0.19f * Mathf.Min(1f,radii[r]) + shelf,
                Mathf.Sin(angle) * radii[r] * 10.8f * edge);
        }
        for (int r = 0; r < radii.Length - 1; r++) for (int s = 0; s < sides; s++)
        {
            int next = (s + 1) % sides;
            int material = r < 2 ? 0 : (r == 3 && s >= 2 && s <= 9 ? 2 : 1);
            float tint = r < 2 ? 0.93f + 0.07f * Mathf.Sin(s * 3.7f + r) : (r >= 4 ? 1f : 0.95f + 0.05f * Mathf.Cos(s * 2.1f));
            PaintedTriangle(vertices, faces[material], colors, rings[r,s], rings[r,next], rings[r+1,s], tint);
            PaintedTriangle(vertices, faces[material], colors, rings[r,next], rings[r+1,next], rings[r+1,s], tint);
        }
        var mesh = new Mesh { subMeshCount = 3 };
        mesh.SetVertices(vertices); mesh.SetColors(colors);
        for (int i = 0; i < faces.Length; i++) mesh.SetTriangles(faces[i],i);
        mesh.RecalculateNormals();
        // Submerged slopes share a calm value instead of producing large triangular light patches.
        var normals=mesh.normals;
        for(int i=0;i<vertices.Count;i++) if(vertices[i].y < -0.1f) normals[i]=Vector3.up;
        mesh.normals=normals;
        mesh.RecalculateBounds(); return mesh;
    }

    static void HarborSettlement(Transform world, MeshCollider island)
    {
        // A navigable inlet frames the boat; the buildings have solid foundations.
        Jetty(new Vector3(-0.8f, 1.05f, 11f), new Vector3(-5.5f, 1.05f, -0.8f), 2.25f, "Landing pier", world);
        Jetty(new Vector3(-3.6f, 1.05f, 4f), new Vector3(-13f, 1.05f, 7f), 1.5f, "West boardwalk", world);
        Jetty(new Vector3(-13f, 1.05f, 7f), new Vector3(-11.5f, 1.05f, 17f), 1.5f, "West walkway", world);
        Hut(new Vector3(-10.8f, 1.05f, 10.8f), new Vector3(0.85f, 0.92f, 0.95f), 10, world, "Net mender's hut");
        Hut(new Vector3(-7.5f, 1.05f, 20.8f), new Vector3(1, 1.05f, 1), -12, world, "Old fishery");
        Jetty(new Vector3(-11.5f, 1.05f, 17f), new Vector3(-7.5f, 1.05f, 18.5f), 1.5f, "Fishery landing", world);
        var hillHouse = new Vector3(8.5f, IslandHeight(island,8.5f,15.5f) + 0.12f,15.5f);
        Hut(hillHouse, new Vector3(1.05f,1,1.1f), -8,world,"Harbormaster's cottage");
        Jetty(new Vector3(19f,1.0f,15f),new Vector3(20f,1.0f,3.5f),1.6f,"East fishing pier",world);
        Hut(new Vector3(21.8f,1.0f,9f),new Vector3(0.9f,0.88f,0.9f),-18,world,"Tide house");
        Jetty(new Vector3(20f,1.0f,5.8f),new Vector3(21.7f,1.0f,6.8f),1.5f,"Tide house landing",world);
        Lighthouse(new Vector3(11f,IslandHeight(island,11f,22f),22f),world);
        for (int i = 0; i < 16; i++)
        {
            float angle = i * 2.399f;
            var rock = MeshObject("Broken shoreline boulder",rockMesh,i % 3 == 0 ? timberDark : stone,world);
            rock.transform.position = IslandCenter + new Vector3(Mathf.Cos(angle) * (12.5f + i % 3), -0.15f + i % 3 * 0.2f, Mathf.Sin(angle) * (10.5f + i % 2));
            rock.transform.localRotation = Quaternion.Euler(0,i * 41,0);
            rock.transform.localScale = new Vector3(1.25f + i % 3 * 0.42f,0.9f + i % 4 * 0.24f,1.1f + i % 2 * 0.6f);
            var c = rock.AddComponent<MeshCollider>(); c.sharedMesh = rockMesh; c.convex = true;
        }
        for (int i = 0; i < 11; i++)
        {
            float x = 1.1f + i * 0.7f, z = 10.4f + i * 0.39f;
            var step = MeshObject("Worn path stone",rockMesh,pathPaint,world);
            step.transform.position = new Vector3(x,IslandHeight(island,x,z)+0.025f,z);
            step.transform.localScale = new Vector3(0.65f,0.035f,0.4f); step.transform.localRotation = Quaternion.Euler(0,i*27,0);
        }
        var underwaterStone=Lit("Submerged green stone",Hex("869382"));
        for(int i=0;i<27;i++)
        {
            float x=-2f+(i%9)*2.3f+Mathf.Sin(i*7.3f)*0.7f;
            float z=-0.5f+(i/9)*1.9f+Mathf.Cos(i*4.1f)*0.8f;
            var b=MeshObject("Submerged reef stone",rockMesh,underwaterStone,world);
            b.transform.position=new Vector3(x,-1.1f-(i%3)*0.25f,z);
            b.transform.localScale=new Vector3(0.7f+(i%3)*0.3f,0.4f,0.6f+(i%4)*0.2f);
            b.transform.localRotation=Quaternion.Euler(0,i*43,0);
            var c=b.AddComponent<MeshCollider>();c.sharedMesh=rockMesh;c.convex=true;
        }
        Crate(new Vector3(-4.6f,1.17f,0.8f),world,0.55f);
        Barrel(new Vector3(-4.4f,1.16f,2.2f),world,0.65f);
        // Offshore silhouettes give the playable harbor a coastline beyond the hero island.
        for (int i = 0; i < 7; i++)
        {
            var far = MeshObject("Distant headland",rockMesh,stone,world);
            far.transform.position = new Vector3(-66f + i * 17f,-2.6f,69f + i%3 * 9f);
            far.transform.localScale = new Vector3(13 + i%2*5,4+i%3*1.5f,7+i%3*2);
        }
    }

    static void Jetty(Vector3 start, Vector3 end, float width, string name, Transform parent)
    {
        var root = new GameObject(name).transform; root.SetParent(parent,false); root.position = start;
        Vector3 delta = end - start; float length = delta.magnitude;
        root.rotation = Quaternion.LookRotation(delta.normalized,Vector3.up);
        int count = Mathf.CeilToInt(length / 0.55f);
        for (int i = 0; i <= count; i++)
        {
            var board = Primitive("Uneven deck plank",PrimitiveType.Cube,root,
                new Vector3(Mathf.Sin(i*12.7f)*0.04f,Mathf.Sin(i*5.3f)*0.018f,i*length/count),
                new Vector3(width + Mathf.Sin(i*7.2f)*0.14f,0.18f,length/count*0.93f),i%4==0 ? timberLight : wood);
            board.transform.localRotation = Quaternion.Euler(0,Mathf.Sin(i*4.2f)*1.3f,0);
        }
        var solid = root.gameObject.AddComponent<BoxCollider>(); solid.center = new Vector3(0,0,length*0.5f); solid.size = new Vector3(width,0.23f,length+0.5f);
        for (int i = 0; i <= count; i += 4) for (int side = -1; side <= 1; side+=2)
        {
            var post = MeshObject("Salt worn piling",postMesh,timberDark,root);
            post.transform.localPosition = new Vector3(side*(width*0.5f-0.1f),-1.05f,i*length/count);
            post.transform.localScale = new Vector3(0.26f,3.65f,0.26f);
            var c = post.AddComponent<BoxCollider>(); c.size = Vector3.one;
            if (i + 4 <= count && i%8==0)
                Beam(root,new Vector3(side*width*0.48f,0.58f,i*length/count),new Vector3(side*width*0.48f,0.63f,(i+4)*length/count),0.1f,timberLight);
        }
        for (int side=-1;side<=1;side+=2)
            Beam(root,new Vector3(side*width*0.33f,-0.2f,0),new Vector3(side*width*0.33f,-0.2f,length),0.2f,timberDark);
    }

    static void Hut(Vector3 position, Vector3 scale, float yaw, Transform parent, string name)
    {
        var root = new GameObject(name).transform; root.SetParent(parent,false); root.position=position;
        root.localScale=scale; root.rotation=Quaternion.Euler(0,yaw,0);
        Primitive("Raised porch",PrimitiveType.Cube,root,new Vector3(0,0.05f,0),new Vector3(4.9f,0.22f,5.3f),wood,true);
        Primitive("Plaster wall",PrimitiveType.Cube,root,new Vector3(0,1.45f,0.2f),new Vector3(3.9f,2.65f,3.9f),plaster,true);
        Primitive("Dark foundation",PrimitiveType.Cube,root,new Vector3(0,0.36f,0.2f),new Vector3(4.04f,0.38f,4.02f),timberDark);
        foreach (float x in new[] {-2.1f,2.1f}) foreach (float z in new[] {-2.3f,0f,2.3f})
        {
            var p = MeshObject("House stilt",postMesh,timberDark,root);
            p.transform.localPosition=new Vector3(x,-1.35f,z); p.transform.localScale=new Vector3(0.28f,3.5f,0.28f);
            p.AddComponent<BoxCollider>().size=Vector3.one;
        }
        for (int side=-1;side<=1;side+=2)
        {
            for (int i=0;i<4;i++)
            {
                float z=-1.45f+i*1.06f;
                Primitive("Side timber frame",PrimitiveType.Cube,root,new Vector3(side*1.98f,1.55f,z),new Vector3(0.13f,2.45f,0.13f),timberDark);
                if(i<3) Primitive("Shaded side window",PrimitiveType.Cube,root,new Vector3(side*1.97f,1.68f,z+0.49f),new Vector3(0.06f,0.88f,0.7f),navy);
            }
            Beam(root,new Vector3(side*2.03f,0.62f,-1.7f),new Vector3(side*2.03f,1.42f,-0.7f),0.11f,wood);
            Primitive("Front corner post",PrimitiveType.Cube,root,new Vector3(side*1.9f,1.55f,-1.8f),new Vector3(0.18f,2.45f,0.18f),timberDark);
            Primitive("Front window",PrimitiveType.Cube,root,new Vector3(side*1.12f,1.74f,-1.77f),new Vector3(0.72f,0.8f,0.08f),navy);
            Primitive("Window lintel",PrimitiveType.Cube,root,new Vector3(side*1.12f,2.19f,-1.84f),new Vector3(0.94f,0.11f,0.12f),timberLight);
            Primitive("Window sill",PrimitiveType.Cube,root,new Vector3(side*1.12f,1.3f,-1.85f),new Vector3(0.92f,0.14f,0.18f),wood);
        }
        Primitive("Door recess",PrimitiveType.Cube,root,new Vector3(0,1.27f,-1.79f),new Vector3(0.88f,1.95f,0.09f),timberDark);
        for(int i=0;i<4;i++) Primitive("Door plank",PrimitiveType.Cube,root,new Vector3(-0.28f+i*0.19f,1.23f,-1.85f),new Vector3(0.17f,1.74f,0.07f),wood);
        var roof=MeshObject("Layered thatch",roofMesh,thatch,root); roof.transform.localPosition=Vector3.up*2.7f;
        Beam(root,new Vector3(-1.8f,2.62f,-1.9f),new Vector3(1.8f,2.62f,-1.9f),0.18f,timberDark);
        Crate(new Vector3(-1.8f,0.18f,-2.1f),root,0.65f);
        Barrel(new Vector3(1.8f,0.18f,-2f),root,0.68f);
        var sign=Primitive("Fishery sign",PrimitiveType.Cube,root,new Vector3(0,2.63f,-2.04f),new Vector3(1.2f,0.35f,0.09f),timberLight);
        Primitive("Sign fish mark",PrimitiveType.Cube,sign.transform,Vector3.zero,new Vector3(0.47f,0.38f,1.25f),timberDark).transform.localRotation=Quaternion.Euler(0,0,-12);
    }

    static void DressFishingBoat(Transform root)
    {
        root.Find("Turquoise roof").GetComponent<Renderer>().sharedMaterial=timberDark;
        root.Find("Roof trim").GetComponent<Renderer>().sharedMaterial=teal;
        root.Find("Red gunwale").GetComponent<Renderer>().sharedMaterial=wood;
        root.Find("Mast").localScale=new Vector3(0.055f,0.78f,0.055f);
        root.Find("Mast").localPosition=new Vector3(0,2.25f,-0.52f);
        root.Find("Summer pennant").localPosition=new Vector3(0,2.95f,-0.52f);
        Primitive("Cabin front mullion",PrimitiveType.Cube,root,new Vector3(0,1.16f,0.94f),new Vector3(0.075f,0.49f,0.07f),cream);
        Primitive("Chimney",PrimitiveType.Cube,root,new Vector3(-0.52f,1.95f,-0.3f),new Vector3(0.23f,0.76f,0.25f),navy);
        Primitive("Chimney lip",PrimitiveType.Cube,root,new Vector3(-0.52f,2.34f,-0.3f),new Vector3(0.3f,0.08f,0.31f),wood);
        for(int side=-1;side<=1;side+=2)
        {
            Beam(root,new Vector3(side*0.76f,0.6f,1.25f),new Vector3(side*0.76f,0.97f,1.25f),0.045f,cream);
            Beam(root,new Vector3(side*0.76f,0.97f,1.25f),new Vector3(side*0.12f,1.04f,2.29f),0.045f,cream);
            Beam(root,new Vector3(0,2.8f,-0.52f),new Vector3(side*0.82f,0.64f,-1.6f),0.016f,navy);
        }
        Crate(new Vector3(-0.38f,0.61f,-1.33f),root,0.48f);
        var rope=MeshObject("Coiled deck rope",ringMesh,timberLight,root);
        rope.transform.localPosition=new Vector3(0.4f,0.64f,-1.45f); rope.transform.localScale=new Vector3(0.65f,0.25f,0.65f);
        for(int i=0;i<3;i++) Primitive("Hull rubbing strake",PrimitiveType.Cube,root,new Vector3(-0.99f,0.08f+i*0.13f,-0.43f),new Vector3(0.035f,0.025f,2.25f),wood);
    }

    static void Crate(Vector3 p,Transform parent,float size)
    {
        var root=new GameObject("Fish crate").transform; root.SetParent(parent,false); root.localPosition=p; root.localScale=Vector3.one*size;
        Primitive("Dark interior",PrimitiveType.Cube,root,new Vector3(0,0.43f,0),new Vector3(0.9f,0.82f,0.9f),timberDark);
        for(int side=-1;side<=1;side+=2) for(int i=0;i<3;i++)
        {
            Primitive("Crate slat",PrimitiveType.Cube,root,new Vector3(side*0.48f,0.18f+i*0.28f,0),new Vector3(0.11f,0.22f,1.03f),timberLight);
            Primitive("Crate slat",PrimitiveType.Cube,root,new Vector3(0,0.18f+i*0.28f,side*0.48f),new Vector3(0.9f,0.22f,0.1f),wood);
        }
    }

    static void Barrel(Vector3 p,Transform parent,float size)
    {
        var b=MeshObject("Wooden barrel",barrelMesh,wood,parent); b.transform.localPosition=p+Vector3.up*size*0.65f; b.transform.localScale=new Vector3(size,size*1.3f,size);
        for(int i=-1;i<=1;i+=2)
        {
            var hoop=MeshObject("Iron barrel hoop",ringMesh,navy,b.transform);
            hoop.transform.localPosition=new Vector3(0,i*0.28f,0); hoop.transform.localScale=new Vector3(0.88f,0.32f,0.88f);
        }
    }

    static void Beam(Transform parent,Vector3 a,Vector3 b,float width,Material material)
    {
        var go=Primitive("Timber brace",PrimitiveType.Cube,parent,(a+b)*0.5f,new Vector3(width,(b-a).magnitude,width),material);
        go.transform.localRotation=Quaternion.FromToRotation(Vector3.up,(b-a).normalized);
    }

    static Mesh TaperedPostMesh(int sides)
    {
        var v=new List<Vector3>(); var t=new List<int>(); var colors=new List<Color>();
        for(int i=0;i<sides;i++)
        {
            float a=i*Mathf.PI*2/sides,b=(i+1)*Mathf.PI*2/sides;
            Vector3 p=new(Mathf.Cos(a)*0.5f,-0.5f,Mathf.Sin(a)*0.5f), q=new(Mathf.Cos(b)*0.5f,-0.5f,Mathf.Sin(b)*0.5f);
            Vector3 r=new(p.x*0.82f+0.04f,0.5f,p.z*0.82f),s=new(q.x*0.82f+0.04f,0.5f,q.z*0.82f);
            float tone=0.9f+i%3*0.05f;
            PaintedTriangle(v,t,colors,p,r,q,tone); PaintedTriangle(v,t,colors,q,r,s,tone);
            PaintedTriangle(v,t,colors,new Vector3(0,0.5f,0),s,r,1.06f);
        }
        var mesh=MeshFrom(v,t); mesh.SetColors(colors); return mesh;
    }

    static Mesh ThatchedRoofMesh()
    {
        Vector3[] edge={ new(-2.3f,0,-2.5f),new(0,0,-2.65f),new(2.3f,0,-2.5f),new(2.55f,0,0),new(2.3f,0,2.5f),new(0,0,2.65f),new(-2.3f,0,2.5f),new(-2.55f,0,0) };
        var v=new List<Vector3>(); var t=new List<int>(); var colors=new List<Color>();
        for(int face=0;face<edge.Length;face++)
        {
            Vector3 a=edge[face],b=edge[(face+1)%edge.Length];
            Vector3 peak=new(0.13f,2.05f,0.24f);
            const int strips=9;
            for(int s=0;s<strips;s++)
            {
                Vector3 l=Vector3.Lerp(a,b,(float)s/strips), r=Vector3.Lerp(a,b,(float)(s+1)/strips);
                l.y-=0.13f+0.12f*Mathf.Sin(s*17.3f+face); r.y-=0.13f+0.12f*Mathf.Sin((s+1)*17.3f+face);
                float tone=0.84f+0.18f*(0.5f+0.5f*Mathf.Sin(s*7.9f+face*3));
                PaintedTriangle(v,t,colors,peak,r,l,tone);
                // A second, shorter course leaves an irregular thatched eave.
                Vector3 midL=Vector3.Lerp(peak,l,0.68f)+Vector3.up*0.065f;
                Vector3 midR=Vector3.Lerp(peak,r,0.68f)+Vector3.up*0.065f;
                Vector3 endL=l*1.045f-Vector3.up*0.14f,endR=r*1.045f-Vector3.up*0.14f;
                PaintedTriangle(v,t,colors,midL,midR,endL,tone*0.94f);
                PaintedTriangle(v,t,colors,endL,midR,endR,tone*0.94f);
                PaintedTriangle(v,t,colors,l,r,Vector3.Lerp(l,r,0.5f)+Vector3.down*0.24f,tone*0.78f);
            }
        }
        var mesh=MeshFrom(v,t); mesh.SetColors(colors); return mesh;
    }

    static Mesh AngularRockMesh()
    {
        Vector3[] baseRing={new(-1,-0.5f,-0.7f),new(0.45f,-0.5f,-1),new(1,-0.5f,-0.1f),new(0.62f,-0.5f,0.8f),new(-0.7f,-0.5f,0.85f)};
        Vector3[] top={new(-0.75f,0.35f,-0.5f),new(0.2f,0.65f,-0.6f),new(0.72f,0.32f,0.02f),new(0.35f,0.58f,0.48f),new(-0.55f,0.45f,0.6f)};
        var v=new List<Vector3>();var t=new List<int>();var colors=new List<Color>();
        for(int i=0;i<5;i++)
        {
            int n=(i+1)%5;float tint=0.88f+i*0.035f;
            PaintedTriangle(v,t,colors,baseRing[i],top[i],baseRing[n],tint);
            PaintedTriangle(v,t,colors,baseRing[n],top[i],top[n],tint);
            PaintedTriangle(v,t,colors,new Vector3(0,0.56f,0),top[n],top[i],1f);
            PaintedTriangle(v,t,colors,new Vector3(0,-0.5f,0),baseRing[i],baseRing[n],0.8f);
        }
        var mesh=MeshFrom(v,t);mesh.SetColors(colors);return mesh;
    }

    static Mesh BarrelMesh()
    {
        var v=new List<Vector3>();var t=new List<int>();var colors=new List<Color>();const int sides=10;
        for(int i=0;i<sides;i++)
        {
            float a=i*Mathf.PI*2/sides,b=(i+1)*Mathf.PI*2/sides;
            for(int ring=0;ring<2;ring++)
            {
                float y=ring==0?-0.5f:0, ny=ring==0?0:0.5f, r=ring==0?0.4f:0.49f,nr=ring==0?0.49f:0.4f;
                Vector3 p=new(Mathf.Cos(a)*r,y,Mathf.Sin(a)*r),q=new(Mathf.Cos(b)*r,y,Mathf.Sin(b)*r);
                Vector3 u=new(Mathf.Cos(a)*nr,ny,Mathf.Sin(a)*nr),w=new(Mathf.Cos(b)*nr,ny,Mathf.Sin(b)*nr);
                float tint=0.86f+i%3*0.07f;PaintedTriangle(v,t,colors,p,u,q,tint);PaintedTriangle(v,t,colors,q,u,w,tint);
            }
            PaintedTriangle(v,t,colors,new Vector3(0,0.5f,0),new Vector3(Mathf.Cos(b)*0.4f,0.5f,Mathf.Sin(b)*0.4f),new Vector3(Mathf.Cos(a)*0.4f,0.5f,Mathf.Sin(a)*0.4f),0.9f);
        }
        var mesh=MeshFrom(v,t);mesh.SetColors(colors);return mesh;
    }

    static void PaintedTriangle(List<Vector3> v,List<int> t,List<Color> c,Vector3 a,Vector3 b,Vector3 d,float tint)
    {
        Triangle(v,t,a,b,d);var color=new Color(tint,tint,tint,1);c.Add(color);c.Add(color);c.Add(color);
    }

    static void CombineEnvironmentMeshes(Transform world)
    {
        // Keep the editable hierarchy/colliders, but render static decoration in material groups.
        var groups=new Dictionary<Material,List<CombineInstance>>();
        var whiteCopies=new Dictionary<Mesh,Mesh>();
        foreach(var renderer in world.GetComponentsInChildren<MeshRenderer>())
        {
            if(renderer.name=="Summer ocean" || renderer.name=="Sandy seabed" || renderer.name=="Distant headland")continue;
            var filter=renderer.GetComponent<MeshFilter>(); if(filter==null || filter.sharedMesh==null)continue;
            Mesh source=filter.sharedMesh;
            if(source.colors.Length==0)
            {
                if(!whiteCopies.TryGetValue(source,out var copy))
                {
                    copy=Object.Instantiate(source); var colors=new Color[copy.vertexCount];
                    Array.Fill(colors,Color.white); copy.colors=colors; whiteCopies.Add(source,copy);
                }
                source=copy;
            }
            var materials=renderer.sharedMaterials;
            for(int s=0;s<source.subMeshCount;s++)
            {
                var material=materials[Mathf.Min(s,materials.Length-1)];
                if(!groups.TryGetValue(material,out var list)){list=new List<CombineInstance>();groups.Add(material,list);}
                list.Add(new CombineInstance{mesh=source,subMeshIndex=s,transform=world.worldToLocalMatrix*renderer.localToWorldMatrix});
            }
            renderer.enabled=false;
        }
        foreach(var group in groups)
        {
            int vertexCount=0; foreach(var instance in group.Value)vertexCount+=instance.mesh.vertexCount;
            var mesh=new Mesh {indexFormat=vertexCount > 65535 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16};
            mesh.CombineMeshes(group.Value.ToArray(),true,true,false);
            MeshObject("Batched environment - "+group.Key.name,SaveMesh(mesh,"Environment "+group.Key.name),group.Key,world);
        }
        foreach(var copy in whiteCopies.Values)Object.DestroyImmediate(copy);
    }
}
