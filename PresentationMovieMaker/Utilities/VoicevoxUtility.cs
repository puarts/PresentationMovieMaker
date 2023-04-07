using System.Collections.Generic;
using System.Media;
using System.Net.Http;
using System.Net.Http.Headers;
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

public static class VoicevoxUtility
{
    const string baseUrl = "http://localhost:50021/";

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
        using var httpClient = new HttpClient();
        using var requestMessage = new HttpRequestMessage(new HttpMethod("GET"), $"{baseUrl}speakers");
        requestMessage.Headers.TryAddWithoutValidation("accept", "application/json");

        requestMessage.Content = new StringContent("");
        requestMessage.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");
        var response = await httpClient.SendAsync(requestMessage);
        return await response.Content.ReadAsStringAsync();
    }


    public static async Task Speek(string text, int speakerId)
    {
        using var httpClient = new HttpClient();
        string query = await CreateAudioQuery(text, speakerId, httpClient);

        // 音声合成
        using var request = new HttpRequestMessage(new HttpMethod("POST"), $"{baseUrl}synthesis?speaker={speakerId}&enable_interrogative_upspeak=true");
        request.Headers.TryAddWithoutValidation("accept", "audio/wav");

        request.Content = new StringContent(query);
        request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/json");

        var response = await httpClient.SendAsync(request);

        // 音声再生
        using var httpStream = await response.Content.ReadAsStreamAsync();
        var player = new SoundPlayer(httpStream);
        player.PlaySync();
    }

    public static async Task RecordSpeech(string outputWaveFilePath, string text, int speaker)
    {
        using var httpClient = new HttpClient();
        string query = await CreateAudioQuery(text, speaker, httpClient);

        // 音声合成
        using var request = new HttpRequestMessage(new HttpMethod("POST"), $"{baseUrl}synthesis?speaker={speaker}&enable_interrogative_upspeak=true");
        request.Headers.TryAddWithoutValidation("accept", "audio/wav");

        request.Content = new StringContent(query);
        request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/json");

        var response = await httpClient.SendAsync(request);

        // 書き出し
        using var fs = System.IO.File.Create(outputWaveFilePath);
        using var stream = await response.Content.ReadAsStreamAsync();
        stream.CopyTo(fs);
        fs.Flush();
    }

    /// <summary>
    /// オーディオクエリを作ります。
    /// </summary>
    /// <param name="text"></param>
    /// <param name="speakerId"></param>
    /// <param name="httpClient"></param>
    /// <returns></returns>
    private static async Task<string> CreateAudioQuery(string text, int speakerId, HttpClient httpClient)
    {
        using var requestMessage = new HttpRequestMessage(new HttpMethod("POST"), $"{baseUrl}audio_query?text={text}&speaker={speakerId}");
        requestMessage.Headers.TryAddWithoutValidation("accept", "application/json");

        requestMessage.Content = new StringContent("");
        requestMessage.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");
        var response = await httpClient.SendAsync(requestMessage);
        return await response.Content.ReadAsStringAsync();
    }
}
