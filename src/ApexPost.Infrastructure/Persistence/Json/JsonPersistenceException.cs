namespace ApexPost.Infrastructure.Persistence.Json;

public sealed class JsonPersistenceException : Exception
{
    public JsonPersistenceException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
