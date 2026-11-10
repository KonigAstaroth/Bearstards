
using Photon.Pun;
using Photon.Realtime;
using ExitGames.Client.Photon;
using TMPro;
using UnityEngine;

public class ScoreManager : MonoBehaviourPunCallbacks
{
    [Header("HUD")]
    [SerializeField] private TMP_Text localScoreText;
    [SerializeField] private TMP_Text opponentScoreText;

    // Último valor que escribió este cliente para sus propias claves.
    // Solo el dueño escribe Score_/Kills_ de su actor, así que esto manda
    // aunque la caché de la sala aún no tenga el eco del servidor (dos kills seguidas).
    // Es estático porque el jugador se destruye y reaparece al morir.
    private static string partidaLocal;
    private static int puntosLocales;
    private static int killsLocales;

    private void Start()
    {
        if (!photonView.IsMine)
            return;

        if (PhotonNetwork.InRoom)
        {
            InitializeScore();
            RefreshScores();
        }
    }

    private void InitializeScore()
    {
        int actor = PhotonNetwork.LocalPlayer.ActorNumber;
        var props = PhotonNetwork.CurrentRoom.CustomProperties;
        var nuevas = new Hashtable();

        // Solo si la clave no existe, para no pisar valores reales.
        if (!props.ContainsKey(EstadoPartida.ClavePuntos(actor)))
            nuevas[EstadoPartida.ClavePuntos(actor)] = 0;

        if (!props.ContainsKey(EstadoPartida.ClaveKills(actor)))
            nuevas[EstadoPartida.ClaveKills(actor)] = 0;

        if (nuevas.Count > 0)
            PhotonNetwork.CurrentRoom.SetCustomProperties(nuevas);
    }

    // Puntos sueltos (p. ej. el castigo por matar un NPC, con valor negativo).
    [PunRPC]
    public void AwardKillPoints(int points)
    {
        if (!photonView.IsMine || !PuedePuntuar())
            return;

        Sumar(points, 0);
    }

    // Kill de un jugador: suma los puntos y la kill en una sola escritura.
    [PunRPC]
    public void AwardPlayerKill(int points)
    {
        if (!photonView.IsMine || !PuedePuntuar())
            return;

        Sumar(points, 1);
    }

    private void Sumar(int puntos, int kills)
    {
        int actor = PhotonNetwork.LocalPlayer.ActorNumber;
        CargarValoresLocales(actor);

        puntosLocales += puntos;
        killsLocales += kills;

        var nuevas = new Hashtable
        {
            { EstadoPartida.ClavePuntos(actor), puntosLocales }
        };

        if (kills != 0)
            nuevas[EstadoPartida.ClaveKills(actor)] = killsLocales;

        PhotonNetwork.CurrentRoom.SetCustomProperties(nuevas);
    }

    // Si cambió la partida (otra sala u otro MatchId) se parte de lo que diga la sala.
    private static void CargarValoresLocales(int actor)
    {
        string partida = $"{PhotonNetwork.CurrentRoom.Name}|{EstadoPartida.MatchId}|{actor}";

        if (partidaLocal == partida)
            return;

        partidaLocal = partida;
        puntosLocales = EstadoPartida.Puntos(actor);
        killsLocales = EstadoPartida.Kills(actor);
    }

    // Solo cuenta mientras la partida está en juego.
    private static bool PuedePuntuar()
    {
        if (!PhotonNetwork.InRoom || !EstadoPartida.Iniciada || EstadoPartida.Terminada)
            return false;

        // El master ya pidió terminar aunque su caché todavía no lo refleje.
        if (MatchManager.Instance != null && MatchManager.Instance.FinSolicitado)
            return false;

        var props = PhotonNetwork.CurrentRoom.CustomProperties;
        if (props.TryGetValue(EstadoPartida.ClaveTiempoAgotado, out object valor)
            && valor is bool agotado && agotado)
            return false;

        return true;
    }

    public override void OnRoomPropertiesUpdate(
        Hashtable propertiesThatChanged)
    {
        RefreshScores();
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        RefreshScores();
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        RefreshScores();
    }

    private void RefreshScores()
    {
        if (!photonView.IsMine || !PhotonNetwork.InRoom)
            return;

        Player localPlayer = PhotonNetwork.LocalPlayer;

        Player opponent = null;

        foreach (Player player in PhotonNetwork.PlayerList)
        {
            if (player.ActorNumber != localPlayer.ActorNumber)
            {
                opponent = player;
                break;
            }
        }

        if (localScoreText != null)
        {
            localScoreText.text = $"{EstadoPartida.Puntos(localPlayer.ActorNumber)}";
        }

        if (opponentScoreText != null)
        {
            opponentScoreText.text = opponent != null
                ? $"{EstadoPartida.Puntos(opponent.ActorNumber)}"
                : "0";
        }
    }
}
