using System;

namespace ClickHouse.Client.Types
{
    public sealed class DbTuple<T1, T2, T3> : Tuple<T1, T2, T3>, IDbTuple
    {
        public DbTuple(T1 item1, T2 item2, T3 item3)
            : base(item1, item2, item3)
        {
        }

        public int Length => 3;

        public object this[int index] => index switch
        {
            0 => Item1,
            1 => Item2,
            2 => Item3,
            _ => throw new IndexOutOfRangeException(),
        };
    }
}
