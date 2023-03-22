using System.Buffers;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Client
{
    public class Program
    {
        private static async Task Main(string[] args)
        {
            Socket clientSocket = new Socket(SocketType.Stream, ProtocolType.Tcp);
            await clientSocket.ConnectAsync(new IPEndPoint(IPAddress.Loopback, 8080));
            _ = ConnectedAsync(clientSocket);
        }

        private static async Task ConnectedAsync(Socket socket)
        {
            Console.WriteLine($"[{socket.RemoteEndPoint}]: connected");

            var stream = new NetworkStream(socket);
            var reader = PipeReader.Create(stream);
            var writer = PipeWriter.Create(stream);

            await Task.WhenAll(ReceiveAsync(reader), SendAsync(writer));

            await reader.CompleteAsync();
            await writer.CompleteAsync();

            Console.WriteLine($"[{socket.RemoteEndPoint}]: disconnected");
        }

        private static async Task SendAsync(PipeWriter writer)
        {
            while (true)
            {
                var buffer = Console.ReadLine();
                if (buffer == null)
                {
                    continue;
                }
                buffer += '\0';

                byte[] bytes = Encoding.UTF8.GetBytes(buffer);
                FlushResult result = await writer.WriteAsync(bytes);
                if (result.IsCompleted)
                {
                    break;
                }
            }
        }
        private static async Task ReceiveAsync(PipeReader reader)
        {
            while (true)
            {
                ReadResult result = await reader.ReadAsync();
                ReadOnlySequence<byte> buffer = result.Buffer;

                while (TryReadLine(ref buffer, out ReadOnlySequence<byte> line))
                {
                    ProcessLine(line);
                }

                reader.AdvanceTo(buffer.Start, buffer.End);

                if (result.IsCompleted)
                {
                    break;
                }
            }
        }

        private static bool TryReadLine(ref ReadOnlySequence<byte> buffer, out ReadOnlySequence<byte> line)
        {
            SequencePosition? position = buffer.PositionOf((byte)'\n') ?? buffer.PositionOf((byte)'\0');

            if (position == null)
            {
                if (buffer.Length > 0)
                {
                    line = buffer.Slice(0, buffer.Length);
                    buffer = buffer.Slice(buffer.Length);
                    return true;
                }

                line = default;
                return false;
            }

            line = buffer.Slice(0, position.Value);
            buffer = buffer.Slice(buffer.GetPosition(1, position.Value));
            return true;
        }

        private static void ProcessLine(in ReadOnlySequence<byte> buffer)
        {
            foreach (var segment in buffer)
            {
                Console.Write(Encoding.UTF8.GetString(segment.Span));
            }
            Console.WriteLine();
        }
    }
}
