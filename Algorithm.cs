using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;

namespace MidiFlattenerGUI;

public static class Algorithm
{
    public static Task ConvertAsync(
        int targetTempoValue,
        byte beatsPerBar,
        byte noteLengthPerBeat,
        string[] midiPath)
    {
        var tasks = new Task[midiPath.Length];
        for (var i = 0; i < midiPath.Length; i++)
        {
            var path = midiPath[i];
            tasks[i] = Task.Run(() =>
                {
                    try
                    {
                        RunCore(
                            targetTempoValue,
                            beatsPerBar,
                            noteLengthPerBeat,
                            path
                        );
                    }
                    catch(Exception exception)
                    {
                        throw new MidiFlattenException(path, exception);
                    }
                }
            );
        }

        return Task.WhenAll(tasks);
    }

    public class MidiFlattenException(string midiPath, Exception innerException) : Exception(innerException.Message, innerException)
    {
        public string MidiPath { get; } = midiPath;
    }
    
    private static void RunCore(
        int targetTempoValue,
        byte numerator,
        byte denominator,
        string file)
    {
        const string extension = ".flattened.mid";
        var sourceMidiFile = MidiFile.Read(file);

        var targetTempo = Tempo.FromBeatsPerMinute(targetTempoValue);
        var dstTempoMap = TempoMap.Create(targetTempo);
        var sourceTempoMap = sourceMidiFile.GetTempoMap();
        
        var midiTrackList = new List<MidiChunk>
        {
            new TrackChunk(
                new TimeSignatureEvent(numerator, denominator),
                new SetTempoEvent(targetTempo.MicrosecondsPerQuarterNote)
            )
        };

        foreach (var sourceMidiTrack in sourceMidiFile.Chunks.OfType<TrackChunk>())
        {
            var noteTime = sourceMidiTrack
                .GetNotes()
                .Select(x => (
                    x.NoteName,
                    x.Octave,
                    x.Velocity,
                    x.OffVelocity,
                    Time: x.TimeAs<MetricTimeSpan>(sourceTempoMap),
                    Length: x.LengthAs<MetricTimeSpan>(sourceTempoMap))
                ).ToList();

            var timedEvents = sourceMidiTrack
                .GetTimedEvents()
                .Where(x => x.Event is ControlChangeEvent)
                .Select(x =>
                    {
                        var cc = (ControlChangeEvent)x.Event;
                        return (
                            cc.ControlNumber,
                            cc.ControlValue,
                            Time: x.TimeAs<MetricTimeSpan>(sourceTempoMap)
                        );
                    }
                );

            var midiTrack = new TrackChunk();
            midiTrackList.Add(midiTrack);

            var noteManager = midiTrack.ManageNotes();
            foreach (var data in noteTime)
            {
                var time = TimeConverter.ConvertFrom(data.Time, dstTempoMap);
                var length = TimeConverter.ConvertFrom(data.Length, dstTempoMap);

                var note = new Note(
                    data.NoteName,
                    data.Octave,
                    length,
                    time
                )
                {
                    Velocity = data.Velocity,
                    OffVelocity = data.OffVelocity
                };

                noteManager.Objects.Add(note);
            }

            noteManager.SaveChanges();

            var eventManager = midiTrack.ManageTimedEvents();
            foreach (var data in timedEvents)
            {
                var time = TimeConverter.ConvertFrom(data.Time, dstTempoMap);
                var cc = new ControlChangeEvent(data.ControlNumber, data.ControlValue);
                eventManager.Objects.Add(new TimedEvent(cc, time));
            }

            eventManager.SaveChanges();
        }

        new MidiFile(midiTrackList).Write(Path.ChangeExtension(file, extension), true);
    }
}