using System;
using System.IO.Ports;
using System.Threading;

namespace SerialChat
{
    class Program
    {
        static void Main(string[] args)
        {
            const string portName = "/dev/serial0";
            const int baudRate = 9600;

            SerialPort serialPort = new SerialPort(portName, baudRate)
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
                Console.WriteLine($"Connected to {portName} at {baudRate} bps.");
                Console.WriteLine("Type messages and press Enter to send.\n");

                // Thread for receiving messages
                Thread receiveThread = new Thread(() =>
                {
                    while (true)
                    {
                        try
                        {
                            string incoming = serialPort.ReadLine();
                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.WriteLine($"\nFriend: {incoming}");
                            Console.ResetColor();
                            Console.Write("You: ");
                        }
                        catch (TimeoutException)
                        {
                            // Ignore read timeouts
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[Receive Error] {ex.Message}");
                            Console.WriteLine(ex);
                            break;
                        }
                    }
                });

                receiveThread.IsBackground = true;
                receiveThread.Start();

                // Sending loop
                while (true)
                {
                    Console.Write("You: ");
                    string message = Console.ReadLine();
                    if (string.IsNullOrWhiteSpace(message)) continue;

                    if (message[0] == '!')
                    {
                        string controlMessage = message[1..];
                        if (controlMessage == "exit")
                        {
                            Console.WriteLine("Exiting the application...");
                            break;
                        }
                        else if (controlMessage == "activate")
                        {
                            message = "\x16" + "T" + "\x0D";
                        }
                        else if (controlMessage == "deactivate")
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
    }
}