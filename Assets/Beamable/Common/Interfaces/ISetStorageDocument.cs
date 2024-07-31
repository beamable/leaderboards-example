namespace Beamable.Common.Interfaces
{
    public interface ISetStorageDocument<in T>
    {
        void Set(T document);
    }
}