using System;
using UnityEditor;
using UnityEngine;

// Prueba de humo de la sesión contra un Django local (http://localhost:8000).
// Uso: Unity -batchmode -projectPath . -executeMethod PruebaSesion.Ejecutar -logFile -
// (sin -quit: este método llama a EditorApplication.Exit al terminar)
public static class PruebaSesion
{
    private const string ClaveToken = "bearstards.sesion.token";
    private const string ClaveUsuario = "bearstards.sesion.usuario";
    private const string Password = "ClaveSegura!2026";
    private const double TimeoutSegundos = 60.0;

    [Serializable]
    public class SaludDto
    {
        public bool ok;
    }

    private static int correctas;
    private static int fallidas;
    private static bool terminado;
    private static double inicio;

    private static bool usarLocalPrevio;
    private static bool habiaToken;
    private static string tokenPrevio;
    private static bool habiaUsuario;
    private static string usuarioPrevio;
    private static SessionManager gestor;
    private static bool gestorCreado;

    [MenuItem("Bearstards/Prueba de sesión (local)")]
    public static void Ejecutar()
    {
        correctas = 0;
        fallidas = 0;
        terminado = false;
        inicio = EditorApplication.timeSinceStartup;

        // Solo en memoria: no se guarda el asset
        usarLocalPrevio = ApiConfig.Actual.usarLocal;
        ApiConfig.Actual.usarLocal = true;

        // Guardamos lo que hubiera en PlayerPrefs para restaurarlo al final
        habiaToken = PlayerPrefs.HasKey(ClaveToken);
        tokenPrevio = PlayerPrefs.GetString(ClaveToken, string.Empty);
        habiaUsuario = PlayerPrefs.HasKey(ClaveUsuario);
        usuarioPrevio = PlayerPrefs.GetString(ClaveUsuario, string.Empty);
        PlayerPrefs.DeleteKey(ClaveToken);

        gestorCreado = SessionManager.Instance == null;
        gestor = gestorCreado ? SessionManager.Crear() : SessionManager.Instance;
        gestor.CargarSesionGuardada();

        EditorApplication.update += Vigilar;
        Log("Iniciando contra " + ApiConfig.Actual.UrlBase);
        Correr();
    }

    private static void Vigilar()
    {
        if (terminado) return;
        if (EditorApplication.timeSinceStartup - inicio > TimeoutSegundos)
        {
            Fallo("Tiempo agotado (" + TimeoutSegundos + " s)");
            Terminar();
        }
    }

    private static async void Correr()
    {
        try
        {
            await Pasos();
        }
        catch (Exception e)
        {
            Fallo("Excepción: " + e);
        }
        Terminar();
    }

    private static async Awaitable Pasos()
    {
        int expiradas = 0;
        gestor.SesionExpirada += () => expiradas++;

        // 1. Health
        var salud = await ApiClient.Get<SaludDto>("/health/", conToken: false);
        if (!Comprobar(salud.Ok && salud.Datos != null && salud.Datos.ok, "GET /health/", salud))
        {
            return; // sin servidor no tiene sentido seguir
        }

        // 2. Registro
        string usuario = "prueba_" + Guid.NewGuid().ToString("N").Substring(0, 10);
        var registro = await gestor.Registrar(usuario, string.Empty, Password);
        Comprobar(registro.Ok && registro.Status == 201 && !string.IsNullOrEmpty(gestor.Token) && gestor.HaySesion
            && gestor.Jugador.username == usuario, "Registro de " + usuario, registro);
        string tokenViejo = gestor.Token;
        Comprobar(PlayerPrefs.GetString(ClaveToken) == tokenViejo && PlayerPrefs.GetString(ClaveUsuario) == usuario,
            "Token y usuario guardados en PlayerPrefs");

        // 3. /yo/ con ese token
        var yo = await gestor.RefrescarJugador();
        Comprobar(yo.Ok && yo.Datos != null && yo.Datos.username == usuario, "GET /yo/ devuelve el mismo usuario", yo);

        // 4. Logout directo (la sesión local sigue con el token ya borrado en el servidor)
        var logout = await ApiClient.Post<Vacio>("/auth/logout/", null);
        Comprobar(logout.Ok && logout.Status == 204, "POST /auth/logout/ -> 204", logout);

        // 5. /yo/ con el token viejo -> 401, sesión limpia y SesionExpirada
        var yoViejo = await gestor.RefrescarJugador();
        Comprobar(yoViejo.Status == 401 && !gestor.HaySesion && string.IsNullOrEmpty(gestor.Token)
            && expiradas == 1 && !PlayerPrefs.HasKey(ClaveToken),
            "GET /yo/ con token viejo -> 401, sesión limpia y SesionExpirada", yoViejo);
        Comprobar(gestor.UltimoUsuario == usuario, "Se conserva el último usuario");

        // 6. Login de nuevo
        var login = await gestor.IniciarSesion(usuario, Password);
        Comprobar(login.Ok && gestor.HaySesion && !string.IsNullOrEmpty(gestor.Token) && gestor.Token != tokenViejo,
            "Login de nuevo -> token nuevo", login);
        string tokenNuevo = gestor.Token;

        var yoNuevo = await gestor.RefrescarJugador();
        Comprobar(yoNuevo.Ok && yoNuevo.Datos != null && yoNuevo.Datos.username == usuario, "GET /yo/ con token nuevo", yoNuevo);

        // 7. CerrarSesion: limpia local sin disparar SesionExpirada
        await gestor.CerrarSesion();
        Comprobar(!gestor.HaySesion && string.IsNullOrEmpty(gestor.Token) && !PlayerPrefs.HasKey(ClaveToken)
            && gestor.UltimoUsuario == usuario && expiradas == 1, "CerrarSesion limpia la sesión local");

        // El token cerrado ya no sirve: restaurarlo desde PlayerPrefs debe dar 401
        PlayerPrefs.SetString(ClaveToken, tokenNuevo);
        gestor.CargarSesionGuardada();
        var restaurada = await gestor.RestaurarSesion();
        Comprobar(restaurada.Status == 401 && !gestor.TieneTokenGuardado && expiradas == 2,
            "RestaurarSesion con token cerrado -> 401", restaurada);

        // 8. Sin token guardado
        var sinToken = await gestor.RestaurarSesion();
        Comprobar(!sinToken.Ok && sinToken.Status == -1 && !string.IsNullOrEmpty(sinToken.Error),
            "RestaurarSesion sin token -> Status -1", sinToken);

        // 9. Contraseña incorrecta
        var mala = await gestor.IniciarSesion(usuario, "incorrecta123");
        Comprobar(!mala.Ok && mala.Status == 400 && !string.IsNullOrEmpty(mala.Error) && !mala.Error.StartsWith("Error ")
            && !gestor.HaySesion, "Login con contraseña incorrecta -> error legible", mala);

        // 10. Usuario duplicado
        var duplicado = await gestor.Registrar(usuario, string.Empty, Password);
        Comprobar(!duplicado.Ok && duplicado.Status == 400 && !string.IsNullOrEmpty(duplicado.Error)
            && duplicado.Error.Contains("username"), "Registro duplicado -> error de campo legible", duplicado);
    }

    private static bool Comprobar<T>(bool condicion, string nombre, ApiResultado<T> resultado)
    {
        string extra = resultado == null ? string.Empty : $" [status {resultado.Status}{(string.IsNullOrEmpty(resultado.Error) ? "" : ", error: " + resultado.Error)}]";
        return Comprobar(condicion, nombre + extra);
    }

    private static bool Comprobar(bool condicion, string nombre)
    {
        if (condicion)
        {
            correctas++;
            Log("OK " + nombre);
        }
        else
        {
            Fallo(nombre);
        }
        return condicion;
    }

    private static void Fallo(string mensaje)
    {
        fallidas++;
        Debug.LogError("[PruebaSesion] FALLO " + mensaje);
    }

    private static void Log(string mensaje)
    {
        Debug.Log("[PruebaSesion] " + mensaje);
    }

    private static void Terminar()
    {
        if (terminado) return;
        terminado = true;
        EditorApplication.update -= Vigilar;

        // Restaurar estado previo
        ApiConfig.Actual.usarLocal = usarLocalPrevio;
        if (habiaToken) PlayerPrefs.SetString(ClaveToken, tokenPrevio);
        else PlayerPrefs.DeleteKey(ClaveToken);
        if (habiaUsuario) PlayerPrefs.SetString(ClaveUsuario, usuarioPrevio);
        else PlayerPrefs.DeleteKey(ClaveUsuario);
        PlayerPrefs.Save();

        if (gestor != null)
        {
            // El objeto oculto creado en modo edición no debe quedar vivo al entrar a Play
            if (gestorCreado && !Application.isPlaying) UnityEngine.Object.DestroyImmediate(gestor.gameObject);
            else gestor.CargarSesionGuardada();
            gestor = null;
        }

        bool exito = fallidas == 0 && correctas > 0;
        Log($"Resumen: {correctas} OK, {fallidas} FALLO -> {(exito ? "ÉXITO" : "FALLÓ")}");

        if (Application.isBatchMode)
        {
            EditorApplication.Exit(exito ? 0 : 1);
        }
    }
}
