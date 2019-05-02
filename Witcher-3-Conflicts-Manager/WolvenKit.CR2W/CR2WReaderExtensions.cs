﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using WolvenKit.CR2W.Types;

namespace WolvenKit.CR2W
{
    public static class CR2WReaderExtensions
    {
        /// <summary>
        ///     Read null terminated string
        /// </summary>
        /// <param name="file">Reader</param>
        /// <param name="len">Fixed length string</param>
        /// <returns>string</returns>
        public static string ReadCR2WString(this BinaryReader file, int len = 0)
        {
            string str = null;
            if (len > 0)
            {
                str = Encoding.Default.GetString(file.ReadBytes(len));
            }
            else
            {
                var sb = new StringBuilder();
                while (true)
                {
                    var c = (char) file.ReadByte();
                    if (c == 0)
                        break;
                    sb.Append(c);
                }
                str = sb.ToString();
            }
            return str;
        }

        public static void WriteCR2WString(this BinaryWriter file, string str)
        {
            if (str != null)
            {
                file.Write(Encoding.Default.GetBytes(str));
            }
            file.Write((byte) 0);
        }

        public static void AddUnique(this Dictionary<string, uint> dic, string str, uint val)
        {
            if (str == null) str = "";

            if (!dic.ContainsKey(str))
            {
                dic.Add(str, val);
            }
        }

        public static uint Get(this Dictionary<string, uint> dic, string str)
        {
            if (str == null)
                str = "";

            return dic[str];
        }

        public static CVariable GetVariableByName(this CVector arr, string name)
        {
            for (var i = 0; i < arr.variables.Count; i++)
            {
                if (arr.variables[i].Name == name)
                    return arr.variables[i];
            }
            return null;
        }

        public static CVariable GetVariableByType(this CVector arr, string type)
        {
            for (var i = 0; i < arr.variables.Count; i++)
            {
                if (arr.variables[i].Type == type)
                    return arr.variables[i];
            }
            return null;
        }

        public static CVariable GetVariableByName(this CR2WChunk arr, string name)
        {
            if (arr.data is CVector)
            {
                var vdata = (CVector) arr.data;

                for (var i = 0; i < vdata.variables.Count; i++)
                {
                    if (vdata.variables[i].Name == name)
                        return vdata.variables[i];
                }
            }

            return null;
        }

        public static CVariable GetVariableByName(this CR2WChunk arr, CR2WFile file, string name)
        {
            if (arr.data is CVector)
            {
                var vdata = (CVector) arr.data;

                for (var i = 0; i < vdata.variables.Count; i++)
                {
                    if (vdata.variables[i].Name == name)
                        return vdata.variables[i];
                }
            }
            return null;
        }

        public static CVariable GetVariableByName(this List<CVariable> list, string name)
        {
            foreach (var item in list)
            {
                if (item.Name == name)
                {
                    return item;
                }
            }

            return null;
        }

        public static void CreateConnection(this CR2WChunk chunk, string in_name, string out_name, CR2WChunk out_target)
        {
            var cachedConnections = (CArray) chunk.GetVariableByName("cachedConnections");

            if (cachedConnections == null)
            {
                cachedConnections =
                    (CArray) chunk.cr2w.CreateVariable(chunk, "array:2,0,SCachedConnections", "cachedConnections");
            }

            {
                // connection 1

                var connection = (CVector) cachedConnections.array.Find(delegate(CVariable item)
                {
                    var vec = (CVector) item;
                    if (vec == null)
                        return false;

                    var socketId = (CName) vec.GetVariableByName("socketId");
                    return socketId != null && socketId.Value == in_name;
                });

                if (connection == null)
                {
                    connection = chunk.cr2w.CreateVector(cachedConnections);
                    ((CName) chunk.cr2w.CreateVariable(connection, "CName", "socketId")).Value = in_name;
                }


                var blocks = (CArray) connection.GetVariableByName("blocks");

                if (blocks == null)
                {
                    blocks = (CArray) chunk.cr2w.CreateVariable(connection, "array:2,0,SBlockDesc", "blocks");
                }

                var block = chunk.cr2w.CreateVector(blocks);
                chunk.cr2w.CreatePtr(block, "ptr:CQuestGraphBlock", out_target, "ock");
                ((CName) chunk.cr2w.CreateVariable(block, "CName", "putName")).Value = out_name;
            }
        }

        public static int ReadVLQInt32(this BinaryReader br)
        {
            var b1 = br.ReadByte();
            var sign = (b1 & 128) == 128;
            var next = (b1 & 64) == 64;
            var size = b1 % 128 % 64;
            var offset = 6;
            while (next)
            {
                var b = br.ReadByte();
                size = (b % 128) << offset | size;
                next = (b & 128) == 128;
                offset += 7;
            }
            return sign ? size * -1 : size;
        }

        public static byte[] ReadRemainingData(this BinaryReader br)
        {
            return br.ReadBytes((int)(br.BaseStream.Length - br.BaseStream.Position));
        }

        /// <summary>
        /// Read a single string from the current stream, where the first bytes indicate the length.
        /// </summary>
        /// <returns>string value read</returns>
        public static string ReadStringDefaultSingle(this BinaryReader br)
        {
            var b = br.ReadByte();
            var nxt = (b & (1 << 6)) != 0;
            var utf = (b & (1 << 7)) == 0;
            int len = b & ((1 << 6) - 1);
            if (nxt)
            {
                len += 64 * br.ReadByte();
            }
            if (utf)
            {
                return Encoding.Unicode.GetString(br.ReadBytes(len * 2));
            }
            return Encoding.ASCII.GetString(br.ReadBytes(len));
        }

        public static void WriteVLQInt32(this BinaryWriter bw, int value)
        {
            bool negative = value < 0;
            value = Math.Abs(value);
            byte b = (byte)(value & 0x3F);
            value >>= 6;
            if (negative)
            {
                b |= 0x80;
            }
            bool cont = value != 0;
            if (cont)
            {
                b |= 0x40;
            }
            bw.Write(b);
            while (cont)
            {
                b = (byte)(value & 0x7F);
                value >>= 7;
                cont = value != 0;
                if (cont)
                {
                    b |= 0x80;
                }
                bw.Write(b);
            }
        }
    }
}