using UnityEngine;

public class LocalBall : MonoBehaviour
{
    public float speed = 9f;
    public Transform leftPaddle;
    public Transform rightPaddle;

    private Vector3 _velocity;
    private int _leftScore;
    private int _rightScore;

    private void Start()
    {
        Launch(1);
    }

    private void Launch(int towardSide)
    {
        transform.position = new Vector3(0f, Court.BallY, 0f);
        float dirX = towardSide == 0 ? -1f : 1f;
        float dirZ = Random.Range(-0.45f, 0.45f);
        _velocity = new Vector3(dirX, 0f, dirZ).normalized * speed;
    }

    private void Update()
    {
        Vector3 pos = transform.position + _velocity * Time.deltaTime;

        float wallZ = Court.HalfHeight - Court.BallRadius;
        if (pos.z > wallZ)
        {
            pos.z = wallZ;
            _velocity.z = -Mathf.Abs(_velocity.z);
        }
        else if (pos.z < -wallZ)
        {
            pos.z = -wallZ;
            _velocity.z = Mathf.Abs(_velocity.z);
        }

        if (_velocity.x < 0f && leftPaddle != null)
        {
            if (pos.x - Court.BallRadius <= -Court.PaddleX && pos.x >= -Court.PaddleX - 0.6f)
            {
                float dz = pos.z - leftPaddle.position.z;
                if (Mathf.Abs(dz) <= Court.PaddleHalfLength + Court.BallRadius)
                {
                    pos.x = -Court.PaddleX + Court.BallRadius;
                    float bounceZ = Mathf.Clamp(dz / Court.PaddleHalfLength, -0.8f, 0.8f);
                    _velocity = new Vector3(1f, 0f, bounceZ).normalized * speed;
                }
            }
        }
        else if (_velocity.x > 0f && rightPaddle != null)
        {
            if (pos.x + Court.BallRadius >= Court.PaddleX && pos.x <= Court.PaddleX + 0.6f)
            {
                float dz = pos.z - rightPaddle.position.z;
                if (Mathf.Abs(dz) <= Court.PaddleHalfLength + Court.BallRadius)
                {
                    pos.x = Court.PaddleX - Court.BallRadius;
                    float bounceZ = Mathf.Clamp(dz / Court.PaddleHalfLength, -0.8f, 0.8f);
                    _velocity = new Vector3(-1f, 0f, bounceZ).normalized * speed;
                }
            }
        }

        transform.position = pos;

        if (pos.x < -Court.HalfWidth)
        {
            _rightScore++;
            Debug.Log($"[Local] RIGHT scored. {_leftScore} : {_rightScore}");
            Launch(0);
        }
        else if (pos.x > Court.HalfWidth)
        {
            _leftScore++;
            Debug.Log($"[Local] LEFT scored. {_leftScore} : {_rightScore}");
            Launch(1);
        }
    }
}
