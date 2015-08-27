using System;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine.Networking;

namespace VildNinja.Net
{
    public class RelayClient : IDisposable
    {
        private int infoChannel;
        private int gameChannel;
        private Client server;

        public bool IsConnected { get; private set; }

#if UNITY_WEBGL
        private const int port = 8387;
#else
        private const int port = 19387;
#endif

        private byte[] data;
        private MemoryStream stream;
        private BinaryReader reader;
        private BinaryWriter writer;

        private bool inMatch;

        public event Action OnConnected = delegate { };
        public event Action OnDisconnected = delegate { };
        public event Action<string, byte, byte[]> OnMatchStarted = delegate {  };
        public event Action<BinaryReader> OnGameDataReceived = delegate { };
        public event Action<FightResult> OnMatchEnded = delegate { };

        public RelayClient()
        {
            IsConnected = false;
        }
        
        public void Connect(string ip)
        {
            NetworkTransport.Init();

            // Config must be identical with the server
            var config = new ConnectionConfig();
            config.PacketSize = 1000;
            infoChannel = config.AddChannel(QosType.ReliableSequenced);
            gameChannel = config.AddChannel(QosType.ReliableSequenced);

            // Setup the host socket (yes also for clients)
            // Using port 0 to let the system assign a random available port
            var topology = new HostTopology(config, 200);
            server.host = NetworkTransport.AddHost(topology, 0);

            byte error;
            server.conn = NetworkTransport.Connect(server.host, ip, port, 0, out error);

            Log.TestError(error, "Failed to connect to the server!");

            // Setup the data buffer:
            data = new byte[config.PacketSize];
            // And setup a reader and writer for easy data manipulation
            stream = new MemoryStream(data);
            reader = new BinaryReader(stream);
            writer = new BinaryWriter(stream);
        }

        private void SendMessage(bool isInfo)
        {
            byte error;
            NetworkTransport.Send(server.host, server.conn, isInfo ? infoChannel : gameChannel, data, (int)stream.Position, out error);
            Log.TestError(error, "Sending " + (isInfo ? "info" : "game") + " message to server failed!");
        }

        public void Poll()
        {
            for (int i = 0; i < 100; i++)
            {
                Client client;
                int channel;
                int size;
                byte error;

                stream.Position = 0;
                var status = NetworkTransport.Receive(out client.host, out client.conn,
                    out channel, data, data.Length, out size, out error);

                Log.TestError(error, "Client loop failed to receive message!");

                switch (status)
                {
                    case NetworkEventType.DataEvent:
                        if (channel == infoChannel)
                        {
                            InfoMessage();
                        }
                        else if (channel == gameChannel)
                        {
                            OnGameDataReceived(reader);
                        }
                        break;
                    case NetworkEventType.ConnectEvent:
                        IsConnected = true;
                        OnConnected();
                        break;
                    case NetworkEventType.DisconnectEvent:
                        IsConnected = false;
                        OnDisconnected();
                        break;
                    case NetworkEventType.Nothing:
                        // Stop the for loop
                        i = 100;
                        break;
                    case NetworkEventType.BroadcastEvent:
                        // Not used in this example
                        break;
                }
            }
        }

        private void InfoMessage()
        {
            var msg = reader.ReadString();

            switch (msg)
            {
                case "victory":
                    if (inMatch)
                    {
                        OnMatchEnded(FightResult.Victory);
                        inMatch = false;
                    }
                    break;
                case "dropped":
                    if (inMatch)
                    {
                        OnMatchEnded(FightResult.Dropped);
                        inMatch = false;
                    }
                    break;
                case "defeat":
                    if (inMatch)
                    {
                        OnMatchEnded(FightResult.Defeat);
                        inMatch = false;
                    }
                    break;
                case "fight":
                    string opponent = reader.ReadString();
                    byte position = reader.ReadByte();
                    byte length = reader.ReadByte();
                    byte[] map = reader.ReadBytes(length);
                    OnMatchStarted(opponent, position, map);
                    inMatch = true;
                    break;
            }
        }

        public void SetName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return;

            if (name.Length > 20)
                name = name.Substring(0, 20);
            name = name.Trim();

            stream.Position = 0;
            writer.Write("name");
            writer.Write(name);

            SendMessage(true);
        }

        public void JoinMatchQueue()
        {
            stream.Position = 0;
            writer.Write("queue");

            SendMessage(true);
        }


        // Yes this is very easy to hack!
        public void WonTheGame()
        {
            inMatch = false;

            stream.Position = 0;
            writer.Write("victory");

            SendMessage(true);
        }

        public void SendGameData(Action<BinaryWriter> poll)
        {
            stream.Position = 0;
            poll(writer);

            SendMessage(false);
        }

        public void Dispose()
        {
            if (NetworkTransport.IsStarted)
            {
                byte error;
                NetworkTransport.Disconnect(server.host, server.conn, out error);
                NetworkTransport.Shutdown();

                stream.Dispose();
            }
        }
    }
}