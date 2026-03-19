using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using System.Collections;

public class DragDropDisc : MonoBehaviour
{
    [Header("Settings")]
    public float snapDistance = 0.3f;
    public float snapDuration = 0.2f;
    public string targetTag = "TargetDisco";
    public ResetPhantom phantomReset;
    public float resetDelay = 5f;
    private XRGrabInteractable grab;
    private Rigidbody rb;
    private bool isSnapping = false; 

    void Start()
    {
        grab = GetComponent<XRGrabInteractable>();
        rb = GetComponent<Rigidbody>();
    }

    void Update()
    {
        // If it's already hooking or if we're holding it, don't do anything
       
        if (isSnapping || grab.isSelected) return;

        CheckForNearestTarget();
    }

    private void CheckForNearestTarget()
    {
        GameObject[] targets = GameObject.FindGameObjectsWithTag(targetTag);
        foreach (GameObject t in targets)
        {
            float dist = Vector3.Distance(transform.position, t.transform.position);
            
           
            if (dist <= snapDistance)
            {
                StartCoroutine(SmoothSnapRoutine(t.transform));
                break; 
            }
        }
    }

    private IEnumerator SmoothSnapRoutine(Transform target)
    {
        isSnapping = true;


        grab.enabled = false;
        if (rb) rb.isKinematic = true;

        Vector3 startPos = transform.position;
        Quaternion startRot = transform.rotation;
        float elapsed = 0f;

        while (elapsed < snapDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / snapDuration);
            t = t * t * (3f - 2f * t); 

            
            transform.position = Vector3.Lerp(startPos, target.position, t);
            transform.rotation = Quaternion.Slerp(startRot, target.rotation, t);
            
            yield return null;
        }

        // Perfect final positioning
        transform.position = target.position;
        transform.rotation = target.rotation;

        Debug.Log("Aggancio completato!");
        
       
        StartCoroutine(ResetPhantomAfterDelay());
    }

    IEnumerator ResetPhantomAfterDelay()
    {
        yield return new WaitForSeconds(resetDelay);

        phantomReset.ResetDeformation();
    }
}