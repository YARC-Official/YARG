using UnityEngine;
using YARG.PlayMode;

namespace YARG.Pools
{
    public class VocalBasicComponent : Poolable
    {
        private void Update()
        {
            transform.localPosition -= new Vector3(Time.deltaTime * MicPlayer.trackSpeed, 0f, 0f);

            if (transform.localPosition.x < -12f)
            {
                MoveToPool();
            }
        }
    }
}