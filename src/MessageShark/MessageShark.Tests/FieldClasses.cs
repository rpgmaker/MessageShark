using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections;
using System.Data;

namespace MessageShark.Tests {
    /// <summary>
    /// Data field
    /// </summary>
    [Serializable]
    public class Field {
        string _name = null;




        #region Properties
        public int ID { get; set; }

        public string Name {
            get {
                return _name;
            }
            set {
                _name = value != null ? value.ToLower() : value;
            }
        }

        public string Description { get; set; }

        public DbType DataType { get; set; }

        public uint Length { get; set; }
        #endregion
    }


    //[Serialiser(typeof(FieldCollectionSerialiser))]
    public class FieldCollection : IEnumerable<Field>, ICollection<Field>, IList<Field>, IEnumerable, ICollection, IList {
        /* Idea: List<string> will contain field names as values so access to a field name at index 123 can be O(1).
         * Dictionary<string, KeyValuePair<int, Field>> will contain field name as a key, so we could to access field by name in o(1) and
         * Field itself as a value.
        */

        List<Field> _fieldList = null;
        Dictionary<string, int> _fieldDictionary = null; // string-field name, int-index at which field is


        private static object _lock = new object(),
                              _lockSync = new object();




        #region Properties
        List<Field> FieldList { get { return _fieldList ?? (_fieldList = new List<Field>()); } set { _fieldList = value; } }

        Dictionary<string, int> FieldDictionary { get { return _fieldDictionary ?? (_fieldDictionary = new Dictionary<string, int>()); } }

        public int Count {
            get { return _fieldList != null ? _fieldList.Count : 0; }
        }

        public bool IsReadOnly {
            get { return false; }
        }

        public Field this[int index] {
            get {
                if (_fieldList == null || index < 0 || index > _fieldList.Count)
                    return null;

                return _fieldList[index];
            }
            set {
                this.SetAtIndex(index, value);
            }
        }

        [MessageSharkIgnore]
        public Field this[string name] {
            get {
                if (_fieldDictionary == null)
                    return null;

                int index = -1;

                if (!_fieldDictionary.TryGetValue(name, out index))
                    return null;

                return this[index];
            }
            set {
                this.SetAtName(name, value);
            }
        }

        public bool IsSynchronized {
            get { return true; }
        }

        public object SyncRoot {
            get { return _lockSync; }
        }

        public bool IsFixedSize {
            get { return false; }
        }

        object IList.this[int index] {
            get {
                return this[index];
            }
            set {
                this[index] = value as Field;
            }
        }
        #endregion




        #region Constructors
        public FieldCollection()
            : this(8) {
        }

        public FieldCollection(int capacity) {
            // this constructor REQUIRED FOR SERIALIZATION!!!!
            _fieldList = new List<Field>(capacity);
            _fieldDictionary = new Dictionary<string, int>(capacity);
        }
        #endregion




        #region Methods
        void SetAtIndex(int index, Field item) {
            if (index < 0 || index > this.FieldList.Count)
                throw new ArgumentOutOfRangeException("index", index, "Index cannot be less than zero or greater than collection length.");

            // check if we trying to replace an existing value
            if (index < this.FieldList.Count) {
                // replacing en existing value in the collection
                if (item == null)
                    throw new ArgumentNullException("item", "Collection item cannot be null.");

                Field oldField = _fieldList[index];

                // check if field with the same key already exists at other index
                int oldIndex = -1;
                if (this.FieldDictionary.TryGetValue(oldField.Name, out oldIndex) && oldIndex != index)
                    throw new Exception("Cannot set Field \"" + item.Name + "\" at index " + index + ". Field with the name \"" + oldField.Name + "\" already exists in this collection at index " + oldIndex);

                _fieldDictionary.Remove(oldField.Name);
                oldField = null;

                // setting value
                _fieldList[index] = item;
                _fieldDictionary[item.Name] = index;
            } else
                this.Add(item);
        }



        void SetAtName(string name, Field item) {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentNullException("name", "Field name cannot be null or empty.");

            int oldIndex = -1;
            if (this.FieldDictionary.TryGetValue(name, out oldIndex)) {
                // Field with the same name exists, override it
                if (item == null)
                    throw new ArgumentNullException("value", "Collection value cannot be null.");

                _fieldList[oldIndex] = item;
            } else
                this.Add(item);

        }



        void RemoveItem(int index, string name) {
            lock (_lock) {
                _fieldList.RemoveAt(index);
                _fieldDictionary.Remove(name);

                // fix indexes after the removed item
                for (int i = index; i < _fieldList.Count; i++)
                    _fieldDictionary[_fieldList[i].Name] = i;
            }
        }



        IEnumerator IEnumerable.GetEnumerator() {
            return this.FieldList.GetEnumerator();
        }

        public IEnumerator<Field> GetEnumerator() {
            foreach (var field in FieldList)
                yield return field;
            //return this.FieldList.GetEnumerator();
        }

        public void Add(Field item) {
            if (item == null)
                throw new ArgumentNullException("item", "Collection value cannot be null.");

            if (this.FieldDictionary.ContainsKey(item.Name))
                throw new ArgumentException("Cannot add Field object. Field object with name \"" + item.Name + "\" already exists in the collection.", "item");

            lock (_lock) {
                this.FieldList.Add(item);
                this.FieldDictionary.Add(item.Name, _fieldList.Count - 1);
            }
        }

        public void Clear() {
            _fieldList.Clear();
            _fieldDictionary.Clear();
        }

        public bool Contains(Field item) {
            if (item == null || _fieldDictionary == null)
                return false;

            return _fieldDictionary.ContainsKey(item.Name);
        }

        public bool Contains(string name) {
            return _fieldDictionary.ContainsKey(name);
        }

        public void CopyTo(Field[] array, int arrayIndex) {
            if (_fieldList != null)
                _fieldList.CopyTo(array, arrayIndex);
        }

        public void CopyTo(Array array, int index) {
            if (_fieldList != null)
                ((ICollection)_fieldList).CopyTo(array, index);
        }

        public bool Remove(Field item) {
            if (item == null)
                return false;

            return this.Remove(item.Name);
        }

        public bool Remove(string name) {
            if (_fieldDictionary == null || _fieldList == null || string.IsNullOrEmpty(name))
                return false;

            int index = -1;

            if (!_fieldDictionary.TryGetValue(name, out index))
                return false; // no Field.Name in the collection, nothing to remove

            this.RemoveItem(index, name);

            return true;
        }

        public int IndexOf(Field item) {
            if (item == null)
                return -1;

            return this.IndexOf(item.Name);
        }

        public int IndexOf(string name) {
            if (_fieldDictionary == null || string.IsNullOrEmpty(name))
                return -1;

            int index = -1;

            _fieldDictionary.TryGetValue(name, out index);

            return index;
        }

        public void Insert(int index, Field item) {
            if (item == null)
                throw new ArgumentNullException("item", "Collection item cannot be null.");

            if (index < 0 || index > this.FieldList.Count)
                throw new ArgumentOutOfRangeException("index", index, "Index cannot be less than zero or greater than collection length.");

            if (this.FieldDictionary.ContainsKey(item.Name))
                throw new ArgumentException("Cannot add Field object. Field object with name \"" + item.Name + "\" already exists in the collection.", "item");

            lock (_lock) {
                _fieldList.Insert(index, item);
                _fieldDictionary.Add(item.Name, index);

                // fix indexes after the removed item
                for (int i = index; i < _fieldList.Count; i++)
                    _fieldDictionary[_fieldList[i].Name] = i;
            }
        }

        public void RemoveAt(int index) {
            if (_fieldList == null)
                return;

            if (index < 0 || index > _fieldList.Count)
                throw new ArgumentOutOfRangeException("index", index, "Index cannot be less than zero or greater than collection length.");

            Field oldField = _fieldList[index];

            this.RemoveItem(index, oldField.Name);
        }

        public int Add(object value) {
            int i = this.Count;
            this.Add(value as Field);
            return i;
        }

        public bool Contains(object value) {
            return this.Contains(value as Field);
        }

        public int IndexOf(object value) {
            return this.IndexOf(value as Field);
        }

        public void Insert(int index, object value) {
            this.Insert(index, value as Field);
        }

        public void Remove(object value) {
            this.Remove(value as Field);
        }
        #endregion



    }


    /// <summary>
    /// Data table
    /// </summary>
    public class Table {
        FieldCollection _fields = null;
        string _name = null;




        #region Properties
        public int ID { get; set; }

        public string Name {
            get { return _name; }
            set { _name = value != null ? value.ToLower() : value; }
        }

        public string Description { get; set; }

        public FieldCollection Fields { get { return _fields ?? (_fields = new FieldCollection()); } set { _fields = value; } }
        #endregion


    }
}
