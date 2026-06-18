using Newtonsoft.Json;
namespace OpenSpeaker.Api;
public class ApiResponse
{
    [JsonProperty("status")]
    public string Status { get; set; } = "ok";

    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    [JsonProperty("error", NullValueHandling = NullValueHandling.Ignore)]
    public string? Error { get; set; }

    public static ApiResponse Ok(string id) => new() { Status = "ok", Id = id };
    public static ApiResponse Err(string id, string error) => new() { Status = "error", Id = id, Error = error };
}
