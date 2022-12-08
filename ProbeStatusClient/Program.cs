namespace ProbeStatusClient
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    internal class Program
    {
        private static readonly CancellationTokenSource canToken = new CancellationTokenSource();

        static async Task Main(string[] args)
        {
            Console.CancelKeyPress += OnCancelled;
            var client = new HostGAPluginClient();
            while (!canToken.IsCancellationRequested)
            {
                try
                {
                    var state = await client.GetState();
                    Console.WriteLine($"GAP state: {state}");
                    await Task.Delay(TimeSpan.FromSeconds(10), canToken.Token);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                    await Task.Delay(TimeSpan.FromMinutes(1), canToken.Token);
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
