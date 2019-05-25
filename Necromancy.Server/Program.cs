using System;
using System.Net;
using System.Text;
using Arrowgene.Services.Buffers;
using Arrowgene.Services.Logging;
using Arrowgene.Services.Networking.Tcp;
using Arrowgene.Services.Networking.Tcp.Consumer;
using Arrowgene.Services.Networking.Tcp.Server.AsyncEvent;

namespace Necromancy.Server
{
    internal class Program
    {
        private static void Main(string[] args) => new Program();

        private object _consoleLock;

        public Program()
        {
            _consoleLock = new object();
            LogProvider.GlobalLogWrite += LogProviderOnGlobalLogWrite;
            AsyncEventServer login = new AsyncEventServer(IPAddress.Any, 60000, new LoginServer());
            AsyncEventServer world = new AsyncEventServer(IPAddress.Any, 12849, new WorldServer());
            login.Start();
            world.Start();
            Console.WriteLine("Press any key to exit..");
            Console.ReadKey();
            login.Stop();
            world.Stop();
            
            //12849
            //12594
        }

        public class LoginServer : Server
        {
            public override void OnReceivedData(ITcpSocket socket, byte[] data)
            {
                IBuffer buffer = new StreamBuffer(data);
                PacketLogIn(buffer);
                buffer.SetPositionStart();

                int size = buffer.ReadInt16(Endianness.Big);
                int opCode = buffer.ReadInt16(Endianness.Big);

                switch (@opCode)
                {
                    case 0x0557:
                    {
                        int minor = buffer.ReadInt32();
                        int major = buffer.ReadInt32();
                        IBuffer res = new StreamBuffer();
                        res.WriteInt32(0);
                        res.WriteCString("127.0.0.1");
                        res.WriteCString("127.0.0.1");
                        // Send(socket, 0x0AEC, res);
                        // Send(socket, 0xDDEF, res); //"network::proto_auth_implement_client::recv_base_check_version_r()"
                        // Send(socket, 0x17B7, res);
                        // - Send(socket, 0xD6D2, res);
                        // - Send(socket, 0x73BA, res);
                        // - Send(socket, 0xEA7E, res);
                        // - Send(socket, 0xC715, res);
                        // - Send(socket, 0x4A17, res);
                        Send(socket, 0x8C84, res); //"network::proto_auth_implement_client::recv_base_select_world_r()"
                        break;
                    }
                    case 0x93AD:
                    {
                        string accountName = buffer.ReadCString();
                        string password = buffer.ReadCString();
                        string macAddress = buffer.ReadCString();
                        int unknown = buffer.ReadInt16();
                        _logger.Info($"[Login]Account:{accountName} Password:{password} Unknown:{unknown}");
                        IBuffer res = new StreamBuffer();
                        res.WriteInt32(0);
                        res.WriteInt32(1);
                        res.WriteInt32(0);
                        // Send(socket, 0xEFDD, res);
                        break;
                    }
                    default:
                    {
                        _logger.Error($"[Login]OPCode: {opCode} not handled");
                        break;
                    }
                }
            }

            public override void OnClientDisconnected(ITcpSocket socket)
            {
                _logger.Info("[Login]Client Disconnected");
            }

            public override void OnClientConnected(ITcpSocket socket)
            {
                _logger.Info("[Login]Client Connected");
            }            
            
            protected override void PacketLogIn(IBuffer packet)
            {
                PacketLog(packet, "IN", "Login");
            }

            protected override void PacketLogOut(IBuffer packet)
            {
                PacketLog(packet, "OUT", "Login");
            }
        }

        public class WorldServer : Server
        {
            public override void OnReceivedData(ITcpSocket socket, byte[] data)
            {
                IBuffer buffer = new StreamBuffer(data);
                PacketLogIn(buffer);
                buffer.SetPositionStart();

                int size = buffer.ReadInt16(Endianness.Big);
                int opCode = buffer.ReadInt16(Endianness.Big);

                switch (@opCode)
                {
                    default:
                    {
                        _logger.Error($"[World]OPCode: {opCode} not handled");
                        break;
                    }
                }
            }

            public override void OnClientDisconnected(ITcpSocket socket)
            {
                _logger.Info("[World]Client Disconnected");
            }

            public override void OnClientConnected(ITcpSocket socket)
            {
                _logger.Info("[World]Client Connected");
            }

            protected override void PacketLogIn(IBuffer packet)
            {
              PacketLog(packet, "IN", "World");
            }

            protected override void PacketLogOut(IBuffer packet)
            {
                PacketLog(packet, "OUT", "World");
            }
        }


        public abstract class Server : IConsumer
        {
            protected ILogger _logger;

            public Server()
            {
                _logger = LogProvider.Logger(this);
            }

            public void OnStart()
            {
            }

            public void OnStarted()
            {
            }

            public void OnStop()
            {
            }

            public void OnStopped()
            {
            }

            protected void Send(ITcpSocket socket, ushort opCode, IBuffer response)
            {
                byte[] payload = response.GetAllBytes();
                int opSize = 2;
                int size = payload.Length + opSize;
                IBuffer buffer = new StreamBuffer();
                buffer.SetPositionStart();
                buffer.WriteInt16((short) size, Endianness.Big);
                buffer.WriteInt16(opCode, Endianness.Big);
                buffer.WriteBytes(payload);
                socket.Send(buffer.GetAllBytes());
                PacketLogOut(buffer);
            }

            protected abstract void PacketLogIn(IBuffer packet);

            protected abstract void PacketLogOut(IBuffer packet);

            protected void PacketLog(IBuffer packet, string tag, string name)
            {
                packet.SetPositionStart();
                int size = packet.ReadInt16(Endianness.Big);
                int opCode = packet.ReadInt16(Endianness.Big);
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("Packet Log:");
                sb.AppendLine($"[{name}][Type:{tag}][TotalSize:{packet.Size}] Header:[Size:{size}][OPCode:{opCode}]");
                sb.AppendLine("------------------------------------------------------------");
                sb.AppendLine(packet.ToAsciiString(true));
                sb.AppendLine(packet.ToHexString(' '));
                sb.AppendLine("------------------------------------------------------------");
                _logger.Write(LogLevel.Debug, tag, sb.ToString());
            }

            public abstract void OnReceivedData(ITcpSocket socket, byte[] data);
            public abstract void OnClientDisconnected(ITcpSocket socket);
            public abstract void OnClientConnected(ITcpSocket socket);
        }

        private void LogProviderOnGlobalLogWrite(object sender, LogWriteEventArgs logWriteEventArgs)
        {
            ConsoleColor consoleColor = ConsoleColor.Gray;
            switch (logWriteEventArgs.Log.LogLevel)
            {
                case LogLevel.Debug:
                    consoleColor = ConsoleColor.Yellow;
                    break;
                case LogLevel.Info:
                    consoleColor = ConsoleColor.Cyan;
                    break;
                case LogLevel.Error:
                    consoleColor = ConsoleColor.Red;
                    break;
            }

            object tag = logWriteEventArgs.Log.Tag;
            if (tag is string)
            {
                switch (tag)
                {
                    case "IN":
                        consoleColor = ConsoleColor.Green;
                        break;
                    case "OUT":
                        consoleColor = ConsoleColor.Blue;
                        break;
                }
            }

            lock (_consoleLock)
            {
                Console.ForegroundColor = consoleColor;
                Console.WriteLine(logWriteEventArgs.Log);
                Console.ResetColor();
            }
        }
    }
}