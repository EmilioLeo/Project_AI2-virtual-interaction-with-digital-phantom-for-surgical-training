using UnityEngine;

public class WeArtPickableWithDebug : MonoBehaviour
{
    private Component weartTouchable;
    private Rigidbody rb;
    private bool isGrabbed = false;

    [Header("Trachea Translation Settings")]
    [Tooltip("The maximum offset (displacement) applied")]
    public Vector3 lateralOffset = new Vector3(0.05f, 0f, 0f); 
    public float moveSpeed = 5f;
    private Vector3 initialPosition;
    private Vector3 targetPosition;
    Transform presser;
    void Start()
    {
        weartTouchable = GetComponent("WeArtTouchableObject");
        rb = GetComponent<Rigidbody>();

        initialPosition = transform.position;
        targetPosition = initialPosition;

        // Let's set the object as kinematic immediately to prevent it from falling or going crazy
        if (rb != null)
        {
            rb.isKinematic = true;
        }

        if (weartTouchable != null)
            Debug.Log("[WeArtPickable] WeArtTouchableObject trovato!");
        else
            Debug.LogWarning("[WeArtPickable] Nessun WeArtTouchableObject trovato su " + gameObject.name);
    }


    void FixedUpdate() 
    {
        // If the current position is different from the target position, we move the object
        if (Vector3.Distance(transform.position, targetPosition) > 0.0001f)
        {
            Vector3 newPosition = Vector3.Lerp(transform.position, targetPosition, Time.fixedDeltaTime * moveSpeed);
            
            if (rb != null)
            {
                rb.MovePosition(newPosition); // Physics-safe movement
            }
            else
            {
                transform.position = newPosition; // Fallback if the Rigidbody is missing
            }
        }
    }

    void OnTriggerEnter(Collider other)
    {
        
        if (other.gameObject.name.Contains("Thimble") || other.gameObject.name.Contains("Index")){
            Debug.Log("[WeArtPickable] Collisione rilevata con: " + other.name);
            presser=other.transform;
            Grab();

        }
        
    }

    void OnTriggerExit(Collider other)
    {
        
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

            // TransformDirection applies the rotated offset like the object, but IGNORE the object's scale.
            // This way we prevent the object from being "shot" away.
            targetPosition = initialPosition + transform.TransformDirection(lateralOffset);

            weartTouchable?.GetType().GetMethod("Grab")?.Invoke(weartTouchable, null);

            
        }
    }

    private void Release()
    {
        if (isGrabbed)
        {
            isGrabbed = false;

            // Return the target to the starting position
            // Secure management of the WeArt SDK
            weartTouchable?.GetType().GetMethod("Release")?.Invoke(weartTouchable, null); 
           
        }
    }

    public void ResetMotion(){
        transform.position=initialPosition;

    }
    
}