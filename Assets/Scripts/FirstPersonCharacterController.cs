using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/*Implimentation Steps
 - Create a capsule to be your player object.
 - Add a CharacterController component to your player.
 - Parent the camera to the player, and position it in their head.
 - Add this script to that camera.
 - Adjust settings according to your needs.
*/

public class FirstPersonCharacterController : MonoBehaviour
{
    #region Serialized Settings
    [Header("Speed Settings")]
    [SerializeField] float movementSpeed = 6.5f;
    [SerializeField] float strafeSpeed = 4f;
    [SerializeField] float sprintSpeedMod = 2;
    [SerializeField] float crouchSpeedMod = .5f;
    
    [Space]
    [Header("Crouch Settings")]
    [SerializeField, Range(.01f, 1)] float crouchHeight = .6f;
    [SerializeField, Range(.01f, 1)] float moveToCrouchSpeed = .1f;
    [SerializeField, Range(.01f, 1)] float moveFromCrouchSpeed = .2f;
    [SerializeField] float maxDistFromLedge = .6f;
    [SerializeField] GameObject crouchPlatform;
    [SerializeField] LayerMask worldMask;
    
    [Space]
    [Header("Jump Settings")]
    [SerializeField] float gravity = .98f;
    [SerializeField] float jumpForce = 14;
    public int airJumps = 0;

    [Space]
    [Header("View Settings")]
    [SerializeField] float mouseSensitivityX = 2.2f;
    [SerializeField] float mouseSensitivityY = 2.2f;
    [SerializeField] Vector2 yawBounds = new Vector2(-90, 35);

    [Space]
    [Header("Audio Settings")]
    [SerializeField] float defaultAudioVolume;
    [SerializeField] float defaultAuioPitch;
    [SerializeField] AudioClip footstepSoundClip;
    [SerializeField] Vector2 footstepsVolumeRandomRange;
    [SerializeField] Vector2 footstepsPitchRandomRange;
    [SerializeField] float footstepFrequency;
    [SerializeField] AudioClip jumpSoundClip;
    [SerializeField] AudioClip doubleJumpSoundClip;
    [SerializeField] float timeInAirToPlayLandingAudio = .2f;
    [SerializeField] AudioClip landingAudioClip;
    #endregion

    #region Local Variables
    Transform player;
    GameObject playerGO;
    CharacterController controller;
    CollisionFlags collisions;
    AudioSource audioSource;

    float pitch = 0;
    float yaw = 0;

    float airJumpCounter = 0;

    bool crouched = false;
    float defaultPlayerHeight;

    bool pWasInAir = false;
    float timeInAir = 0;
    bool footstepCooldown = false;

    public Vector3 velocity;

    Vector3 lastSolidBlockPos;
    #endregion

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        player = transform.parent;
        playerGO = player.gameObject;
        controller = playerGO.GetComponent<CharacterController>();

        defaultPlayerHeight = player.localScale.y;

        audioSource = GetComponent<AudioSource>();
    }

    void Update()
    {
        if (Cursor.lockState == CursorLockMode.Locked)
        {
            #region Rotations
            pitch += Input.GetAxis("Mouse X") * mouseSensitivityX;
            yaw -= Input.GetAxis("Mouse Y") * mouseSensitivityY;

            yaw = Mathf.Clamp(yaw, yawBounds.x, yawBounds.y);

            Vector3 targetPlayerRotation = new Vector3(0, pitch);
            player.eulerAngles = targetPlayerRotation;

            Vector3 targetHeadRotation = new Vector3(yaw, pitch);
            transform.eulerAngles = targetHeadRotation;
            #endregion

            #region Movement
            Vector3 pVelocity = velocity;
            velocity = new Vector3(0, velocity.y, 0);

            float forwardInput = Input.GetAxis("Vertical") * movementSpeed;
            float sideInput = Input.GetAxis("Horizontal") * strafeSpeed;

            //Handle Sprinting & Crouching
            if (Input.GetKey(KeyCode.LeftShift) && (pVelocity.x > 0 || pVelocity.z > 0))
            {
                forwardInput *= sprintSpeedMod;
            }
            else if (Input.GetKey(KeyCode.LeftControl) && controller.isGrounded)
            {
                forwardInput *= crouchSpeedMod;
                sideInput *= crouchSpeedMod;
            }

            handleCrouch();

            //Handle Jumping
            if (controller.isGrounded)
            {
                if (pWasInAir && timeInAir > timeInAirToPlayLandingAudio) 
                {
                    //ResetAudioSettings();
                    //udioSource.PlayOneShot(landingAudioClip);
                }

                timeInAir = 0f;

                velocity.y = 0;
                airJumpCounter = 0;
                if (Input.GetKeyDown(KeyCode.Space) && !crouched) 
                {
                    velocity.y = jumpForce * Time.deltaTime;

                    //ResetAudioSettings();
                    //audioSource.PlayOneShot(jumpSoundClip,2.2f);
                }
                    
            }
            else if (Input.GetKeyDown(KeyCode.Space) && airJumpCounter < airJumps)
            {
                velocity.y = jumpForce * Time.deltaTime;
                airJumpCounter++;

                //ResetAudioSettings();
                //audioSource.PlayOneShot(jumpSoundClip, 1.25f);
            }

            if (!controller.isGrounded) 
            {
                timeInAir += Time.deltaTime;
            }

            velocity += (player.forward * forwardInput * Time.deltaTime) + (player.right * sideInput * Time.deltaTime) + (Vector3.down * gravity * Time.deltaTime);

            if (crouched)
            {
                Vector3 footPos = new Vector3(player.transform.position.x, player.position.y - player.localScale.y, player.transform.position.z);

                if(Vector3.Distance(footPos + velocity, lastSolidBlockPos) > maxDistFromLedge)
                {
                    
                    velocity = Vector3.zero;
                }
            }

            pWasInAir = !controller.isGrounded;

            collisions = controller.Move(velocity);

            if ((controller.collisionFlags & CollisionFlags.Above) != 0)
            {
                velocity.y = 0;
            }

            if (forwardInput != 0 && controller.isGrounded) 
            {
                //audioSource.volume = Random.Range(footstepsVolumeRandomRange.x, footstepsVolumeRandomRange.y);
                //audioSource.pitch = Random.Range(footstepsPitchRandomRange.x, footstepsPitchRandomRange.y);
                if (!footstepCooldown && Input.GetKey(KeyCode.W)) 
                {
                    //audioSource.PlayOneShot(footstepSoundClip); 
                    //StartCoroutine(footstepDelay());
                }
                    
            }

            RaycastHit h;
            if(Physics.Raycast(new Vector3(player.transform.position.x, player.position.y - player.localScale.y, player.transform.position.z), Vector3.down, out h,.1f, worldMask) && controller.isGrounded)
            {
                lastSolidBlockPos = h.point;
            }

            #endregion
        }
        //Shortcut for locking & unlocking the cursor
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Cursor.lockState = (Cursor.lockState == CursorLockMode.Locked) ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = !Cursor.visible;
        }
            
    }

    void handleCrouch() 
    {
        if(!crouched && controller.isGrounded &&Input.GetKey(KeyCode.LeftControl))
        {
            Debug.Log("CROUCHING");
            player.localScale = new Vector3(player.localScale.x, crouchHeight * defaultPlayerHeight, player.localScale.z);
            crouchPlatform.SetActive(true);
            crouched = true;
        }

        if(crouched && !Input.GetKey(KeyCode.LeftControl))
        {
            player.localScale = new Vector3(player.localScale.x, defaultPlayerHeight, player.localScale.z);
            crouchPlatform.SetActive(false);
            crouched = false;
        }

        if (crouched)
        {
            crouchPlatform.transform.position = player.transform.position - new Vector3(0, 1.341f, 0);
            crouchPlatform.transform.position = new Vector3(Mathf.FloorToInt(crouchPlatform.transform.position.x), Mathf.FloorToInt(crouchPlatform.transform.position.y), Mathf.FloorToInt(crouchPlatform.transform.position.z)) + Vector3.one/2;
        }
    }


    //Audio Methods
    IEnumerator footstepDelay() 
    {
        footstepCooldown = true;
        yield return new WaitForSeconds(footstepFrequency);
        footstepCooldown = false;
    }

    void ResetAudioSettings() 
    {
        audioSource.volume = defaultAudioVolume;
        audioSource.pitch = defaultAuioPitch;
    }

    public bool GetGroundState() 
    {
        return pWasInAir;
    }
}
