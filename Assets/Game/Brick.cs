using UnityEngine;
using System.Collections;

public class Brick : MonoBehaviour
{

    public byte x;
    public byte y;

    private byte _health;

    public byte health
    {
        get { return _health; }
        set
        {
            _health = value;
            gameObject.SetActive(value > 0);
        }
    }
}
