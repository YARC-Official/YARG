using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using YARG.Core.Chart;
using YARG.Core.Engine;
using YARG.Core.Extensions;
using YARG.Helpers.Extensions;

namespace YARG.Venue
{
    public sealed class CameraChannel : IVenueChannel
    {
        private readonly Animator         _animator;
        private readonly VenueHashLibrary _hashes;
        private readonly int              _leadingFrames;

        private static readonly List<string> OnlyClose = new()
        {
            "BehindNoDrum", "NearNoDrum", "Guitar", "GuitarBehind", "GuitarCloseup", "DrumsBehind", "DrumsCloseupHand",
            "DrumsCloseupHead", "Bass", "BassBehind", "BassCloseup", "BassCloseupHead", "Vocals", "VocalsCloseup",
            "VocalsBehind", "Keys", "KeysBehind", "KeysCloseupHand", "KeysCloseupHead", "DrumsVocals", "BassDrums",
            "DrumsGuitar", "BassVocalsBehind", "BassVocals", "GuitarVocalsBehind", "GuitarVocals", "KeysVocalsBehind",
            "KeysVocals", "BassGuitarBehind", "BassGuitar", "BassKeysBehind", "BassKeys", "GuitarKeysBehind", "GuitarKeys"
        };

        private static readonly List<string> NoClose = new()
        {
            "Crowd", "Stage", "AllBehind", "AllFar", "AllNear", "BehindNoDrum", "NearNoDrum", "Guitar", "GuitarBehind",
            "DrumsBehind", "Bass", "BassBehind", "Vocals", "VocalsBehind", "Keys", "KeysBehind", "DrumsVocals",
            "BassDrums", "DrumsGuitar", "BassVocalsBehind", "BassVocals", "GuitarVocalsBehind", "GuitarVocals",
            "KeysVocalsBehind", "KeysVocals", "BassGuitarBehind", "BassGuitar", "BassKeysBehind", "BassKeys",
            "GuitarKeysBehind", "GuitarKeys"
        };

        private static readonly List<string> OnlyFar  = new() { "Crowd", "Stage", "AllFar" };
        private static readonly List<string> NoBehind = new()
        {
            "Stage", "AllBehind", "AllFar", "AllNear", "NearNoDrum", "Guitar", "GuitarCloseup", "DrumsCloseupHand",
            "DrumsCloseupHead", "Bass", "BassCloseup", "BassCloseupHead", "Vocals", "VocalsCloseup", "VocalsBehind",
            "Keys", "KeysBehind", "KeysCloseupHand", "KeysCloseupHead", "DrumsVocals", "BassDrums", "DrumsGuitar",
            "BassVocals", "GuitarVocals", "KeysVocals", "BassGuitar", "BassKeys", "GuitarKeys"
        };

        public CameraChannel(Animator animator, VenueHashLibrary hashes, int leadingFrames)
        {
            _animator = animator;
            _hashes = hashes;
            _leadingFrames = leadingFrames;
        }

        public void BuildCommands(SongChart chart, AnimatorCommandQueue queue)
        {
            var subjects = Enum.GetValues(typeof(CameraCutEvent.CameraCutSubject))
                .Cast<CameraCutEvent.CameraCutSubject>()
                .ToList();
            subjects.Remove(CameraCutEvent.CameraCutSubject.Random);

            foreach (var cam in chart.VenueTrack.CameraCuts)
            {
                double t = cam.Time - _leadingFrames / 60.0;

                CameraCutEvent.CameraCutSubject subject;
                if (cam.Subject == CameraCutEvent.CameraCutSubject.Random)
                {
                    if (cam.RandomChoices.Count > 0)
                    {
                        subject = cam.RandomChoices.Pick();
                    }
                    else
                    {
                        subject = GetRandomSubject(cam.Constraint, subjects);
                    }
                }
                else
                {
                    subject = cam.Subject;
                }

                queue.Add(AnimatorCommand.Randomize(t, _animator));
                int hash = _hashes.CameraSubjectHashes[(int) subject];
                queue.Add(AnimatorCommand.Trigger(t, _animator, hash));
            }
        }

        private CameraCutEvent.CameraCutSubject GetRandomSubject(CameraCutEvent.CameraCutConstraint constraint,
            List<CameraCutEvent.CameraCutSubject> subjects)
        {
            var filtered = subjects.AsEnumerable();

            if (constraint.HasFlag(CameraCutEvent.CameraCutConstraint.OnlyClose))
                filtered = filtered.Where(s => OnlyClose.Contains(s.ToString()));
            if (constraint.HasFlag(CameraCutEvent.CameraCutConstraint.OnlyFar))
                filtered = filtered.Where(s => OnlyFar.Contains(s.ToString()));
            if (constraint.HasFlag(CameraCutEvent.CameraCutConstraint.NoClose))
                filtered = filtered.Where(s => NoClose.Contains(s.ToString()));
            if (constraint.HasFlag(CameraCutEvent.CameraCutConstraint.NoBehind))
                filtered = filtered.Where(s => NoBehind.Contains(s.ToString()));

            var list = filtered.ToList();
            if (list.Count == 0) return CameraCutEvent.CameraCutSubject.AllNear;

            return list[UnityEngine.Random.Range(0, list.Count)];
        }

        public void Update(double visualTime)
        {
        }

        public void Initialize(EngineManager manager)
        {
        }
    }
}