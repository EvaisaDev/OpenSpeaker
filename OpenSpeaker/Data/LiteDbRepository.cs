using LiteDB;
namespace OpenSpeaker.Data;

public class LiteDbRepository<T> : IRepository<T>
{
    protected readonly ILiteCollection<T> _collection;

    public LiteDbRepository(ILiteCollection<T> collection)
    {
        _collection = collection;
    }

    public T? Get(object id) => _collection.FindById(new BsonValue(id));
    public IEnumerable<T> GetAll() => _collection.FindAll();
    public void Upsert(T entity) => _collection.Upsert(entity);
    public bool Delete(object id) => _collection.Delete(new BsonValue(id));
}
