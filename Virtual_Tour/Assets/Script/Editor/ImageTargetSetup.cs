using System.Collections.Generic;
using System.IO;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.XR.ARSubsystems;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

/// <summary>
/// Outil Editor qui met en place toute la chaine "image target -> objet 3D" en un clic :
/// creation du prefab placeholder, enregistrement des marqueurs dans la ReferenceImageLibrary,
/// et ajout/cablage des composants sur le XR Origin de la scene ouverte.
///
/// Menu : Virtual Tour > Configurer l'Image Tracking
///
/// Relancable sans risque : les marqueurs deja presents dans la library ne sont pas touches.
/// Pour ajouter une station, ajoute une ligne dans k_Markers puis relance le menu.
/// </summary>
static class ImageTargetSetup
{
    /// <summary>
    /// Description d'un marqueur du parcours.
    /// </summary>
    readonly struct MarkerDefinition
    {
        /// <summary>Chemin de la texture dans le projet.</summary>
        public readonly string texturePath;

        /// <summary>Nom dans la library. C'est la cle utilisee par ImageTargetSpawner.</summary>
        public readonly string imageName;

        /// <summary>
        /// Largeur PHYSIQUE REELLE du marqueur imprime, en metres.
        /// La hauteur est deduite du ratio de la texture, il ne faut donc surtout pas
        /// deformer l'image a l'impression.
        /// </summary>
        public readonly float printedWidthMeters;

        public MarkerDefinition(string texturePath, string imageName, float printedWidthMeters)
        {
            this.texturePath = texturePath;
            this.imageName = imageName;
            this.printedWidthMeters = printedWidthMeters;
        }
    }

    // >>> Les marqueurs du parcours. Ajuste printedWidthMeters a ton impression reelle,
    //     mesuree a la regle : ARKit l'exige et ARCore s'en sert pour l'echelle. <<<
    static readonly MarkerDefinition[] k_Markers =
    {
        new("Assets/Tour Eiffel.jpg", "TourEiffel_Station01", 0.15f),
    };

    const string k_LibraryPath = "Assets/ReferenceImageLibrary.asset";
    const string k_PrefabFolder = "Assets/Prefabs";
    const string k_MaterialFolder = "Assets/Prefabs/Materials";
    const string k_PrefabPath = k_PrefabFolder + "/ARContent_Placeholder.prefab";
    const string k_MaterialPath = k_MaterialFolder + "/ARContent_Placeholder_Mat.mat";

    [MenuItem("Virtual Tour/Configurer l'Image Tracking")]
    static void Configure()
    {
        var prefab = CreatePlaceholderPrefab();
        if (prefab == null)
            return;

        var library = RegisterMarkersInLibrary();
        if (library == null)
            return;

        WireUpScene(library, prefab);
    }

    /// <summary>
    /// Cree Assets/Prefabs/ARContent_Placeholder.prefab (cube URP de 10 cm qui tourne lentement).
    /// </summary>
    static GameObject CreatePlaceholderPrefab()
    {
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(k_PrefabPath);
        if (existing != null)
        {
            Debug.Log($"[Setup] Prefab deja present : {k_PrefabPath}");
            return existing;
        }

        EnsureFolder(k_PrefabFolder);
        EnsureFolder(k_MaterialFolder);

        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            Debug.LogError("[Setup] Shader URP/Lit introuvable. Le projet utilise-t-il bien URP ?");
            return null;
        }

        var material = new Material(shader);
        material.SetColor("_BaseColor", new Color(0f, 0.85f, 1f));
        AssetDatabase.CreateAsset(material, k_MaterialPath);

        // Racine vide : c'est elle qui sera alignee sur le plan de l'image.
        var root = new GameObject("ARContent_Placeholder");
        root.AddComponent<SpinSlowly>();

        // Cube de 10 cm, remonte de 5 cm sur +Y pour reposer SUR la surface de l'image
        // et non a moitie dedans (+Y sort de la surface d'une ARTrackedImage).
        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = "Cube";
        cube.transform.SetParent(root.transform, false);
        cube.transform.localScale = Vector3.one * 0.1f;
        cube.transform.localPosition = new Vector3(0f, 0.05f, 0f);
        cube.GetComponent<Renderer>().sharedMaterial = material;

        // Pas de physique sur du contenu AR purement visuel.
        Object.DestroyImmediate(cube.GetComponent<BoxCollider>());

        var saved = PrefabUtility.SaveAsPrefabAsset(root, k_PrefabPath);
        Object.DestroyImmediate(root);

        Debug.Log($"[Setup] Prefab cree : {k_PrefabPath}");
        return saved;
    }

    /// <summary>
    /// Ajoute chaque marqueur de k_Markers a la ReferenceImageLibrary, avec son nom et sa taille
    /// physique. La hauteur est calculee depuis le ratio reel de la texture.
    /// </summary>
    static XRReferenceImageLibrary RegisterMarkersInLibrary()
    {
        var library = AssetDatabase.LoadAssetAtPath<XRReferenceImageLibrary>(k_LibraryPath);
        if (library == null)
        {
            Debug.LogError($"[Setup] ReferenceImageLibrary introuvable a {k_LibraryPath}");
            return null;
        }

        var changed = 0;

        foreach (var marker in k_Markers)
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(marker.texturePath);
            if (texture == null)
            {
                Debug.LogError($"[Setup] Texture introuvable a \"{marker.texturePath}\"");
                continue;
            }

            // La taille physique DOIT respecter le ratio de l'image, sinon la pose estimee
            // est deformee et le suivi devient erratique.
            var aspect = (float)texture.height / texture.width;
            var size = new Vector2(marker.printedWidthMeters, marker.printedWidthMeters * aspect);

            // On cherche l'entree par GUID de texture, pas par nom : si la meme image a deja ete
            // ajoutee a la main dans l'Inspector (nom different, taille non renseignee), on la
            // repare au lieu de creer un doublon. Deux entrees pointant la meme texture rendraient
            // la detection ambigue et peuvent faire echouer le build ARCore.
            var index = FindIndexByTexture(library, marker.texturePath);
            var isNew = index < 0;

            if (isNew)
            {
                index = library.count;
                library.Add();
                library.SetTexture(index, texture, false);
            }
            else if (library[index].name == marker.imageName &&
                     library[index].specifySize &&
                     library[index].size == size)
            {
                // Deja exactement dans l'etat voulu.
                continue;
            }

            library.SetName(index, marker.imageName);
            library.SetSpecifySize(index, true);
            library.SetSize(index, size);

            changed++;

            Debug.Log(
                $"[Setup] \"{marker.imageName}\" {(isNew ? "ajoute" : "mis a jour")} : " +
                $"{texture.width}x{texture.height} px, imprime en {size.x:0.###} x {size.y:0.###} m.");

            // ARCore refuse les images de moins de 300 px sur le plus petit cote.
            if (Mathf.Min(texture.width, texture.height) < 300)
            {
                Debug.LogWarning(
                    $"[Setup] \"{marker.imageName}\" fait moins de 300 px sur son plus petit cote. " +
                    "ARCore risque de la rejeter : utilise une version haute resolution.", texture);
            }
        }

        if (changed > 0)
        {
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            Selection.activeObject = library;

            Debug.Log(
                "[Setup] Library mise a jour. Regarde la note de qualite de chaque image dans " +
                "l'Inspector (selectionne maintenant) : vise 60+, idealement 80+.", library);
        }

        return library;
    }

    /// <summary>
    /// Index de l'entree de la library qui utilise cette texture, ou -1 si aucune.
    /// </summary>
    static int FindIndexByTexture(XRReferenceImageLibrary library, string texturePath)
    {
        var guidString = AssetDatabase.AssetPathToGUID(texturePath);
        if (string.IsNullOrEmpty(guidString))
            return -1;

        var guid = new System.Guid(guidString);

        for (var i = 0; i < library.count; i++)
        {
            if (library[i].textureGuid == guid)
                return i;
        }

        return -1;
    }

    /// <summary>
    /// Ajoute ARTrackedImageManager + ImageTargetSpawner sur le XR Origin de la scene ouverte,
    /// et remplit leurs references.
    /// </summary>
    static void WireUpScene(XRReferenceImageLibrary library, GameObject prefab)
    {
        var origin = Object.FindFirstObjectByType<XROrigin>();
        if (origin == null)
        {
            Debug.LogError("[Setup] Aucun XR Origin dans la scene ouverte. Ouvre SampleScene.");
            return;
        }

        var go = origin.gameObject;

        var manager = go.GetComponent<ARTrackedImageManager>();
        if (manager == null)
            manager = Undo.AddComponent<ARTrackedImageManager>(go);

        manager.referenceLibrary = library;
        manager.requestedMaxNumberOfMovingImages = 1;
        // trackedImagePrefab volontairement laisse vide : c'est ImageTargetSpawner qui instancie.
        manager.trackedImagePrefab = null;

        // Optionnel mais recommande : sert au mode de persistance "PlaceOnce" de l'ImageTargetSpawner,
        // qui ancre l'objet dans le monde pour qu'il ne derive pas une fois le marqueur perdu.
        if (go.GetComponent<ARAnchorManager>() == null)
            Undo.AddComponent<ARAnchorManager>(go);

        var spawner = go.GetComponent<ImageTargetSpawner>();
        if (spawner == null)
            spawner = Undo.AddComponent<ImageTargetSpawner>(go);

        // m_Targets est prive et [SerializeField] : on passe par SerializedObject.
        var so = new SerializedObject(spawner);
        var targets = so.FindProperty("m_Targets");

        // On n'ajoute que les entrees manquantes, pour ne pas ecraser les prefabs
        // deja assignes a la main dans l'Inspector.
        var wired = new HashSet<string>();
        for (var i = 0; i < targets.arraySize; i++)
            wired.Add(targets.GetArrayElementAtIndex(i).FindPropertyRelative("referenceImageName").stringValue);

        foreach (var marker in k_Markers)
        {
            if (!wired.Add(marker.imageName))
                continue;

            var index = targets.arraySize;
            targets.arraySize = index + 1;

            var entry = targets.GetArrayElementAtIndex(index);
            entry.FindPropertyRelative("referenceImageName").stringValue = marker.imageName;
            entry.FindPropertyRelative("prefab").objectReferenceValue = prefab;
        }

        so.ApplyModifiedProperties();

        EditorUtility.SetDirty(manager);
        EditorUtility.SetDirty(spawner);
        EditorSceneManager.MarkSceneDirty(go.scene);
        EditorSceneManager.SaveScene(go.scene);

        Debug.Log($"[Setup] Scene \"{go.scene.name}\" configuree et sauvegardee.");
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;

        var parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
