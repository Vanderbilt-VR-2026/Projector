using System;
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
        [SerializeField] float pullDistance = 0.15f;
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

        void OnRelease(SelectExitEventArgs args) => hand = null;

        void Update()
        {
            var pulled = hand != null ? grabY - hand.position.y : 0f;
            drop = Mathf.MoveTowards(drop, Mathf.Clamp(pulled, 0f, maxDrop), (hand != null ? 4f : 1.5f) * Time.deltaTime);
            rope.localPosition = rest + Vector3.down * drop;
            if (hand != null && !fired && pulled >= pullDistance)
            {
                fired = true;
                Pulled?.Invoke();
            }
        }
    }
}
