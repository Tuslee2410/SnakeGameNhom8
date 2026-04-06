namespace SnakeGame
{
    public class Player
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public double X { get; set; }
        public double Y { get; set; }
        public double Angle { get; set; }
        public double Speed { get; set; }
        public int Length { get; set; } = 50;
        public List<Vector2> Body { get; set; } = new List<Vector2>();
        public bool IsAlive { get; set; } = true;
        public string Color { get; set; } = "lime";
    }
}