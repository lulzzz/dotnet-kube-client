using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace KubeClient.Models
{
    /// <summary>
    ///     Role is a namespaced, logical grouping of PolicyRules that can be referenced as a unit by a RoleBinding.
    /// </summary>
    [KubeObject("Role", "rbac.authorization.k8s.io/v1alpha1")]
    public partial class RoleV1Alpha1 : KubeResourceV1
    {
        /// <summary>
        ///     Rules holds all the PolicyRules for this Role
        /// </summary>
        [JsonProperty("rules", NullValueHandling = NullValueHandling.Ignore)]
        public List<PolicyRuleV1Alpha1> Rules { get; set; } = new List<PolicyRuleV1Alpha1>();
    }
}
