using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ProtoBuf;

namespace MessageShark.Samples.ProtoBufVsMessageShark {
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    [Serializable]
    public class SimpleObject {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Address { get; set; }
        public List<int> Scores { get; set; }
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    [Serializable]
    public class Simple {
        public int Id { get; set; }
        public MyEnum MEnum { get; set; }
    }

    public enum MyEnum {
        Test, Test2
    }
}
