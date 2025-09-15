using UnityEngine;

public class ObjectNumberGenerator : MonoBehaviour
{
    private int nextUniqueID = 1;

    public int UseUniqueID()
    {
        nextUniqueID++;
        return nextUniqueID;
    }
}
