using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace KubeClient.Models
{
    /// <summary>
    ///     SecretEnvSource selects a Secret to populate the environment variables with.
    ///     
    ///     The contents of the target Secret's Data field will represent the key-value pairs as environment variables.
    /// </summary>
    public partial class SecretEnvSourceV1
    {
        /// <summary>
        ///     Specify whether the Secret must be defined
        /// </summary>
        [JsonProperty("optional")]
        public bool Optional { get; set; }

        /// <summary>
        ///     Name of the referent. More info: https://kubernetes.io/docs/concepts/overview/working-with-objects/names/#names
        /// </summary>
        [JsonProperty("name")]
        public string Name { get; set; }
    }
}
