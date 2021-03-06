﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace UsefulDotNetThings.General
{
    public static class IO
    {
        internal static readonly char[] InvalidPathingChars;  // Characters disallowed in paths.

        static IO()
        {
            // KFreon: Setup some constants
            List<char> vals = new List<char>();
            vals.AddRange(Path.GetInvalidFileNameChars());
            vals.AddRange(Path.GetInvalidPathChars());

            InvalidPathingChars = vals.ToArray(vals.Count);
        }

        #region Stream Compression/Decompression
        /// <summary>
        /// Decompresses stream using GZip. Returns decompressed Stream.
        /// Returns null if stream isn't compressed.
        /// </summary>
        /// <param name="compressedStream">Stream compressed with GZip.</param>
        public static MemoryStream DecompressStream(Stream compressedStream)
        {
            MemoryStream newStream = new MemoryStream();
            compressedStream.Seek(0, SeekOrigin.Begin);

            GZipStream Decompressor = null;
            try
            {
                Decompressor = new GZipStream(compressedStream, CompressionMode.Decompress, true);
                Decompressor.CopyTo(newStream);
            }
            catch (InvalidDataException invdata)
            {
                return null;
            }
            catch (Exception e)
            {
                throw;
            }
            finally
            {
                if (Decompressor != null)
                    Decompressor.Dispose();
            }

            return newStream;
        }


        /// <summary>
        /// Compresses stream with GZip. Returns new compressed stream.
        /// </summary>
        /// <param name="DecompressedStream">Stream to compress.</param>
        /// <param name="compressionLevel">Level of compression to use.</param>
        public static MemoryStream CompressStream(Stream DecompressedStream, CompressionLevel compressionLevel = CompressionLevel.Optimal)
        {
            MemoryStream ms = new MemoryStream();
            using (GZipStream Compressor = new GZipStream(ms, compressionLevel, true))
            {
                DecompressedStream.Seek(0, SeekOrigin.Begin);
                DecompressedStream.CopyTo(Compressor);
            }

            return ms;
        }
        #endregion Stream Compression/Decompression


        #region File IO
        /// <summary>
        /// Changes a filename in a full filepath string.
        /// </summary>
        /// <param name="fullPath">Original full filepath.</param>
        /// <param name="newFilenameWithoutExt">New filename to use.</param>
        /// <returns>Filepath with changed filename.</returns>
        public static string ChangeFilename(string fullPath, string newFilenameWithoutExt)
        {
            return fullPath.Replace(Path.GetFileNameWithoutExtension(fullPath), newFilenameWithoutExt);
        }

        /// <summary>
        /// Converts given double to filesize with appropriate suffix.
        /// </summary>
        /// <param name="size">Size in bytes.</param>
        /// <param name="FullSuffix">True = Bytes, KiloBytes, etc. False = B, KB, etc</param>
        public static string GetFileSizeAsString(double size, bool FullSuffix = false)
        {
            string[] sizes = null;
            if (FullSuffix)
                sizes = new string[] { "Bytes", "Kilobytes", "Megabytes", "Gigabytes" };
            else
                sizes = new string[] { "B", "KB", "MB", "GB" };

            int order = 0;
            while (size >= 1024 && order + 1 < sizes.Length)
            {
                order++;
                size = size / 1024;
            }

            return size.ToString("F1") + " " + sizes[order];
        }

        /// <summary>
        /// Gets file extensions as filter string for SaveFileDialog and OpenFileDialog as a SINGLE filter entry.
        /// </summary>
        /// <param name="exts">List of extensions to use.</param>
        /// <param name="filterName">Name of filter entry. e.g. 'Images|*.jpg;*.bmp...', Images is the filter name</param>
        /// <returns>Filter string from extensions.</returns>
        public static string GetExtsAsFilter(List<string> exts, string filterName)
        {
            StringBuilder sb = new StringBuilder(filterName + "|");
            foreach (string str in exts)
                sb.Append("*" + str + ";");
            sb.Remove(sb.Length - 1, 1);
            return sb.ToString();
        }

        /// <summary>
        /// Gets file extensions as filter string for SaveFileDialog and OpenFileDialog as MULTIPLE filter entries.
        /// </summary>
        /// <param name="exts">List of file extensions. Must have same number as filterNames.</param>
        /// <param name="filterNames">List of file names. Must have same number as exts.</param>
        /// <returns>Filter string of names and extensions.</returns>
        public static string GetExtsAsFilter(List<string> exts, List<string> filterNames)
        {
            // KFreon: Flip out if number of extensions is different to number of names of said extensions
            if (exts.Count != filterNames.Count)
                return null;

            // KFreon: Build filter string
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < exts.Count; i++)
                sb.Append(filterNames[i] + "|*" + exts[i] + "|");
            sb.Remove(sb.Length - 1, 1);
            return sb.ToString();
        }

        /// <summary>
        /// Read text from file as single string.
        /// </summary>
        /// <param name="filename">Path to filename.</param>
        /// <param name="result">Contents of file.</param>
        /// <returns>Null if successful, error as string otherwise.</returns>
        public static string ReadTextFromFile(string filename, out string result)
        {
            result = null;
            string err = null;

            // Try to read file, but fail safely if necessary
            try
            {
                if (filename.IsFile())
                    result = File.ReadAllText(filename);
                else
                    err = "Not a file.";
            }
            catch (Exception e)
            {
                err = e.Message;
            }

            return err;
        }


        /// <summary>
        /// Reads lines of file into List.
        /// </summary>
        /// <param name="filename">File to read from.</param>
        /// <param name="Lines">Contents of file.</param>
        /// <returns>Null if success, error message otherwise.</returns>
        public static string ReadLinesFromFile(string filename, out List<string> Lines)
        {
            Lines = null;
            string err = null;

            try
            {
                // KFreon: Only bother if it is a file
                if (filename.IsFile())
                {
                    string[] lines = File.ReadAllLines(filename);
                    Lines = lines.ToList(lines.Length);
                }

                else
                    err = "Not a file.";
            }
            catch (Exception e)
            {
                err = e.Message;
            }

            return err;
        }

        /// <summary>
        /// Gets external image data as byte[] with some buffering i.e. retries if fails up to 20 times.
        /// </summary>
        /// <param name="file">File to get data from.</param>
        /// <param name="OnFailureSleepTime">Time (in ms) between attempts for which to sleep.</param>
        /// <param name="retries">Number of attempts to read.</param>
        /// <returns>byte[] of image.</returns>
        public static byte[] GetExternalData(string file, int retries = 20, int OnFailureSleepTime = 300)
        {
            for (int i = 0; i < retries; i++)
            {
                try
                {
                    // KFreon: Try readng file to byte[]
                    return File.ReadAllBytes(file);
                }
                catch (IOException e)
                {
                    // KFreon: Sleep for a bit and try again
                    System.Threading.Thread.Sleep(OnFailureSleepTime);
                    Console.WriteLine(e.Message);
                }
                catch (Exception e)
                {
                    Trace.WriteLine($"Failed to get external data: {e.Message}");
                }
            }
            return null;
        }


        /// <summary>
        /// Tests given file path for existence. If exists, adjusts filename until the new filename doesn't exist.
        /// </summary>
        /// <param name="baseName">Original desired name.</param>
        /// <returns>Filename based on original that doesn't already exist.</returns>
        public static string FindValidNewFileName(string baseName)
        {
            if (!baseName.IsFile())
                throw new ArgumentOutOfRangeException($"{nameof(baseName)} must be a testable file path, not a directory path.");

            if (!File.Exists(baseName))
                return baseName;

            int count = 1;
            string ext = Path.GetExtension(baseName);
            string pathWithoutExtension = GetFullPathWithoutExtension(baseName);

            // Detect if a similar path was provided i.e. <path>_#.ext - Remove the _# and start incrementation at #.
            char last = pathWithoutExtension.Last();
            if (pathWithoutExtension[pathWithoutExtension.Length - 2] == '_' && last.IsDigit())
            {
                count = int.Parse(last + "");
                pathWithoutExtension = pathWithoutExtension.Substring(0, pathWithoutExtension.Length - 2);
            }

            string tempName = pathWithoutExtension;
            while (File.Exists(tempName + ext))
            {
                tempName = pathWithoutExtension;
                tempName += "_" + count;
                count++;
            }

            return tempName + ext;
        }

        /// <summary>
        /// Gets a full file path (not just the name) without the file extension.
        /// </summary>
        /// <param name="fullPath">Full path to remove extension from.</param>
        /// <returns>File path without extension.</returns>
        public static string GetFullPathWithoutExtension(string fullPath)
        {
            if (!fullPath.IsFile())
                throw new ArgumentOutOfRangeException($"{nameof(fullPath)} must be a testable file path, not a directory path.");

            return fullPath.Substring(0, fullPath.LastIndexOf('.'));
        }
        #endregion File IO
    }
}
