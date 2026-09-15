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
    readonly struct MarkerDefinition
    {
        public readonly string texturePath;
        public readonly string imageName;
        public readonly float printedWidthMeters;

        public MarkerDefinition(string texturePath, string imageName, float printedWidthMeters)
        {
            this.texturePath = texturePath;
            this.imageName = imageName;
            this.printedWidthMeters = printedWidthMeters;
        }
    }

    static readonly MarkerDefinition[] k_Markers =
    {
        new("Assets/Tour Eiffel.jpg", "TourEiffel_Station01", 0.065f),
    };

    const string k_LibraryPath = "Assets/ReferenceImageLibrary.asset";
    const string k_MaterialFolder = "Assets/Materials";
    const string k_MaterialPath = k_MaterialFolder + "/ARContent_Placeholder_Mat.mat";
    const string k_ContentPrefix = "ARContent_";

    [MenuItem("Virtual Tour/Configurer l'Image Tracking")]
    static void Configure()
    {
        var library = RegisterMarkersInLibrary();
        if (library == null)
            return;

        WireUpScene(library);
    }

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
            EnsureReferenceImageImportSettings(marker.texturePath);

            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(marker.texturePath);
            if (texture == null)
            {
                Debug.LogError($"[Setup] Texture introuvable a \"{marker.texturePath}\"");
                continue;
            }

            var aspect = (float)texture.height / texture.width;
            var size = new Vector2(marker.printedWidthMeters, marker.printedWidthMeters * aspect);

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
                continue;
            }

            library.SetName(index, marker.imageName);
            library.SetSpecifySize(index, true);
            library.SetSize(index, size);

            changed++;

            Debug.Log(
                $"[Setup] \"{marker.imageName}\" {(isNew ? "ajoute" : "mis a jour")} : " +
                $"{texture.width}x{texture.height} px, imprime en {size.x:0.###} x {size.y:0.###} m.");

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

    static void EnsureReferenceImageImportSettings(string texturePath)
    {
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

    static void WireUpScene(XRReferenceImageLibrary library)
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
        manager.requestedMaxNumberOfMovingImages = 1;
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

        foreach (var marker in k_Markers)
        {
            if (!wired.TryGetValue(marker.imageName, out var index))
            {
                index = targets.arraySize;
                targets.arraySize = index + 1;
                targets.GetArrayElementAtIndex(index)
                    .FindPropertyRelative("referenceImageName").stringValue = marker.imageName;
            }

            var slot = targets.GetArrayElementAtIndex(index).FindPropertyRelative("sceneObject");
            if (slot.objectReferenceValue == null)
                slot.objectReferenceValue = FindOrCreateSceneContent(go.scene, marker.imageName);
        }

        so.ApplyModifiedProperties();

        EditorUtility.SetDirty(manager);
        EditorUtility.SetDirty(spawner);
        EditorSceneManager.MarkSceneDirty(go.scene);
        EditorSceneManager.SaveScene(go.scene);

        Debug.Log($"[Setup] Scene \"{go.scene.name}\" configuree et sauvegardee.");
    }

    static GameObject FindOrCreateSceneContent(Scene scene, string imageName)
    {
        var objectName = k_ContentPrefix + imageName;

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
