using LiteDB;
namespace OpenSpeaker.Models;

public class ApiHeader
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}

public class CustomApiDefinition
{
    [BsonId]
    public ObjectId Id { get; set; } = ObjectId.NewObjectId();
    public string Name { get; set; } = "My Engine";
    public bool Enabled { get; set; } = true;

    public string SynthUrl { get; set; } = string.Empty;
    public string SynthMethod { get; set; } = "POST";
    public List<ApiHeader> SynthHeaders { get; set; } = new();
    public string SynthBodyTemplate { get; set; } = "{\"text\": \"{text}\", \"voice\": \"{voice}\"}";
    public string ResponseFormat { get; set; } = "binary";
    public string ResponseAudioPath { get; set; } = string.Empty;
    public string AudioFormat { get; set; } = "mp3";

    public string VoicesUrl { get; set; } = string.Empty;
    public string VoicesMethod { get; set; } = "GET";
    public List<ApiHeader> VoicesHeaders { get; set; } = new();
    public string VoicesArrayPath { get; set; } = string.Empty;
    public string VoiceIdField { get; set; } = "id";
    public string VoiceNameField { get; set; } = "name";

    public string EngineId => "custom:" + Id.ToString();
}
