using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SpatialTracking;
namespace Autohand
{
    [HelpURL("https://app.gitbook.com/s/5zKO0EvOjzUDeT2aiFk3/auto-hand/teleportation")]
    public class Teleporter : MonoBehaviour
    {
        [Header("Teleport")]
        [Tooltip("The object to teleport")]
        public GameObject teleportObject;
        [Tooltip("Can be left empty - Used for if there is a container that should be teleported in addition to the main teleport object")]
        public Transform[] additionalTeleports;

        [Header("Aim Settings")]
        [Tooltip("The Object to Shoot the Beam From")]
        public Transform aimer;
        [Tooltip("Layers You Can Teleport On")]
        public LayerMask layer;
        [Tooltip("The Maximum Slope You Can Teleport On")]
        public float maxSurfaceAngle = 45;
        [Min(0)]
        public float distanceMultiplyer = 1;
        [Min(0)]
        public float curveStrength = 1;
        [Tooltip("Use Worldspace Must be True")]
        public LineRenderer line;
        [Tooltip("Maximum Length of The Teleport Line")]
        public int lineSegments = 50;

        [Header("Line Settings")]
        public Gradient canTeleportColor = new Gradient() { colorKeys = new GradientColorKey[] { new GradientColorKey() { color = Color.green, time = 0 } } };
        public Gradient cantTeleportColor = new Gradient() { colorKeys = new GradientColorKey[] { new GradientColorKey() { color = Color.red, time = 0 } } };

        [Tooltip("This gameobject will match the position of the teleport point when aiming")]
        public GameObject indicator;

        [Header("Unity Events")]
        public UnityEvent OnStartTeleport;
        public UnityEvent OnStopTeleport;
        public UnityEvent OnTeleport;

        Vector3[] lineArr;
        [SerializeField] bool aiming;
        bool hitting;
        RaycastHit aimHit;
        HandTeleportGuard[] teleportGuards;
        AutoHandPlayer playerBody;

        private Vector3 _targetTeleportDirection;

        [SerializeField] private BlockConfiguration _activeBlock;

        public GameObject indicatorArrow;

        public AudioSource teleportSound;
        public bool stopRaycast;

        [SerializeField] private BlockConfiguration _previousBlock;
        [SerializeField] private BlockConfiguration currentHitBlock;

        public TrackedPoseDriver trackedPoseDriver;




        private void Start()
        {
            playerBody = FindObjectOfType<AutoHandPlayer>();
            if (playerBody != null && playerBody.transform.gameObject == teleportObject)
                teleportObject = null;

            lineArr = new Vector3[lineSegments];
            teleportGuards = FindObjectsOfType<HandTeleportGuard>();
            indicatorArrow = indicator.transform.Find("ArrowPivot").gameObject;

        }

        void Update()
        {
            if (aiming)
                CalculateTeleport();
            else
                line.positionCount = 0;

            DrawIndicator();
        }

        void CalculateTeleport()
        {
            line.colorGradient = cantTeleportColor;
            var lineList = new List<Vector3>();
            int i;
            hitting = false;

            for (i = 0; i < lineSegments; i++)
            {
                var time = i / 60f;
                lineArr[i] = aimer.transform.position;
                lineArr[i] += transform.forward * time * distanceMultiplyer * 15;
                lineArr[i].y += curveStrength * (time - Mathf.Pow(9.8f * 0.5f * time, 2));
                lineList.Add(lineArr[i]);

                if (i != 0)
                {
                    if (Physics.Raycast(lineArr[i - 1], lineArr[i] - lineArr[i - 1], out aimHit, Vector3.Distance(lineArr[i], lineArr[i - 1]), ~Hand.GetHandsLayerMask(), QueryTriggerInteraction.Ignore))
                    {
                        if (aimHit.collider != null)
                        {
                            if (Vector3.Angle(aimHit.normal, Vector3.up) <= maxSurfaceAngle &&
                                layer == (layer | (1 << aimHit.collider.gameObject.layer)))
                            {
                                _activeBlock = aimHit.collider.gameObject.GetComponent<BlockConfiguration>();
                                if (_activeBlock != null)
                                {
                                    if (_activeBlock.snapEnabled)
                                    {
                                        // Snap to the snap point immediately and adjust trajectory
                                        lineList.Add(_activeBlock.SnapPoint);
                                        lineArr[i] = _activeBlock.SnapPoint; // Direct the trajectory to the snap point
                                        _targetTeleportDirection = _activeBlock.pointOfInterest.transform.position - _activeBlock.SnapPoint;
                                        line.colorGradient = canTeleportColor;
                                        Debug.Log("Snap enabled is true, snapping to point");
                                        line.SetPositions(lineArr);
                                        line.positionCount = i + 1; // Adjust the line position count to current segment
                                        hitting = true;
                                        _activeBlock.SetTileHighlight(true);
                                        _previousBlock = _activeBlock;
                                        
                                        break; // Break out of the loop as we've hit the snap point
                                    }
                                    else
                                    {
                                        lineList.Add(aimHit.point);
                                        _targetTeleportDirection = _activeBlock.pointOfInterest.transform.position - aimHit.point;
                                        Debug.Log("Snap enabled is false, not snapping");
                                    }

                                    _activeBlock.SetTileHighlight(true);
                                    _previousBlock = _activeBlock;
                                    hitting = true;
                                }
                            }
                        }
                    }
                    if (_previousBlock != null)
                    {
                        _previousBlock.SetTileHighlight(false);
                        _previousBlock = null;
                    }
                }
            }
            if (!hitting)
            {
                line.positionCount = i; // Set line position count to total segments if not hitting a snap point
                line.SetPositions(lineArr);
            }
        }

    


    void DrawIndicator()
        {
            if (indicator != null)
            {
                if (hitting)
                {
                    indicator.SetActive(true);
                    indicator.transform.position = _activeBlock != null && _activeBlock.snapEnabled ? _activeBlock.SnapPoint : aimHit.point;
                    indicator.transform.up = aimHit.normal;
                    if (indicatorArrow != null && _targetTeleportDirection != Vector3.zero)
                    {
                        Quaternion quat = Quaternion.LookRotation(_targetTeleportDirection, Vector3.up);
                        indicatorArrow.transform.rotation = quat;
                    }
                }
                else
                    indicator.SetActive(false);
            }
        }

        public void StartTeleport()
        {
            aiming = true;
            OnStartTeleport?.Invoke();

        }

        public void CancelTeleport()
        {
            line.positionCount = 0;
            hitting = false;
            aiming = false;
            OnStopTeleport?.Invoke();
        }

        public void Teleport()
        {
            

            Queue<Vector3> fromPos = new Queue<Vector3>();
            foreach (var guard in teleportGuards)
            {
                if (guard.gameObject.activeInHierarchy)
                    fromPos.Enqueue(guard.transform.position);
            }

            if (hitting)
            {
                if (teleportObject != null)
                {
                    var diff = aimHit.point - teleportObject.transform.position;
                    teleportObject.transform.position = aimHit.point;


                    foreach (var teleport in additionalTeleports)
                    {
                        teleport.position += diff;
                    }

                }

                Quaternion quat = Quaternion.LookRotation(_targetTeleportDirection, Vector3.up);
                _activeBlock.DisableFootPrint();
                playerBody?.SetPosition(_activeBlock != null && _activeBlock.snapEnabled ? _activeBlock.SnapPoint : aimHit.point, quat);
                //playerBody?.Recenter();
                //trackedPoseDriver.RecalibrateToLocalOrigin();
                OnTeleport?.Invoke();
                
                if (teleportSound != null && !teleportSound.isPlaying)
                {
                    teleportSound.Play();
                }
                _activeBlock.SetTileHighlight(true);
                currentHitBlock?.isblockGotHit?.Invoke(false);
                foreach (var guard in teleportGuards)
                {
                    if (guard.gameObject.activeInHierarchy)
                    {
                        guard.TeleportProtection(fromPos.Dequeue(), guard.transform.position);
                    }
                }
            }
            CancelTeleport();
        }
    }
}
