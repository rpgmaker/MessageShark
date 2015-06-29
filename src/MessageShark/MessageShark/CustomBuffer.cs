using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace MessageShark {
    public class CustomBuffer {
        private byte[] _buffer;
        private int _origin;
        private int _size;
        private int _position;
        private int _length;
        private static readonly int MIN_BUFFER_SIZE = 200;
        private static readonly int MIN_SIZE = 0x100;
        private Stream _stream;


        public CustomBuffer(Stream stream) {
            _stream = stream;
        }

        public CustomBuffer() : this(MIN_BUFFER_SIZE) { }
        public CustomBuffer(int size) {
            _size = size;
            _buffer = new byte[size];
            _origin = 0;
            _length = _buffer.Length;
        }

        private bool EnsureCapacity(int size) {
            if (size < 0 || size <= _size) return false;
            var dsize = _size << 1;
            var size2 = size;
            if (size2 < MIN_SIZE) size2 = MIN_SIZE;
            if (size2 < dsize) size2 = dsize;
            if (size2 != _size) {
                if (size2 > 0) {
                    var buffer = new byte[size2];
                    if (_length > 0)
                        Buffer.BlockCopy(_buffer, 0, buffer, 0, _length);
                    _buffer = buffer;
                } else _buffer = null;
                _size = size2;
            }
            return true;
        }

        public void Write(byte[] buffer) {

            if (_stream != null) {
                _stream.Write(buffer, 0, buffer.Length);
                return;
            }

            var count = buffer.Length;
            var size = _position + count;
            if (size < 0) return;
            if (size > _length) {
                var resize = _position > _length;
                if (size > _size && EnsureCapacity(size)) resize = false;
                if (resize) Array.Clear(_buffer, _length, size - _length);
                _length = size;
            }
            if (count <= 8 && buffer != _buffer) {
                var size2 = count;
                while (--size2 >= 0)
                    _buffer[_position + size2] = buffer[size2];
            } else Buffer.BlockCopy(buffer, 0, _buffer, _position, count);
            _position = size;
        }

        public byte[] ToArray() {
            var size = _position;
            var buffer = new byte[size];
            Buffer.BlockCopy(_buffer, _origin, buffer, _origin, size);
            return buffer;
        }

        public byte[] GetBuffer() {
            return _buffer;
        }
    }
}
