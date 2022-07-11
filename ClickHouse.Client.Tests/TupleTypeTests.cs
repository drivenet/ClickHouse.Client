using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using ClickHouse.Client.Types;
using ClickHouse.Client.Utility;

using NUnit.Framework;

namespace ClickHouse.Client.Tests
{
    public class TupleTypeTests : AbstractConnectionTestFixture
    {
        [Test]
        public async Task ShouldSelectTuple([Range(1, 24, 4)] int count)
        {
            var items = string.Join(",", Enumerable.Range(1, count));
            var result = await connection.ExecuteScalarAsync($"select tuple({items})");
            Assert.IsInstanceOf<IDbTuple>(result);
            var tuple = result as IDbTuple;
            Assert.AreEqual(count, tuple.Length);
            CollectionAssert.AreEqual(Enumerable.Range(1, count), AsEnumerable(tuple));
        }

        private static IEnumerable<object> AsEnumerable(IDbTuple tuple) => Enumerable.Range(0, tuple.Length).Select(i => tuple[i]);
    }
}
