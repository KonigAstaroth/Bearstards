using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine.SceneManagement;
public class RoomManager : MonoBehaviourPunCallbacks
{
    // Se activa cuando hay que desconectar primero (otro usuario o desconexion en curso).
    private bool reconectarAlDesconectar;

    private void Start()
    {
        SessionManager sesion = SessionManager.Instance;
        if (sesion == null || !sesion.HaySesion || sesion.Jugador == null)
        {
            Debug.Log("[RoomManager] No hay sesion, volviendo al login");
            SceneManager.LoadScene("Login");
            return;
        }

        JugadorDto jugador = sesion.Jugador;

        // Una desconexion anterior (p. ej. cerrar sesion) todavia no termina.
        if (PhotonNetwork.NetworkClientState == ClientState.Disconnecting)
        {
            Debug.Log("[RoomManager] Esperando a que termine la desconexion...");
            reconectarAlDesconectar = true;
            return;
        }

        // Entro otro usuario: hay que reconectar con su identidad.
        if (PhotonNetwork.IsConnected && !PhotonIdentidad.CoincideCon(jugador))
        {
            Debug.Log("[RoomManager] Cambio de usuario, reconectando a Photon...");
            reconectarAlDesconectar = true;
            PhotonNetwork.Disconnect();
            return;
        }

        Debug.Log("Connecting...");
        if (!PhotonNetwork.IsConnected)
        {
            Conectar(jugador);
        }else if (PhotonNetwork.InLobby)
        {
            SceneManager.LoadScene("Lobby");
        }else if (PhotonNetwork.NetworkClientState == ClientState.ConnectedToMasterServer)
        {
            // Ya estaba en el master (OnConnectedToMaster paso antes de cargar esta escena).
            PhotonNetwork.JoinLobby();
        }
    }

    private void Conectar(JugadorDto jugador)
    {
        PhotonIdentidad.Aplicar(jugador);
        PhotonNetwork.ConnectUsingSettings();
    }

    public override void OnConnectedToMaster()
    {
        Debug.Log("Connected to Master");
        PhotonNetwork.JoinLobby();
    }

    public override void OnJoinedLobby()
    {
        Debug.Log("Joined to Lobby");
        PhotonNetwork.AutomaticallySyncScene = true;
        SceneManager.LoadScene("Lobby");
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        Debug.Log("[RoomManager] Desconectado de Photon: " + cause);

        if (!reconectarAlDesconectar) return;
        reconectarAlDesconectar = false;

        SessionManager sesion = SessionManager.Instance;
        if (sesion == null || !sesion.HaySesion || sesion.Jugador == null)
        {
            SceneManager.LoadScene("Login");
            return;
        }

        Conectar(sesion.Jugador);
    }
}
