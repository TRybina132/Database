using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ManagedCode.Database.Core.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ManagedCode.Database.Core;

public abstract class BaseDatabase : IDatabase
{
    protected readonly ILogger Logger = NullLogger.Instance;

    private bool _disposed;

    public bool IsInitialized { get; protected set; }

    public async Task InitializeAsync(CancellationToken token = default)
    {
        if (IsInitialized == false)
        {
            await InitializeAsyncInternal(token);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        
        _disposed = true;
        DisposeInternal();
    }

    public ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return new ValueTask(Task.CompletedTask);
        }

        _disposed = true;
        return DisposeAsyncInternal();
    }

    protected abstract Task InitializeAsyncInternal(CancellationToken token = default);

    protected abstract ValueTask DisposeAsyncInternal();
    protected abstract void DisposeInternal();


    protected abstract IDBCollection<TId, TItem> GetCollectionInternal<TId, TItem>(string name) where TItem : class, IItem<TId>, new();
    public abstract Task Delete(CancellationToken token = default);

    public IDBCollection<TId, TItem> GetCollection<TId, TItem>() where TItem : class, IItem<TId>, new()
    {
        if (!IsInitialized)
        {
            throw new RepositoryNotInitializedException(GetType());
        }
        
        return GetCollectionInternal<TId, TItem>(typeof(TItem).FullName);
    }

    public IDBCollection<TId, TItem> GetCollection<TId, TItem>(string name) where TItem : class, IItem<TId>, new()
    {
        if (!IsInitialized)
        {
            throw new RepositoryNotInitializedException(GetType());
        }
        
        return GetCollectionInternal<TId, TItem>(name);
    }
}