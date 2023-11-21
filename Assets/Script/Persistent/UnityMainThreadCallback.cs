using System;
using System.Collections.Generic;
using UnityEngine;

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
                        Debug.LogError("Exception thrown while running main thread callbacks. " +
                            "See error below for more details.");
                        Debug.LogException(e);
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