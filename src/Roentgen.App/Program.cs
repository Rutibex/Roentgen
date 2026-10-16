namespace Roentgen.App;

public static class Program
{
    [STAThread]
    public static void Main()
    {
        using var game = new RoentgenGame();
        game.Run();
    }
}
