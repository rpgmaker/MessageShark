using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MessageShark {
    [AttributeUsage(AttributeTargets.Interface | AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    public class MessageSharkIncludeAttribute : Attribute {
        public MessageSharkIncludeAttribute(Type knownType, byte tag) {
            KnownType = knownType;
            Tag = tag;
        }
        public Type KnownType { get; private set; }
        public byte Tag { get; private set; }
    }
}
