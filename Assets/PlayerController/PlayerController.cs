using System.Collections;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    public TileClass selectedTile;

    public int playerRange;
    public Vector2Int mousePos;

    public float moveSpeed;
    public float jumpForce;
    public bool onGround;

    private Rigidbody2D rb;
    private Animator anim;

    public float horizontal;
    public bool hit;
    public bool place;

    [HideInInspector]
    public Vector2 spawnPos;
    public TerrianGeneration terrianGenerator;

    public void Spawn()
    {
        GetComponent<Transform>().position = spawnPos;
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
    }

    private void OnTriggerStay2D(Collider2D col)
    {
        if (col.CompareTag("Ground"))
            onGround = true;
    }
    private void OnTriggerExit2D(Collider2D col)
    {
        if (col.CompareTag("Ground"))
            onGround = false;
    }

    public void FixedUpdate()
    {
        horizontal = Input.GetAxis("Horizontal");
        float jump = Input.GetAxisRaw("Jump");
        float vertical = Input.GetAxisRaw("Vertical");

        Vector2 movement = new Vector2(horizontal * moveSpeed, rb.velocity.y);

        if (horizontal > 0)
            transform.localScale = new Vector3(-1, 1, 1);
        else if (horizontal < 0)
            transform.localScale = new Vector3(1, 1, 1);

        if(vertical > .1f || jump > .1f)
        {
            if (onGround)
                movement.y = jumpForce;
        }

        rb.velocity = movement;
    }

    private void Update()
    {
        hit = Input.GetMouseButtonDown(0);
        place = Input.GetMouseButton(1);

        // 不超出范围的才可以进行放置破坏操作
        if (Vector2.Distance(transform.position, mousePos) <= playerRange)
        {
            if (hit)
                terrianGenerator.RemoveTile(mousePos.x, mousePos.y);
            else if (place && Vector2.Distance(transform.position, mousePos) > 1f)
                terrianGenerator.CheckTile(selectedTile, mousePos.x, mousePos.y, false);
        }

        mousePos.x = Mathf.RoundToInt(Camera.main.ScreenToWorldPoint(Input.mousePosition).x - 0.5f);
        mousePos.y = Mathf.RoundToInt(Camera.main.ScreenToWorldPoint(Input.mousePosition).y - 0.5f);

        anim.SetFloat("horizontal", horizontal);
        anim.SetBool("hit", hit);
    }
}
