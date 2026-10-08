using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

// An item players pick up and arrange in the Projector Room to cast shadows on the wall.
// Holding it still where it should go confirms the placement, which freezes it there for good.
// Items may not overlap: a placement cannot be confirmed while the item is inside another one.
[RequireComponent(typeof(Rigidbody), typeof(XRGrabInteractable))]
public class PlaceableItem : MonoBehaviour
{
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly List<PlaceableItem> Items = new List<PlaceableItem>();

    [SerializeField] float holdSeconds = 2f;
    // Hands are never perfectly still in VR: drifting less than this from where the hold began still counts.
    [SerializeField] float positionLeeway = 0.04f;
    [SerializeField] float rotationLeeway = 12f;
    // Items resting against each other sink in slightly; only deeper contact than this counts as overlapping.
    [SerializeField] float overlapTolerance = 0.005f;
    [SerializeField] Color confirmingColor = new Color(1f, 0.8f, 0.3f);
    [SerializeField] Color overlappingColor = new Color(0.9f, 0.15f, 0.12f);
    [SerializeField] UnityEvent confirmed = new UnityEvent();

    Rigidbody body;
    XRGrabInteractable grab;
    Collider[] colliders;
    Renderer[] renderers;
    MaterialPropertyBlock tintBlock;
    Vector3 holdPosition;
    Quaternion holdRotation;
    float heldStillSeconds;
    // False until the item has been carried away from where it was picked up, so lifting it never confirms it.
    bool carried;

    public bool IsLocked { get; private set; }
    public UnityEvent Confirmed => confirmed;

    // Turns any model into a grabbable item; item generation calls this on whatever it spawns.
    public static PlaceableItem MakePlaceable(GameObject item)
    {
        if (item.GetComponentInChildren<Collider>() == null)
        {
            foreach (MeshFilter filter in item.GetComponentsInChildren<MeshFilter>())
            {
                if (filter.sharedMesh == null)
                    continue;

                MeshCollider collider = filter.gameObject.AddComponent<MeshCollider>();
                collider.sharedMesh = filter.sharedMesh;
                collider.convex = true;
            }
        }

        if (!item.TryGetComponent(out Rigidbody body))
        {
            body = item.AddComponent<Rigidbody>();
            body.mass = 0.25f;
            body.linearDamping = 0.5f;
            body.angularDamping = 0.5f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
        }

        if (!item.TryGetComponent(out PlaceableItem placeable))
            placeable = item.AddComponent<PlaceableItem>();
        placeable.ConfigureGrab();
        return placeable;
    }

    void Reset()
    {
        ConfigureGrab();
    }

    void ConfigureGrab()
    {
        XRGrabInteractable grab = GetComponent<XRGrabInteractable>();
        // Velocity tracking keeps a held item solid, so it is stopped by other items instead of passing through them.
        grab.movementType = XRBaseInteractable.MovementType.VelocityTracking;
        grab.throwOnDetach = true;
    }

    void Awake()
    {
        body = GetComponent<Rigidbody>();
        grab = GetComponent<XRGrabInteractable>();
        colliders = GetComponentsInChildren<Collider>();
        renderers = GetComponentsInChildren<Renderer>();
        tintBlock = new MaterialPropertyBlock();
    }

    void OnEnable()
    {
        Items.Add(this);
        grab.selectEntered.AddListener(OnGrabbed);
        grab.selectExited.AddListener(OnReleased);
    }

    void OnDisable()
    {
        Items.Remove(this);
        grab.selectEntered.RemoveListener(OnGrabbed);
        grab.selectExited.RemoveListener(OnReleased);
    }

    void OnGrabbed(SelectEnterEventArgs args)
    {
        carried = false;
        RestartHold();
    }

    void OnReleased(SelectExitEventArgs args)
    {
        RestartHold();
    }

    void Update()
    {
        if (IsLocked || !grab.isSelected)
            return;

        bool moved = Vector3.Distance(transform.position, holdPosition) > positionLeeway
            || Quaternion.Angle(transform.rotation, holdRotation) > rotationLeeway;
        if (moved)
        {
            carried = true;
            RestartHold();
        }

        if (OverlapsAnotherItem())
        {
            heldStillSeconds = 0f;
            SetTint(overlappingColor, 1f);
            return;
        }

        if (moved || !carried)
        {
            SetTint(confirmingColor, 0f);
            return;
        }

        heldStillSeconds += Time.deltaTime;
        SetTint(confirmingColor, heldStillSeconds / holdSeconds);
        if (heldStillSeconds >= holdSeconds)
            Lock();
    }

    void RestartHold()
    {
        holdPosition = transform.position;
        holdRotation = transform.rotation;
        heldStillSeconds = 0f;
        SetTint(confirmingColor, 0f);
    }

    public bool OverlapsAnotherItem()
    {
        foreach (PlaceableItem other in Items)
        {
            if (other == this)
                continue;

            foreach (Collider mine in colliders)
            {
                foreach (Collider theirs in other.colliders)
                {
                    if (Physics.ComputePenetration(
                            mine, mine.transform.position, mine.transform.rotation,
                            theirs, theirs.transform.position, theirs.transform.rotation,
                            out _, out float depth)
                        && depth > overlapTolerance)
                        return true;
                }
            }
        }

        return false;
    }

    // Freezes the item where it is and takes it out of play: it can no longer be grabbed or pushed.
    void Lock()
    {
        IsLocked = true;
        SetTint(confirmingColor, 0f);

        // A locked item must not fly out of the hand that was holding it.
        grab.throwOnDetach = false;
        if (grab.isSelected && grab.interactionManager != null)
            grab.interactionManager.CancelInteractableSelection((IXRSelectInteractable)grab);
        grab.enabled = false;

        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        body.isKinematic = true;

        confirmed.Invoke();
    }

    // Blends every material toward `color`; an amount of 0 restores the item's own colors.
    void SetTint(Color color, float amount)
    {
        foreach (Renderer itemRenderer in renderers)
        {
            Material[] materials = itemRenderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
            {
                if (amount <= 0f || materials[i] == null || !materials[i].HasProperty(BaseColorId))
                {
                    itemRenderer.SetPropertyBlock(null, i);
                    continue;
                }

                tintBlock.SetColor(BaseColorId, Color.Lerp(materials[i].GetColor(BaseColorId), color, Mathf.Clamp01(amount) * 0.8f));
                itemRenderer.SetPropertyBlock(tintBlock, i);
            }
        }
    }
}
