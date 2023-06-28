using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.InputSystem;
using YARG.Input;
using YARG.Util;

namespace YARG.Serialization
{
    public static class InputBindSerializer
    {
        private class InputDeviceSave
        {
            public string inputStrategy;
            public string deviceName;
            public Dictionary<string, InputBindSave> binds = new();
        }

        private class InputBindSave
        {
            public string controlPath;
            public long debounceThreshold;
        }

        private static List<InputDeviceSave> inputBindSaves = new();

        private static string InputBindFile => Path.Combine(PathHelper.PersistentDataPath, "inputBinds.json");

        static InputBindSerializer()
        {
            // Load from JSON
            try
            {
                var json = File.ReadAllText(InputBindFile);
                inputBindSaves = JsonConvert.DeserializeObject<List<InputDeviceSave>>(json);
            }
            catch (Exception)
            {
                Debug.LogWarning("Failed to load input binds from JSON. Ignoring.");
            }
        }

        public static void LoadBindsFromSave(InputStrategy inputStrategy)
        {
            try
            {
                // Look from correct bind (using input strategy and device)
                InputDeviceSave inputBindSave = null;
                foreach (InputDeviceSave bindSave in inputBindSaves)
                {
                    if (bindSave.deviceName != inputStrategy.InputDevice.name)
                    {
                        continue;
                    }

                    if (bindSave.inputStrategy != inputStrategy.GetType().Name)
                    {
                        continue;
                    }

                    inputBindSave = bindSave;
                    break;
                }

                if (inputBindSave == null)
                {
                    return;
                }

                // Set binds
                foreach (var binding in inputStrategy.Mappings.Values)
                {
                    if (!inputBindSave.binds.ContainsKey(binding.BindingKey))
                    {
                        continue;
                    }

                    var savedBinding = inputBindSave.binds[binding.BindingKey];
                    var control = InputControlPath.TryFindControl(inputStrategy.InputDevice, savedBinding.controlPath);
                    if (control is not InputControl<float> floatControl)
                    {
                        continue;
                    }

                    binding.Control = floatControl;
                    binding.DebounceThreshold = savedBinding.debounceThreshold;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("Failed to load input binds from JSON. Ignoring.");
                Debug.LogException(e);

                if (File.Exists(InputBindFile))
                {
                    File.Delete(InputBindFile);
                }
            }
        }

        public static void SaveBindsFromInputStrategy(InputStrategy inputStrategy)
        {
            try
            {
                // Look for existing bind save
                InputDeviceSave inputBindSave = null;
                foreach (InputDeviceSave bindSave in inputBindSaves)
                {
                    if (bindSave.deviceName != inputStrategy.InputDevice.name)
                    {
                        continue;
                    }

                    if (bindSave.inputStrategy != inputStrategy.GetType().Name)
                    {
                        continue;
                    }

                    inputBindSave = bindSave;
                    break;
                }

                if (inputBindSave != null)
                {
                    inputBindSaves.Remove(inputBindSave);
                }

                // Create new bind save if none found
                inputBindSave = new InputDeviceSave
                {
                    inputStrategy = inputStrategy.GetType().Name, deviceName = inputStrategy.InputDevice.name
                };
                inputBindSaves.Add(inputBindSave);

                // Save binds
                inputBindSave.binds.Clear();
                foreach (var binding in inputStrategy.Mappings.Values)
                {
                    var control = binding.Control;
                    if (control == null)
                    {
                        continue;
                    }

                    var bindingSave = new InputBindSave
                    {
                        controlPath = control.path, debounceThreshold = binding.DebounceThreshold
                    };
                    inputBindSave.binds.Add(binding.BindingKey, bindingSave);
                }

                // Save to JSON
                SaveToJsonFile();
            }
            catch (Exception e)
            {
                Debug.LogWarning("Failed to save input binds to JSON. Ignoring.");
                Debug.LogException(e);
            }
        }

        private static void SaveToJsonFile()
        {
            var json = JsonConvert.SerializeObject(inputBindSaves);
            File.WriteAllText(Path.Combine(PathHelper.PersistentDataPath, "inputBinds.json"), json);
        }
    }
}