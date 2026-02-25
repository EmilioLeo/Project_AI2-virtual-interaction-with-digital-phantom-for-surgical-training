using UnityEngine;

public class WeArtPickableWithDebug : MonoBehaviour
{
    private Component weartTouchable;
    private Rigidbody rb;
    private bool isGrabbed = false;

    [Header("Impostazioni Traslazione Trachea")]
    [Tooltip("L'offset massimo (spostamento) applicato. Assicurati che il valore sia piccolo, es. 0.05 per 5cm")]
    public Vector3 lateralOffset = new Vector3(0.05f, 0f, 0f); 
    public float moveSpeed = 5f;
    //public string weartFingerComponentName = "WeArtThimbleTrackingObject";
    private Vector3 initialPosition;
    private Vector3 targetPosition;
    Transform presser;
    void Start()
    {
        weartTouchable = GetComponent("WeArtTouchableObject");
        rb = GetComponent<Rigidbody>();

        initialPosition = transform.position;
        targetPosition = initialPosition;

        // Impostiamo l'oggetto come cinematico da subito per evitare che cada o impazzisca
        if (rb != null)
        {
            rb.isKinematic = true;
        }

        if (weartTouchable != null)
            Debug.Log("[WeArtPickable] WeArtTouchableObject trovato!");
        else
            Debug.LogWarning("[WeArtPickable] Nessun WeArtTouchableObject trovato su " + gameObject.name);
    }

    // FixedUpdate è più sicuro per aggiornare oggetti che hanno un Rigidbody
    void FixedUpdate() 
    {
        // Se la posizione attuale è diversa da quella target, muoviamo l'oggetto
        if (Vector3.Distance(transform.position, targetPosition) > 0.0001f)
        {
            Vector3 newPosition = Vector3.Lerp(transform.position, targetPosition, Time.fixedDeltaTime * moveSpeed);
            
            if (rb != null)
            {
                rb.MovePosition(newPosition); // Movimento sicuro per la fisica
            }
            else
            {
                transform.position = newPosition; // Fallback se manca il Rigidbody
            }
        }
    }

    void OnTriggerEnter(Collider other)
    {
        //Component weartFinger = other.GetComponent(weartFingerComponentName);
        if (other.gameObject.name.Contains("Thimble") || other.gameObject.name.Contains("Index")){
            Debug.Log("[WeArtPickable] Collisione rilevata con: " + other.name);
            presser=other.transform;
            Grab();

        }
        
    }

    void OnTriggerExit(Collider other)
    {
        //Component weartFinger = other.GetComponent(weartFingerComponentName);
        if (other.transform == presser && presser!=null)
        {
            Debug.Log("[WeArtPickable] Mano WEART uscita: " + other.name);
            Release();
        }
    }

    private void Grab()
    {
        if (!isGrabbed)
        {
            isGrabbed = true;

            // TransformDirection applica l'offset ruotato come l'oggetto, ma IGNORA la scala dell'oggetto.
            // In questo modo evitiamo che l'oggetto venga "sparato" via.
            targetPosition = initialPosition + transform.TransformDirection(lateralOffset);

            weartTouchable?.GetType().GetMethod("Grab")?.Invoke(weartTouchable, null);

            Debug.Log("[WeArtPickable] Oggetto preso. Inizio traslazione.");
        }
    }

    private void Release()
    {
        if (isGrabbed)
        {
            isGrabbed = false;

            // Riporta il target alla posizione iniziale
            //targetPosition = initialPosition;
            // Gestione sicura del WeArt SDK
            //TryInvokeWeArtMethod("Release", finger);
            weartTouchable?.GetType().GetMethod("Release")?.Invoke(weartTouchable, null);
            //rb.isKinematic = false;
            // IMPORTANTE: Abbiamo rimosso  in modo che 
            // la trachea resti ferma al suo posto e non cada per la gravità!
            
            Debug.Log("[WeArtPickable] Oggetto rilasciato. Ritorno alla posizione originale.");
        }
    }
    /*
    private void TryInvokeWeArtMethod(string methodName, Component finger)
    {
        if (weartTouchable == null) return;

        try
        {
            MethodInfo method = weartTouchable.GetType().GetMethod(methodName);
            if (method != null)
            {
                ParameterInfo[] parameters = method.GetParameters();
                
                // Se il metodo non richiede parametri (es. Grab())
                if (parameters.Length == 0)
                {
                    method.Invoke(weartTouchable, null);
                }
                // Se il metodo richiede un parametro (es. Grab(WeArtThimble))
                else if (parameters.Length == 1)
                {
                    method.Invoke(weartTouchable, new object[] { finger });
                }
                else
                {
                    Debug.LogWarning($"[WeArtPickable] Il metodo {methodName} richiede {parameters.Length} parametri. Impossibile chiamarlo in automatico.");
                }
            }
        }catch (System.Exception e)
        {
            // Se fallisce, catturiamo l'errore senza bloccare lo spostamento della trachea!
            Debug.LogError($"[WeArtPickable] Errore interno WeArt durante {methodName}: " + e.Message);
        }


    }*/
}