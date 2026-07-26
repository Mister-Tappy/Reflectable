using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Reflectable
{
    public sealed class GameplayFeedbackManager : MonoBehaviour
    {
        [SerializeField] Camera gameCamera;
        [SerializeField] Text comboLabel;
        Vector3 cameraBase;
        Coroutine shake;

        void Awake(){ if(!gameCamera)gameCamera=Camera.main; if(gameCamera)cameraBase=gameCamera.transform.localPosition; }
        public void Hit(int combo){ if(combo>=5)StartCoroutine(PunchCombo(combo)); if(combo>=10)Shake(combo>=30?.15f:combo>=20?.10f:.06f,.12f); }
        public void Shake(float strength,float duration){if(shake!=null)StopCoroutine(shake);shake=StartCoroutine(ShakeRoutine(strength,duration));}
        IEnumerator ShakeRoutine(float strength,float duration){if(!gameCamera)yield break;for(float t=0;t<duration;t+=Time.unscaledDeltaTime){gameCamera.transform.localPosition=cameraBase+(Vector3)Random.insideUnitCircle*strength;yield return null;}if(gameCamera)gameCamera.transform.localPosition=cameraBase;shake=null;}
        IEnumerator PunchCombo(int combo){if(!comboLabel)yield break;var rect=comboLabel.rectTransform;var baseScale=Vector3.one;float amount=combo>=30?1.7f:combo>=20?1.5f:combo>=10?1.35f:1.2f;for(float t=0;t<.08f;t+=Time.unscaledDeltaTime){rect.localScale=Vector3.Lerp(baseScale,baseScale*amount,t/.08f);yield return null;}for(float t=0;t<.14f;t+=Time.unscaledDeltaTime){rect.localScale=Vector3.Lerp(baseScale*amount,baseScale,t/.14f);yield return null;}rect.localScale=baseScale;}
    }
}
