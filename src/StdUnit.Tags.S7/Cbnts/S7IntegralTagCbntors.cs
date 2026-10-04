using System.Buffers.Binary;

namespace StdUnit.Tags.S7;

/// <summary>
/// S7 Int16 组合子：缓存 = PLC 内存原始字节（西门子大端），按 EndianKind 直读。
/// </summary>
public class S7Int16TagCbntor : S7TagCbntorBase
{
    /// <summary>
    /// c'tor
    /// </summary>
    /// <param name="tagDescriptor"></param>
    /// <param name="tagCbnt">S7 组合（byte 缓存）</param>
    /// <param name="cacheOffset"></param>
    internal S7Int16TagCbntor(TagDescriptor tagDescriptor, TagCbnt<byte> tagCbnt, int cacheOffset)
        : base(tagDescriptor, tagCbnt, cacheOffset, cacheOffset)
    {
    }

    /// <summary>
    /// 测点值
    /// </summary>
    public override object? Value
    {
        get
        {
            var span = this.Cache.Span.Slice(this.CacheOffset, 2);
            return this.TagEndian() == EndianKinds.BigEndian
                ? BinaryPrimitives.ReadInt16BigEndian(span)
                : BinaryPrimitives.ReadInt16LittleEndian(span);
        }
        set
        {
#pragma warning disable CS8605 // Unboxing a possibly null value.
            var data = (short)value;
#pragma warning restore CS8605 // Unboxing a possibly null value.
            var dst = this.Cache.Span.Slice(this.CacheOffset, 2);
            if (this.TagEndian() == EndianKinds.BigEndian)
            {
                BinaryPrimitives.WriteInt16BigEndian(dst, data);
            }
            else
            {
                BinaryPrimitives.WriteInt16LittleEndian(dst, data);
            }
            this.Timestamp = DateTime.Now;
            this.MarkDirty();
        }
    }
}

/// <summary>
/// S7 UInt16 组合子
/// </summary>
public class S7UInt16TagCbntor : S7TagCbntorBase
{
    /// <summary>
    /// c'tor
    /// </summary>
    /// <param name="tagDescriptor"></param>
    /// <param name="tagCbnt">S7 组合（byte 缓存）</param>
    /// <param name="cacheOffset"></param>
    internal S7UInt16TagCbntor(TagDescriptor tagDescriptor, TagCbnt<byte> tagCbnt, int cacheOffset)
        : base(tagDescriptor, tagCbnt, cacheOffset, cacheOffset)
    {
    }

    /// <summary>
    /// 测点值
    /// </summary>
    public override object? Value
    {
        get
        {
            var span = this.Cache.Span.Slice(this.CacheOffset, 2);
            return this.TagEndian() == EndianKinds.BigEndian
                ? BinaryPrimitives.ReadUInt16BigEndian(span)
                : BinaryPrimitives.ReadUInt16LittleEndian(span);
        }
        set
        {
#pragma warning disable CS8605 // Unboxing a possibly null value.
            var data = (ushort)value;
#pragma warning restore CS8605 // Unboxing a possibly null value.
            var dst = this.Cache.Span.Slice(this.CacheOffset, 2);
            if (this.TagEndian() == EndianKinds.BigEndian)
            {
                BinaryPrimitives.WriteUInt16BigEndian(dst, data);
            }
            else
            {
                BinaryPrimitives.WriteUInt16LittleEndian(dst, data);
            }
            this.Timestamp = DateTime.Now;
            this.MarkDirty();
        }
    }
}

/// <summary>
/// S7 Int32 组合子
/// </summary>
public class S7Int32TagCbntor : S7TagCbntorBase
{
    /// <summary>
    /// c'tor
    /// </summary>
    /// <param name="tagDescriptor"></param>
    /// <param name="tagCbnt">S7 组合（byte 缓存）</param>
    /// <param name="cacheOffset"></param>
    internal S7Int32TagCbntor(TagDescriptor tagDescriptor, TagCbnt<byte> tagCbnt, int cacheOffset)
        : base(tagDescriptor, tagCbnt, cacheOffset, cacheOffset)
    {
    }

    /// <summary>
    /// 测点值
    /// </summary>
    public override object? Value
    {
        get
        {
            var span = this.Cache.Span.Slice(this.CacheOffset, 4);
            return this.TagEndian() == EndianKinds.BigEndian
                ? BinaryPrimitives.ReadInt32BigEndian(span)
                : BinaryPrimitives.ReadInt32LittleEndian(span);
        }
        set
        {
#pragma warning disable CS8605 // Unboxing a possibly null value.
            var data = (int)value;
#pragma warning restore CS8605 // Unboxing a possibly null value.
            var dst = this.Cache.Span.Slice(this.CacheOffset, 4);
            if (this.TagEndian() == EndianKinds.BigEndian)
            {
                BinaryPrimitives.WriteInt32BigEndian(dst, data);
            }
            else
            {
                BinaryPrimitives.WriteInt32LittleEndian(dst, data);
            }
            this.Timestamp = DateTime.Now;
            this.MarkDirty();
        }
    }
}

/// <summary>
/// S7 UInt32 组合子
/// </summary>
public class S7UInt32TagCbntor : S7TagCbntorBase
{
    /// <summary>
    /// c'tor
    /// </summary>
    /// <param name="tagDescriptor"></param>
    /// <param name="tagCbnt">S7 组合（byte 缓存）</param>
    /// <param name="cacheOffset"></param>
    internal S7UInt32TagCbntor(TagDescriptor tagDescriptor, TagCbnt<byte> tagCbnt, int cacheOffset)
        : base(tagDescriptor, tagCbnt, cacheOffset, cacheOffset)
    {
    }

    /// <summary>
    /// 测点值
    /// </summary>
    public override object? Value
    {
        get
        {
            var span = this.Cache.Span.Slice(this.CacheOffset, 4);
            return this.TagEndian() == EndianKinds.BigEndian
                ? BinaryPrimitives.ReadUInt32BigEndian(span)
                : BinaryPrimitives.ReadUInt32LittleEndian(span);
        }
        set
        {
#pragma warning disable CS8605 // Unboxing a possibly null value.
            var data = (uint)value;
#pragma warning restore CS8605 // Unboxing a possibly null value.
            var dst = this.Cache.Span.Slice(this.CacheOffset, 4);
            if (this.TagEndian() == EndianKinds.BigEndian)
            {
                BinaryPrimitives.WriteUInt32BigEndian(dst, data);
            }
            else
            {
                BinaryPrimitives.WriteUInt32LittleEndian(dst, data);
            }
            this.Timestamp = DateTime.Now;
            this.MarkDirty();
        }
    }
}

/// <summary>
/// S7 Int64 组合子
/// </summary>
public class S7Int64TagCbntor : S7TagCbntorBase
{
    /// <summary>
    /// c'tor
    /// </summary>
    /// <param name="tagDescriptor"></param>
    /// <param name="tagCbnt">S7 组合（byte 缓存）</param>
    /// <param name="cacheOffset"></param>
    internal S7Int64TagCbntor(TagDescriptor tagDescriptor, TagCbnt<byte> tagCbnt, int cacheOffset)
        : base(tagDescriptor, tagCbnt, cacheOffset, cacheOffset)
    {
    }

    /// <summary>
    /// 测点值
    /// </summary>
    public override object? Value
    {
        get
        {
            var span = this.Cache.Span.Slice(this.CacheOffset, 8);
            return this.TagEndian() == EndianKinds.BigEndian
                ? BinaryPrimitives.ReadInt64BigEndian(span)
                : BinaryPrimitives.ReadInt64LittleEndian(span);
        }
        set
        {
#pragma warning disable CS8605 // Unboxing a possibly null value.
            var data = (long)value;
#pragma warning restore CS8605 // Unboxing a possibly null value.
            var dst = this.Cache.Span.Slice(this.CacheOffset, 8);
            if (this.TagEndian() == EndianKinds.BigEndian)
            {
                BinaryPrimitives.WriteInt64BigEndian(dst, data);
            }
            else
            {
                BinaryPrimitives.WriteInt64LittleEndian(dst, data);
            }
            this.Timestamp = DateTime.Now;
            this.MarkDirty();
        }
    }
}

/// <summary>
/// S7 UInt64 组合子
/// </summary>
public class S7UInt64TagCbntor : S7TagCbntorBase
{
    /// <summary>
    /// c'tor
    /// </summary>
    /// <param name="tagDescriptor"></param>
    /// <param name="tagCbnt">S7 组合（byte 缓存）</param>
    /// <param name="cacheOffset"></param>
    internal S7UInt64TagCbntor(TagDescriptor tagDescriptor, TagCbnt<byte> tagCbnt, int cacheOffset)
        : base(tagDescriptor, tagCbnt, cacheOffset, cacheOffset)
    {
    }

    /// <summary>
    /// 测点值
    /// </summary>
    public override object? Value
    {
        get
        {
            var span = this.Cache.Span.Slice(this.CacheOffset, 8);
            return this.TagEndian() == EndianKinds.BigEndian
                ? BinaryPrimitives.ReadUInt64BigEndian(span)
                : BinaryPrimitives.ReadUInt64LittleEndian(span);
        }
        set
        {
#pragma warning disable CS8605 // Unboxing a possibly null value.
            var data = (ulong)value;
#pragma warning restore CS8605 // Unboxing a possibly null value.
            var dst = this.Cache.Span.Slice(this.CacheOffset, 8);
            if (this.TagEndian() == EndianKinds.BigEndian)
            {
                BinaryPrimitives.WriteUInt64BigEndian(dst, data);
            }
            else
            {
                BinaryPrimitives.WriteUInt64LittleEndian(dst, data);
            }
            this.Timestamp = DateTime.Now;
            this.MarkDirty();
        }
    }
}
