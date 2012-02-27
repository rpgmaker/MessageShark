using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using MessageShark;
using System.IO;

namespace MessageShark.Samples.ProtoBufVsMessageShark {
    public static class Proto {
        public static byte[] Serialize<T>(T obj) {
            var ms = new MemoryStream();
            ProtoBuf.Serializer.Serialize(ms, obj);
            return ms.ToArray();
        }

        public static T Deserialize<T>(byte[] buffer) {
            var ms = new MemoryStream();
            ms.Write(buffer, 0, buffer.Length);
            ms.Seek(0, SeekOrigin.Begin);
            return ProtoBuf.Serializer.Deserialize<T>(ms);
        }
    }

    class Program {
        static void Main(string[] args) {

            //UnCommenting this line boost speed
            //MessageSharkSerializer.Build();

            var simpleObject = new SimpleObject
            {
                Id = 10,
                Name = "Yan",
                Address = "Planet Earth",
                Scores = Enumerable.Range(1, 10).ToList()
            };

            Test("Message Shark - Simple Object", () =>
            {
                var cBuffer = MessageSharkSerializer.Serialize(simpleObject);
                var cData = MessageSharkSerializer.Deserialize<SimpleObject>(cBuffer);
                return cBuffer.Length;
            });

            Test("Protobuf - Simple Object", () =>
            {
                var pBuffer = Proto.Serialize(simpleObject);
                var pData = Proto.Deserialize<SimpleObject>(pBuffer);
                return pBuffer.Length;
            });

            Console.ReadLine();
        }

        static void Test(string name, Func<int> func) {
            var count = 10000;
            var length = 0;
            var stopWatch = new Stopwatch();
            stopWatch.Start();
            for (var i = 0; i < count; i++)
                length = func();
            stopWatch.Stop();
            Console.WriteLine("Iteration: {0}", count);
            Console.WriteLine("{0} Size: {1}", name, length);
            Console.WriteLine("Completed {0} in Avg {1} Milliseconds", name, stopWatch.ElapsedMilliseconds);
            Console.WriteLine();
        }
    }
}
