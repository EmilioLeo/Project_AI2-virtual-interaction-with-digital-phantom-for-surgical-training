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
    private bool isSnapping = false; // La nostra "chiave" di sicurezza

    void Start()
    {
        grab = GetComponent<XRGrabInteractable>();
        rb = GetComponent<Rigidbody>();
    }

    void Update()
    {
        // Se si sta già agganciando o se lo stiamo tenendo in mano, non fare nulla
        // (Nota: se vuoi che lo snap avvenga ANCHE mentre lo tieni, togli grab.isSelected)
        if (isSnapping || grab.isSelected) return;

        CheckForNearestTarget();
    }

    private void CheckForNearestTarget()
    {
        GameObject[] targets = GameObject.FindGameObjectsWithTag(targetTag);
        foreach (GameObject t in targets)
        {
            float dist = Vector3.Distance(transform.position, t.transform.position);
            
            // LOG PER DEBUG: Così vedi subito in console se lo rileva
            //Debug.Log($"Distanza da {t.name}: {dist}");

            if (dist <= snapDistance)
            {
                StartCoroutine(SmoothSnapRoutine(t.transform));
                break; // Esci dal ciclo appena ne trovi uno valido
            }
        }
    }

    private IEnumerator SmoothSnapRoutine(Transform target)
    {
        isSnapping = true;

        Debug.Log($"<color=cyan>[SNAP START]</color> Mi muovo verso {target.name} a posizione: {target.position}");
        
        // Disabilitiamo l'interactable per evitare che la mano lo "rubi" durante il movimento
        grab.enabled = false;
        if (rb) rb.isKinematic = true;

        Vector3 startPos = transform.position;
        Quaternion startRot = transform.rotation;
        float elapsed = 0f;

        while (elapsed < snapDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / snapDuration);
            t = t * t * (3f - 2f * t); // Movimento fluido

            // Usiamo target.position (World Space) per evitare l'errore del "va troppo su"
            transform.position = Vector3.Lerp(startPos, target.position, t);
            transform.rotation = Quaternion.Slerp(startRot, target.rotation, t);
            
            yield return null;
        }

        // Posizionamento finale perfetto
        transform.position = target.position;
        transform.rotation = target.rotation;

        Debug.Log("Aggancio completato!");
        
        // Se vuoi poterlo riprendere dopo lo snap, riabilita grab.enabled qui
        // grab.enabled = true; 
        StartCoroutine(ResetPhantomAfterDelay());
    }

    IEnumerator ResetPhantomAfterDelay()
    {
        yield return new WaitForSeconds(resetDelay);

        phantomReset.ResetDeformation();
    }
}