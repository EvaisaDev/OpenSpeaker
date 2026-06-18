namespace OpenSpeaker.Data;
public interface IRepository<T>
{
    T? Get(object id);
    IEnumerable<T> GetAll();
    void Upsert(T entity);
    bool Delete(object id);
}
