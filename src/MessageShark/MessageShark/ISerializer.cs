using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace MessageShark {
    public interface ISerializer<T> {
        byte[] Serialize(T obj);
        T Deserialize(byte[] buffer);
        void Serialize(T obj, Stream stream);
    }
}
