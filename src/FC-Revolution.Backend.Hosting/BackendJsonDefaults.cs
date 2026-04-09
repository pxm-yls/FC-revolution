using System.Text.Json;

namespace FCRevolution.Backend.Hosting;

internal static class BackendJsonDefaults
{
    internal static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
}
