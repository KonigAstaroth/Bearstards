
using Photon.Pun;
using Photon.Realtime;
using ExitGames.Client.Photon;
using TMPro;
using UnityEngine;

public class MatchManager : MonoBehaviourPunCallbacks
{
    [SerializeField] private TMP_Text timerText;

    private const double MatchDuration = 600.0; // 10 minutos

    private const string StartTimeKey = "MatchStartTime";
    private const string EndedKey = "MatchEnded";

    private void Awake()
    {
        Debug.Log("[MatchManager] Awake: el objeto está activo.");
    }

    private void Start()
    {
        Debug.Log("[MatchManager] Start: el script está ejecutándose.");

        // Por si el script se activa cuando ya estamos dentro de la sala.
        if (PhotonNetwork.InRoom)
        {
            TryStartMatch();
        }
    }

    private void Update()
    {
        if (!PhotonNetwork.InRoom)
        {
            ShowTime(MatchDuration);
            return;
        }

        if (!PhotonNetwork.CurrentRoom.CustomProperties
            .TryGetValue(StartTimeKey, out object startValue))
        {
            ShowTime(MatchDuration);
            return;
        }

        double startTime = (double)startValue;
        double elapsed = PhotonNetwork.Time - startTime;
        double remaining = System.Math.Max(0, MatchDuration - elapsed);

        ShowTime(remaining);

        // Solo el Master Client declara el final de la partida.
        // if (remaining <= 0 && PhotonNetwork.IsMasterClient)
        // {
        //     if (!PhotonNetwork.CurrentRoom.CustomProperties
        //         .ContainsKey(EndedKey))
        //     {
        //         PhotonNetwork.CurrentRoom.SetCustomProperties(
        //             new Hashtable { { EndedKey, true } }
        //         );
        //     }
        // }
    }

    public override void OnJoinedRoom()
    {
        TryStartMatch();
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        TryStartMatch();
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        // Si queda un jugador, el contador no comienza de nuevo.
        // Si ya había comenzado, continúa corriendo.
    }

    public override void OnMasterClientSwitched(Player newMasterClient)
    {
        TryStartMatch();
    }

    private void TryStartMatch()
    {
        Debug.Log(
            $"Master: {PhotonNetwork.IsMasterClient} | " +
            $"En sala: {PhotonNetwork.InRoom} | " +
            $"Jugadores: {(PhotonNetwork.InRoom ? PhotonNetwork.CurrentRoom.PlayerCount : 0)}"
        );
        if (!PhotonNetwork.IsMasterClient ||
            !PhotonNetwork.InRoom ||
            PhotonNetwork.CurrentRoom.PlayerCount < 1)
        {
            return;
        }

        var properties = PhotonNetwork.CurrentRoom.CustomProperties;

        // No reiniciar el contador si ya comenzó.
        if (properties.ContainsKey(StartTimeKey))
            return;

        // PhotonNetwork.Time usa el reloj sincronizado de Photon.
        double startTime = PhotonNetwork.Time;

        PhotonNetwork.CurrentRoom.SetCustomProperties(
            new Hashtable
            {
                { StartTimeKey, startTime }
            }
        );
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
