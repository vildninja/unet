using UnityEngine;
using System.Collections;
using System;
using UnityEngine.Networking;


namespace VildNinja.Net
{
    public enum FightResult
    {
        Victory,
        Defeat,
        Draw,
        Dropped
    }

    public enum GameStatus
    {
        Disconnected,
        Connected,
        Fighting
    }

    public struct Client : IEquatable<Client>
    {
        public int host;
        public int conn;

        public Client(int h, int c)
        {
            host = h;
            conn = c;
        }

        // To prevent garbage when using as key in dictionary
        // http://www.gamasutra.com/blogs/RobertZubek/20150811/250750/C_memory_and_performance_tips_for_Unity_part_2.php

        public bool Equals(Client other)
        {
            return other.conn == conn &&
                other.host == host;
        }

        public override bool Equals(object obj)
        {
            return (obj is Client) && Equals((Client)obj);
        }

        public override int GetHashCode()
        {
            return host * 9901 + conn * 331;
        }

        public override string ToString()
        {
            return "Client:" + conn + "@" + host;
        }
    }

    public static class Log
    {
        public static void TestError(byte error, string line)
        {
            if (error > 0)
            {
                Line("Err_" + error + " " + line);
            }
        }

        public static void Line(string line)
        {
            Debug.Log(line);
        }
    }

    public static class Util
    {
        
    }
}