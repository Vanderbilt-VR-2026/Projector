using System;
using Projector.Audio;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Projector.Gameplay
{
    // A rope hanging from the ceiling with a tassel to grab, up close or with a ray. Pulling the hand down by
    // `pullDistance` while holding it fires Pulled once per grab; the rope follows the hand down a little and
    // springs back when let go.
    [RequireComponent(typeof(XRSimpleInteractable))]
    public class PullRope : MonoBehaviour
    {
        [Tooltip("The part that moves: the rope and its tassel.")]
        [SerializeField] Transform rope;
        [Tooltip("Downward hand travel in metres required for one pull.")]
        [SerializeField, Min(0.01f)] float pullDistance = 0.10f;
        [SerializeField] float maxDrop = 0.3f;

        public event Action Pulled;

        XRSimpleInteractable interactable;
        Transform hand;
        float grabY;
        float drop;
        bool fired;
        Vector3 rest;

        void Awake()
        {
            interactable = GetComponent<XRSimpleInteractable>();
            rest = rope.localPosition;
            interactable.selectEntered.AddListener(OnGrab);
            interactable.selectExited.AddListener(OnRelease);
        }

        void OnDestroy()
        {
            interactable.selectEntered.RemoveListener(OnGrab);
            interactable.selectExited.RemoveListener(OnRelease);
        }

        void OnGrab(SelectEnterEventArgs args)
        {
            hand = args.interactorObject.transform;
            grabY = hand.position.y;
            fired = false;
        }

        void OnRelease(SelectExitEventArgs args)
        {
            // A quick pull can reach the threshold and release between Update calls.
            if (!args.isCanceled)
                CheckPull();
            hand = null;
        }

        void Update()
        {
            var pulled = hand != null ? grabY - hand.position.y : 0f;
            drop = Mathf.MoveTowards(drop, Mathf.Clamp(pulled, 0f, maxDrop), (hand != null ? 4f : 1.5f) * Time.deltaTime);
            rope.localPosition = rest + Vector3.down * drop;
            CheckPull();
        }

        void CheckPull()
        {
            var pulled = hand != null ? grabY - hand.position.y : 0f;
            if (hand != null && !fired && pulled >= pullDistance)
            {
                fired = true;
                GameAudio.RopeWhoosh();
                Pulled?.Invoke();
            }
        }
    }
}
