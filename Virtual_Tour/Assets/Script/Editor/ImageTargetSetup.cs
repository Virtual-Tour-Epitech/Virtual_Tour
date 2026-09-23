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
    const string k_ContainerName = "ARContentsContainer";
    const string k_ReferenceName = "MarkerReference";
    const float k_ReferenceThickness = 0.001f;
    const float k_ContentMarginMeters = 0.05f;

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

    [MenuItem("Virtual Tour/Display ARContent/True")]
    static void ShowContents()
    {
        SetContentsActive(true);
    }

    [MenuItem("Virtual Tour/Display ARContent/False")]
    static void HideContents()
    {
        SetContentsActive(false);
    }

    static void SetContentsActive(bool active)
    {
        var scene = SceneManager.GetActiveScene();
        var container = FindInScene(scene, k_ContainerName);

        if (container == null)
        {
            Debug.LogError(
                $"[Setup] \"{k_ContainerName}\" introuvable dans la scene ouverte. " +
                "Lance d'abord \"Configurer l'Image Tracking\".");
            return;
        }

        var count = 0;

        foreach (Transform child in container.transform)
        {
            Undo.RecordObject(child.gameObject, "Basculer l'affichage des contenus AR");
            child.gameObject.SetActive(active);

            var reference = child.Find(k_ReferenceName);
            if (reference != null)
            {
                Undo.RecordObject(reference.gameObject, "Basculer l'affichage des contenus AR");
                reference.gameObject.SetActive(active);
            }

            count++;
        }

        EditorSceneManager.MarkSceneDirty(scene);
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
        }

        changed += PruneLibrary(catalog, library);

        if (changed > 0)
        {
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            Selection.activeObject = library;
        }

        return library;
    }

    static int PruneLibrary(MarkerCatalog catalog, XRReferenceImageLibrary library)
    {
        var known = new HashSet<System.Guid>();

        foreach (var marker in catalog.Markers)
        {
            if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(marker.image, out var guidString, out long _))
                known.Add(new System.Guid(guidString));
        }

        var removed = 0;

        for (var i = library.count - 1; i >= 0; i--)
        {
            if (known.Contains(library[i].textureGuid))
                continue;
            library.RemoveAt(i);
            removed++;
        }

        return removed;
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

        PruneTargets(catalog, targets);

        var wired = new Dictionary<string, int>();
        for (var i = 0; i < targets.arraySize; i++)
        {
            var name = targets.GetArrayElementAtIndex(i).FindPropertyRelative("referenceImageName").stringValue;
            wired[name] = i;
        }

        var laidOut = new List<(GameObject content, float widthMeters)>();

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
                slot.objectReferenceValue = FindOrCreateSceneContent(go.scene, marker);

            if (slot.objectReferenceValue is GameObject content)
            {
                EnsureMarkerReference(content, marker);
                laidOut.Add((content, marker.WidthInMeters));
            }
        }

        LayoutContents(laidOut);

        so.ApplyModifiedProperties();

        EditorUtility.SetDirty(manager);
        EditorUtility.SetDirty(spawner);
        EditorSceneManager.MarkSceneDirty(go.scene);
        EditorSceneManager.SaveScene(go.scene);
    }

    static void PruneTargets(MarkerCatalog catalog, SerializedProperty targets)
    {
        var known = new HashSet<string>();

        foreach (var marker in catalog.Markers)
            known.Add(marker.imageName);

        for (var i = targets.arraySize - 1; i >= 0; i--)
        {
            var name = targets.GetArrayElementAtIndex(i).FindPropertyRelative("referenceImageName").stringValue;

            if (known.Contains(name))
                continue;
            targets.DeleteArrayElementAtIndex(i);
        }
    }

    static void LayoutContents(List<(GameObject content, float widthMeters)> items)
    {
        var cursorX = 0f;

        for (var i = 0; i < items.Count; i++)
        {
            var (content, widthMeters) = items[i];
            var halfWidth = widthMeters * 0.5f;

            if (i > 0)
                cursorX += halfWidth;

            Undo.RecordObject(content.transform, "Ranger les contenus AR");
            content.transform.localPosition = new Vector3(cursorX, 0f, 0f);

            cursorX += halfWidth + k_ContentMarginMeters;
        }
    }

    static GameObject FindOrCreateContainer(Scene scene)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            if (root.name == k_ContainerName)
                return root;
        }

        var container = new GameObject(k_ContainerName);
        Undo.RegisterCreatedObjectUndo(container, "Creer le conteneur AR");

        return container;
    }

    static GameObject FindOrCreateSceneContent(Scene scene, MarkerCatalog.Marker marker)
    {
        var objectName = k_ContentPrefix + marker.imageName;
        var container = FindOrCreateContainer(scene);

        var inContainer = container.transform.Find(objectName);
        if (inContainer != null)
            return inContainer.gameObject;

        var existing = FindInScene(scene, objectName);
        if (existing != null)
        {
            Undo.SetTransformParent(existing.transform, container.transform, "Ranger le contenu AR");
            return existing;
        }

        var material = FindOrCreatePlaceholderMaterial();
        if (material == null)
            return null;

        var content = new GameObject(objectName);
        content.transform.SetParent(container.transform, false);

        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = "Cube";
        cube.transform.SetParent(content.transform, false);
        cube.transform.localScale = Vector3.one * 0.1f;
        cube.transform.localPosition = new Vector3(0f, 0.05f, 0f);
        cube.GetComponent<Renderer>().sharedMaterial = material;

        Object.DestroyImmediate(cube.GetComponent<BoxCollider>());

        content.SetActive(false);

        Undo.RegisterCreatedObjectUndo(content, "Creer le contenu AR");

        return content;
    }

    static GameObject FindInScene(Scene scene, string objectName)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
            {
                if (transform.name == objectName)
                    return transform.gameObject;
            }
        }

        return null;
    }

    static void EnsureMarkerReference(GameObject content, MarkerCatalog.Marker marker)
    {
        var existing = content.transform.Find(k_ReferenceName);
        GameObject reference;

        if (existing != null)
        {
            reference = existing.gameObject;
        }
        else
        {
            reference = GameObject.CreatePrimitive(PrimitiveType.Cube);
            reference.name = k_ReferenceName;
            reference.transform.SetParent(content.transform, false);

            Object.DestroyImmediate(reference.GetComponent<BoxCollider>());
            Undo.RegisterCreatedObjectUndo(reference, "Creer le repere de marqueur");
        }

        reference.transform.localPosition = Vector3.zero;

        reference.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

        reference.transform.localScale =
            new Vector3(marker.WidthInMeters, k_ReferenceThickness, marker.HeightInMeters);

        var material = marker.referenceMaterial;

        if (material == null)
        {
            material = FindOrCreatePlaceholderMaterial();
        }

        reference.GetComponent<Renderer>().sharedMaterial = material;
        reference.SetActive(false);
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
