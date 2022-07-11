namespace ClickHouse.Client.Types
{
    public interface IDbTuple
    {
        int Length { get; }

        object this[int index] { get; }
    }
}
