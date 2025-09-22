using UnityEngine;

public class DragDropDisc : MonoBehaviour
{
    [Header("Layers/Tags")]
   // [Tooltip("Layer name used while dragging (should not collide with Bone)")]
    public string ghostLayerName = "GhostDisc";
    //[Tooltip("Layer name used when solid")]
    public string solidLayerName = "Disc";
   // [Tooltip("Layer name of vertebrae (to optionally force-ignore during drag)")]
    public string boneLayerName = "Bone";
    //[Tooltip("If true, toggles the global layer collision matrix at runtime for GhostDisc vs Bone")]
    public bool manageLayerMatrixAtRuntime =false;

    public float snapGraceSec = 0.30f;
    //[Tooltip("Freeze constraints after successful snap")]
    public bool freezeWhenSnapped = true;
    //[Tooltip("Constraints applied when snapped (if freezeWhenSnapped=true)")]
    public RigidbodyConstraints snappedConstraints = RigidbodyConstraints.FreezeAll;    
    public bool kinematicWhenSnapped = true;

    private RigidbodyConstraints savedConstraints;
    private bool savedIsKinematic;

    public bool useMovePosition = true;
    private int originalLayer;

    //ids layers 
    private int ghostLayer = -1, solidLayer = -1, boneLayer = -1;
    private Vector3 offset; //offset tra mouse e posizione dell'oggetto
    //private float zCoordinate; //coordinata z
    private Rigidbody rb; //rigid body del target
    private bool isDragging = false;
    Transform snapTarget = null;
    private Camera cam;
    private bool isOverTarget = false;
    
    
   
    private float lastOverTargetTime = -999f;  
    
    void Start()
    {
        cam = Camera.main;
       
        rb = GetComponent<Rigidbody>();
        
        //at the beginning of simulation save kinematic 
        savedConstraints = rb ? rb.constraints : RigidbodyConstraints.None;
        savedIsKinematic = rb ? rb.isKinematic : false;
        /*if (ghostLayer == -1 || solidLayer == -1)
        {
            Debug.LogError("DragDropDisc: Please create and assign layers '" + ghostLayerName + "' and '" + solidLayerName + "'.");
        }*/
        ghostLayer = LayerMask.NameToLayer(ghostLayerName);
        solidLayer = LayerMask.NameToLayer(solidLayerName);
        boneLayer = LayerMask.NameToLayer(boneLayerName);
        originalLayer = gameObject.layer;
    }
   
    void OnMouseDown()
    {
        isDragging = true;
        offset = this.gameObject.transform.position - GetMouseWorldPos();
        
        // Rendi il Rigidbody "kinematic" per evitare che la fisica interferisca durante il trascinamento
        if (rb != null)
        {
            savedConstraints = rb.constraints;
            savedIsKinematic = rb.isKinematic;  
            rb.isKinematic = true;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            
        }
        //switch layer
        SetLayerRecursively(gameObject, ghostLayer);
       
    }
    void Update()
    {
        if(!isDragging) return;
        if (Input.GetMouseButton(0))
        {
            //continua a seguire il mouse anche se non sei più sopra il collider del disco
            Vector3 target = GetMouseWorldPos() + offset;
            if (rb && useMovePosition) rb.MovePosition(target); 
            else transform.position = target;
        }
        if(Input.GetMouseButtonUp(0))
        {
            bool graceOk = (Time.time - lastOverTargetTime) <= snapGraceSec;
            bool canSnap = (snapTarget != null) && (isOverTarget || graceOk);
            if (canSnap)
            {
                Transform anchor = snapTarget.Find("Anchor") ?? snapTarget;
                Debug.Log("anchor"+anchor.position.ToString());
                if(anchor){
                    if(rb)
                    {
                        rb.MovePosition(anchor.position);
                        rb.MoveRotation(anchor.rotation);
                    }else
                    {
                        transform.SetPositionAndRotation(anchor.position, anchor.rotation);
                    }
                }
                SetLayerRecursively(gameObject, solidLayer);
                if (rb)
                {
                    rb.isKinematic = kinematicWhenSnapped;
                    rb.constraints = freezeWhenSnapped ? snappedConstraints : savedConstraints;
                }
                isDragging = false;
                return;
            }else
            {
                SetLayerRecursively(gameObject, solidLayer);
                if (rb)
                {
                    rb.isKinematic = savedIsKinematic; // usually false
                    rb.constraints = savedConstraints;
                }
                isDragging = false;

            }
        }
    }

    private Vector3 GetMouseWorldPos()
    {
        Vector3 mousePoint = Input.mousePosition;
        mousePoint.z =  Camera.main.WorldToScreenPoint(transform.position).z;
        return cam.ScreenToWorldPoint(mousePoint);
    }

    //Rileva l'ingresso in un'area trigger
    void OnTriggerEnter(Collider other)
    {
        // Controlla se l'area in cui siamo entrati ha il tag del target
        if (other.CompareTag("TargetDisco"))
        {
            Debug.Log("Sopra un'area di aggancio!");
            // Memorizza il transform del target
            isOverTarget = true;
            snapTarget = other.transform.parent; 
            lastOverTargetTime = Time.time;
        }
    }
    void OnTriggerStay(Collider other)
    {
        if (other.CompareTag("TargetDisco"))
        {
            isOverTarget = true;
            snapTarget = other.transform.parent; 
            lastOverTargetTime = Time.time; // aggiorna finché resti dentro
        }
    }
    //Rileva l'uscita da un'area trigger
    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("TargetDisco"))
        {
            Debug.Log("Uscito dall'area di aggancio.");
            if (snapTarget == other.transform.parent) snapTarget = null;
            isOverTarget = false;
        }
    }

    //static function to set correct layer
    private static void SetLayerRecursively(GameObject obj, int layer)
    {
        if (layer < 0) return;
        obj.layer = layer;
        foreach (Transform t in obj.transform)
            SetLayerRecursively(t.gameObject, layer);
    }

}
