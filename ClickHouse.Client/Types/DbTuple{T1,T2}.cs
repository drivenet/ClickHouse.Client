using System;

namespace ClickHouse.Client.Types
{
    public sealed class DbTuple<T1, T2> : Tuple<T1, T2>, IDbTuple
    {
        public DbTuple(T1 item1, T2 item2)
            : base(item1, item2)
        {
        }

        public int Length => 2;

        public object this[int index] => index switch
        {
            0 => Item1,
            1 => Item2,
            _ => throw new IndexOutOfRangeException(),
        };
    }
}
