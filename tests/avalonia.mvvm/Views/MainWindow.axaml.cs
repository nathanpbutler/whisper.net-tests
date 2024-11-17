using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using avalonia.mvvm.ViewModels;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using avalonia.mvvm.Models;
using Whisper.net;
using Whisper.net.Ggml;

namespace avalonia.mvvm.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }
    
    // File picker to select a *.wav file
    private async void SelectFileBtn_OnClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (TranscribeBtn.IsEnabled) TranscribeBtn.IsEnabled = false;
            
            var topLevel = GetTopLevel(this);
        
            if (topLevel == null)
            {
                StatusBox.Text = "No top-level window found.";
                return;
            }
        
            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions()
            {
                Title = "Select a Whisper Audio File",
                FileTypeFilter = [WhisperAudioType, FilePickerFileTypes.TextPlain],
                AllowMultiple = false
            });

            if (files.Count < 1)
            {
                StatusBox.Text = "No file selected.";
                return;
            }
        
            var file = files[0];
            
            /*var viewModel = (MainWindowViewModel)DataContext!;*/

            FilePathTextBox.Text = file.Path.ToString();
            StatusBox.Text = "Ready for transcription...";
            TranscribeBtn.IsEnabled = true;
        }
        catch (Exception error)
        {
            StatusBox.Text = $"Error: {error.Message}";
        }
    }

    private async void TranscribeBtn_OnClick(object? sender, RoutedEventArgs e)
    {
        var wavFileName = FilePathTextBox.Text;
        if (string.IsNullOrWhiteSpace(wavFileName))
        {
            StatusBox.Text = "No file selected.";
            return;
        }

        try
        {
            if (!File.Exists(wavFileName))
            {
                StatusBox.Text = $"File {wavFileName} does not exist";
                return;
            }

            if (!wavFileName.ToLower().EndsWith(".wav"))
            {
                StatusBox.Text = "Only .wav files are supported.";
                return;
            }
            
            var ggmlType = GgmlType.SmallEn;
            var modelFileName = "small.en.bin";
            var coreMlModelcName = "ggml-small.en-encoder.mlmodelc";
            
            if (!File.Exists(modelFileName))
            {
                StatusBox.Text = $"Model {modelFileName} not found, downloading...";
                await DownloadModel(modelFileName, ggmlType);
            }
            
            // This sections detects whether the modelc directory (used by CoreML) is in out project disk. If it doesn't, it downloads it and extract it to the current folder.
            if (!Directory.Exists(coreMlModelcName))
            {
                // Note: The modelc directory needs to be extracted at the same level as the "ggml-base.bin" file (and the current executable).
                await WhisperGgmlDownloader.GetEncoderCoreMLModelAsync(ggmlType)
                    .ExtractToPath(".");
            }
            
            // This section creates the whisperFactory object which is used to create the processor object.
            using var whisperFactory = WhisperFactory.FromPath(modelFileName);

            // This section creates the processor object which is used to process the audio file, it uses language `auto` to detect the language of the audio file.
            await using var processor = whisperFactory.CreateBuilder()
                .WithLanguage("auto")
                .WithTokenTimestamps()
                .Build();

            await using var fileStream = File.OpenRead(wavFileName);

            StatusBox.Text = "Processing audio file...";
            
            ((MainWindowViewModel)DataContext!).Segments.Clear();
            
            // This section processes the audio file and prints the results (start time, end time and text) to the console.
            await foreach (var result in processor.ProcessAsync(fileStream))
            {
                // Add the result to SegmentsDataGrid
                var segment = new Segment
                {
                    StartTime = result.Start.ToString(@"hh\:mm\:ss\.fff"),
                    EndTime = result.End.ToString(@"hh\:mm\:ss\.fff"),
                    Text = result.Text
                };
                ((MainWindowViewModel)DataContext!).Segments.Add(segment);
            }
            
            StatusBox.Text = "Transcription complete.";

        }
        catch (Exception error)
        {
            StatusBox.Text = $"Error: {error.Message}";
            return;
        }
    }

    private static readonly FilePickerFileType WhisperAudioType = new("Whisper Audio Files")
    {
        Patterns = ["*.wav"],
        AppleUniformTypeIdentifiers = ["public.wav"],
        MimeTypes = ["audio/wav"]
    };
    
    private async Task DownloadModel(string fileName, GgmlType ggmlType)
    {
        StatusBox.Text = $"Downloading Model {fileName}";
        await using var modelStream = await WhisperGgmlDownloader.GetGgmlModelAsync(ggmlType);
        await using var fileWriter = File.OpenWrite(fileName);
        await modelStream.CopyToAsync(fileWriter);
    }
}