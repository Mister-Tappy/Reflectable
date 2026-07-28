using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Reflectable
{
    public sealed partial class GameplayFeedbackManager : MonoBehaviour
    {
        [SerializeField] Camera gameCamera;
        [SerializeField] Text comboLabel;
        Vector3 cameraBase;
        Coroutine shake;

        void Awake() => InitializeArcadeFeedback();
        public void Hit(int combo) => Hit(combo, Vector3.zero, 0, false, ArcadeHitKind.Direct, false);
        public void Shake(float strength,float duration) => StartArcadeShake(strength, duration);
        IEnumerator ShakeRoutine(float strength,float duration){if(!gameCamera)yield break;for(float t=0;t<duration;t+=Time.unscaledDeltaTime){gameCamera.transform.localPosition=cameraBase+(Vector3)Random.insideUnitCircle*strength;yield return null;}if(gameCamera)gameCamera.transform.localPosition=cameraBase;shake=null;}
    }
}
