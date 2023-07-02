using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using YARG.Chart;
using YARG.Data;
using YARG.Input;
using YARG.Pools;
using YARG.Settings;
using YARG.Util;

namespace YARG.PlayMode
{
    public sealed class DrumsTrack : AbstractTrack
    {
        private InputStrategy input;

        [Space]
        [SerializeField]
        private bool fiveLaneMode = false;

        private int kickIndex = 4;

        [Space]
        [SerializeField]
        private Fret[] drums;

        [SerializeField]
        private NotePool notePool;

        [SerializeField]
        private ParticleGroup kickNoteParticles;

        [SerializeField]
        private MeshRenderer kickFretInside;

        [SerializeField]
        private Animation kickFretAnimation;

        [SerializeField]
        public bool shakeOnKick = true;

        private Queue<List<NoteInfo>> expectedHits = new();

        private int fillIndex = 0;
        private int fillVisualIndex = 0;
        private List<EventInfo> fillSections = new();
        public EventInfo CurrentFill => fillIndex < fillSections.Count ? fillSections[fillIndex] : null;

        public EventInfo CurrentVisualFill =>
            fillVisualIndex < fillSections.Count ? fillSections[fillVisualIndex] : null;

        private readonly string[] proInst =
        {
            "realDrums", "ghDrums"
        };

        private int ptsPerNote;

        protected override void StartTrack()
        {
            notePool.player = player;
            genericPool.player = player;

            // Inputs

            input = player.inputStrategy;
            input.ResetForSong();

            if (input is DrumsInputStrategy drumStrat)
            {
                drumStrat.DrumHitEvent += DrumHitAction;
            }
            else if (input is GHDrumsInputStrategy ghStrat)
            {
                ghStrat.DrumHitEvent += GHDrumHitAction;
            }

            if (input.BotMode)
            {
                input.InitializeBotMode(Chart);
            }

            // GH vs RB

            kickIndex = fiveLaneMode ? 5 : 4;

            // Lefty flip

            if (player.leftyFlip)
            {
                drums = drums.Reverse().ToArray();
                // Make the drum colors follow the original order even though the chart is flipped
                Array.Reverse(commonTrack.colorMappings, 0, kickIndex);
            }

            // Color drums
            for (int i = 0; i < drums.Length; i++)
            {
                var fret = drums[i].GetComponent<Fret>();
                fret.SetColor(commonTrack.FretColor(i), commonTrack.FretInnerColor(i), commonTrack.SustainColor(i));
                drums[i] = fret;
            }

            kickNoteParticles.Colorize(commonTrack.FretColor(kickIndex));

            // Color Kick Frets
            kickFretInside.material.color = (commonTrack.FretColor(kickIndex));
            kickFretInside.material.SetColor("_EmissionColor", commonTrack.FretColor(kickIndex) * 2);

            // initialize scoring variables
            ptsPerNote = proInst.Contains(player.chosenInstrument) ? 60 : 50;
            starsKeeper = new(Chart, scoreKeeper,
                player.chosenInstrument,
                ptsPerNote);

            // Queue up events
            string fillName = $"fill_{player.chosenInstrument}";
            foreach (var eventInfo in Play.Instance.chart.events)
            {
                if (eventInfo.name == fillName)
                {
                    fillSections.Add(eventInfo);
                }
            }
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            // Unbind input
            if (input is DrumsInputStrategy drumStrat)
            {
                drumStrat.DrumHitEvent -= DrumHitAction;
            }
            else if (input is GHDrumsInputStrategy ghStrat)
            {
                ghStrat.DrumHitEvent -= GHDrumHitAction;
            }
        }

        protected override void UpdateTrack()
        {
            // Ignore everything else until the song starts
            if (!Play.Instance.SongStarted)
            {
                return;
            }

            // Since chart is sorted, this is guaranteed to work
            while (Chart.Count > visualChartIndex && Chart[visualChartIndex].time <= TrackStartTime)
            {
                var noteInfo = Chart[visualChartIndex];
                var chosenActivatorType = 0;
                NoteInfo chosenActivatorNote = null;

                // Check three notes before and after the current note to ensure none of the notes in the chord are skipped.
                for (int i = -3; i < 3; i++)
                {
                    // Prevent out-of-bounds access at the beginning or end of a chart
                    if (Chart.Count <= visualChartIndex + i || visualChartIndex <= 3)
                    {
                        break;
                    }

                    var chordNote = Chart[visualChartIndex + i];
                    if (chordNote.time == noteInfo.time)
                    {
                        // Cymbals always take priority.
                        if (chordNote.hopo)
                        {
                            chosenActivatorType = 3;
                            chosenActivatorNote = chordNote;
                            break;
                        }

                        // If there are no cymbals on this beat, pads are second.
                        if (chordNote.fret != 4)
                        {
                            chosenActivatorType = 2;
                            chosenActivatorNote = chordNote;
                            continue;
                        }

                        // Finally, if there's nothing else, kick notes must be used. 
                        if (chosenActivatorType < 2)
                        {
                            chosenActivatorType = 1;
                            chosenActivatorNote = chordNote;
                            continue;
                        }
                    }
                }

                // Skip kick notes if noKickMode is enabled
                if (noteInfo.fret == kickIndex && SettingsManager.Settings.NoKicks.Data)
                {
                    visualChartIndex++;
                    continue;
                }

                if (CurrentVisualFill?.EndTime == noteInfo.time && starpowerCharge >= 0.5f && !IsStarPowerActive)
                {
                    if (chosenActivatorNote != null)
                    {
                        chosenActivatorNote.isActivator = true;
                    }
                }

                SpawnNote(noteInfo, TrackStartTime);
                visualChartIndex++;
            }

            // Clear out passed fill sections
            while (CurrentFill?.EndTime < HitMarginEndTime)
            {
                fillIndex++;
            }

            while (CurrentVisualFill?.EndTime < TrackStartTime)
            {
                fillVisualIndex++;
            }

            // Update expected input
            while (Chart.Count > inputChartIndex && Chart[inputChartIndex].time <= HitMarginStartTime)
            {
                var noteInfo = Chart[inputChartIndex];

                // Skip kick notes if noKickMode is enabled
                if (noteInfo.fret == kickIndex && SettingsManager.Settings.NoKicks.Data)
                {
                    inputChartIndex++;
                    continue;
                }

                var peeked = expectedHits.ReversePeekOrNull();
                if (peeked?[0].time == noteInfo.time)
                {
                    // Add notes as chords
                    peeked.Add(noteInfo);
                }
                else
                {
                    // Or add notes as singular
                    var l = new List<NoteInfo>(6)
                    {
                        noteInfo
                    };
                    expectedHits.Enqueue(l);
                }

                inputChartIndex++;
            }

            UpdateInput();
        }

        public override void SetReverb(bool on)
        {
            Play.Instance.ReverbAudio("drums", on);
            Play.Instance.ReverbAudio("drums_1", on);
            Play.Instance.ReverbAudio("drums_2", on);
            Play.Instance.ReverbAudio("drums_3", on);
            Play.Instance.ReverbAudio("drums_4", on);
        }

        private void UpdateInput()
        {
            // Ignore inputs until the first note enters the hit window
            if (!CurrentlyInChart)
            {
                return;
            }

            // Handle misses (multiple a frame in case of lag)
            while (HitMarginEndTime > expectedHits.PeekOrNull()?[0].time)
            {
                var missedChord = expectedHits.Dequeue();

                // Call miss for each component
                foreach (var hit in missedChord)
                {
                    hitChartIndex++;

                    // The player should not be penalized for missing activator notes
                    if (hit.isActivator)
                    {
                        continue;
                    }

                    Combo = 0;
                    missedAnyNote = true;
                    notePool.MissNote(hit);
                    StopAudio = true;
                }
            }
        }

        protected override void PauseToggled(bool pause)
        {
            if (!pause)
            {
                if (input is DrumsInputStrategy drumStrat)
                {
                    drumStrat.DrumHitEvent += DrumHitAction;
                }
                else if (input is GHDrumsInputStrategy ghStrat)
                {
                    ghStrat.DrumHitEvent += GHDrumHitAction;
                }
            }
            else
            {
                if (input is DrumsInputStrategy drumStrat)
                {
                    drumStrat.DrumHitEvent -= DrumHitAction;
                }
                else if (input is GHDrumsInputStrategy ghStrat)
                {
                    ghStrat.DrumHitEvent -= GHDrumHitAction;
                }
            }
        }

        private void GHDrumHitAction(int drum)
        {
            DrumHitAction(drum, false);
        }

        private void DrumHitAction(int drum, bool cymbal)
        {
            // invert input in case lefty flip is on, bots don't need it
            if (player.leftyFlip && !input.BotMode)
            {
                switch (drum)
                {
                    case 0:
                        drum = kickIndex == 4 ? 3 : 4;
                        break;
                    case 1:
                        drum = kickIndex == 4 ? 2 : 3;
                        break;
                    case 2:
                        drum = kickIndex == 4 ? 1 : 2;
                        break;
                    case 3:
                        // lefty flip on pro drums means physically moving the green cymbal above the red snare
                        // so while the position on the chart has changed, the input object is the same
                        if (!cymbal)
                        {
                            drum = kickIndex == 4 ? 0 : 1;
                        }

                        break;
                    case 4:
                        if (kickIndex == 5)
                        {
                            drum = 0;
                        }

                        break;
                }
            }

            if (drum != kickIndex)
            {
                // Hit effect
                drums[drum].PlayAnimationDrums();
                drums[drum].Pulse();
            }
            else
            {
                PlayKickFretAnimation();
                // Only play kick flash/shake now when outside of the chart,
                // otherwise only play it when actually hitting a kick
                if (Chart.Count < 1 || CurrentTime < Chart[0].time || CurrentTime >= Chart[^1].time)
                {
                    commonTrack.kickFlash.PlayAnimation();
                    if (shakeOnKick && SettingsManager.Settings.KickBounce.Data)
                    {
                        trackAnims.PlayKickShakeCameraAnim();
                    }
                }
            }

            // Ignore inputs until the first note enters the hit window
            if (!CurrentlyInChart)
            {
                return;
            }

            // Overstrum if no expected
            if (expectedHits.Count <= 0)
            {
                Combo = 0;

                return;
            }

            // Handle hits (one per frame so no double hits)
            var notes = expectedHits.Peek();

            // Check if a drum was hit
            NoteInfo hit = null;
            foreach (var note in notes)
            {
                // Check if correct cymbal was hit
                bool cymbalHit = note.hopo == cymbal;
                if (player.chosenInstrument == "drums")
                {
                    cymbalHit = true;
                }

                // Check if correct drum was hit
                if (note.fret == drum && cymbalHit)
                {
                    hit = note;
                    if (note.isActivator)
                    {
                        (input as DrumsInputStrategy).ActivateStarpower();
                    }

                    // Play kick flash/shake
                    if (note.fret == kickIndex)
                    {
                        commonTrack.kickFlash.PlayAnimation();
                        if (shakeOnKick && SettingsManager.Settings.KickBounce.Data)
                        {
                            trackAnims.PlayKickShakeCameraAnim();
                        }
                    }

                    break;
                }
            }

            // "Overstrum" (or overhit in this case)
            if (hit == null)
            {
                Combo = 0;

                return;
            }

            // If so, hit! (Remove from "chord")
            // bool lastNote = false;
            notes.RemoveAll(i => i.fret == drum);
            if (notes.Count <= 0)
            {
                //lastNote = true;  //  <-- This comment (disable) on the line is a solution for drum notes stop being counted as "chords" and being clumped together, which shouldn't happen. -Mia
                expectedHits.Dequeue();
            }

            // Activators should not affect combo
            if (!hit.isActivator)
            {
                Combo++;
            }

            // Hit note
            hitChartIndex++;
            notePool.HitNote(hit);
            StopAudio = false;

            // Solo stuff
            if (soloInProgress)
            {
                soloNotesHit++;
            }

            // Play particles
            if (hit.fret != kickIndex)
            {
                drums[hit.fret].PlayParticles();
            }
            else
            {
                kickNoteParticles.Stop();
                kickNoteParticles.Play();
            }

            // Add stats
            notesHit++;
            // TODO: accomodate for disabled cymbal lanes, rework 5-lane scoring depending on re-charting
            scoreKeeper.Add(Multiplier * ptsPerNote);
        }

        private void SpawnNote(NoteInfo noteInfo, float time)
        {
            // Set correct position
            float lagCompensation = CalcLagCompensation(time, noteInfo.time);
            float x = noteInfo.fret == kickIndex ? 0f : drums[noteInfo.fret].transform.localPosition.x;
            var pos = new Vector3(x, 0f, TRACK_SPAWN_OFFSET - lagCompensation);

            // Get model type
            var model = NoteComponent.ModelType.NOTE;
            if (noteInfo.fret == kickIndex)
            {
                // Kick
                model = NoteComponent.ModelType.FULL;
            }
            else if (player.chosenInstrument == "ghDrums" &&
                SettingsManager.Settings.UseCymbalModelsInFiveLane.Data)
            {
                if (noteInfo.fret == 1 || noteInfo.fret == 3)
                {
                    // Cymbal (only for gh-drums if enabled)
                    model = NoteComponent.ModelType.HOPO;
                }
            }
            else
            {
                if (noteInfo.hopo && player.chosenInstrument == "realDrums")
                {
                    // Cymbal (only for pro-drums)
                    model = NoteComponent.ModelType.HOPO;
                }
            }

            // Set note info
            var noteComp = notePool.AddNote(noteInfo, pos);
            startFCDetection = true;
            noteComp.SetInfo(
                noteInfo,
                commonTrack.NoteColor(noteInfo.fret),
                commonTrack.SustainColor(noteInfo.fret),
                noteInfo.length,
                model,
                noteInfo.time >= CurrentVisualStarpower?.time && noteInfo.time < CurrentVisualStarpower?.EndTime,
                noteInfo.isActivator
            );
        }

        private void PlayKickFretAnimation()
        {
            StopKickFretAnimation();

            kickFretAnimation["KickFrets"].wrapMode = WrapMode.Once;
            kickFretAnimation.Play("KickFrets");
        }

        private void StopKickFretAnimation()
        {
            kickFretAnimation.Stop();
            kickFretAnimation.Rewind();
        }
    }
}