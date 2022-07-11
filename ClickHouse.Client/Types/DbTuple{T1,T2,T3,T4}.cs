using System;

namespace ClickHouse.Client.Types
{
    public sealed class DbTuple<T1, T2, T3, T4> : Tuple<T1, T2, T3, T4>, IDbTuple
    {
        public DbTuple(T1 item1, T2 item2, T3 item3, T4 item4)
            : base(item1, item2, item3, item4)
        {
        }

        public int Length => 4;

        public object this[int index] => index switch
        {
            0 => Item1,
            1 => Item2,
            2 => Item3,
            3 => Item4,
            _ => throw new IndexOutOfRangeException(),
        };
    }
}
