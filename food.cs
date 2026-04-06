namespace SnakeGame
{
    public class Food
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public double X { get; set; }
        public double Y { get; set; }
        public string Type { get; set; } = "normal"; // normal, mega, magnet, shield, ghost, lucky
        public double Size { get; set; } = 6;
        public string Color { get; set; } = "gold";
        public string? Icon { get; set; }

        public Food(double x, double y)
        {
            X = x;
            Y = y;
        }
    }
}