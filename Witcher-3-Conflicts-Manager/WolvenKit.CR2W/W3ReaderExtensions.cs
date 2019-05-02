﻿using System;
using System.Collections.Generic;
using System.IO;

namespace WolvenKit.CR2W
{
    public static class W3ReaderExtensions
    {
        public static int ReadBit6(this BinaryReader stream)
        {
            var result = 0;
            var shift = 0;
            byte b = 0;
            var i = 1;

            do
            {
                b = stream.ReadByte();
                if (b == 128)
                    return 0;
                byte s = 6;
                byte mask = 255;
                if (b > 127)
                {
                    mask = 127;
                    s = 7;
                }
                else if (b > 63)
                {
                    if (i == 1)
                    {
                        mask = 63;
                    }
                }
                result = result | ((b & mask) << shift);
                shift = shift + s;
                i = i + 1;
            } while (!(b < 64 || (i >= 3 && b < 128)));

            return result;
        }

        public static void WriteBit6(this BinaryWriter stream, int c)
        {
            if (c == 0)
            {
                stream.Write((byte)128);
                return;
            }

            //var str2 = Convert.ToString(c, 2);

            var bytes = new List<int>();
            var left = c;

            for (var i = 0; (left > 0); i++)
            {
                if (i == 0)
                {
                    bytes.Add(left & 63);
                    left = left >> 6;
                }
                else
                {
                    bytes.Add(left & 255);
                    left = left >> 7;
                }
            }


            for (var i = 0; i < bytes.Count; i++)
            {
                var last = (i == bytes.Count - 1);
                var cleft = (bytes.Count - 1) - i;

                if (!last)
                {
                    if (cleft >= 1 && i >= 1)
                    {
                        bytes[i] = bytes[i] | 128;
                    }
                    else if (bytes[i] < 64)
                    {
                        bytes[i] = bytes[i] | 64;
                    }
                    else
                    {
                        bytes[i] = bytes[i] | 128;
                    }
                }

                if (bytes[i] == 128)
                {
                    throw new Exception("No clue what to do here, still need to think about it... :p");
                }

                stream.Write((byte)bytes[i]);
            }
        }

        public static float ReadHalfFloat(this BinaryReader stream)
        {
            ushort data = stream.ReadUInt16();
            // half (binary16) format IEEE 754-2008
            uint dataSign = (uint)data >> 15;
            uint dataExp = ((uint)data >> 10) & 0x001F;
            uint dataFrac = (uint)data & 0x03FF;

            uint floatExp = 0;
            uint floatFrac = 0;

            switch (dataExp)
            {
                case 0: // subnormal : (-1)^sign * 2^-14 * 0.frac
                    if (dataFrac != 0) // subnormals but non-zeros -> normals in float32
                    {
                        floatExp = -15 + 127;
                        while ((dataFrac & 0x200) == 0) { dataFrac <<= 1; floatExp--; }
                        floatFrac = (dataFrac & 0x1FF) << 14;
                    }
                    else { floatFrac = 0; floatExp = 0; } // ± 0 -> ± 0
                    break;
                case 31: // infinity or NaNs : frac ? NaN : (-1)^sign * infinity
                    floatExp = 255;
                    floatFrac = dataFrac != 0 ? (uint)0x200000 : 0; // signaling Nan or zero
                    break;
                default: // normal : (-1)^sign * 2^(exp-15) * 1.frac
                    floatExp = dataExp - 15 + 127;
                    floatFrac = dataFrac << 13;
                    break;
            }
            // single precision floating point (binary32) format IEEE 754-2008
            uint floatNum = dataSign << 31 | floatExp << 23 | floatFrac;
            return BitConverter.ToSingle(BitConverter.GetBytes(floatNum), 0);
        }
    }
}