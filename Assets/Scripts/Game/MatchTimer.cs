
using Photon.Pun;
using Photon.Realtime;
using ExitGames.Client.Photon;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Text;

public class MatchManager : MonoBehaviourPunCallbacks
{
    public static MatchManager Instance { get; private set; }

    private const double MatchDuration = 190.0;

    private const string StartTimeKey = EstadoPartida.ClaveInicio;
    private const string EndedKey = EstadoPartida.ClaveTerminada;
    private const string WinnerKey = EstadoPartida.ClaveGanador;
    private const string TimeUpKey = EstadoPartida.ClaveTiempoAgotado;

    // Largo máximo del nombre de la sala dentro del MatchId (el id no pasa de 64).
    private const int MaxSalaEnMatchId = 32;

    [Header("Match Settings")]
    [SerializeField] private int requiredPlayers = 2;
    [SerializeField] private GunSpawner gunSpawner;
    private PlayerMatchUI playerUI;

    private bool gunSpawned;
    private bool resultsLoading;
    private bool startSequenceStarted;
    private bool endSequenceStarted;
    private bool finalClearanceShown;

    // El master ya pidió terminar la partida (por tiempo o por abandono).
    // La caché de la sala tarda un viaje al servidor, así que esto evita dos finales.
    private bool finSolicitado;

    private Coroutine phaseCoroutine;

    public bool MatchStarted { get; private set; }

    // true en el master en cuanto pide terminar, antes de que vuelva el eco de la sala.
    public bool FinSolicitado => finSolicitado;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[MatchManager] Duplicate instance destroyed.");
            Destroy(gameObject);
            return;
        }

        Instance = this;

        PhotonNetwork.AutomaticallySyncScene = true;

        Debug.Log("[MatchManager] Initialized.");
    }

    private void Start()
    {
        MatchStarted = false;

        gunSpawned = false;
        // El jugador local registra su UI hasta que aparece (RegisterPlayerUI),
        // así que aquí todavía puede no existir.
        if (playerUI != null)
        {
            if (playerUI.phasePanel != null) playerUI.phasePanel.SetActive(false);
            if (playerUI.waitingPanel != null) playerUI.waitingPanel.SetActive(true);
        }

        TryStartMatch();
        RevisarAbandono();
        CheckMatchEnded();
    }

    private void Update()
    {
        if (!PhotonNetwork.InRoom)
            return;

        MatchStarted = IsMatchStarted();

        var properties = PhotonNetwork.CurrentRoom.CustomProperties;

        // Check if match has ended.
        if (properties.TryGetValue(EndedKey, out object endedValue)
            && endedValue is bool ended && ended)
        {
            ShowTime(0);
            LoadResultsOnce();
            return;
        }

        // Check if match has started.
        if (!properties.TryGetValue(StartTimeKey, out object startValue)
            || !(startValue is double startTime))
        {
            ShowTime(MatchDuration);
            UpdateWaitingUI(false);
            return;
        }

        double elapsed = PhotonNetwork.Time - startTime;

        double remaining = System.Math.Max(
            0,
            MatchDuration - elapsed
        );

        ShowTime(remaining);

        UpdateWaitingUI(MatchStarted);

        // Spawn weapon once.
        if (remaining <= 180 && !gunSpawned)
        {
            gunSpawned = true;

            if (PhotonNetwork.IsMasterClient)
            {
                photonView.RPC(
                    nameof(ShowPhaseText),
                    RpcTarget.All,
                    "New Merchandise!\nFind the gun!",
                    3f
                );

                if (gunSpawner != null)
                {
                    gunSpawner.SpawnGun();
                }
                else
                {
                    Debug.LogError("[MatchManager] GunSpawner not found!");
                }
            }
        }

        // Final clearance announcement.
        if (remaining <= 60 && remaining > 58
            && PhotonNetwork.IsMasterClient
            && !finalClearanceShown)
        {
            finalClearanceShown = true;

            photonView.RPC(
                nameof(ShowPhaseText),
                RpcTarget.All,
                "Final clearance!",
                2f
            );

            if (playerUI != null && playerUI.timerText != null)
                playerUI.timerText.color = Color.red;
        }

        // Time is up.
        if (remaining <= 0
            && PhotonNetwork.IsMasterClient
            && !properties.ContainsKey(TimeUpKey)
            && !endSequenceStarted
            && !finSolicitado)
        {
            endSequenceStarted = true;

            PhotonNetwork.CurrentRoom.SetCustomProperties(
                new ExitGames.Client.Photon.Hashtable
                {
                    { TimeUpKey, true }
                }
            );

            Debug.Log("[MatchManager] Se acabó el tiempo.");

            StartCoroutine(EndMatchSequence());
        }

        // Waiting screen during the final delay.
        if (properties.TryGetValue(TimeUpKey, out object timeUpValue)
            && timeUpValue is bool timeUp && timeUp)
        {
            MatchStarted = false;

            UpdateWaitingUI(false);

            if (playerUI != null && playerUI.waitingText != null)
                playerUI.waitingText.text = "Time's Up!";

            return;
        }
    }

    // Register the local player's Canvas.
    public void RegisterPlayerUI(PlayerMatchUI ui)
    {
        if (ui == null)
            return;

        playerUI = ui;

        Debug.Log("[MatchManager] Local player UI registered.");

        // Initialize UI immediately.
        if (playerUI.waitingPanel != null)
            playerUI.waitingPanel.SetActive(!IsMatchStarted());

        MatchStarted = IsMatchStarted();

        ShowTime(GetRemainingTime());

        CheckMatchEnded();
    }

    private void UpdateWaitingUI(bool matchStarted)
    {
        if (playerUI == null || playerUI.waitingPanel == null)
            return;

        playerUI.waitingPanel.SetActive(!matchStarted);
    }

    public override void OnJoinedRoom()
    {
        TryStartMatch();
        CheckMatchEnded();
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        TryStartMatch();
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        // El tiempo de la partida no se reinicia.
        // Si la partida ya estaba en juego, el que se queda gana por abandono.
        // Aquí ya puede ser master (si el que se fue lo era).
        if (otherPlayer != null)
            TerminarPorAbandono(otherPlayer.ActorNumber);
    }

    public override void OnMasterClientSwitched(Player newMasterClient)
    {
        // Según el orden de los callbacks, el abandono puede verse aquí primero.
        RevisarAbandono();
        ContinuarFinPorTiempo();
        TryStartMatch();
        CheckMatchEnded();
    }

    public override void OnRoomPropertiesUpdate(ExitGames.Client.Photon.Hashtable propertiesThatChanged)
    {
        if (propertiesThatChanged.ContainsKey(StartTimeKey))
        {
            MatchStarted = IsMatchStarted();

            // Alguien pudo irse mientras llegaba el inicio.
            RevisarAbandono();
        }

        if (propertiesThatChanged.ContainsKey(EndedKey))
        {
            CheckMatchEnded();
        }
    }

    private void TryStartMatch()
    {
        if (!PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient)
            return;

        if (PhotonNetwork.CurrentRoom.PlayerCount < requiredPlayers)
            return;

        var properties = PhotonNetwork.CurrentRoom.CustomProperties;

        if (properties.ContainsKey(StartTimeKey)
            || properties.ContainsKey(EndedKey))
            return;

        Player[] jugadores = PhotonNetwork.PlayerList;
        int[] actores = new int[jugadores.Length];
        string matchId = CrearMatchId(PhotonNetwork.CurrentRoom.Name);

        // Todo el estado inicial va en la misma llamada que el inicio.
        var props = new ExitGames.Client.Photon.Hashtable
        {
            { StartTimeKey, PhotonNetwork.Time },
            { EstadoPartida.ClaveMatchId, matchId }
        };

        for (int i = 0; i < jugadores.Length; i++)
        {
            Player jugador = jugadores[i];
            int actor = jugador.ActorNumber;
            actores[i] = actor;

            int apiId = PhotonIdentidad.ApiIdDe(jugador);
            if (apiId == -1)
            {
                Debug.LogWarning($"[MatchManager] El actor {actor} ({jugador.NickName}) no tiene apiId; esta partida no se podrá reportar.");
            }

            props[EstadoPartida.ClaveApiId(actor)] = apiId;
            props[EstadoPartida.ClaveNombre(actor)] = jugador.NickName ?? "";

            // Solo si faltan, para no pisar lo que ya escribió el dueño.
            if (!properties.ContainsKey(EstadoPartida.ClavePuntos(actor)))
                props[EstadoPartida.ClavePuntos(actor)] = 0;

            if (!properties.ContainsKey(EstadoPartida.ClaveKills(actor)))
                props[EstadoPartida.ClaveKills(actor)] = 0;
        }

        props[EstadoPartida.ClaveActores] = actores;

        if (!PhotonNetwork.CurrentRoom.SetCustomProperties(props))
        {
            Debug.LogWarning("[MatchManager] No se pudo iniciar la partida.");
            return;
        }

        Debug.Log($"[MatchManager] Partida iniciada. MatchId: {matchId} | Actores: {DescribirActores(actores, props)}");

        if (!startSequenceStarted)
        {
            startSequenceStarted = true;
            StartCoroutine(StartMatchSequence());
        }
    }

    // "{sala}-{fecha}-{guid corto}", siempre <= 64 caracteres.
    private static string CrearMatchId(string sala)
    {
        var limpio = new StringBuilder();

        if (!string.IsNullOrEmpty(sala))
        {
            foreach (char c in sala)
            {
                if (limpio.Length >= MaxSalaEnMatchId)
                    break;

                limpio.Append(char.IsLetterOrDigit(c) || c == '_' ? c : '_');
            }
        }

        if (limpio.Length == 0)
            limpio.Append("sala");

        string guid = System.Guid.NewGuid().ToString("N").Substring(0, 8);
        string id = $"{limpio}-{System.DateTime.UtcNow:yyyyMMddHHmmss}-{guid}";

        return id.Length > 64 ? id.Substring(0, 64) : id;
    }

    private static string DescribirActores(int[] actores, ExitGames.Client.Photon.Hashtable props)
    {
        var sb = new StringBuilder();

        foreach (int actor in actores)
        {
            if (sb.Length > 0) sb.Append(", ");

            object apiId = props[EstadoPartida.ClaveApiId(actor)];
            object nombre = props[EstadoPartida.ClaveNombre(actor)];
            sb.Append($"{actor} ({nombre}, apiId {apiId})");
        }

        return sb.ToString();
    }

    private IEnumerator StartMatchSequence()
    {
        yield return new WaitForSecondsRealtime(0.5f);

        photonView.RPC(
            nameof(ShowPhaseText),
            RpcTarget.All,
            "Lights Out!",
            3f
        );
    }

    // Busca entre los que empezaron la partida a alguien que ya no esté.
    private void RevisarAbandono()
    {
        if (!PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient)
            return;

        foreach (int actor in EstadoPartida.Actores)
        {
            if (!SigueEnSala(actor))
            {
                TerminarPorAbandono(actor);
                return;
            }
        }
    }

    // Solo el master. Idempotente: si ya se pidió el final no hace nada.
    private void TerminarPorAbandono(int actorQueSeFue)
    {
        if (!PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient || finSolicitado)
            return;

        var properties = PhotonNetwork.CurrentRoom.CustomProperties;

        // Irse en la pantalla de espera no cuenta como abandono.
        if (!properties.ContainsKey(StartTimeKey))
            return;

        // Ya terminó, o se acabó el tiempo y el final normal sigue su curso.
        if (properties.ContainsKey(EndedKey) || properties.ContainsKey(TimeUpKey))
            return;

        IReadOnlyList<int> actores = EstadoPartida.Actores;

        // Solo cuenta si era uno de los jugadores de la partida.
        bool eraJugador = false;
        foreach (int actor in actores)
        {
            if (actor == actorQueSeFue)
            {
                eraJugador = true;
                break;
            }
        }

        if (!eraJugador)
            return;

        // Gana el que sigue en la sala (el local primero, que es quien escribe).
        int ganador = -1;

        foreach (int actor in actores)
        {
            if (actor != actorQueSeFue && SigueEnSala(actor))
            {
                ganador = actor;
                if (actor == EstadoPartida.ActorLocal)
                    break;
            }
        }

        finSolicitado = true;

        PhotonNetwork.CurrentRoom.SetCustomProperties(
            new ExitGames.Client.Photon.Hashtable
            {
                { EstadoPartida.ClaveAbandono, actorQueSeFue },
                { WinnerKey, ganador },
                { EndedKey, true }
            }
        );

        Debug.Log($"[MatchManager] Abandono del actor {actorQueSeFue}. Ganador: {ganador} | MatchId: {EstadoPartida.MatchId}");
    }

    private static bool SigueEnSala(int actor)
    {
        Player jugador = EstadoPartida.BuscarJugador(actor);
        return jugador != null && !jugador.IsInactive;
    }

    // Si el master se fue durante la espera de "Time's Up", el nuevo master termina.
    private void ContinuarFinPorTiempo()
    {
        if (!PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient)
            return;

        var properties = PhotonNetwork.CurrentRoom.CustomProperties;

        if (!properties.ContainsKey(TimeUpKey) || properties.ContainsKey(EndedKey))
            return;

        if (endSequenceStarted || finSolicitado)
            return;

        endSequenceStarted = true;
        StartCoroutine(EndMatchSequence());
    }

    private void FinishMatch()
    {
        if (!PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient || finSolicitado)
            return;

        var properties = PhotonNetwork.CurrentRoom.CustomProperties;

        if (properties.ContainsKey(EndedKey))
            return;

        // Se usa la foto del inicio, así cuenta también quien ya se fue.
        var actores = new List<int>(EstadoPartida.Actores);
        if (actores.Count == 0)
        {
            foreach (Player player in PhotonNetwork.PlayerList)
                actores.Add(player.ActorNumber);
        }

        int highestScore = int.MinValue;
        int winnerActorNumber = -1;
        bool tie = false;
        var resumen = new StringBuilder();

        foreach (int actor in actores)
        {
            int score = EstadoPartida.Puntos(actor);

            if (resumen.Length > 0) resumen.Append(", ");
            resumen.Append($"{actor}: {score} pts / {EstadoPartida.Kills(actor)} kills");

            if (score > highestScore)
            {
                highestScore = score;
                winnerActorNumber = actor;
                tie = false;
            }
            else if (score == highestScore)
            {
                tie = true;
            }
        }

        if (tie || winnerActorNumber == -1)
        {
            winnerActorNumber = -1;
        }

        finSolicitado = true;

        Debug.Log($"[MatchManager] Partida terminada por tiempo. Ganador: {winnerActorNumber} | MatchId: {EstadoPartida.MatchId} | {resumen}");

        PhotonNetwork.CurrentRoom.SetCustomProperties(
            new ExitGames.Client.Photon.Hashtable
            {
                { WinnerKey, winnerActorNumber },
                { EndedKey, true }
            }
        );
    }

    private void CheckMatchEnded()
    {
        if (!PhotonNetwork.InRoom)
            return;

        var properties = PhotonNetwork.CurrentRoom.CustomProperties;

        if (properties.TryGetValue(EndedKey, out object value)
            && value is bool ended && ended)
        {
            LoadResultsOnce();
        }
    }

    private void LoadResultsOnce()
    {
        if (!PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient)
            return;

        if (resultsLoading)
            return;

        resultsLoading = true;

        Debug.Log("[MatchManager] Loading Results.");

        PhotonNetwork.LoadLevel("Results");
    }

    private void ShowTime(double seconds)
    {
        if (playerUI == null || playerUI.timerText == null)
            return;

        int totalSeconds = Mathf.CeilToInt((float)seconds);

        int minutes = totalSeconds / 60;
        int remainingSeconds = totalSeconds % 60;

        playerUI.timerText.text =
            $"{minutes:00}:{remainingSeconds:00}";

        if (seconds <= 60)
            playerUI.timerText.color = Color.red;
    }

    private double GetRemainingTime()
    {
        if (!PhotonNetwork.InRoom)
            return MatchDuration;

        var properties = PhotonNetwork.CurrentRoom.CustomProperties;

        if (!properties.TryGetValue(StartTimeKey, out object startValue)
            || !(startValue is double startTime))
            return MatchDuration;

        double elapsed = PhotonNetwork.Time - startTime;

        return System.Math.Max(0, MatchDuration - elapsed);
    }

    private bool IsMatchStarted()
    {
        if (!PhotonNetwork.InRoom)
            return false;

        var properties = PhotonNetwork.CurrentRoom.CustomProperties;

        return properties.ContainsKey(StartTimeKey)
            && !properties.ContainsKey(TimeUpKey)
            && !properties.ContainsKey(EndedKey);
    }

    private IEnumerator EndMatchSequence()
    {
        yield return new WaitForSecondsRealtime(5f);

        FinishMatch();
    }

    [PunRPC]
    private void ShowPhaseText(string message, float duration)
    {
        if (phaseCoroutine != null)
            StopCoroutine(phaseCoroutine);

        phaseCoroutine = StartCoroutine(
            PhaseTextChange(message, duration)
        );
    }

    private IEnumerator PhaseTextChange(
        string message,
        float displayDuration)
    {
        if (playerUI == null ||
            playerUI.phaseTextUI == null ||
            playerUI.phasePanel == null)
        {
            yield break;
        }

        playerUI.phaseTextUI.text = message;

        playerUI.phasePanel.SetActive(true);

        yield return new WaitForSecondsRealtime(displayDuration);

        playerUI.phasePanel.SetActive(false);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
}
