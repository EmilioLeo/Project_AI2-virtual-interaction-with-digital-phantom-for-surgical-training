using UnityEngine;

public class WeArtPickableWithDebug : MonoBehaviour
{
    private Component weartTouchable;
    private Rigidbody rb;
    private bool isGrabbed = false;

    void Start()
    {
        weartTouchable = GetComponent("WeArtTouchableObject");
        rb = GetComponent<Rigidbody>();

        if (weartTouchable != null)
            Debug.Log("[WeArtPickable] WeArtTouchableObject trovato!");
        else
            Debug.LogWarning("[WeArtPickable] Nessun WeArtTouchableObject trovato su " + gameObject.name);

        if (rb == null)
            Debug.LogWarning("[WeArtPickable] Nessun Rigidbody trovato.");
    }

    void OnTriggerEnter(Collider other)
    {
        // Qui puoi filtrare se vuoi solo guanto specifico
        Debug.Log("[WeArtPickable] Collisione rilevata con: " + other.name);

        // Se vuoi puoi verificare il tag
            Debug.Log("[WeArtPickable] Mano WEART rilevata!");
            Grab();
       
    }

    void OnTriggerExit(Collider other)
    {
      
            Debug.Log("[WeArtPickable] Mano WEART uscita dal collider");
            Release();
    
    }

    private void Grab()
    {
        if (!isGrabbed)
        {
            isGrabbed = true;

            // Chiamata al metodo Grab di WeArtTouchableObject
            weartTouchable?.GetType().GetMethod("Grab")?.Invoke(weartTouchable, null);

            if (rb != null) rb.isKinematic = true;
            Debug.Log("[WeArtPickable] Oggetto preso.");
        }
    }

    private void Release()
    {
        if (isGrabbed)
        {
            isGrabbed = false;

            // Chiamata al metodo Release di WeArtTouchableObject
            weartTouchable?.GetType().GetMethod("Release")?.Invoke(weartTouchable, null);

            if (rb != null) rb.isKinematic = false;
            Debug.Log("[WeArtPickable] Oggetto rilasciato.");
        }
    }
}