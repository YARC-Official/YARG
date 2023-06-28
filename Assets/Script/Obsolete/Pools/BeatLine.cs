using UnityEngine;

namespace YARG.Pools
{
    public class BeatLine : Poolable
    {
        private void Update()
        {
            transform.localPosition -= new Vector3(0f, 0f, Time.deltaTime * pool.player.trackSpeed);

            if (transform.localPosition.z < -3f)
            {
                MoveToPool();
            }
        }
    }
}