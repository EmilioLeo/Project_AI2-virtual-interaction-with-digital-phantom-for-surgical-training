using UnityEngine;
using System.Linq;

public class MouseRemovalTool : MonoBehaviour
{
    public Camera cam;
    public LayerMask removableMask;               // layer del disco (es. RemoveDisc)
    public float removalRatePerSecond = 0.2f;     // ridotto per rimozione graduale
    public float maxDistance = 500f;              // maggiore della distanza reale
    public float cursorRadius = 0.01f;
    public float moveTowardsCamRate = 0.05f;      // spostamento verso la camera (opzionale)

    private void Reset()
    {
        if (cam == null) cam = Camera.main;
    }

    void Update()
    {
        if (cam == null) return;

        if (Input.GetMouseButton(0))
        {
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);

            // RaycastAll per ignorare oggetti sopra il disco
            RaycastHit[] hits = Physics.RaycastAll(ray, maxDistance, removableMask, QueryTriggerInteraction.Ignore);
            foreach (var hit in hits.OrderBy(h => h.distance))
            {
                var tissue = hit.collider.GetComponentInParent<RemovableDisc>();
                if (tissue != null)
                {
                    float removalAmount = removalRatePerSecond * Time.deltaTime;
                    tissue.ApplyRemoval(removalAmount);

                    // Spostamento leggero verso la camera per effetto "trascinamento"
                    Vector3 dir = (cam.transform.position - tissue.transform.position).normalized;
                    tissue.transform.position += dir * moveTowardsCamRate * Time.deltaTime;

                    break; // applichiamo solo al primo disco rilevato
                }
            }
        }
    }

    void OnDrawGizmos()
    {
        if (cam == null) return;
        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;
        if (Physics.Raycast(ray, out hit, maxDistance, removableMask, QueryTriggerInteraction.Ignore))
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(hit.point, cursorRadius);
        }
    }
}
