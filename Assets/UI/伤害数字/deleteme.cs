using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class deleteme : MonoBehaviour
{
    void Start()
    {
        StartCoroutine(deletemyself());
    }

    public IEnumerator deletemyself()
    {
        yield return new WaitForSeconds(2.5f);
        Destroy(gameObject);
    }
}
