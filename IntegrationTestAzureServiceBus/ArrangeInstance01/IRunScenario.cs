namespace ArrangeInstance01
{
    public interface IScenario
    {
        Task Run();

        bool IsRepeatable { get; }
    }
}
