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
            lock (CallbackQueue)
            {
                while (CallbackQueue.Count > 0)
                {
                    try
                    {
                        CallbackQueue.Dequeue().Invoke();
                    }
                    catch (Exception e)
                    {
                        YargLogger.LogException(e, "Failed to run main thread callbacks");
                    }
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