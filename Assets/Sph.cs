using UnityEngine;

public class SimpleGraspSnap : MonoBehaviour
{
    public Transform palm; // assegna qui il palm (il tuo _grasper)
    private GameObject currentObject;

    private void OnTriggerEnter(Collider other)
    {
        if (other.GetComponent<Rigidbody>() && other.GetComponent<WeArt.Components.WeArtTouchableObject>())
        {
            currentObject = other.gameObject;
            AttachObject();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.gameObject == currentObject)
        {
            DetachObject();
            currentObject = null;
        }
    }

    private void AttachObject()
    {
        if (currentObject != null)
        {
            var rb = currentObject.GetComponent<Rigidbody>();
            rb.isKinematic = true; // disattiva fisica
            currentObject.transform.SetParent(palm);
            currentObject.transform.localPosition = Vector3.zero;
        }
    }

    private void DetachObject()
    {
        if (currentObject != null)
        {
            currentObject.transform.SetParent(null);
            var rb = currentObject.GetComponent<Rigidbody>();
            rb.isKinematic = false;
        }
    }
}
