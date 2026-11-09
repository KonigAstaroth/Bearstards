using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// Pantalla de inicio de sesion / registro contra la API web
public class LoginUI : MonoBehaviour
{
    [Header("Paneles")]
    [SerializeField] private GameObject panelLogin;
    [SerializeField] private GameObject panelRegistro;
    [SerializeField] private GameObject cargando;

    [Header("Login")]
    [SerializeField] private InputField usuarioLogin;
    [SerializeField] private InputField passwordLogin;

    [Header("Registro")]
    [SerializeField] private InputField usuarioRegistro;
    [SerializeField] private InputField emailRegistro;
    [SerializeField] private InputField passwordRegistro;
    [SerializeField] private InputField passwordConfirmar;

    [Header("Botones")]
    [SerializeField] private Button botonEntrar;
    [SerializeField] private Button botonRegistrarse;
    [SerializeField] private Button botonIrARegistro;
    [SerializeField] private Button botonIrALogin;
    [SerializeField] private Button botonSalir;
    [SerializeField] private Button botonReintentar;

    [Header("Mensajes")]
    [SerializeField] private Text mensaje;

    [Header("Navegacion")]
    [SerializeField] private string escenaSiguiente = "Loading";
    [SerializeField] private int minimoPassword = 8;

    private bool ocupado;
    private bool cambiandoEscena;

    private void Awake()
    {
        ConfigurarPassword(passwordLogin);
        ConfigurarPassword(passwordRegistro);
        ConfigurarPassword(passwordConfirmar);
        if (emailRegistro != null) emailRegistro.contentType = InputField.ContentType.EmailAddress;

        Conectar(botonEntrar, Entrar);
        Conectar(botonRegistrarse, Registrarse);
        Conectar(botonIrARegistro, MostrarRegistro);
        Conectar(botonIrALogin, MostrarLogin);
        Conectar(botonSalir, Salir);
        Conectar(botonReintentar, ComprobarSesion);
    }

    private void Start()
    {
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        if (botonReintentar != null) botonReintentar.gameObject.SetActive(false);
        MostrarMensaje("");
        ComprobarSesion();
    }

    private void Update()
    {
        if (ocupado || cambiandoEscena) return;

        if (TeclaEnter())
        {
            if (panelRegistro != null && panelRegistro.activeInHierarchy) Registrarse();
            else if (panelLogin != null && panelLogin.activeInHierarchy) Entrar();
        }
        else if (TeclaTab())
        {
            SiguienteCampo(TeclaShift());
        }
    }

    // ---------- Flujo de sesion ----------

    private async void ComprobarSesion()
    {
        var sesion = SessionManager.Instance;
        if (usuarioLogin != null && string.IsNullOrEmpty(usuarioLogin.text))
            usuarioLogin.text = sesion.UltimoUsuario ?? "";

        if (sesion.HaySesion)
        {
            IrASiguienteEscena();
            return;
        }

        if (!sesion.TieneTokenGuardado)
        {
            MostrarLogin();
            return;
        }

        OcultarPaneles();
        if (botonReintentar != null) botonReintentar.gameObject.SetActive(false);
        MostrarMensaje("Comprobando sesión...");
        SetOcupado(true);

        try
        {
            var resultado = await sesion.RestaurarSesion();
            if (this == null) return;

            if (resultado.Ok)
            {
                IrASiguienteEscena();
                return;
            }

            if (resultado.SinConexion)
            {
                MostrarMensaje("Sin conexión con el servidor.");
                if (botonReintentar != null) botonReintentar.gameObject.SetActive(true);
                return;
            }

            MostrarLogin();
            if (resultado.Status == 401 || resultado.Status == 403)
                MostrarMensaje("Tu sesión expiró. Vuelve a iniciar sesión.");
            else if (resultado.Status != -1 && !string.IsNullOrEmpty(resultado.Error))
                MostrarMensaje(resultado.Error);
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            if (this == null) return;
            MostrarLogin();
            MostrarMensaje("No se pudo comprobar la sesión.");
        }
        finally
        {
            if (this != null) SetOcupado(false);
        }
    }

    private async void Entrar()
    {
        if (ocupado) return;

        string usuario = Texto(usuarioLogin);
        string password = passwordLogin != null ? passwordLogin.text : "";

        if (usuario.Length == 0) { Error("Escribe tu usuario.", usuarioLogin); return; }
        if (password.Length == 0) { Error("Escribe tu contraseña.", passwordLogin); return; }

        MostrarMensaje("Entrando...");
        SetOcupado(true);
        try
        {
            var resultado = await SessionManager.Instance.IniciarSesion(usuario, password);
            if (this == null) return;

            if (resultado.Ok) { IrASiguienteEscena(); return; }
            MostrarMensaje(MensajeError(resultado.Error, resultado.SinConexion, "No se pudo iniciar sesión."));
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            if (this != null) MostrarMensaje("No se pudo iniciar sesión.");
        }
        finally
        {
            if (this != null) SetOcupado(false);
        }
    }

    private async void Registrarse()
    {
        if (ocupado) return;

        string usuario = Texto(usuarioRegistro);
        string email = Texto(emailRegistro);
        string password = passwordRegistro != null ? passwordRegistro.text : "";
        string confirmar = passwordConfirmar != null ? passwordConfirmar.text : "";

        if (usuario.Length == 0) { Error("Escribe un nombre de usuario.", usuarioRegistro); return; }
        if (email.Length > 0 && (!email.Contains("@") || !email.Contains(".")))
        { Error("El correo no parece válido.", emailRegistro); return; }
        if (password.Length < minimoPassword)
        { Error($"La contraseña debe tener al menos {minimoPassword} caracteres.", passwordRegistro); return; }
        if (password != confirmar) { Error("Las contraseñas no coinciden.", passwordConfirmar); return; }

        MostrarMensaje("Creando cuenta...");
        SetOcupado(true);
        try
        {
            var resultado = await SessionManager.Instance.Registrar(usuario, email, password);
            if (this == null) return;

            if (resultado.Ok) { IrASiguienteEscena(); return; }
            MostrarMensaje(MensajeError(resultado.Error, resultado.SinConexion, "No se pudo crear la cuenta."));
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            if (this != null) MostrarMensaje("No se pudo crear la cuenta.");
        }
        finally
        {
            if (this != null) SetOcupado(false);
        }
    }

    private void IrASiguienteEscena()
    {
        if (cambiandoEscena) return;
        cambiandoEscena = true;
        MostrarMensaje("Conectando...");
        SceneManager.LoadScene(escenaSiguiente);
    }

    private void Salir()
    {
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    // ---------- Paneles ----------

    private void MostrarLogin()
    {
        if (botonReintentar != null) botonReintentar.gameObject.SetActive(false);
        if (panelRegistro != null) panelRegistro.SetActive(false);
        if (panelLogin != null) panelLogin.SetActive(true);
        MostrarMensaje("");

        // Si ya hay usuario, empieza en la contrasena
        Seleccionar(usuarioLogin != null && usuarioLogin.text.Length > 0 ? passwordLogin : usuarioLogin);
    }

    private void MostrarRegistro()
    {
        if (botonReintentar != null) botonReintentar.gameObject.SetActive(false);
        if (panelLogin != null) panelLogin.SetActive(false);
        if (panelRegistro != null) panelRegistro.SetActive(true);
        MostrarMensaje("");

        if (usuarioRegistro != null && usuarioRegistro.text.Length == 0 && usuarioLogin != null)
            usuarioRegistro.text = usuarioLogin.text.Trim();
        Seleccionar(usuarioRegistro);
    }

    private void OcultarPaneles()
    {
        if (panelLogin != null) panelLogin.SetActive(false);
        if (panelRegistro != null) panelRegistro.SetActive(false);
    }

    private void SetOcupado(bool valor)
    {
        ocupado = valor;
        if (cargando != null) cargando.SetActive(valor);

        foreach (var b in new[] { botonEntrar, botonRegistrarse, botonIrARegistro, botonIrALogin, botonReintentar })
            if (b != null) b.interactable = !valor;
    }

    // ---------- Utilidades ----------

    private void MostrarMensaje(string texto)
    {
        if (mensaje != null) mensaje.text = texto;
    }

    private void Error(string texto, InputField campo)
    {
        MostrarMensaje(texto);
        Seleccionar(campo);
    }

    private static string MensajeError(string error, bool sinConexion, string porDefecto)
    {
        if (sinConexion) return "Sin conexión con el servidor.";
        return string.IsNullOrEmpty(error) ? porDefecto : error;
    }

    private static string Texto(InputField campo)
    {
        return campo != null ? campo.text.Trim() : "";
    }

    private static void ConfigurarPassword(InputField campo)
    {
        if (campo == null) return;
        campo.contentType = InputField.ContentType.Password;
        campo.ForceLabelUpdate();
    }

    private static void Conectar(Button boton, UnityEngine.Events.UnityAction accion)
    {
        if (boton != null) boton.onClick.AddListener(accion);
    }

    private static void Seleccionar(InputField campo)
    {
        if (campo == null || !campo.gameObject.activeInHierarchy) return;
        campo.Select();
        campo.ActivateInputField();
    }

    // Tab pasa al siguiente campo del panel visible
    private void SiguienteCampo(bool haciaAtras)
    {
        var campos = new List<InputField>();
        if (panelRegistro != null && panelRegistro.activeInHierarchy)
            campos.AddRange(new[] { usuarioRegistro, emailRegistro, passwordRegistro, passwordConfirmar });
        else if (panelLogin != null && panelLogin.activeInHierarchy)
            campos.AddRange(new[] { usuarioLogin, passwordLogin });
        campos.RemoveAll(c => c == null);
        if (campos.Count == 0) return;

        var actual = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        int indice = campos.FindIndex(c => c.gameObject == actual);
        int siguiente = indice < 0 ? 0 : (indice + (haciaAtras ? -1 : 1) + campos.Count) % campos.Count;
        Seleccionar(campos[siguiente]);
    }

    private static bool TeclaEnter()
    {
#if ENABLE_INPUT_SYSTEM
        var k = Keyboard.current;
        return k != null && (k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame);
#else
        return Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
#endif
    }

    private static bool TeclaTab()
    {
#if ENABLE_INPUT_SYSTEM
        var k = Keyboard.current;
        return k != null && k.tabKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.Tab);
#endif
    }

    private static bool TeclaShift()
    {
#if ENABLE_INPUT_SYSTEM
        var k = Keyboard.current;
        return k != null && (k.leftShiftKey.isPressed || k.rightShiftKey.isPressed);
#else
        return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
#endif
    }
}
