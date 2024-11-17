# whisper.net-tests
Test CLI and GUI applications using Whisper.net

## SimpleWhisperUI

A simple Avalonia UI application that uses Whisper.net to transcribe audio from an input file.

If the file is not a wav, it will use FFMpegCore to convert it, so long as FFmpeg is available in the PATH.

<img src="tests/SimpleWhisperUI.png" alt="Screenshot of SimpleWhisperUI showing a transcription interface. The application includes a file selection field with a selected file '/Users/nathan/Movies/rick.wav', options for translation and model selection, and a 'Transcribe' button. Below is a table with columns for line number, start time, end time, and text, displaying lyrics from a popular song.">
