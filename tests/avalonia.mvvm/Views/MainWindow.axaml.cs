using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using avalonia.mvvm.ViewModels;
using System.IO;
using System.Threading.Tasks;
using avalonia.mvvm.Models;
using Whisper.net;
using Whisper.net.Ggml;
using FFMpegCore;
using FFMpegCore.Enums;

namespace avalonia.mvvm.Views;

public partial class MainWindow : Window
{
    public bool ShouldCancelTranscription { get; private set; } = false;

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

            var file = files[0].TryGetLocalPath();
            
            if (string.IsNullOrWhiteSpace(file))
            {
                StatusBox.Text = "No file selected.";
                return;
            }

            if (!file.ToLower().EndsWith(".wav"))
            {
                StatusBox.Text = $"Converting {file} to .wav format...";
                var result = await ConvertMp3ToWav(file);
                
                if (!result)
                {
                    StatusBox.Text = $"Error converting {file} to .wav format.";
                }
                else
                {
                    StatusBox.Text = "Ready for transcription...";
                    TranscribeBtn.IsEnabled = true;
                }
            }
            else
            {
                FilePathTextBox.Text = file;
                StatusBox.Text = "Ready for transcription...";
                TranscribeBtn.IsEnabled = true;
            }
        }
        catch (Exception error)
        {
            StatusBox.Text = $"Error: {error.Message}";
        }
    }

    private void TranscribeBtn_OnClick(object? sender, RoutedEventArgs e)
    {
        if ((string)TranscribeBtn.Content! == "Cancel")
        {
            ShouldCancelTranscription = true;
            TranscribeBtn.Content = "Transcribe"; // Optionally, you may want to change the button text back
        }
        else
        {
            ShouldCancelTranscription = false;
            TranscribeAsync(); // Call transcription method
        }
    }
    
    private async void TranscribeAsync()
    {
        try
        {
            var wavFileName = FilePathTextBox.Text;
            
            var modelName = ModelChoice.SelectedValue != null ? ModelChoice.SelectedValue.ToString() : "small.en";
            
            if (string.IsNullOrWhiteSpace(modelName))
            {
                ((MainWindowViewModel)DataContext!).Status = "No model selected.";
                return;
            }
            
            if (string.IsNullOrWhiteSpace(wavFileName))
            {
                ((MainWindowViewModel)DataContext!).Status = "No file selected.";
                return;
            }
        
            var segments = ((MainWindowViewModel)DataContext!).Segments;
        
            if (segments.Count > 0)
            {
                segments.Clear();
            }
            
            if (!File.Exists(wavFileName))
            {
                StatusBox.Text = $"File {wavFileName} does not exist";
                return;
            }
            
            // Method to get the model file name based on the selected model
            var modelFileName = await GetModel(modelName);
            
            if (modelFileName == null)
            {
                StatusBox.Text = $"Error getting model {modelName}";
                return;
            }
            
            // This section creates the whisperFactory object which is used to create the processor object.
            using var whisperFactory = WhisperFactory.FromPath(modelFileName);

            // This section creates the processor object which is used to process the audio file, it uses language `auto` to detect the language of the audio file.
            var whisperProcessorBuilder = whisperFactory.CreateBuilder()
                .WithLanguage("auto")
                .WithProbabilities();
            
            if (TranslateCheckBox.IsChecked == true)
            {
                Console.WriteLine("Translate is checked");
                whisperProcessorBuilder.WithTranslate().WithLanguageDetection();
            }
            
            await using var processor = whisperProcessorBuilder
                .Build();

            await using var fileStream = File.OpenRead(wavFileName);

            StatusBox.Text = "Processing audio file...";
            
            ((MainWindowViewModel)DataContext!).Segments.Clear();
            
            TranscribeBtn.Content = "Cancel";
            
            var number = 1;
            
            // This section processes the audio file and prints the results (start time, end time and text) to the console.
            await foreach (var result in processor.ProcessAsync(fileStream))
            {
                // Add the result to SegmentsDataGrid
                var segment = new Segment
                {
                    Num = number++,
                    Start = result.Start.ToString(@"hh\:mm\:ss\.fff"),
                    End = result.End.ToString(@"hh\:mm\:ss\.fff"),
                    Text = result.Text
                };

                if (ShouldCancelTranscription)
                {
                    break;
                }
                
                segments.Add(segment);
            }
            
            if (ShouldCancelTranscription)
            {
                StatusBox.Text = "Transcription cancelled.";
                return;
            }
            
            TranscribeBtn.Content = "Transcribe";
            StatusBox.Text = "Transcription complete.";
        }
        catch (Exception error)
        {
            StatusBox.Text = $"Error: {error.Message}";
        }
    }

    private static readonly FilePickerFileType WhisperAudioType = new("Whisper Audio Files")
    {
        Patterns = ["*.wav", "*.mp3", "*.webm", "*.mp4"],
        AppleUniformTypeIdentifiers = ["public.wav", "public.mp3", "public.webm", "public.mp4"],
        MimeTypes = ["audio/wav", "audio/mp3", "audio/webm", "video/mp4"]
    };
    
    private async Task DownloadModel(string fileName, GgmlType ggmlType)
    {
        StatusBox.Text = $"Downloading Model {fileName}";
        await using var modelStream = await WhisperGgmlDownloader.GetGgmlModelAsync(ggmlType);
        await using var fileWriter = File.OpenWrite(fileName);
        await modelStream.CopyToAsync(fileWriter);
    }
    
    // Method to convert .mp3 to .wav
    private async Task<bool> ConvertMp3ToWav(string inputFile)
    {
        try
        {
            var wavFileName = Path.ChangeExtension(inputFile, ".wav");
            
            await FFMpegArguments
                .FromFileInput(inputFile)
                .OutputToFile(wavFileName, true, options => options
                    .WithAudioCodec("pcm_s16le")
                    .WithAudioSamplingRate(16000)
                    .WithCustomArgument("-ac 1"))
                .ProcessAsynchronously();
        
            if (!File.Exists(wavFileName))
            {
                throw new Exception($"Error converting {inputFile} to .wav format.");
            }
            
            FilePathTextBox.Text = wavFileName;
            StatusBox.Text = $"Converted {inputFile} to .wav format.";
            return true;
        }
        catch (Exception e)
        {
            StatusBox.Text = $"Error: {e.Message}";
            return false;
        }
    }

    private async Task<string?> GetModel(string model)
    {
        try
        {
            var ggmlType = model switch
            {
                "small" => GgmlType.Small,
                "small.en" => GgmlType.SmallEn,
                "medium" => GgmlType.Medium,
                "medium.en" => GgmlType.MediumEn,
                "large-v1" => GgmlType.LargeV1,
                "large-v2" => GgmlType.LargeV2,
                "large-v3" => GgmlType.LargeV3,
                "large-v3-turbo" => GgmlType.LargeV3Turbo,
                _ => GgmlType.SmallEn
            };

            var modelFileName = $"ggml-{model}.bin";
            
            if (!File.Exists(modelFileName))
            {
                StatusBox.Text = $"Model {modelFileName} not found, downloading...";
                await DownloadModel(modelFileName, ggmlType);
            }
            
            // If operating system is macOS, use CoreML model
            if (!OperatingSystem.IsMacOS()) return modelFileName;
            var coreMlModelcName = $"ggml-{model}-encoder.mlmodelc";
            // This sections detects whether the modelc directory (used by CoreML) is in out project disk. If it doesn't, it downloads it and extract it to the current folder.
            if (Directory.Exists(coreMlModelcName)) return modelFileName;
            // Note: The modelc directory needs to be extracted at the same level as the "ggml-base.bin" file (and the current executable).
            await WhisperGgmlDownloader.GetEncoderCoreMLModelAsync(ggmlType)
                .ExtractToPath(".");
            
            if (!Directory.Exists(coreMlModelcName)) throw new Exception($"Error downloading {coreMlModelcName}");

            return modelFileName;
        }
        catch (Exception e)
        {
            StatusBox.Text = $"Error: {e.Message}";
            return null;
        }
    }
}