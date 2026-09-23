using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.UI;

public class CrosshairPointer : MonoBehaviour
{
    [SerializeField]
    Camera m_Camera;

    [SerializeField]
    ImageTargetLogger m_Logger;

    [SerializeField]
    Graphic[] m_Branches;

    [SerializeField]
    Color m_IdleColor = new(0f, 0f, 0f, 0.8f);

    [SerializeField]
    Color m_HoverColor = new(0f, 0.85f, 1f, 1f);

    [SerializeField]
    float m_MaxDistance = 10f;

    [SerializeField]
    LayerMask m_LayerMask = ~0;

    string m_AimedName;
    bool m_Initialised;

    void Awake()
    {
        if (m_Camera == null)
        {
            var origin = FindAnyObjectByType<XROrigin>();
            if (origin != null)
                m_Camera = origin.Camera;
        }

        if (m_Camera == null)
            m_Camera = Camera.main;

        if (m_Logger == null)
            m_Logger = FindAnyObjectByType<ImageTargetLogger>();

        if (m_Camera == null)
            Debug.LogError("[CrosshairPointer] Aucune camera trouvee : le pointeur restera inerte.", this);
    }

    void Update()
    {
        if (m_Camera == null)
            return;

        var center = new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f);
        var ray = m_Camera.ScreenPointToRay(center);

        var aimed = Physics.Raycast(ray, out var hit, m_MaxDistance, m_LayerMask)
            ? hit.collider.gameObject.name
            : null;

        if (m_Initialised && aimed == m_AimedName)
            return;

        m_Initialised = true;
        m_AimedName = aimed;

        var color = aimed != null ? m_HoverColor : m_IdleColor;

        foreach (var branch in m_Branches)
        {
            if (branch != null)
                branch.color = color;
        }

        if (m_Logger != null)
            m_Logger.LogAim(aimed);
    }
}
