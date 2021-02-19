using System.Text.Json.Serialization;

public class Language
{
    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("code")]
    public string Code{ get; set; }
}