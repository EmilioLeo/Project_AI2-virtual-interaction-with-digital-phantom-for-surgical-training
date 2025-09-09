using UnityEngine;

public class MuscleMenuTrigger : MonoBehaviour
{
    [Header("UI")]
    public GameObject menuScelta;

    [Header("Scene References")]
    public GameObject cutLine;             
    public ChoiceTotalRemova choiceTotal;  

    private bool hasChosen = false;

    void Start()
    {
        if (menuScelta != null)
            menuScelta.SetActive(false);

        if (cutLine != null)
            cutLine.SetActive(false);

        if (choiceTotal != null)
            choiceTotal.enabled = false;   // drag disattivo all’inizio
    }

    void OnMouseDown()
    {
        if (hasChosen) return;  // menu compare solo una volta

        if (menuScelta != null)
        {
            menuScelta.SetActive(true);
            menuScelta.transform.position = Input.mousePosition;
        }
    }

    public void AsportaParziale()
    {
        Debug.Log("Hai scelto ASPORTAZIONE PARZIALE per: " + gameObject.name);

        NascondiMenu();
    }

    public void AsportaTotale()
    {
        Debug.Log("Hai scelto ASPORTAZIONE TOTALE per: " + gameObject.name);
        hasChosen = true;

        if (choiceTotal != null)
            choiceTotal.enabled = true;

        NascondiMenu();
    }

    public void NascondiMenu()
    {
        if (menuScelta != null)
            menuScelta.SetActive(false);
    }
}
