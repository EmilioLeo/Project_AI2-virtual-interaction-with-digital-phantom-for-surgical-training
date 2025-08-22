using UnityEngine;
using TMPro;
public class TreatmentSetup : MonoBehaviour
{
    [Header("Riferimenti oggetti in scena")]
    /*public GameObject vertebraC5;
    public GameObject vertebraC6;
    public GameObject discoC5C6;
    */
    public GameObject[] vertebre;
    public GameObject[] dischi;
    [Header("Messaggi di click")]
    public TextMeshProUGUI message; //testo da visualizzare

    [Header("Materiali trasparenti")]
    public Material yellowTransparent; // colore vertebra selezionata
    public Material redTransparent; //colore disco selezionato
    
    Camera cam;

    bool[] vertebreMarked;
    bool[] dischiMarked;

    //colore originale delle vertebre
    Material[] originalVertebraMaterials;
    Material[] originalDischiMaterials;

    void Start()
    {
        /* Applica giallo semitrasparente a C5 e C6
        if (vertebraC5) vertebraC5.GetComponent<Renderer>().material = yellowTransparent;
        if (vertebraC6) vertebraC6.GetComponent<Renderer>().material = yellowTransparent;

        Applica rosso semitrasparente al disco
        if (discoC5C6) discoC5C6.GetComponent<Renderer>().material = redTransparent; */
        cam = Camera.main;
        vertebreMarked = new bool[vertebre.Length];
        dischiMarked = new bool[dischi.Length];
        if (message != null) message.gameObject.SetActive(false);

        originalVertebraMaterials = new Material[vertebre.Length];

        for(int i=0; i<vertebre.Length; i++)
        {
            originalVertebraMaterials[i]=vertebre[i].GetComponent<Renderer>().material;
        }

        originalDischiMaterials=new Material[dischi.Length]; 

        for(int i=0; i<dischi.Length; i++)
        {
            originalDischiMaterials[i]=dischi[i].GetComponent<Renderer>().material;
        }
    
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0)) //tasto sx del mouse
        { 
            
            //prendi il raggio 3D che identica l'oggetto selezionato alla posizione del mouse nella simulazione
            Ray ray = cam.ScreenPointToRay(Input.mousePosition); 
            //se è stato selezionato un oggetto allora è un collider.
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                for (int i = 0; i < vertebre.Length; i++)
                {
                    // se vertebra i-esima non è ancora stata marcata ed è stato cliccato -> colora+messaggio
                     if(vertebre[i] != null && !vertebreMarked[i] && hit.collider.gameObject == vertebre[i]) 
                     {
                        vertebre[i].GetComponent<Renderer>().material = yellowTransparent;
                        vertebreMarked[i] = true;
                        
                        //mostra messaggio
                        ShowMessage($"Selezionata vertebra: {vertebre[i].name}"); 
                     }
 
                }

                for (int i = 0; i < dischi.Length; i++)
                {
                    // se disco i-esimo non è ancora stato marcato ed è stato cliccato -> colora+messaggio
                    if(dischi[i]!=null && !dischiMarked[i] && hit.collider.gameObject==dischi[i])
                    {
                        dischi[i].GetComponent<Renderer>().material = redTransparent;
                        dischiMarked[i] = true;

                        //mostra messaggio
                        ShowMessage($"Selezionato disco: {dischi[i].name}");
                    }
                }
            }

        }else if(Input.GetMouseButtonDown(1)) //click del tasto destro disattiva color 
        {
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                for (int i = 0; i < vertebre.Length; i++)
                {   
                    //se vertebra è stata già marcata con il colore ed è quella da deselezionare 
                     if (vertebre[i] != null && hit.collider.gameObject == vertebre[i] && vertebreMarked[i])
                    {
                        vertebre[i].GetComponent<Renderer>().material = originalVertebraMaterials[i];
                        vertebreMarked[i] = false;
                        ShowMessage($"Ripristinata vertebra: {vertebre[i].name}");
                    }
                }

                for (int i = 0; i < dischi.Length; i++)
                {
                    if (dischi[i]!=null && hit.collider.gameObject == dischi[i] && dischiMarked[i])
                    {
                        dischi[i].GetComponent<Renderer>().material = originalDischiMaterials[i];
                        dischiMarked[i] = false;
                        ShowMessage($"Ripristinato disco: {dischi[i].name}");
                    }
                }
            }

        }
    }


    void ShowMessage(string msg)
    {
        if (message == null)
        {
            Debug.Log(msg);
            return;
        }

        message.text = msg;
        message.gameObject.SetActive(true);
    }
}

