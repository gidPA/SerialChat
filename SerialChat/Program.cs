using System;
using System.IO.Ports;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SerialChat
{
    class Program
    {
        static async Task Main(string[] args)
        {
            const string defaultPortName = "/dev/serial0";
            int baudRate = 9600;

            // Parse CLI arguments for baud rate
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--baud-rate" && i + 1 < args.Length)
                {
                    if (int.TryParse(args[i + 1], out int parsedBaud))
                    {
                        baudRate = parsedBaud;
                    }
                    else
                    {
                        Console.WriteLine($"Invalid baud rate: {args[i + 1]}. Using default {baudRate}.");
                    }
                }
            }

            SerialPort serialPort = new SerialPort(defaultPortName, baudRate)
            {
                Parity = Parity.None,
                DataBits = 8,
                StopBits = StopBits.One,
                Handshake = Handshake.None,
                ReadTimeout = 500,
                WriteTimeout = 500
            };

            try
            {
                serialPort.Open();
                Console.WriteLine($"Connected to {defaultPortName} at {baudRate} bps.");
                Console.WriteLine("Type messages and press Enter to send.\n");

                using CancellationTokenSource cts = new CancellationTokenSource();

                // Receiving task
                Task receiveTask = Task.Run(async () =>
                {
                    while (!cts.Token.IsCancellationRequested)
                    {
                        try
                        {
                            string incoming = await ReadLineWithACKAsync(serialPort, cts.Token);
                            if (!string.IsNullOrEmpty(incoming))
                            {
                                Console.ForegroundColor = ConsoleColor.Green;
                                Console.WriteLine($"\rScanner: {incoming}");
                                Console.ResetColor();
                                Console.Write("You: ");
                            }
                        }
                        catch (OperationCanceledException)
                        {
                            // Graceful cancellation
                            break;
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[Receive Error] {ex.Message}");
                            break;
                        }
                    }
                }, cts.Token);

                // Sending loop
                while (true)
                {
                    Console.Write("You: ");
                    string? message = Console.ReadLine();
                    if (string.IsNullOrWhiteSpace(message)) continue;

                    if (message[0] == '!')
                    {
                        string controlMessage = message[1..];
                        if (controlMessage == "exit")
                        {
                            Console.WriteLine("Exiting the application...");
                            break;
                        }
                        else if (controlMessage == "activate" || controlMessage == "a")
                        {
                            message = "\x16" + "T" + "\x0D";
                        }
                        else if (controlMessage == "deactivate" || controlMessage == "da")
                        {
                            message = "\x16" + "U" + "\x0D";
                        }
                    }
                    else if (message[0] == '?')
                    {
                        string controlMessage = message[1..];
                        message = "\x16" + "M" + "\x0D" + controlMessage;
                    }
                    try
                    {
                        serialPort.WriteLine(message);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[Send Error] {ex.Message}");
                        break;
                    }
                }

                // Stop receive task
                cts.Cancel();
                await receiveTask;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Connection Error] {ex.Message}");
            }
            finally
            {
                if (serialPort.IsOpen)
                    serialPort.Close();
            }
        }

        /// <summary>
        /// Reads a line from the serial port that can be terminated by either:
        /// - Carriage Return (\r)
        /// - Line Feed (\n)
        /// - ASCII ACK character (\x06)
        /// - ASCII NAK (\x15)
        /// - ASCII ENQ (\x05)
        /// </summary>
        static async Task<string> ReadLineWithACKAsync(SerialPort port, CancellationToken token)
        {
            byte[] buffer = new byte[1];
            StringBuilder lineBuffer = new StringBuilder();

            while (!token.IsCancellationRequested)
            {
                int bytesRead = await port.BaseStream.ReadAsync(buffer, 0, 1, token);
                if (bytesRead == 0) // No more data
                    continue;

                char ch = (char)buffer[0];

                // Termination characters
                if (ch == '\r' || ch == '\n' || ch == '\x06' || ch == '\x15' || ch == '\x05')
                {
                    string result = lineBuffer.ToString();

                    if (ch == '\x15')
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("[NAK Received: Bad command, or out of range command parameters]");
                        Console.ResetColor();
                    }
                    if (ch == '\x05')
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("[ENQ Received: Bad command]");
                        Console.ResetColor();
                    }

                    return result;
                }
                else
                {
                    lineBuffer.Append(ch);
                }
            }

            return string.Empty;
        }
    }
}
