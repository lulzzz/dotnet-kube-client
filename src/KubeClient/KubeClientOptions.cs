using System;
using System.IO;
using System.Security.Cryptography.X509Certificates;

namespace KubeClient
{
    /// <summary>
    ///     Options for the Kubernetes API client.
    /// </summary>
    public class KubeClientOptions
    {
        /// <summary>
        ///     Create new <see cref="KubeClientOptions"/>.
        /// </summary>
        public KubeClientOptions()
        {
        }

        /// <summary>
        ///     Create new <see cref="KubeClientOptions"/>.
        /// </summary>
        /// <param name="apiEndPoint">
        ///     The base address of the Kubernetes API end-point.
        /// </param>
        public KubeClientOptions(string apiEndPoint)
        {
            if (String.IsNullOrWhiteSpace(apiEndPoint))
                throw new ArgumentException("Argument cannot be null, empty, or entirely composed of whitespace: 'apiEndPoint'.", nameof(apiEndPoint));

            ApiEndPoint = new Uri(apiEndPoint);
        }

        /// <summary>
        ///     The default Kubernetes namespace to use when no specific namespace is specified.
        /// </summary>
        public string KubeNamespace { get; set; } = "default";
        
        /// <summary>
        ///     The base address of the Kubernetes API end-point.
        /// </summary>
        public Uri ApiEndPoint { get; set; }

        /// <summary>
        ///     The access token used to authenticate to the Kubernetes API.
        /// </summary>
        public string AccessToken { get; set; }

        /// <summary>
        ///     The client certificate used to authenticate to the Kubernetes API.
        /// </summary>
        public X509Certificate2 ClientCertificate { get; set; }

        /// <summary>
        ///     The expected CA certificate used by the Kubernetes API.
        /// </summary>
        public X509Certificate2 CertificationAuthorityCertificate { get; set; }

        /// <summary>
        ///     Skip verification of the server's SSL certificate?
        /// </summary>
        public bool AllowInsecure { get; set; }

        /// <summary>
        ///     Log request / response headers?
        /// </summary>
        public bool LogHeaders { get; set; }

        /// <summary>
        ///     Load request / response payloads (bodies)?
        /// </summary>
        public bool LogPayloads { get; set; }

        /// <summary>
        ///     Ensure that the <see cref="KubeClientOptions"/> are valid.
        /// </summary>
        /// <returns>
        ///     The <see cref="KubeClientOptions"/> (enables inline use).
        /// </returns>
        public KubeClientOptions EnsureValid()
        {
            if (ApiEndPoint == null || !ApiEndPoint.IsAbsoluteUri)
                throw new KubeClientException("Invalid KubeClientOptions: must specify a valid API end-point.");

            if (ClientCertificate != null && !ClientCertificate.HasPrivateKey)
                throw new KubeClientException("Invalid KubeClientOptions: the private key for the supplied client certificate is not available.");

            if (String.IsNullOrWhiteSpace(KubeNamespace))
                throw new KubeClientException("Invalid KubeClientOptions: must specify a valid default namespace.");

            return this;
        }

        /// <summary>
        ///     Create a copy of the <see cref="KubeClientOptions"/>.
        /// </summary>
        /// <returns>
        ///     The new <see cref="KubeClientOptions"/>.
        /// </returns>
        public KubeClientOptions Clone()
        {
            return new KubeClientOptions
            {
                ApiEndPoint = ApiEndPoint,
                AccessToken = AccessToken,
                AllowInsecure = AllowInsecure,
                CertificationAuthorityCertificate = CertificationAuthorityCertificate,
                ClientCertificate = ClientCertificate,
                KubeNamespace = KubeNamespace,
                LogHeaders = LogHeaders,
                LogPayloads = LogPayloads
            };
        }

        /// <summary>
        ///     Create new <see cref="KubeClientOptions"/> using pod-level configuration.
        /// </summary>
        /// <returns>
        ///     The configured <see cref="KubeClientOptions"/>.
        /// </returns>
        /// <remarks>
        ///     Only works from within a container running in a Kubernetes Pod.
        /// </remarks>
        public static KubeClientOptions FromPodServiceAccount()
        {
            string kubeServiceHost = Environment.GetEnvironmentVariable("KUBERNETES_SERVICE_HOST");
            if (String.IsNullOrWhiteSpace(kubeServiceHost))
                throw new InvalidOperationException("KubeApiClient.CreateFromPodServiceAccount can only be called when running in a Kubernetes Pod (KUBERNETES_SERVICE_HOST environment variable is not defined).");

            const string apiEndPoint = "https://kubernetes/";
            string accessToken = File.ReadAllText("/var/run/secrets/kubernetes.io/serviceaccount/token");
            var kubeCACertificate = new X509Certificate2(
                File.ReadAllBytes("/var/run/secrets/kubernetes.io/serviceaccount/ca.crt")
            );

            return new KubeClientOptions
            {
                ApiEndPoint = new Uri(apiEndPoint),
                AccessToken = accessToken,
                CertificationAuthorityCertificate = kubeCACertificate
            };
        }
    }
}