using System.Collections.Generic;

namespace avalonia.mvvm.Models;

public class WhisperModels
{
    public List<string> Models { get; } =
    [
        "small",
        "small.en",
        "medium",
        "medium.en",
        "large-v1",
        "large-v2",
        "large-v3",
        "large-v3-turbo"
    ];
}