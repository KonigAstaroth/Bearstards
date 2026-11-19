using UnityEngine;

/// <summary>
/// Se coloca en cada escena e indica qué música debe sonar (Lobby, Game o None).
/// Si todavía no existe un MusicManager (por ejemplo, al abrir la escena directamente
/// desde el editor), lo crea a partir del prefab "MusicManager" de la carpeta Resources.
/// NO lo uses en la escena de resultados: ahí la música la decide ResultsManager.
/// </summary>
public class SceneMusic : MonoBehaviour
{
    [SerializeField] private MusicMode mode = MusicMode.Game;

    private void Start()
    {
        MusicManager.Request(mode);
    }
}