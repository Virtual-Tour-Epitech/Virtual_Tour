using UnityEngine;

public class RuntimeMeshHider : MonoBehaviour
{
    [SerializeField]
    bool m_HideInEditor;

    void Awake()
    {
        if (Application.isEditor && !m_HideInEditor)
            return;

        foreach (var meshRenderer in GetComponentsInChildren<MeshRenderer>(true))
            meshRenderer.enabled = false;
    }
}
