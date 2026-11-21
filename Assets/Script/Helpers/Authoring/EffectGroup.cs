using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace YARG.Helpers.Authoring
{
    // WARNING: Changing this could break themes or venues!
    //
    // This script is used a lot in theme creation.
    // Changing the serialized fields in this file will result in older themes
    // not working properly. Only change if you need to.

    public class EffectGroup : MonoBehaviour
    {
        public List<EffectParticle> EffectParticles { get; private set; }
        public List<EffectLight>    EffectLights    { get; private set; }

        private void Awake()
        {
            EffectParticles = GetComponentsInChildren<EffectParticle>().ToList();
            EffectLights = GetComponentsInChildren<EffectLight>().ToList();
        }

        public void SetColor(Color c)
        {
            foreach (var particles in EffectParticles)
                particles.InitializeColor(c);
            foreach (var lights in EffectLights)
                lights.InitializeColor(c);
        }

        public void Play()
        {
            foreach (var particles in EffectParticles)
                particles.Play();
            foreach (var lights in EffectLights)
                lights.Play();
        }

        public void Stop()
        {
            foreach (var particles in EffectParticles)
                particles.Stop();
            foreach (var lights in EffectLights)
                lights.Stop();
        }

        public void SetBreMode(bool breMode)
        {
            foreach (var particles in EffectParticles)
            {
                particles.SetBreMode(breMode);
            }
        }

        // See EffectParticle.ZeroStartDelay - applies it to every particle in this group.
        public void ZeroStartDelay()
        {
            foreach (var particles in EffectParticles)
                particles.ZeroStartDelay();
        }

        // See EffectParticle.FastForward - applies it to every particle in this group.
        public void FastForward(float seconds)
        {
            foreach (var particles in EffectParticles)
                particles.FastForward(seconds);
        }

        // See EffectParticle.StopImmediate - applies it to every particle in this group.
        public void StopImmediate()
        {
            foreach (var particles in EffectParticles)
                particles.StopImmediate();
            foreach (var lights in EffectLights)
                lights.Stop();
        }
    }
}