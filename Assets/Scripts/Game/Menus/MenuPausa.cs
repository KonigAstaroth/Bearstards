using Photon.Pun;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Menú de pausa de la partida (Esc). El juego NO se detiene: es multijugador.
// Salir de la partida o cerrar el juego a media partida cuenta como abandono.
public class MenuPausa : MonoBehaviourPunCallbacks
{
    // El jugador local no se mueve, no apunta ni ataca mientras el menú está abierto.
    public static bool Abierto { get; private set; }

    [SerializeField] private GameObject panel;
    [SerializeField] private GameObject panelOpciones;
    [SerializeField] private GameObject panelConfirmar;
    [SerializeField] private Text textoConfirmar;
    [SerializeField] private Button botonContinuar;
    [SerializeField] private Button botonSalirPartida;
    [SerializeField] private Button botonSalirJuego;
    [SerializeField] private Button botonConfirmar;
    [SerializeField] private Button botonCancelar;
    [SerializeField] private string escenaAlSalir = "Loading";

    private enum Accion { Ninguna, SalirPartida, SalirJuego }
    private Accion pendiente = Accion.Ninguna;
    private bool saliendo;

    private void Awake()
    {
        Abierto = false;
        if (botonContinuar != null) botonContinuar.onClick.AddListener(Cerrar);
        if (botonSalirPartida != null) botonSalirPartida.onClick.AddListener(() => PedirConfirmacion(Accion.SalirPartida));
        if (botonSalirJuego != null) botonSalirJuego.onClick.AddListener(() => PedirConfirmacion(Accion.SalirJuego));
        if (botonConfirmar != null) botonConfirmar.onClick.AddListener(Confirmar);
        if (botonCancelar != null) botonCancelar.onClick.AddListener(MostrarOpciones);
        Mostrar(false);
    }

    private void OnDestroy()
    {
        Abierto = false;
    }

    private void Update()
    {
        if (saliendo || Keyboard.current == null) return;
        if (!Keyboard.current.escapeKey.wasPressedThisFrame) return;

        if (!Abierto)
        {
            if (PuedeAbrir()) Abrir();
        }
        else if (pendiente != Accion.Ninguna)
        {
            MostrarOpciones();
        }
        else
        {
            Cerrar();
        }
    }

    private void LateUpdate()
    {
        // Al reaparecer, el jugador vuelve a bloquear el cursor: mientras el menú esté abierto, se libera.
        if (Abierto && Cursor.lockState != CursorLockMode.None)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    // Con la partida terminada ya se va a Results; ahí no hace falta el menú.
    private static bool PuedeAbrir()
    {
        return PhotonNetwork.InRoom && !EstadoPartida.Terminada;
    }

    public void Abrir()
    {
        Mostrar(true);
        MostrarOpciones();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void Cerrar()
    {
        Mostrar(false);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Mostrar(bool visible)
    {
        Abierto = visible;
        if (panel != null) panel.SetActive(visible);
        if (!visible) pendiente = Accion.Ninguna;
    }

    private void MostrarOpciones()
    {
        pendiente = Accion.Ninguna;
        if (panelOpciones != null) panelOpciones.SetActive(true);
        if (panelConfirmar != null) panelConfirmar.SetActive(false);
    }

    private void PedirConfirmacion(Accion accion)
    {
        pendiente = accion;
        bool enJuego = EstadoPartida.Iniciada && !EstadoPartida.Terminada;

        if (textoConfirmar != null)
        {
            string que = accion == Accion.SalirPartida ? "Leave the match?" : "Quit the game?";
            textoConfirmar.text = enJuego ? que + "\nIt will count as a defeat." : que;
        }

        if (panelOpciones != null) panelOpciones.SetActive(false);
        if (panelConfirmar != null) panelConfirmar.SetActive(true);
    }

    private void Confirmar()
    {
        Accion accion = pendiente;
        if (accion == Accion.Ninguna) return;

        saliendo = true;
        if (botonConfirmar != null) botonConfirmar.interactable = false;
        if (botonCancelar != null) botonCancelar.interactable = false;

        if (accion == Accion.SalirJuego)
        {
            Debug.Log("[MenuPausa] Cerrando el juego desde la partida");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
            return;
        }

        Debug.Log("[MenuPausa] Saliendo de la partida");
        if (PhotonNetwork.InRoom)
            PhotonNetwork.LeaveRoom();
        else
            IrAlLobby();
    }

    public override void OnLeftRoom()
    {
        if (saliendo) IrAlLobby();
    }

    private void IrAlLobby()
    {
        Abierto = false;
        // Loading reconecta con la sesión y lleva al Lobby.
        SceneManager.LoadScene(escenaAlSalir);
    }
}
