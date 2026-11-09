using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Genera Assets/Scenes/Login.unity reciclando la UI del Lobby.
// Uso en batch: -batchmode -executeMethod GenerarEscenaLogin.Generar -quit
public static class GenerarEscenaLogin
{
    private const string RutaLobby = "Assets/Scenes/Lobby.unity";
    private const string RutaLogin = "Assets/Scenes/Login.unity";
    private const string Log = "[GenerarEscenaLogin] ";

    // Objetos raiz del Lobby que se copian (Canvas es obligatorio)
    private static readonly string[] RaicesACopiar =
        { "Canvas", "EventSystem", "Main Camera", "Directional Light", "Global Volume" };

    // Controles del Lobby que sobran en la copia
    private static readonly string[] ControlesSobrantes =
        { "CreateInputField", "JoinInputField", "CreateButton", "JoinButton", "ControlsButton", "Panel" };

    [MenuItem("Bearstards/Generar escena Login")]
    public static void GenerarDesdeMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Generar();
    }

    public static void Generar()
    {
        try
        {
            GenerarInterno();
            Debug.Log(Log + "OK");
        }
        catch (Exception e)
        {
            Debug.LogError(Log + "Error: " + e);
            if (Application.isBatchMode) EditorApplication.Exit(1);
        }
    }

    private static void GenerarInterno()
    {
        // 1. Abrir el Lobby (no se guarda nunca)
        Scene lobby = EditorSceneManager.OpenScene(RutaLobby, OpenSceneMode.Single);
        if (!lobby.IsValid()) throw new Exception("No se pudo abrir " + RutaLobby);

        var originales = new Dictionary<string, GameObject>();
        foreach (var raiz in lobby.GetRootGameObjects())
            if (RaicesACopiar.Contains(raiz.name) && !originales.ContainsKey(raiz.name))
                originales[raiz.name] = raiz;

        foreach (var nombre in RaicesACopiar)
            if (!originales.ContainsKey(nombre))
                Debug.LogWarning(Log + "No se encontro el objeto raiz '" + nombre + "' en el Lobby.");
        if (!originales.ContainsKey("Canvas")) throw new Exception("El Lobby no tiene un objeto raiz 'Canvas'.");

        // 2. Escena nueva y copias
        Scene login = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(login);

        var copias = new Dictionary<string, GameObject>();
        foreach (var par in originales)
        {
            var copia = (GameObject)Object.Instantiate(par.Value);
            copia.name = par.Key;
            if (copia.scene != login) SceneManager.MoveGameObjectToScene(copia, login);
            copias[par.Key] = copia;
        }

        // 3. Limpiar componentes del juego y listeners
        foreach (var copia in copias.Values)
        {
            QuitarScriptsDelJuego(copia);
            LimpiarEventos(copia);
        }

        GameObject canvasGO = copias["Canvas"];
        var canvas = canvasGO.GetComponent<Canvas>();
        if (copias.TryGetValue("Main Camera", out var camaraGO) && canvas != null)
            canvas.worldCamera = camaraGO.GetComponent<Camera>();
        else if (canvas != null)
        {
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            Debug.LogWarning(Log + "Sin camara copiada, el Canvas pasa a Screen Space Overlay.");
        }

        // 4. Plantillas
        GameObject plantillaInput = Requerido(canvasGO, "CreateInputField");
        GameObject plantillaBoton = Requerido(canvasGO, "CreateButton");
        GameObject botonSalirGO = Requerido(canvasGO, "ExitButton");
        if (Buscar(canvasGO, "LogoImg") == null) Debug.LogWarning(Log + "No se encontro 'LogoImg'.");

        if (plantillaInput.GetComponent<InputField>() == null)
            throw new Exception("'CreateInputField' no tiene InputField (legacy).");
        if (plantillaBoton.GetComponent<Button>() == null)
            throw new Exception("'CreateButton' no tiene Button.");

        Transform raizCanvas = canvasGO.transform;
        int indiceSobreFondo = IndiceDespuesDelFondo(canvasGO);

        // 5. Paneles
        RectTransform panelLogin = CrearPanel("PanelLogin", raizCanvas, indiceSobreFondo);
        RectTransform panelRegistro = CrearPanel("PanelRegistro", raizCanvas, indiceSobreFondo + 1);

        // Login
        var usuarioLogin = CrearInput(plantillaInput, panelLogin, "UsuarioLoginInput", "Usuario", new Vector2(0, 150));
        var passwordLogin = CrearInput(plantillaInput, panelLogin, "PasswordLoginInput", "Contraseña", new Vector2(0, 30));
        var botonEntrar = CrearBoton(plantillaBoton, panelLogin, "EntrarButton", "Entrar", new Vector2(-140, -95));
        var botonIrARegistro = CrearBoton(plantillaBoton, panelLogin, "IrARegistroButton", "Registrarme", new Vector2(140, -95));
        passwordLogin.contentType = InputField.ContentType.Password;

        // Registro
        var usuarioRegistro = CrearInput(plantillaInput, panelRegistro, "UsuarioRegistroInput", "Usuario", new Vector2(-170, 150));
        var emailRegistro = CrearInput(plantillaInput, panelRegistro, "EmailRegistroInput", "Correo (opc.)", new Vector2(170, 150));
        var passwordRegistro = CrearInput(plantillaInput, panelRegistro, "PasswordRegistroInput", "Contraseña", new Vector2(-170, 30));
        var passwordConfirmar = CrearInput(plantillaInput, panelRegistro, "PasswordConfirmarInput", "Confirmar contraseña", new Vector2(170, 30));
        var botonRegistrarse = CrearBoton(plantillaBoton, panelRegistro, "RegistrarseButton", "Crear cuenta", new Vector2(-140, -95));
        var botonIrALogin = CrearBoton(plantillaBoton, panelRegistro, "IrALoginButton", "Ya tengo", new Vector2(140, -95));
        emailRegistro.contentType = InputField.ContentType.EmailAddress;
        passwordRegistro.contentType = InputField.ContentType.Password;
        passwordConfirmar.contentType = InputField.ContentType.Password;

        // Fuera de los paneles
        var botonReintentar = CrearBoton(plantillaBoton, raizCanvas, "ReintentarButton", "Reintentar", new Vector2(0, -95));
        botonReintentar.gameObject.SetActive(false);

        botonSalirGO.name = "SalirButton";
        var botonSalir = botonSalirGO.GetComponent<Button>();
        PonerPosicion(botonSalirGO, new Vector2(0, -310));
        PonerTextoBoton(botonSalirGO, "Salir");

        var mensaje = CrearMensaje(botonSalirGO, raizCanvas, new Vector2(0, -195));

        // 6. Borrar lo que sobra del Lobby
        foreach (var nombre in ControlesSobrantes)
        {
            var go = Buscar(canvasGO, nombre);
            if (go != null) Object.DestroyImmediate(go);
            else Debug.LogWarning(Log + "No se encontro '" + nombre + "' para borrar (se ignora).");
        }

        panelLogin.gameObject.SetActive(true);
        panelRegistro.gameObject.SetActive(false);

        // 7. Objeto LoginUI y cableado
        var loginGO = new GameObject("LoginUI");
        if (loginGO.scene != login) SceneManager.MoveGameObjectToScene(loginGO, login);
        var loginUI = loginGO.AddComponent<LoginUI>();

        var so = new SerializedObject(loginUI);
        Asignar(so, "panelLogin", panelLogin.gameObject);
        Asignar(so, "panelRegistro", panelRegistro.gameObject);
        Asignar(so, "usuarioLogin", usuarioLogin);
        Asignar(so, "passwordLogin", passwordLogin);
        Asignar(so, "usuarioRegistro", usuarioRegistro);
        Asignar(so, "emailRegistro", emailRegistro);
        Asignar(so, "passwordRegistro", passwordRegistro);
        Asignar(so, "passwordConfirmar", passwordConfirmar);
        Asignar(so, "botonEntrar", botonEntrar);
        Asignar(so, "botonRegistrarse", botonRegistrarse);
        Asignar(so, "botonIrARegistro", botonIrARegistro);
        Asignar(so, "botonIrALogin", botonIrALogin);
        Asignar(so, "botonSalir", botonSalir);
        Asignar(so, "botonReintentar", botonReintentar);
        Asignar(so, "mensaje", mensaje);
        var escena = so.FindProperty("escenaSiguiente");
        if (escena != null) escena.stringValue = "Loading";
        so.ApplyModifiedPropertiesWithoutUndo();

        // 8. Guardar Login, cerrar Lobby sin guardar
        if (!EditorSceneManager.SaveScene(login, RutaLogin))
            throw new Exception("No se pudo guardar " + RutaLogin);
        SceneManager.SetActiveScene(login);
        if (!EditorSceneManager.CloseScene(lobby, true))
            Debug.LogWarning(Log + "No se pudo cerrar el Lobby (no se guardo).");

        AssetDatabase.ImportAsset(RutaLogin, ImportAssetOptions.ForceSynchronousImport);

        // 9. Login primero en Build Settings
        var escenas = EditorBuildSettings.scenes
            .Where(s => s.path != RutaLogin)
            .ToList();
        escenas.Insert(0, new EditorBuildSettingsScene(RutaLogin, true));
        EditorBuildSettings.scenes = escenas.ToArray();
    }

    // ---------- Construccion ----------

    private static RectTransform CrearPanel(string nombre, Transform padre, int indice)
    {
        var go = new GameObject(nombre, typeof(RectTransform));
        go.layer = padre.gameObject.layer;
        var rt = (RectTransform)go.transform;
        rt.SetParent(padre, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.localScale = Vector3.one;
        rt.SetSiblingIndex(Mathf.Clamp(indice, 0, padre.childCount - 1));
        return rt;
    }

    private static InputField CrearInput(GameObject plantilla, Transform padre, string nombre, string placeholder, Vector2 pos)
    {
        var go = (GameObject)Object.Instantiate(plantilla, padre, false);
        go.name = nombre;
        PonerPosicion(go, pos);

        var input = go.GetComponent<InputField>();
        input.text = "";
        input.contentType = InputField.ContentType.Standard;

        if (input.placeholder is Text ph)
        {
            ph.text = placeholder;
            ph.resizeTextForBestFit = true;
            ph.resizeTextMinSize = 14;
            ph.resizeTextMaxSize = Mathf.Max(14, ph.fontSize);
        }
        else Debug.LogWarning(Log + nombre + ": el placeholder no es un Text legacy.");

        return input;
    }

    private static Button CrearBoton(GameObject plantilla, Transform padre, string nombre, string texto, Vector2 pos)
    {
        var go = (GameObject)Object.Instantiate(plantilla, padre, false);
        go.name = nombre;
        PonerPosicion(go, pos);
        PonerTextoBoton(go, texto);
        return go.GetComponent<Button>();
    }

    private static void PonerTextoBoton(GameObject boton, string texto)
    {
        var label = boton.GetComponentInChildren<Text>(true);
        if (label == null)
        {
            Debug.LogWarning(Log + boton.name + ": no tiene Text legacy para la etiqueta.");
            return;
        }
        label.text = texto;
        AjustarAlBoton(label);
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.resizeTextForBestFit = true;
        label.resizeTextMinSize = 14;
        label.resizeTextMaxSize = Mathf.Max(14, label.fontSize);
    }

    // La etiqueta ocupa todo el boton (con margen) y queda centrada, para que el texto no se salga
    private static void AjustarAlBoton(Text label)
    {
        var rt = label.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = new Vector2(12, 8);
        rt.offsetMax = new Vector2(-12, -8);
        label.alignment = TextAnchor.MiddleCenter;
        label.verticalOverflow = VerticalWrapMode.Truncate;
    }

    // Texto de estado: copia de la etiqueta de un boton (misma fuente)
    private static Text CrearMensaje(GameObject botonFuente, Transform padre, Vector2 pos)
    {
        var fuente = botonFuente.GetComponentInChildren<Text>(true);
        if (fuente == null) throw new Exception("No hay Text legacy para clonar el mensaje.");

        var go = (GameObject)Object.Instantiate(fuente.gameObject, padre, false);
        go.name = "MensajeText";
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(1000, 60);
        rt.anchoredPosition = pos;
        rt.localScale = Vector3.one;

        var texto = go.GetComponent<Text>();
        texto.text = "";
        texto.alignment = TextAnchor.MiddleCenter;
        texto.horizontalOverflow = HorizontalWrapMode.Wrap;
        texto.verticalOverflow = VerticalWrapMode.Truncate;
        texto.resizeTextForBestFit = true;
        texto.resizeTextMinSize = 14;
        texto.resizeTextMaxSize = 30;
        texto.color = new Color(1f, 0.95f, 0.85f, 1f);
        texto.raycastTarget = false;

        var borde = go.GetComponent<Outline>();
        if (borde == null) borde = go.AddComponent<Outline>();
        borde.effectColor = new Color(0f, 0f, 0f, 0.85f);
        borde.effectDistance = new Vector2(2f, -2f);
        return texto;
    }

    private static void PonerPosicion(GameObject go, Vector2 pos)
    {
        if (go.transform is RectTransform rt)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
        }
    }

    // El fondo ("Image") va primero; los paneles van justo encima
    private static int IndiceDespuesDelFondo(GameObject canvasGO)
    {
        var fondo = canvasGO.transform.Find("Image");
        return fondo != null ? fondo.GetSiblingIndex() + 1 : 0;
    }

    // ---------- Limpieza ----------

    private static void QuitarScriptsDelJuego(GameObject raiz)
    {
        foreach (var t in raiz.GetComponentsInChildren<Transform>(true))
        {
            int faltantes = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
            if (faltantes > 0) Debug.LogWarning(Log + "Quitados " + faltantes + " scripts perdidos en " + t.name);

            foreach (var mb in t.GetComponents<MonoBehaviour>())
            {
                if (mb == null) continue;
                string ns = mb.GetType().Namespace ?? "";
                if (ns.StartsWith("UnityEngine") || ns.StartsWith("TMPro")) continue;
                Debug.Log(Log + "Quitando " + mb.GetType().Name + " de " + t.name);
                Object.DestroyImmediate(mb);
            }
        }
    }

    private static void LimpiarEventos(GameObject raiz)
    {
        foreach (var b in raiz.GetComponentsInChildren<Button>(true))
            QuitarListeners(b.onClick);

        foreach (var i in raiz.GetComponentsInChildren<InputField>(true))
        {
            QuitarListeners(i.onSubmit);
            QuitarListeners(i.onEndEdit);
            QuitarListeners(i.onValueChanged);
        }
    }

    private static void QuitarListeners(UnityEventBase evento)
    {
        for (int i = evento.GetPersistentEventCount() - 1; i >= 0; i--)
            UnityEventTools.RemovePersistentListener(evento, i);
    }

    // ---------- Busqueda y asignacion ----------

    private static GameObject Buscar(GameObject raiz, string nombre)
    {
        foreach (var t in raiz.GetComponentsInChildren<Transform>(true))
            if (t.name == nombre) return t.gameObject;
        return null;
    }

    private static GameObject Requerido(GameObject raiz, string nombre)
    {
        var go = Buscar(raiz, nombre);
        if (go == null) throw new Exception("No se encontro '" + nombre + "' dentro de '" + raiz.name + "' en el Lobby.");
        return go;
    }

    private static void Asignar(SerializedObject so, string campo, Object valor)
    {
        var prop = so.FindProperty(campo);
        if (prop == null)
        {
            Debug.LogError(Log + "LoginUI no tiene el campo '" + campo + "'.");
            return;
        }
        if (valor == null) Debug.LogWarning(Log + "Valor nulo para '" + campo + "'.");
        prop.objectReferenceValue = valor;
    }
}
