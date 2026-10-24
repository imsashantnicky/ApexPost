namespace ApexPost.Infrastructure.Persistence.Json;

public sealed class JsonDataOptions
{
    public const string SectionName = "ApexPost:JsonData";

    /// <summary>Absolute or application-relative directory for JSON collection files.</summary>
    public string DataDirectory { get; set; } = "App_Data";
}
