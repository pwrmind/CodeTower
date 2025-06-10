namespace CodeTower;

public class DependencyConflict
{
    private string sourceNs;
    private string dependency;
    private string v;

    public DependencyConflict(string sourceNs, string dependency, string v)
    {
        this.sourceNs = sourceNs;
        this.dependency = dependency;
        this.v = v;
    }
}
