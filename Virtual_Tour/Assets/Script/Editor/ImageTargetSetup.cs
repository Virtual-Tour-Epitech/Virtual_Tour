using System.Collections.Generic;
using System.IO;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.XR.ARSubsystems;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

static class ImageTargetSetup
{
    const string k_LibraryPath = "Assets/ReferenceImageLibrary.asset";
    const string k_MaterialFolder = "Assets/Materials";
    const string k_MaterialPath = k_MaterialFolder + "/ARContent_Placeholder_Mat.mat";
    const string k_ContentPrefix = "ARContent_";

    [MenuItem("Virtual Tour/Configurer l'Image Tracking")]
    static void Configure()
    {
        var catalog = LoadCatalog();
        if (catalog == null)
            return;

        if (!Validate(catalog))
            return;

        var library = RegisterMarkersInLibrary(catalog);
        if (library == null)
            return;

        WireUpScene(catalog, library);
    }

    static MarkerCatalog LoadCatalog()
    {
        var guids = AssetDatabase.FindAssets("t:MarkerCatalog");

        if (guids.Length == 0)
        {
            Debug.LogError(
                "[Setup] Aucun MarkerCatalog dans le projet. " +
                "Cree-le avec Create > Virtual Tour > Marker Catalog.");
            return null;
        }

        if (guids.Length > 1)
        {
            Debug.LogError(
                $"[Setup] {guids.Length} MarkerCatalog trouves, il n'en faut qu'un seul :\n  - " +
                string.Join("\n  - ", System.Array.ConvertAll(guids, AssetDatabase.GUIDToAssetPath)));
            return null;
        }

        return AssetDatabase.LoadAssetAtPath<MarkerCatalog>(AssetDatabase.GUIDToAssetPath(guids[0]));
    }

    static bool Validate(MarkerCatalog catalog)
    {
        var problems = new List<string>();
        var names = new HashSet<string>();

        for (var i = 0; i < catalog.Markers.Count; i++)
        {
            var marker = catalog.Markers[i];
            var label = $"entree {i}";

            if (marker == null)
            {
                problems.Add($"{label} : vide");
                continue;
            }

            if (marker.image == null)
                problems.Add($"{label} (\"{marker.imageName}\") : aucune image assignee");

            if (string.IsNullOrWhiteSpace(marker.imageName))
                problems.Add($"{label} : Image Name vide");
            else if (!names.Add(marker.imageName))
                problems.Add($"{label} : Image Name \"{marker.imageName}\" en double");

            // On ne valide que le jeu actif : une station en mode ecran n'a pas besoin
            // d'avoir ses dimensions papier renseignees tant qu'on n'a pas mesure le panneau.
            if (marker.ActiveWidthCm <= 0f)
                problems.Add($"{label} (\"{marker.imageName}\") : mode {marker.ModeLabel}, mais largeur invalide ({marker.ActiveWidthCm} cm)");

            if (marker.ActiveHeightCm <= 0f)
                problems.Add($"{label} (\"{marker.imageName}\") : mode {marker.ModeLabel}, mais hauteur invalide ({marker.ActiveHeightCm} cm)");
        }

        if (problems.Count == 0)
            return true;

        Debug.LogError(
            "[Setup] Catalogue invalide, aucune modification effectuee :\n  - " +
            string.Join("\n  - ", problems), catalog);

        return false;
    }

    static XRReferenceImageLibrary RegisterMarkersInLibrary(MarkerCatalog catalog)
    {
        var library = AssetDatabase.LoadAssetAtPath<XRReferenceImageLibrary>(k_LibraryPath);
        if (library == null)
        {
            Debug.LogError($"[Setup] ReferenceImageLibrary introuvable a {k_LibraryPath}");
            return null;
        }

        var changed = 0;

        foreach (var marker in catalog.Markers)
        {
            var texture = marker.image;

            EnsureReferenceImageImportSettings(texture);

            var size = new Vector2(marker.WidthInMeters, marker.HeightInMeters);

            WarnOnRatioMismatch(marker, texture);

            var index = FindIndexByTexture(library, texture);
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
                continue;
            }

            library.SetName(index, marker.imageName);
            library.SetSpecifySize(index, true);
            library.SetSize(index, size);

            changed++;

            Debug.Log(
                $"[Setup] \"{marker.imageName}\" {(isNew ? "ajoute" : "mis a jour")} : " +
                $"{texture.width}x{texture.height} px, {marker.ModeLabel} {size.x:0.###} x {size.y:0.###} m.");

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

    /// La taille physique declaree devrait respecter le ratio de l'image : si les deux divergent,
    /// la pose estimee par ARKit/ARCore est deformee et le suivi devient erratique.
    static void WarnOnRatioMismatch(MarkerCatalog.Marker marker, Texture2D texture)
    {
        var imageRatio = (float)texture.height / texture.width;
        var declaredRatio = marker.ActiveHeightCm / marker.ActiveWidthCm;

        if (Mathf.Abs(declaredRatio - imageRatio) / imageRatio < 0.02f)
            return;

        Debug.LogWarning(
            $"[Setup] \"{marker.imageName}\" : la taille {marker.ModeLabel} declaree " +
            $"({marker.ActiveWidthCm} x {marker.ActiveHeightCm} cm, ratio 1:{declaredRatio:0.###}) " +
            $"ne respecte pas le ratio de l'image " +
            $"({texture.width}x{texture.height} px, ratio 1:{imageRatio:0.###}). " +
            $"Pour cette largeur, la hauteur coherente serait {marker.ActiveWidthCm * imageRatio:0.#} cm.",
            texture);
    }

    static void EnsureReferenceImageImportSettings(Texture2D texture)
    {
        var texturePath = AssetDatabase.GetAssetPath(texture);

        if (AssetImporter.GetAtPath(texturePath) is not TextureImporter importer)
            return;

        var needsReimport = false;

        if (importer.npotScale != TextureImporterNPOTScale.None)
        {
            importer.npotScale = TextureImporterNPOTScale.None;
            needsReimport = true;
        }

        if (importer.mipmapEnabled)
        {
            importer.mipmapEnabled = false;
            needsReimport = true;
        }

        if (!needsReimport)
            return;

        importer.SaveAndReimport();

        Debug.Log(
            $"[Setup] Reglages d'import corriges pour \"{texturePath}\" " +
            "(pas de redimensionnement en puissance de deux, mipmaps desactivees).");
    }

    static int FindIndexByTexture(XRReferenceImageLibrary library, Texture2D texture)
    {
        // Meme appel que GetGuidForTexture() d'AR Foundation : le GUID lu ici et celui ecrit
        // par SetTexture() concordent donc toujours, y compris apres un renommage de fichier.
        if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(texture, out var guidString, out long _))
            return -1;

        var guid = new System.Guid(guidString);

        for (var i = 0; i < library.count; i++)
        {
            if (library[i].textureGuid == guid)
                return i;
        }

        return -1;
    }

    static void WireUpScene(MarkerCatalog catalog, XRReferenceImageLibrary library)
    {
        var origin = Object.FindAnyObjectByType<XROrigin>();
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
        manager.requestedMaxNumberOfMovingImages = catalog.Markers.Count;
        manager.trackedImagePrefab = null;

        if (go.GetComponent<ARAnchorManager>() == null)
            Undo.AddComponent<ARAnchorManager>(go);

        var spawner = go.GetComponent<ImageTargetSpawner>();
        if (spawner == null)
            spawner = Undo.AddComponent<ImageTargetSpawner>(go);

        var so = new SerializedObject(spawner);
        var targets = so.FindProperty("m_Targets");

        var wired = new Dictionary<string, int>();
        for (var i = 0; i < targets.arraySize; i++)
        {
            var name = targets.GetArrayElementAtIndex(i).FindPropertyRelative("referenceImageName").stringValue;
            wired[name] = i;
        }

        foreach (var marker in catalog.Markers)
        {
            if (!wired.TryGetValue(marker.imageName, out var index))
            {
                index = targets.arraySize;
                targets.arraySize = index + 1;

                var created = targets.GetArrayElementAtIndex(index);
                created.FindPropertyRelative("referenceImageName").stringValue = marker.imageName;

                // Agrandir un tableau serialise DUPLIQUE le dernier element : sans ce reset,
                // la nouvelle station herite du sceneObject de la precedente.
                created.FindPropertyRelative("sceneObject").objectReferenceValue = null;
            }

            var slot = targets.GetArrayElementAtIndex(index).FindPropertyRelative("sceneObject");
            var expectedName = k_ContentPrefix + marker.imageName;
            var current = slot.objectReferenceValue;

            if (current == null || current.name != expectedName)
            {
                if (current != null)
                {
                    Debug.LogWarning(
                        $"[Setup] \"{marker.imageName}\" pointait sur \"{current.name}\" au lieu de " +
                        $"\"{expectedName}\". Reference corrigee.");
                }

                slot.objectReferenceValue = FindOrCreateSceneContent(go.scene, marker);
            }
        }

        so.ApplyModifiedProperties();

        EditorUtility.SetDirty(manager);
        EditorUtility.SetDirty(spawner);
        EditorSceneManager.MarkSceneDirty(go.scene);
        EditorSceneManager.SaveScene(go.scene);

        Debug.Log($"[Setup] Scene \"{go.scene.name}\" configuree et sauvegardee.");
    }

    static GameObject FindOrCreateSceneContent(Scene scene, MarkerCatalog.Marker marker)
    {
        var objectName = k_ContentPrefix + marker.imageName;

        foreach (var root in scene.GetRootGameObjects())
        {
            if (root.name == objectName)
                return root;
        }

        var material = FindOrCreatePlaceholderMaterial();
        if (material == null)
            return null;

        var content = new GameObject(objectName);

        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = "Cube";
        cube.transform.SetParent(content.transform, false);
        cube.transform.localScale = Vector3.one * 0.1f;
        cube.transform.localPosition = new Vector3(0f, 0.05f, 0f);
        cube.GetComponent<Renderer>().sharedMaterial = material;

        Object.DestroyImmediate(cube.GetComponent<BoxCollider>());

        content.SetActive(false);

        Undo.RegisterCreatedObjectUndo(content, "Creer le contenu AR");

        Debug.Log(
            $"[Setup] \"{objectName}\" cree dans la scene, desactive. " +
            "Remplace son enfant Cube par ton propre modele 3D.");

        return content;
    }

    static Material FindOrCreatePlaceholderMaterial()
    {
        var existing = AssetDatabase.LoadAssetAtPath<Material>(k_MaterialPath);
        if (existing != null)
            return existing;

        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            Debug.LogError("[Setup] Shader URP/Lit introuvable. Le projet utilise-t-il bien URP ?");
            return null;
        }

        EnsureFolder(k_MaterialFolder);

        var material = new Material(shader);
        material.SetColor("_BaseColor", new Color(0f, 0.85f, 1f));
        AssetDatabase.CreateAsset(material, k_MaterialPath);

        return material;
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
