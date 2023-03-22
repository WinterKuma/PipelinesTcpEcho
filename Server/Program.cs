using System.Buffers;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Server
{
    public class Program
    {
        private static async Task Main(string[] args)
        {
            Socket serverSockeet = new Socket(SocketType.Stream, ProtocolType.Tcp);
            serverSockeet.Bind(new IPEndPoint(IPAddress.Any, 8080));
            serverSockeet.Listen((int)SocketOptionName.MaxConnections);

            try
            {
                while (true)
                {
                    if (serverSockeet is null)
                    {
                        break;
                    }

                    var socket = await serverSockeet.AcceptAsync();
                    _ = AcceptedAsync(socket);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        private static async Task AcceptedAsync(Socket socket)
        {
            Console.WriteLine($"[{socket.RemoteEndPoint}]: connected");

            NetworkStream stream = new(socket);
            PipeReader reader = PipeReader.Create(stream);
            PipeWriter writer = PipeWriter.Create(stream);

            await ReceiveAsync(reader, writer);

            await reader.CompleteAsync();
            await writer.CompleteAsync();

            Console.WriteLine($"[{socket.RemoteEndPoint}]: disconnected");
        }

        private static async Task SendAsync(PipeWriter writer, ReadOnlySequence<byte> buffer)
        {
            foreach (var segment in buffer)
            {
                FlushResult result = await writer.WriteAsync(segment);
                if (result.IsCompleted)
                {
                    break;
                }
            }
        }
        private static async Task ReceiveAsync(PipeReader reader, PipeWriter writer)
        {
            while (true)
            {
                ReadResult result = await reader.ReadAsync();
                ReadOnlySequence<byte> buffer = result.Buffer;

                while (TryReadLine(ref buffer, out ReadOnlySequence<byte> line))
                {
                    _ = SendAsync(writer, line);
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
