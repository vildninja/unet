using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using VildNinja.Net;

namespace VildNinja.Game
{
    public class TwoPlayerGame : MonoBehaviour
    {
        public string ip;


        private RelayClient net;

        public Brick brickPrefab;

        public Rigidbody2D me;
        public Rigidbody2D other;

        private Brick[,] grid;

        private List<Brick> changes = new List<Brick>();

        [Header("UI and menus")]
        public GameObject[] panels;

        private bool nameSet = false;
        public InputField nameField;
        public Button submitName;
        public Text matchResultText;

        void Start()
        {
            // setup menu
            nameField.onEndEdit.AddListener(SetName);
            submitName.onClick.AddListener(() => SetName(nameField.text));

            ShowPanel("Connecting");



            // setup networking
            net = new RelayClient();

            net.OnConnected += ConnectedToServer;
            net.OnDisconnected += DisconnectedFromServer;
            net.OnMatchStarted += StartMatch;
            net.OnMatchEnded += EndMatch;
            net.OnGameDataReceived += ReceiveGameData;

            net.Connect(ip);

            //var map = new byte[32];
            //for (int i = 0; i < map.Length; i++)
            //{
            //    map[i] = (byte)UnityEngine.Random.Range(3, 8);
            //}
            //StartMatch("my", 3, map);
        }

        private void ShowPanel(string panel)
        {
            for (int i = 0; i < panels.Length; i++)
            {
                panels[i].SetActive(panels[i].name == panel);
            }
        }

        private float syncTimer = 0;
        void Update()
        {
            net.Poll();

            if (net.IsConnected)
            {
                syncTimer -= Time.deltaTime;
                if (syncTimer < 0 || changes.Count > 0)
                {
                    syncTimer = 0.1f;
                    net.SendGameData(DataWriter);
                }
            }
        }

        public void SetName(string name)
        {
            if (nameSet || name.Trim().Length == 0)
                return;

            Debug.Log("set name to " + name);
            net.SetName(name);

            net.JoinMatchQueue();

            ShowPanel("WaitingForPlayers");

            nameSet = true;
        }

        private void ConnectedToServer()
        {
            ShowPanel("EnterName");
        }

        private void DisconnectedFromServer()
        {
            ShowPanel("LostConnection");
        }

        private void StartMatch(string opponent, byte position, byte otherPosition, byte[] map)
        {
            other.name = opponent;

            // Delete the old map, if present
            if (grid != null)
            {
                for (int x = 0; x < grid.GetLength(0); x++)
                {
                    for (int y = 0; y < grid.GetLength(1); y++)
                    {
                        var b = grid[x, y];
                        if (b != null)
                        {
                            Destroy(b.gameObject);
                        }
                    }
                }
                grid = null;
            }

            Vector2 lowerLeft = new Vector2(-map.Length / 2f + 0.5f, 0);
            grid = new Brick[map.Length, 16];

            for (int x = 0; x < map.Length; x++)
            {
                for (int y = 0; y < 16; y++)
                {
                    var b = Instantiate(brickPrefab);
                    b.transform.position = lowerLeft + new Vector2(x, y);
                    b.x = (byte)x;
                    b.y = (byte)y;
                    b.health = (byte)(y <= map[x] ? 255 : 0);
                    grid[x, y] = b;
                }
            }


            me.position = lowerLeft + new Vector2(position, map[position] + 1);
            other.position = lowerLeft + new Vector2(otherPosition, map[otherPosition] + 1);

            me.isKinematic = false;
            other.isKinematic = false;

            ShowPanel(null);
        }

        private void EndMatch(FightResult result)
        {
            ShowPanel("MatchResult");

            switch (result)
            {
                case FightResult.Victory:
                    matchResultText.text = "VICTORY!\nYou defeated\n" + other.name;
                    break;
                case FightResult.Defeat:
                    matchResultText.text = "DEFEAT!\nYou lost to\n" + other.name;
                    break;
                case FightResult.Draw:
                    matchResultText.text = "DRAW!\nAgainst\n" + other.name;
                    break;
                case FightResult.Dropped:
                    matchResultText.text = "DROPPED!\n" + other.name + "\nTimed out";
                    break;
                default:
                    throw new ArgumentOutOfRangeException("result", result, null);
            }

            me.isKinematic = true;
            other.isKinematic = true;
        }

        private void ReceiveGameData(BinaryReader reader)
        {
            Vector2 pos;
            Vector2 vel;

            pos.x = reader.ReadSingle();
            pos.y = reader.ReadSingle();
            vel.x = reader.ReadSingle();
            vel.y = reader.ReadSingle();

            other.position = pos;
            other.velocity = vel;

            byte length = reader.ReadByte();
            for (int i = 0; i < length; i++)
            {
                byte x = reader.ReadByte();
                byte y = reader.ReadByte();
                grid[x, y].health = reader.ReadByte();
            }
        }

        private void DataWriter(BinaryWriter writer)
        {
            var pos = me.position;
            var vel = me.velocity;

            writer.Write(pos.x);
            writer.Write(pos.y);
            writer.Write(vel.x);
            writer.Write(vel.y);

            writer.Write((byte) changes.Count);
            for (int i = 0; i < changes.Count; i++)
            {
                writer.Write(changes[i].x);
                writer.Write(changes[i].y);
                writer.Write(changes[i].health);
            }
            changes.Clear();
        }
    }
}