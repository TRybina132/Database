using System.Linq.Expressions;
using ManagedCode.Database.Core;
using Microsoft.Extensions.Logging;
using Tenray.ZoneTree.Options;

namespace ManagedCode.ZoneTree.Cluster.DB;

public class ZoneTreeDBCollection<TId, TItem> : BaseDBCollection<TId, TItem> where TItem : IItem<TId>
{
    private readonly ZoneTreeWrapper<TId, TItem> _zoneTree;
    public ZoneTreeDBCollection(ILogger logger, string path) 
    {
        _zoneTree = new ZoneTreeWrapper<TId, TItem>(logger, path);
        _zoneTree.Open(new ZoneTreeOptions<TId, TItem?>()
        {
            Path = path,
            WALMode = WriteAheadLogMode.Sync,
            DiskSegmentMode = DiskSegmentMode.SingleDiskSegment,
            StorageType = StorageType.File,
            ValueSerializer = new JsonSerializer<TItem?>()
        });
    }
    
    public override void Dispose()
    {
        _zoneTree.Dispose();
    }

    public override ValueTask DisposeAsync()
    {
        _zoneTree.Dispose();
        return ValueTask.CompletedTask;
    }

    protected override Task<TItem> InsertAsyncInternal(TItem item, CancellationToken token = default)
    {
        _zoneTree.Insert(item.Id, item);
        return Task.FromResult(item);
    }

    protected override Task<int> InsertAsyncInternal(IEnumerable<TItem> items, CancellationToken token = default)
    {
        var i = 0;
        foreach (var item in items)
        {
            i++;
            _zoneTree.Insert(item.Id, item);
        }

        return Task.FromResult(i);
    }

    protected override Task<TItem> UpdateAsyncInternal(TItem item, CancellationToken token = default)
    {
        _zoneTree.Update(item.Id, item);
        return Task.FromResult(item);
    }

    protected override Task<int> UpdateAsyncInternal(IEnumerable<TItem> items, CancellationToken token = default)
    {
        var i = 0;
        foreach (var item in items)
        {
            _zoneTree.Update(item.Id, item);
            i++;
        }

        return Task.FromResult(i);
    }

    protected override Task<bool> DeleteAsyncInternal(TId id, CancellationToken token = default)
    {
        _zoneTree.Delete(id);
        return Task.FromResult(true);
    }

    protected override Task<bool> DeleteAsyncInternal(TItem item, CancellationToken token = default)
    {
        _zoneTree.Delete(item.Id);
        return Task.FromResult(true);
    }

    protected override Task<int> DeleteAsyncInternal(IEnumerable<TId> ids, CancellationToken token = default)
    {
        var i = 0;
        foreach (var id in ids)
        {
            _zoneTree.Delete(id);
            i++;
        }

        return Task.FromResult(i);
    }

    protected override Task<int> DeleteAsyncInternal(IEnumerable<TItem> items, CancellationToken token = default)
    {
        var i = 0;
        foreach (var item in items)
        {
            _zoneTree.Delete(item.Id);
            i++;
        }

        return Task.FromResult(i);
    }

    protected override Task<int> DeleteAsyncInternal(Expression<Func<TItem, bool>> predicate, CancellationToken token = default)
    {
        var i = 0;
        var compiled = predicate.Compile();
        foreach (var item in  _zoneTree.Enumerate().Where(compiled))
        {
            _zoneTree.Delete(item.Id);
            i++;
        }
        
        return Task.FromResult(i);
    }

    protected override Task<bool> DeleteAllAsyncInternal(CancellationToken token = default)
    {
        _zoneTree.DeleteAll();
        return Task.FromResult(true);
    }

    protected override Task<TItem> InsertOrUpdateAsyncInternal(TItem item, CancellationToken token = default)
    {
        _zoneTree.Upsert(item.Id, item);
        return Task.FromResult(item);
    }

    protected override Task<int> InsertOrUpdateAsyncInternal(IEnumerable<TItem> items, CancellationToken token = default)
    {
        var i = 0;
        foreach (var item in  items)
        {
            _zoneTree.Upsert(item.Id, item);
            i++;
        }
        
        return Task.FromResult(i);
    }

    protected override Task<TItem> GetAsyncInternal(TId id, CancellationToken token = default)
    {
        return Task.FromResult(_zoneTree.Get(id));
    }

    protected override Task<TItem> GetAsyncInternal(Expression<Func<TItem, bool>> predicate, CancellationToken token = default)
    {
        var compiled = predicate.Compile();
        return Task.FromResult(_zoneTree.Enumerate().FirstOrDefault(compiled));
    }

    protected override async IAsyncEnumerable<TItem> GetAllAsyncInternal(int? take = null, int skip = 0, CancellationToken token = default)
    {
        foreach (var item in _zoneTree.Enumerate().Skip(skip).Take(take ?? int.MaxValue))
        {
            yield return item;
        }
    }

    protected override async IAsyncEnumerable<TItem> GetAllAsyncInternal(Expression<Func<TItem, object>> orderBy, Order orderType, int? take = null, int skip = 0, CancellationToken token = default)
    {
        var compiled = orderBy.Compile();
        foreach (var item in _zoneTree.Enumerate().OrderBy(compiled).Skip(skip).Take(take ?? int.MaxValue))
        {
            yield return item;
        }
    }

    protected override async IAsyncEnumerable<TItem> FindAsyncInternal(IEnumerable<Expression<Func<TItem, bool>>> predicates, int? take = null, int skip = 0, CancellationToken token = default)
    {
        var conditions = predicates.Select(s => s.Compile()).ToArray();
        
        foreach (var item in _zoneTree.Enumerate().Where(w => conditions.All(a=>a.Invoke(w))).Skip(skip).Take(take ?? int.MaxValue))
        {
            yield return item;
        }
    }

    protected override async IAsyncEnumerable<TItem> FindAsyncInternal(IEnumerable<Expression<Func<TItem, bool>>> predicates,
        Expression<Func<TItem, object>> orderBy,
        Order orderType,
        int? take = null,
        int skip = 0,
        CancellationToken token = default)
    {
        var conditions = predicates.Select(s => s.Compile()).ToArray();
        var order = orderBy.Compile();
        
        foreach (var item in _zoneTree.Enumerate().Where(w => conditions.All(a=>a.Invoke(w))).OrderBy(order).Skip(skip).Take(take ?? int.MaxValue))
        {
            yield return item;
        }
    }

    protected override async IAsyncEnumerable<TItem> FindAsyncInternal(IEnumerable<Expression<Func<TItem, bool>>> predicates,
        Expression<Func<TItem, object>> orderBy,
        Order orderType,
        Expression<Func<TItem, object>> thenBy,
        Order thenType,
        int? take = null,
        int skip = 0,
        CancellationToken token = default)
    {
        var conditions = predicates.Select(s => s.Compile()).ToArray();
        var order = orderBy.Compile();
        var then = thenBy.Compile();
        
        foreach (var item in _zoneTree.Enumerate().Where(w => conditions.All(a=>a.Invoke(w))).OrderBy(order).ThenBy(then)
                     .Skip(skip).Take(take ?? int.MaxValue))
        {
            yield return item;
        }
    }

    protected override Task<long> CountAsyncInternal(CancellationToken token = default)
    {
        return Task.FromResult(_zoneTree.Count());
    }

    protected override Task<long> CountAsyncInternal(IEnumerable<Expression<Func<TItem, bool>>> predicates, CancellationToken token = default)
    {
        var conditions = predicates.Select(s => s.Compile()).ToArray();
        return Task.FromResult(_zoneTree.Enumerate().Where(w => conditions.All(a => a.Invoke(w))).LongCount());
    }
}