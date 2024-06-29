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
        private List<EffectParticle> _effectParticles;
        private List<EffectLight> _effectLights;

        private void Awake()
        {
            _effectParticles = GetComponentsInChildren<EffectParticle>().ToList();
            _effectLights = GetComponentsInChildren<EffectLight>().ToList();
        }

        public void SetColor(Color c)
        {
            foreach (var particles in _effectParticles)
                particles.SetColor(c);
            foreach (var lights in _effectLights)
                lights.SetColor(c);
        }

        public void Play()
        {
            foreach (var particles in _effectParticles)
                particles.Play();
            foreach (var lights in _effectLights)
                lights.Play();
        }

        public void Stop()
        {
            foreach (var particles in _effectParticles)
                particles.Stop();
            foreach (var lights in _effectLights)
                lights.Stop();
        }
    }
}