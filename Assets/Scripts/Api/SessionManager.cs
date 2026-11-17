using System;
using UnityEngine;

// Sesión del jugador contra la API. Se crea sola antes de cargar la primera escena.
public class SessionManager : MonoBehaviour
{
    private const string ClaveToken = "bearstards.sesion.token";
    private const string ClaveUsuario = "bearstards.sesion.usuario";

    public static SessionManager Instance { get; private set; }

    public string Token { get; private set; }
    public JugadorDto Jugador { get; private set; }
    public bool HaySesion => !string.IsNullOrEmpty(Token) && Jugador != null;
    public bool TieneTokenGuardado => !string.IsNullOrEmpty(Token);
    public string UltimoUsuario { get; private set; }
    public bool Ocupado { get; private set; }

    // Login, logout, refresco o invalidación
    public event Action SesionCambiada;
    // Solo cuando el servidor rechaza el token (401)
    public event Action SesionExpirada;

    private bool cerrandoSesion;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reiniciar()
    {
        // Por si el "domain reload" está desactivado
        Instance = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void CrearAlIniciar()
    {
        if (Instance == null)
        {
            Crear();
        }
    }

    // Crea la instancia. En modo edición (pruebas) crea un objeto oculto que no se guarda.
    public static SessionManager Crear()
    {
        var objeto = new GameObject("SessionManager");
        if (!Application.isPlaying)
        {
            objeto.hideFlags = HideFlags.HideAndDontSave;
        }
        var gestor = objeto.AddComponent<SessionManager>();
        // En modo edición Awake no se llama
        gestor.Registrarse();
        return gestor;
    }

    private void Awake()
    {
        Registrarse();
    }

    private void Registrarse()
    {
        if (Instance == this) return;
        if (Instance != null)
        {
            Debug.LogWarning("[SessionManager] Instancia duplicada destruida.");
            if (Application.isPlaying) Destroy(gameObject);
            else DestroyImmediate(gameObject);
            return;
        }

        Instance = this;
        if (Application.isPlaying)
        {
            DontDestroyOnLoad(gameObject);
        }
        CargarSesionGuardada();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    // Lee el token y el último usuario de PlayerPrefs (sin validar)
    public void CargarSesionGuardada()
    {
        Token = PlayerPrefs.GetString(ClaveToken, string.Empty);
        UltimoUsuario = PlayerPrefs.GetString(ClaveUsuario, string.Empty);
        Jugador = null;
    }

    public async Awaitable<ApiResultado<SesionDto>> IniciarSesion(string usuario, string password)
    {
        var peticion = new LoginPeticion { username = usuario, password = password };
        return await Autenticar("/auth/login/", peticion);
    }

    public async Awaitable<ApiResultado<SesionDto>> Registrar(string usuario, string email, string password)
    {
        var peticion = new RegistroPeticion { username = usuario, email = email ?? string.Empty, password = password };
        return await Autenticar("/auth/registro/", peticion);
    }

    private async Awaitable<ApiResultado<SesionDto>> Autenticar(string ruta, object peticion)
    {
        if (Ocupado)
        {
            return new ApiResultado<SesionDto> { Status = -1, Error = "Another request is in progress." };
        }

        Ocupado = true;
        try
        {
            var resultado = await ApiClient.Post<SesionDto>(ruta, peticion, conToken: false);
            if (resultado.Ok)
            {
                if (resultado.Datos == null || string.IsNullOrEmpty(resultado.Datos.token) || resultado.Datos.jugador == null)
                {
                    resultado.Ok = false;
                    resultado.Error = "Invalid response from the server.";
                    return resultado;
                }
                GuardarSesion(resultado.Datos.token, resultado.Datos.jugador);
                ReintentarReportes();
            }
            return resultado;
        }
        finally
        {
            Ocupado = false;
        }
    }

    public async Awaitable<ApiResultado<JugadorDto>> RestaurarSesion()
    {
        if (!TieneTokenGuardado)
        {
            return new ApiResultado<JugadorDto> { Status = -1, Error = "No saved session." };
        }

        // 401 -> SesionInvalida (lo hace ApiClient); sin conexión -> conservamos el token
        var resultado = await RefrescarJugador();
        if (resultado.Ok)
        {
            ReintentarReportes();
        }
        return resultado;
    }

    public async Awaitable<ApiResultado<JugadorDto>> RefrescarJugador()
    {
        string tokenUsado = Token;
        var resultado = await ApiClient.Get<JugadorDto>("/yo/");
        // Si mientras tanto se cerró o cambió la sesión, la respuesta ya no aplica
        if (resultado.Ok && resultado.Datos != null && !string.IsNullOrEmpty(Token) && Token == tokenUsado)
        {
            Jugador = resultado.Datos;
            if (!string.IsNullOrEmpty(Jugador.username) && Jugador.username != UltimoUsuario)
            {
                UltimoUsuario = Jugador.username;
                PlayerPrefs.SetString(ClaveUsuario, UltimoUsuario);
                PlayerPrefs.Save();
            }
            SesionCambiada?.Invoke();
        }
        return resultado;
    }

    public async Awaitable CerrarSesion()
    {
        cerrandoSesion = true;
        try
        {
            if (TieneTokenGuardado)
            {
                // Si falla da igual: limpiamos la sesión local de todos modos
                await ApiClient.Post<Vacio>("/auth/logout/", null);
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("[SessionManager] Error al cerrar sesión: " + e.Message);
        }
        finally
        {
            cerrandoSesion = false;
        }

        LimpiarSesion();
        SesionCambiada?.Invoke();
    }

    // El servidor rechazó el token
    public void SesionInvalida()
    {
        if (cerrandoSesion) return;

        Debug.Log("[SessionManager] Sesión expirada o inválida.");
        LimpiarSesion();
        SesionExpirada?.Invoke();
        SesionCambiada?.Invoke();
    }

    private void GuardarSesion(string token, JugadorDto jugador)
    {
        Token = token;
        Jugador = jugador;
        UltimoUsuario = jugador.username;
        PlayerPrefs.SetString(ClaveToken, token);
        PlayerPrefs.SetString(ClaveUsuario, UltimoUsuario ?? string.Empty);
        PlayerPrefs.Save();
        SesionCambiada?.Invoke();
    }

    // Manda las partidas que quedaron pendientes, sin bloquear la UI
    private static async void ReintentarReportes()
    {
        try
        {
            await ReportePartida.ReintentarPendientes();
        }
        catch (Exception e)
        {
            Debug.LogWarning("[SessionManager] Error al reintentar reportes: " + e.Message);
        }
    }

    // Borra token y jugador; conserva el último usuario
    private void LimpiarSesion()
    {
        Token = null;
        Jugador = null;
        PlayerPrefs.DeleteKey(ClaveToken);
        PlayerPrefs.Save();
    }
}
