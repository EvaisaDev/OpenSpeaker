using Newtonsoft.Json;
namespace OpenSpeaker.Api;
public class ApiResponse
{
    [JsonProperty("id", Order = 0)]
    public string Id { get; set; } = string.Empty;

    [JsonProperty("result", Order = 1, NullValueHandling = NullValueHandling.Ignore)]
    public object? Result { get; set; }

    [JsonProperty("status", Order = 2)]
    public string Status { get; set; } = "ok";

    [JsonProperty("error", Order = 3, NullValueHandling = NullValueHandling.Ignore)]
    public string? Error { get; set; }

    public static ApiResponse Ok(string id) => new() { Id = id, Status = "ok" };
    public static ApiResponse WithResult(string id, object result) => new() { Id = id, Status = "ok", Result = result };
    public static ApiResponse Err(string id, string error) => new() { Id = id, Status = "error", Error = error };
}
