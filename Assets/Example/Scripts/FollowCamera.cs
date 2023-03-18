using UnityEngine;

[ExecuteInEditMode]
public class FollowCamera : MonoBehaviour
{
    [SerializeField] Transform m_target;
    [SerializeField] Vector3 m_offset;

    void Update()
    {
        if (m_target == null) return;
        transform.position = m_target.position + m_offset;
    }
}
