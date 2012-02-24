using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MessageShark {
        [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class MessageSharkIgnoreAttribute : Attribute {
    }
}
