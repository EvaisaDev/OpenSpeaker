using System.Linq.Expressions;
using LiteDB;
namespace OpenSpeaker.Data;

internal sealed class SynchronizedLiteCollection<T> : ILiteCollection<T>
{
    private readonly ILiteCollection<T> _inner;
    private readonly object _gate;

    public SynchronizedLiteCollection(ILiteCollection<T> inner, object gate)
    {
        _inner = inner;
        _gate = gate;
    }

    public string Name { get { lock (_gate) return _inner.Name; } }
    public BsonAutoId AutoId { get { lock (_gate) return _inner.AutoId; } }
    public EntityMapper EntityMapper { get { lock (_gate) return _inner.EntityMapper; } }

    public ILiteCollection<T> Include<K>(Expression<Func<T, K>> keySelector)
    {
        lock (_gate) return new SynchronizedLiteCollection<T>(_inner.Include(keySelector), _gate);
    }

    public ILiteCollection<T> Include(BsonExpression keySelector)
    {
        lock (_gate) return new SynchronizedLiteCollection<T>(_inner.Include(keySelector), _gate);
    }

    public bool Upsert(T entity) { lock (_gate) return _inner.Upsert(entity); }
    public int Upsert(IEnumerable<T> entities) { lock (_gate) return _inner.Upsert(entities); }
    public bool Upsert(BsonValue id, T entity) { lock (_gate) return _inner.Upsert(id, entity); }

    public bool Update(T entity) { lock (_gate) return _inner.Update(entity); }
    public bool Update(BsonValue id, T entity) { lock (_gate) return _inner.Update(id, entity); }
    public int Update(IEnumerable<T> entities) { lock (_gate) return _inner.Update(entities); }
    public int UpdateMany(BsonExpression transform, BsonExpression predicate) { lock (_gate) return _inner.UpdateMany(transform, predicate); }
    public int UpdateMany(Expression<Func<T, T>> extend, Expression<Func<T, bool>> predicate) { lock (_gate) return _inner.UpdateMany(extend, predicate); }

    public BsonValue Insert(T entity) { lock (_gate) return _inner.Insert(entity); }
    public void Insert(BsonValue id, T entity) { lock (_gate) _inner.Insert(id, entity); }
    public int Insert(IEnumerable<T> entities) { lock (_gate) return _inner.Insert(entities); }
    public int InsertBulk(IEnumerable<T> entities, int batchSize = 5000) { lock (_gate) return _inner.InsertBulk(entities, batchSize); }

    public bool EnsureIndex(string name, BsonExpression expression, bool unique = false) { lock (_gate) return _inner.EnsureIndex(name, expression, unique); }
    public bool EnsureIndex(BsonExpression expression, bool unique = false) { lock (_gate) return _inner.EnsureIndex(expression, unique); }
    public bool EnsureIndex<K>(Expression<Func<T, K>> keySelector, bool unique = false) { lock (_gate) return _inner.EnsureIndex(keySelector, unique); }
    public bool EnsureIndex<K>(string name, Expression<Func<T, K>> keySelector, bool unique = false) { lock (_gate) return _inner.EnsureIndex(name, keySelector, unique); }
    public bool DropIndex(string name) { lock (_gate) return _inner.DropIndex(name); }

    public ILiteQueryable<T> Query() { lock (_gate) return _inner.Query(); }

    public T FindById(BsonValue id) { lock (_gate) return _inner.FindById(id); }
    public T FindOne(BsonExpression predicate) { lock (_gate) return _inner.FindOne(predicate); }
    public T FindOne(BsonExpression predicate, BsonValue[] args) { lock (_gate) return _inner.FindOne(predicate, args); }
    public T FindOne(string predicate, BsonDocument parameters) { lock (_gate) return _inner.FindOne(predicate, parameters); }
    public T FindOne(Expression<Func<T, bool>> predicate) { lock (_gate) return _inner.FindOne(predicate); }
    public T FindOne(Query query) { lock (_gate) return _inner.FindOne(query); }

    public IEnumerable<T> Find(BsonExpression predicate, int skip = 0, int limit = int.MaxValue) { lock (_gate) return _inner.Find(predicate, skip, limit).ToArray(); }
    public IEnumerable<T> Find(Query query, int skip = 0, int limit = int.MaxValue) { lock (_gate) return _inner.Find(query, skip, limit).ToArray(); }
    public IEnumerable<T> Find(Expression<Func<T, bool>> predicate, int skip = 0, int limit = int.MaxValue) { lock (_gate) return _inner.Find(predicate, skip, limit).ToArray(); }
    public IEnumerable<T> FindAll() { lock (_gate) return _inner.FindAll().ToArray(); }

    public bool Delete(BsonValue id) { lock (_gate) return _inner.Delete(id); }
    public int DeleteAll() { lock (_gate) return _inner.DeleteAll(); }
    public int DeleteMany(BsonExpression predicate) { lock (_gate) return _inner.DeleteMany(predicate); }
    public int DeleteMany(string predicate, BsonDocument parameters) { lock (_gate) return _inner.DeleteMany(predicate, parameters); }
    public int DeleteMany(string predicate, params BsonValue[] args) { lock (_gate) return _inner.DeleteMany(predicate, args); }
    public int DeleteMany(Expression<Func<T, bool>> predicate) { lock (_gate) return _inner.DeleteMany(predicate); }

    public int Count() { lock (_gate) return _inner.Count(); }
    public int Count(BsonExpression predicate) { lock (_gate) return _inner.Count(predicate); }
    public int Count(string predicate, BsonDocument parameters) { lock (_gate) return _inner.Count(predicate, parameters); }
    public int Count(string predicate, params BsonValue[] args) { lock (_gate) return _inner.Count(predicate, args); }
    public int Count(Query query) { lock (_gate) return _inner.Count(query); }
    public int Count(Expression<Func<T, bool>> predicate) { lock (_gate) return _inner.Count(predicate); }

    public long LongCount() { lock (_gate) return _inner.LongCount(); }
    public long LongCount(BsonExpression predicate) { lock (_gate) return _inner.LongCount(predicate); }
    public long LongCount(string predicate, BsonDocument parameters) { lock (_gate) return _inner.LongCount(predicate, parameters); }
    public long LongCount(string predicate, params BsonValue[] args) { lock (_gate) return _inner.LongCount(predicate, args); }
    public long LongCount(Query query) { lock (_gate) return _inner.LongCount(query); }
    public long LongCount(Expression<Func<T, bool>> predicate) { lock (_gate) return _inner.LongCount(predicate); }

    public bool Exists(BsonExpression predicate) { lock (_gate) return _inner.Exists(predicate); }
    public bool Exists(string predicate, BsonDocument parameters) { lock (_gate) return _inner.Exists(predicate, parameters); }
    public bool Exists(string predicate, params BsonValue[] args) { lock (_gate) return _inner.Exists(predicate, args); }
    public bool Exists(Query query) { lock (_gate) return _inner.Exists(query); }
    public bool Exists(Expression<Func<T, bool>> predicate) { lock (_gate) return _inner.Exists(predicate); }

    public BsonValue Min(BsonExpression keySelector) { lock (_gate) return _inner.Min(keySelector); }
    public BsonValue Min() { lock (_gate) return _inner.Min(); }
    public K Min<K>(Expression<Func<T, K>> keySelector) { lock (_gate) return _inner.Min(keySelector); }
    public BsonValue Max(BsonExpression keySelector) { lock (_gate) return _inner.Max(keySelector); }
    public BsonValue Max() { lock (_gate) return _inner.Max(); }
    public K Max<K>(Expression<Func<T, K>> keySelector) { lock (_gate) return _inner.Max(keySelector); }
}
