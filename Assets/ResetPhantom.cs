using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ResetPhantom : MonoBehaviour
{
   [Header("Oggetti da resettare")]
    public RetractMusclePython sternoLeft;
    public  RetractMusclePython sternoRight;
    public WeArtPickableWithDebug trachea;
    
    public SoftTissueDeformer veins_dx;
    public Deformation_muscle_general carotide_dx;
    public SoftTissueDeformer vagusNerve;
    public DeformerFlexSpinal flexor_spinal;

    public void ResetDeformation()
    {
        if (sternoLeft) {sternoLeft.enabled=false; sternoLeft.StartResetAnimation(); }
        if (sternoRight) {sternoRight.enabled=false; sternoRight.StartResetAnimation();}
        if (trachea) {trachea.enabled=false; trachea.ResetMotion();}
        if (veins_dx) {veins_dx.enabled=false; veins_dx.StartResetAnimationV();}
        if (carotide_dx) {carotide_dx.enabled=false; carotide_dx.StartResetAnimationC();}
        if (vagusNerve) {vagusNerve.enabled=false; vagusNerve.StartResetAnimationV();}
        if (flexor_spinal) {flexor_spinal.enabled=false; flexor_spinal.StartResetAnimationF();}
        
    }
}
