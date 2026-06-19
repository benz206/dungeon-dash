using UnityEngine;
using UnityEngine.InputSystem;

/// Minimal WASD movement + animation driver for the Player prefab.
/// Starter rig for the eventual port of the Godot Player.gd logic.
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    public float moveSpeed = 5f;

    Rigidbody2D _rb;
    Animator _anim;
    SpriteRenderer _sr;
    Vector2 _input;

    void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _anim = GetComponent<Animator>();
        _sr = GetComponent<SpriteRenderer>();
    }

    void Update()
    {
        var k = Keyboard.current;
        if (k == null) return;

        float x = (k.dKey.isPressed ? 1f : 0f) - (k.aKey.isPressed ? 1f : 0f);
        float y = (k.wKey.isPressed ? 1f : 0f) - (k.sKey.isPressed ? 1f : 0f);
        _input = new Vector2(x, y);
        if (_input.sqrMagnitude > 1f) _input.Normalize();

        if (_anim != null) _anim.SetFloat("Speed", _input.magnitude);
        if (_sr != null && x != 0f) _sr.flipX = x < 0f;
    }

    void FixedUpdate()
    {
        _rb.linearVelocity = _input * moveSpeed;
    }
}
