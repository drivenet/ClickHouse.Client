namespace ClickHouse.Client.Types
{
    public sealed class LargeTuple : IDbTuple
    {
        private readonly object[] items;

        public LargeTuple(params object[] items)
        {
            this.items = items;
        }

        public int Length => items.Length;

        public object this[int index] => items[index];
    }
}
