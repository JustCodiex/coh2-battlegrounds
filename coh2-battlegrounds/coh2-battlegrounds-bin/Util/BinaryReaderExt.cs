﻿using System;
using System.IO;
using System.Text;

namespace coh2_battlegrounds_bin.Util {
    
    /// <summary>
    /// Extension class for extending the utilities of <see cref="BinaryReader"/>
    /// </summary>
    public static class BinaryReaderExt {

        /// <summary>
        /// Read an ASCII string from a length that's given before the string
        /// </summary>
        /// <param name="reader">The stream to read string from</param>
        /// <returns>The read string</returns>
        public static string ReadASCIIString(this BinaryReader reader)
            => reader.ReadASCIIString((int)reader.ReadUInt32());

        /// <summary>
        /// Read an ASCII string from a known length
        /// </summary>
        /// <param name="reader">The stream to read string from</param>
        /// <param name="length"></param>
        /// <returns>The read string</returns>
        public static string ReadASCIIString(this BinaryReader reader, int length) 
            => Encoding.ASCII.GetString(reader.ReadBytes(length));

        /// <summary>
        /// Read a UTF-8 string of unknown length
        /// </summary>
        /// <param name="reader">The stream to read string from</param>
        /// <returns></returns>
        public static string ReadUTF8String(this BinaryReader reader) {
            StringBuilder strBuilder = new StringBuilder();
            while (!reader.HasReachedEOS()) {
                ushort u = BitConverter.ToUInt16(reader.ReadBytes(2));
                if (u == 0) {
                    break;
                } else {
                    strBuilder.Append((char)u);
                }
            }
            return strBuilder.ToString();
        }

        /// <summary>
        /// Read a UTF-8 of known character count
        /// </summary>
        /// <param name="reader">The stream to read string from</param>
        /// <param name="characterCount">The amount of UTF-8 characters to read</param>
        /// <returns></returns>
        public static string ReadUTF8String(this BinaryReader reader, uint characterCount) {
            byte[] content = reader.ReadBytes((int)characterCount * 2);
            StringBuilder strBuilder = new StringBuilder();
            for (int i = 0; i < content.Length; i += 2) {
                ushort u = BitConverter.ToUInt16(content, i);
                strBuilder.Append((char)u);
            }
            return strBuilder.ToString();
        }

        /// <summary>
        /// Extension method: Skips the specified amount of bytes
        /// </summary>
        /// <param name="reader">The stream to skip bytes in</param>
        /// <param name="count">The amount of bytes to skip</param>
        public static void Skip(this BinaryReader reader, long count)
            => reader.BaseStream.Seek(count, SeekOrigin.Current);

        /// <summary>
        /// Has the stream reached the end of stream (EOS)
        /// </summary>
        /// <param name="reader">The stream to check</param>
        /// <returns>True if the stream position is equal or greater than the stream length</returns>
        public static bool HasReachedEOS(this BinaryReader reader)
            => reader.BaseStream.Position >= reader.BaseStream.Length;

    }

}
