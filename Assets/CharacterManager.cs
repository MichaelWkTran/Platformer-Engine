using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class CharacterManager : MonoBehaviour
{
    public static CharacterManager Singleton
    {
        get
        {
            CharacterManager singleton = FindAnyObjectByType<CharacterManager>();
            if (singleton == null) return new GameObject("Character Manager").AddComponent<CharacterManager>();
            else return singleton;
        }
    }

    void Update()
    {
        foreach (var i in FindObjectsOfType<RigidbodyCharacterController>())
        {
            i.Move(new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")));
            if (Input.GetButtonDown("Jump") && i.m_isGrounded) i.Jump();
        }
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(CharacterManager))]
public class CharacterManagerEditor : Editor
{
    static CharacterManager m_singleton = null;

    public override void OnInspectorGUI()
    {
        var character = target as CharacterManager;
        if (m_singleton == null) m_singleton = character;
        else if (m_singleton != character)
        {
            DestroyImmediate(character);
            EditorUtility.DisplayDialog(
                "Can't create multiple of the same component",
                "The component, CharacterManager, is a singleton. Thus multiple CharacterManagers can not be created.",
                "Cancel");
        }
        DrawDefaultInspector();
    }
}
#endif