
using Photon.Pun;
using Photon.Realtime;
using ExitGames.Client.Photon;
using UnityEngine;
using System.Collections;

public class MatchManager : MonoBehaviourPunCallbacks
{
    public static MatchManager Instance { get; private set; }

    private const double MatchDuration = 190.0;

    private const string StartTimeKey = "MatchStartTime";
    private const string EndedKey = "MatchEnded";
    private const string WinnerKey = "WinnerActorNumber";
    private const string TimeUpKey = "TimeIsUp";

    [Header("Match Settings")]
    [SerializeField] private int requiredPlayers = 2;
    [SerializeField] private GunSpawner gunSpawner;
    private PlayerMatchUI playerUI;

    private bool gunSpawned;
    private bool resultsLoading;
    private bool startSequenceStarted;
    private bool endSequenceStarted;
    private bool finalClearanceShown;

    private Coroutine phaseCoroutine;

    public bool MatchStarted { get; private set; }

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
        playerUI.phasePanel.SetActive(false);

        if (playerUI.waitingPanel != null) playerUI.waitingPanel.SetActive(true);

        TryStartMatch();
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
        if (!properties.TryGetValue(StartTimeKey, out object startValue))
        {
            ShowTime(MatchDuration);
            UpdateWaitingUI(false);
            return;
        }

        double startTime = (double)startValue;

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
            && !endSequenceStarted)
        {
            endSequenceStarted = true;

            PhotonNetwork.CurrentRoom.SetCustomProperties(
                new ExitGames.Client.Photon.Hashtable
                {
                    { TimeUpKey, true }
                }
            );

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
        // Match timer does not reset.
    }

    public override void OnMasterClientSwitched(Player newMasterClient)
    {
        TryStartMatch();
        CheckMatchEnded();
    }

    public override void OnRoomPropertiesUpdate(ExitGames.Client.Photon.Hashtable propertiesThatChanged)
    {
        if (propertiesThatChanged.ContainsKey(StartTimeKey))
        {
            MatchStarted = IsMatchStarted();
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

        if (!PhotonNetwork.CurrentRoom.SetCustomProperties(
            new ExitGames.Client.Photon.Hashtable
            {
                { StartTimeKey, PhotonNetwork.Time }
            }))
        {
            return;
        }

        if (!startSequenceStarted)
        {
            startSequenceStarted = true;
            StartCoroutine(StartMatchSequence());
        }
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

    private void FinishMatch()
    {
        if (!PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient)
            return;

        var properties = PhotonNetwork.CurrentRoom.CustomProperties;

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

        if (tie || winnerActorNumber == -1)
        {
            winnerActorNumber = -1;
        }

        Debug.Log($"[MatchManager] Match ended. Winner: {winnerActorNumber}");

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

        if (!properties.TryGetValue(StartTimeKey, out object startValue))
            return MatchDuration;

        double elapsed = PhotonNetwork.Time - (double)startValue;

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
