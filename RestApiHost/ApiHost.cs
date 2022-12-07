// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ApiHost.cs" company="Microsoft Corporation">
//   Copyright (c) 2020 Microsoft Corporation.  All rights reserved.
// </copyright>
// <summary>
// </summary>
// --------------------------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http.SelfHost;
using System.Web.Http;

namespace RestApiHost
{
    using System.Threading;

    public class ApiHost : IDisposable
    {
        private readonly string HostFile = Path.Combine(Environment.SystemDirectory, @"drivers\etc\hosts");
        private readonly string BackupHostFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "hosts");
        private HttpSelfHostServer server;
        private readonly string ipAddress;
        private readonly int port;
        private readonly Action<HttpConfiguration> registeredAction;

        public ApiHost(string ip, int port, Action<HttpConfiguration> actions)
        {
            this.ipAddress = ip;
            this.port = port;
            this.registeredAction = actions;
        }

        public async Task Start()
        {
            if (ipAddress != "127.0.0.1" && !ipAddress.Equals("localhost", StringComparison.OrdinalIgnoreCase))
            {
                SetupRedirectionOnHostFile(ipAddress);
            }

            var configuration = new HttpSelfHostConfiguration($"http://localhost:{port}");
            registeredAction(configuration);
            server = new HttpSelfHostServer(configuration);
            await server.OpenAsync();
            Console.WriteLine("api server started on port " + port);
        }

        public async Task Stop()
        {
            RestoreHostFile();

            if (server == null) return;

            await server.CloseAsync();
            server.Dispose();
            server = null;

            Console.WriteLine("api server stopped");
        }

        public void Dispose()
        {
            Stop().Wait();
            GC.SuppressFinalize(this);
        }

        private void SetupRedirectionOnHostFile(string remoteIp)
        {
            if (File.Exists(BackupHostFile))
            {
                File.Delete(BackupHostFile);
            }

            File.Copy(HostFile, BackupHostFile, true);
            var hostFileContent = File.ReadAllText(HostFile);
            var buffer = new StringBuilder(hostFileContent);
            buffer.AppendLine();
            buffer.AppendLine($"{ipAddress}     localhost");
            File.WriteAllText(HostFile, buffer.ToString());
        }

        private void RestoreHostFile()
        {
            if (File.Exists(BackupHostFile))
            {
                File.Copy(BackupHostFile, HostFile, true);
            }
        }
    }
}
