// --------------------------------------------------------------------------------------------------------------------
// <copyright file="HostGAPluginClient.cs" company="Microsoft Corporation">
//   Copyright (c) 2020 Microsoft Corporation.  All rights reserved.
// </copyright>
// <summary>
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ProbeStatusClient
{
    using Newtonsoft.Json.Linq;
    using System.Net.Http;
    using System.Text;
    using System;
    using System.Net;
    using System.Threading.Tasks;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Sockets;
    using System.Runtime.Serialization;

    public enum AgentState
    {
        Healthy,
        Unhealthy,
        Unresponsive
    }

    public class HostGAPluginClient
    {
        /// <summary>
        /// We create the HttpClient only once per class so that we
        /// reuse sockets.
        /// </summary>
        private static readonly HttpClient Client = new HttpClient();

        /// <summary>
        /// HostGAPlugin Host Config Name Header Name
        /// </summary>
        private static readonly string HostGAPluginHostConfigNameHeaderName = "x-ms-host-config-name";

        /// <summary>
        /// HostGAPlugin Host Config Name (just needs to be present, unused by AzureStack HostGAPlugin)
        /// </summary>
        private static readonly string HostGAPluginHostConfigName = "DummyHostConfigName";

        /// <summary>
        /// HostGAPlugin Container ID header name
        /// </summary>
        private static readonly string HostGAPluginContainerIdHeaderName = "x-ms-containerid";

        /// <summary>
        /// HostGAPlugin Probe Container ID (just needs to be present, unused by AzureStack HostGAPlugin)
        /// </summary>
        private static readonly string HostGAPluginContainerId = "11111111-aaaa-aaaa-aaaa-111111111111";

        /// <summary>
        /// HostGAPlugin content type.
        /// </summary>
        private static readonly string HostGAPluginContentType = "application/json";

        /// <summary>
        /// HostGAPlugin version header name.
        /// </summary>
        private static readonly string HostGAPluginBlobTypeHeaderName = "x-ms-blob-type";

        /// <summary>
        /// Supported HostGAPlugin blob type we use.
        /// </summary>
        private static readonly string HostGAPluginBlobType = "BlockBlob";

        /// <summary>
        /// Azure correlation request Id header name.
        /// </summary>
        private static readonly string CorrelationIdHeaderName = "x-ms-correlation-request-id";

        /// <summary>
        /// Well known Uri for Metadata Server aggregate status that HostGAPlugin forwards request to
        /// </summary>
        private static readonly string WireServerStatusRequestUri = "http://asappgateway.azurestack.local:4443/Microsoft.Compute.WireServer/wireServerAggregateStatus?sv=2017-04-17&se=9999-01-01T00:00:00Z&sr=c&sp=rw&sk=system-1";

        /// <summary>
        /// Well known Uri for monitor to send probe VM aggregate status request to HostGAPlugin
        /// </summary>
        private static readonly Uri HostGAPluginStatusRequestUri = new Uri($"http://127.0.0.1:32526/{HostGAPluginContainerId}/status");

        /// <summary>
        /// The client timeout.
        /// </summary>
        private static readonly TimeSpan ClientTimeout = TimeSpan.FromSeconds(10);

        private static string DummyHostGAPluginDummyStatusContent
        {
            get
            {
                return new JObject(
                    new JProperty("requestUri", WireServerStatusRequestUri),
                    new JProperty("headers",
                        new JArray(
                            new JObject(
                                new JProperty("headerName", HostGAPluginBlobTypeHeaderName),
                                new JProperty("headerValue", HostGAPluginBlobType)
                            )
                        )
                    ),
                    new JProperty("content",
                        Convert.ToBase64String(
                Encoding.UTF8.GetBytes(
                                new JObject(
                                    new JProperty("aggregateStatus",
                                        new JObject(
                                            new JProperty("guestAgentStatus",
                                                new JObject()
                                            )
                                        )
                                    ),
                                    new JProperty("timestampUTC", DateTime.UtcNow.ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'Z'"))
                                ).ToString()
                            )
                        )
                    )
                ).ToString();
            }
        }

        /// <summary>
        /// Exceptions which should be caught and handled either as a top level exception, or within an AggregateException.
        /// </summary>
        private static readonly HashSet<Type> HandledExceptions = new HashSet<Type>()
        {
            typeof(HttpRequestException),
            typeof(TaskCanceledException),
            typeof(SocketException),
            typeof(WebException),
            typeof(UnauthorizedAccessException)
        };

        public HostGAPluginClient()
        {
            Client.Timeout = ClientTimeout;
        }

        /// <summary>
        /// Get the state of the goalstate path.
        /// </summary>
        public async Task<AgentState> GetState()
        {
            // The correlation ID is going to be pushed through the entire pipeline.
            var correlationId = Guid.NewGuid().ToString();
            using (var request = this.GetHostGAPluginRequest(correlationId))
            {
                try
                {
                    var response = await Client.SendAsync(request);
                    return response.StatusCode == HttpStatusCode.OK ? AgentState.Healthy : AgentState.Unhealthy;
                }
                catch (Exception ex) when (AgentClientHelper.CompareNestedExceptions(HandledExceptions, ex))
                {
                    throw new WindowsServiceClientException($"Failed to get HostGAPlugin health with CorrelationId={correlationId}.", ex);
                }
            }
        }

        /// <summary>
        /// Construct the request to HostGAPlugin
        /// </summary>
        private HttpRequestMessage GetHostGAPluginRequest(string correlationId)
        {
            var httpRequestMessage = new HttpRequestMessage
            {
                Method = HttpMethod.Put,
                RequestUri = new Uri(WireServerStatusRequestUri), //HostGAPluginStatusRequestUri,
                Headers =
                {
                    { CorrelationIdHeaderName, correlationId },
                    { HostGAPluginContainerIdHeaderName, HostGAPluginContainerId },
                    { HostGAPluginHostConfigNameHeaderName, HostGAPluginHostConfigName }
                },
                Content = new StringContent(HostGAPluginClient.DummyHostGAPluginDummyStatusContent, System.Text.Encoding.UTF8, HostGAPluginContentType)
            };

            return httpRequestMessage;
        }
    }

    public static class AgentClientHelper
    {
        public static bool CompareNestedExceptions(HashSet<Type> exceptionSet, Exception exception)
        {
            if (exception is AggregateException ax)
            {
                foreach (var ix in ax.InnerExceptions)
                {
                    var innerExceptionType = ix.GetType();
                    return exceptionSet.Any(e => e.IsAssignableFrom(innerExceptionType));
                }
            }

            var exceptionType = exception.GetType();

            return exceptionSet.Any(e => e.IsAssignableFrom(exceptionType));
        }
    }

    public class WindowsServiceClientException : Exception
    {
        public WindowsServiceClientException(string message) : base(message)
        {
        }

        public WindowsServiceClientException(string message, Exception inner) : base(message, inner)
        {
        }

        protected WindowsServiceClientException(
            SerializationInfo info,
            StreamingContext context) : base(info, context)
        {
        }
    }
}
