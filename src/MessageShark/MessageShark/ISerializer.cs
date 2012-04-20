using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MessageShark {
    public interface ISerializer<T> {
        byte[] Serialize(T obj);
        T Deserialize(byte[] buffer);
    }
}
