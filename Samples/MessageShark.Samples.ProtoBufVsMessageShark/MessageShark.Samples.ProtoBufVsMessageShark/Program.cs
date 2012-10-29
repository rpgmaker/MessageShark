using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using MessageShark;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text.RegularExpressions;
using System.Security;
using System.Runtime.InteropServices;

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

    public static class Xml {
        public static string Serialize<T>(T obj) {
            return PServiceBus.Serializer.Xml.XmlSerializer.Serialize(obj);
        }
        public static T Deserialize<T>(string xml) {
            return PServiceBus.Serializer.Xml.XmlSerializer.Deserialize<T>(xml);
        }
    }

    public static class XmlOld {
        public static string Serialize<T>(T obj) {
            return PServiceBus.Core.Runtime.Serializers.XmlSerializer.Serialize(obj);
        }
        public static T Deserialize<T>(string xml) {
            return PServiceBus.Core.Runtime.Serializers.XmlSerializer.Deserialize<T>(xml);
        }
    }


    public static class NewtonJS {
        public static string Serialize<T>(T obj) {
            return Newtonsoft.Json.JsonConvert.SerializeObject(obj);
        }

        public static T Deserialize<T>(string json) {
            return Newtonsoft.Json.JsonConvert.DeserializeObject<T>(json);
        }
    }

    public static class SS {
        public static string Serialize<T>(T obj) {
            return ServiceStack.Text.JsonSerializer.SerializeToString(obj);
        }

        public static T Deserialize<T>(string json) {
            return ServiceStack.Text.JsonSerializer.DeserializeFromString<T>(json);
        }
    }


    public static class SSJsv {
        public static string Serialize<T>(T obj) {
            return new ServiceStack.Text.Jsv.JsvSerializer<T>().SerializeToString(obj);
        }

        public static T Deserialize<T>(string json) {
            return new ServiceStack.Text.Jsv.JsvSerializer<T>().DeserializeFromString(json);
        }
    }

    public static class SSXml {
        public static string Serialize<T>(T obj) {
            return ServiceStack.Text.XmlSerializer.SerializeToString(obj);
        }

        public static T Deserialize<T>(string json) {
            return ServiceStack.Text.XmlSerializer.DeserializeFromString<T>(json);
        }
    }

    public static class NetBinary {
        public static byte[] Serialize<T>(T obj) {
            using (var ms = new MemoryStream()) {
                var binaryFormatter = new BinaryFormatter();
                binaryFormatter.Serialize(ms, obj);
                return ms.ToArray();
            }
        }

        public static T Deserialize<T>(byte[] buffer) {
            using (var ms = new MemoryStream()) {
                ms.Write(buffer, 0, buffer.Length);
                ms.Position = 0L;
                var binaryFormatter = new BinaryFormatter();
                return (T)binaryFormatter.Deserialize(ms);
            }
        }
    }

    static class Program {
        static readonly Regex _invalidChar = new Regex(@"(>|<|'|""|&)", RegexOptions.Compiled |
           RegexOptions.IgnoreCase | RegexOptions.Multiline);


        internal static string ReplaceEx(this string original, string pattern, string replacement,
    StringComparison comparisonType = StringComparison.CurrentCulture, int stringBuilderInitialSize = -1) {
            if (original == null) {
                return null;
            }

            if (String.IsNullOrEmpty(pattern)) {
                return original;
            }


            int posCurrent = 0;
            int lenPattern = pattern.Length;
            int idxNext = original.IndexOf(pattern, comparisonType);
            StringBuilder result = new StringBuilder(stringBuilderInitialSize < 0 ? Math.Min(4096, original.Length) : stringBuilderInitialSize);

            while (idxNext >= 0) {
                result.Append(original, posCurrent, idxNext - posCurrent);
                result.Append(replacement);

                posCurrent = idxNext + lenPattern;

                idxNext = original.IndexOf(pattern, posCurrent, comparisonType);
            }

            result.Append(original, posCurrent, original.Length - posCurrent);

            return result.ToString();
        }

        static unsafe void Main(string[] args) {

            

            //UnCommenting this line boost speed
            //MessageSharkSerializer.Build();

            //PServiceBus.Serializer.Xml.XmlSerializer.Build();

            //PServiceBus.Serializer.Xml.XmlSerializer.Prepare<SimpleObject>();

            var simpleObject = new SimpleObject
            {
                Id = 10,
                Name = "Yan",
                Address = "Planet Earth",
                Scores = Enumerable.Range(1, 10).ToList()
            };

            //Test("Message Shark - Simple Object", () =>
            //{
            //    var cBuffer = MessageSharkSerializer.Serialize(simpleObject);
            //    //var cData = MessageSharkSerializer.Deserialize<SimpleObject>(cBuffer);
            //});

            //Test("Protobuf - Simple Object", () =>
            //{
            //    var pBuffer = Proto.Serialize(simpleObject);
            //    //var pData = Proto.Deserialize<SimpleObject>(pBuffer);
            //});

            //Test("ServiceStackJsv - Simple Object", () =>
            //{
            //    var json = SSJsv.Serialize(simpleObject);
            //    //var data = SS.Deserialize<SimpleObject>(json);
            //});


            //Test("ServiceStackJson - Simple Object", () =>
            //{
            //    var json = SS.Serialize(simpleObject);
            //    var data = SS.Deserialize<SimpleObject>(json);
            //});

            //Test("ServiceStackXml - Simple Object", () =>
            //{
            //    var xml = SSXml.Serialize(simpleObject);
            //    //var data = SSXml.Deserialize<SimpleObject>(xml);
            //});

            //Test("NewtonSoft Json - Simple Object", () =>
            //{
            //    var json = NewtonJS.Serialize(simpleObject);
            //    //var data = NewtonJS.Deserialize<SimpleObject>(json);
            //});

            var xmlText = "<List><Items type=\"Items\"><Message><UserName>TJ</UserName><Message>add-user</Message><ESBTOPIC_TOPIC_NAME>ChatTopic</ESBTOPIC_TOPIC_NAME><ESBTOPIC_HEADERS><Items type=\"Items\"><Item type=\"dict\"><Key>ESBTOPIC_CREATEDATE</Key><Value>8/5/2012 2:37:18 PM</Value></Item><Item type="dict"><Key>ESBTOPIC_MESSAGE_ID</Key><Value>8723e00b-a081-4178-b2bf-592ed3920adc</Value></Item></Items></ESBTOPIC_HEADERS></Message></Items></List>";

            Test("Xml - Simple Object", () =>
            {
                var xml = Xml.Serialize(simpleObject);
                var data = Xml.Deserialize<SimpleObject>(xml);
            });


            //var xmlx = "<SimpleObject><Id>10</Id><Name>Yan</Name><Address>Planet Earth</Address><ListInt32><Items type = \"Items\"><Int32>1</Int32><Int32>2</Int32><Int32>3</Int32><Int32>4</Int32><Int32>5</Int32><Int32>6</Int32><Int32>7</Int32><Int32>8</Int32><Int32>9</Int32><Int32>10</Int32></Items></ListInt32></SimpleObject>";



            #region
            //var text = "tesdfd&sfd\"sfd>sf<ds'";
            //bool result = false;
            //var t = "";

            //Test("ContainInvalidXml", () => {
            //    result = text.Contains("<") ||
            //        text.Contains(">") ||
            //        text.Contains("'") ||
            //        text.Contains("\"") ||
            //        text.Contains("&");
            //});

            //Test("RemoveBadCharsWithReplace", () =>
            //{
            //    t = text.Replace("&", "&amp;")
            //        .Replace("<", "&lt;")
            //        .Replace(">", "&gt;")
            //        .Replace("\"", "&quot;")
            //        .Replace("'", "&apos;");
            //});

            //Test("RemoveBadCharsWithSecurityElement", () =>
            //{
            //    t = SecurityElement.Escape(text);
            //});

            //Test("RegexInvalidXmlTest", () =>
            //{
            //    _invalidChar.IsMatch(text);
            //});

            //Test("Create XmlReader", () =>
            //{
            //    using (MemoryStream ms = new MemoryStream()) {
            //        using (StreamWriter sw = new StreamWriter(ms)) {
            //            sw.Flush();
            //            ms.Position = 0;
            //            using (var reader = System.Xml.XmlReader.Create(ms)) {

            //            }
            //        }
            //    }
            //});

            //Test("Create XmlReaderWithStringReader", () =>
            //{
            //    using (var sr = new StringReader("")) {
            //        using (var reader = System.Xml.XmlReader.Create(sr)) {
                        
            //        }
            //    }
            //});
            #endregion

            //Test("XmlOld - Simple Object", () =>
            //{
            //    var xml = XmlOld.Serialize(simpleObject);
            //    //var data = XmlOld.Deserialize<SimpleObject>(xml);
            //});

            //Test(".Net Binary - Simple Object", () =>
            //{
            //    var nBuffer = NetBinary.Serialize(simpleObject);
            //    //var nData = NetBinary.Deserialize<SimpleObject>(nBuffer);
            //});

            Console.ReadLine();
        }

        static void Test(string name, Action action) {
            var count = 100000;
            var length = 0;
            var stopWatch = new Stopwatch();
            stopWatch.Start();
            for (var i = 0; i < count; i++)
                action();
            stopWatch.Stop();
            Console.WriteLine("Iteration: {0}", count);
            Console.WriteLine("{0} Size: {1}", name, length);
            Console.WriteLine("Completed {0} in Avg {1} Milliseconds", name, stopWatch.ElapsedMilliseconds);
            Console.WriteLine();
        }
    }
}
