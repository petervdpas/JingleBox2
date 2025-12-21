namespace JingleBox2.Models;

public sealed class OutputDevice
{
    public int Id { get; }
    public string Name { get; }

    public OutputDevice(int id, string name)
    {
        Id = id;
        Name = name;
    }

    public override string ToString() => Name;
}
