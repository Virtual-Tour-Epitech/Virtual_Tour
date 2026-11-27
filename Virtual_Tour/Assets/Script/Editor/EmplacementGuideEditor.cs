using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[CustomEditor(typeof(EmplacementGuide))]
public class EmplacementGuideEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var guide = (EmplacementGuide)target;

        EditorGUILayout.Space();

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Generate"))
                Apply(guide, "Generer le chemin", guide.Generate);

            if (GUILayout.Button("Clear"))
                Apply(guide, "Effacer le chemin", guide.Clear);
        }
    }

    static void Apply(EmplacementGuide guide, string undoName, System.Action action)
    {
        var lines = new Object[] { guide.Line, guide.ArrowHead };
        var recorded = System.Array.FindAll(lines, line => line != null);

        if (recorded.Length > 0)
            Undo.RecordObjects(recorded, undoName);

        action();

        foreach (var line in recorded)
            EditorUtility.SetDirty(line);

        if (!Application.isPlaying)
            EditorSceneManager.MarkSceneDirty(guide.gameObject.scene);
    }
}
