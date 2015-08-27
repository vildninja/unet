using System;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine.Networking;

using Random = UnityEngine.Random;

namespace VildNinja.Net
{
    public class RelayServer : MonoBehaviour
    {
        // Keep track of all connected clients
        private Dictionary<Client, string> clients;
        private Dictionary<Client, Client> matches;
        private List<Client> queue;

        private int infoChannel;
        private int gameChannel;
        private int udpHost;
        private int webHost;

        private byte[] data;
        private MemoryStream stream;
        private BinaryReader reader;
        private BinaryWriter writer;


        // Use this for initialization
        private void Start()
        {
            clients = new Dictionary<Client, string>();
            matches = new Dictionary<Client, Client>();
            queue = new List<Client>();

            NetworkTransport.Init();

            // In many cases Unreliable or StateUpdate would be a better fit for
            // sending game states, but since I want this to work seemless with
            // WebGL everything will be reliable in order any ways.
            var config = new ConnectionConfig();
            config.PacketSize = 1000;
            infoChannel = config.AddChannel(QosType.ReliableSequenced);
            gameChannel = config.AddChannel(QosType.ReliableSequenced);

            // Setup the two hosts (one for reqular builds and one for WebGL)
            var topology = new HostTopology(config, 200);
            udpHost = NetworkTransport.AddHost(topology, 19387);
            webHost = NetworkTransport.AddWebsocketHost(topology, 8387);

            // That is it. The server is now up and running
            Log.Line("Started the server on ports udp:19387 and web:8387");

            // Setup the data buffer:
            data = new byte[config.PacketSize];
            // And setup a reader and writer for easy data manipulation
            stream = new MemoryStream(data);
            reader = new BinaryReader(stream);
            writer = new BinaryWriter(stream);
        }

        // Update is called once per frame
        private void Update()
        {
            // Fail safe to avoid freezing the entire game in case of flooded buffers
            for (int i = 0; i < 100; i++)
            {
                Client client;
                int channel;
                int size;
                byte error;

                stream.Position = 0;
                var status = NetworkTransport.Receive(out client.host, out client.conn,
                    out channel, data, data.Length, out size, out error);

                Log.TestError(error, "Server loop failed to receive message!");

                switch (status)
                {
                    case NetworkEventType.DataEvent:
                        if (channel == infoChannel)
                        {
                            InfoMessage(client);
                        }
                        else if (channel == gameChannel)
                        {
                            Relay(client, size);
                        }
                        break;
                    case NetworkEventType.ConnectEvent:
                        Connect(client);
                        break;
                    case NetworkEventType.DisconnectEvent:
                        Disconnect(client);
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

            while (queue.Count >= 2)
            {
                Fight(queue[0], queue[1]);
                queue.RemoveRange(0, 2);
            }
        }

        private void SendMessage(Client client, bool isInfo)
        {
            byte error;
            NetworkTransport.Send(client.host, client.conn, isInfo ? infoChannel : gameChannel, data, (int) stream.Position, out error);
            Log.TestError(error, "Sending " + (isInfo ? "info" : "game") + " message to " + client + " failed!");
        }

        private void Disconnect(Client client)
        {
            string clientName;

            // Get name and test if client is even connected
            if (!clients.TryGetValue(client, out clientName))
                return;

            clients.Remove(client);

            // Make sure to stop current matches
            Client other;
            if (matches.TryGetValue(client, out other))
            {
                matches.Remove(client);
                matches.Remove(other);
            }

            // Remove client from match queue
            queue.Remove(client);

            Log.Line(client + " (" + clientName + ") left the game");
        }

        private void Connect(Client client)
        {
            if (clients.ContainsKey(client))
                return;

            clients.Add(client, client.conn + "@" + (client.host == webHost ? "web" : "udp"));
            Log.Line(client + " connected to server on " + (client.host == webHost ? "web" : "udp"));
        }

        private readonly byte[] map = new byte[32];
        private void Fight(Client a, Client b)
        {
            matches.Add(a, b);
            matches.Add(b, a);

            // Generate a random map for each fight
            for (int i = 0; i < map.Length; i++)
            {
                map[i] = (byte) Random.Range(3, 8);
            }

            stream.Position = 0;
            writer.Write("fight");
            writer.Write(clients[b]);
            writer.Write((byte)3);
            writer.Write((byte)map.Length);
            writer.Write(map);
            SendMessage(a, true);


            stream.Position = 0;
            writer.Write("fight");
            writer.Write(clients[a]);
            writer.Write((byte)map.Length - 4);
            writer.Write((byte)map.Length);
            writer.Write(map);
            SendMessage(b, true);
        }

        private void InfoMessage(Client client)
        {
            var msg = reader.ReadString();

            switch (msg)
            {
                case "name":
                    SetName(client, reader.ReadString());
                    break;
                case "queue":
                    JoinMathcQueue(client);
                    break;
                case "victory":
                    MatchWon(client);
                    break;
            }
        }

        private void SetName(Client client, string name)
        {
            if (!string.IsNullOrEmpty(name) && clients.ContainsKey(client))
            {
                if (name.Length > 20)
                    name = name.Substring(0, 20);
                name = name.Trim();
                clients[client] = name;
                Log.Line(client + " changed name to " + name);
            }
        }

        private void JoinMathcQueue(Client client)
        {
            if (!clients.ContainsKey(client) || matches.ContainsKey(client) || queue.Contains(client))
                return;
            queue.Add(client);
            Log.Line(client + " joint the match queue");
        }

        private void MatchWon(Client client)
        {
            Client other;
            if (matches.TryGetValue(client, out other))
            {
                stream.Position = 0;
                writer.Write("victory");
                SendMessage(client, true);
                stream.Position = 0;
                writer.Write("defeat");
                SendMessage(other, true);

                matches.Remove(client);
                matches.Remove(other);

                Log.Line("Match over " + client + " won over " + other);
            }
        }

        private void Relay(Client client, int length)
        {
            Client other;
            if (matches.TryGetValue(client, out other))
            {
                stream.Position = length;
                SendMessage(other, false);
            }
            else
            {
                stream.Position = 0;
                writer.Write("dropped");
                SendMessage(client, true);

                Log.Line("Match dropped " + client);
            }
        }
    }
}