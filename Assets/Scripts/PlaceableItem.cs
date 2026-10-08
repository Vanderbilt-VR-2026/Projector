using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

// An item players pick up and arrange in the Projector Room to cast shadows on the wall.
[RequireComponent(typeof(Rigidbody), typeof(XRGrabInteractable))]
public class PlaceableItem : MonoBehaviour
{
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
}
