using System;
using System.IO;
using NiceBoat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static partial class SummerHarborBuilder
{
    const int AudioRate = 22050;

    [MenuItem("NiceBoat/Refresh Harbor Audio")]
    public static void RefreshAudioFromMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        RefreshAudio();
    }

    public static void RefreshAudio()
    {
        if (SceneManager.GetActiveScene().path != ScenePath) EditorSceneManager.OpenScene(ScenePath);
        var boat = Object.FindFirstObjectByType<BoatController>();
        if (boat == null || boat.GetComponent<BoatCannon>() == null)
            throw new InvalidOperationException("Build the harbor and its cannon first.");
        ConfigureHarborAudio(boat);
        PrefabUtility.SaveAsPrefabAsset(boat.gameObject, AssetRoot + "/Prefabs/PlayerBoat.prefab");
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        Debug.Log("HARBOR_AUDIO_REFRESHED");
    }

    static void ConfigureHarborAudio(BoatController boat)
    {
        EnsureFolder(AssetRoot + "/Audio");
        var audio = boat.GetComponent<HarborAudio>();
        if (audio == null) audio = boat.gameObject.AddComponent<HarborAudio>();
        audio.oceanLoop = BakeAudio("Sheltered sea", 12f, 2, 0, true, 0.64f);
        audio.engineLoop = BakeAudio("Fishing boat engine", 2f, 1, 1, true, 0.65f);
        audio.hullWashLoop = BakeAudio("Water along hull", 4f, 1, 2, true, 0.55f);
        audio.cannonShot = BakeAudio("Bow cannon", 1.2f, 1, 3, false, 0.8f);
        audio.splash = BakeAudio("Cannonball splash", 0.95f, 1, 4, false, 0.72f);
        audio.hullKnock = BakeAudio("Wooden hull knock", 0.5f, 1, 5, false, 0.7f);
        audio.reload = BakeAudio("Cannon breech", 0.32f, 1, 6, false, 0.55f);
    }

    // Deterministic original synthesis; no downloaded recordings or runtime DSP allocation.
    static AudioClip BakeAudio(string name, float duration, int channels, int kind, bool loop, float peak)
    {
        int frames = Mathf.RoundToInt(duration * AudioRate);
        int overlap = loop ? AudioRate / 4 : 0;
        int total = frames + overlap;
        var samples = new float[total * channels];
        for (int channel = 0; channel < channels; channel++)
        {
            var random = new System.Random(1207 + kind * 79 + channel * 451);
            double slow = 0, mid = 0, fast = 0, phase = 0;
            for (int i = 0; i < total; i++)
            {
                double t = (double)i / AudioRate;
                double noise = random.NextDouble() * 2 - 1;
                slow += (noise - slow) * 0.008;
                mid += (noise - mid) * 0.07;
                fast += (noise - fast) * 0.3;
                double sample;
                switch (kind)
                {
                    case 0:
                        double swell = 0.7 + 0.2 * Math.Sin(t * Math.PI / 3 + channel * 0.5) + 0.1 * Math.Sin(t * Math.PI / 2);
                        sample = (slow * 1.6 + mid * 0.6 + fast * 0.15) * swell;
                        break;
                    case 1:
                        double pulse = Math.Pow(0.5 + 0.5 * Math.Sin(t * Math.PI * 2 * 25), 4);
                        sample = Math.Sin(t * Math.PI * 100) * 0.19 + Math.Sin(t * Math.PI * 200) * 0.08
                            + Math.Sin(t * Math.PI * 300) * 0.04 + mid * (0.14 + pulse * 0.45);
                        break;
                    case 2:
                        sample = (mid * 0.75 + fast * 0.28) * (0.7 + 0.2 * Math.Sin(t * Math.PI * 4) + 0.1 * Math.Sin(t * Math.PI * 7));
                        break;
                    case 3:
                        phase += 2 * Math.PI * (43 + 55 * Math.Exp(-t * 14)) / AudioRate;
                        sample = Math.Sin(phase) * Math.Exp(-t * 8) * 0.7 + mid * Math.Exp(-t * 5) * 1.8
                            + noise * Math.Exp(-t * 110) * 0.42 + slow * Math.Exp(-t * 3) * 0.8;
                        break;
                    case 4:
                        phase += 2 * Math.PI * (80 + 130 * Math.Exp(-t * 10)) / AudioRate;
                        double splashEnvelope = (1 - Math.Exp(-t * 55)) * Math.Exp(-t * 5);
                        sample = (mid * 1.8 + fast * 0.5 + Math.Sin(phase) * Math.Exp(-t * 18) * 0.4) * splashEnvelope;
                        break;
                    case 5:
                        sample = (Math.Sin(t * Math.PI * 2 * 145) * 0.55 + Math.Sin(t * Math.PI * 2 * 231) * 0.18) * Math.Exp(-t * 24)
                            + mid * Math.Exp(-t * 40) * 1.5;
                        break;
                    default:
                        sample = 0;
                        for (int tap = 0; tap < 3; tap++)
                        {
                            double age = t - 0.02 - tap * 0.085;
                            if (age >= 0) sample += (Math.Sin(age * Math.PI * 2 * (1050 + tap * 170)) * 0.12 + fast * 0.7) * Math.Exp(-age * 65) * Math.Min(age * 900, 1);
                        }
                        break;
                }
                if (!loop) sample *= Math.Min(t * 500, 1) * Math.Min((duration - t) * 80, 1);
                samples[i * channels + channel] = (float)sample;
            }
        }
        if (loop)
        {
            // Overlap-add the tail into the opening, then discard the consumed opening.
            // The seam follows adjacent samples instead of fading to silence each cycle.
            for (int i = 0; i < overlap; i++)
            {
                float blend = Mathf.SmoothStep(0f, 1f, (float)i / (overlap - 1));
                for (int channel = 0; channel < channels; channel++)
                {
                    int tail = (frames + i) * channels + channel;
                    samples[tail] = Mathf.Lerp(samples[tail], samples[i * channels + channel], blend);
                }
            }
        }
        int start = overlap * channels;
        float maximum = 0.0001f;
        for (int i = start; i < samples.Length; i++) maximum = Mathf.Max(maximum, Mathf.Abs(samples[i]));
        string path = AssetRoot + "/Audio/" + name + ".wav";
        using (var writer = new BinaryWriter(File.Create(path)))
        {
            int byteCount = frames * channels * 2;
            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + byteCount);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16);
            writer.Write((short)1); writer.Write((short)channels); writer.Write(AudioRate);
            writer.Write(AudioRate * channels * 2); writer.Write((short)(channels * 2)); writer.Write((short)16);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(byteCount);
            for (int i = start; i < samples.Length; i++) writer.Write((short)Mathf.RoundToInt(samples[i] / maximum * peak * short.MaxValue));
        }
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = (AudioImporter)AssetImporter.GetAtPath(path);
        importer.forceToMono = channels == 1; importer.loadInBackground = false;
        var settings = importer.defaultSampleSettings;
        settings.loadType = AudioClipLoadType.DecompressOnLoad;
        settings.preloadAudioData = true;
        settings.compressionFormat = AudioCompressionFormat.PCM;
        settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
        importer.defaultSampleSettings = settings;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
    }
}
