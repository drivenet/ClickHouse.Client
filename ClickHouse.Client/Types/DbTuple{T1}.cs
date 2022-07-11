using System;

namespace ClickHouse.Client.Types
{
    public sealed class DbTuple<T1> : Tuple<T1>, IDbTuple
    {
        public DbTuple(T1 item1)
            : base(item1)
        {
        }

        public int Length => 1;

        public object this[int index] => index switch
        {
            0 => Item1,
            _ => throw new IndexOutOfRangeException(),
        };
    }
}
