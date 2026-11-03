using System.Collections;
using Projector.Networking;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Projector.Audio
{
    // All of the game's sound, from one object that lives across scenes. Clips load from Resources/Audio, so no
    // scene needs wiring: gameplay scripts call the static Play methods, and every UI button clicks.
    // Everything is 2D (no spatial blend): each player hears their own copy, which keeps it cheap on the Quest.
    // Music changes fade the current song out, then fade the next one in.
    public class GameAudio : MonoBehaviour
    {
        public enum Song { None, Landing, BuildPhase, Race }

        const float MusicVolume = 0.5f;
        const float PopcornVolume = 0.12f;
        const float FadeSeconds = 1.2f;
        // Songs fade out over their last seconds instead of stopping dead, and a looping song fades back in.
        const float SongEndFadeSeconds = 4f;

        static GameAudio instance;

        AudioClip destiny, showYourself, imagination;
        AudioClip coin, beep, go, whoosh, thunk, applause, popcorn, click;
        AudioSource music, ambience, effects, curtainSource, thunkSource;
        Song song;
        Coroutine musicFade, ambienceFade;
        float thunkPitchFrom, thunkPitchTo;
        // The music's level from song changes; Update scales it down near the start and end of the song.
        float musicLevel;
        float lastMusicTime;
        bool musicLooped;
        bool popcornOn;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Create()
        {
            if (instance != null)
                return;
            var host = new GameObject("Game Audio");
            DontDestroyOnLoad(host);
            instance = host.AddComponent<GameAudio>();
        }

        void Awake()
        {
            instance = this;
            // If the app was launched with the headset off, the Quest opened the audio output before the app had a
            // window and nothing played through it is heard. A fresh output fixes that; nothing is playing yet.
            ResetAudioOutput();
            destiny = Load("Music/Destiny");
            showYourself = Load("Music/ShowYourself");
            imagination = Load("Music/Imagination");
            coin = Load("Sfx/Coin");
            beep = Load("Sfx/Beep");
            go = Load("Sfx/Go");
            whoosh = Load("Sfx/Whoosh");
            thunk = Load("Sfx/Thunk");
            applause = Load("Sfx/Applause");
            popcorn = Load("Sfx/Popcorn");
            click = Load("Sfx/Click");

            music = NewSource();
            ambience = NewSource();
            ambience.clip = popcorn;
            ambience.loop = true;
            effects = NewSource();
            curtainSource = NewSource();
            curtainSource.clip = whoosh;
            curtainSource.pitch = 0.7f;
            thunkSource = NewSource();
            thunkSource.clip = thunk;

            SceneManager.sceneLoaded += OnSceneLoaded;
            OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        void OnDestroy() => SceneManager.sceneLoaded -= OnSceneLoaded;

        static AudioClip Load(string path)
        {
            var clip = Resources.Load<AudioClip>("Audio/" + path);
            if (clip == null)
                Debug.LogWarning($"Missing audio clip Resources/Audio/{path}.");
            return clip;
        }

        AudioSource NewSource()
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            return source;
        }

        // The landing page has its own music and popcorn; the projector room's music follows the game phase.
        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            foreach (var button in FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (button.gameObject.scene == scene)
                    button.onClick.AddListener(Click);

            var landing = scene.name == GameScenes.LandingMenu;
            SetPopcorn(landing);
            PlaySong(landing ? Song.Landing : Song.None);
        }

        void Update()
        {
            if (thunkSource.isPlaying && thunk != null)
                thunkSource.pitch = Mathf.Lerp(thunkPitchFrom, thunkPitchTo, thunkSource.time / thunk.length);

            if (music.isPlaying && music.clip != null)
            {
                var time = music.time;
                if (time < lastMusicTime)
                    musicLooped = true;
                lastMusicTime = time;
                var ending = Mathf.Clamp01((music.clip.length - time) / SongEndFadeSeconds);
                var restarting = musicLooped ? Mathf.Clamp01(time / SongEndFadeSeconds) : 1f;
                music.volume = musicLevel * ending * restarting;
            }
        }

        // The Quest pauses the app whenever the headset comes off, and its audio output can come back silent. On
        // resume, open a fresh output and pick the song and popcorn up where they were (a reset stops every source).
        void OnApplicationPause(bool paused)
        {
            if (paused || music == null)
                return;
            ResetAudioOutput();
            if (song != Song.None && musicLevel > 0f && music.clip != null
                && (music.loop || lastMusicTime < music.clip.length - SongEndFadeSeconds))
            {
                music.time = lastMusicTime;
                music.Play();
            }
            if (popcornOn && popcorn != null)
                ambience.Play();
        }

        static void ResetAudioOutput()
        {
            var reset = AudioSettings.Reset(AudioSettings.GetConfiguration());
            Debug.Log($"Game audio: output reset {(reset ? "succeeded" : "failed")}.");
        }

        static void Play(AudioClip clip, float volume)
        {
            if (instance != null && clip != null)
                instance.effects.PlayOneShot(clip, volume);
        }

        public static void Coin() => Play(instance != null ? instance.coin : null, 0.45f);
        public static void CountdownBeep() => Play(instance != null ? instance.beep : null, 0.7f);
        public static void Go() => Play(instance != null ? instance.go : null, 0.8f);
        public static void RopeWhoosh() => Play(instance != null ? instance.whoosh : null, 0.35f);
        // The rope's whoosh, louder and lower. Its own source, since a pitch change would bend every one-shot.
        public static void CurtainWhoosh()
        {
            if (instance != null && instance.whoosh != null)
                instance.curtainSource.Play();
        }
        public static void Applause() => Play(instance != null ? instance.applause : null, 0.6f);
        public static void Click() => Play(instance != null ? instance.click : null, 0.6f);

        // A dull thunk whose pitch slides down as a block is picked up, and up as it is let go.
        public static void Thunk(bool pickingUp)
        {
            if (instance == null || instance.thunk == null)
                return;
            instance.thunkPitchFrom = pickingUp ? 1.15f : 0.8f;
            instance.thunkPitchTo = pickingUp ? 0.8f : 1.15f;
            instance.thunkSource.pitch = instance.thunkPitchFrom;
            instance.thunkSource.volume = 0.7f;
            instance.thunkSource.Play();
        }

        // The build phase lasts as long as its song, so the race starts when the song ends.
        public static float BuildPhaseSeconds(float fallback) =>
            instance != null && instance.showYourself != null ? instance.showYourself.length : fallback;

        // Starts a song (or silence) unless it is already the one playing. `startAt` is where in the song to begin,
        // so a player who arrives partway through the build phase hears it in time with everyone else.
        public static void PlaySong(Song next, float startAt = 0f)
        {
            if (instance == null || next == instance.song)
                return;
            instance.song = next;
            if (instance.musicFade != null)
                instance.StopCoroutine(instance.musicFade);
            instance.musicFade = instance.StartCoroutine(instance.FadeToSong(next, startAt));
        }

        IEnumerator FadeToSong(Song next, float startAt)
        {
            var faded = 0f;
            if (music.isPlaying)
            {
                for (var start = musicLevel; musicLevel > 0f; faded += Time.unscaledDeltaTime)
                {
                    musicLevel = Mathf.MoveTowards(musicLevel, 0f, start / FadeSeconds * Time.unscaledDeltaTime);
                    yield return null;
                }
                music.Stop();
            }

            var clip = next == Song.Landing ? destiny : next == Song.BuildPhase ? showYourself : next == Song.Race ? imagination : null;
            if (clip == null)
                yield break;
            music.clip = clip;
            music.loop = next != Song.BuildPhase;
            musicLevel = 0f;
            music.volume = 0f;
            musicLooped = false;
            // The fade-out took time; skip ahead by it so the song still ends on cue.
            var time = next == Song.BuildPhase ? startAt + faded : startAt;
            if (time >= clip.length)
                yield break;
            music.time = time;
            lastMusicTime = time;
            music.Play();
            while (musicLevel < MusicVolume)
            {
                musicLevel = Mathf.MoveTowards(musicLevel, MusicVolume, MusicVolume / FadeSeconds * Time.unscaledDeltaTime);
                yield return null;
            }
        }

        void SetPopcorn(bool on)
        {
            popcornOn = on;
            if (popcorn == null)
                return;
            if (ambienceFade != null)
                StopCoroutine(ambienceFade);
            ambienceFade = StartCoroutine(FadePopcorn(on));
        }

        IEnumerator FadePopcorn(bool on)
        {
            var target = on ? PopcornVolume : 0f;
            if (on && !ambience.isPlaying)
            {
                ambience.volume = 0f;
                ambience.Play();
            }
            while (!Mathf.Approximately(ambience.volume, target))
            {
                ambience.volume = Mathf.MoveTowards(ambience.volume, target, PopcornVolume / FadeSeconds * Time.unscaledDeltaTime);
                yield return null;
            }
            if (!on)
                ambience.Stop();
        }
    }
}
