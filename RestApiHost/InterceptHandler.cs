// --------------------------------------------------------------------------------------------------------------------
// <copyright file="InterceptHandler.cs" company="Microsoft Corporation">
//   Copyright (c) 2020 Microsoft Corporation.  All rights reserved.
// </copyright>
// <summary>
// </summary>
// --------------------------------------------------------------------------------------------------------------------

using System.Collections.Generic;
using System.Net.Http;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace RestApiHost
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.ObjectModel;
    using System.Linq;

    internal class InterceptHandler : DelegatingHandler
    {
        private readonly ConcurrentBag<InterceptedRequest> interceptedRequests;
        private long reqid;

        public InterceptHandler()
        {
            reqid = 0;
            interceptedRequests = new ConcurrentBag<InterceptedRequest>();
        }

        public IReadOnlyList<InterceptedRequest> InterceptedRequests =>
            new ReadOnlyCollection<InterceptedRequest>(interceptedRequests.ToArray());

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Argument.NotNull(nameof(request), request);
            var req = new InterceptedRequest()
            {
                RequestUri = request.RequestUri.ToString(),
                Method = request.Method,
                Headers = new Dictionary<string, string>()
            };
            if (request.Content != null)
            {
                req.Body = await request.Content.ReadAsStringAsync();
            }
            if (request.Headers != null)
            {
                foreach (var kvp in request.Headers)
                {
                    req.Headers.Add(kvp.Key, string.Join(",", kvp.Value));
                }
            }
            interceptedRequests.Add(req);
            Console.WriteLine($"{DateTime.Now} {Interlocked.Increment(ref reqid)}");
            Console.WriteLine(req);

            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }

    internal class InterceptedRequest
    {
        public HttpMethod Method { get; set; }
        public string RequestUri { get; set; }
        public string Body { get; set; }
        public Dictionary<string, string> Headers { get; set; }

        public override string ToString()
        {
            return $"{Method} {RequestUri}\n{string.Join(",", Headers.Select(h => $"{h.Key}={h.Value}"))}\n\n{Body}";
        }
    }
}
