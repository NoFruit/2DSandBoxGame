using System.Collections;
using UnityEngine;

public class TileDropController : MonoBehaviour
{
    private void OnTriggerEnter2D(Collider2D col)
    {
        if (col.gameObject.CompareTag("Player"))
        {
            Destroy(this.gameObject);

            // TODO ���뵽��ҵı�����
        }
    }
}
