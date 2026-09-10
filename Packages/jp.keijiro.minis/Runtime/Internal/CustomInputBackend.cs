using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Utilities;

namespace Minis
{
    // Borrowed from https://github.com/TheNathannator/HIDrogen
    internal abstract class CustomInputBackend<TBackendDevice> : IDisposable
        where TBackendDevice : class
    {
        // Safety limit, to avoid allocating too much on the stack
        // (InputSystem.StateEventBuffer.kMaxSize)
        protected const int kMaxStateSize = 512;

        // Available devices by InputSystem device ID
        private readonly Dictionary<InputDevice, TBackendDevice> m_DeviceLookup
            = new Dictionary<InputDevice, TBackendDevice>();

        // Queue for devices; they must be managed on the main thread
        private readonly ConcurrentBag<(InputDeviceDescription description, IDisposable context)> m_AdditionQueue
            = new ConcurrentBag<(InputDeviceDescription, IDisposable)>();

        // We use a custom buffering implementation because the built-in implementation is
        // not friendly to managed threads, despite what the docs for InputSystem.QueueEvent/QueueStateEvent
        // may claim, so we need to flush events on the main thread.
        private readonly SlimEventBuffer[] m_InputBuffers = new SlimEventBuffer[2];
        private volatile int m_CurrentBuffer = 0;

        protected unsafe CustomInputBackend()
        {
            for (int i = 0; i < m_InputBuffers.Length; i++)
            {
                m_InputBuffers[i] = new SlimEventBuffer();
            }

            InputSystem.onBeforeUpdate += Update;
            InputSystem.onDeviceChange += OnDeviceChange;
            InputSystem.onDeviceCommand += DeviceCommand;
        }

        ~CustomInputBackend()
        {
            Debug.LogError($"[Minis] Input backend {GetType()} was not disposed correctly! " +
                "Input system resources cannot safely be reclaimed on the finalizer thread.");
        }

        public unsafe void Dispose()
        {
            InputSystem.onBeforeUpdate -= Update;
            InputSystem.onDeviceChange -= OnDeviceChange;
            InputSystem.onDeviceCommand -= DeviceCommand;

            foreach (var pair in m_DeviceLookup)
            {
                OnDeviceRemoved(pair.Value);
                InputSystem.RemoveDevice(pair.Key);
            }
            m_DeviceLookup.Clear();

            OnDispose();

            foreach (var buffer in m_InputBuffers)
            {
                buffer.Dispose();
            }

            GC.SuppressFinalize(this);
        }

        protected abstract void OnDispose();

        protected virtual void OnUpdate() { }

        protected abstract TBackendDevice OnDeviceAdded(InputDevice device, IDisposable context);
        protected abstract void OnDeviceRemoved(TBackendDevice device);

        protected virtual unsafe long? OnDeviceCommand(TBackendDevice device, InputDeviceCommand* command) => null;

        private void Update()
        {
            while (!m_AdditionQueue.IsEmpty)
            {
                if (m_AdditionQueue.TryTake(out var context))
                    AddDevice(context.description, context.context);
            }

            OnUpdate();
            FlushEventBuffer();
        }

        public void QueueDeviceAdd(InputDeviceDescription description, IDisposable context)
            => m_AdditionQueue.Add((description, context));

        public void QueueDeviceRemove(InputDevice device)
        {
            var removeEvent = DeviceRemoveEvent.Create(device.deviceId);
            QueueEvent(ref removeEvent);
        }

        private void OnDeviceChange(InputDevice device, InputDeviceChange change)
        {
            if (change == InputDeviceChange.Removed)
            {
                if (!m_DeviceLookup.TryGetValue(device, out var backendDevice))
                    return;

                OnDeviceRemoved(backendDevice);
                m_DeviceLookup.Remove(device);
            }
        }

        private void AddDevice(InputDeviceDescription description, IDisposable context)
        {
            using (context)
            {
                // The input system will throw if a device layout can't be found
                InputDevice device;
                try
                {
                    device = InputSystem.AddDevice(description);
                }
                catch (ArgumentException)
                {
                    // Ignore layout-not-found exception
                    return;
                }
                catch (Exception ex)
                {
                    Debug.LogError("[Minis] Failed to add device to the input system!");
                    Debug.LogException(ex);
                    return;
                }

                try
                {
                    var backendDevice = OnDeviceAdded(device, context);
                    m_DeviceLookup.Add(device, backendDevice);
                }
                catch (Exception ex)
                {
                    InputSystem.RemoveDevice(device);
                    Debug.LogError("[Minis] Error in device added callback!");
                    Debug.LogException(ex);
                    return;
                }
            }
        }

        private void FlushEventBuffer()
        {
            SlimEventBuffer buffer;
            lock (m_InputBuffers)
            {
                buffer = m_InputBuffers[m_CurrentBuffer];
                m_CurrentBuffer = (m_CurrentBuffer + 1) % m_InputBuffers.Length;
            }

            foreach (var eventPtr in buffer)
            {
                try
                {
                    InputSystem.QueueEvent(eventPtr);
                }
                catch (Exception ex)
                {
                    Debug.LogError("[Minis] Error when flushing an event!");
                    Debug.LogException(ex);
                }
            }
            buffer.Reset();
        }

        public unsafe void QueueEvent(InputEventPtr eventPtr)
        {
            lock (m_InputBuffers)
            {
                m_InputBuffers[m_CurrentBuffer].AppendEvent(eventPtr);
            }
        }

        public unsafe void QueueEvent<TEvent>(ref TEvent inputEvent)
            where TEvent : struct, IInputEventTypeInfo
        {
            QueueEvent((InputEvent*)UnsafeUtility.AddressOf(ref inputEvent));
        }

        public unsafe void QueueStateEvent<TState>(InputDevice device, TState state)
            where TState : unmanaged, IInputStateTypeInfo
        {
            QueueStateEvent(device, state.format, &state, sizeof(TState));
        }

        public unsafe void QueueStateEvent(InputDevice device, FourCC format, byte[] stateBuffer)
        {
            fixed (byte* ptr = stateBuffer)
            {
                QueueStateEvent(device, format, ptr, stateBuffer.Length);
            }
        }

        // Based on InputSystem.QueueStateEvent<T>
        public unsafe void QueueStateEvent(InputDevice device, FourCC format, void* stateBuffer, int stateLength)
        {
            if (stateBuffer == null || stateLength < 1 || stateLength > kMaxStateSize)
                return;

            // Create state buffer
            int eventSize = stateLength + (sizeof(StateEvent) - 1); // StateEvent already includes 1 byte at the end
            byte* _stateEvent = stackalloc byte[eventSize];
            StateEvent* stateEvent = (StateEvent*)_stateEvent;
            *stateEvent = new StateEvent
            {
                baseEvent = new InputEvent(StateEvent.Type, eventSize, device.deviceId),
                stateFormat = format
            };

            // Copy state data
            UnsafeUtility.MemCpy(stateEvent->state, stateBuffer, stateLength);

            // Queue state event
            QueueEvent((InputEvent*)stateEvent);
        }

        private unsafe long? DeviceCommand(InputDevice device, InputDeviceCommand* command)
        {
            if (device == null)
                return null;
            if (command == null)
                return InputDeviceCommand.GenericFailure;

            if (!m_DeviceLookup.TryGetValue(device, out var backendDevice))
                return null;

            return OnDeviceCommand(backendDevice, command);
        }
    }
}