using System.Collections.ObjectModel;

namespace ServerWrapper
{
    public interface IServerScript
    {
        string Name { get; }
        string TypeName { get; }
        ReadOnlyCollection<string> Dependencies { get; }

        void Load();

        void Unload();

        void Tick();
    }
}