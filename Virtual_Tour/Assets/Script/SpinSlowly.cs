using UnityEngine;

public class SpinSlowly : MonoBehaviour
{
    [SerializeField]
    float m_DegreesPerSecond = 45f;

    [SerializeField]
    Vector3 m_Axis = Vector3.up;

    void Update()
    {
        transform.Rotate(m_Axis, m_DegreesPerSecond * Time.deltaTime, Space.Self);
    }
}
