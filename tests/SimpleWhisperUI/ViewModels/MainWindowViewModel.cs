﻿using System.Collections.Generic;
using System.Collections.ObjectModel;
using SimpleWhisperUI.Models;

namespace SimpleWhisperUI.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    public string SelectedFilePath { get; set; } = string.Empty;
    public string Status { get; set; } = "Ready...";

    public ObservableCollection<Segment> Segments { get; set; } = [];
    
    public List<string> Models { get; set; } = new WhisperModels().Models;
}
