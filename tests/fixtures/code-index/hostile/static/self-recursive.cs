namespace Hostile;

public class Overloads
{
    public void Same() { Same(); Same(1); Same(1, 2); }
    public void Same(int a) { Same(); }
    public void Same(int a, int b) { Same(a); }
}
