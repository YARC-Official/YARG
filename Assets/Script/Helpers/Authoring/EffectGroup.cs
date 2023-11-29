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
            _effectParticles.ForEach(i => i.SetColor(c));
            _effectLights.ForEach(i => i.SetColor(c));
        }

        public void Play()
        {
            _effectParticles.ForEach(i => i.Play());
            _effectLights.ForEach(i => i.Play());
        }

        public void Stop()
        {
            _effectParticles.ForEach(i => i.Stop());
            _effectLights.ForEach(i => i.Stop());
        }
    }
}