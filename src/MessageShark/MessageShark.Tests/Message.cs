using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MessageShark.Tests {

    public class Test {
        public string Str { get; set; }
        public int Int { get; set; }
        public Guid UUID { get; set; }
    }

    public class Message {
        public ulong ID { get; set; }
        public DateTime CreateDate { get; set; }
        public string Data { get; set; }
        public Test test { get; set; }
        public List<Test> Tests { get; set; }
    }
}
