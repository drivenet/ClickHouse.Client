using System;

namespace ClickHouse.Client.Types
{
    public sealed class DbTuple<T1, T2, T3, T4, T5, T6, T7> : Tuple<T1, T2, T3, T4, T5, T6, T7>, IDbTuple
    {
        public DbTuple(T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6, T7 item7)
            : base(item1, item2, item3, item4, item5, item6, item7)
        {
        }

        public int Length => 7;

        public object this[int index] => index switch
        {
            0 => Item1,
            1 => Item2,
            2 => Item3,
            3 => Item4,
            4 => Item5,
            5 => Item6,
            6 => Item7,
            _ => throw new IndexOutOfRangeException(),
        };
    }
}
