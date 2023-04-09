using System.Collections.Generic;
using System.Media;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.NetworkInformation;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace PresentationMovieMaker.Utilities;

public class Style
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("id")]
    public int Id { get; set; }
}

public class SupportedFeatures
{
    [JsonPropertyName("permitted_synthesis_morphing")]
    public string? PermittedSynthesisMorphing { get; set; }
}

public class Speaker
{
    [JsonPropertyName("supported_features")]
    public SupportedFeatures? SupportedFeatures { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("speaker_uuid")]
    public string? SpeakerUuid { get; set; }

    [JsonPropertyName("styles")]
    public List<Style>? Styles { get; set; }

    [JsonPropertyName("version")]
    public string? Version { get; set; }
}



public class Moras
{
    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("consonant")]
    public string? Consonant { get; set; }

    [JsonPropertyName("consonant_length")]
    public double? ConsonantLength { get; set; }

    [JsonPropertyName("vowel")]
    public string? Vowel { get; set; }

    [JsonPropertyName("vowel_length")]
    public double? VowelLength { get; set; }

    [JsonPropertyName("pitch")]
    public double Pitch { get; set; }
}

public class AccentPhrase
{
    [JsonPropertyName("moras")]
    public List<Moras>? Moras { get; set; }

    [JsonPropertyName("accent")]
    public int Accent { get; set; }

    [JsonPropertyName("pause_mora")]
    public object? PauseMora { get; set; }

    [JsonPropertyName("is_interrogative")]
    public bool IsInterrogative { get; set; }
}

public class AudioQueryResponse
{
    [JsonPropertyName("accent_phrases")]
    public List<AccentPhrase>? AccentPhrases { get; set; }

    [JsonPropertyName("speedScale")]
    public double SpeedScale { get; set; }

    [JsonPropertyName("pitchScale")]
    public double PitchScale { get; set; }

    [JsonPropertyName("intonationScale")]
    public double IntonationScale { get; set; }

    [JsonPropertyName("volumeScale")]
    public double VolumeScale { get; set; }

    [JsonPropertyName("prePhonemeLength")]
    public double PrePhonemeLength { get; set; }

    [JsonPropertyName("postPhonemeLength")]
    public double PostPhonemeLength { get; set; }

    [JsonPropertyName("outputSamplingRate")]
    public int OutputSamplingRate { get; set; }

    [JsonPropertyName("outputStereo")]
    public bool OutputStereo { get; set; }

    [JsonPropertyName("kana")]
    public string? Kana { get; set; }
}

public static class VoicevoxUtility
{
    //const string baseUrl = "http://localhost:50021/";
    const string baseUrl = "http://127.0.0.1:50021/";

    private static readonly HttpClient _client = new HttpClient();

    public static IEnumerable<Speaker> EnumerateSpeakers()
    {
        var jsonStr = GetSpeakersAsJson().Result;
        var deserialized = JsonSerializer.Deserialize<List<Speaker>>(jsonStr);
        if (deserialized is null)
        {
            yield break;
        }

        foreach (var speakerInfo in deserialized)
        {
            yield return speakerInfo;
        }
    }

    private static async Task<string> GetSpeakersAsJson()
    {
        using var requestMessage = new HttpRequestMessage(new HttpMethod("GET"), $"{baseUrl}speakers");
        requestMessage.Headers.TryAddWithoutValidation("accept", "application/json");

        requestMessage.Content = new StringContent("");
        requestMessage.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");
        var response = await _client.SendAsync(requestMessage);
        return await response.Content.ReadAsStringAsync();
    }


    public static async Task Speek(
        string text, int speakerId, float speedScale = 1.0f, float pitchScale = 0.0f)
    {
        string queryJson = await CreateAudioQuery(text, speakerId);
        var queryData = JsonSerializer.Deserialize<AudioQueryResponse>(queryJson) ?? throw new System.Exception();
        queryData.SpeedScale = speedScale;
        queryData.PitchScale = pitchScale;
        var synthesisParamJson = JsonSerializer.Serialize(queryData);

        // 音声合成
        using var request = new HttpRequestMessage(new HttpMethod("POST"), $"{baseUrl}synthesis?speaker={speakerId}&enable_interrogative_upspeak=true");
        request.Headers.TryAddWithoutValidation("accept", "audio/wav");

        request.Content = new StringContent(synthesisParamJson);
        request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/json");

        var response = await _client.SendAsync(request);

        // 音声再生
        using var httpStream = await response.Content.ReadAsStreamAsync();
        var player = new SoundPlayer(httpStream);
        player.PlaySync();
    }

    public static async Task RecordSpeech(string outputWaveFilePath, string text, int speaker, float speedScale = 1.0f)
    {
        string queryJson = await CreateAudioQuery(text, speaker);

        var queryData =  JsonSerializer.Deserialize<AudioQueryResponse>(queryJson) ?? throw new System.Exception();
        queryData.SpeedScale = speedScale;
        var synthesisParamJson = JsonSerializer.Serialize(queryData);

        // 音声合成
        using var request = new HttpRequestMessage(new HttpMethod("POST"), $"{baseUrl}synthesis?speaker={speaker}&enable_interrogative_upspeak=true");
        request.Headers.TryAddWithoutValidation("accept", "audio/wav");

        request.Content = new StringContent(synthesisParamJson);
        request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/json");

        var response = await _client.SendAsync(request);

        // 書き出し
        using var fs = System.IO.File.Create(outputWaveFilePath);
        using var stream = await response.Content.ReadAsStreamAsync();
        stream.CopyTo(fs);
        fs.Flush();
    }

    private static async Task<string> CreateAudioQuery(string text, int speakerId)
    {
        using var requestMessage = new HttpRequestMessage(new HttpMethod("POST"), $"{baseUrl}audio_query?text={text}&speaker={speakerId}");
        requestMessage.Headers.TryAddWithoutValidation("accept", "application/json");

        requestMessage.Content = new StringContent("");
        requestMessage.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");
        var response = await _client.SendAsync(requestMessage);
        return await response.Content.ReadAsStringAsync();
    }
}
