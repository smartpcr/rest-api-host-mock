namespace RestApiHost
{
    using Fclp;
    using System.Web.Http;
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    internal class Program
    {
        private static readonly CancellationTokenSource canToken = new CancellationTokenSource();

        static async Task Main(string[] args)
        {
            var p = new FluentCommandLineParser<AppArgs>();
            p.Setup(arg => arg.IpAddress).As('h', "host").SetDefault("localhost");
            p.Setup(arg=>arg.Port).As('p', "port").Required();
            var result = p.Parse(args);
            if (!result.HasErrors)
            {
                await  RunAsync(p.Object, canToken.Token);
            }
        }

        private static async Task RunAsync(AppArgs args, CancellationToken cancel)
        {
            Console.CancelKeyPress += OnCancelled;

            Action<HttpConfiguration> act = config =>
            {
                config.MessageHandlers.Clear();
                config.MessageHandlers.Add(new InterceptHandler());
            };

            using (var testHost = new ApiHost(args.IpAddress, args.Port, act))
            {
                await testHost.Start();
                while (!cancel.IsCancellationRequested)
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), cancel);
                }
            }
        }

        private static void OnCancelled(object sender, ConsoleCancelEventArgs e)
        {
            if (e.SpecialKey == ConsoleSpecialKey.ControlC)
            {
                Console.WriteLine("Cancel event triggered");
                canToken.Cancel();
                e.Cancel = true;
            }
        }
    }
}
