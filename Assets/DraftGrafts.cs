using UnityEngine;
using System.Collections;
public class DraftGrafts : MonoBehaviour
{
    public Transform cage;
    public Transform[] grafts;    // I 3 innesti dove il primo centrale è cuboe due trapezi rettangolari laterali
    public Transform[] anchors;   // 3 posizioni target dentro la cage
    public float offsetDistance = 1.0f; //tolleranza distanza tra mouse and cage
    public float insertDuration = 2.0f; //time di inserimento di innesti  
    private bool isInserting = false; //verifica se i 3 innesti si trova nello stato inserimento
    private bool inserted = false; //verifica se i 3 innesti sono già stati inseriti 


    // Update is called once per frame
    void Update()
    {
        if (Input.GetMouseButton(1) && !inserted && !isInserting)
        {
            if(IsMouseOverCage()){
                StartCoroutine(InsertAllGrafts());
            }
                
        }
    }
    IEnumerator InsertAllGrafts()
    {
        isInserting = true;
        
        //control per verificare che il numero di posizioni di ancoraggio siano uguali al numero degli innesti 
        if (grafts.Length != anchors.Length) yield break;
        
        float t=0f;
        //creation di due array di vettori vector3 e Quaternions
        Vector3[] startpos=new Vector3[grafts.Length];
        Quaternion[] startrot=new Quaternion[anchors.Length];

        for(int i=0; i<grafts.Length;i++)
        {
            startpos[i]=grafts[i].position;
            startrot[i]=grafts[i].rotation;
        }
        
        while(t<1f)
        {
            //computation istant time t 
            t+=Time.deltaTime / insertDuration; 
            
            float smoothT=Mathf.SmoothStep(0f,1f,t);
            
            for(int i=0;i<grafts.Length;i++)
            {
                grafts[i].position = Vector3.Lerp(startpos[i], anchors[i].position, smoothT);
                grafts[i].rotation = Quaternion.Slerp(startrot[i], anchors[i].rotation, smoothT);
            }
            yield return null;
        }
        
        //fix final position
        for (int i = 0; i < grafts.Length; i++)
        {
            grafts[i].SetPositionAndRotation(anchors[i].position, anchors[i].rotation);
            grafts[i].SetParent(cage); // diventano figli della cage
        }
        inserted = true;
        isInserting = false;
        Debug.Log("3 innesti ossei inseriti nella cage intersomatica.");
    }
    bool IsMouseOverCage()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        // Se colpisce qualcosa
        if (Physics.Raycast(ray, out hit))
        {
            // Se è la cage
            if (hit.transform == cage || hit.transform.IsChildOf(cage))
            {
                return true;
            }

            // Altrimenti controllo se è "vicino alla cage"
            float dist = Vector3.Distance(hit.point, cage.position);
            if (dist <= offsetDistance)
            {
                return true;
            }
        }
        return false;
    }
}
