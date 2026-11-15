using System.Collections.Generic;
using Projector.Networking;
using Unity.Netcode;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;

namespace Projector.Gameplay
{
    // A player's head, body and hands in the 3D scenes. The owner copies its XR rig's poses onto the head/hand
    // children every frame (their owner-authority NetworkTransforms sync them); everyone else sees the result,
    // with the body hung below the head and turned to face where the head faces. The owner hides its own avatar
    // so it doesn't render inside the headset.
    public class NetworkAvatar : NetworkBehaviour
    {
        // How far below the eyes the body hangs, and how far behind them (the eyes sit at the front of the head).
        const float NeckDrop = 0.2f;
        const float NeckBack = 0.07f;
        // Seconds the body takes to catch up when the head turns, so it doesn't spin with every glance.
        const float BodyTurnTime = 0.25f;

        [SerializeField] Transform head;
        [SerializeField] Transform body;
        [SerializeField] Transform leftHand;
        [SerializeField] Transform rightHand;
        [SerializeField] Transform nameLabel;

        Transform rigHead;
        HandSource[] rigLefts;
        HandSource[] rigRights;
        bool lastHeadAvailable;
        bool hasReportedHeadAvailability;
        bool appFocused = true;
        float bodyYaw;
        float bodyYawSpeed;

        // A tracked pose a glove can follow, and how the glove sits on it: the gloves are modeled palm down,
        // fingers forward, and a held controller's grip is turned a quarter roll from that.
        struct HandSource
        {
            public Transform Pose;
            public Quaternion Offset;
        }

        public override void OnNetworkSpawn()
        {
            var identity = GetComponent<PlayerIdentity>();
            if (!IsOwner)
                return;

            foreach (var avatarRenderer in GetComponentsInChildren<Renderer>())
                avatarRenderer.enabled = false;
            nameLabel.gameObject.SetActive(false);

            var origin = FindFirstObjectByType<XROrigin>();
            if (origin != null)
            {
                rigHead = origin.Camera.transform;
                rigLefts = FindHand(origin.transform, "Left", "L", 90f);
                rigRights = FindHand(origin.transform, "Right", "R", -90f);
            }

            if (PlayerSpawner.Current != null)
                PlayerSpawner.Current.PlaceLocalRig(identity.Slot.Value);
        }

        void LateUpdate()
        {
            if (IsOwner)
            {
                PollHeadTracking();
                if (rigHead == null)
                    return;

                head.SetPositionAndRotation(rigHead.position, rigHead.rotation);
                CopyPose(rigLefts, leftHand);
                CopyPose(rigRights, rightHand);
                return;
            }

            FollowHead();

            // Keep the name above the head and turned toward the local viewer.
            var viewer = Camera.main;
            nameLabel.position = head.position + Vector3.up * 0.28f;
            if (viewer != null)
                nameLabel.rotation = Quaternion.LookRotation(nameLabel.position - viewer.transform.position);
        }

        // The Quest suspends the app (or takes its focus) when the headset comes off or the system menu opens, and
        // resumes it when the headset goes back on. The head InputDevice's isTracked/userPresence values can stay
        // stale after a resume, which left the game paused forever, so read the OpenXR session itself instead: it
        // is FOCUSED only while the headset is worn and the app has input, and the runtime reports presence
        // changes as events, so neither goes stale.
        void OnApplicationFocus(bool hasFocus) => SetAppFocused(hasFocus);
        void OnApplicationPause(bool paused) => SetAppFocused(!paused);

        void SetAppFocused(bool focused)
        {
            appFocused = focused;
            if (IsOwner && !focused)
                ReportHeadAvailable(false);
        }

        void PollHeadTracking()
        {
            var openXRRunning = XRGeneralSettings.Instance != null && XRGeneralSettings.Instance.Manager != null
                && XRGeneralSettings.Instance.Manager.activeLoader is OpenXRLoader;
            // Without a running OpenXR session (e.g. the editor with XR off) app focus is all there is to go on.
            var present = !openXRRunning || (OpenXRUtility.IsSessionFocused && OpenXRUtility.IsUserPresent);
            ReportHeadAvailable(appFocused && present);
        }

        void ReportHeadAvailable(bool headAvailable)
        {
            if (hasReportedHeadAvailability && headAvailable == lastHeadAvailable)
                return;

            if (ProjectorGame.Current == null || !IsSpawned)
                return;

            lastHeadAvailable = headAvailable;
            hasReportedHeadAvailability = true;
            ProjectorGame.Current.SetLocalHeadsetAvailable(headAvailable);
        }

        // The body stays upright under the head and turns toward where the head faces. Looking almost straight
        // up or down says little about which way the player faces, so the body keeps its heading then.
        void FollowHead()
        {
            if (body == null)
                return;

            var facing = Vector3.ProjectOnPlane(head.forward, Vector3.up);
            if (facing.sqrMagnitude > 0.04f)
            {
                var yaw = Quaternion.LookRotation(facing).eulerAngles.y;
                bodyYaw = Mathf.SmoothDampAngle(bodyYaw, yaw, ref bodyYawSpeed, BodyTurnTime);
            }
            var rotation = Quaternion.Euler(0f, bodyYaw, 0f);
            body.SetPositionAndRotation(head.position + Vector3.down * NeckDrop + rotation * Vector3.back * NeckBack, rotation);
        }

        // Hands and controllers swap depending on what's tracked; follow whichever is active.
        static void CopyPose(HandSource[] sources, Transform target)
        {
            foreach (var source in sources)
            {
                if (source.Pose != null && source.Pose.gameObject.activeInHierarchy)
                {
                    target.SetPositionAndRotation(source.Pose.position, source.Pose.rotation * source.Offset);
                    return;
                }
            }
        }

        // A tracked hand is followed by its palm joint (the hand object itself never moves, and each of its hand
        // visuals has a palm, so any active one will do); a controller by its grip, with the glove rolled so its
        // palm faces in around the handle.
        static HandSource[] FindHand(Transform root, string side, string jointPrefix, float controllerRoll)
        {
            var sources = new List<HandSource>();
            foreach (var child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == side + " Hand")
                {
                    foreach (var joint in child.GetComponentsInChildren<Transform>(true))
                    {
                        if (joint.name == jointPrefix + "_Palm")
                            sources.Add(new HandSource { Pose = joint, Offset = Quaternion.identity });
                    }
                }
                else if (child.name == side + " Controller")
                {
                    sources.Add(new HandSource { Pose = child, Offset = Quaternion.Euler(0f, 0f, controllerRoll) });
                }
            }
            return sources.ToArray();
        }
    }
}
