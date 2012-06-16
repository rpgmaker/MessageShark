using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MessageShark.Tests {
    public class Message {
        public ulong ID { get; set; }
        public DateTime CreateDate { get; set; }
        public string Data { get; set; }
    }
}
