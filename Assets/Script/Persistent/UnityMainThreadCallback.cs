using System;
using System.Collections.Generic;
using UnityEngine;
using YARG.Core.Logging;

namespace YARG
{
    public class UnityMainThreadCallback : MonoBehaviour
    {
        private static readonly Queue<Action> CallbackQueue = new();

        private void Update()
        {
            while (true)
            {
                Action action;
                lock (CallbackQueue)
                {
                    if (CallbackQueue.Count == 0)
                    {
                        return;
                    }
                    action = CallbackQueue.Dequeue();
                }

                try
                {
                    action.Invoke();
                }
                catch (Exception e)
                {
                    YargLogger.LogException(e, "Failed to run main thread callbacks");
                }
            }
        }

        public static void QueueEvent(Action action)
        {
            lock (CallbackQueue)
            {
                CallbackQueue.Enqueue(action);
            }
        }
    }
}
