using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

static class EmplacementSelectorSetup
{
    const string k_RootName = "EmplacementSelector";
    const string k_TemplateName = "ToggleMarkerRefButton";
    const string k_UndoName = "Creer le selecteur d'emplacements";
    const float k_Spacing = 20f;
    const float k_ButtonHeight = 90f;

    [MenuItem("Virtual Tour/Creer le selecteur d'emplacements")]
    static void Create()
    {
        var panel = Object.FindAnyObjectByType<InteractionPanel>(FindObjectsInactive.Include);
        if (panel == null)
        {
            Debug.LogError("[Setup] CanvasContainer (InteractionPanel) introuvable dans la scene ouverte.");
            return;
        }

        var parent = panel.transform;
        var template = FindTemplate(parent);
        if (template == null)
        {
            Debug.LogError($"[Setup] Aucun bouton modele sous \"{parent.name}\".");
            return;
        }

        var templateLabel = template.GetComponentInChildren<TMP_Text>(true);
        if (templateLabel == null)
        {
            Debug.LogError($"[Setup] Le bouton modele \"{template.name}\" n'a pas de texte TMP.");
            return;
        }

        var existing = parent.Find(k_RootName);
        if (existing != null)
            Undo.DestroyObjectImmediate(existing.gameObject);

        var root = CreateRect(k_RootName, parent);
        root.SetAsLastSibling();

        var row = root.gameObject.AddComponent<HorizontalLayoutGroup>();
        row.spacing = k_Spacing;
        row.childControlWidth = true;
        row.childControlHeight = true;
        row.childForceExpandWidth = true;
        row.childForceExpandHeight = false;

        root.gameObject.AddComponent<LayoutElement>();

        var selector = root.gameObject.AddComponent<EmplacementSelector>();
        var so = new SerializedObject(selector);

        BuildColumn(root, "StartColumn", "Depart", "Start_", TextAnchor.UpperLeft, template, templateLabel, so.FindProperty("m_StartButtons"));
        BuildColumn(root, "EndColumn", "Arrivee", "End_", TextAnchor.UpperRight, template, templateLabel, so.FindProperty("m_EndButtons"));

        so.FindProperty("m_Guide").objectReferenceValue =
            Object.FindAnyObjectByType<EmplacementGuide>(FindObjectsInactive.Include);

        so.ApplyModifiedPropertiesWithoutUndo();

        Selection.activeGameObject = root.gameObject;
        EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
    }

    static Button FindTemplate(Transform parent)
    {
        var buttons = parent.GetComponentsInChildren<Button>(true);

        foreach (var button in buttons)
        {
            if (button.name == k_TemplateName)
                return button;
        }

        return buttons.Length > 0 ? buttons[0] : null;
    }

    static RectTransform CreateRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, k_UndoName);

        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);

        return rect;
    }

    static void BuildColumn(
        Transform root,
        string columnName,
        string title,
        string buttonPrefix,
        TextAnchor alignment,
        Button template,
        TMP_Text templateLabel,
        SerializedProperty buttons)
    {
        var column = CreateRect(columnName, root);

        var layout = column.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = k_Spacing;
        layout.childAlignment = alignment;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        var buttonSize = ((RectTransform)template.transform).sizeDelta;

        var titleLabel = Object.Instantiate(templateLabel.gameObject, column, false);
        titleLabel.name = "Title";
        titleLabel.GetComponent<TMP_Text>().text = title;
        ((RectTransform)titleLabel.transform).sizeDelta = buttonSize;
        Undo.RegisterCreatedObjectUndo(titleLabel, k_UndoName);

        var values = (EmplacementGuide.Emplacement[])System.Enum.GetValues(typeof(EmplacementGuide.Emplacement));
        buttons.arraySize = values.Length;

        for (var i = 0; i < values.Length; i++)
        {
            var go = Object.Instantiate(template.gameObject, column, false);
            go.name = buttonPrefix + values[i];
            go.SetActive(true);
            ((RectTransform)go.transform).sizeDelta = new Vector2(buttonSize.x, k_ButtonHeight);
            Undo.RegisterCreatedObjectUndo(go, k_UndoName);

            var button = go.GetComponent<Button>();
            button.onClick = new Button.ButtonClickedEvent();

            var label = go.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
                label.text = values[i].ToString();

            var entry = buttons.GetArrayElementAtIndex(i);
            entry.FindPropertyRelative("emplacement").enumValueIndex = i;
            entry.FindPropertyRelative("button").objectReferenceValue = button;
        }
    }
}
