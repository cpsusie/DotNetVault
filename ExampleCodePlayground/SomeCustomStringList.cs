using System;
using System.Collections;
using System.Collections.Generic;
using JetBrains.Annotations;

namespace ExampleCodePlayground
{
    sealed class SomeCustomStringList : IList<string>
    {
        public int Count => _wrappedList.Count;
        public bool IsReadOnly => false;

        public string this[int index]
        {
            get => _wrappedList[index];
            set => _wrappedList[index] = value;
        }
        
        public SomeCustomStringList() => _wrappedList = new List<string>();
        public SomeCustomStringList([NotNull] IEnumerable<string> source) => _wrappedList =
            new List<string>(source ?? throw new ArgumentNullException(nameof(source)));

        public IEnumerator<string> GetEnumerator() => _wrappedList.GetEnumerator();
        public void Add(string item) => _wrappedList.Add(item);
        public void Clear() => _wrappedList.Clear();
        public bool Contains(string item) => _wrappedList.Contains(item);
        public void CopyTo(string[] array, int arrayIndex) => _wrappedList.CopyTo(array, arrayIndex);
        public bool Remove(string item) => _wrappedList.Remove(item);
        public int IndexOf(string item) => _wrappedList.IndexOf(item);
        public void Insert(int index, string item) => _wrappedList.Insert(index, item);
        public void RemoveAt(int index) => _wrappedList.RemoveAt(index);
        IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)_wrappedList).GetEnumerator();

        private readonly List<string> _wrappedList;
    }

    public sealed class CustomStringList : IList<string>
    {
        public object SyncObject { get; } = new object();

        public int Count { get { lock (SyncObject) return _wrappedList.Count;} }

        public bool IsReadOnly => false;

        public string this[int index]
        {
            get { lock (SyncObject) return _wrappedList[index]; }
            set { lock (SyncObject) _wrappedList[index] = value; }
        }

        public IEnumerator<string> GetEnumerator()
        {
            lock (SyncObject) //can't return an enumerator to this list, have to return an enumerator of a snapshot
            {
                var snap = new SomeCustomStringList(_wrappedList);
                return snap.GetEnumerator();
            }
        }
        public void Add(string item)
        {
            lock (SyncObject) _wrappedList.Add(item);
        }

        public void Clear()
        {
            lock (SyncObject) _wrappedList.Clear();
        }   

        public bool Contains(string item)
        {
            lock (SyncObject)
            {
                return _wrappedList.Contains(item);
            }
        }

        public void CopyTo(string[] array, int arrayIndex)
        {
            lock (SyncObject)
            {
                _wrappedList.CopyTo(array, arrayIndex);
            }
        }

        public bool Remove(string item)
        {
            lock (SyncObject)
            {
                return _wrappedList.Remove(item);
            }
        }

        public int IndexOf(string item)
        {
            lock (SyncObject)
            {
                return _wrappedList.IndexOf(item);
            }
        }

        public void Insert(int index, string item)
        {
            lock (SyncObject)
            {
                _wrappedList.Insert(index, item);
            }
        }

        public void RemoveAt(int index)
        {
            lock (SyncObject) _wrappedList.RemoveAt(index);
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        
        public CustomStringList() => _wrappedList = new SomeCustomStringList();
        public CustomStringList([NotNull] IEnumerable<string> source) => _wrappedList =
            new SomeCustomStringList(source ?? throw new ArgumentNullException(nameof(source)));

        private readonly SomeCustomStringList _wrappedList;
    }

    public static class Example
    {
        public static void DoStuffWithList([NotNull] CustomStringList list)
        {
            if (list == null) throw new ArgumentNullException(nameof(list));

            lock (list.SyncObject)
            {
                var idxToFirstJaneInList = list.IndexOf("Jane");
                if (idxToFirstJaneInList > -1)
                {
                    list[idxToFirstJaneInList] = "Janey";
                }
            }
        }
    }
}
