using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Channels;

internal sealed class TestSmtpServer : IDisposable
{
    private readonly TcpListener listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource stop = new();
    private readonly Channel<string> messages = Channel.CreateUnbounded<string>();
    private readonly Task loop;
    public int Port => ((IPEndPoint)listener.LocalEndpoint).Port;
    public int MessageCount { get; private set; }

    public TestSmtpServer() { listener.Start(); loop = Run(); }
    public Task<string> ReadMessage() => messages.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));

    private async Task Run()
    {
        try
        {
            while (!stop.IsCancellationRequested)
            {
                using var client = await listener.AcceptTcpClientAsync(stop.Token);
                using var stream = client.GetStream();
                using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
                using var writer = new StreamWriter(stream, Encoding.ASCII, leaveOpen: true) { AutoFlush = true, NewLine = "\r\n" };
                await writer.WriteLineAsync("220 localhost test SMTP");
                while (await reader.ReadLineAsync(stop.Token) is { } line)
                {
                    if (line.StartsWith("EHLO", StringComparison.OrdinalIgnoreCase)) await writer.WriteLineAsync("250 localhost");
                    else if (line.StartsWith("HELO", StringComparison.OrdinalIgnoreCase)) await writer.WriteLineAsync("250 localhost");
                    else if (line.StartsWith("MAIL FROM:", StringComparison.OrdinalIgnoreCase) || line.StartsWith("RCPT TO:", StringComparison.OrdinalIgnoreCase)) await writer.WriteLineAsync("250 OK");
                    else if (line == "DATA")
                    {
                        await writer.WriteLineAsync("354 End with a dot");
                        var content = new StringBuilder();
                        while (await reader.ReadLineAsync(stop.Token) is { } data && data != ".") content.AppendLine(data.StartsWith("..") ? data[1..] : data);
                        MessageCount++;
                        await messages.Writer.WriteAsync(content.ToString(), stop.Token);
                        await writer.WriteLineAsync("250 Queued");
                    }
                    else if (line == "QUIT") { await writer.WriteLineAsync("221 Bye"); break; }
                    else await writer.WriteLineAsync("250 OK");
                }
            }
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
        catch (SocketException) when (stop.IsCancellationRequested) { }
    }

    public void Dispose()
    {
        stop.Cancel(); listener.Stop();
        loop.GetAwaiter().GetResult();
        stop.Dispose();
    }
}
