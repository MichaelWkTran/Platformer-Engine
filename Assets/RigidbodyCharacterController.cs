using UnityEngine;
using System.Collections.Generic;
using System;
#if UNITY_EDITOR
using UnityEditor;
#endif

[RequireComponent(typeof(CapsuleCollider), typeof(Rigidbody))]
public class RigidbodyCharacterController : MonoBehaviour
{
    [Header("Movement")]
    [Tooltip("The speed of the character when moving")] public float m_speed = 10.0f;
    [Tooltip("How fast would the character rotate to the desired direction")] public float m_turnRate = 10.0f;
    [Tooltip("The acceleration of the character when moving")] public float m_acceleration = 10.0f;
    [Tooltip("The acceleration of the character when moving against their current velocity")] public float m_brakingAcceleration = 10.0f;
    [Tooltip("The deceleration of the character when returning to idle")] public float m_idleDeceleration = 10.0f;
    protected Vector2 m_desiredMoveDir; //The desired direction the character would be moving towards
    public bool IsBraking //Whether the player is moving with or against their current velocity
    {
        get
        {
            Vector2 currentVelocity = new Vector2(m_rigidbody.velocity.x, m_rigidbody.velocity.z);
            return Vector2.SignedAngle(m_desiredMoveDir, currentVelocity) > 90.0f && m_desiredMoveDir.magnitude > 0.0f;
        }
    }

    [Header("Slope")]
    [Tooltip("The angle of a slope from which the character could walk on")] public float m_slopeLimit = 45.0f;

    [Header("Jump & Grounded")]
    [Tooltip("How high should the character jump")] public float m_jumpHeight = 2.0f;
    [Tooltip("How fast the player would fall when not on the ground")] public float m_gravity = 9.18f;
    public bool m_isGrounded { get; private set; } //Whether the character is on the ground
    [Tooltip("What layer should the character be standing on")] public LayerMask m_groundLayers = Physics.AllLayers;

    [Space] //Miscellaneous variables
    public Advanced m_advanced;
    [Serializable] public struct Advanced
    {
        [Tooltip("The gravity applied to the character when they are jumping. If this is set to 0, then the character will use the default character gravity parameter")]
        public float m_jumpGravity;

        [Tooltip("The amount of force on the character when grounded. Use to make the character 'stick' to the ground")]
        public float m_groundForce;
    }

#if UNITY_EDITOR
    public Debug m_debug;
    [Serializable] public struct Debug
    {
        public bool m_showJumpArc;
        public bool m_showSlopeLimit;
    }

    [SerializeField] Info m_info;
    [Serializable] struct Info
    {
        [Tooltip("Whether the character is standing on a surface included in Ground Layers or not. ")] public bool m_grounded;
        [Tooltip("Whether the player is moving with or against their current velocity")] public bool m_isBreaking;
    }
#endif

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
            currentAcceleration = !IsBraking ? m_acceleration : m_brakingAcceleration;
        }
        else
        {
            //If the character is returning to idle, then chose the deceleration
            //based on whether they are grounded
            currentAcceleration = m_idleDeceleration;
        }

        currentAcceleration *= Time.fixedDeltaTime;

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
                    m_rigidbody.velocity = Vector3.MoveTowards(m_rigidbody.velocity, slopeMoveDirection * m_speed, currentAcceleration);
                    break;
                }

                goto case false;
            //If the character is in the air
            case false:
                Vector2 currentVelocity = Vec3XZToVec2(m_rigidbody.velocity);
                currentVelocity = Vector2.MoveTowards(currentVelocity, m_desiredMoveDir * m_speed, currentAcceleration);
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
        m_info.m_isBreaking = IsBraking;
    }
#endif

    #endregion

    #region Methods
    /// <summary>
    /// Move the character along the global axis.
    /// <para> The Y component of <c>_moveInput</c> will make the character move along the world Z axis while the X component will make the character move along the world X axis. </para>
    /// </summary>
    public void Move(Vector2 _moveInput)
    {
        m_desiredMoveDir = _moveInput.normalized;
    }

    /// <summary>
    /// Move the character relative to the view direction of the camera.
    /// <para> The Y component of <c>_moveInput</c> will make the character move along the view direction while the X component will make the character move across the view direction. </para>
    /// </summary>
    public void MoveCameraRelative(Vector2 _moveInput, Camera _camera = null)
    {
        if (_camera == null) _camera = Camera.main;
        m_desiredMoveDir = Quaternion.AngleAxis(-_camera.transform.rotation.eulerAngles.y, Vector3.forward) * _moveInput.normalized;
    }

    /// <summary>
    /// Make the character jump.
    /// </summary>
    public void Jump() 
    {
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

        //Force Rigidbody Character Settings
        {
            void IgnoreNegitiveFloats(ref float _value) { if (_value < 0.0f) _value = 0.0f; }
            IgnoreNegitiveFloats(ref character.m_speed);
            IgnoreNegitiveFloats(ref character.m_turnRate);
            IgnoreNegitiveFloats(ref character.m_acceleration);
            IgnoreNegitiveFloats(ref character.m_brakingAcceleration);
            IgnoreNegitiveFloats(ref character.m_idleDeceleration);
            IgnoreNegitiveFloats(ref character.m_slopeLimit);
            IgnoreNegitiveFloats(ref character.m_jumpHeight);
            IgnoreNegitiveFloats(ref character.m_gravity);
            IgnoreNegitiveFloats(ref character.m_advanced.m_jumpGravity);
            IgnoreNegitiveFloats(ref character.m_advanced.m_groundForce);

        }

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

        //Draw Jump Height Slider and Jump Arch
        if (character.m_debug.m_showJumpArc)
        {
            Handles.color = Color.magenta;
            Collider collider = character.GetComponent<Collider>();
            Vector3 bottom = collider.bounds.center + (Vector3.down * collider.bounds.extents.y); //Get bottom of character
            Vector3 characterLookDir = character.transform.forward; {
                characterLookDir.y = 0.0f; characterLookDir = characterLookDir.normalized;
                if (characterLookDir == Vector3.zero) characterLookDir = Vector3.forward;
            } //Get the direction from which the jump arch will be drawn

            float jumpGravity = character.m_advanced.m_jumpGravity > 0.0f ? character.m_advanced.m_jumpGravity : character.m_gravity; //What gravity is used when the player is jumping
            float jumpSpeed = jumpSpeed = Mathf.Sqrt(2.0f * jumpGravity * character.m_jumpHeight); //The velocity of the jump
            float fallTime = Mathf.Sqrt(2.0f * character.m_jumpHeight / character.m_gravity); //How long would the character be falling
            float jumpTime = jumpSpeed / jumpGravity; //How long would the character be jumping

            //Draw Jump Height Slider
            {
                Vector3 handlePos = bottom;
                handlePos += Vector3.up * character.m_jumpHeight;
                if (character.m_gravity > 0.0f) handlePos += characterLookDir * jumpTime * character.m_speed;

                character.m_jumpHeight = Handles.ScaleValueHandle
                (
                    character.m_jumpHeight,
                    handlePos,
                    Quaternion.LookRotation(Vector3.up),
                    4.0f * HandleUtility.GetHandleSize(handlePos),
                    Handles.ArrowHandleCap,
                    0.0f
                );
            }

            //Draw Arch
            if (character.m_gravity > 0.0f)
            {
                List<Vector3> archPoints = new List<Vector3>();
                float archTimeSegment = 0.01f;

                //Add start arch point
                archPoints.Add(bottom);

                //Set arch points
                for (float archTime = archTimeSegment; archTime < jumpTime + fallTime; archTime += archTimeSegment)
                {
                    //Move point forward in the axis
                    Vector3 point = archPoints[archPoints.Count - 1];
                    point += characterLookDir * archTimeSegment * character.m_speed;
                    point.y = bottom.y;

                    //Set the point height from the first half of the arch
                    if (archTime < jumpTime)
                    {
                        point.y += (jumpSpeed * archTime) - (0.5f * jumpGravity * archTime * archTime);
                    }
                    //Set the point height from the second half of the arch
                    else
                    {
                        float archFallTime = archTime - jumpTime;
                        point.y += character.m_jumpHeight;
                        point.y -= 0.5f * character.m_gravity * archFallTime * archFallTime;
                    }

                    //Add arch point
                    archPoints.Add(point);
                }

                //Add end arch point
                archPoints.Add(bottom + (characterLookDir * (jumpTime + fallTime) * character.m_speed));

                //Draw the arch
                Handles.DrawPolyLine(archPoints.ToArray());
            }
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