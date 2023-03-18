using UnityEngine;
using System.Collections.Generic;
using System;
#if UNITY_EDITOR
using UnityEditor;
#endif

[RequireComponent(typeof(CapsuleCollider), typeof(Rigidbody))]
public class RigidbodyCharacterController : MonoBehaviour
{
    //Movement Variables
    [Tooltip("The speed of the character when moving")] public float m_speed = 10.0f;
    [Tooltip("How fast would the character rotate to the desired direction")] public float m_turnRate = 10.0f;
    [Tooltip("The acceleration of the character when moving")] public float m_acceleration = 10.0f;
    [Tooltip("The acceleration of the character when moving against their current velocity")] public float m_brakingAcceleration = 10.0f;
    [Tooltip("The deceleration of the character when returning to idle")] public float m_idleDeceleration = 10.0f;

    //Slope Variables
    [Tooltip("The angle of a slope from which the character could walk on")] public float m_slopeLimit = 45.0f;

    //Jump & Grounded Variables
    [Tooltip("How high should the character jump")] public float m_jumpHeight = 2.0f;
    [Tooltip("How fast the player would fall when not on the ground")] public float m_gravity = 9.18f;
    public bool m_isGrounded { get; private set; } //Whether the character is on the ground
    [Tooltip("What layer should the character be standing on")] public LayerMask m_groundLayers = Physics.AllLayers;

    //Miscellaneous variables
    [Serializable] public struct Advanced
    {
        [Tooltip("The gravity applied to the character when they are jumping. If this is set to 0, then the character will use the default character gravity")]
            public float m_jumpGravity;
        
        [Tooltip("The amount of force on the character when grounded. Use to make the character 'stick' to the ground")]
            public float m_groundForce;
    }
    public Advanced m_advanced;

#if UNITY_EDITOR
    [Serializable] public struct Debug
    {
        public bool m_showJumpArc;
        public bool m_showSlopeLimit;
    }
    public Debug m_debug;

    [Serializable] struct Info
    {
        public bool m_grounded;
    }
    [SerializeField] Info m_info;
#endif

    protected float m_moveInputMagnitude; //How far would the 
    protected Vector2 m_desiredMoveDir; //The desired direction the character would be moving towards
    protected float m_turn = 0.0f; //How much as the player rotated since the previous frame
    public bool IsBraking //Whether the player is moving with or against their current velocity
    {
        get { return m_turn > -90.0f && m_turn < 90.0f && m_desiredMoveDir.magnitude > 0.0f; }
    }

    //Components
    protected CapsuleCollider m_collider;
    protected Rigidbody m_rigidbody;

    #region Events
    void Awake()
    {
        m_collider = GetComponent<CapsuleCollider>();
        m_rigidbody = GetComponent<Rigidbody>();
    }

    void FixedUpdate()
    {
        Vector2 Vec3XZToVec2(Vector3 _vector) { return new Vector2(_vector.x, _vector.z); }
        Vector3 Vec2ToVec3XZ(Vector2 _vector) { return new Vector3(_vector.x, 0.0f, _vector.y); }

        //Apply Gravity
        if (!m_isGrounded)
        {
            //If the character is moving upwards and jump gravity > 0, apply jump gravity
            //Otherwise, apply default gravity
            m_rigidbody.AddForce
            (
                Vector3.down *
                (
                    m_rigidbody.velocity.y > 0.0f && m_advanced.m_jumpGravity > 0 ?
                        m_advanced.m_jumpGravity :
                        m_gravity
                ),
                ForceMode.Acceleration
           );
        }
        //Apply Ground Force
        else if (m_advanced.m_groundForce != 0)
        {
            m_rigidbody.AddForce(Vector3.down * m_advanced.m_groundForce, ForceMode.Acceleration);
        }

        //Get current acceleration/deceleration
        float currentAcceleration;
        if (m_desiredMoveDir.magnitude > 0.0f)
        {
            //If the character is moving, then choose the acceleration
            //based on whether they are moving with or against velocity
            currentAcceleration = IsBraking ? m_acceleration : m_brakingAcceleration;
        }
        else
        {
            //If the character is returning to idle, then chose the deceleration
            //based on whether they are grounded
            currentAcceleration = m_idleDeceleration;
        }

        currentAcceleration *= Time.fixedDeltaTime;

        //Get set speed modified by m_moveInputMagnitude
        float speed = m_speed * m_moveInputMagnitude;

        //Move the character based on whether they are grounded
        switch (m_isGrounded)
        {
            //If the character is grounded
            case true:
                //Get all colliders that the character might be standing on
                RaycastHit[] slopeHits = Physics.RaycastAll(m_collider.bounds.center + (Vector3.down * m_collider.bounds.extents.y), Vector3.down, 0.01f, m_groundLayers);
                foreach (RaycastHit hit in slopeHits)
                {
                    //Ignore the collider from the player
                    if (hit.collider == m_collider) continue;

                    //Get the direction of the slope in which the character is moving
                    Vector3 slopeMoveDirection = Vector3.ProjectOnPlane(Vec2ToVec3XZ(m_desiredMoveDir), hit.normal).normalized;

                    //Check whether the slope is too steep for the character to move on,
                    //if it is then move the character is if they are in the air
                    if (90.0f - Vector3.Angle(hit.normal, Vector3.up) > m_slopeLimit) goto case false;

                    //Apply the movement
                    m_rigidbody.velocity = Vector3.MoveTowards(m_rigidbody.velocity, slopeMoveDirection * speed, currentAcceleration);
                    break;
                }

                goto case false;
            //If the character is in the air
            case false:
                Vector2 currentVelocity = Vec3XZToVec2(m_rigidbody.velocity);
                currentVelocity = Vector2.MoveTowards(currentVelocity, m_desiredMoveDir * speed, currentAcceleration);
                m_rigidbody.velocity = new Vector3(currentVelocity.x, m_rigidbody.velocity.y, currentVelocity.y);
                break;
        }

        //Rotate the character to face the direction they are moving in
        if (m_turnRate != 0.0f && Vec3XZToVec2(m_rigidbody.velocity) != Vector2.zero && m_desiredMoveDir != Vector2.zero)
        {
            transform.rotation = Quaternion.Slerp
            (
                transform.rotation,
                Quaternion.LookRotation(new Vector3(m_rigidbody.velocity.x, 0.0f, m_rigidbody.velocity.z), Vector3.up),
                m_turnRate * Time.fixedDeltaTime
            );
        }

        //Set grounded to false
        m_isGrounded = false;
    }

    void OnCollisionStay(Collision _col)
    {
        //Check Grounded
        {
            float bottom = m_collider.bounds.center.y - m_collider.bounds.extents.y + m_collider.radius;
            foreach (ContactPoint contact in _col.contacts)
            {
                if (bottom - contact.point.y > 0.0f && m_groundLayers == (m_groundLayers | (1 << contact.otherCollider.gameObject.layer)))
                {
                    m_isGrounded = true;
                    break;
                }
            }
        }
    }

#if UNITY_EDITOR
    void LateUpdate()
    {
        m_info.m_grounded = m_isGrounded;
    }
#endif

    #endregion

    #region Methods
    public void Move(Vector2 _moveInputVector)
    {
        m_moveInputMagnitude = _moveInputVector.magnitude;
        m_desiredMoveDir = Quaternion.AngleAxis(-Camera.main.transform.rotation.eulerAngles.y, Vector3.forward) * _moveInputVector.normalized;
    }

    public void Jump()
    {
        //Make the player jump
        m_rigidbody.velocity = new Vector3(m_rigidbody.velocity.x,
                Mathf.Sqrt(2.0f * (m_advanced.m_jumpGravity > 0 ? m_advanced.m_jumpGravity : m_gravity) * m_jumpHeight),
                m_rigidbody.velocity.z);
    }
    #endregion
}

#if UNITY_EDITOR
[CustomEditor(typeof(RigidbodyCharacterController))]
public class CharacterEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var character = target as RigidbodyCharacterController;
        DrawDefaultInspector();

        //Force rigidbody settings
        Rigidbody rigidbody = character.GetComponent<Rigidbody>();
        rigidbody.useGravity = false;
        rigidbody.freezeRotation = true;

        CapsuleCollider collider = character.GetComponent<CapsuleCollider>();
        collider.isTrigger = false;
        collider.direction = 1;

        //Give warnings of components that would cause problems
        if (character.GetComponent<CharacterController>())
        {
            DestroyImmediate(character.GetComponent<CharacterController>());
            EditorUtility.DisplayDialog(
                "Can't create CharacterController",
                "The component, CharacterController, will interfere with the Character Component and thus can not be created.",
                "Cancel");
        }
    }

    public void OnSceneGUI()
    {
        var character = target as RigidbodyCharacterController;

        //Jump Height Slider
        if (character.m_debug.m_showJumpArc)
        {
            Handles.color = Color.magenta;
            Collider collider = character.GetComponent<Collider>();
            Vector3 bottom = collider.bounds.center + (Vector3.down * collider.bounds.extents.y); //Get bottom of character
            Vector3 characterLookDir = character.transform.forward; {
                characterLookDir.y = 0.0f; characterLookDir = characterLookDir.normalized;
                if (characterLookDir == Vector3.zero) characterLookDir = Vector3.forward;
            } //Get the direction from which the jump arch will be drawn

            float jumpSpeed = Mathf.Sqrt(2.0f * character.m_gravity * character.m_jumpHeight); //The velocity of the jump
            float jumpTime = 2.0f * jumpSpeed / character.m_gravity; //How long will the player be in the air

            Vector3 handlePos = bottom + (0.5f * characterLookDir * jumpTime * character.m_speed) + (Vector3.up * character.m_jumpHeight);
            character.m_jumpHeight = Handles.ScaleValueHandle(character.m_jumpHeight, handlePos, Quaternion.LookRotation(Vector3.up), 4.0f * HandleUtility.GetHandleSize(handlePos), Handles.ArrowHandleCap, 0.0f);

            //Draw Arch
            List<Vector3> archPoints = new List<Vector3>();
            float archSpeed = jumpSpeed;
            float archTimeSegment = 0.05f;

            archPoints.Add(bottom);
            for (float archTime = archTimeSegment; archTime < jumpTime; archTime += archTimeSegment)
            {
                Vector3 point = archPoints[archPoints.Count - 1];
                point += characterLookDir * archTimeSegment * character.m_speed;

                point.y = (archSpeed * archTime) + (0.5f * -character.m_gravity * archTime * archTime);

                point += bottom;
                archPoints.Add(point);
            }
            archPoints.Add(bottom + (characterLookDir * jumpTime * character.m_speed));

            Handles.DrawPolyLine(archPoints.ToArray());
        }

        //Draw slope limit handle
        if (character.m_debug.m_showSlopeLimit)
        {
            Handles.color = new Color(1.0f, 1.0f, 1.0f, 0.2f);
            Collider collider = character.GetComponent<Collider>();
            Vector3 bottom = collider.bounds.center + (Vector3.down * collider.bounds.extents.y); //Get bottom of character
            Vector3 characterLookDir = character.transform.forward; {
                characterLookDir.y = 0.0f; characterLookDir = characterLookDir.normalized;
                if (characterLookDir == Vector3.zero) characterLookDir = Vector3.forward;
            } //Get the direction from which the jump arch will be drawn
            Vector3 handleNormal = character.transform.right; {
                handleNormal.y = 0.0f; handleNormal = handleNormal.normalized;
                if (handleNormal == Vector3.zero) handleNormal = Vector3.forward;
            } //Get the direction from which the jump arch will be drawn

            Handles.DrawSolidArc(bottom, handleNormal, characterLookDir, -character.m_slopeLimit, HandleUtility.GetHandleSize(bottom));
            Handles.DrawSolidArc(bottom, handleNormal, characterLookDir, character.m_slopeLimit, HandleUtility.GetHandleSize(bottom));
        }
    }
}
#endif