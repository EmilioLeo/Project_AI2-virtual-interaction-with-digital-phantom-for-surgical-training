using UnityEngine;
using TMPro;

public class TreatmentSetup : MonoBehaviour
{
    [Header("References objects on stage")]
    public GameObject[] vertebre;   // C1–C7
    public GameObject[] dischi;     // C1-2, C2-3, … C6-7
   

    [Header("Transparent Material")]
    public Material yellowTransparent; // colore vertebra selezionata
    public Material redTransparent;    // colore disco selezionato

    Camera cam;

    bool[] vertebreMarked;
    bool[] dischiMarked;

    Material[] originalVertebraMaterials;
    Material[] originalDischiMaterials;

    void Start()
    {
        cam = Camera.main;

        vertebreMarked = new bool[vertebre.Length];
        dischiMarked = new bool[dischi.Length];
     
       

        for (int i = 0; i < dischi.Length; i++)
        {
            // example: we already color the C4-5 disk
            if (i == 2 && dischi[i] != null)
            {
                dischi[i].GetComponent<Renderer>().material = redTransparent;
            }
        }

        for(int j=0;j<vertebre.Length;j++ )
        {
            //example: we already color the vertebras C4-C5
            if ((j==3  || j==4) && vertebre[j]!=null)
            {
                vertebre[j].GetComponent<Renderer>().material = yellowTransparent;

            }
        }
        // saves original vertebras materials
        originalVertebraMaterials = new Material[vertebre.Length];
        for (int i = 0; i < vertebre.Length; i++)
            originalVertebraMaterials[i] = vertebre[i].GetComponent<Renderer>().material;

        // saves original materials to disks
        originalDischiMaterials = new Material[dischi.Length];
        for (int i = 0; i < dischi.Length; i++)
            originalDischiMaterials[i] = dischi[i].GetComponent<Renderer>().material;
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0)) // click left→ selection
        {
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                // VERTEBRAS
                for (int i = 0; i < vertebre.Length; i++)
                {
                    if (vertebre[i] != null && !vertebreMarked[i] && hit.collider.gameObject == vertebre[i])
                    {
                        vertebre[i].GetComponent<Renderer>().material = yellowTransparent;
                        vertebreMarked[i] = true;
                      
                    }
                }

                // DISCKS
                for (int i = 0; i < dischi.Length; i++)
                {
                    if (dischi[i] != null && !dischiMarked[i] && hit.collider.gameObject == dischi[i])
                    {
                        dischi[i].GetComponent<Renderer>().material = redTransparent;
                        dischiMarked[i] = true;
                        
                    }
                }
            }
        }
        else if (Input.GetMouseButtonDown(1)) // click right→ deselection
        {
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                // VERTEBRAS
                for (int i = 0; i < vertebre.Length; i++)
                {
                    if (vertebre[i] != null && hit.collider.gameObject == vertebre[i] && vertebreMarked[i])
                    {
                        vertebre[i].GetComponent<Renderer>().material = originalVertebraMaterials[i];
                        vertebreMarked[i] = false;
                       
                    }
                }

                // DISKS
                for (int i = 0; i < dischi.Length; i++)
                {
                    if (dischi[i] != null && hit.collider.gameObject == dischi[i] && dischiMarked[i])
                    {
                        dischi[i].GetComponent<Renderer>().material = originalDischiMaterials[i];
                        dischiMarked[i] = false;
                        
                    }
                }
            }
        }
    }

   
}
