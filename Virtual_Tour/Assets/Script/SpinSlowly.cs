using UnityEngine;

/// <summary>
/// Fait tourner lentement l'objet sur lui-meme.
/// Sert uniquement a rendre la detection d'image visuellement evidente pendant les tests.
/// </summary>
public class SpinSlowly : MonoBehaviour
{
    [Tooltip("Vitesse de rotation en degres par seconde.")]
    [SerializeField]
    float m_DegreesPerSecond = 45f;

    [Tooltip("Axe de rotation, en espace local.")]
    [SerializeField]
    Vector3 m_Axis = Vector3.up;

    void Update()
    {
        transform.Rotate(m_Axis, m_DegreesPerSecond * Time.deltaTime, Space.Self);
    }
}
