using System.Collections;
using UnityEngine;

public enum MusicMode
{
    None,
    Lobby,
    Game,
    Victory,
    Defeat,
    Tie
}

/// <summary>
/// Reproduce la música del juego. Persiste entre escenas (DontDestroyOnLoad).
/// - Lobby y Game: listas de pistas en orden aleatorio, sin repetir la anterior, que pasan solas a la siguiente.
/// - Victory, Defeat y Tie: una sola pista que suena UNA vez; al terminar, el gestor pasa a la música de Lobby.
///   Si una pista de resultado no está asignada, pasa directo a la música de Lobby.
/// - Cambia de modo con un fundido de salida y de entrada.
/// La música es local: no usa Photon, cada jugador oye la suya.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class MusicManager : MonoBehaviour
{
    public static MusicManager Instance { get; private set; }

    [Header("Pistas de lobby y partida")]
    [SerializeField] private AudioClip[] lobbyTracks;
    [SerializeField] private AudioClip[] gameTracks;

    [Header("Pistas de resultado (suenan una sola vez)")]
    [SerializeField] private AudioClip victoryTrack;
    [SerializeField] private AudioClip defeatTrack;
    [SerializeField] private AudioClip tieTrack;

    [Header("Volumen")]
    [Range(0f, 1f)]
    [SerializeField] private float volume = 0.5f;
    [Tooltip("Segundos del fundido al cambiar de modo. 0 = cambio inmediato.")]
    [SerializeField] private float fadeDuration = 1.5f;
    [Tooltip("Fundido de entrada de las pistas de resultado (más corto para que no se coma el inicio).")]
    [SerializeField] private float resultFadeDuration = 0.3f;

    private const string VolumeKey = "MusicVolume";

    private AudioSource source;
    private MusicMode currentMode = MusicMode.None;
    private AudioClip lastClip;
    private Coroutine transition;

    public MusicMode CurrentMode => currentMode;
    public float Volume => volume;

    private void Awake()
    {
        // Solo puede haber un MusicManager
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        source = GetComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f; // sonido 2D

        volume = PlayerPrefs.GetFloat(VolumeKey, volume);
    }

    private void Update()
    {
        if (transition != null || currentMode == MusicMode.None) return;

        // La pista terminó
        if (!source.isPlaying && source.time <= 0f)
        {
            if (IsResultMode(currentMode))
                Play(MusicMode.Lobby);   // tras el resultado, vuelve la música del lobby
            else
                PlayNextTrack();
        }
    }

    /// <summary>
    /// Cambia el modo de música desde cualquier script. Si el MusicManager todavía no existe
    /// (por ejemplo, al abrir una escena directamente), lo crea desde el prefab de Resources.
    /// </summary>
    public static void Request(MusicMode mode)
    {
        if (Instance == null)
        {
            MusicManager prefab = Resources.Load<MusicManager>("MusicManager");

            if (prefab == null)
            {
                Debug.LogWarning("MusicManager: no se encontró el prefab 'MusicManager' en una carpeta Resources.");
                return;
            }

            Instantiate(prefab);
        }

        Instance.Play(mode);
    }

    /// <summary>Cambia el modo de música. Si ya está en ese modo, no hace nada.</summary>
    public void Play(MusicMode mode)
    {
        if (mode == currentMode) return;

        currentMode = mode;

        if (transition != null)
            StopCoroutine(transition);

        transition = StartCoroutine(SwitchTo(mode));
    }

    public void Stop()
    {
        Play(MusicMode.None);
    }

    /// <summary>Ajusta el volumen de la música (0 a 1) y lo guarda para la próxima vez.</summary>
    public void SetVolume(float newVolume)
    {
        volume = Mathf.Clamp01(newVolume);
        PlayerPrefs.SetFloat(VolumeKey, volume);

        // Si no hay un fundido en curso, se aplica al instante
        if (transition == null)
            source.volume = volume;
    }

    private static bool IsResultMode(MusicMode mode)
    {
        return mode == MusicMode.Victory || mode == MusicMode.Defeat || mode == MusicMode.Tie;
    }

    private IEnumerator SwitchTo(MusicMode mode)
    {
        // Baja el volumen de lo que esté sonando
        if (source.isPlaying)
            yield return Fade(source.volume, 0f, fadeDuration);

        source.Stop();

        AudioClip next = PickClip(mode);

        if (next == null)
        {
            transition = null;
            yield break;
        }

        source.clip = next;
        lastClip = next;
        source.volume = 0f;
        source.Play();

        float fadeIn = IsResultMode(mode) ? resultFadeDuration : fadeDuration;
        yield return Fade(0f, volume, fadeIn);

        transition = null;
    }

    private IEnumerator Fade(float from, float to, float duration)
    {
        if (duration <= 0f)
        {
            source.volume = to;
            yield break;
        }

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            source.volume = Mathf.Lerp(from, to, elapsed / duration);
            yield return null;
        }

        source.volume = to;
    }

    private void PlayNextTrack()
    {
        AudioClip next = PickClip(currentMode);

        if (next == null) return;

        lastClip = next;
        source.clip = next;
        source.volume = volume;
        source.Play();
    }

    private AudioClip PickClip(MusicMode mode)
    {
        switch (mode)
        {
            case MusicMode.Victory: return victoryTrack;
            case MusicMode.Defeat: return defeatTrack;
            case MusicMode.Tie: return tieTrack;
        }

        AudioClip[] list = null;

        if (mode == MusicMode.Lobby) list = lobbyTracks;
        else if (mode == MusicMode.Game) list = gameTracks;

        if (list == null || list.Length == 0) return null;
        if (list.Length == 1) return list[0];

        // Aleatoria, sin repetir la pista anterior
        AudioClip clip;
        do
        {
            clip = list[Random.Range(0, list.Length)];
        }
        while (clip == lastClip);

        return clip;
    }
}