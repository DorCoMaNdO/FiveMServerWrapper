namespace ServerWrapper
{
    public interface IServerScript
    {
        string Name { get; }

        void Load();
        void Unload();

        void Tick();
    }
}
