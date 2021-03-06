using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace KubeClient.Models
{
    /// <summary>
    ///     CustomResourceDefinition represents a resource that should be exposed on the API server.  Its name MUST be in the format &lt;.spec.name&gt;.&lt;.spec.group&gt;.
    /// </summary>
    [KubeObject("CustomResourceDefinition", "v1beta1")]
    public partial class CustomResourceDefinitionV1Beta1 : KubeResourceV1
    {
        /// <summary>
        ///     Spec describes how the user wants the resources to appear
        /// </summary>
        [JsonProperty("spec")]
        public CustomResourceDefinitionSpecV1Beta1 Spec { get; set; }

        /// <summary>
        ///     Status indicates the actual state of the CustomResourceDefinition
        /// </summary>
        [JsonProperty("status")]
        public CustomResourceDefinitionStatusV1Beta1 Status { get; set; }
    }
}
