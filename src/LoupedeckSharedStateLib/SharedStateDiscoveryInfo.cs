namespace Loupedeck.SharedState
{
    using System;
    using System.Text.Json.Serialization;

    public sealed class SharedStateDiscoveryInfo
    {
        [JsonPropertyName("version")]
        public Int32 Version { get; set; }

        [JsonPropertyName("provider")]
        public String Provider { get; set; }

        [JsonPropertyName("capabilities")]
        public String[] Capabilities { get; set; } = [];

        [JsonPropertyName("endpoint")]
        public String Endpoint { get; set; }
    }
}
