using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using ManagedCode.Database.Core;
using Microsoft.Azure.Cosmos.Table;

namespace ManagedCode.Database.AzureTable;

public class AzureTableDBCollection : BaseDatabase, IDatabase<CloudTableClient>
{
    private readonly AzureTableRepositoryOptions _options;
    private Dictionary<string, object> tableAdapters = new();
    public AzureTableDBCollection(AzureTableRepositoryOptions options)
    {
        _options = options;
        IsInitialized = true;
    }
    protected override Task InitializeAsyncInternal(CancellationToken token = default)
    {
        return Task.CompletedTask;
    }
    
    protected override ValueTask DisposeAsyncInternal()
    {
        DisposeInternal();
        return new ValueTask(Task.CompletedTask);
    }

    protected override void DisposeInternal()
    {
        tableAdapters.Clear();
    }

    protected override IDBCollection<TId, TItem> GetCollectionInternal<TId, TItem>(string name)
    {
        lock (tableAdapters)
        {
            if (tableAdapters.TryGetValue(name, out var table))
            {
                return (IDBCollection<TId, TItem>)table;
            }

            AzureTableAdapter<TItem> tableAdapter;
            if (string.IsNullOrEmpty(_options.ConnectionString))
            {
                tableAdapter = new AzureTableAdapter<TItem>(Logger, _options.TableStorageCredentials, _options.TableStorageUri);
            }
            else
            {
                tableAdapter = new AzureTableAdapter<TItem>(Logger, _options.ConnectionString);
            }
            
            tableAdapters[name] = new AzureTableDBCollection<TItem>(tableAdapter);
            return tableAdapter;
        }
        
        
    }

    public override Task Delete(CancellationToken token = default)
    {
        throw new NotImplementedException();
    }

    public CloudTableClient DataBase { get; }
}

public class AzureTableDBCollection<TItem> : BaseDBCollection<TableId, TItem>
    where TItem : class, IItem<TableId>, ITableEntity, new()
{
    private readonly AzureTableAdapter<TItem> _tableAdapter;

    public AzureTableDBCollection(AzureTableAdapter<TItem> tableAdapter)
    {
        _tableAdapter = tableAdapter;
    }
    
    public override void Dispose()
    {
    }

    public override ValueTask DisposeAsync()
    {
        return new ValueTask(Task.CompletedTask);
    }

    #region Insert
    
    protected override async Task<TItem> InsertAsyncInternal(TItem item, CancellationToken token = default)
    {
        var result = await _tableAdapter.ExecuteAsync(TableOperation.Insert(item), token);
        return result as TItem;
    }

    protected override async Task<int> InsertAsyncInternal(IEnumerable<TItem> items, CancellationToken token = default)
    {
        return await _tableAdapter.ExecuteBatchAsync(items.Select(s => TableOperation.Insert(s)), token);
    }

    #endregion

    #region InsertOrUpdate

    protected override async Task<TItem> InsertOrUpdateAsyncInternal(TItem item, CancellationToken token = default)
    {
        var result = await _tableAdapter.ExecuteAsync<TItem>(TableOperation.InsertOrReplace(item), token);
        return result;
    }

    protected override async Task<int> InsertOrUpdateAsyncInternal(IEnumerable<TItem> items, CancellationToken token = default)
    {
        return await _tableAdapter.ExecuteBatchAsync(items.Select(s => TableOperation.InsertOrReplace(s)), token);
    }

    #endregion

    #region Update

    protected override async Task<TItem> UpdateAsyncInternal(TItem item, CancellationToken token = default)
    {
        if (string.IsNullOrEmpty(item.ETag))
        {
            item.ETag = "*";
        }

        var result = await _tableAdapter.ExecuteAsync<TItem>(TableOperation.Replace(item), token);
        return result;
    }

    protected override async Task<int> UpdateAsyncInternal(IEnumerable<TItem> items, CancellationToken token = default)
    {
        return await _tableAdapter.ExecuteBatchAsync(items.Select(s =>
        {
            if (string.IsNullOrEmpty(s.ETag))
            {
                s.ETag = "*";
            }

            return TableOperation.Replace(s);
        }), token);
    }

    #endregion

    #region Delete

    protected override async Task<bool> DeleteAsyncInternal(TableId id, CancellationToken token = default)
    {
        var result = await _tableAdapter.ExecuteAsync(TableOperation.Delete(new DynamicTableEntity(id.PartitionKey, id.RowKey)
        {
            ETag = "*"
        }), token);
        return result != null;
    }

    protected override async Task<bool> DeleteAsyncInternal(TItem item, CancellationToken token = default)
    {
        var result = await _tableAdapter.ExecuteAsync(TableOperation.Delete(new DynamicTableEntity(item.PartitionKey, item.RowKey)
        {
            ETag = "*"
        }), token);
        return result != null;
    }

    protected override async Task<int> DeleteAsyncInternal(IEnumerable<TableId> ids, CancellationToken token = default)
    {
        return await _tableAdapter.ExecuteBatchAsync(ids.Select(s => TableOperation.Delete(new DynamicTableEntity(s.PartitionKey, s.RowKey)
        {
            ETag = "*"
        })), token);
    }

    protected override async Task<int> DeleteAsyncInternal(IEnumerable<TItem> items, CancellationToken token = default)
    {
        return await _tableAdapter.ExecuteBatchAsync(items.Select(s => TableOperation.Delete(new DynamicTableEntity(s.PartitionKey, s.RowKey)
        {
            ETag = "*"
        })), token);
    }

    protected override async Task<int> DeleteAsyncInternal(Expression<Func<TItem, bool>> predicate, CancellationToken token = default)
    {
        var count = 0;
        var totalCount = 0;

        do
        {
            token.ThrowIfCancellationRequested();
            var ids = await _tableAdapter
                .Query<DynamicTableEntity>(new[] { predicate }, selectExpression: item => new DynamicTableEntity(item.PartitionKey, item.RowKey),
                    take: _tableAdapter.BatchSize, cancellationToken: token)
                .ToListAsync(token);

            count = ids.Count;

            token.ThrowIfCancellationRequested();
            totalCount += await _tableAdapter.ExecuteBatchAsync(ids.Select(s => TableOperation.Delete(new DynamicTableEntity(s.PartitionKey, s.RowKey)
            {
                ETag = "*"
            })), token);
        } while (count > 0);

        return totalCount;
    }

    protected override async Task<bool> DeleteAllAsyncInternal(CancellationToken token = default)
    {
        //return _tableAdapter.DropTable(token);
        return await DeleteAsyncInternal(item => true, token) > 0;
    }

    #endregion

    #region Get

    protected override Task<TItem> GetAsyncInternal(TableId id, CancellationToken token = default)
    {
        return _tableAdapter.ExecuteAsync<TItem>(TableOperation.Retrieve<TItem>(id.PartitionKey, id.RowKey), token);
    }

    protected override async Task<TItem> GetAsyncInternal(Expression<Func<TItem, bool>> predicate, CancellationToken token = default)
    {
        var item = await _tableAdapter.Query<TItem>(new[] { predicate }, take: 1, cancellationToken: token).ToListAsync(token);
        return item.FirstOrDefault();
    }

    protected override IAsyncEnumerable<TItem> GetAllAsyncInternal(int? take = null, int skip = 0, CancellationToken token = default)
    {
        return _tableAdapter.Query<TItem>(null, take: take, skip: skip, cancellationToken: token);
    }

    protected override IAsyncEnumerable<TItem> GetAllAsyncInternal(Expression<Func<TItem, object>> orderBy,
        Order orderType,
        int? take = null,
        int skip = 0,
        CancellationToken token = default)
    {
        return _tableAdapter.Query(null, orderBy, orderType, take: take, skip: skip, cancellationToken: token);
    }

    #endregion

    #region Find

    protected override IAsyncEnumerable<TItem> FindAsyncInternal(IEnumerable<Expression<Func<TItem, bool>>> predicates,
        int? take = null,
        int skip = 0,
        CancellationToken token = default)
    {
        return _tableAdapter.Query<TItem>(predicates, take: take, skip: skip, cancellationToken: token);
    }

    protected override IAsyncEnumerable<TItem> FindAsyncInternal(IEnumerable<Expression<Func<TItem, bool>>> predicates,
        Expression<Func<TItem, object>> orderBy,
        Order orderType,
        int? take = null,
        int skip = 0,
        CancellationToken token = default)
    {
        return _tableAdapter.Query(predicates, orderBy, orderType, take: take, skip: skip, cancellationToken: token);
    }

    protected override IAsyncEnumerable<TItem> FindAsyncInternal(IEnumerable<Expression<Func<TItem, bool>>> predicates,
        Expression<Func<TItem, object>> orderBy,
        Order orderType,
        Expression<Func<TItem, object>> thenBy,
        Order thenType,
        int? take = null,
        int skip = 0,
        CancellationToken token = default)
    {
        return _tableAdapter.Query(predicates, orderBy, orderType, thenBy, thenType, take: take, skip: skip);
    }

    #endregion

    #region Count

    protected override async Task<int> CountAsyncInternal(CancellationToken token = default)
    {
        var count = 0;

        Expression<Func<TItem, bool>> predicate = item => true;

        await foreach (var item in _tableAdapter
                           .Query<DynamicTableEntity>(new[] { predicate }, selectExpression: item => new DynamicTableEntity(item.PartitionKey, item.RowKey),
                               cancellationToken: token))
        {
            count++;
        }

        return count;
    }

    protected override async Task<int> CountAsyncInternal(IEnumerable<Expression<Func<TItem, bool>>> predicates, CancellationToken token = default)
    {
        var count = 0;

        await foreach (var item in _tableAdapter
                           .Query<DynamicTableEntity>(predicates, selectExpression: item => new DynamicTableEntity(item.PartitionKey, item.RowKey),
                               cancellationToken: token))
        {
            count++;
        }

        return count;
    }

    #endregion
}