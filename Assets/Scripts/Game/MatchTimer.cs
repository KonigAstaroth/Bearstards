
using Photon.Pun;
using Photon.Realtime;
using ExitGames.Client.Photon;
using TMPro;
using UnityEngine;

public class MatchManager : MonoBehaviourPunCallbacks
{
    [SerializeField] private TMP_Text timerText;

    private const double MatchDuration = 600.0;

    private const string StartTimeKey = "MatchStartTime";
    private const string EndedKey = "MatchEnded";
    private const string WinnerKey = "WinnerActorNumber";

    private bool resultsLoading;

    private void Awake()
    {
        PhotonNetwork.AutomaticallySyncScene = true;
        Debug.Log("[MatchManager] Awake.");
    }

    private void Start()
    {
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

        // Únicamente el Master Client determina al ganador.
        if (remaining <= 0 && PhotonNetwork.IsMasterClient)
        {
            FinishMatch();
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
        Hashtable propertiesThatChanged)
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
            PhotonNetwork.CurrentRoom.PlayerCount < 2)
        {
            return;
        }

        var properties = PhotonNetwork.CurrentRoom.CustomProperties;

        if (properties.ContainsKey(StartTimeKey) ||
            properties.ContainsKey(EndedKey))
        {
            return;
        }

        PhotonNetwork.CurrentRoom.SetCustomProperties(
            new Hashtable
            {
                { StartTimeKey, PhotonNetwork.Time }
            }
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
            new Hashtable
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
}
