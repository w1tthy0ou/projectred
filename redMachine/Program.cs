using System;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Text;
using System.Threading;

namespace redMachine
{
    class redMachineApp
    {

        static string filePath;
        static string searchedWord;
        static bool termination = false;
        static int proccessID;
        static Int64 beginLine;
        static Int64 endLine;

        static List<Tuple<Int64, int>> indexes;

        static int progress = 0;
        static Int64 found = 0;

        static MemoryMappedFile foundGate;
        static MemoryMappedViewAccessor foundAccessor;

        static MemoryMappedFile progressGate;
        static MemoryMappedViewAccessor progressAccessor;

        static MemoryMappedFile spentTimeGate;
        static MemoryMappedViewAccessor spentTimeAccessor;

        static Int64 startTime;

        static Mutex mutex;
        public static byte[] ReadMMFAllBytes(string fileName) // Method to read string from mmf that was borrowed from internet so idc what's happening
        {
            using (var mmf = MemoryMappedFile.OpenExisting(fileName))
            {
                using (var stream = mmf.CreateViewStream())
                {
                    using (BinaryReader binReader = new BinaryReader(stream))
                    {
                        int c = 0;
                        var a = binReader.ReadBytes((int)stream.Length);
                        List<byte> b = new List<byte>();
                        foreach (var d in a)
                        {
                            if (c > 3)
                                break;

                            if (d == (byte)'\0')
                                c++;
                            else
                            {
                                c = 0;
                                b.Add(d);
                            }
                        }

                        return b.ToArray();
                    }
                }
            }
        }

        public static void WriteStatusAndProgress()
        { // Writes progress and amount of words found to mmf
            mutex.WaitOne();
            //Console.WriteLine($"Setting {progress} {found}");

            if (foundAccessor.CanWrite)
            {
                var b = BitConverter.GetBytes(found);
                foundAccessor.WriteArray<byte>(0, b, 0, b.Length);
            }
            else
            {
                termination = true;
            }

            if (progressAccessor.CanWrite)
            {
                var b = BitConverter.GetBytes(progress);
                progressAccessor.WriteArray<byte>(0, b, 0, b.Length);
            }
            else
            {
                termination = true;
            }

            if (spentTimeAccessor.CanWrite)
            {
                var b = BitConverter.GetBytes(_getCurrentTime() - startTime);
                spentTimeAccessor.WriteArray<byte>(0, b, 0, b.Length);
            }
            else
            {
                termination = true;
            }

            mutex.ReleaseMutex();
        }

        private static Int64 _getCurrentTime()
        {
            return DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
        }

        public static void Seek(string[] src, string t)
        { // Searching word method
            t = t.ToLower();

            for (Int64 i = 0; i < src.Length; i++)
            {
                if (src[i].Count() < 1)
                    continue;
                var lj = -1;
                while ((lj = src[i].ToLower().IndexOf(t, lj + 1)) != -1)
                    indexes.Add(new Tuple<Int64, int>(i, lj));

                progress = (int)(Math.Floor(((double)i + 1) / (double)src.Length * 100.0));

                //Console.WriteLine(progress);

                found = indexes.Count();

                WriteStatusAndProgress();
            }
            progress = 100;
            WriteStatusAndProgress();
        }

        public static void Main(String[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            proccessID = int.Parse(args[0]);
            mutex = new Mutex(false, proccessID + "Mutex");

            beginLine = Int32.Parse(args[1]);
            endLine = Int32.Parse(args[2]);

            Console.WriteLine($"{proccessID}, {beginLine}, {endLine}");

            // mmfs for status and amount of words found

            foundGate = MemoryMappedFile.OpenExisting(proccessID + "FoundMMF");

            foundAccessor = foundGate.CreateViewAccessor();
            progressGate = MemoryMappedFile.OpenExisting(proccessID + "ProgressMMF");
            progressAccessor = progressGate.CreateViewAccessor();
            spentTimeGate = MemoryMappedFile.OpenExisting(proccessID + "SepntTimeMMF");
            spentTimeAccessor = spentTimeGate.CreateViewAccessor();

            filePath += System.Text.Encoding.Default.GetString(ReadMMFAllBytes("FilePathMMF"));
            searchedWord = System.Text.Encoding.Default.GetString(ReadMMFAllBytes("SearchedWordMMF"));

            indexes = new List<Tuple<Int64, int>>();

            var line = File.ReadLines(filePath).Skip((Int32)beginLine).Take((Int32)(endLine - beginLine)).ToArray();

            Console.WriteLine("Starting seek() method");
            startTime = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
            Seek(line, searchedWord);

            //foreach (var i in indexes) 
            //{
            //    Console.WriteLine($"{i.Item1}, {i.Item2}\n");
            //}
        }

    }
}