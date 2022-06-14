﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ClickHouse.Client.ADO.Parameters;
using ClickHouse.Client.ADO.Readers;
using ClickHouse.Client.Formats;
using ClickHouse.Client.Utility;

namespace ClickHouse.Client.ADO
{
    public class ClickHouseCommand : DbCommand, IClickHouseCommand, IDisposable
    {
        private readonly CancellationTokenSource cts = new CancellationTokenSource();
        private readonly ClickHouseParameterCollection commandParameters = new ClickHouseParameterCollection();
        private ClickHouseConnection connection;

        public ClickHouseCommand()
        {
        }

        public ClickHouseCommand(ClickHouseConnection connection)
        {
            this.connection = connection;
        }

        public override string CommandText { get; set; }

        public override int CommandTimeout { get; set; }

        public override CommandType CommandType { get; set; }

        public override bool DesignTimeVisible { get; set; }

        public override UpdateRowSource UpdatedRowSource { get; set; }

        /// <summary>
        /// Gets or sets QueryId associated with command
        /// After query execution, will be set by value provided by server
        /// Value will be same if provided or a UUID generated by server if not
        /// </summary>
        public string QueryId { get; set; }

        protected override DbConnection DbConnection
        {
            get => connection;
            set => connection = (ClickHouseConnection)value;
        }

        protected override DbParameterCollection DbParameterCollection => commandParameters;

        protected override DbTransaction DbTransaction { get; set; }

        public override void Cancel() => cts.Cancel();

        public override int ExecuteNonQuery() => ExecuteNonQueryAsync(cts.Token).GetAwaiter().GetResult();

        public override async Task<int> ExecuteNonQueryAsync(CancellationToken cancellationToken)
        {
            if (connection == null)
                throw new InvalidOperationException("Connection is not set");

            using var lcts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, cancellationToken);
            using var response = await PostSqlQueryAsync(CommandText, lcts.Token).ConfigureAwait(false);
            using var reader = new ExtendedBinaryReader(await response.Content.ReadAsStreamAsync().ConfigureAwait(false));

            return reader.PeekChar() != -1 ? reader.Read7BitEncodedInt() : 0;
        }

        /// <summary>
        ///  Allows to return raw result from a query (with custom FORMAT)
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>ClickHouseRawResult object containing response stream</returns>
        public async Task<ClickHouseRawResult> ExecuteRawResultAsync(CancellationToken cancellationToken)
        {
            if (connection == null)
                throw new InvalidOperationException("Connection is not set");

            using var lcts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, cancellationToken);
            var response = await PostSqlQueryAsync(CommandText, lcts.Token).ConfigureAwait(false);
            return new ClickHouseRawResult(response);
        }

        public override object ExecuteScalar() => ExecuteScalarAsync(cts.Token).GetAwaiter().GetResult();

        public override async Task<object> ExecuteScalarAsync(CancellationToken cancellationToken)
        {
            using var lcts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, cancellationToken);
            using var reader = await ExecuteDbDataReaderAsync(CommandBehavior.Default, lcts.Token).ConfigureAwait(false);
            return reader.Read() ? reader.GetValue(0) : null;
        }

        public override void Prepare() { /* ClickHouse has no notion of prepared statements */ }

        public new ClickHouseDbParameter CreateParameter() => new ClickHouseDbParameter();

        protected override DbParameter CreateDbParameter() => CreateParameter();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // Dispose token source but do not cancel
                cts.Dispose();
            }
        }

        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) => ExecuteDbDataReaderAsync(behavior, cts.Token).GetAwaiter().GetResult();

        protected override async Task<DbDataReader> ExecuteDbDataReaderAsync(CommandBehavior behavior, CancellationToken cancellationToken)
        {
            if (connection == null)
                throw new InvalidOperationException("Connection is not set");

            using var lcts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, cancellationToken);
            var sqlBuilder = new StringBuilder(CommandText);
            switch (behavior)
            {
                case CommandBehavior.SingleRow:
                    sqlBuilder.Append(" LIMIT 1");
                    break;
                case CommandBehavior.SchemaOnly:
                    sqlBuilder.Append(" LIMIT 0");
                    break;
                default:
                    break;
            }
            var result = await PostSqlQueryAsync(sqlBuilder.ToString(), lcts.Token).ConfigureAwait(false);
            return new ClickHouseDataReader(result);
        }

        private async Task<HttpResponseMessage> PostSqlQueryAsync(string sqlQuery, CancellationToken token)
        {
            if (connection == null)
                throw new InvalidOperationException("Connection not set");

            var uriBuilder = connection.CreateUriBuilder();
            if (commandParameters != null)
            {
                await connection.EnsureOpenAsync().ConfigureAwait(false); // Preserve old behavior
                if (connection.SupportedFeatures.HasFlag(FeatureFlags.SupportsHttpParameters))
                {
                    foreach (ClickHouseDbParameter parameter in commandParameters)
                        uriBuilder.AddQueryParameter(parameter.ParameterName, HttpParameterFormatter.Format(parameter));
                }
                else
                {
                    var formattedParameters = new Dictionary<string, string>(commandParameters.Count);
                    foreach (ClickHouseDbParameter parameter in commandParameters)
                        formattedParameters.TryAdd(parameter.ParameterName, InlineParameterFormatter.Format(parameter));
                    sqlQuery = SubstituteParameters(sqlQuery, formattedParameters);
                }
            }

            if (!string.IsNullOrEmpty(QueryId))
                uriBuilder.CustomParameters.Add("query_id", QueryId);

            string uri = uriBuilder.ToString();

            using var postMessage = new HttpRequestMessage(HttpMethod.Post, uri);

            connection.AddDefaultHttpHeaders(postMessage.Headers);
            HttpContent content = new StringContent(sqlQuery);
            content.Headers.ContentType = new MediaTypeHeaderValue("text/sql");
            if (connection.UseCompression)
            {
                content = new CompressedContent(content, DecompressionMethods.GZip);
            }

            postMessage.Content = content;

            var response = await connection.HttpClient.SendAsync(postMessage, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
            QueryId = ExtractQueryId(response);
            return await ClickHouseConnection.HandleError(response, sqlQuery).ConfigureAwait(false);
        }

        private string ExtractQueryId(HttpResponseMessage response)
        {
            const string queryIdHeader = "X-ClickHouse-Query-Id";
            if (response.Headers.Contains(queryIdHeader))
                return response.Headers.GetValues(queryIdHeader).FirstOrDefault();
            else
                return null;
        }

        private static string SubstituteParameters(string query, IDictionary<string, string> parameters)
        {
            var builder = new StringBuilder(query.Length);

            var paramStartPos = query.IndexOf('{');
            var paramEndPos = -1;

            while (paramStartPos != -1)
            {
                builder.Append(query.Substring(paramEndPos + 1, paramStartPos - paramEndPos - 1));

                paramStartPos += 1;
                paramEndPos = query.IndexOf('}', paramStartPos);
                var param = query.Substring(paramStartPos, paramEndPos - paramStartPos);
                var delimiterPos = param.LastIndexOf(':');
                if (delimiterPos == -1)
                    throw new NotSupportedException($"param {param} doesn`t have data type");
                var name = param.Substring(0, delimiterPos);

                if (!parameters.TryGetValue(name, out var value))
                    throw new ArgumentOutOfRangeException($"Parameter {name} not found in parameters list");

                builder.Append(value);

                paramStartPos = query.IndexOf('{', paramEndPos);
            }

            builder.Append(query.Substring(paramEndPos + 1, query.Length - paramEndPos - 1));

            return builder.ToString();
        }
    }
}
