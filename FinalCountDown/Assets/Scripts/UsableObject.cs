using UnityEngine;

public abstract class UsableObject : MonoBehaviour
{
    public string UsableTag = "Usable";

    private void Awake()
    {
        gameObject.tag = UsableTag;
    }

    public virtual void Use(){ }
}
