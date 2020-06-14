using System;

public enum eGrowPolicy
{
    Normal,
    HighSpeed,
    LowMemory,
}

public class FastArray<T> : ICloneable
{
    public T[] data;
    private int length;
    private int capacity;
    private int minCapacity;
    private eGrowPolicy growPolicy;

    public T this[int index]
    {
        get
        {
            this.BoundCheck(index);
            return this.data[index];
        }
        set
        {
            this.BoundCheck(index);
            this.data[index] = value;
        }
    }

    public int Count
    {
        get
        {
            return this.length;
        }
        set
        {
            this.SetCount(value);
        }
    }

    public eGrowPolicy GrowPolicy
    {
        get
        {
            return this.growPolicy;
        }
        set
        {
            this.growPolicy = value;
        }
    }

    object ICloneable.Clone()
    {
        return (object)this.Clone();
    }

    public FastArray<T> Clone()
    {
        return (FastArray<T>)this.MemberwiseClone();
    }

    protected void InitVars()
    {
        this.data = (T[])null;
        this.length = 0;
        this.capacity = 0;
        this.minCapacity = 0;
    }

    public void Clear()
    {
        this.Count = 0;
    }

    protected void BoundCheck(int index)
    {
        if (index >= 0 && index < this.Count)
            return;
        throw new Exception("Out of bound");
    }

    public void AddRange(T[] data)
    {
        if (data == null || data.Length < 0)
            return;
        if (data.Length == 0)
            return;
        int count = this.Count;
        this.SetCount(this.Count + data.Length);
        int destinationIndex = count;
        Array.Copy((Array)data, 0, (Array)this.data, destinationIndex, data.Length);
    }

    public void AddRange(T[] data, int dataLength)
    {
        if (data == null || data.Length < 0 || (dataLength < 0 || data.Length < dataLength))
            return;
        if (dataLength == 0)
            return;
        int count = this.Count;
        this.SetCount(this.Count + dataLength);
        int destinationIndex = count;
        Array.Copy((Array)data, 0, (Array)this.data, destinationIndex, dataLength);
    }

    public void AddRange(T[] data, int offset, int dataLength)
    {
        if (data == null || data.Length < 0 || (dataLength < 0 || data.Length < dataLength) || offset > data.Length)
            return;
        if (dataLength == 0)
            return;
        int count = this.Count;
        this.SetCount(this.Count + dataLength);
        int destinationIndex = count;
        Array.Copy((Array)data, offset, (Array)this.data, destinationIndex, dataLength);
    }

    public void Add(T value)
    {
        this.Insert(this.Count, value);
    }

    public void Insert(int indexAt, T value)
    {
        this.InsertRange(indexAt, value);
    }

    public void InsertRange(int indexAt, T data)
    {
        if ((object)data == null || indexAt > this.Count || indexAt < 0)
            return;
        int count = this.Count;
        this.SetCount(this.Count + 1);
        int num = count - indexAt;
        if (num > 0)
        {
            for (int index = num - 1; index >= 0; --index)
                this.data[index + indexAt + 1] = this.data[index + indexAt];
        }
        this.data[indexAt] = data;
    }

    public void InsertRange(int indexAt, T[] data)
    {
        if (data == null || indexAt > this.Count || indexAt < 0)
            return;
        int count = this.Count;
        this.SetCount(this.Count + data.Length);
        int num = count - indexAt;
        if (num > 0)
        {
            for (int index = num - 1; index >= 0; --index)
                this.data[index + indexAt + 1] = this.data[index + indexAt];
        }
        Array.Copy((Array)data, 0, (Array)this.data, indexAt, data.Length);
    }

    private int GetRecommendedCapacity(int actualCount)
    {
        switch (this.growPolicy)
        {
            case eGrowPolicy.Normal:
                int num1 = Math.Max(Math.Min(this.length / 8, 1024), 4);
                return Math.Max(this.minCapacity, actualCount + num1);
            case eGrowPolicy.HighSpeed:
                int num2 = Math.Max(Math.Max(this.length / 8, 16), 64);
                return Math.Max(this.minCapacity, actualCount + num2);
            case eGrowPolicy.LowMemory:
                return Math.Max(this.minCapacity, actualCount);
            default:
                return -1;
        }
    }

    public void SetCount(int newVal)
    {
        if (newVal < 0)
            return;
        if (newVal == this.length)
            return;
        if (newVal > this.capacity)
        {
            int recommendedCapacity = this.GetRecommendedCapacity(newVal);
            if (this.capacity == 0)
                this.data = new T[recommendedCapacity];
            else
                Array.Resize<T>(ref this.data, recommendedCapacity);
            this.capacity = recommendedCapacity;
        }
        this.length = newVal;
    }

    public void UseExternalBuffer(ref T[] buffer, int capacity)
    {
        if (this.data != null)
            throw new Exception("FastArray.UseExternalBuffer");
        if (buffer == null || capacity == 0)
            return;
        this.capacity = capacity;
        this.data = buffer;
        this.length = 0;
    }
}
