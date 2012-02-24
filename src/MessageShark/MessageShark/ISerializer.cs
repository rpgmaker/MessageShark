using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MessageShark {
    public interface ISerializer<T> where T : class {
        byte[] Serialize(T obj);
        T Deserialize(byte[] buffer);
    }
}
