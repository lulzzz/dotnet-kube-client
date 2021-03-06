using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace KubeClient.Models
{
    /// <summary>
    ///     Pod Security Policy List is a list of PodSecurityPolicy objects.
    /// </summary>
    [KubeObject("PodSecurityPolicyList", "extensions/v1beta1")]
    public class PodSecurityPolicyListV1Beta1 : KubeResourceListV1<PodSecurityPolicyV1Beta1>
    {
        /// <summary>
        ///     Items is a list of schema objects.
        /// </summary>
        [JsonProperty("items", ObjectCreationHandling = ObjectCreationHandling.Reuse)]
        public override List<PodSecurityPolicyV1Beta1> Items { get; } = new List<PodSecurityPolicyV1Beta1>();
    }
}
