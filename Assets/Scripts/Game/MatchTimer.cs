
using Photon.Pun;
using Photon.Realtime;
using ExitGames.Client.Photon;
using TMPro;
using UnityEngine;
using System.Collections;

public class MatchManager : MonoBehaviourPunCallbacks
{
    public static MatchManager Instance { get; private set; }
    [SerializeField] private TMP_Text timerText;

    private const double MatchDuration = 600.0;
    private GunSpawner gunSpawner;
    private bool gunSpawned;

    private const string StartTimeKey = "MatchStartTime";
    private const string EndedKey = "MatchEnded";
    private const string WinnerKey = "WinnerActorNumber";

    private bool resultsLoading;

    public bool MatchStarted {get; private set;}
    [Header("UI")]
    [SerializeField] private GameObject waitingPanel;
    [SerializeField] private TMP_Text waitingText;
    [SerializeField] private TMP_Text phaseTextUI;
    [SerializeField] private GameObject phasePanel;
    private bool timeIsUp;

    private void Awake()
    {
        PhotonNetwork.AutomaticallySyncScene = true;
        Instance = this;
        Debug.Log("[MatchManager] Awake.");
    }

    private void Start()
    {
        MatchStarted = false;
        timeIsUp = false;
        gunSpawned = false;
        phasePanel.SetActive(false);
        gunSpawner = FindAnyObjectByType<GunSpawner>();

        if (waitingPanel != null) waitingPanel.SetActive(true);

        if (!PhotonNetwork.InRoom)
            return;

        TryStartMatch();

        // Por si la partida ya terminó antes de iniciar este componente.
        CheckMatchEnded();
    }

    private void Update()
    {
        if (!PhotonNetwork.InRoom)
        {
            ShowTime(MatchDuration);
            return;
        }

        MatchStarted = IsMatchStarted();
        if(waitingPanel != null) waitingPanel.SetActive(!MatchStarted);

        var properties = PhotonNetwork.CurrentRoom.CustomProperties;

        if (properties.TryGetValue(EndedKey, out object endedValue)
            && endedValue is bool ended && ended)
        {
            ShowTime(0);
            LoadResultsOnce();
            return;
        }

        if (!properties.TryGetValue(StartTimeKey, out object startValue))
        {
            ShowTime(MatchDuration);
            return;
        }

        double startTime = (double)startValue;
        double elapsed = PhotonNetwork.Time - startTime;
        double remaining = System.Math.Max(
            0, MatchDuration - elapsed
        );

        ShowTime(remaining);

        // Spawn gun
        if (remaining <= 180 && !gunSpawned)
        {
            gunSpawned = true;

            if (PhotonNetwork.IsMasterClient)
            {
                photonView.RPC(
                    nameof(ShowPhaseText),
                    RpcTarget.All,
                    "New Merchandise \n Find the gun!",
                    3f
                );
                gunSpawner.SpawnGun();
            }
        }

        if (remaining <= 60 && remaining >=58)
        {
            if(PhotonNetwork.IsMasterClient)
            { 
                photonView.RPC(
                    nameof(ShowPhaseText),
                    RpcTarget.All,
                    "Final clearance!",
                    2f
                );
                timerText.color = Color.red;
            }
        }

        // Determine the winner
        if (remaining <= 0 && PhotonNetwork.IsMasterClient && !properties.ContainsKey("TimeIsUp"))
        {
            timeIsUp = true;

            PhotonNetwork.CurrentRoom.SetCustomProperties(
                new ExitGames.Client.Photon.Hashtable
                {
                    { "TimeIsUp", true }
                }
            );

            StartCoroutine(EndMatchSequence());
        }

        if (properties.TryGetValue("TimeIsUp", out object timeUpValue)
            && timeUpValue is bool timeUp && timeUp)
        {
            MatchStarted = false;

            if (waitingPanel != null)
                waitingPanel.SetActive(true);

            if (waitingText != null)
                waitingText.text = "Time's Up!";

            return;
        }
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
        // El contador no se reinicia.
    }

    public override void OnMasterClientSwitched(Player newMasterClient)
    {
        TryStartMatch();
        CheckMatchEnded();
    }

    public override void OnRoomPropertiesUpdate(
        ExitGames.Client.Photon.Hashtable propertiesThatChanged)
    {
        if (propertiesThatChanged.ContainsKey(EndedKey)
            || propertiesThatChanged.ContainsKey(WinnerKey))
        {
            CheckMatchEnded();
        }
    }

    private void TryStartMatch()
    {
        if (!PhotonNetwork.IsMasterClient ||
            !PhotonNetwork.InRoom ||
            PhotonNetwork.CurrentRoom.PlayerCount < 2) //Cambiar a 2 ya en produccion
        {
            return;
        }

        StartCoroutine(StartMatchSequence());

        var properties = PhotonNetwork.CurrentRoom.CustomProperties;

        if (properties.ContainsKey(StartTimeKey) ||
            properties.ContainsKey(EndedKey))
        {
            return;
        }

        PhotonNetwork.CurrentRoom.SetCustomProperties(
            new ExitGames.Client.Photon.Hashtable
            {
                { StartTimeKey, PhotonNetwork.Time }
            }
        );
    }

    private IEnumerator StartMatchSequence()
    {
        waitingPanel.SetActive(false);
        yield return new WaitForSecondsRealtime(0.5f);

        photonView.RPC(
            nameof(ShowPhaseText),
            RpcTarget.All,
            "Lights Out!",
            3f
        );
    }

    private void FinishMatch()
    {
        if (!PhotonNetwork.IsMasterClient ||
            !PhotonNetwork.InRoom)
        {
            return;
        }

        var properties = PhotonNetwork.CurrentRoom.CustomProperties;

        // Evita declarar el resultado más de una vez.
        if (properties.ContainsKey(EndedKey))
            return;

        int highestScore = int.MinValue;
        int winnerActorNumber = -1;
        bool tie = false;

        foreach (Player player in PhotonNetwork.PlayerList)
        {
            string scoreKey = $"Score_{player.ActorNumber}";
            int score = 0;

            if (properties.TryGetValue(scoreKey, out object value))
            {
                score = (int)value;
            }

            if (score > highestScore)
            {
                highestScore = score;
                winnerActorNumber = player.ActorNumber;
                tie = false;
            }
            else if (score == highestScore)
            {
                tie = true;
            }
        }

        // -1 representa un empate.
        if (tie || winnerActorNumber == -1)
        {
            winnerActorNumber = -1;
        }

        Debug.Log(
            $"Partida terminada. Ganador: {winnerActorNumber}"
        );

        // Ambos valores se publican juntos en la sala.
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
        if (resultsLoading)
            return;

        resultsLoading = true;

        if (PhotonNetwork.IsMasterClient)
        {
            PhotonNetwork.LoadLevel("Results");
        }
    }

    private void ShowTime(double seconds)
    {
        if (timerText == null)
            return;

        int totalSeconds = Mathf.CeilToInt((float)seconds);
        int minutes = totalSeconds / 60;
        int remainingSeconds = totalSeconds % 60;

        timerText.text = $"{minutes:00}:{remainingSeconds:00}";
    }

    private bool IsMatchStarted()
    {
        if (!PhotonNetwork.InRoom)
        return false;

        var properties = PhotonNetwork.CurrentRoom.CustomProperties;

        return properties.ContainsKey(StartTimeKey)
            && !properties.ContainsKey("TimeIsUp");
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

        phaseCoroutine = StartCoroutine(PhaseTextChange(message, duration));
    }

    private Coroutine phaseCoroutine;

    private IEnumerator PhaseTextChange(string phaseText, float displayDuration)
    {
        if (phaseTextUI == null && phasePanel == null)
            yield break;

        phaseTextUI.text = phaseText;
        phasePanel.SetActive(true);

        // Mantener visible
        yield return new WaitForSecondsRealtime(displayDuration);
        phasePanel.SetActive(false);
    }
}
