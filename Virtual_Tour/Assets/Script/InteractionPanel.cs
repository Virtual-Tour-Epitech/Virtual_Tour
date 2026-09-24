using TMPro;
using UnityEngine;

public class InteractionPanel : MonoBehaviour
{
    [SerializeField]
    GameObject m_InteractButton;

    [SerializeField]
    GameObject m_InteractionDisplay;

    [SerializeField]
    TextMeshProUGUI m_Label;

    AimHighlight m_Target;

    void Awake()
    {
        if (m_Label == null && m_InteractionDisplay != null)
            m_Label = m_InteractionDisplay.GetComponentInChildren<TextMeshProUGUI>(true);

        if (m_InteractButton == null || m_InteractionDisplay == null || m_Label == null)
            Debug.LogError("[InteractionPanel] References d'UI manquantes : l'interaction ne s'affichera pas.", this);

        SetTarget(null);
    }

    public void SetTarget(AimHighlight target)
    {
        m_Target = target;

        if (m_InteractButton != null)
            m_InteractButton.SetActive(target != null);

        if (target == null && m_InteractionDisplay != null)
            m_InteractionDisplay.SetActive(false);
    }

    public void Interact()
    {
        if (m_Target == null)
            return;

        if (m_Label != null)
            m_Label.text = m_Target.name;

        if (m_InteractionDisplay != null)
            m_InteractionDisplay.SetActive(true);
    }
}
