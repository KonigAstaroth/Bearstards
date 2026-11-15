using UnityEngine;

/// <summary>
/// Se coloca en cada escena e indica qué música debe sonar (Lobby, Game o None).
/// Si todavía no existe un MusicManager (por ejemplo, al abrir la escena directamente
/// desde el editor), lo crea a partir del prefab "MusicManager" de la carpeta Resources.
/// </summary>
public class SceneMusic : MonoBehaviour
{
    [SerializeField] private MusicMode mode = MusicMode.Game;

    private void Start()
    {
        if (MusicManager.Instance == null)
        {
            MusicManager prefab = Resources.Load<MusicManager>("MusicManager");

            if (prefab == null)
            {
                Debug.LogWarning("SceneMusic: no se encontró el prefab 'MusicManager' en una carpeta Resources.");
                return;
            }

            Instantiate(prefab);
        }

        MusicManager.Instance.Play(mode);
    }
}