using System.Text.Json.Serialization;

namespace Xvm.Blitz.Windows.Client.Core.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ApiKeyType
{
    Trial = 1,

    FullAccess = 2,
}
