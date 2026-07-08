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

            // Handle case where chart has no camera cuts
            if (chart.VenueTrack.CameraCuts.Count == 0)
            {
                // Switch to a random subject every 3-6 seconds for the duration of the chart, starting at the first note
                // At the last note, switch to the stage subject
                var firstNote = chart.GetFirstNoteStartTime();
                var lastNote = chart.GetLastNoteEndTime();

                var beatlines = chart.SyncTrack.Beatlines;

                var subject = GetRandomSubject(CameraCutEvent.CameraCutConstraint.None, subjects);
                queue.Add(AnimatorCommand.Randomize(firstNote, _animator));
                queue.Add(AnimatorCommand.Trigger(firstNote, _animator, _hashes.CameraSubjectHashes[(int) subject]));

                // Generate a list of times
                var beatIndex = 0;
                var times = new List<double>();
                var time = firstNote;
                while (time < lastNote)
                {
                    time += UnityEngine.Random.Range(3, 6);
                    // Use the next beatline as the actual time (unless there is no next beatline)
                    double nextBeatTime = 0;
                    while (time < beatlines[beatIndex].Time)
                    {
                        if (beatIndex >= beatlines.Count)
                        {
                            break;
                        }
                        nextBeatTime = beatlines[beatIndex].Time;
                        beatIndex++;
                    }

                    if (time <= nextBeatTime)
                    {
                        time = nextBeatTime;
                    }

                    times.Add(time - _leadingFrames / 60.0f);
                }

                // Switch at each time
                foreach (var t in times)
                {
                    subject = GetRandomSubject(CameraCutEvent.CameraCutConstraint.None, subjects);
                    queue.Add(AnimatorCommand.Randomize(t, _animator));
                    queue.Add(AnimatorCommand.Trigger(t, _animator, _hashes.CameraSubjectHashes[(int) subject]));
                }

                // Final switch
                queue.Add(AnimatorCommand.Randomize(lastNote, _animator));
                queue.Add(AnimatorCommand.Trigger(lastNote, _animator,
                    _hashes.CameraSubjectHashes[(int) CameraCutEvent.CameraCutSubject.Stage]));

                return;
            }

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