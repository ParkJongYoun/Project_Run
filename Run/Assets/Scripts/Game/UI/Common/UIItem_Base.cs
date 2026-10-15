using UnityEngine;

namespace ProjectRun.Race
{
    public abstract class UIItem_Base<T> : MonoBehaviour
    {
        public abstract void Bind(T data);
        public virtual void ResetItem() { gameObject.SetActive(false); }
    }
}
