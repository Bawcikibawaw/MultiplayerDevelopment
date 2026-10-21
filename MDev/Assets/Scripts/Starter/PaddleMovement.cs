using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class PaddleMovement : MonoBehaviour
{
    [Tooltip("0 = LEFT (W/S), 1 = RIGHT (Up/Down Arrows)")]
    public int side = 0;
    public float speed = 8f;

    private void Update()
    {
        float axis = 0f;

        if (side == 0)
        {
            if (IsKeyHeld(KeyCode.W)) axis += 1f;
            if (IsKeyHeld(KeyCode.S)) axis -= 1f;
        }
        else
        {
            if (IsKeyHeld(KeyCode.UpArrow)) axis += 1f;
            if (IsKeyHeld(KeyCode.DownArrow)) axis -= 1f;
        }

        transform.position += Vector3.forward * axis * speed * Time.deltaTime;
        Court.ClampPaddle(transform);
    }

    private static bool IsKeyHeld(KeyCode key)
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            switch (key)
            {
                case KeyCode.W: return Keyboard.current.wKey.isPressed;
                case KeyCode.S: return Keyboard.current.sKey.isPressed;
                case KeyCode.UpArrow: return Keyboard.current.upArrowKey.isPressed;
                case KeyCode.DownArrow: return Keyboard.current.downArrowKey.isPressed;
            }
        }
#endif
        return Input.GetKey(key);
    }
}
