using Godot;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;

namespace MidiFlattenerGUI;

public partial class Program : Control
{
    [Export] private SpinBox? _targetTempo;
    [Export] private LineEdit? _targetTimeSignature;
    [Export] private Button? _selectMidi;
    [Export] private ItemList? _selectedMidi;
    [Export] private Button? _clearAll;
    [Export] private Button? _removeSelected;
    [Export] private Button? _runConvert;
    [Export] private Control? _convertingIndicator;
    [Export] private Label? _convertLog;
    [Export] private Label? _version;

    private static readonly SearchValues<char> ValidStarterDigit = SearchValues.Create("123456789");
    private static readonly SearchValues<char> ValidDigit = SearchValues.Create("1234567890");
    [GeneratedRegex(@"^(?<BeatsPerBar>[1-9][0-9]?[0-9]?)/(?<NoteLengthPerBeat>[1-9][0-9]?[0-9]?)$")]
    private static partial Regex ValidTimeSignatureRegex { get; }

    public override void _Ready()
    {
        _targetTempo.NotNull();
        _targetTimeSignature.NotNull();
        _selectMidi.NotNull();
        _selectedMidi.NotNull();
        _clearAll.NotNull();
        _removeSelected.NotNull();
        _runConvert.NotNull();
        _convertingIndicator.NotNull();
        _convertLog.NotNull();
        _version.NotNull();

        _version.Text = ProjectSettings.GetSetting("application/config/version").ToString();
        
        void ToggleControls(bool enabled)
        {
            _targetTempo.NotNull();
            _targetTimeSignature.NotNull();
            _selectMidi.NotNull();
            _selectedMidi.NotNull();
            _clearAll.NotNull();
            _removeSelected.NotNull();
            _runConvert.NotNull();
            
            _targetTempo.FocusMode = enabled ? FocusModeEnum.All : FocusModeEnum.None;
            _targetTempo.MouseFilter = enabled ? MouseFilterEnum.Stop : MouseFilterEnum.Ignore;
            _targetTimeSignature.FocusMode = enabled ? FocusModeEnum.All : FocusModeEnum.None;
            _targetTimeSignature.MouseFilter = enabled ? MouseFilterEnum.Stop : MouseFilterEnum.Ignore;
            _selectMidi.FocusMode = enabled ? FocusModeEnum.All : FocusModeEnum.None;
            _selectMidi.MouseFilter = enabled ? MouseFilterEnum.Stop : MouseFilterEnum.Ignore;
            _selectedMidi.FocusMode = enabled ? FocusModeEnum.All : FocusModeEnum.None;
            _selectedMidi.MouseFilter = enabled ? MouseFilterEnum.Stop : MouseFilterEnum.Ignore;
            _clearAll.FocusMode = enabled ? FocusModeEnum.All : FocusModeEnum.None;
            _clearAll.MouseFilter = enabled ? MouseFilterEnum.Stop : MouseFilterEnum.Ignore;
            _removeSelected.FocusMode = enabled ? FocusModeEnum.All : FocusModeEnum.None;
            _removeSelected.MouseFilter = enabled ? MouseFilterEnum.Stop : MouseFilterEnum.Ignore;
            _runConvert.FocusMode = enabled ? FocusModeEnum.All : FocusModeEnum.None;
            _runConvert.MouseFilter = enabled ? MouseFilterEnum.Stop : MouseFilterEnum.Ignore;
        }

        _targetTempo.Value = 140;
        _targetTimeSignature.Text = "4/4";
        var lastValidatedTimeSignature = _targetTimeSignature.Text;
        _targetTimeSignature.TextChanged += text =>
        {
            const int beatsPerBarFirst = 0;
            const int beatsPerBar = 1;
            const int noteLengthPerBeatFirst = 2;
            const int noteLengthPerBeat = 3;
            var checkMode = beatsPerBarFirst;
            foreach (var inputChar in text)
            {
                switch (checkMode)
                {
                    case beatsPerBarFirst:
                        if (ValidStarterDigit.Contains(inputChar))
                        {
                            checkMode = beatsPerBar;
                            continue;
                        }
                        Validate();
                        return;
                    case beatsPerBar:
                        if(ValidDigit.Contains(inputChar)) continue;
                        if (inputChar == '/')
                        {
                            checkMode = noteLengthPerBeatFirst;
                            continue;
                        }
                        Validate();
                        return;
                    case noteLengthPerBeatFirst:
                        if (ValidStarterDigit.Contains(inputChar))
                        {
                            checkMode = noteLengthPerBeat;
                            continue;
                        }
                        Validate();
                        return;
                    case noteLengthPerBeat:
                        if (ValidDigit.Contains(inputChar)) continue;
                        Validate();
                        return;
                }
            }
        };
        _targetTimeSignature.TextSubmitted += _ => Validate();
        _targetTimeSignature.FocusExited += Validate;

        void Validate()
        {
            var match = ValidTimeSignatureRegex.Match(_targetTimeSignature.Text);
            if (!match.Success)
            {
                _targetTimeSignature.Text = lastValidatedTimeSignature;
                _targetTimeSignature.SelectAll();
            }
            else
            {
                var beatsPerBar = int.Parse(match.Groups["BeatsPerBar"].Value); 
                var noteLengthPerBeat = int.Parse(match.Groups["NoteLengthPerBeat"].Value);
                beatsPerBar = Mathf.Clamp(beatsPerBar, 1, 255);
                noteLengthPerBeat = Mathf.Clamp(noteLengthPerBeat, 1, 255);
                lastValidatedTimeSignature = $"{beatsPerBar}/{noteLengthPerBeat}";
            }
        }
        
        HashSet<string> selectedMidiSet = [];
        _selectMidi.Pressed += () =>
        {
            var fileDialog = new FileDialog
            {
                Access = FileDialog.AccessEnum.Filesystem,
                Filters = ["*.mid, *.midi;MIDI Files"],
                UseNativeDialog = true,
                ForceNative = true,
                FileMode = FileDialog.FileModeEnum.OpenFiles,
                CurrentDir = OS.GetSystemDir(OS.SystemDir.Desktop)
            };
            fileDialog.FilesSelected += filePaths =>
            {
                foreach (var filePath in filePaths)
                {
                    if (!selectedMidiSet.Add(filePath)) continue;
                    _selectedMidi.AddItem(filePath);
                }

                _removeSelected.Disabled = _selectedMidi.GetSelectedItems().Length == 0;
                var hasNoSelected = selectedMidiSet.Count == 0;
                _clearAll.Disabled = hasNoSelected;
                _runConvert.Disabled = hasNoSelected;
                fileDialog.QueueFree();
            };
            fileDialog.Canceled += fileDialog.QueueFree;
            AddChild(fileDialog);
            fileDialog.PopupCentered();
        };
        _selectedMidi.SelectMode = ItemList.SelectModeEnum.Multi;
        _selectedMidi.MultiSelected += (_, _) => 
            _removeSelected.Disabled = !_selectedMidi.IsAnythingSelected();
        _selectedMidi.EmptyClicked += (_, _) =>
        {
            _removeSelected.Disabled = true;
            _selectedMidi.DeselectAll();
        };
        _clearAll.Disabled = true;
        _clearAll.Pressed += () =>
        {
            selectedMidiSet.Clear();
            _selectedMidi.Clear();
            _removeSelected.Disabled = true;
            _clearAll.Disabled = true;
            _runConvert.Disabled = true;
        };
        _removeSelected.Disabled = true;
        _removeSelected.Pressed += () =>
        {
            foreach (var index in _selectedMidi.GetSelectedItems().OrderDescending())
            {
                var item = _selectedMidi.GetItemText(index);
                selectedMidiSet.Remove(item);
                _selectedMidi.RemoveItem(index);
            }

            _removeSelected.Disabled = _selectedMidi.GetSelectedItems().Length == 0;
            var hasNoSelected = selectedMidiSet.Count == 0;
            _clearAll.Disabled = hasNoSelected;
            _runConvert.Disabled = hasNoSelected;
        };
        _convertingIndicator.Hide();
        _convertLog.Hide();
        _runConvert.Disabled = true;
        _runConvert.Pressed += () =>
        {
            ToggleControls(false);
            _convertingIndicator.Show();
            var match = ValidTimeSignatureRegex.Match(_targetTimeSignature.Text);
            var beatsPerBar = byte.Parse(match.Groups["BeatsPerBar"].Value);
            var noteLengthPerBeat = byte.Parse(match.Groups["NoteLengthPerBeat"].Value);
            var task = Algorithm.ConvertAsync(
                (int)_targetTempo.Value,
                beatsPerBar,
                noteLengthPerBeat,
                selectedMidiSet.ToArray()
            );
            task.ConfigureAwait(true).GetAwaiter().OnCompleted(() =>
                {
                    var preserved = new HashSet<string>();
                    
                    if (task.IsFaulted)
                    {
                        var exceptionBuilder = new StringBuilder();
                        foreach (var exception in task.Exception.InnerExceptions)
                        {
                            if (exception is Algorithm.MidiFlattenException midiFlattenException)
                            {
                                exceptionBuilder.AppendLine(midiFlattenException.Message);
                                preserved.Add(midiFlattenException.MidiPath);
                            }
                            else
                            {
                                exceptionBuilder.Append(exception.GetType().Name).AppendLine(":").Append(" - ").Append(exception.Message).AppendLine();
                            }
                        }

                        OS.Alert(Tr("CONVERTING_ERROR_TEXT").Replace("<ErrorMessage>", exceptionBuilder.ToString()), Tr("CONVERTING_ERROR_TITLE"));
                    }

                    ToggleControls(true);
                    _convertingIndicator.Hide();
                    
                    _convertLog.Show();
                    _convertLog.Text = Tr("LAST_CONVERT_LOG").Replace("<Time>", Time.GetTimeStringFromSystem()).Replace("<Amount>", selectedMidiSet.Count.ToString());

                    for (var i = _selectedMidi.GetItemCount() - 1; i >= 0; i--)
                    {
                        var path = _selectedMidi.GetItemText(i);
                        if(preserved.Contains(path)) continue;
                        _selectedMidi.RemoveItem(i);
                        selectedMidiSet.Remove(path);
                    }
                    
                    _removeSelected.Disabled = _selectedMidi.GetSelectedItems().Length == 0;
                    var hasNoSelected = selectedMidiSet.Count == 0;
                    _clearAll.Disabled = hasNoSelected;
                    _runConvert.Disabled = hasNoSelected;
                }
            );
        };
        var window = GetTree().GetRoot();
        window.GuiEmbedSubwindows = false;
        window.FilesDropped += files =>
        {
            foreach (var filePath in files.Where(x => x.EndsWith(".mid") || x.EndsWith(".midi")))
            {
                if (!selectedMidiSet.Add(filePath)) continue;
                _selectedMidi.AddItem(filePath);
            }
            
            _removeSelected.Disabled = _selectedMidi.GetSelectedItems().Length == 0;
            var hasNoSelected = selectedMidiSet.Count == 0;
            _clearAll.Disabled = hasNoSelected;
            _runConvert.Disabled = hasNoSelected;
        };
    }
}

public static class Utils
{
    public static void NotNull<T>(
        [NotNull] this T? obj,
        [CallerArgumentExpression(nameof(obj))]
        string name = "") where T : notnull
    {
        if (obj != null) return;
        throw new ArgumentNullException(nameof(obj), $"{name} cannot be null");
    }
}