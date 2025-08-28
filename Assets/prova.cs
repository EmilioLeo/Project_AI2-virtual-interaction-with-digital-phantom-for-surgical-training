using UnityEngine;

public class SurgicalTool : MonoBehaviour
{
    // Oggetti anatomici da interagire
    public GameObject muscle;
    public GameObject artery;
    public GameObject vein;
    public GameObject recurrentNerve;

    // Materiali o colori per indicare stato (opzionale)
    public Material dissectedMaterial;      // per fascia/muscolo disseccato
    public Material protectedNerveMaterial; // per nervo protetto

    void Update()
    {
        // Muove lo strumento seguendo il cursore del mouse
        MoveWithCursor();
    }

    void MoveWithCursor()
    {
        Vector3 mousePos = Input.mousePosition;
        mousePos.z = 5f; // distanza dalla camera (adatta alla tua scena)
        Vector3 worldPos = Camera.main.ScreenToWorldPoint(mousePos);
        transform.position = worldPos;
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.gameObject == muscle)
        {
            Debug.Log("Muscolo divaricato!");
            if (dissectedMaterial != null)
                muscle.GetComponent<Renderer>().material = dissectedMaterial;
        }
        else if (other.gameObject == artery)
        {
            Debug.Log("Arteria retratta lateralmente");
            // qui puoi aggiungere logica di retrazione
        }
        else if (other.gameObject == vein)
        {
            Debug.Log("Vena retratta lateralmente");
            // qui puoi aggiungere logica di retrazione
        }
        else if (other.gameObject == recurrentNerve)
        {
            Debug.Log("Nervo laringeo ricorrente protetto!");
            if (protectedNerveMaterial != null)
                recurrentNerve.GetComponent<Renderer>().material = protectedNerveMaterial;
        }
    }
}