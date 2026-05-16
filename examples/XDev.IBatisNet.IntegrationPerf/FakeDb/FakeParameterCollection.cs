using System.Collections;
using System.Data;

namespace XDev.IBatisNet.IntegrationPerf.FakeDb;

public sealed class FakeParameterCollection : IDataParameterCollection
{
    private readonly ArrayList _items = [];

    public object? this[string parameterName]
    {
        get => _items.Cast<IDataParameter>().FirstOrDefault(x => x.ParameterName == parameterName);
        set
        {
            var index = IndexOf(parameterName);
            if (index < 0)
            {
                Add(value!);
            }
            else
            {
                _items[index] = value!;
            }
        }
    }

    public object? this[int index]
    {
        get => _items[index];
        set => _items[index] = value!;
    }

    public bool IsFixedSize => false;
    public bool IsReadOnly => false;
    public int Count => _items.Count;
    public bool IsSynchronized => false;
    public object SyncRoot => this;

    public int Add(object value) => _items.Add(value);
    public void Clear() => _items.Clear();
    public bool Contains(string parameterName) => IndexOf(parameterName) >= 0;
    public bool Contains(object value) => _items.Contains(value);
    public void CopyTo(Array array, int index) => _items.CopyTo(array, index);
    public IEnumerator GetEnumerator() => _items.GetEnumerator();
    public int IndexOf(string parameterName)
    {
        for (var i = 0; i < _items.Count; i++)
        {
            if (_items[i] is IDataParameter parameter && parameter.ParameterName == parameterName)
            {
                return i;
            }
        }

        return -1;
    }

    public int IndexOf(object value) => _items.IndexOf(value);
    public void Insert(int index, object value) => _items.Insert(index, value);
    public void Remove(object value) => _items.Remove(value);
    public void RemoveAt(string parameterName)
    {
        var index = IndexOf(parameterName);
        if (index >= 0)
        {
            RemoveAt(index);
        }
    }

    public void RemoveAt(int index) => _items.RemoveAt(index);
}
