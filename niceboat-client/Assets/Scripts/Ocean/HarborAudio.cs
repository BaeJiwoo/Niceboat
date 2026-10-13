using UnityEngine;
using UnityEngine.InputSystem;

namespace NiceBoat
{
    [RequireComponent(typeof(BoatController), typeof(BoatCannon))]
    public sealed class HarborAudio : MonoBehaviour
    {
        public AudioClip oceanLoop, engineLoop, hullWashLoop;
        public AudioClip cannonShot, splash, hullKnock, reload;
        [Range(0f, 1f)] public float masterVolume = 0.8f;
        [Range(0f, 1f)] public float ambienceVolume = 0.26f;
        [Range(0f, 1f)] public float engineVolume = 0.23f;
        [Range(0f, 1f)] public float effectsVolume = 0.85f;
        public const int VoiceCapacity = 12;
        readonly AudioSource[] voices = new AudioSource[VoiceCapacity];
        readonly float[] voiceStarted = new float[VoiceCapacity];
        AudioSource sea, engine, wash;
        BoatController boat;
        BoatCannon cannon;
        float throttle, nextCollision, reloadAt = -1f;
        public bool Muted { get; private set; }
        public float EnginePitch => engine != null ? engine.pitch : 0f;
        public int ShotSounds { get; private set; }
        public int ImpactSounds { get; private set; }
        public int CollisionSounds { get; private set; }
        public int ReloadSounds { get; private set; }
        public int ActiveVoiceCount
        {
            get { int count = 0; foreach (var voice in voices) if (voice != null && voice.isPlaying) count++; return count; }
        }

        void Awake()
        {
            boat = GetComponent<BoatController>(); cannon = GetComponent<BoatCannon>();
            sea = CreateSource("Ocean ambience", false);
            engine = CreateSource("Boat engine", false);
            wash = CreateSource("Hull wash", false);
            SetLoop(sea, oceanLoop); SetLoop(engine, engineLoop); SetLoop(wash, hullWashLoop);
            for (int i = 0; i < voices.Length; i++) voices[i] = CreateSource("Harbor sound voice " + i, true);
        }

        void OnEnable()
        {
            cannon.Fired += OnFired; cannon.Impacted += OnImpact;
            if (sea.clip != null) sea.Play();
            if (engine.clip != null) engine.Play();
            if (wash.clip != null) wash.Play();
        }

        AudioSource CreateSource(string label, bool spatial)
        {
            var child = new GameObject(label);
            child.transform.SetParent(transform, false);
            var source = child.AddComponent<AudioSource>();
            source.playOnAwake = false; source.dopplerLevel = 0f;
            source.spatialBlend = spatial ? 0.85f : 0f;
            source.rolloffMode = AudioRolloffMode.Linear;
            // The listener lives on the elevated follow camera, not on the boat.
            source.minDistance = 18f; source.maxDistance = 115f;
            source.spread = 45f; source.reverbZoneMix = 0f;
            source.priority = spatial ? 80 : 160;
            source.volume = 0f;
            return source;
        }

        static void SetLoop(AudioSource source, AudioClip clip) { source.clip = clip; source.loop = true; }

        void Update()
        {
            if (Keyboard.current != null && Keyboard.current.mKey.wasPressedThisFrame) SetMuted(!Muted);
            float wet = boat.Floating != null ? Mathf.Clamp01(boat.Floating.WetFraction) : 0f;
            float speed = Mathf.Clamp01(boat.Speed / Mathf.Max(boat.topSpeed, 0.1f));
            throttle = Mathf.MoveTowards(throttle, Mathf.Max(Mathf.Abs(boat.Control.y), speed * 0.8f), Time.deltaTime * 1.5f);
            sea.volume = Mathf.MoveTowards(sea.volume, ambienceVolume * masterVolume, Time.unscaledDeltaTime * 0.25f);
            engine.pitch = Mathf.Lerp(0.72f, 1.48f, throttle);
            engine.volume = Mathf.MoveTowards(engine.volume, engineVolume * masterVolume * Mathf.Lerp(0.24f, 1f, throttle), Time.deltaTime * 0.4f);
            wash.pitch = Mathf.Lerp(0.85f, 1.2f, speed);
            wash.volume = Mathf.MoveTowards(wash.volume, speed * wet * 0.2f * masterVolume, Time.deltaTime * 0.4f);
            if (reloadAt >= 0f && Time.time >= reloadAt)
            {
                Play(reload, transform.position, 0.28f, 1f);
                reloadAt = -1f; ReloadSounds++;
            }
            // One-shot sources stay at their impact position even while the listener's boat moves.
            for (int i = 0; i < voices.Length; i++)
                if (voices[i].isPlaying) voices[i].transform.position = voicePositions[i];
        }

        readonly Vector3[] voicePositions = new Vector3[VoiceCapacity];

        void OnFired(Vector3 point)
        {
            Play(cannonShot, point, 1f, Random.Range(0.96f, 1.04f));
            ShotSounds++;
            reloadAt = Time.time + Mathf.Max(0.1f, cannon.reloadSeconds) * 0.8f;
        }

        void OnImpact(Vector3 point, bool water)
        {
            Play(water ? splash : hullKnock, point, water ? 0.7f : 0.65f, Random.Range(0.9f, 1.08f));
            ImpactSounds++;
        }

        public void PlayEnemyBurst(Vector3 point)
        {
            if (!isActiveAndEnabled) return;
            Play(cannonShot, point, 0.75f, 0.65f);
            Play(splash, point, 0.4f, 0.85f);
        }

        void OnCollisionEnter(Collision collision)
        {
            if (!isActiveAndEnabled) return;
            float speed = collision.relativeVelocity.magnitude;
            if (speed < 0.8f || Time.time < nextCollision || collision.contactCount == 0) return;
            Play(hullKnock, collision.GetContact(0).point, Mathf.Lerp(0.25f, 0.8f, Mathf.Clamp01(speed / 6f)), Random.Range(0.85f, 1.05f));
            nextCollision = Time.time + 0.35f; CollisionSounds++;
        }

        void Play(AudioClip clip, Vector3 position, float volume, float pitch)
        {
            if (clip == null) return;
            int index = 0;
            for (int i = 0; i < voices.Length; i++)
            {
                if (!voices[i].isPlaying) { index = i; break; }
                if (voiceStarted[i] < voiceStarted[index]) index = i;
            }
            var source = voices[index]; source.Stop();
            source.clip = clip; source.pitch = pitch;
            source.volume = volume * effectsVolume * masterVolume;
            source.mute = Muted; source.transform.position = position;
            voicePositions[index] = position; voiceStarted[index] = Time.unscaledTime;
            source.Play();
        }

        public void SetMuted(bool muted)
        {
            Muted = muted;
            if (sea != null) sea.mute = muted;
            if (engine != null) engine.mute = muted;
            if (wash != null) wash.mute = muted;
            foreach (var voice in voices) if (voice != null) voice.mute = muted;
        }

        void OnDisable()
        {
            if (cannon != null) { cannon.Fired -= OnFired; cannon.Impacted -= OnImpact; }
            if (sea != null) sea.Stop();
            if (engine != null) engine.Stop();
            if (wash != null) wash.Stop();
            foreach (var voice in voices) if (voice != null) voice.Stop();
            reloadAt = -1f;
        }
    }
}
