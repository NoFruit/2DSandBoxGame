using System.Collections;
using UnityEngine;

public class TileDropController : MonoBehaviour
{
    private void OnTriggerEnter2D(Collider2D col)
    {
        if (col.gameObject.CompareTag("Player"))
        {
            Destroy(this.gameObject);

            // TODO 加入到玩家的背包栏
        }
    }
}
