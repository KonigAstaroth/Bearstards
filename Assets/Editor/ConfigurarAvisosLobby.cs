using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Agrega al Lobby el texto de avisos de sala (errores al crear/unirse) y lo conecta a CreateAndJoinRooms.
// Batch: -batchmode -executeMethod ConfigurarAvisosLobby.Configurar -quit
public static class ConfigurarAvisosLobby
{
    private const string RutaEscena = "Assets/Scenes/Lobby.unity";
    private const string NombreAviso = "AvisoSala";

    [MenuItem("Bearstards/Configurar avisos del Lobby")]
    public static void Configurar()
    {
        try
        {
            Scene escena = EditorSceneManager.OpenScene(RutaEscena, OpenSceneMode.Single);

            Transform canvas = BuscarEnEscena(escena, "Canvas");
            if (canvas == null) throw new Exception("No se encontro 'Canvas' en " + RutaEscena);

            RectTransform exitButton = BuscarRecursivo(canvas, "ExitButton") as RectTransform;
            if (exitButton == null) throw new Exception("No se encontro 'ExitButton' dentro del Canvas");

            Text textoBase = exitButton.GetComponentInChildren<Text>(true);
            if (textoBase == null) throw new Exception("'ExitButton' no tiene un Text (Legacy) hijo para clonar");

            RectTransform crearBoton = BuscarRecursivo(canvas, "CreateButton") as RectTransform;
            RectTransform crearInput = BuscarRecursivo(canvas, "CreateInputField") as RectTransform;
            RectTransform unirBoton = BuscarRecursivo(canvas, "JoinButton") as RectTransform;
            if (crearBoton == null || crearInput == null || unirBoton == null)
                throw new Exception("Faltan 'CreateButton', 'CreateInputField' o 'JoinButton' en el Canvas");

            CreateAndJoinRooms salas = BuscarSalas(escena);
            if (salas == null) throw new Exception("No hay un CreateAndJoinRooms en la escena");

            // Idempotente: si ya existe, se reconstruye.
            Transform viejo = BuscarRecursivo(canvas, NombreAviso);
            while (viejo != null)
            {
                UnityEngine.Object.DestroyImmediate(viejo.gameObject);
                viejo = BuscarRecursivo(canvas, NombreAviso);
            }

            // Hueco entre la fila de Create (la de abajo) y ExitButton.
            float abajoFilas = Mathf.Min(Abajo(crearBoton), Abajo(crearInput));
            float arribaSalir = Arriba(exitButton);
            float hueco = abajoFilas - arribaSalir;
            if (hueco < 20f)
            {
                Debug.LogWarning("[ConfigurarAvisosLobby] Hay poco espacio entre las filas y ExitButton (" + hueco + "), se usa 30");
                hueco = 30f;
                arribaSalir = abajoFilas - hueco;
            }

            Text aviso = ClonarTexto(textoBase, canvas, NombreAviso);
            RectTransform rt = aviso.rectTransform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, (abajoFilas + arribaSalir) * 0.5f);
            rt.sizeDelta = new Vector2(820f, Mathf.Max(hueco - 2f, 20f));
            rt.SetAsLastSibling();

            aviso.text = "";
            aviso.fontStyle = FontStyle.Bold;
            aviso.alignment = TextAnchor.MiddleCenter;
            aviso.color = new Color(1f, 0.92f, 0.6f, 1f);
            aviso.fontSize = 24;
            aviso.resizeTextForBestFit = true;
            aviso.resizeTextMinSize = 12;
            aviso.resizeTextMaxSize = 24;
            aviso.horizontalOverflow = HorizontalWrapMode.Wrap;
            aviso.verticalOverflow = VerticalWrapMode.Truncate;

            // Contorno oscuro para que se lea sobre el fondo.
            foreach (Shadow s in aviso.GetComponents<Shadow>())
            {
                UnityEngine.Object.DestroyImmediate(s);
            }
            Outline contorno = aviso.gameObject.AddComponent<Outline>();
            contorno.effectColor = new Color(0f, 0f, 0f, 0.85f);
            contorno.effectDistance = new Vector2(1.5f, -1.5f);

            SerializedObject so = new SerializedObject(salas);
            AsignarCampo(so, "aviso", aviso);
            AsignarCampo(so, "botonCrear", crearBoton.GetComponent<Button>());
            AsignarCampo(so, "botonUnir", unirBoton.GetComponent<Button>());
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(escena);
            if (!EditorSceneManager.SaveScene(escena)) throw new Exception("No se pudo guardar " + RutaEscena);

            Debug.Log("[ConfigurarAvisosLobby] OK");
        }
        catch (Exception e)
        {
            Debug.LogError("[ConfigurarAvisosLobby] Error: " + e);
            if (Application.isBatchMode) EditorApplication.Exit(1);
        }
    }

    // Bordes en coordenadas del padre (todos van anclados al centro del Canvas).
    private static float Abajo(RectTransform rt)
    {
        return rt.anchoredPosition.y - rt.sizeDelta.y * rt.pivot.y * rt.localScale.y;
    }

    private static float Arriba(RectTransform rt)
    {
        return rt.anchoredPosition.y + rt.sizeDelta.y * (1f - rt.pivot.y) * rt.localScale.y;
    }

    private static CreateAndJoinRooms BuscarSalas(Scene escena)
    {
        foreach (GameObject raiz in escena.GetRootGameObjects())
        {
            CreateAndJoinRooms c = raiz.GetComponentInChildren<CreateAndJoinRooms>(true);
            if (c != null) return c;
        }
        return null;
    }

    private static Text ClonarTexto(Text original, Transform padre, string nombre)
    {
        GameObject go = UnityEngine.Object.Instantiate(original.gameObject, padre, false);
        go.name = nombre;
        go.layer = padre.gameObject.layer;

        // Por si el original tuviera hijos.
        for (int i = go.transform.childCount - 1; i >= 0; i--)
        {
            UnityEngine.Object.DestroyImmediate(go.transform.GetChild(i).gameObject);
        }

        Text texto = go.GetComponent<Text>();
        texto.raycastTarget = false;
        texto.rectTransform.localScale = Vector3.one;
        return texto;
    }

    private static void AsignarCampo(SerializedObject so, string campo, UnityEngine.Object valor)
    {
        SerializedProperty prop = so.FindProperty(campo);
        if (prop == null) throw new Exception("CreateAndJoinRooms no tiene el campo '" + campo + "'");
        if (valor == null) throw new Exception("No hay valor para el campo '" + campo + "'");
        prop.objectReferenceValue = valor;
    }

    private static Transform BuscarEnEscena(Scene escena, string nombre)
    {
        foreach (GameObject raiz in escena.GetRootGameObjects())
        {
            Transform t = BuscarRecursivo(raiz.transform, nombre);
            if (t != null) return t;
        }
        Debug.LogError("[ConfigurarAvisosLobby] Falta el objeto '" + nombre + "' en la escena");
        return null;
    }

    private static Transform BuscarRecursivo(Transform padre, string nombre)
    {
        if (padre.name == nombre) return padre;
        foreach (Transform hijo in padre)
        {
            Transform t = BuscarRecursivo(hijo, nombre);
            if (t != null) return t;
        }
        return null;
    }
}
